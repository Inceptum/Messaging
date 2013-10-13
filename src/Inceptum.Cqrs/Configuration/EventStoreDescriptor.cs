using System;
using System.Collections.Generic;
 using Inceptum.Cqrs.EventSourcing;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    internal class EventStoreDescriptor : IBoundedContextDescriptor
    {
        private readonly Func<IDispatchCommits, Wireup> m_ConfigureEventStore;

        public EventStoreDescriptor(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            if (configureEventStore == null) throw new ArgumentNullException("configureEventStore");
            m_ConfigureEventStore = configureEventStore;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, Func<Type, object> resolve)
        {
            IStoreEvents eventStore = m_ConfigureEventStore(new CommitDispatcher(boundedContext.EventsPublisher)).Build();

            boundedContext.EventStore = new NEventStoreAdapter(eventStore);
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {

        }
      
    }
}