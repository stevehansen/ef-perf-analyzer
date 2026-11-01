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

            // The diagnostic is on the name of the method that runs the query
            var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
            var query = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;

            // query.ToList() => query.AsNoTracking().ToList()
            var asNoTracking = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, query.WithoutTrailingTrivia(), SyntaxFactory.IdentifierName("AsNoTracking")))
                .WithTrailingTrivia(query.GetTrailingTrivia());

            var newRoot = root.ReplaceNode(query, asNoTracking);
            newRoot = UsingDirectives.Ensure(newRoot, diagnostic.Properties[EFPerfAnalyzer.EFNamespaceProperty]);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
