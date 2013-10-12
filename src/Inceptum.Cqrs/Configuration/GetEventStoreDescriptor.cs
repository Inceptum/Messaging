using System;
using System.Collections.Generic;
using System.Reflection;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStore;
using EventStore.ClientAPI;
using EventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    internal class GetEventStoreDescriptor : IBoundedContextDescriptor
    {
        private readonly IEventStoreConnection m_EventStoreConnection;

        public GetEventStoreDescriptor(IEventStoreConnection eventStoreConnection)
        {
            m_EventStoreConnection = eventStoreConnection;
        }

        public IEnumerable<Type> GetDependedncies()
        {
            return new Type[0];
        }

        public void Create(BoundedContext boundedContext, Func<Type, object> resolve)
        {
            boundedContext.Repository = new GetEventStoreRepository(m_EventStoreConnection,boundedContext.EventsPublisher);
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {

        }
    }
}