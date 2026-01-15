
using System.Text.Json.Serialization;

namespace NOCAPI.Modules.Users.DTOs
{
    public class PrometheusMetric
    {
        public string MetricName { get; set; } = "alertsite_app_health";
        public string Region { get; set; } = string.Empty;
        public string App { get; set; } = string.Empty;

        // 0 = healthy, 1 = unhealthy
        public int Value { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StatusDesc { get; set; }

        // If you still want the raw info message for some rows
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? InfoMsg { get; set; }
    }
}
