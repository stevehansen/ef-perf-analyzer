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
