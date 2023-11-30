using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api.Models;
using System.Text.Json;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.Utils.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Shared;

namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend.OctopusEnergy.Api
{
    public class GetAgileElectricityPrice
    {
        private readonly ILogger _logger;
        private HttpClient _httpClient;


        public GetAgileElectricityPrice(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetAgileElectricityPrice>();
            _httpClient = new HttpClient();
        }

        [Function("GetAgileElectricityPrice")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("GetAgileElectricityPrice http trigger function processed a request.");

            string? productCode;
            string? apiKey;
            string? page;
            string? nextUrl;

            if (req.Method == "GET")
            {
                productCode = req.Query["productCode"];
                // apiKey = req.Query["apiKey"];
                page = req.Query["page"];
                // nextUrl = req.Query["nextUrl"];

                // apikey from header
                IEnumerable<string>? values;
                if (req.Headers.TryGetValues("apiKey", out values))
                {
                    apiKey = values.First();
                }
                else
                {
                    apiKey = null;
                    // bad request, no api key
                    HttpResponseData? response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("No api key provided");
                    return response;
                }

                // nextUrl from header
                if (req.Headers.TryGetValues("nextUrl", out values))
                {
                    nextUrl = values.First();
                }
                else
                {
                    nextUrl = null;
                }

                // bad request, no product code
                if (string.IsNullOrEmpty(productCode))
                {
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("No product code provided");
                    return response;
                }
            }
            else if (req.Method == "POST")
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonContent.Create(requestBody);
                productCode = data?.productCode;
                apiKey = data?.apiKey;
                page = data?.page;
                nextUrl = data?.nextUrl;

                // bad request, no product code, api key
                if (string.IsNullOrEmpty(productCode) || string.IsNullOrEmpty(apiKey))
                {
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("No product code or api key provided");
                    return response;
                }
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.MethodNotAllowed);
                await response.WriteStringAsync("Method not allowed");
                return response;
            }

            // parse get query parameter
            int pageInt = 1;
            if (!string.IsNullOrEmpty(page))
            {
                int.TryParse(page, out pageInt);
            }

            // get agile price from octopus api
            AgilePriceResponse? priceResponse = await OctopusEnergyWebApiOperations.GetAgilePriceFromOctopusApi(productCode, apiKey, _httpClient, pageInt, nextUrl ?? "");

            // return response
            var response2 = req.CreateResponse(HttpStatusCode.OK);
            // await response2.WriteAsJsonAsync(priceResponse);
            response2.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response2.WriteStringAsync(JsonSerializer.Serialize(priceResponse));
            return response2;
        }


    }
}
