# SourceGenerationPresentation

## Setup the Projects

- Create a solution with a WebApi project (.NET 6 or higher) called ```SourceGeneratorTestApi```
- Create a class library **.NET Standard 2.0**  called ```SourceGeneration```
- Remove Class1.cs
- Make `SourceGeneration.csproj` look like this:
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>Latest</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <Version>0.1</Version>
    <Nullable>enable</Nullable>
  </PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.6.0" PrivateAssets="all" />
		<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="7.0.0" />
	</ItemGroup>
</Project>

```
- Within `SourceGeneratorTestApi` right click `Dependencies > Add Project Reference...` and select `SourceGeneration`

## Create the baserecord and set up test records

Create the baserecord of the enum in the `SourceGeneration.csproj`

```csharp
namespace SourceGeneration;

public abstract record MyEnum<TEnum> where TEnum : class;
```

In `SourceGeneratorTestApi` create a folder called `Enums` and created the following:

`Fruit.cs`
```csharp
public record Fruit
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}
```

`Fruits.cs`
```csharp
public record Fruits : MyEnum<Fruit>
{
    public static Fruit Apple { get; } = new() { Name = "Apple", Description = "A red fruit" };
    public static Fruit Banana { get; } = new() { Name = "Banana", Description = "A yellow fruit" };
}
```


## Setting up the SourceGenerator

Create `SourceGenerators\MyEnumGenerator.cs` file in `SourceGeneration`  

```csharp
using Microsoft.CodeAnalysis;

namespace SourceGeneration.SourceGenerators;

[Generator]
internal class MyEnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

    }
}
```

## Debugging the source generator
Now we have instantiated our Source generator that will be fired on writing code. But how do we debug it?

We can add a debugger! It is a bit tedious however.


```csharp
using Microsoft.CodeAnalysis;

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
    }
}


```

If we now build the project.... nothing happens.

We need to change something in the `SourceGeneration.csproj`
```csharp
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SourceGeneration\SourceGeneration.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
  </ItemGroup>

</Project>

```

If we rebuild all now we can select a debugger. Select option with SourceGenerator in the title since that is the name of our solution.
Now our debugging tool hit the Debugger line and will hit any subsequent breakppoint.

## Screening for relevant increments.

Because we want to quickly filter out all the increments we dont want to use we need to create a syntaxprovider.

Update `MyEnumGenerator.cs`
```csharp
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

```