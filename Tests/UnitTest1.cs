using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Tests;
using TypedStrings;

namespace Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void EmmitterDoesNotMakeStructMethodReadonly()
        {
            string source = @"
using TypedStrings;
namespace Foo {

    public class OrdinalIgnoreCaseComparer : IStaticStringComparer
    {
        public static bool Equals(string x, string y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
        public static int GetHashCode(string s) => s.GetHashCode();
    }

    [TypedString(typeof(OrdinalIgnoreCaseComparer))]
    public readonly partial struct ZipCode
    {
        public ZipCode(string s)
        {
            if (s.Length == 5 && s.All(char.IsAsciiDigit))
                Raw = s;
            else
                throw new ArgumentOutOfRangeException();
        }
    }
}";

            Compilation compilation = CreateCompilation(source);
            var driver = CSharpGeneratorDriver.Create(new Generator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
    }

        public static Compilation CreateCompilation(
            string source,
            MetadataReference[]? additionalReferences = null,
            string assemblyName = "TestAssembly")
        {
            string corelib = Assembly.GetAssembly(typeof(object))!.Location;
            string runtimeDir = Path.GetDirectoryName(corelib)!;

            var refs = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(corelib),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            };

            if (additionalReferences != null)
            {
                foreach (MetadataReference reference in additionalReferences)
                {
                    refs.Add(reference);
                }
            }

            return CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
                references: refs.ToArray(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
        }
    }

    public class OrdinalIgnoreCaseComparer : IStaticStringComparer
    {
        public static bool Equals(string x, string y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
        public static int GetHashCode(string s) => s.GetHashCode();
    }

    [TypedString(typeof(OrdinalIgnoreCaseComparer))]
    public readonly partial struct ZipCode
    {
        public ZipCode(string s)
        {
            if (s.Length == 5 && s.All(char.IsAsciiDigit))
                Raw = s;
            else
                throw new ArgumentOutOfRangeException();
        }
    }
}