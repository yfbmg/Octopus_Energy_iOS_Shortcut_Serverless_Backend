using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api.Models;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Shared;

namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api
{
    public class GetElectricityConsumptionForIOSShortCuts
    {
        private readonly ILogger _logger;
        private HttpClient _httpClient = new();

        // Use serial number as key, and store the last updated time and the consumption response
        private ConcurrentDictionary<string, (DateTime, List<MeterReading>)> _priceResponseCache = new();
        public GetElectricityConsumptionForIOSShortCuts(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetElectricityConsumptionForIOSShortCuts>();
        }

        [Function("GetElectricityConsumptionForIOSShortCuts")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            Guid guid = Guid.NewGuid();
            _logger.LogInformation($"GetElectricityConsumptionForIOSShortCuts http trigger function processed a request. Guid: {guid}");

            string? apiKey;
            string? serial_number;
            string? mpan;

            string? productCode;
            string? previousDays;

            productCode = req.Query["productCode"];
            previousDays = req.Query["previousDays"];

            // apikey from header
            IEnumerable<string>? headerValues;
            if (req.Headers.TryGetValues("apiKey", out headerValues))
            {
                apiKey = headerValues.First();
            }
            else
            {
                apiKey = null;
                // bad request, no api key
                HttpResponseData? response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No api key provided");
                return response;
            }

            // serial number from header
            if (req.Headers.TryGetValues("serial_number", out headerValues))
            {
                serial_number = headerValues.First();
            }
            else
            {
                serial_number = null;
                // bad request, no serial_number
                HttpResponseData? response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No serial_number provided");
                return response;
            }

            // mpan from header
            if (req.Headers.TryGetValues("mpan", out headerValues))
            {
                mpan = headerValues.First();
            }
            else
            {
                mpan = null;
                // bad request, no mpan
                HttpResponseData? response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No mpan provided");
                return response;
            }


            // if no product code, try header
            if (string.IsNullOrEmpty(productCode))
            {
                if (req.Headers.TryGetValues("productCode", out headerValues))
                {
                    productCode = headerValues.First();
                }
            }

            // bad request, no product code
            if (string.IsNullOrEmpty(productCode))
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No product code provided");
                return response;
            }


            // if no previous days, set to 30
            if (string.IsNullOrEmpty(previousDays))
            {
                previousDays = "30";
            }

            // previous hour must be between 0 and 23, must be an integer
            if (!int.TryParse(previousDays, out int previousDaysInt) || previousDaysInt < 0 || previousDaysInt > 180)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Previous day number must be between 0 and 180, must be an integer");
                return response;
            }


            // try get consumption response from cache
            bool successGetCache = _priceResponseCache.TryGetValue(serial_number, out var value);

            // if last updated is more than 5 minutes ago, get a new consumption response
            if (DateTime.Now.Subtract(value.Item1).TotalMinutes > 5 || !successGetCache)
            {
                var startDate = DateTime.Now.AddDays(-previousDaysInt);
                var endDate = DateTime.Now;
                // get consumption response from octopus api
                var consumptionResponse = await OctopusEnergyWebApiOperations.GetEnergyConsumptionsFromOctopusApi(mpan, serial_number, apiKey, _httpClient, startDate, endDate);
                _priceResponseCache[serial_number] = (DateTime.Now, consumptionResponse);
            }

            // get consumption response from cache
            var consumptionResponseCache = _priceResponseCache[serial_number].Item2;
            if (consumptionResponseCache == null)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("No consumption response, can't read cache");
                return response;
            }


            // convert utc to UK time
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            bool isDaylightSavingTime = tz.IsDaylightSavingTime(DateTime.UtcNow);


            // group by date
            var groupedConsumptions = consumptionResponseCache.GroupBy(c => c.IntervalStart?.Date).OrderByDescending(g => g.Key);



            // format response for ios shortcut, use local time, convert to string, remove quotes, show in a pretty format
            // string header = $"Con {productCode} from {previousDaysInt} hours ago to now";
            string header = $"Consumption for mpan: {mpan}, Serial Number:{serial_number} from {previousDaysInt} days ago to now";
            // note server time is utc, so need to convert to local time

            // string body = string.Join("\n", groupedPrices.Select(g => $"\n{g.Key?.ToLocalTime().ToString("ddd. dd MMM. yyyy")}\n{string.Join("\n", g.Select(p => $"{p.ValidFrom?.ToLocalTime().ToString("HH:mm")}~{p.ValidTo?.ToLocalTime().ToString("HH:mm")} - {p.ValueIncVat}p/kWh"))}"));
            // show the prices in a pretty way, grouped by date, and sorted by time, with the time range and price, in pence per kWh, for each time range on each day, with fixed width columns, with determined character widths, , and a blank line between each day, and a blank line at the end
            var template = "{0,-5} ~ {1,-8}  \t  {2,15}\tp/kWh";
            var sb = new StringBuilder();
            foreach (var group in groupedConsumptions)
            {
                sb.AppendLine(group.Key?.ToLocalTime().ToString("ddd. dd MMM. yyyy"));
                foreach (var consumption in group)
                {

                    sb.AppendLine($"{consumption.IntervalStart?.ToLocalTime().ToString("HH:mm")} ~ {consumption.IntervalEnd?.ToLocalTime().ToString("HH:mm")}  \t  {consumption.ElectricityConsumption?.ToString("0.0000")}\tkWh");
                }
                sb.AppendLine();
            }
            string body = sb.ToString();

            string footer = $"Last updated at {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz):HH:mm}";

            string responseString = $"{header}\n\n{body}\n\n{footer}";

            // return response
            var response2 = req.CreateResponse(HttpStatusCode.OK);
            response2.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await response2.WriteStringAsync(responseString);
            return response2;

        }
    }
}
