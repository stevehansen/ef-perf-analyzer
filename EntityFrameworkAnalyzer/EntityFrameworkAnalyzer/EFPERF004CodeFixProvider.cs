using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EFPERF004CodeFixProvider)), Shared]
    public class EFPERF004CodeFixProvider : CodeFixProvider
    {
        private const string Title = "Move filtering before materialization";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Diagnostics.EFPERF004.Id);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            context.RegisterCodeFix(CodeAction.Create(Title, c => MoveFilterBeforeMaterializationAsync(context.Document, diagnostic, c), Title), diagnostic);
            return Task.FromResult(0);
        }

        private static async Task<Document> MoveFilterBeforeMaterializationAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the filter operation (e.g., Where, Select)
            var filterInvocation = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
            var filterMemberAccess = (MemberAccessExpressionSyntax)filterInvocation.Expression;

            // Find the ToList/ToArray call
            var materializeInvocation = filterMemberAccess.Expression as InvocationExpressionSyntax;
            if (materializeInvocation != null)
            {
                var materializeMemberAccess = (MemberAccessExpressionSyntax)materializeInvocation.Expression;

                // Reconstruct: query.Where(...).ToList() instead of query.ToList().Where(...)
                var newFilterMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    materializeMemberAccess.Expression,
                    filterMemberAccess.Name);

                var newFilterInvocation = SyntaxFactory.InvocationExpression(
                    newFilterMemberAccess,
                    filterInvocation.ArgumentList);

                var newMaterializeMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newFilterInvocation,
                    materializeMemberAccess.Name);

                var newMaterializeInvocation = SyntaxFactory.InvocationExpression(
                    newMaterializeMemberAccess,
                    materializeInvocation.ArgumentList);

                var newRoot = root.ReplaceNode(filterInvocation, newMaterializeInvocation);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
