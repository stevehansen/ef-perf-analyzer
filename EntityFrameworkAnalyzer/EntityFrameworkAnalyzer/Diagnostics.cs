using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace EntityFrameworkAnalyzer
{
    public static class Diagnostics
    {
        // EFPERF001: Prefer projection
        private static readonly LocalizableString title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString messageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        public static readonly DiagnosticDescriptor EFPERF001 = new DiagnosticDescriptor(nameof(EFPERF001), title, messageFormat, "Performance", DiagnosticSeverity.Warning, true, description);

        // EFPERF002: Use AsNoTracking for read-only queries
        private static readonly LocalizableString title002 = new LocalizableResourceString(nameof(Resources.EFPERF002Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString messageFormat002 = new LocalizableResourceString(nameof(Resources.EFPERF002MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString description002 = new LocalizableResourceString(nameof(Resources.EFPERF002Description), Resources.ResourceManager, typeof(Resources));
        public static readonly DiagnosticDescriptor EFPERF002 = new DiagnosticDescriptor(nameof(EFPERF002), title002, messageFormat002, "Performance", DiagnosticSeverity.Warning, true, description002);

        // EFPERF003: N+1 query detection
        private static readonly LocalizableString title003 = new LocalizableResourceString(nameof(Resources.EFPERF003Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString messageFormat003 = new LocalizableResourceString(nameof(Resources.EFPERF003MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString description003 = new LocalizableResourceString(nameof(Resources.EFPERF003Description), Resources.ResourceManager, typeof(Resources));
        public static readonly DiagnosticDescriptor EFPERF003 = new DiagnosticDescriptor(nameof(EFPERF003), title003, messageFormat003, "Performance", DiagnosticSeverity.Warning, true, description003);

        // EFPERF004: Client-side evaluation
        private static readonly LocalizableString title004 = new LocalizableResourceString(nameof(Resources.EFPERF004Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString messageFormat004 = new LocalizableResourceString(nameof(Resources.EFPERF004MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString description004 = new LocalizableResourceString(nameof(Resources.EFPERF004Description), Resources.ResourceManager, typeof(Resources));
        public static readonly DiagnosticDescriptor EFPERF004 = new DiagnosticDescriptor(nameof(EFPERF004), title004, messageFormat004, "Performance", DiagnosticSeverity.Warning, true, description004);

        // EFPERF005: Use async query methods
        private static readonly LocalizableString title005 = new LocalizableResourceString(nameof(Resources.EFPERF005Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString messageFormat005 = new LocalizableResourceString(nameof(Resources.EFPERF005MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString description005 = new LocalizableResourceString(nameof(Resources.EFPERF005Description), Resources.ResourceManager, typeof(Resources));
        public static readonly DiagnosticDescriptor EFPERF005 = new DiagnosticDescriptor(nameof(EFPERF005), title005, messageFormat005, "Performance", DiagnosticSeverity.Info, true, description005);

        public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            EFPERF001,
            EFPERF002,
            EFPERF003,
            EFPERF004,
            EFPERF005
            );
    }
}
