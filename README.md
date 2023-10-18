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