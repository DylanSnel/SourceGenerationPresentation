using SourceGeneration;

namespace SourceGeneratorTestApi.Enums;

public record Fruits : MyEnum<Fruit>
{

    public static Fruit Apple { get; } = new() { Name = "Apple", Description = "A red fruit" };
    public static Fruit Banana { get; } = new() { Name = "Banana", Description = "A yellow fruit" };
}

