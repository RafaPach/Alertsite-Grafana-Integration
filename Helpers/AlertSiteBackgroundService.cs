using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOCAPI.Modules.Users.DTOs;
using NOCAPI.Modules.Users.Prometheus;
using Prometheus;

namespace NOCAPI.Modules.Users.Helpers
{
    public class AlertsiteMetricsBackgroundService : BackgroundService
    {
        private readonly AlertsiteHelper _alertsiteHelper;
        private readonly TokenService _tokenService;
        private readonly ILogger<AlertsiteMetricsBackgroundService> _logger;
        private readonly PrometheusMetrics _prometheusGauges;

        public static string CachedMetrics = "# No Prometheus data yet";

        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1); // scrape every 5 min

        private static int ExtractStatusCode(PrometheusMetric metric)
        {
            if (metric.Value == 0)
                return 0; // OK

            if (metric.InfoMsg?.Contains("500") == true) return 500;
            if (metric.InfoMsg?.Contains("404") == true) return 404;
            if (metric.InfoMsg?.Contains("403") == true) return 403;

            return 1; // generic error
        }

        private static string Categorise(PrometheusMetric metric)
        {
            if (metric.Value == 0)
                return "healthy";

            if (metric.InfoMsg == null)
                return "unknown";

            if (metric.InfoMsg.Contains("500"))
                return "http_5xx";

            if (metric.InfoMsg.Contains("404"))
                return "http_404";
            if (metric.InfoMsg.Contains("403"))

                return "http_403";

            if (metric.InfoMsg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "timeout";

            return "other";
        }

        public AlertsiteMetricsBackgroundService(
            AlertsiteHelper alertsiteHelper,
            TokenService tokenService,
            ILogger<AlertsiteMetricsBackgroundService> logger, PrometheusMetrics prometheuGauges)
        {
            _alertsiteHelper = alertsiteHelper;
            _tokenService = tokenService;
            _logger = logger;
            _prometheusGauges = prometheuGauges;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Alertsite metrics background service...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var token = await _tokenService.GetAccessTokenAsync();

                    foreach (var region in _alertsiteHelper.GetRegions().Keys)
                    {
                        var metrics = await _alertsiteHelper.GetRegionMetricsAsync(token, region);

                        foreach (var metric in metrics)
                        {
                            var regionLabel = metric.Region;
                            var appLabel = metric.App;

                            //  Primary health metric
                            _prometheusGauges._appHealthGauge
                                .WithLabels(regionLabel, appLabel)
                                .Set(metric.Value); // already 1 or 0

                            // 2Status code gauge
                            var statusCode = ExtractStatusCode(metric);
                            _prometheusGauges._appStatusCodeGauge
                                .WithLabels(regionLabel, appLabel)
                                .Set(statusCode);

                            // 3Error category gauge (bounded labels)
                            var category = Categorise(metric);
                            _prometheusGauges._appErrorCategoryGauge
                                .WithLabels(regionLabel, appLabel, category)
                                .Set(1);
                        }

                        _logger.LogInformation(
                            "Refreshed Alertsite metrics for region {Region}, {Count} apps.",
                            region, metrics.Count);
                    }

                    using var stream = new MemoryStream();
                    await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
                    stream.Position = 0;
                    using var reader = new StreamReader(stream);
                    CachedMetrics = reader.ReadToEnd();
                    _logger.LogInformation("Premetheus metrics updated.");

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing Alertsite metrics.");
                }

                await Task.Delay(_refreshInterval, stoppingToken);
            }
         
        }


    }

}
