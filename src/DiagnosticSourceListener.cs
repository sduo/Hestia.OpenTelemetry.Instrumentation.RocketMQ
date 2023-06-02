using Aliyun.MQ.Model;
using Aliyun.MQ.Runtime;
using Aliyun.MQ.Runtime.Internal;
using Aliyun.MQ.Util;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ExecutionContext = Aliyun.MQ.Runtime.Internal.ExecutionContext;

namespace Hestia.OpenTelemetry.Instrumentation.RocketMQ
{
    public class DiagnosticSourceListener : IObserver<KeyValuePair<string, object>>
    {

        Func<string,string, string> filter;
        IDictionary<string, Func<string>> attach;

        public DiagnosticSourceListener(Func<string, string, string> filter,IDictionary<string,Func<string>> attach)
        {
            this.filter = filter;
            this.attach = attach;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        private void SetTag(Activity activity, string name, string value)
        {
            if (activity is null) { return; }
            if (string.IsNullOrEmpty(name)) { return; }
            if (value is null){return;}
            activity?.SetTag(name, value);
        }


        private void SetFilterTag(Activity activity,string name,string value) 
        {
            SetTag(activity,name, (filter is null) ? (value ?? string.Empty) : filter.Invoke(name, value) );
        }


        private void BuildActivity(Activity activity, ExecutionContext context)
        {            
            SetFilterTag(activity, TraceSemanticConventions.AttributeMessagingSystem, "rocketmq");
            SetFilterTag(activity, TraceSemanticConventions.AttributeMessagingProtocol, "http");
            SetFilterTag(activity, TraceSemanticConventions.AttributeMessagingProtocolVersion, context.RequestContext.ClientConfig.ServiceVersion);
            SetFilterTag(activity, TraceSemanticConventions.AttributeMessagingRocketmqNamespace, $"{context.RequestContext.ClientConfig.RegionEndpoint}");
            SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.ua", $"{context.RequestContext.ClientConfig.UserAgent}");

            if(context.RequestContext.OriginalRequest is WebServiceRequest request && request is not null)
            {
                foreach (var header in request.Headers)
                {
                    SetFilterTag(activity,$"{TraceSemanticConventions.PrefixHttp}.request.{header.Key}", header.Value);
                }
            }   

            if(context.ResponseContext.Response is WebServiceResponse response && response is not null)
            {
                SetFilterTag(activity,$"{TraceSemanticConventions.PrefixHttp}.code", $"{(int)response.HttpStatusCode}");
                foreach (var header in response.Headers)
                {
                    SetFilterTag(activity, $"{TraceSemanticConventions.PrefixHttp}.response.{header.Key}", header.Value);
                } 
            }

            if(attach is not null)
            {
                foreach (var item in attach)
                {
                    SetTag(activity, item.Key, item.Value?.Invoke() ?? string.Empty);
                }
            }            
        }

        public void OnNext(KeyValuePair<string, object> e)
        {
            if(e.Value is not ExecutionContext context)
            {
                return;
            }
            
            try
            {
                switch (e.Key)
                {
                    case "PublishMessageRequestBeforeInvokeSync":
                        {
                            var activity = Instrumentation.ActivitySource.StartActivity("PublishMessage", ActivityKind.Producer, Activity.Current?.Id);
                            BuildActivity(activity, context);
                            if (context.RequestContext.OriginalRequest is PublishMessageRequest request && request is not null)
                            {                                
                                var properties = new Dictionary<string, string>();
                                AliyunSDKUtils.StringToDict(request.Properties, properties);
                                if (properties.ContainsKey(Instrumentation.ActivityIdName))
                                {
                                    properties.Remove(Instrumentation.ActivityIdName);
                                }
                                properties.Add(Instrumentation.ActivityIdName, activity.Id);
                                request.Properties = AliyunSDKUtils.DictToString(properties);
                            }                            
                            break;
                        }
                    case "PublishMessageRequestAfterInvokeSync":
                        {
                            var activity = Activity.Current;
                            BuildActivity(activity, context);
                            if (context.RequestContext.OriginalRequest is PublishMessageRequest request && request is not null
                                && context.ResponseContext.Response is PublishMessageResponse response && response is not null)
                            {
                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.intance", request.IntanceId);
                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.topic", request.TopicName);

                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.id", response.MessageId);
                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.tag", request.MessageTag);
                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.body", request.MessageBody);
                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.properties", request.Properties);
                                SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.md5", response.MessageBodyMD5);                                
                            }
                            activity?.Stop();
                            activity?.Dispose();
                            break;
                        }
                    case "ConsumeMessageRequestAfterInvokeSync":
                        {                            
                            if (context.RequestContext.OriginalRequest is ConsumeMessageRequest request && request is not null
                                &&  context.ResponseContext.Response is ConsumeMessageResponse response && response is not null)
                            {
                                foreach (var message in response.Messages)
                                {
                                    if(message.Properties.TryGetValue(Instrumentation.ActivityIdName, out var aid))
                                    {
                                        message.Properties.Remove(Instrumentation.ActivityIdName);

                                        using var activity = Instrumentation.ActivitySource.StartActivity("ConsumeMessage", ActivityKind.Consumer, aid);
                                        BuildActivity(activity, context);

                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.intance", request.IntanceId);
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.topic", request.TopicName);
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.subscription", request.MessageTag);
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.consumer", request.Consumer);

                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.id", message.Id);
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.tag", message.MessageTag);
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.body", message.Body);
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.properties", AliyunSDKUtils.DictToString(message.Properties));
                                        SetFilterTag(activity, $"{TraceSemanticConventions.PrefixMessagingRocketmq}.md5", message.BodyMD5);                                        
                                    }
                                }
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                InstrumentationEventSource.Log.UnknownErrorProcessingEvent(e.Key, ex);
            }
        }
    }
}
