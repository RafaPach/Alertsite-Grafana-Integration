using NOCAPI.Modules.Users.DTOs;
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
            { Region.EMEA, new[] { "Investor Centre", "Issuer Online" } },
            { Region.NA, new[] { "CGS GEMS" } },
            { Region.OCEANIA, new[] { "Issuer", "InvestorVote" } }
        };

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


            
   var filtered = data.Results
        .Where(r => !string.IsNullOrWhiteSpace(r.Devicename) &&
                    filters.Any(f => r.Devicename.Contains(f, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    // Build the list ONCE and return it
    var metrics = new List<PrometheusMetric>(filtered.Count);

    foreach (var r in filtered)
    {
        var isHealthy = r.Laststatus == "0";

                
        var errorText = string.IsNullOrWhiteSpace(r.InfoMsg)
            ? (string.IsNullOrWhiteSpace(r.Laststatusdesc) ? null : r.Laststatusdesc)
            : r.InfoMsg;


        metrics.Add(new PrometheusMetric
        {
            Region = region.ToString(),
            App    = r.Devicename,
            Value  = isHealthy ? 0 : 1,


            // Only emit these when unhealthy to keep JSON clean
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