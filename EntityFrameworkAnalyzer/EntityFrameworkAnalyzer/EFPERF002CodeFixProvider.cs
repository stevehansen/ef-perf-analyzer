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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EFPERF002CodeFixProvider)), Shared]
    public class EFPERF002CodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add AsNoTracking()";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Diagnostics.EFPERF002.Id);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            context.RegisterCodeFix(CodeAction.Create(Title, c => AddAsNoTrackingAsync(context.Document, diagnostic, c), Title), diagnostic);
            return Task.FromResult(0);
        }

        private static async Task<Document> AddAsNoTrackingAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            // Create AsNoTracking() call
            var asNoTrackingExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                memberAccess.Expression,
                SyntaxFactory.IdentifierName("AsNoTracking"));

            var asNoTrackingInvocation = SyntaxFactory.InvocationExpression(
                asNoTrackingExpression,
                SyntaxFactory.ArgumentList());

            // Replace the expression before the terminal method with the AsNoTracking call
            var newMemberAccess = memberAccess.WithExpression(asNoTrackingInvocation);
            var newInvocation = invocation.WithExpression(newMemberAccess);

            var newRoot = root.ReplaceNode(invocation, newInvocation);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
