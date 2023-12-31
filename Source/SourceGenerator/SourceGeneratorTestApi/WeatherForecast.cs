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
