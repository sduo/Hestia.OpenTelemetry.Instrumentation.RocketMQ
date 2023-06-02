using System;
using System.Diagnostics.Tracing;

namespace Hestia.OpenTelemetry.Instrumentation.RocketMQ
{
    [EventSource(Name = "OpenTelemetry-Instrumentation-Rocketmq")]
    internal sealed class InstrumentationEventSource : EventSource
    {
        public static InstrumentationEventSource Log = new();

        [NonEvent]
        public void UnknownErrorProcessingEvent(string @event,Exception ex)
        {
            if (IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                UnknownErrorProcessingEvent(@event, $"{ex.Message}({ex.GetBaseException().Message})");
            }
        }

        [Event(4, Message = "{0} Exception: {1}.", Level = EventLevel.Error)]
        public void UnknownErrorProcessingEvent(string @event, string exception)
        {
            WriteEvent(4, @event,exception);
        }
    }
}
