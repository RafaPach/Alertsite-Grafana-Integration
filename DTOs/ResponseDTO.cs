using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NOCAPI.Modules.Users.DTOs
{
    public class ResponseDTO
    {
        public List<AlertSiteResult> Results { get; set; } = [];
    }

    public class AlertSiteResult
    {

        [JsonPropertyName("device_name")]
        public string Devicename { get; set; }

        [JsonPropertyName("last_status")]
        public string Laststatus { get; set; } = string.Empty;

        [JsonPropertyName("last_status_desc")]
        public string Laststatusdesc { get; set; } = string.Empty;

        [JsonPropertyName("monitor")]
        public string? Monitor { get; set;}

        [JsonPropertyName("dt_last_status")]
        public string Dtlaststatus { get; set; }

        [JsonPropertyName("resptime_last")]
        public string ResponseTime { get; set; }

        [JsonPropertyName("info_msg")]
        public string InfoMsg { get; set; }
    }
 }
