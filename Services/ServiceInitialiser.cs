using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOCAPI.Modules.Users.Helpers;
using NOCAPI.Modules.Users.Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace NOCAPI.Modules.Alertsite.Initialiser
{
    public class ServiceInitialiser
    {
        private static readonly object _lock = new();
        private static bool _initialized = false;

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public static void Initialize()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                var services = new ServiceCollection();

                // Core services
                services.AddMemoryCache();
                services.AddScoped<AlertsiteHelper>();
                services.AddSingleton<TokenService>();
                services.AddSingleton<PrometheusMetrics>();
                //services.AddHostedService<ZdxBackgroundService>();
                services.AddHostedService<AlertsiteMetricsBackgroundService>();


                // HTTP Client
                services.AddHttpClient("Default")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new HttpClientHandler
                        {
                            UseProxy = true,
                            Proxy = WebRequest.GetSystemWebProxy(),
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        });

                // If you want metrics background refresh:
                // Build container
                ServiceProvider = services.BuildServiceProvider();

                // Start hosted services (ONLY because you do not have Program.cs)
                foreach (var hosted in ServiceProvider.GetServices<IHostedService>())
                {
                    hosted.StartAsync(CancellationToken.None)
                          .GetAwaiter()
                          .GetResult();
                }

                _initialized = true;
            }
        }
    }
}
