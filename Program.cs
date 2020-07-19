using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.EntryPointDriver;
using Microsoft.Quantum.Simulation.Core;

namespace Strathweb.Samples.QSharpCompiler
{
    class Program
    {
        static async Task Main(string[] args)
        {
           var qvoid = typeof(QVoid); // Microsoft.Quantum.Simulation.Core
           var option = typeof(System.CommandLine.Option); // System.CommandLine
           var iEntryPoint = typeof(IEntryPoint<,>); // Microsoft.Quantum.EntryPointDriver

            var references = new string[] {
                    "/Users/filip/.nuget/packages/microsoft.quantum.qsharp.core/0.12.20070124/lib/netstandard2.1/Microsoft.Quantum.QSharp.Core.dll",
                    "/Users/filip/.nuget/packages/microsoft.quantum.runtime.core/0.12.20070124/lib/netstandard2.1/Microsoft.Quantum.Runtime.Core.dll",
                    "/Users/filip/.nuget/packages/microsoft.quantum.simulators/0.12.20070124/lib/netstandard2.1/Microsoft.Quantum.Simulation.Common.dll",
                    "/Users/filip/.nuget/packages/microsoft.quantum.simulators/0.12.20070124/lib/netstandard2.1/Microsoft.Quantum.Simulation.QCTraceSimulatorRuntime.dll"
            };

            var qsharpCode = @"
namespace HelloQuantum {

    open Microsoft.Quantum.Canon;
    open Microsoft.Quantum.Intrinsic;

    @EntryPoint()
    operation HelloQ() : Unit {
        Message(""Hello quantum world!"");
    }
}";
            CompilationLoader.CompilationTaskEvent += (sender, args) => 
            {
                Console.WriteLine($"{args.ParentTaskName} {args.TaskName}");
            };

            var config = new CompilationLoader.Configuration 
            { 
                ExposeReferencesViaTestNames = true,
                BuildOutputFolder = "out",
                IsExecutable = true,
                RewriteSteps = new List<(string, string)>
                {
                    ( Assembly.GetExecutingAssembly().Location, null)
                }
            };

            var compilationLoader = new CompilationLoader(loadFromDisk => 
                new Dictionary<Uri, string> { { new Uri(Path.GetFullPath("__CODE_SNIPPET__.qs")), qsharpCode } }.ToImmutableDictionary(), references, options: config, logger: new ConsoleLogger()); 
            var compilation = compilationLoader.CompilationOutput;

            Console.WriteLine("Diagnostics:" + Environment.NewLine + string.Join(Environment.NewLine, compilationLoader.LoadDiagnostics.Select(s => $"{s.Code} {s.Message}")));

            var syntaxTrees = Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(), "out"), "*.cs", SearchOption.AllDirectories)
                .Select(x => File.ReadAllText(x)).Select(x => CSharpSyntaxTree.ParseText(x));

            var metadataReferences = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).Select(x => MetadataReference.CreateFromFile(x.Location)).Union(references.Select(x => MetadataReference.CreateFromFile(x)));

            var csharpCompilation = CSharpCompilation.Create("hello-qsharp", syntaxTrees, metadataReferences);

            using var peStream = new MemoryStream();
            var emitResult = csharpCompilation.Emit(peStream);
            if (emitResult.Success)
            {
                peStream.Position = 0;
                var qsharpLoadContext = new QSharpLoadContext();
                var qsharpAssembly = qsharpLoadContext.LoadFromStream(peStream);
                var entryPoint = qsharpAssembly.GetTypes().First(x => x.Name == "__QsEntryPoint__").GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static);
                var entryPointTask = entryPoint.Invoke(null, new object[] { null }) as Task<int>;
                await entryPointTask;
                qsharpLoadContext.Unload();
            } 
            else 
            {
                foreach (var diag in emitResult.Diagnostics)
                {
                    Console.WriteLine($"{diag.Id} {diag.GetMessage()}");
                }
            }
        }
    }
}