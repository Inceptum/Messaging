namespace Inceptum.Cqrs.Configuration
{
    public interface IEventPublisher
    {
        void PublishEvent(object @event);
    }
}