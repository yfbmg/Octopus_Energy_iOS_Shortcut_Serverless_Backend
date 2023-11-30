using System.Net;
using System.Text;
using System.Text.Json;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api.Models;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Shared;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.Utils.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api
{
    public class GetAgilePriceForIOSShortCut
    {
        private readonly ILogger _logger;
        private HttpClient _httpClient;
        private DateTime _lastUpdated;
        private AgilePriceResponse? _priceResponseCache;

        public GetAgilePriceForIOSShortCut(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetAgilePriceForIOSShortCut>();
            _httpClient = new HttpClient();
            _priceResponseCache = null;
            _lastUpdated = DateTime.MinValue;
        }

        [Function("GetAgilePriceForIOSShortCuts")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("GetAgileElectricityPrice http trigger function processed a request.");

            string? apiKey;

            string? productCode;
            string? previousHours;

            productCode = req.Query["productCode"];
            previousHours = req.Query["previousHours"];

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

            // if no previous hour, set to 6
            if (string.IsNullOrEmpty(previousHours))
            {
                previousHours = "6";
            }

            // previous hour must be between 0 and 23, must be an integer
            if (!int.TryParse(previousHours, out int previousHourInt) || previousHourInt < 0 || previousHourInt > 48)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Previous hours must be between 0 and 48, must be an integer");
                return response;
            }

            // if price response cache is null, or last updated is more than 5 minutes ago, get a new price response
            if (_priceResponseCache == null || DateTime.Now.Subtract(_lastUpdated).TotalMinutes > 5)
            {
                // get agile price from octopus api
                _priceResponseCache = await OctopusEnergyWebApiOperations.GetAgilePriceFromOctopusApi(productCode, apiKey, _httpClient);
                _lastUpdated = DateTime.Now;
            }

            // get agile price from octopus api
            AgilePriceResponse? priceResponse = _priceResponseCache;

            // bad request, no price response
            if (priceResponse == null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No price response");
                return response;
            }

            // bad request, no results
            if (priceResponse.Prices == null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No results");
                return response;
            }


            // convert utc to UK time
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            bool isDaylightSavingTime = tz.IsDaylightSavingTime(DateTime.UtcNow);

            var prices = priceResponse.Prices;


            // A set of data needs to be filtered, including all data from 6 hours ago up to all future time.
            // The data is returned in 30 minute intervals, so we need to filter out all data that is not in the previous hour.
            // use local time
            var UtcNow = DateTime.UtcNow;
            var previousHourDateTime = UtcNow.AddHours(-previousHourInt);
            var filteredPrices = prices.Where(p => p.ValidFrom >= previousHourDateTime).OrderBy(p => p.ValidFrom).ToList();


            // bad request, no filtered results
            if (filteredPrices == null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("No filtered results");
                return response;
            }

            var timeConvertedPrices = filteredPrices.Select(p => new AgilePrice
            {
                ValidFrom = TimeZoneInfo.ConvertTimeFromUtc(p.ValidFrom ?? DateTime.UtcNow, tz),
                ValidTo = TimeZoneInfo.ConvertTimeFromUtc(p.ValidTo ?? DateTime.UtcNow, tz),
                ValueIncVat = p.ValueIncVat
            }).ToList();
            // group by date
            var groupedPrices = timeConvertedPrices.GroupBy(p => p.ValidFrom?.Date).ToList();


            // format response for ios shortcut, use local time, convert to string, remove quotes, show in a pretty format
            string header = $"Agile electricity price for {productCode} from {previousHourInt} hours ago to now";
            // note server time is utc, so need to convert to local time

            // string body = string.Join("\n", groupedPrices.Select(g => $"\n{g.Key?.ToLocalTime().ToString("ddd. dd MMM. yyyy")}\n{string.Join("\n", g.Select(p => $"{p.ValidFrom?.ToLocalTime().ToString("HH:mm")}~{p.ValidTo?.ToLocalTime().ToString("HH:mm")} - {p.ValueIncVat}p/kWh"))}"));
            // show the prices in a pretty way, grouped by date, and sorted by time, with the time range and price, in pence per kWh, for each time range on each day, with fixed width columns, with determined character widths, , and a blank line between each day, and a blank line at the end
            var template = "{0,-5} ~ {1,-8}  \t  {2,15}\tp/kWh";
            var sb = new StringBuilder();
            foreach (var group in groupedPrices)
            {
                sb.AppendLine(group.Key?.ToLocalTime().ToString("ddd. dd MMM. yyyy"));
                foreach (var price in group)
                {
                    sb.AppendLine(string.Format(template, price.ValidFrom?.ToString("HH:mm"), price.ValidTo?.ToString("HH:mm"), String.Format("{0:0.0000}", price.ValueIncVat)));
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
