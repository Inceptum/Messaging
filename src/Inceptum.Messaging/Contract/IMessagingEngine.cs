using System;
using System.Collections.Generic;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.Contract
{
    public enum TransportEvents
    {
        None,
        Failure
    }

    public delegate void TransportEventHandler(string transportId, TransportEvents @event);

    /// <summary>
    /// Ack/nack message
    /// </summary>
    /// <param name="delay">number of ms to wait before reporting ack/nack to broker</param>
    /// <param name="acknowledge">if set to <c>true</c> ack , if set to <c>false</c> nack.</param>
    public delegate void AcknowledgeDelegate(long delay,bool acknowledge);
    /// <summary>
    /// Message processing callback
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <param name="message">The message.</param>
    /// <param name="acknowledge">The acknowledge delegate (implementation should call it to report ack/nack to broker).</param>
    public delegate void CallbackDelegate<in TMessage>(TMessage message, AcknowledgeDelegate acknowledge, Dictionary<string, string> headers);
    
    //TODO: CallbackDelegate overloads for SendRequest RegisterHandler
    public interface IMessagingEngine:IDisposable
    {

        void AddProcessingGroup(string name, ProcessingGroupInfo info);
        bool GetProcessingGroupInfo(string name, out ProcessingGroupInfo groupInfo);
        ISerializationManager SerializationManager { get; }
        IDisposable SubscribeOnTransportEvents(TransportEventHandler handler);
        void Send<TMessage>(TMessage message, Endpoint endpoint, string processingGroup = null, Dictionary<string, string> headers = null);

        void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl, string processingGroup = null, Dictionary<string, string> headers = null);
        void Send(object message, Endpoint endpoint, string processingGroup = null, Dictionary<string, string> headers = null);
        Destination CreateTemporaryDestination(string transportId, string processingGroup);

        IDisposable Subscribe<TMessage>(Endpoint endpoint, Action<TMessage> callback);
        IDisposable Subscribe<TMessage>(Endpoint endpoint, CallbackDelegate<TMessage> callback, string processingGroup = null, int priority = 0);
        IDisposable Subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, params Type[] knownTypes);
        IDisposable Subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, string processingGroup, int priority = 0, params Type[] knownTypes);
        //TODO: pass type to callback
        IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<object> callback, Action<string, AcknowledgeDelegate> unknownTypeCallback, params Type[] knownTypes);
        IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<object> callback, Action<string, AcknowledgeDelegate> unknownTypeCallback, string processingGroup, int priority = 0, params Type[] knownTypes);

        TResponse SendRequest<TRequest, TResponse>(TRequest request, Endpoint endpoint, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT);
        IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, Endpoint endpoint, Action<TResponse> callback, Action<Exception> onFailure, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT, string processingGroup = null);
		IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint) where TResponse : class;

        bool VerifyEndpoint(Endpoint endpoint, EndpointUsage usage, bool configureIfRequired, out string error);
        string GetStatistics();

        void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl, ReplyTo replyTo, string processingGroup = null, Dictionary<string, string> headers = null);
        void Send<TMessage>(TMessage message, Endpoint endpoint, ReplyTo replyTo, string processingGroup = null, Dictionary<string, string> headers = null);
    }

    [Flags]
    public enum EndpointUsage
    {
        None,
        Publish,
        Subscribe,
    }
}
