using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api.Models;

public class AgilePriceDS
{
    [JsonPropertyName("valid_from")]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("valid_to")]
    public DateTime? ValidTo { get; set; }

    [JsonPropertyName("value_exc_vat")]
    public double? ValueExcVat { get; set; }

    [JsonPropertyName("value_inc_vat")]
    public double? ValueIncVat { get; set; }

    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; set; }
}

public class AgilePriceResponse
{
    [JsonPropertyName("count")]
    public long? PriceRecordCount { get; set; }
    [JsonPropertyName("next")]
    public string? Next { get; set; }
    [JsonPropertyName("previous")]
    public string? Previous { get; set; }
    [JsonPropertyName("results")]
    public AgilePrice[]? Prices { get; set; }
}

public class AgilePrice
{
    [JsonPropertyName("valid_from")]
    public DateTime? ValidFrom { get; set; }
    [JsonPropertyName("valid_to")]
    public DateTime? ValidTo { get; set; }
    [JsonPropertyName("value_exc_vat")]
    public double? ValueExcVat { get; set; }
    [JsonPropertyName("value_inc_vat")]
    public double? ValueIncVat { get; set; }
    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; set; }
}