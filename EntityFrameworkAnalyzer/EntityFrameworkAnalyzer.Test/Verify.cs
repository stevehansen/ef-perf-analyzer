using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace EntityFrameworkAnalyzer.Test
{
    // Framework and EF assemblies the test sources compile against; the analyzer enables EFPERF002/005 only when EF is referenced
    internal static class References
    {
        public static readonly ReferenceAssemblies NoEF = ReferenceAssemblies.Net.Net80;

        public static readonly ReferenceAssemblies EFCore = ReferenceAssemblies.Net.Net80
            .AddPackages(ImmutableArray.Create(new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.33")));

        public static readonly ReferenceAssemblies EF6 = ReferenceAssemblies.NetFramework.Net48.Default
            .AddPackages(ImmutableArray.Create(new PackageIdentity("EntityFramework", "6.5.1")));
    }

    internal static class Verify
    {
        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) => new DiagnosticResult(descriptor);

        public static Task AnalyzerAsync(ReferenceAssemblies references, string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpCodeFixTest<EFPerfAnalyzer, EmptyCodeFixProvider, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = references,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        public static Task CodeFixAsync<TCodeFix>(ReferenceAssemblies references, string source, string fixedSource, params DiagnosticResult[] expected)
            where TCodeFix : CodeFixProvider, new()
        {
            var test = new CSharpCodeFixTest<EFPerfAnalyzer, TCodeFix, DefaultVerifier>
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = references,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }
    }
}
