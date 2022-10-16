using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.Diagnostics;
using Strathweb.Samples.QSharpCompiler;

// sample Q# code
var qsharpCode = @"
namespace HelloQuantum {

    open Microsoft.Quantum.Canon;
    open Microsoft.Quantum.Measurement;
    open Microsoft.Quantum.Intrinsic;
    open Microsoft.Quantum.Convert;

    @EntryPoint()
    operation HelloQ() : Unit {
        let ones = GetRandomBit(100);
        Message(""Ones: "" + IntAsString(ones));
        Message(""Zeros: "" + IntAsString((100 - ones)));
    }
	
    operation GetRandomBit(count : Int) : Int {
 
        mutable resultsTotal = 0;
 
        use qubit = Qubit();       
            for idx in 0..count {               
                H(qubit);
                let result = MResetZ(qubit);
                set resultsTotal += result == One ? 1 | 0;
            }
            return resultsTotal;
    }
}";

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
    Console.WriteLine($"{parentTaskName} {taskName} - {eventType}");
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
if (compilationLoader.LoadDiagnostics.Any())
{
    Console.WriteLine("Diagnostics:" + Environment.NewLine + string.Join(Environment.NewLine, compilationLoader.LoadDiagnostics.Select(d => $"{d.Severity} {d.Code} {d.Message}")));

    // if there are any errors, exit
    if (compilationLoader.LoadDiagnostics.Any(d => d.Severity == Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity.Error))
    {
        return;
    }
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
if (csharpDiagnostics.Any())
{
    Console.WriteLine("C# Diagnostics:" + Environment.NewLine + string.Join(Environment.NewLine, csharpDiagnostics.Select(d => $"{d.Severity} {d.Id} {d.GetMessage()}")));

    // if there are any errors, exit
    if (csharpDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
    {
        return;
    }
}

// emit C# code into an in memory assembly
using var peStream = new MemoryStream();
var emitResult = csharpCompilation.Emit(peStream);
peStream.Position = 0;
var qsharpLoadContext = new QSharpLoadContext();

// run the assembly using reflection
var qsharpAssembly = qsharpLoadContext.LoadFromStream(peStream);

// the entry point has a special name "__QsEntryPoint__"
var entryPoint = qsharpAssembly.GetTypes().FirstOrDefault(x => x.Name == "__QsEntryPoint__").GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static);

if (entryPoint == null)
{
    Console.WriteLine("ERROR: Could not find entrypoint.");
    return;
}

var entryPointTask = entryPoint.Invoke(null, new object[] { null }) as Task<int>;
await entryPointTask;
qsharpLoadContext.Unload();