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

        // Diagnostic property naming the namespace of the EF extension methods (AsNoTracking, ToListAsync, ...) so code fixes can import it
        public const string EFNamespaceProperty = "EFNamespace";
        public const string AsyncVariantProperty = "AsyncVariant";

        private static readonly ImmutableHashSet<string> TrackingQueryMethods = ImmutableHashSet.Create(
            "ToList", "ToArray", "First", "FirstOrDefault", "Single", "SingleOrDefault",
            "ToListAsync", "ToArrayAsync", "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync");

        private static readonly ImmutableHashSet<string> EntityChangingMethods = ImmutableHashSet.Create(
            "SaveChanges", "SaveChangesAsync", "Attach", "AttachRange", "Update", "UpdateRange", "Remove", "RemoveRange", "Entry");

        private static readonly ImmutableHashSet<string> MaterializingMethods = ImmutableHashSet.Create("ToList", "ToArray");

        private static readonly ImmutableHashSet<string> InMemoryOperators = ImmutableHashSet.Create("Where", "Skip", "Take");

        // Only methods with an async counterpart in both EF Core and EF6
        private static readonly ImmutableDictionary<string, string> AsyncVariants = new Dictionary<string, string>
        {
            { "ToList", "ToListAsync" },
            { "ToArray", "ToArrayAsync" },
            { "First", "FirstAsync" },
            { "FirstOrDefault", "FirstOrDefaultAsync" },
            { "Single", "SingleAsync" },
            { "SingleOrDefault", "SingleOrDefaultAsync" },
            { "Count", "CountAsync" },
            { "Any", "AnyAsync" },
            { "All", "AllAsync" },
        }.ToImmutableDictionary();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.SupportedDiagnostics;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeQueryableVariable, SyntaxKind.LocalDeclarationStatement);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var queryable = compilationContext.Compilation.GetTypeByMetadataName("System.Linq.IQueryable");
                if (queryable == null)
                    return;

                compilationContext.RegisterSyntaxNodeAction(c => AnalyzeForClientSideEvaluation(c, queryable), SyntaxKind.InvocationExpression);

                // EFPERF002 and EFPERF005 suggest EF extension methods, so they only apply when EF Core or EF6 is referenced
                var efExtensions = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")
                    ?? compilationContext.Compilation.GetTypeByMetadataName("System.Data.Entity.QueryableExtensions");
                if (efExtensions == null)
                    return;

                var efNamespace = efExtensions.ContainingNamespace.ToDisplayString();
                compilationContext.RegisterSyntaxNodeAction(c => AnalyzeForAsNoTracking(c, queryable, efNamespace), SyntaxKind.InvocationExpression);
                compilationContext.RegisterSyntaxNodeAction(c => AnalyzeForSyncQueries(c, queryable, efNamespace), SyntaxKind.InvocationExpression);
            });
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

        // EFPERF002: change tracking only pays off when the loaded entities get changed and saved
        private static void AnalyzeForAsNoTracking(SyntaxNodeAnalysisContext context, INamedTypeSymbol queryable, string efNamespace)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!IsQueryOperator(context.SemanticModel, invocation, queryable, out var memberAccess) || !TrackingQueryMethods.Contains(memberAccess.Name.Identifier.Text))
                return;

            // Projections are not tracked, and an explicit AsNoTracking/AsTracking is a decision already made
            if (QueryOperatorNames(context.SemanticModel, memberAccess.Expression).Any(name => name == "Select" || name == "AsTracking" || name.StartsWith("AsNoTracking")))
                return;

            var member = invocation.FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (member == null || MayChangeEntities(member) || ResultEscapes(context.SemanticModel, invocation, member))
                return;

            var properties = ImmutableDictionary<string, string>.Empty.Add(EFNamespaceProperty, efNamespace);
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF002, memberAccess.Name.GetLocation(), properties, memberAccess.Name.Identifier.Text));
        }

        // EFPERF004: Where/Skip/Take after ToList/ToArray run in memory over every row the query loaded
        private static void AnalyzeForClientSideEvaluation(SyntaxNodeAnalysisContext context, INamedTypeSymbol queryable)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess) || !InMemoryOperators.Contains(memberAccess.Name.Identifier.Text))
                return;

            if (!(memberAccess.Expression is InvocationExpressionSyntax materialization)
                || !IsQueryOperator(context.SemanticModel, materialization, queryable, out var materializationAccess)
                || !MaterializingMethods.Contains(materializationAccess.Name.Identifier.Text))
                return;

            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (methodSymbol?.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
                return;

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF004, memberAccess.Name.GetLocation(), memberAccess.Name.Identifier.Text, materializationAccess.Name.Identifier.Text));
        }

        // EFPERF005: a synchronous query blocks the thread of an async function
        private static void AnalyzeForSyncQueries(SyntaxNodeAnalysisContext context, INamedTypeSymbol queryable, string efNamespace)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!IsQueryOperator(context.SemanticModel, invocation, queryable, out var memberAccess) || !AsyncVariants.TryGetValue(memberAccess.Name.Identifier.Text, out var asyncVariant))
                return;

            // The innermost function decides whether await is possible: a lambda inside an async method may be sync or an expression tree
            if (!(context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart, context.CancellationToken) is IMethodSymbol function) || !function.IsAsync)
                return;

            var properties = ImmutableDictionary<string, string>.Empty
                .Add(EFNamespaceProperty, efNamespace)
                .Add(AsyncVariantProperty, asyncVariant);
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFPERF005, memberAccess.Name.GetLocation(), properties, memberAccess.Name.Identifier.Text, asyncVariant));
        }

        // True for receiver.Method(...) where Method is an extension method and receiver is a query, so the call composes or runs it
        private static bool IsQueryOperator(SemanticModel semanticModel, InvocationExpressionSyntax invocation, INamedTypeSymbol queryable, out MemberAccessExpressionSyntax memberAccess)
        {
            memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return false;

            if (!(semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method) || method.MethodKind != MethodKind.ReducedExtension)
                return false;

            var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
            return receiverType != null && (SymbolEqualityComparer.Default.Equals(receiverType, queryable) || receiverType.AllInterfaces.Contains(queryable, SymbolEqualityComparer.Default));
        }

        // Names of the chained calls that built a query, outermost first, following locals back to their initializer
        private static IEnumerable<string> QueryOperatorNames(SemanticModel semanticModel, ExpressionSyntax expression)
        {
            var visitedLocals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
            while (true)
            {
                if (expression is InvocationExpressionSyntax invocation && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    yield return memberAccess.Name.Identifier.Text;
                    expression = memberAccess.Expression;
                }
                else if (semanticModel.GetSymbolInfo(expression).Symbol is ILocalSymbol local
                    && visitedLocals.Add(local)
                    && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer: { } initializer })
                {
                    expression = initializer.Value;
                }
                else
                {
                    yield break;
                }
            }
        }

        // Deliberately broad: any saved, attached or removed entity, or any written member, keeps tracking useful
        private static bool MayChangeEntities(MemberDeclarationSyntax member)
        {
            foreach (var node in member.DescendantNodes())
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax memberAccess && EntityChangingMethods.Contains(memberAccess.Name.Identifier.Text):
                    case AssignmentExpressionSyntax assignment when assignment.Left is MemberAccessExpressionSyntax || assignment.Left is ElementAccessExpressionSyntax:
                    case PrefixUnaryExpressionSyntax prefix when prefix.Operand is MemberAccessExpressionSyntax:
                    case PostfixUnaryExpressionSyntax postfix when postfix.Operand is MemberAccessExpressionSyntax:
                        return true;
                }
            }

            return false;
        }

        // True when the loaded entities can outlive the member, where a caller could change and save them
        private static bool ResultEscapes(SemanticModel semanticModel, ExpressionSyntax query, MemberDeclarationSyntax member)
        {
            SyntaxNode node = query;
            while (node.Parent is AwaitExpressionSyntax || node.Parent is ParenthesizedExpressionSyntax)
                node = node.Parent;

            switch (node.Parent)
            {
                case ReturnStatementSyntax _:
                case YieldStatementSyntax _:
                case ArrowExpressionClauseSyntax _:
                case AnonymousFunctionExpressionSyntax _:
                    return true;
                case AssignmentExpressionSyntax assignment when assignment.Right == node:
                    return !(semanticModel.GetSymbolInfo(assignment.Left).Symbol is ILocalSymbol local) || IsReturned(local, member);
                case EqualsValueClauseSyntax equalsValue when equalsValue.Parent is VariableDeclaratorSyntax declarator:
                    return !(semanticModel.GetDeclaredSymbol(declarator) is ILocalSymbol declaredLocal) || IsReturned(declaredLocal, member);
                default:
                    return false;
            }
        }

        private static bool IsReturned(ILocalSymbol local, MemberDeclarationSyntax member)
        {
            return member.DescendantNodes()
                .Select(node => (node as ReturnStatementSyntax)?.Expression ?? (node as YieldStatementSyntax)?.Expression ?? (node as ArrowExpressionClauseSyntax)?.Expression)
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.Text == local.Name);
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
