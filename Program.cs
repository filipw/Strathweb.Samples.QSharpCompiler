﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Quantum.QsCompiler;

namespace Strathweb.Samples.QSharpCompiler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
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
 
        using (qubit = Qubit()) {       
            for (idx in 0..count) {               
                H(qubit);
                let result = MResetZ(qubit);
                set resultsTotal += result == One ? 1 | 0;
            }
            return resultsTotal;
        }
    }
}";

            // necessary references to compile our Q# program
            var qsharpReferences = new string[]
            {
                "Microsoft.Quantum.QSharp.Core",
                "Microsoft.Quantum.Runtime.Core",
            }.Select(x => Assembly.Load(new AssemblyName(x))).Select(a => a.Location);

            // necessary references to compile C# simulation of the Q# compilation
            var csharpReferences = new string[]
            {
                "Microsoft.Quantum.QSharp.Core",
                "Microsoft.Quantum.Runtime.Core",
                "Microsoft.Quantum.Simulators",
                "Microsoft.Quantum.EntryPointDriver",
                "System.CommandLine",
                "System.Runtime",
                "netstandard",
                "System.Collections.Immutable",
                typeof(object).Assembly.FullName
            }.Select(x => Assembly.Load(new AssemblyName(x))).Select(a => a.Location);

            // events emitted by the Q# compiler
            CompilationLoader.CompilationTaskEvent += (sender, args) =>
            {
                Console.WriteLine($"{args.ParentTaskName} {args.TaskName}");
            };

            // to load our custom rewrite step, we need to point Q# compiler config at our current assembly
            var config = new CompilationLoader.Configuration
            {
                IsExecutable = true,
                RewriteSteps = new List<(string, string)>
                {
                    ( Assembly.GetExecutingAssembly().Location, null)
                }
            };

            // compile Q# code
            var compilationLoader = new CompilationLoader(loadFromDisk =>
                new Dictionary<Uri, string> { { new Uri(Path.GetFullPath("__CODE_SNIPPET__.qs")), qsharpCode } }.ToImmutableDictionary(), qsharpReferences, options: config, logger: new ConsoleLogger());
            
            // if there are any errors, print diagostics and exit
            if (compilationLoader.LoadDiagnostics.Any(d => d.Severity == Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity.Error))
            {
                PrintDiagnostics(compilationLoader);
                return;
            }

            // there were no errors, but print any other diagnostics
            PrintDiagnostics(compilationLoader);

            // we captured the emitted C# syntax trees into a static variable in the rewrite step
            var syntaxTrees = InMemoryEmitter.GeneratedFiles.Select(x => CSharpSyntaxTree.ParseText(x.Value));

            // compile C# code
            // make sure to pass in the C# references as Roslyn's metadata references
            var csharpCompilation = CSharpCompilation.Create("hello-qsharp", syntaxTrees)
                .WithReferences(csharpReferences.Select(x => MetadataReference.CreateFromFile(x)));

            // emit C# code into an in memory assembly
            using var peStream = new MemoryStream();
            var emitResult = csharpCompilation.Emit(peStream);
            if (emitResult.Success)
            {
                peStream.Position = 0;
                var qsharpLoadContext = new QSharpLoadContext();

                // run the assembly using reflection
                var qsharpAssembly = qsharpLoadContext.LoadFromStream(peStream);

                // the entry point has a special name "__QsEntryPoint__"
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

        private static void PrintDiagnostics(CompilationLoader compilationLoader)
        {
            var diagnostics = compilationLoader.LoadDiagnostics.Select(s => $"{s.Code} {s.Message}");
            if (diagnostics.Any()) 
            {
                Console.WriteLine("Diagnostics:" + Environment.NewLine + string.Join(Environment.NewLine, diagnostics));
            }
        }
    }
}