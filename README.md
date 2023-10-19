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

# Debugging the source generator
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
Now our debugging tool hit the Debugger line and will hit any subsequent breakppoint