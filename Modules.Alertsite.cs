using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Reflection;
using NOCAPI.Plugins.Config;
using NOCAPI.Modules.Users.Helpers;
using NOCAPI.Modules.Alertsite.Services;
//using NOCAPI.Modules.Alertsite.Initialiser;

namespace NOCAPI.Modules.Alertsite
{
   
    [ApiController]
    [Route("api/alertsite")]
    public class AlertsiteController : ControllerBase
    {

        public AlertsiteController()
        {

            ServiceInitialiser.Initialize();

        }

        [HttpGet("status")]
        public IActionResult GetHealthMetrics()
        {
            try
            {

                var metrics = AlertsiteMetricsBackgroundService.CachedMetrics;

                if (string.IsNullOrWhiteSpace(metrics) || metrics == "# No Prometheus data yet")
                {
                    return Content("# No prometheus metrics yet, waiting for background refresh.", "text/plain");
                }

                return Content(metrics, "text/plain; version=0.0.4");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Failed to fetch metrics.");
            }
        }



    }
}