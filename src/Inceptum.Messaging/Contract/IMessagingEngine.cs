using System;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.Contract
{
    public enum TransportEvents
    {
        None,
        Failure
    }

    public delegate void TrasnportEventHandler(string transportId, TransportEvents @event);
     
    public interface IMessagingEngine:IDisposable
    {
        ISerializationManager SerializationManager { get; }
        IDisposable SubscribeOnTransportEvents(TrasnportEventHandler handler);
        void Send<TMessage>(TMessage message, Endpoint endpoint);
        void Send<TMessage>(TMessage message, Endpoint endpoint, int ttl);
        void Send(object message, Endpoint endpoint);
        IDisposable Subscribe<TMessage>(Endpoint endpoint, Action<TMessage> callback);
        IDisposable Subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, params Type[] knownTypes);
        TResponse SendRequest<TRequest, TResponse>(TRequest request, Endpoint endpoint, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT);
        IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, Endpoint endpoint, Action<TResponse> callback, Action<Exception> onFailure, long timeout = TransportConstants.DEFAULT_REQUEST_TIMEOUT);
		IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, Endpoint endpoint) where TResponse : class;
    }
}
