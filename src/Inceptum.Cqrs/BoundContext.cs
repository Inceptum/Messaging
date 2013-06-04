using System;
using System.Collections.Generic;
using EventStore;
using EventStore.Dispatcher;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class BoundContext
    {
        private readonly List<MessageRouting> m_CommandsRouting = new List<MessageRouting>();
        private readonly List<MessageRouting> m_EventsRouting = new List<MessageRouting>();
        private Func<IDispatchCommits, Wireup> m_EventStoreInitializer;
        private readonly bool m_IsLocal;
        public IStoreEvents EventStore { get; private set; }

        public BoundContext(string name, bool isLocal)
        {
            m_IsLocal = isLocal;
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name should be not empty string", "name");
            Name = name;
        }

        public string Name { get; private set; }

        public IEnumerable<MessageRouting> EventsRouting
        {
            get { return m_EventsRouting.AsReadOnly(); }
        }

        public IEnumerable<MessageRouting> CommandsRouting
        {
            get { return m_CommandsRouting.AsReadOnly(); }
        }

        internal void AddEventsRouting(Type[] types, Endpoint endpoint)
        {
            m_EventsRouting.Add(m_IsLocal
                ?new MessageRouting(types, publishEndpoint:endpoint,subscribeEndpoint:null)
                : new MessageRouting(types, publishEndpoint: null, subscribeEndpoint: endpoint)
                );
        }

        public void AddEventsRouting(Endpoint publishEndpoint, Endpoint subscribeEndpoint, Type[] types)
        {
            m_EventsRouting.Add(new MessageRouting(types, publishEndpoint,subscribeEndpoint));
        }

        internal void AddCommandsRouting(Type[] types, Endpoint endpoint)
        {
            m_CommandsRouting.Add(m_IsLocal
                                      ? new MessageRouting(types, publishEndpoint: null, subscribeEndpoint: endpoint)
                                      : new MessageRouting(types, publishEndpoint: endpoint, subscribeEndpoint: null)
                );
        }

        public void AddCommandsRouting(Endpoint publishEndpoint, Endpoint subscribeEndpoint, Type[] types)
        {
            m_CommandsRouting.Add(new MessageRouting(types, publishEndpoint, subscribeEndpoint));
        }



        public void SetEventStoreInitializer(Func<IDispatchCommits,Wireup> wireUpEventStore)
        {
            m_EventStoreInitializer = wireUpEventStore;
        }

        public void InitEventStore(IDispatchCommits dispatchCommits)
        {
            if(m_EventStoreInitializer!=null)
                EventStore=m_EventStoreInitializer(dispatchCommits).Build();
        }
    }
}