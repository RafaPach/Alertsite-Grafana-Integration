//using NOCAPI.Modules.Alertsite.DTOs;
using NOCAPI.Modules.Users.DTOs;

//using NOCAPI.Modules.Users.DTOs;
using Prometheus;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NOCAPI.Modules.Users.Helpers
{
    public class AlertsiteHelper
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public enum Region
        {
            NA,
            EMEA,
            OCEANIA
        }

        private static readonly IReadOnlyDictionary<Region, int> Accounts_CostumerIds =
            new Dictionary<Region, int>
        {
            { Region.EMEA, 24333 },
            { Region.NA, 24332 },
            { Region.OCEANIA, 24334 }
        };

        private static readonly IReadOnlyDictionary<Region, string[]> RegionFilters =
            new Dictionary<Region, string[]>
        {
            { Region.EMEA, new[] { "Investor Centre Responsive - Site Check", "Investor Centre Responsive - Performance", "UK Investor Centre Responsive - Core Functionality Test", "UK Issuer Online - Site Check", "UK Issuer Online - Advanced Search", "UK Issuer Online Core Functionality","EquatePlus","PING", "UK Proxy Vote - Performance", "Sphere", "Global Viewpoint", "Summit" } },
            { Region.NA, new[] { "CGS GEMS", "NA_Issuer_Online_Holder","Equateplus", "InvestorVote","Sphere","Investor Center", "NOC ServiceNow Test Environment"} },
                //{ Region.OCEANIA, new[] { "EquatePlus","IssuerAU_V3.16 - After Hours", "IssuerAU_V3.16 - Core Hours","IssuerNZ - Monitored from NZ_V2.6 - After Hours", "IssuerNZ - Monitored from NZ_V2.6 - Core Hours", "IC3 Registration AU_V5.0 - Core Hours", "InvestorVote", "GEMS"}
                {Region.OCEANIA, new[]{ "EquatePlus", "IssuerAU_V3.16 - After Hours", "IssuerAU_V3.16 - Core Hours", "IssuerNZ - Monitored from NZ_V2.6 - After Hours", "IssuerNZ - Monitored from NZ_V2.6 - Core Hours", "IC3 Registration AU_V5.0 - Core Hours", "InvestorVote", "GEMS" } 
          } 
        };


        private static string NormalizeBusApplication(string app)
        {
            if (string.IsNullOrWhiteSpace(app))
                return string.Empty;

            return app.Replace("Investor Center", "Investor Centre", StringComparison.OrdinalIgnoreCase)
                      .Trim();
        }


        public AlertsiteHelper(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient CreateAuthClient(string token)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token?.Trim());
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public async Task<List<PrometheusMetric>> GetRegionMetricsAsync(
            string token,
            Region region)
        {
            var customerId = Accounts_CostumerIds[region];
            var filters = RegionFilters[region];

            var client = CreateAuthClient(token);

            var url =
                $"https://api.alertsite.com/api/v3/report-sitestatus" +
                $"?showsubaccounts=true&sub_accounts={customerId}&summarize=true";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<ResponseDTO>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (data?.Results == null)
                return [];



            //var filtered = data.Results
            //     .Where(r => !string.IsNullOrWhiteSpace(r.Devicename) &&
            //                 filters.Any(f => r.Devicename.Contains(f, StringComparison.OrdinalIgnoreCase)) && !r.Devicename.Contains("UAT", StringComparison.OrdinalIgnoreCase))
            //     .ToList();




            var filtered = data.Results.Where(r => string.Equals(r.Monitor, "y", StringComparison.OrdinalIgnoreCase))
                 .Where(r => !string.IsNullOrWhiteSpace(r.Devicename) &&
                             filters.Any(f => r.Devicename.Contains(f, StringComparison.OrdinalIgnoreCase) && !r.Devicename.Contains("UAT")) )
                 .ToList();

            var metrics = new List<PrometheusMetric>(filtered.Count);

            foreach (var r in filtered)
            {
                var isHealthy = r.Laststatus == "0";

                
                var errorText = string.IsNullOrWhiteSpace(r.InfoMsg)
                    ? (string.IsNullOrWhiteSpace(r.Laststatusdesc) ? null : r.Laststatusdesc)
                    : r.InfoMsg;

                //Console.WriteLine($"[{r.Devicename}] UAT? {r.Devicename.Contains("UAT")}");


                metrics.Add(new PrometheusMetric
                {
                    Region = region.ToString(),
                    //App    = r.Devicename,
                    App = NormalizeBusApplication(r.Devicename),
                    Value  = isHealthy ? 0 : 1,
                    ResponseTime = r.ResponseTime,
                    LastStatusAt = r.Dtlaststatus,
                    StatusDesc = isHealthy ? null : (string.IsNullOrWhiteSpace(r.Laststatusdesc) ? null : r.Laststatusdesc),
                    InfoMsg = isHealthy ? null : (string.IsNullOrWhiteSpace(r.InfoMsg) ? null : r.InfoMsg),
                });

            }

            return metrics;

        }

        public IReadOnlyDictionary<Region, int> GetRegions() =>
            Accounts_CostumerIds;
    }
}