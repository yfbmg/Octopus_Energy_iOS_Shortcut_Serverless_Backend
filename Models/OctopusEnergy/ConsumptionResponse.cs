using System.Text.Json.Serialization;


namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api.Models;


public class MeterReadingDS
{
    [JsonPropertyName("consumption")]
    public double? ElectricityConsumption { get; set; }
    [JsonPropertyName("interval_start")]
    public string? IntervalStart { get; set; }
    [JsonPropertyName("interval_end")]
    public string? IntervalEnd { get; set; }
}

public class ConsumptionResponse
{
    [JsonPropertyName("results")]
    public MeterReading[]? MeterReadings { get; set; }
    [JsonPropertyName("count")]
    public long? CosumptionRecordCount { get; set; }
    [JsonPropertyName("next")]
    public string? NextUrl { get; set; }
    [JsonPropertyName("previous")]
    public string? PreviousUrl { get; set; }
}

public class MeterReading
{
    [JsonPropertyName("interval_start")]
    public DateTime? IntervalStart { get; set; }
    [JsonPropertyName("interval_end")]
    public DateTime? IntervalEnd { get; set; }
    [JsonPropertyName("consumption")]
    public double? ElectricityConsumption { get; set; }
}
