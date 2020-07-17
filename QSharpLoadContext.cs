using System.Runtime.Loader;

namespace Strathweb.Samples.QSharpCompiler
{
    public class QSharpLoadContext : AssemblyLoadContext
    {
        public QSharpLoadContext() : base(isCollectible: true)
        {
        }
    }
}