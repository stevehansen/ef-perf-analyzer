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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EFPERF001CodeFixProvider)), Shared]
    public class EFPERF001CodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add projection";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Diagnostics.EFPERF001.Id);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            context.RegisterCodeFix(CodeAction.Create(Title, c => AddProjectionAsync(context.Document, diagnostic, c), Title), diagnostic);
            return Task.FromResult(0);
        }

        private static async Task<Document> AddProjectionAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declarator = (VariableDeclaratorSyntax)root.FindToken(diagnosticSpan.Start).Parent;
            var invocation = (InvocationExpressionSyntax)declarator.Initializer.Value;
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            var firstArgument = invocation.ArgumentList.Arguments.FirstOrDefault();
            if (firstArgument != null)
            {
                // Move condition from .First() to extra .Where() call so that we can place the .Select() between them
                var whereExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, memberAccess.Expression, SyntaxFactory.IdentifierName("Where"));
                var withWhere = SyntaxFactory.InvocationExpression(whereExpression, invocation.ArgumentList);
                var whereMemberAccess = memberAccess.WithExpression(withWhere);
                var newInvocation = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, whereMemberAccess.Expression, memberAccess.Name));
                root = root.ReplaceNode(invocation, newInvocation);

                declarator = (VariableDeclaratorSyntax)root.FindToken(diagnosticSpan.Start).Parent;
                invocation = (InvocationExpressionSyntax)declarator.Initializer.Value;
                memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            }

            var selectExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, memberAccess.Expression, SyntaxFactory.IdentifierName("Select"));
            var itExpr = SyntaxFactory.IdentifierName("it");
            var it = SyntaxFactory.Identifier("it");
            var members = diagnostic.Properties["Members"].Split('\n').Select(m => SyntaxFactory.AnonymousObjectMemberDeclarator(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, itExpr, SyntaxFactory.IdentifierName(m))));
            var newExpr = SyntaxFactory.AnonymousObjectCreationExpression(SyntaxFactory.SeparatedList(members));
            var argument = SyntaxFactory.Argument(SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(it), newExpr));
            var arguments = SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(argument));
            var projectedInvocationExpression = SyntaxFactory.InvocationExpression(selectExpression, arguments);
            var newMemberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, projectedInvocationExpression, memberAccess.Name);

            var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}