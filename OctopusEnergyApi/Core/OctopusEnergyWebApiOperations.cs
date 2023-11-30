using System.Text.Json;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api.Models;

namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Shared;

public static class OctopusEnergyWebApiOperations
{

    public static async Task<AgilePriceResponse?> GetAgilePriceFromOctopusApi(string productCode, string apiKey, HttpClient _httpClient, int page = 1, string? NextUrl = null, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var unitRateUrl = $"https://api.octopus.energy/v1/products/{productCode}/electricity-tariffs/E-1R-{productCode}-E/standard-unit-rates/";
        if (page > 1)
        {
            unitRateUrl += $"?page={page}";
        }
        else if (!string.IsNullOrEmpty(NextUrl))
        {
            unitRateUrl = NextUrl;
        }
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, unitRateUrl);
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(apiKey)));
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string responseBody = await response.Content.ReadAsStringAsync();

        AgilePriceResponse? priceResponse = JsonSerializer.Deserialize<AgilePriceResponse?>(responseBody, jsonSerializerOptions);
        return priceResponse;
    }

    public static async Task<List<MeterReading>> GetEnergyConsumptionsFromOctopusApi(string mpan, string serial_number, string apiKey, HttpClient _httpClient, DateTime? startDate, DateTime? endDate, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        startDate = startDate ?? DateTime.Now.AddMonths(-1);
        endDate = endDate ?? DateTime.Now;
        var consumptionUrl = $"https://api.octopus.energy/v1/electricity-meter-points/{mpan}/meters/{serial_number}/consumption/?period_from={startDate?.ToString("yyyy-MM-ddTHH:mm:ssZ")}&period_to={endDate?.ToString("yyyy-MM-ddTHH:mm:ssZ")}";

        var meterReadings = new List<MeterReading>();

        do
        {
            var request = MakeRequest(consumptionUrl, apiKey);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var consumptionResponse = JsonSerializer.Deserialize<ConsumptionResponse>(content, jsonSerializerOptions) ?? new ConsumptionResponse();
            meterReadings.AddRange(consumptionResponse.MeterReadings ?? new MeterReading[0]);

            consumptionUrl = $"{consumptionResponse.NextUrl}";
        } while (!string.IsNullOrEmpty(consumptionUrl));

        return meterReadings;

    }

    static HttpRequestMessage MakeRequest(string url, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(apiKey)));
        return request;
    }
}