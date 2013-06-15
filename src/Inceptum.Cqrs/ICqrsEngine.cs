namespace Inceptum.Cqrs
{
    public interface ICqrsEngine
    {
        void Init();
        void SendCommand<T>(T command, string boundContext);
        ICqrsEngine WireEventsListener(object eventListener);
        ICqrsEngine WireCommandsHandler(object commandsHandler, string localBoundContext);
        void PublishEvent(object @event, string boundContext);
        bool IsInitialized { get;  }
        event OnInitizlizedDelegate Initialized;

    }
    public delegate void OnInitizlizedDelegate();
}