using System;
using System.Collections.Generic;
using Microsoft.Quantum.QsCompiler.Diagnostics;
using Diagnostic = Microsoft.VisualStudio.LanguageServer.Protocol.Diagnostic;
using DiagnosticSeverity = Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity;

namespace Strathweb.Samples.QSharpCompiler
{
    public class ConsoleLogger : LogTracker 
    {
        private readonly Func<Diagnostic, string> applyFormatting;

        protected internal virtual string Format(Diagnostic msg) =>
            this.applyFormatting(msg);

        /// <inheritdoc/>
        protected sealed override void Print(Diagnostic msg) =>
            PrintToConsole(msg.Severity, this.Format(msg));

        public ConsoleLogger(
            Func<Diagnostic, string> format = null,
            DiagnosticSeverity verbosity = DiagnosticSeverity.Hint,
            IEnumerable<int> noWarn = null,
            int lineNrOffset = 0)
        : base(verbosity, noWarn, lineNrOffset) =>
            this.applyFormatting = format ?? Formatting.HumanReadableFormat;

        private static void PrintToConsole(DiagnosticSeverity severity, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            var (stream, color) =
                severity == DiagnosticSeverity.Error ? (Console.Error, ConsoleColor.Red) :
                severity == DiagnosticSeverity.Warning ? (Console.Error, ConsoleColor.Yellow) :
                (Console.Out, Console.ForegroundColor);

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