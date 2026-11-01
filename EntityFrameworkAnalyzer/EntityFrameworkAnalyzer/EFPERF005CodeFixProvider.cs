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

            // The diagnostic is on the name of the synchronous query method
            var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            var asyncName = SyntaxFactory.IdentifierName(diagnostic.Properties[EFPerfAnalyzer.AsyncVariantProperty]).WithTriviaFrom(memberAccess.Name);
            var asyncInvocation = invocation.WithExpression(memberAccess.WithName(asyncName)).WithoutTrivia();
            ExpressionSyntax awaited = SyntaxFactory.AwaitExpression(SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space), asyncInvocation);

            // await binds looser than member and element access: query.ToList().Count => (await query.ToListAsync()).Count
            if (invocation.Parent is MemberAccessExpressionSyntax || invocation.Parent is ConditionalAccessExpressionSyntax || invocation.Parent is ElementAccessExpressionSyntax)
                awaited = SyntaxFactory.ParenthesizedExpression(awaited);

            var newRoot = root.ReplaceNode(invocation, awaited.WithTriviaFrom(invocation));
            newRoot = UsingDirectives.Ensure(newRoot, diagnostic.Properties[EFPerfAnalyzer.EFNamespaceProperty]);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
