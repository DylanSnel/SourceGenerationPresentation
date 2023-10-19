using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace SourceGeneration.SourceGenerators;

[Generator]
internal class MyEnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif

        // We want to generate code for all the enums that have the MyEnum Basetype
        IncrementalValuesProvider<RecordDeclarationSyntax> myEnumRecords = context.SyntaxProvider
          .CreateSyntaxProvider(
              predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select records with basetypes
              transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)) // select the enum with the EpicEnum basetype
          .Where(static m => m is not null)!; // filter out enums that we don't care about


        IncrementalValueProvider<(Compilation compilation, ImmutableArray<RecordDeclarationSyntax> myEnums)> compilationAndEnums
           = context.CompilationProvider.Combine(myEnumRecords.Collect());

        // Generate the source using the compilation and enums
        context.RegisterSourceOutput(compilationAndEnums,
            static (spc, source) => Execute(source.compilation, source.myEnums, spc));
    }

    //Predicate for selecting the syntax nodes we want to generate code for
    static bool IsSyntaxTargetForGeneration(SyntaxNode node)
       => node is RecordDeclarationSyntax m
           && m.BaseList?.Types.Count > 0;

    static RecordDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        // We know the node is a EnumDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        var recordDeclarationSyntax = (RecordDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetDeclaredSymbol(recordDeclarationSyntax) is not INamedTypeSymbol recordSymbol)
        {
            // Unexpected: the symbol didnt match what we expected
            return null;
        }
        else if (recordSymbol.BaseType?.Name == "MyEnum")
        {
            return recordDeclarationSyntax;
        }

        // we didn't find the attribute we were looking for
        return null;
    }


    static void Execute(Compilation compilation, ImmutableArray<RecordDeclarationSyntax> myEnums, SourceProductionContext context)
    {
        IEnumerable<RecordDeclarationSyntax> distinctEnums = myEnums.Distinct();

        // Convert each RecordDeclarationSyntax to an EnumToGenerate
        List<EnumToGenerate> enumsToGenerate = GetTypesToGenerate(compilation, distinctEnums, context.CancellationToken);
        foreach (var enumToGenerate in enumsToGenerate)
        {
            if (enumToGenerate.Values.Count < 1)
            {
                continue;
            }
            GenerateEnum(context, enumToGenerate);
        }
    }

    static List<EnumToGenerate> GetTypesToGenerate(Compilation compilation, IEnumerable<RecordDeclarationSyntax> myEnums, CancellationToken ct)
    {
        var enumsToGenerate = new List<EnumToGenerate>();

        foreach (RecordDeclarationSyntax recordDeclarationSyntax in myEnums)
        {
            // stop if we're asked to
            ct.ThrowIfCancellationRequested();

            // Get the semantic representation of the enum syntax
            SemanticModel semanticModel = compilation.GetSemanticModel(recordDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(recordDeclarationSyntax) is not INamedTypeSymbol recordSymbol)
            {
                // something went wrong, bail out
                continue;
            }

            string recordName = recordSymbol.ToString();

            var baseType = recordSymbol.BaseType;
            var enumType = baseType!.IsGenericType ? baseType.TypeArguments.First() : baseType.BaseType!.TypeArguments.First();

            var properties = recordSymbol.GetMembers().OfType<IPropertySymbol>()
                                .Where(m => SymbolEqualityComparer.Default.Equals(m.Type, enumType));
            var members = properties.Select(x => x.Name).ToList();

            // Create an EnumToGenerate for use in the generation phase
            enumsToGenerate.Add(new EnumToGenerate(recordSymbol.Name, members, recordSymbol.ContainingNamespace.ToString()));
        }

        return enumsToGenerate;
    }

    static void GenerateEnum(SourceProductionContext context, EnumToGenerate enumToGenerate)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {enumToGenerate.Namespace};");
        sb.AppendLine($"public enum {enumToGenerate.Name}Enum");
        sb.AppendLine("{");

        foreach (var property in enumToGenerate.Values)
        {
            sb.AppendLine("    " + property + ",");
        }

        sb.AppendLine("}");
        context.AddSource($"{enumToGenerate.Namespace}.{enumToGenerate.Name}Enum.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

}

internal readonly struct EnumToGenerate
{
    public readonly string Name;
    public readonly string Namespace;
    public readonly List<string> Values;

    public EnumToGenerate(string name, List<string> values, string @namespace)
    {
        Name = name;
        Values = values;
        Namespace = @namespace;
    }
}
