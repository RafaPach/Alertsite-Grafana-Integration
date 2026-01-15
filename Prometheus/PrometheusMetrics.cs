using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NOCAPI.Modules.Users.Prometheus
{
    public class PrometheusMetrics
    {

        // 1) Primary health metric: 0=healthy, 1=unhealthy
        public readonly Gauge _appHealthGauge = Metrics.CreateGauge(
            "alertsite_app_health",
            "AlertSite app health (1=healthy, 0=unhealthy).",
            new GaugeConfiguration { LabelNames = new[] { "region", "app" } });

        // 2) Optional: numeric status code (0=OK, non-zero=error code)
        public readonly Gauge _appStatusCodeGauge = Metrics.CreateGauge(
            "alertsite_app_status_code",
            "AlertSite last_status as a numeric code (0=OK).",
            new GaugeConfiguration { LabelNames = new[] { "region", "app" } });

        // 3) Optional: bounded error category (keep set small)
        public readonly Gauge _appErrorCategoryGauge = Metrics.CreateGauge(
            "alertsite_app_error_category",
            "AlertSite app state bucketed in a small set of categories.",
            new GaugeConfiguration { LabelNames = new[] { "region", "app", "category" } });

    }
}
