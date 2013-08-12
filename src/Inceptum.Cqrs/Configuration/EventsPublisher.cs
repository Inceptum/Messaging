using System;

namespace Inceptum.Cqrs.Configuration
{
    class EventsPublisher : IEventPublisher
    {
        private readonly CommandSender m_CommandSender;
        private readonly BoundedContext m_BoundedContext;

        public EventsPublisher(CommandSender commandSender,BoundedContext boundedContext)
        {
            m_BoundedContext = boundedContext;
            m_CommandSender = commandSender;
        }

        public void PublishEvent(object @event)
        {
            if (@event == null) throw new ArgumentNullException("event");
            string endpoint;
            if (!m_BoundedContext.EventRoutes.TryGetValue(@event.GetType(), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", m_BoundedContext.Name, @event.GetType()));
            }
            m_CommandSender.PublishEvent(@event,endpoint);
        }
 
    }
}