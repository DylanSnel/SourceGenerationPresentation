# SourceGenerationPresentation

This guide will help you recreate my EpicEnums source generation, or at least a simplified version of that. Please check out the EpicEnums repository.

**Note:** Every step of this project has its own completed branch, so if you get lost you can see the solution there.

## Install the tooling

Go to your Visual Studio Installer > Modify > .NET Comnpiler Platform SDK


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



# Source Generation


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

Because we want to quickly filter out all the increments we dont want to use we need to create a syntaxprovider. The syntaxprovider helps us weed out what incremental changes we want to act on.

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

## Generating code
Okay time to generate some code. First we create a method that will extract features for the enum we want to create and return a list of those feature objects

```csharp
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
```

Secondly we create our code to generate the enum. You have to make sure that when more enums have to be made the name of the file is always unique.

```csharp
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
```

Now we can tie it all together by filling in our `Execute` method.

```csharp
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
```

and that is it! We made our first source generator and we are ready to test it. Make sure you rebuild the `SourceGeneration` package befor you press *Rebuild All* 

Head over to `WeatherForecast.cs` and lets test it out.
```csharp
using SourceGeneratorTestApi.Enums;
namespace SourceGeneratorTestApi;

public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }

    public FruitsEnum Fruits => FruitsEnum.Banana;

}

```

**You might still get red squiggles under the new enum property. In that case restart your Visual Studio since it wont always update its Analyzers to deal with code generation**


# Analyzers

When someone now would create a Fruit in the Enum that would not be marked static it would generate all sorts of unclear issues for the user

```csharp
public record Fruits : MyEnum<Fruit>
{
    public static Fruit Apple { get; } = new() { Name = "Apple", Description = "A red fruit" };
    public static Fruit Banana { get; } = new() { Name = "Banana", Description = "A yellow fruit" };
    public Fruit Kiwi { get; } = new() { Name = "Kiwi", Description = "A brown fruit" };

}

```

We could write an analyzer for us that would help us understand whats wrong.

Create a new class `Analyzers/StaticEnumPropertiesAnalyzer.cs` in our `SourceGeneration` project.

```csharp
namespace SourceGeneration.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StaticEnumPropertiesAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => throw new NotImplementedException();

    public override void Initialize(AnalysisContext context)
    {
        throw new NotImplementedException();
    }
}

```


First we will register what type of issue the analyzer will report. Add the following and replace `SupportedDiagnostics`
```csharp
internal const string ErrorId = "ME0001";
readonly DiagnosticDescriptor _enumPropertiesShouldBeStaticDescriptor = new(
           id: ErrorId,
           title: "MyEnum",
           messageFormat: "MyEnum: Property '{0}' of type '{1}' should be marked as static",
           category: "MyEnum",
           DiagnosticSeverity.Error,
           isEnabledByDefault: true);

public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_enumPropertiesShouldBeStaticDescriptor);
```

Now let us implement the actual analyzer.

```csharp
public override void Initialize(AnalysisContext context)
{
    //Register our analyzer for the syntax node types we care about
    context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
}

private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
{
    var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;

    // Check if the enclosing type is a record that inherits from MyEnum<T>
    if (propertyDeclaration.Parent is RecordDeclarationSyntax recordDeclaration &&
        recordDeclaration.BaseList is not null)
    {
        foreach (var baseType in recordDeclaration.BaseList.Types)
        {
            var typeSymbol = context.SemanticModel.GetTypeInfo(baseType.Type).Type as INamedTypeSymbol;

            // Check if the base type is MyEnum<T> where T is the same type as the property
            if (typeSymbol?.ConstructedFrom.Name == "MyEnum" &&
                typeSymbol.TypeArguments.Length == 1 &&
                typeSymbol.TypeArguments[0].Name == propertyDeclaration.Type.ToString())
            {
                // Check if the property is not static
                if (!propertyDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    // Report the diagnostic
                    var diagnostic = Diagnostic.Create(_enumPropertiesShouldBeStaticDescriptor, propertyDeclaration.GetLocation(), propertyDeclaration.Identifier.Text, propertyDeclaration.Type.ToString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
```

Now we should up the version in the `SourceGeneration.csproj` rebuild and then restart Visual Studio. It should now give us the error for the kiwi property.


# CodeFix

Having the analyzer tell the user how they messed up is great. But what would be better than actually solving the problem. So lets write a code fix provider.

Create a new class `CodeFixProviders/StaticEnumPropertiesCodeFixProvider.cs` in our `SourceGeneration` project.
```csharp
namespace SourceGeneration.CodeFixProviders;
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaticEnumPropertiesCodeFixProvider)), Shared]
public class StaticEnumPropertiesCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => throw new NotImplementedException();

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        throw new NotImplementedException();
    }
}
```

Then we will declare what diagnostics we intend to solve:

```csharp
public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(StaticEnumPropertiesAnalyzer.ErrorId);
```

Now we have to get our diagnostic that we got from the analyzers and find the correct `SyntaxNode`:

```csharp
public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
{
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    // Find the diagnostic that has the matching span.
    var diagnostic = context.Diagnostics.First();
    var diagnosticSpan = diagnostic.Location.SourceSpan;
    // Find the property declaration identified by the diagnostic.
    var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().First();
}
```

All there is left to do now is implement a method to change the code, and register it:

```csharp
        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Make property static",
                createChangedDocument: c => MakePropertyStaticAsync(context.Document, declaration, c),
                equivalenceKey: "MakePropertyStatic"),
            diagnostic);
    }

    private async Task<Document> MakePropertyStaticAsync(Document document, PropertyDeclarationSyntax propertyDecl, CancellationToken cancellationToken)
    {
        // Get the symbol representing the type to be renamed.
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Add the static modifier to the property
        editor.SetModifiers(propertyDecl, editor.Generator.GetModifiers(propertyDecl).WithIsStatic(true));

        return editor.GetChangedDocument();
    }
```

Now we should up the version in the `SourceGeneration.csproj` rebuild and then restart Visual Studio. If we revisit the Kiwi property again and press `ctrl + . ` We should be able to tee the codefix.



