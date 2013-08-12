using System;
using System.Collections.Generic;
using System.Reflection;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStore;
using EventStore.Dispatcher;

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

        public IEnumerable<Type> GetDependedncies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, Func<Type, object> resolve)
        {
            var eventStore = m_ConfigureEventStore(new CommitDispatcher(boundedContext.EventsPublisher)).Build();

            boundedContext.Repository = new EventStoreRepository(eventStore, new AggregateFactory(), new ConflictDetector());
        }

        public void Process(BoundedContext boundedContext, CommandSender commandSender)
        {

        }

        public class AggregateFactory : IConstructAggregates
        {
            public IAggregate Build(Type type, Guid id, IMemento snapshot)
            {
                ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Guid), typeof(IMemento) }, null);
                return constructor.Invoke(new object[] {id, snapshot}) as IAggregate;
            }
        }
    }
}