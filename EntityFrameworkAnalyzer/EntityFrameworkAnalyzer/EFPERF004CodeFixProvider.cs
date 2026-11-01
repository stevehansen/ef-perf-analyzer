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
        private const string Title = "Move before materialization";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Diagnostics.EFPERF004.Id);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var invocation = FindOperatorInvocation(root, diagnostic);

            // A query needs an expression tree: a statement lambda would not compile, and a delegate or method group would still bind to the in-memory Where
            var arguments = invocation.ArgumentList.Arguments.Select(a => a.Expression).ToArray();
            var isWhere = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.Text == "Where";
            if (arguments.Any(a => a is AnonymousFunctionExpressionSyntax function && function.Body is BlockSyntax)
                || (isWhere && !arguments.All(a => a is AnonymousFunctionExpressionSyntax)))
                return;

            context.RegisterCodeFix(CodeAction.Create(Title, c => MoveBeforeMaterializationAsync(context.Document, diagnostic, c), Title), diagnostic);
        }

        private static InvocationExpressionSyntax FindOperatorInvocation(SyntaxNode root, Diagnostic diagnostic)
        {
            // The diagnostic is on the name of the Where/Skip/Take call
            return root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        }

        private static async Task<Document> MoveBeforeMaterializationAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var operatorInvocation = FindOperatorInvocation(root, diagnostic);
            var operatorAccess = (MemberAccessExpressionSyntax)operatorInvocation.Expression;
            var materializeInvocation = (InvocationExpressionSyntax)operatorAccess.Expression;
            var materializeAccess = (MemberAccessExpressionSyntax)materializeInvocation.Expression;

            // query.ToList().Where(f) => query.Where(f).ToList(), swapping only names and arguments so a multi-line chain keeps its layout
            var newOperatorInvocation = SyntaxFactory.InvocationExpression(
                materializeAccess.WithName(operatorAccess.Name.WithTriviaFrom(materializeAccess.Name)),
                operatorInvocation.ArgumentList.WithTriviaFrom(materializeInvocation.ArgumentList));
            var newMaterializeInvocation = operatorInvocation
                .WithExpression(operatorAccess.WithExpression(newOperatorInvocation).WithName(materializeAccess.Name.WithTriviaFrom(operatorAccess.Name)))
                .WithArgumentList(materializeInvocation.ArgumentList.WithTriviaFrom(operatorInvocation.ArgumentList));

            return document.WithSyntaxRoot(root.ReplaceNode(operatorInvocation, newMaterializeInvocation));
        }
    }
}
