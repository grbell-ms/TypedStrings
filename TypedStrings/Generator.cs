﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;
using System.Threading;

namespace TypedStrings
{
    [Generator]
    public sealed class Generator : IIncrementalGenerator
    {
        private const string AttributeName = $"{nameof(TypedStrings)}.TypedStringAttribute";
        private static readonly string s_generatedCodeAttribute = $"[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"{typeof(Generator).Assembly.GetName().Name}\", \"{typeof(Generator).Assembly.GetName().Version}\")]";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<TypedStringStruct> typedStringStructs =
                context.SyntaxProvider.ForAttributeWithMetadataName(
                    AttributeName,
                    (node, _) => node is StructDeclarationSyntax,
                    GetSemanticTargetForGeneration)
                .Where(x => x is not null)!;

            context.RegisterSourceOutput(typedStringStructs, EmitSourceFile);
            context.RegisterPostInitializationOutput(InjectTypedStringAttribute);
        }

        private void InjectTypedStringAttribute(IncrementalGeneratorPostInitializationContext obj)
        {
            obj.AddSource("TypedStringAttribute",
@"
#nullable enable
namespace TypedStrings
{
    public sealed class TypedStringAttribute : global::System.Attribute
    {
        public global::System.Type Comparer { get; }

        public TypedStringAttribute(global::System.Type comparer) => Comparer = comparer;
    }
}
");

            obj.AddSource("IStaticStringComparer",
@"
namespace TypedStrings
{
#nullable enable
    public interface IStaticStringComparer
    {
        public static abstract bool Equals(string x, string y);

        public static abstract int GetHashCode(string s);
    }
}
");
        }

        private static void GenType(TypedStringStruct tss, StringBuilder sb)
        {
            if (!string.IsNullOrWhiteSpace(tss.Namespace))
            {
                sb.AppendLine($@"
namespace {tss.Namespace}
{{");
            }

            sb.AppendLine($@"
    {s_generatedCodeAttribute}
    partial struct {tss.StructName} : global::System.IEquatable<{tss.StructName}>
    {{
        public string Raw {{ get; init; }} 

        public override int GetHashCode() =>
            {tss.ComparerFullName}.GetHashCode(this.Raw);

        public override bool Equals(object? other) => other switch
        {{
            string s => Equals(s),
            {tss.StructName} ts => Equals(ts),
            _ => false,
        }};

        public bool Equals({tss.StructName} other) =>
            {tss.ComparerFullName}.Equals(this.Raw, other.Raw);

        public bool Equals(string other) =>
            {tss.ComparerFullName}.Equals(this.Raw, other);

        public static bool operator==({tss.StructName} left, {tss.StructName} right) =>
            {tss.ComparerFullName}.Equals(left.Raw, right.Raw);

        public static bool operator!=({tss.StructName} left, {tss.StructName} right) =>
            !{tss.ComparerFullName}.Equals(left.Raw, right.Raw);
        
        public static bool operator==(string left, {tss.StructName} right) =>
            {tss.ComparerFullName}.Equals(left, right.Raw);

        public static bool operator!=(string left, {tss.StructName} right) =>
            !{tss.ComparerFullName}.Equals(left, right.Raw);

        public static bool operator==({tss.StructName} left, string right) =>
            {tss.ComparerFullName}.Equals(left.Raw, right);

        public static bool operator!=({tss.StructName} left, string right) =>
            !{tss.ComparerFullName}.Equals(left.Raw, right);

        public static implicit operator string({tss.StructName} x) => x.Raw;
");



            sb.AppendLine($@"    }}");

            if (!string.IsNullOrWhiteSpace(tss.Namespace))
            {
                sb.AppendLine($@"}}");
            }
        }

        private static void EmitSourceFile(SourceProductionContext context, TypedStringStruct ec)
        {
            StringBuilder sb = new StringBuilder(1024);

            sb.AppendLine(@"// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            GenType(ec, sb);

            context.AddSource($"{ec.StructName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static TypedStringStruct? GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var structDef = (StructDeclarationSyntax)context.TargetNode;
            NamespaceDeclarationSyntax? structNamespace = structDef.Parent as NamespaceDeclarationSyntax;
            if (structNamespace is null && structDef.Parent is not CompilationUnitSyntax)
            {
                // since this generator doesn't know how to generate a nested type...
                return null;
            }

            foreach (AttributeData attribute in context.TargetSymbol.GetAttributes())
            {
                if ((attribute.AttributeClass?.Name) == "TypedStringAttribute" &&
                    attribute.AttributeClass.ToDisplayString() == AttributeName &&
                    !attribute.ConstructorArguments.IsDefaultOrEmpty &&
                    attribute.ConstructorArguments[0].Value is INamedTypeSymbol comparerType &&
                    comparerType.AllInterfaces.Any(symbol => symbol.Name.Equals("IStaticStringComparer")))
                {
                    var stuctNamespace = ConstructNamespace(structNamespace);
                    string structName = structDef.Identifier.ValueText;
                    var comparerNamespace = ConstructNamespace(comparerType.ContainingNamespace);
                    return new TypedStringStruct(stuctNamespace, structName, $"{comparerNamespace}.{comparerType.Name}");
                }
            }

            return null;
        }

        private static string ConstructNamespace(INamespaceSymbol ns)
        {
            if (ns is null)
                return string.Empty;

            string nspace = ns.Name.ToString();
            while (true)
            {
                ns = ns.ContainingNamespace;
                if (ns == null || ns.IsGlobalNamespace)
                {
                    return $"global::{nspace}";
                }

                nspace = $"{ns.Name}.{nspace}";
            }
        }

        private static string ConstructNamespace(NamespaceDeclarationSyntax? ns)
        {
            if (ns is null)
                return string.Empty;

            string nspace = ns.Name.ToString();
            while (true)
            {
                ns = ns.Parent as NamespaceDeclarationSyntax;
                if (ns == null)
                {
                    break;
                }

                nspace = $"{ns.Name}.{nspace}";
            }

            return nspace;
        }
    }
}
