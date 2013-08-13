using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    public interface IRegistration
    {
        void Create(CqrsEngine cqrsEngine);
        void Process(CqrsEngine cqrsEngine);
        IEnumerable<Type> Dependencies { get; }
    }

    public class BoundedContextRegistration : IRegistration
    {
        readonly Dictionary<Type, string> m_EventsSubscriptions = new Dictionary<Type, string>();
        readonly Dictionary<Type, string> m_CommandsSubscriptions = new Dictionary<Type, string>();
        readonly List<IBoundedContextDescriptor> m_Configurators = new List<IBoundedContextDescriptor>();
        readonly Dictionary<Type, string> m_CommandRoutes=new Dictionary<Type, string>();
        readonly Dictionary<Type, string> m_EventRoutes=new Dictionary<Type, string>();

        Type[] m_Dependencies=new Type[0];
        private readonly string m_Name;

        public IEnumerable<Type> Dependencies
        {
            get { return m_Dependencies; }
        }


        public string Name
        {
            get { return m_Name; }
        }

        protected BoundedContextRegistration(string name)
        {
            m_Name = name;
            AddDescriptor(new SubscriptionDescriptor(m_EventsSubscriptions, m_CommandsSubscriptions));
            AddDescriptor(new RoutingDescriptor(m_EventRoutes, m_CommandRoutes));
        }

        protected internal void AddDescriptor(IBoundedContextDescriptor descriptor)
        {
            m_Dependencies = m_Dependencies.Concat(descriptor.GetDependedncies()).Distinct().ToArray();
            m_Configurators.Add(descriptor);
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            var boundedContext=new BoundedContext(cqrsEngine,Name);
            foreach (var descriptor in m_Configurators)
            {
                descriptor.Create(boundedContext, cqrsEngine.ResolveDependency);
            }
            cqrsEngine.BoundedContexts.Add(boundedContext);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            var boundedContext = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == Name);
            foreach (var descriptor in m_Configurators)
            {
                descriptor.Process(boundedContext, cqrsEngine);
            }
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


        protected void RegisterProjections(object projection, string fromBoundContext)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            AddDescriptor(new ProjectionDescriptor(projection, fromBoundContext));
        }  
        
        protected void RegisterProjections(Type projection, string fromBoundContext)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            AddDescriptor(new ProjectionDescriptor(projection, fromBoundContext));
        }
 
    }
}