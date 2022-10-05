using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CsharpGeneration;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.Quantum.QsCompiler.Transformations.BasicTransformations;
using Microsoft.Quantum.QsCompiler.DataTypes;

namespace Strathweb.Samples.QSharpCompiler
{
    class InMemoryEmitter : IRewriteStep
    {
        public static Dictionary<string, string> GeneratedFiles { get; } = new Dictionary<string, string>();

        private readonly Dictionary<string, string> _assemblyConstants = new Dictionary<string, string>();
        private readonly List<IRewriteStep.Diagnostic> _diagnostics = new List<IRewriteStep.Diagnostic>();

        public string Name => "InMemoryCsharpGeneration";

        public int Priority => -2;

        public IDictionary<string, string> AssemblyConstants => _assemblyConstants;

        public IEnumerable<IRewriteStep.Diagnostic> GeneratedDiagnostics => _diagnostics;

        public bool ImplementsPreconditionVerification => false;

        public bool ImplementsTransformation => true;

        public bool ImplementsPostconditionVerification => false;

        public bool PreconditionVerification(QsCompilation compilation)
        {
            throw new NotImplementedException();
        }

        public bool Transformation(QsCompilation compilation, out QsCompilation transformed)
        {
            var context = CodegenContext.Create(compilation, _assemblyConstants);
            var sources = GetSourceFiles.Apply(compilation.Namespaces);

            foreach (var source in sources.Where(s => !s.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var content = SimulationCode.generate(source, context);
                GeneratedFiles.Add(source, content);
            }

            if (!compilation.EntryPoints.IsEmpty)
            {
                var callable = context.allCallables.First(c => c.Key.Name == compilation.EntryPoints.First().Name).Value;
                
                var mainContent = EntryPoint.generateMainSource(context, new[] { callable });
                var mainName = callable.SourceFile + ".g.Main.cs";
                GeneratedFiles.Add(mainName, mainContent);

                var content = EntryPoint.generateSource(context, new[] { callable });
                var entryPointName = callable.SourceFile + ".g.EntryPoint.cs";
                GeneratedFiles.Add(entryPointName, content);
            }

            transformed = compilation;
            return true;
        }

        public bool PostconditionVerification(QsCompilation compilation)
        {
            throw new NotImplementedException();
        }
    }
}