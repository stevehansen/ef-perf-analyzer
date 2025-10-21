using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EntityFrameworkAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EFPerfAnalyzer : DiagnosticAnalyzer
    {
        // TODO: Project nested member access (var companyName = person.Company.Name)
        // TODO: Convert binary member access to .Any()

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.SupportedDiagnostics;

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeQueryableVariable, SyntaxKind.LocalDeclarationStatement);
            context.RegisterSyntaxNodeAction(AnalyzeForAsNoTracking, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeForClientSideEvaluation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeForSyncQueries, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeQueryableVariable(SyntaxNodeAnalysisContext context)
        {
            var localDeclaration = (LocalDeclarationStatementSyntax)context.Node;
            if (localDeclaration.IsConst)
                return;

            var declaration = localDeclaration.Declaration;
            if (declaration.Variables.Count != 1 || !declaration.Type.IsVar) // TODO: EFPERF002 as information for replacing type with var
                return;

            var declarator = declaration.Variables[0];
            var init = declarator.Initializer?.Value;
            if (init is InvocationExpressionSyntax invocation && invocation.ArgumentList.Arguments.Count <= 1 && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var name = memberAccess.Name.Identifier.Text;
                if (name != "First" && name != "FirstOrDefault" && name != "Single" && name != "SingleOrDefault")
                    return;

                bool IsNameofExpression(SyntaxNode parent)
                {
                    return parent.Parent is ArgumentSyntax arg
                        && arg.Parent.Parent is InvocationExpressionSyntax invExprAsArgument
                        && invExprAsArgument.Expression is IdentifierNameSyntax nameExpr
                        && nameExpr.Identifier.Text == "nameof";
                }

                bool IsCollectionMemberMethodExpression(SyntaxNode parent)
                {
                    if (parent.Parent is MemberAccessExpressionSyntax memberAccessExpr && memberAccessExpr.Parent is InvocationExpressionSyntax memberInvExpr)
                    {
                        var memberMethodSymbol = context.SemanticModel.GetSymbolInfo(memberInvExpr.Expression).Symbol as IMethodSymbol;
                        return memberMethodSymbol?.Name == "Add" || memberMethodSymbol?.Name == "Remove";
                    }

                    return false;
                }

                var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
                if (methodSymbol?.ContainingType.Name == "Queryable" && methodSymbol.TypeArguments.Length == 1 && !methodSymbol.TypeArguments[0].IsAnonymousType)
                {
                    var variableSymbol = context.SemanticModel.GetDeclaredSymbol(declarator);

                    var scope = declarator.Parent.Parent.Parent;
                    var tokens = scope
                        .DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Where(n => n.Identifier.Text == variableSymbol.Name)
                        .Select(n =>
                        {
                            var parent = n.Parent;
                            if (parent is MemberAccessExpressionSyntax memberAccessExpr)
                            {
                                string type;
                                if (parent.Parent is InvocationExpressionSyntax)
                                    type = "MethodAccess";
                                else if (parent.Parent is AssignmentExpressionSyntax assExpr && assExpr.Left == parent)
                                    type = "MemberAssignment";
                                else if (parent.Parent is PostfixUnaryExpressionSyntax || parent.Parent is PrefixUnaryExpressionSyntax)
                                    type = "MemberUnary";
                                else if (IsNameofExpression(parent))
                                    type = "Ignore";
                                else if (IsCollectionMemberMethodExpression(parent))
                                    type = "MemberMethodAccess";
                                else
                                    type = "MemberAccess";
                                return new { Parent = parent, Identifier = n, Type = type, Name = memberAccessExpr.Name.Identifier.Text };
                            }

                            if (parent is BinaryExpressionSyntax binExpr && binExpr.Right is LiteralExpressionSyntax litExpr && litExpr.Token.Value == null)
                                return new { Parent = parent, Identifier = n, Type = "Ignore", Name = default(string) };

                            if (parent is ConditionalAccessExpressionSyntax conditionalAccessExpr && conditionalAccessExpr.WhenNotNull is MemberBindingExpressionSyntax memberBindingExpr)
                                return new { Parent = parent, Identifier = n, Type = "MemberAccess", Name = memberBindingExpr.Name.Identifier.Text };

                            return new { Parent = parent, Identifier = n, Type = "Unknown", Name = default(string) };
                        })
                        .Where(t => t.Type != "Ignore")
                        .ToArray();

                    if (tokens.Length > 0)
                    {
                        if (tokens.All(t => t.Type == "MemberAccess"))
                        {
                            // Only properties are used this might be a candidate for projection
                            var names = tokens.Select(t => t.Name).Distinct().ToArray();
                            var properties = new Dictionary<string, string>
                            {
                                { "Members", string.Join("\n", names) }
                            }.ToImmutableDictionary();
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF001, declarator.GetLocation(), properties, variableSymbol.Name, string.Join(", ", names)));
                            return;
                        }

                        // TODO: Check for usage as method argument
                    }
                }
            }
        }

        // EFPERF002: Check for missing AsNoTracking on read-only queries
        private static void AnalyzeForAsNoTracking(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                // Check if it's a terminal query method
                if (methodName == "ToList" || methodName == "ToArray" || methodName == "FirstOrDefault" ||
                    methodName == "First" || methodName == "SingleOrDefault" || methodName == "Single")
                {
                    var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
                    if (methodSymbol?.ContainingType.Name == "Queryable" ||
                        methodSymbol?.ContainingType.Name == "EntityFrameworkQueryableExtensions")
                    {
                        // Walk up the expression chain to check if AsNoTracking is already called
                        var current = memberAccess.Expression;
                        var hasAsNoTracking = false;

                        while (current != null)
                        {
                            if (current is InvocationExpressionSyntax innerInvocation &&
                                innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess &&
                                innerMemberAccess.Name.Identifier.Text == "AsNoTracking")
                            {
                                hasAsNoTracking = true;
                                break;
                            }

                            current = (current as MemberAccessExpressionSyntax)?.Expression ??
                                     (current as InvocationExpressionSyntax)?.Expression;
                        }

                        if (!hasAsNoTracking)
                        {
                            // Check if the result is modified (not read-only)
                            var isReadOnly = IsQueryReadOnly(context, invocation);
                            if (isReadOnly)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF002, invocation.GetLocation(), methodName));
                            }
                        }
                    }
                }
            }
        }

        // EFPERF004: Check for client-side evaluation (filtering after ToList/ToArray)
        private static void AnalyzeForClientSideEvaluation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                // Check if it's a LINQ method that should be done server-side
                if (methodName == "Where" || methodName == "Select" || methodName == "OrderBy" ||
                    methodName == "OrderByDescending" || methodName == "Skip" || methodName == "Take")
                {
                    var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
                    if (methodSymbol?.ContainingType.Name == "Enumerable")
                    {
                        // Check if the previous operation was ToList or ToArray
                        var current = memberAccess.Expression;
                        if (current is InvocationExpressionSyntax prevInvocation &&
                            prevInvocation.Expression is MemberAccessExpressionSyntax prevMemberAccess)
                        {
                            var prevMethodName = prevMemberAccess.Name.Identifier.Text;
                            if (prevMethodName == "ToList" || prevMethodName == "ToArray")
                            {
                                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF004, invocation.GetLocation(), methodName));
                            }
                        }
                    }
                }
            }
        }

        // EFPERF005: Check for synchronous query methods that should be async
        private static void AnalyzeForSyncQueries(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                // Check if it's a synchronous terminal query method
                var asyncVariant = GetAsyncVariant(methodName);
                if (asyncVariant != null)
                {
                    var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
                    if (methodSymbol?.ContainingType.Name == "Queryable" ||
                        methodSymbol?.ContainingType.Name == "EntityFrameworkQueryableExtensions")
                    {
                        // Check if we're in an async method context
                        var enclosingMethod = context.Node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                        if (enclosingMethod?.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)) == true)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF005, invocation.GetLocation(), methodName, asyncVariant));
                        }
                    }
                }
            }
        }

        private static bool IsQueryReadOnly(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            // Simple heuristic: if the variable is not assigned to or passed to a modification method, it's read-only
            // This is a simplified check - in production, you'd want more sophisticated analysis
            var parent = invocation.Parent;
            while (parent != null)
            {
                if (parent is AssignmentExpressionSyntax assignment && assignment.Right == invocation)
                {
                    // Check if the assigned variable is later modified
                    return true; // Simplified - assume read-only for now
                }
                parent = parent.Parent;
            }
            return true;
        }

        private static string GetAsyncVariant(string methodName)
        {
            switch (methodName)
            {
                case "ToList": return "ToListAsync";
                case "ToArray": return "ToArrayAsync";
                case "FirstOrDefault": return "FirstOrDefaultAsync";
                case "First": return "FirstAsync";
                case "SingleOrDefault": return "SingleOrDefaultAsync";
                case "Single": return "SingleAsync";
                case "Count": return "CountAsync";
                case "Any": return "AnyAsync";
                case "All": return "AllAsync";
                default: return null;
            }
        }

        [Conditional("IGNORE")]
        private static void Test()
        {
            // Is only used for the Syntax Visualizer extension
            IQueryable<AnalyzerOptions> query = null;

            var entity = query.FirstOrDefault(o => o.AdditionalFiles != null);
            var name = entity.AdditionalFiles;

            var fixedCall = query.Select(it => new { it.AdditionalFiles }).FirstOrDefault(o => o.AdditionalFiles != null);
            var fixedName = fixedCall.AdditionalFiles;
        }
    }
}
