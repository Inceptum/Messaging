using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class BoundContextRegistration
    {
        readonly Dictionary<Type, Endpoint> m_EventsSubscriptions = new Dictionary<Type, Endpoint>();
        readonly Dictionary<Type, Endpoint> m_CommandsSubscriptions = new Dictionary<Type, Endpoint>();
        readonly List<IBoundContextDescriptor> m_Configurators = new List<IBoundContextDescriptor>();
        readonly Dictionary<Type, Endpoint> m_CommandRoutes=new Dictionary<Type, Endpoint>();
        readonly List<Type> m_SupportedCommands=new List<Type>();    
        readonly Dictionary<Type, Endpoint> m_EventRoutes=new Dictionary<Type, Endpoint>();
        readonly List<Type> m_SupportedEvents=new List<Type>(); 
        
        private readonly string m_Name;

        public BoundContextRegistration(string name)
        {
            m_Name = name;
            m_Configurators.Add(new NameDescriptor(name));
            m_Configurators.Add(new SubscriptionDescriptor(m_EventsSubscriptions,m_CommandsSubscriptions));
        }

        internal BoundContextRegistration AddSubscribedEvents(IEnumerable<Type> types, Endpoint endpoint)
        {
            foreach (var type in types)
            {
                if (m_CommandsSubscriptions.ContainsKey(type))
                    throw new ConfigurationErrorsException(string.Format("Can not register {0} as event in bound context {1}, it is already registered as command",type, m_Name));
                if (m_CommandsSubscriptions.ContainsValue(endpoint))
                    throw new ConfigurationErrorsException(string.Format("Can not register endpoint '{0}' as command endpoint in bound context {1}, it is already registered as events endpoint", endpoint, m_Name));
                m_EventsSubscriptions.Add(type,endpoint);
            }
            return this;
        }

        public BoundContextRegistration AddSubscribedCommands(IEnumerable<Type> types, Endpoint endpoint)
        {
            foreach (var type in types)
            {
                if (m_EventsSubscriptions.ContainsKey(type))
                    throw new ConfigurationErrorsException(string.Format("Can not register {0} as command in bound context {1}, it is already registered as event",type, m_Name));
                if (m_EventsSubscriptions.ContainsValue(endpoint))
                    throw new ConfigurationErrorsException(string.Format("Can not register endpoint '{0}' as events endpoint in bound context {1}, it is already registered as commands endpoint", endpoint, m_Name));
                m_CommandsSubscriptions.Add(type, endpoint);
            }
            return this;
        }

        public void AddCommandsRoute(IEnumerable<Type> types, Endpoint endpoint)
        {
            foreach (var type in types)
            {
                if (m_SupportedCommands.Contains(type))
                    throw new ConfigurationErrorsException(string.Format("Route for command '{0}' is already registered", type));
                m_CommandRoutes.Add(type,endpoint); 
                m_SupportedCommands.Add(type);
            }
        }
        public void AddCommandsLocalRoute(IEnumerable<Type> types)
        {
            var localEndpoint = new Endpoint("inmemory", "inmemory '"+m_Name + "' commands", serializationFormat: "json");
            var typesArray = types as Type[] ?? types.ToArray();
            AddSubscribedCommands(typesArray, localEndpoint);
            AddCommandsRoute(typesArray, localEndpoint);
        } 
        
        public void AddEventsRoute(IEnumerable<Type> types, Endpoint endpoint)
        {
            foreach (var type in types)
            {
                if (m_SupportedEvents.Contains(type))
                    throw new ConfigurationErrorsException(string.Format("Route for event '{0}' is already registered", type));
                m_EventRoutes.Add(type,endpoint); 
                m_SupportedEvents.Add(type);
            }
        }
        public void AddEventsLocalRoute(IEnumerable<Type> types)
        {
            var localEndpoint = new Endpoint("inmemory","inmemory '" + m_Name + "' events", serializationFormat: "json");
            var typesArray = types as Type[] ?? types.ToArray();
            AddSubscribedEvents(typesArray, localEndpoint);
            AddEventsRoute(typesArray, localEndpoint);

        }
    }
}