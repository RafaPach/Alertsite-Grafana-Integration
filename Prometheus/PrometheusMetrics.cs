using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NOCAPI.Modules.Alertsite.Prometheus.ModuleRegistry;

namespace NOCAPI.Modules.Users.Prometheus
{
    public class PrometheusMetrics
    {

        // 1) Primary health metric: 0=healthy, 1=unhealthy
        public readonly Gauge _appHealthGauge = AlertsiteRegistryHolder.M.CreateGauge(
            "alertsite_app_health",
            "AlertSite app health (0=unhealthy, 1=healthy).",
            new GaugeConfiguration { LabelNames = new[] { "region", "app" } });

        // 2) Optional: numeric status code (0=OK, non-zero=error code)
        public readonly Gauge _appStatusCodeGauge = AlertsiteRegistryHolder.M.CreateGauge(  
            "alertsite_app_status_code",
            "AlertSite last_status as a numeric code (0=OK).",
            new GaugeConfiguration { LabelNames = new[] { "region", "app" } });

        // 3) Optional: bounded error category (keep set small)
        public readonly Gauge _appErrorCategoryGauge = AlertsiteRegistryHolder.M.CreateGauge(
            "alertsite_app_error_category",
            "AlertSite app state bucketed in a small set of categories.",
            new GaugeConfiguration { LabelNames = new[] { "region", "app", "category" } });


        public readonly Gauge _appResponseSecondsGauge = AlertsiteRegistryHolder.M.CreateGauge(
            "alertsite_app_response_seconds",
            "Latest AlertSite response time in seconds.",
            new GaugeConfiguration { LabelNames = new[] { "region", "app" } });

        public readonly Gauge _appLastStatusTsGauge = AlertsiteRegistryHolder.M.CreateGauge(
            "alertsite_app_last_status_timestamp_seconds",
            "UNIX timestamp (seconds) of the last status reported by AlertSite.",
            new GaugeConfiguration { LabelNames = new[] { "region", "app" } });


        public readonly Gauge _alertsite_heartbeat =
         AlertsiteRegistryHolder.M.CreateGauge(
        "alertsite_heartbeat",
        "Heartbeat from Alertsite Background Service",
        new GaugeConfiguration { LabelNames = new[] { "instance" } });
    }
}
