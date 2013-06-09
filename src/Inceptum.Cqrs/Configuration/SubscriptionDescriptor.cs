using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    class SubscriptionDescriptor : IBoundContextDescriptor
    {
        private readonly Dictionary<Type, Endpoint> m_EventsSubscriptions;
        private readonly Dictionary<Type, Endpoint> m_CommandsSubscriptions;

        public SubscriptionDescriptor(Dictionary<Type, Endpoint> eventsSubscriptions, Dictionary<Type, Endpoint> commandsSubscriptions)
        {
            m_CommandsSubscriptions = commandsSubscriptions;
            m_EventsSubscriptions = eventsSubscriptions;
        }

        public void Apply(BC boundContext)
        {
            var eventSubscriptions = from pair in m_EventsSubscriptions
                                     group pair by pair.Value
                                     into grouping
                                     select new { endpoint = grouping.Key, types = grouping.Select(g => g.Key) };
            boundContext.EventsSubscriptions = eventSubscriptions.ToDictionary(o => o.endpoint, o => o.types);
            var commandsSubscriptions = from pair in m_CommandsSubscriptions
                                        group pair by pair.Value
                                        into grouping
                                        select new {endpoint = grouping.Key, types = grouping.Select(g => g.Key)};
            boundContext.CommandsSubscriptions = commandsSubscriptions.ToDictionary(o => o.endpoint, o => o.types);
        }
    }
}