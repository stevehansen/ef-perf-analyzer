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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EFPERF005CodeFixProvider)), Shared]
    public class EFPERF005CodeFixProvider : CodeFixProvider
    {
        private const string Title = "Use async variant";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Diagnostics.EFPERF005.Id);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            context.RegisterCodeFix(CodeAction.Create(Title, c => ConvertToAsyncAsync(context.Document, diagnostic, c), Title), diagnostic);
            return Task.FromResult(0);
        }

        private static async Task<Document> ConvertToAsyncAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            // Get the async method name
            var syncMethodName = memberAccess.Name.Identifier.Text;
            var asyncMethodName = GetAsyncMethodName(syncMethodName);

            if (asyncMethodName != null)
            {
                // Replace the method name with async variant
                var newName = SyntaxFactory.IdentifierName(asyncMethodName);
                var newMemberAccess = memberAccess.WithName(newName);
                var newInvocation = invocation.WithExpression(newMemberAccess);

                // Wrap with await
                var awaitExpression = SyntaxFactory.AwaitExpression(newInvocation);

                var newRoot = root.ReplaceNode(invocation, awaitExpression);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }

        private static string GetAsyncMethodName(string syncMethodName)
        {
            switch (syncMethodName)
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
    }
}
