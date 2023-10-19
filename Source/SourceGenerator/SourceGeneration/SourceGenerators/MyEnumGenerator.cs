using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

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

    }

}
