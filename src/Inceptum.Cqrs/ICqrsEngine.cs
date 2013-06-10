namespace Inceptum.Cqrs
{
    public interface ICqrsEngine
    {
        void Init();
        void SendCommand<T>(T command, string boundContext);
        ICqrsEngine WireEventsListener(object eventListener);
        ICqrsEngine WireCommandsHandler(object commandsHandler, string localBoundContext);
        void PublishEvent(object @event, string boundContext);
//        void SendLocalCommand<T>(T command, string boundContext);
    }
}