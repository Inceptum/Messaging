namespace Inceptum.Cqrs
{
    public interface ICqrsEngine
    {
        void Init();
        void SendCommand<T>(T command, string boundedContext);
        ICqrsEngine WireEventsListener(object eventListener);
        ICqrsEngine WireCommandsHandler(object commandsHandler, string localBoundedContext);
        void PublishEvent(object @event, string boundedContext);
        bool IsInitialized { get;  }
        event OnInitizlizedDelegate Initialized;

    }
    public delegate void OnInitizlizedDelegate();
}