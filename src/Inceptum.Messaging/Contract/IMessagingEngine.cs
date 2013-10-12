using System;
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
    public delegate void CallbackDelegate<in TMessage>(TMessage message, AcknowledgeDelegate acknowledge);
    
    //TODO: CallbackDelegate overloads for SendRequest RegisterHandler
    public interface IMessagingEngine:IDisposable
    {
        ISerializationManager SerializationManager { get; }
        IDisposable SubscribeOnTransportEvents(TransportEventHandler handler);
        void Send<TMessage>(TMessage message, Endpoint endpoint);
        void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl);
        void Send(object message, Endpoint endpoint);
        IDisposable Subscribe<TMessage>(Endpoint endpoint, Action<TMessage> callback);
        IDisposable Subscribe<TMessage>(Endpoint endpoint, CallbackDelegate<TMessage> callback);
        Destination CreateTemporaryDestination(string transportId);

        IDisposable Subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, params Type[] knownTypes);
        IDisposable Subscribe(Endpoint endpoint, CallbackDelegate<object> callback, Action<string, AcknowledgeDelegate> unknownTypeCallback, params Type[] knownTypes);
        TResponse SendRequest<TRequest, TResponse>(TRequest request, Endpoint endpoint, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT);
        IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, Endpoint endpoint, Action<TResponse> callback, Action<Exception> onFailure, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT);
		IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint) where TResponse : class;
    }
}
