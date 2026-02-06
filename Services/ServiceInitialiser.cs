using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOCAPI.Modules.Users.Helpers;
using NOCAPI.Modules.Users.Prometheus;
using System.Net;

namespace NOCAPI.Modules.Alertsite.Services
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

                services.AddMemoryCache();

                services.AddScoped<AlertsiteHelper>();
                services.AddSingleton<TokenService>();
                services.AddSingleton<PrometheusMetrics>();

                services.AddHostedService<AlertsiteMetricsBackgroundService>();

                services.AddHttpClient("Default")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new HttpClientHandler
                        {
                            UseProxy = true,
                            Proxy = WebRequest.GetSystemWebProxy(),
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        });

                ServiceProvider = services.BuildServiceProvider();

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
