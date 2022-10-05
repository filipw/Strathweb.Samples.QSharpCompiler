using Microsoft.Quantum.QsCompiler.Diagnostics;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Strathweb.Samples.QSharpCompiler
{
    public class ConsoleLogger : LogTracker 
    {
        private readonly Func<Diagnostic, string> applyFormatting;

        protected internal virtual string Format(Diagnostic msg) =>
            this.applyFormatting(msg);

        protected sealed override void Print(Diagnostic msg) =>
            PrintToConsole(msg.Severity, this.Format(msg));

        public ConsoleLogger(
            Func<Diagnostic, string> format = null,
            DiagnosticSeverity verbosity = DiagnosticSeverity.Hint,
            IEnumerable<int> noWarn = null,
            int lineNrOffset = 0)
        : base(verbosity, noWarn, lineNrOffset) =>
            this.applyFormatting = format ?? Formatting.HumanReadableFormat;

        private static void PrintToConsole(DiagnosticSeverity? severity, string message)
        {
            var (stream, color) = severity switch
            {
                DiagnosticSeverity.Error => (Console.Error, ConsoleColor.Red),
                DiagnosticSeverity.Warning => (Console.Error, ConsoleColor.Yellow),
                _ => (Console.Out, Console.ForegroundColor),
            };

            var consoleColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            try
            {
                var output = message;
                stream.WriteLine(output);
            }
            finally
            {
                Console.ForegroundColor = consoleColor;
            }
        }
    }
}