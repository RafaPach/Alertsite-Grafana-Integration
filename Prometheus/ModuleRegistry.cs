using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NOCAPI.Modules.Alertsite.Prometheus
{
    public class ModuleRegistry
    {

        public static class AlertsiteRegistryHolder
        {
            public static readonly CollectorRegistry Registry = new CollectorRegistry();
            public static readonly MetricFactory M = Metrics.WithCustomRegistry(Registry);
        }

    }
}
