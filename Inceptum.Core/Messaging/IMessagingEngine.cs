using System;

namespace Inceptum.Core.Messaging
{
    public enum TransportEvents
    {
        None,
        Failure
    }

    public delegate void TrasnportEventHandler(string transportId, TransportEvents @event);

    public interface IMessagingEngine:IDisposable
    {
        IDisposable SubscribeOnTransportEvents(TrasnportEventHandler handler);
        void Send<TMessage>(TMessage message, string destination, string transportId);
        IDisposable Subscribe<TMessage>(string source, string transportId, Action<TMessage> callback/*, Action<Exception> onFailure*/);
        TResponse SendRequest<TRequest, TResponse>(TRequest request, string queue, string transportId, int timeout = 30000);
        IDisposable SendRequestAsync<TRequest, TResponse>(TRequest request, string queue, string transportId, Action<TResponse> callback, Action<Exception> onFailure);
        IDisposable RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler, string source, string transportId) where TResponse : class;
    }

    

/*    public interface ITransportEngine
    {
       

        IDisposable Subscribe<TResult>(SubscriptionData flowKey,
                                          DataArrivedEventHandler<TResult, Object> callback,Action<Exception> onFailure);

        IDisposable Subscribe<TResult, TClosure>(SubscriptionData flowKey,
                                                    DataArrivedEventHandler<TResult, TClosure> callback,
                                                    Action<Exception> onFailure,
                                                    TClosure closure);
        void SendCommand(CommandBase command, string transportId);
        bool TrySendCommand(CommandBase command, string transportId);
        void SendRequestAsync<TResponse>(RequestCommandBase<TResponse> command, string transportId, ResponseArrivedHandler<TResponse> callback);
        void SendRequestAsync<TResponse>(RequestCommandBase<TResponse> command, string transportId, ResponseArrivedHandler<TResponse> callback, double timeout);
        TResponse SendRequest<TResponse>(RequestCommandBase<TResponse> command, string transportId, double timeout);

        IFailoverController FailoverController { get; }
    }*/
}