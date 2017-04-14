using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace EntityFrameworkAnalyzer
{
    public static class Diagnostics
    {
        private static readonly LocalizableString title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString messageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        public static readonly DiagnosticDescriptor EFPERF001 = new DiagnosticDescriptor(nameof(EFPERF001), title, messageFormat, "Performance", DiagnosticSeverity.Warning, true, description);

        public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            EFPERF001
            );
    }
}
