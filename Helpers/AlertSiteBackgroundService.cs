using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOCAPI.Modules.Users.DTOs;
using NOCAPI.Modules.Users.Prometheus;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static NOCAPI.Modules.Alertsite.Prometheus.ModuleRegistry;

namespace NOCAPI.Modules.Users.Helpers
{
    public class AlertsiteMetricsBackgroundService : BackgroundService
    {
        private readonly AlertsiteHelper _alertsiteHelper;
        private readonly TokenService _tokenService;
        private readonly ILogger<AlertsiteMetricsBackgroundService> _logger;
        private readonly PrometheusMetrics _prometheusGauges;

        public static string CachedMetrics = "# No Prometheus data yet";

        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);

        private static int ExtractStatusCode(PrometheusMetric metric)
        {
            if (metric.Value == 0)
                return 0; // ok

            if (metric.InfoMsg?.Contains("500") == true) return 500;
            if (metric.InfoMsg?.Contains("404") == true) return 404;
            if (metric.InfoMsg?.Contains("403") == true) return 403;

            return 1; 
        }

        private static string Categorise(PrometheusMetric metric)
        {
            if (metric.Value == 0)
                return "OK";

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

        private static bool TryParseToUnixSecondsUtc(string? dt, out long unix)
        {
            unix = 0;
            if (string.IsNullOrWhiteSpace(dt)) return false;

            if (DateTime.TryParseExact(
                    dt,
                    "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed))
            {
                var dto = new DateTimeOffset(parsed, TimeSpan.Zero);
                unix = dto.ToUnixTimeSeconds();
                return true;
            }

            return false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Alertsite metrics background service...");

            //_prometheusGauges._appStatusCodeGauge.Unpublish();
            //_prometheusGauges._appHealthGauge.Unpublish();
            //_prometheusGauges._appLastStatusTsGauge.Unpublish();
            //_prometheusGauges._appResponseSecondsGauge.Unpublish();
            //_prometheusGauges._appErrorCategoryGauge.Unpublish();

            _prometheusGauges._alertsite_heartbeat
            .WithLabels(Environment.MachineName)
            .Inc();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var token = await _tokenService.GetAccessTokenAsync(stoppingToken);

                    foreach (var region in _alertsiteHelper.GetRegions().Keys)
                    {
                        //var metrics = await _alertsiteHelper.GetRegionMetricsAsync(token, region);

                        _logger.LogWarning("ALERTSITE: calling API for region {Region}", region);
                        var metrics = await _alertsiteHelper.GetRegionMetricsAsync(token, region);
                        _logger.LogWarning("ALERTSITE: API returned {Count} metrics for region {Region}", metrics.Count, region);

                        foreach (var metric in metrics)
                        {
                            var regionLabel = metric.Region;
                            var appLabel = metric.App;

                            _prometheusGauges._appHealthGauge
                                .WithLabels(regionLabel, appLabel)
                                .Set(metric.Value); 

                            var statusCode = ExtractStatusCode(metric);
                            _prometheusGauges._appStatusCodeGauge
                                .WithLabels(regionLabel, appLabel)
                                .Set(statusCode);

                            var category = Categorise(metric);
                            _prometheusGauges._appErrorCategoryGauge
                                .WithLabels(regionLabel, appLabel, category)
                                .Set(1);

                             if (double.TryParse(metric.ResponseTime, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var rtSeconds))
                                {
                                    _prometheusGauges._appResponseSecondsGauge
                                        .WithLabels(regionLabel, appLabel)
                                        .Set(rtSeconds);
                                }


                            if (TryParseToUnixSecondsUtc(metric.LastStatusAt, out var unix))
                            {
                                _prometheusGauges._appLastStatusTsGauge
                                    .WithLabels(regionLabel, appLabel)
                                    .Set(unix);
                            }


                        }

                        _logger.LogInformation(
                            "Refreshed Alertsite metrics for region {Region}, {Count} apps.",
                            region, metrics.Count);
                    }     
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing Alertsite metrics.");
                }

                try
                {

                    using var stream = new MemoryStream();
                    await AlertsiteRegistryHolder.Registry.CollectAndExportAsTextAsync(stream);
                    stream.Position = 0;
                    using var reader = new StreamReader(stream);
                    CachedMetrics = reader.ReadToEnd();

                    _logger.LogInformation("Prometheus metrics updated.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error exporting Alertsite metrics.");
                }

                await Task.Delay(_refreshInterval, stoppingToken);
            }
         
        }


    }

}
