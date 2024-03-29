﻿using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.Diagnostics;
using Strathweb.Samples.QSharpCompiler;
using LspDiagnostic = Microsoft.VisualStudio.LanguageServer.Protocol;

// read Q# code
var qsharpCode = File.ReadAllText("Code.qs");

// necessary references to compile our Q# program
var qsharpReferences = new string[]
{
    "Microsoft.Quantum.QSharp.Foundation",
    "Microsoft.Quantum.QSharp.Core",
    "Microsoft.Quantum.Runtime.Core",
    "Microsoft.Quantum.Standard",
}.Select(x => Assembly.Load(new AssemblyName(x))).Select(a => a.Location);

// events emitted by the Q# compiler
PerformanceTracking.CompilationTaskEvent += (eventType, parentTaskName, taskName) =>
{
    Console.Error.WriteLine($"Diagnostic: {parentTaskName} {taskName} - {eventType}");
};

var inMemoryEmitter = new InMemoryEmitter();

// load our custom rewrite step
var config = new CompilationLoader.Configuration
{
    IsExecutable = true,
    RewriteStepInstances = new List<(IRewriteStep, string)>
    {
        ( inMemoryEmitter, ""),
    },
};

// compile Q# code
var compilationLoader = new CompilationLoader(
    loadFromDisk =>
        new Dictionary<Uri, string> {
            { new Uri(Path.GetFullPath("__CODE_SNIPPET__.qs")), qsharpCode  }
        }.ToImmutableDictionary(), qsharpReferences, options: config, logger: new ConsoleLogger());

// print any diagnostics
foreach (var diagnostic in compilationLoader.LoadDiagnostics)
{
    ConsoleLogger.PrintToConsole(diagnostic.Severity, diagnostic.Message);
}

// if there are any errors, exit
if (compilationLoader.LoadDiagnostics.Any(d => d.Severity == LspDiagnostic.DiagnosticSeverity.Error))
{
    return;
}

// necessary references to compile C# simulation of the Q# compilation
var csharpReferences = new string[]
{
    "Microsoft.Quantum.QSharp.Foundation",
    "Microsoft.Quantum.QSharp.Core",
    "Microsoft.Quantum.Runtime.Core",
    "Microsoft.Quantum.Standard",
    "Microsoft.Quantum.Simulators",
    "Microsoft.Quantum.EntryPointDriver",
    "System.CommandLine",
    "System.Runtime",
    "netstandard",
    "System.Collections.Immutable",
    typeof(object).Assembly.FullName,
}.Select(x => Assembly.Load(new AssemblyName(x))).Select(a => a.Location);

// we captured the emitted C# syntax trees in the rewrite step
var syntaxTrees = inMemoryEmitter.GeneratedFiles.Select(x => CSharpSyntaxTree.ParseText(x.Value));

// compile C# code
// make sure to pass in the C# references as Roslyn's metadata references
var csharpCompilation = CSharpCompilation.Create("hello-qsharp", syntaxTrees)
    .WithReferences(csharpReferences.Select(x => MetadataReference.CreateFromFile(x)));

// print any diagnostics
var csharpDiagnostics = csharpCompilation.GetDiagnostics().Where(d => d.Severity != DiagnosticSeverity.Hidden);
foreach (var diagnostic in csharpDiagnostics)
{
    ConsoleLogger.PrintToConsole(diagnostic.Severity, diagnostic.GetMessage());
}

// if there are any errors, exit
if (csharpDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
{
    return;
}

// emit C# code into an in memory assembly
using var peStream = new MemoryStream();
var emitResult = csharpCompilation.Emit(peStream);
peStream.Position = 0;
var qsharpLoadContext = new QSharpLoadContext();

// run the assembly using reflection
var qsharpAssembly = qsharpLoadContext.LoadFromStream(peStream);

// the entry point has a special name "__QsEntryPoint__"
var entryPoint = qsharpAssembly.EntryPoint;

if (entryPoint == null)
{
    ConsoleLogger.PrintToConsole(DiagnosticSeverity.Error, "Could not find entrypoint.");
    return;
}

entryPoint.Invoke(null, new object[] { null });
qsharpLoadContext.Unload();