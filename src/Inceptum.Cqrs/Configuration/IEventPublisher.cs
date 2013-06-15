namespace Inceptum.Cqrs.Configuration
{
    internal interface IEventPublisher
    {
        void PublishEvent(object @event);
    }
}