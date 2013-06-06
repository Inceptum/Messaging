namespace Inceptum.Cqrs
{
    public interface ICqrsEngine
    {
        void Init();
        void SendCommand<T>(T command, string boundContext);
        void WireEventsListener(object eventListener);
        void WireCommandsHandler(object commandsHandler, string localBoundContext);
        void PublishEvent(object @event, string boundContext);
    }
}