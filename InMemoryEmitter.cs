using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CsharpGeneration;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.Quantum.QsCompiler.Transformations.BasicTransformations;

namespace Strathweb.Samples.QSharpCompiler
{
    class InMemoryEmitter : IRewriteStep
    {
        public Dictionary<string, string> GeneratedFiles { get; } = new();

        private readonly Dictionary<string, string> _assemblyConstants = new();

        private readonly List<IRewriteStep.Diagnostic> _diagnostics = new();

        public string Name => "InMemoryCsharpGeneration";

        public int Priority => -2;

        public IDictionary<string, string> AssemblyConstants => _assemblyConstants;

        public IEnumerable<IRewriteStep.Diagnostic> GeneratedDiagnostics => _diagnostics;

        public bool ImplementsPreconditionVerification => false;

        public bool ImplementsTransformation => true;

        public bool ImplementsPostconditionVerification => false;

        public bool PreconditionVerification(QsCompilation compilation) => throw new NotImplementedException();

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
                var mainName = callable.Source.CodeFile + ".g.Main.cs";
                GeneratedFiles.Add(mainName, mainContent);

                var content = EntryPoint.generateSource(context, new[] { callable });
                var entryPointName = callable.Source.CodeFile + ".g.EntryPoint.cs";
                GeneratedFiles.Add(entryPointName, content);
            }

            transformed = compilation;
            return true;
        }

        public bool PostconditionVerification(QsCompilation compilation) => throw new NotImplementedException();
    }
}