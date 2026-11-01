using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkAnalyzer
{
    internal static class UsingDirectives
    {
        // Adds "using namespaceName;" after the file's usings unless the file already has it
        public static SyntaxNode Ensure(SyntaxNode root, string namespaceName)
        {
            if (!(root is CompilationUnitSyntax compilationUnit) || root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(u => u.Alias == null && u.Name.ToString() == namespaceName))
                return root;

            var newLine = root.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
            var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
                .NormalizeWhitespace()
                .WithTrailingTrivia(newLine.IsKind(SyntaxKind.EndOfLineTrivia) ? newLine : SyntaxFactory.CarriageReturnLineFeed);
            return compilationUnit.AddUsings(directive);
        }
    }
}
