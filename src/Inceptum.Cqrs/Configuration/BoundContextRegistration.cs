using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class BoundContextRegistration
    {
        readonly Dictionary<Type, string> m_EventsSubscriptions = new Dictionary<Type, string>();
        readonly Dictionary<Type, string> m_CommandsSubscriptions = new Dictionary<Type, string>();
        readonly List<IBoundContextDescriptor> m_Configurators = new List<IBoundContextDescriptor>();
        readonly Dictionary<Type, string> m_CommandRoutes=new Dictionary<Type, string>();
        readonly Dictionary<Type, string> m_EventRoutes=new Dictionary<Type, string>();
        private readonly string m_Name;

        public BoundContextRegistration(string name)
        {
            m_Name = name;
            m_Configurators.Add(new NameDescriptor(name));
            m_Configurators.Add(new SubscriptionDescriptor(m_EventsSubscriptions,m_CommandsSubscriptions));
            m_Configurators.Add(new RoutingDescriptor(m_EventRoutes, m_CommandRoutes));
        }

        internal BoundContext Apply(BoundContext boundContext)
        {
            foreach (var descriptor in m_Configurators)
            {
                descriptor.Create(boundContext);
            }
            return boundContext;
        }

        internal void AddSubscribedEvents(IEnumerable<Type> types, string endpoint)
        {
            foreach (var type in types)
            {
                if (m_CommandsSubscriptions.ContainsKey(type))
                    throw new ConfigurationErrorsException(string.Format("Can not register {0} as event in bound context {1}, it is already registered as command",type, m_Name));
                if (m_CommandsSubscriptions.ContainsValue(endpoint))
                    throw new ConfigurationErrorsException(string.Format("Can not register endpoint '{0}' as command string in bound context {1}, it is already registered as events endpoint", endpoint, m_Name));
                m_EventsSubscriptions.Add(type,endpoint);
            }
        }

        public void AddSubscribedCommands(IEnumerable<Type> types, string endpoint)
        {
            foreach (var type in types)
            {
                if (m_EventsSubscriptions.ContainsKey(type))
                    throw new ConfigurationErrorsException(string.Format("Can not register {0} as command in bound context {1}, it is already registered as event",type, m_Name));
                if (m_EventsSubscriptions.ContainsValue(endpoint))
                    throw new ConfigurationErrorsException(string.Format("Can not register endpoint '{0}' as events string in bound context {1}, it is already registered as commands endpoint", endpoint, m_Name));
                m_CommandsSubscriptions.Add(type, endpoint);
            }
        }

        public void AddCommandsRoute(IEnumerable<Type> types, string endpoint)
        {
            foreach (var type in types)
            {
                if (m_CommandRoutes.ContainsKey(type))
                    throw new ConfigurationErrorsException(string.Format("Route for command '{0}' is already registered", type));
                m_CommandRoutes.Add(type,endpoint); 
            }
        }
  
        
        public void AddEventsRoute(IEnumerable<Type> types, string endpoint)
        {
            foreach (var type in types)
            {
                if (m_EventRoutes.ContainsKey(type))
                    throw new ConfigurationErrorsException(string.Format("Route for event '{0}' is already registered", type));
                m_EventRoutes.Add(type, endpoint); 
            }
        }
    
    }
}