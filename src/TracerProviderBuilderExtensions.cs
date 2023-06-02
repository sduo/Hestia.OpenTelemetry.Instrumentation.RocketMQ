using Hestia.OpenTelemetry.Instrumentation.RocketMQ;
using System;
using System.Collections.Generic;

namespace OpenTelemetry.Trace
{
    public static class TracerProviderBuilderExtensions
    {
        public static TracerProviderBuilder AddRocketMQInstrumentation(this TracerProviderBuilder builder, Func<string, string, string> filter, IDictionary<string, Func<string>> attach)
        {

            builder.AddRocketMQInstrumentation(() => { 
                return new DiagnosticSourceListener(filter, attach);
            });            

            return builder;
        }

        public static TracerProviderBuilder AddRocketMQInstrumentation(this TracerProviderBuilder builder, Func<DiagnosticSourceListener> factory)
        {
            AddRocketMQInstrumentationSource(builder);

            builder.AddInstrumentation(() =>
            {
                return new Instrumentation(factory);
            });

            return builder;
        }

        internal static void AddRocketMQInstrumentationSource(TracerProviderBuilder builder)
        {
            builder.AddSource(Instrumentation.ActivitySourceName);
        }
    }
}
