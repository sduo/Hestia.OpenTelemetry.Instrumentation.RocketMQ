using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hestia.OpenTelemetry.Instrumentation.RocketMQ
{


    public class Instrumentation:IDisposable, IObserver<DiagnosticListener>
    {
        // https://github.com/sduo/Hestia.RocketMQ.SDK4.HTTP/blob/ff203e8a4c2aee507f6cb86a56cb5af27b375664/src/Runtime/Internal/Tracing.cs#L8
        public const string ActivitySourceName = "aliyun.rocketmq.http";
        public const string ActivityIdName = "aliyun.rocketmq.http.traceid";
        
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        private readonly List<IDisposable> subscriptions;
        private long disposed;
        private IDisposable subscription;
        private Func<DiagnosticSourceListener> factory;

        public Instrumentation(Func<DiagnosticSourceListener> factory)
        {
            this.factory = factory;

            subscriptions = new List<IDisposable>();
            if (subscription == null)
            {
                subscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 1)
            {
                return;
            }
            lock (subscriptions)
            {
                foreach (var listener in subscriptions)
                {
                    listener?.Dispose();
                }
                subscriptions.Clear();
            }
            subscription?.Dispose();
            subscription = null;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            if ((Interlocked.Read(ref disposed) == 0) && value.Name == ActivitySourceName)
            {
                lock (subscriptions)
                {
                    var listener = factory.Invoke();
                    subscriptions.Add(value.Subscribe(listener));
                }
            }           
        }
    }
}
