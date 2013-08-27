using System;
using System.Linq;
using EventStore;
using EventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public class LocalBoundedContextRegistration : BoundedContextRegistration 
    {
        public LocalBoundedContextRegistration(string name)
            : base(name)
        {
        }
        public LocalBoundedContextRegistration WithCommandsHandler(object handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }
        public LocalBoundedContextRegistration WithCommandsHandler<T>()
        {
            AddDescriptor(new CommandsHandlerDescriptor(typeof(T)));
            return this;
        }  
        
        public LocalBoundedContextRegistration WithCommandsHandlers(params Type[] handlers)
        {
            AddDescriptor(new CommandsHandlerDescriptor(handlers));
            return this;
        }
      
        public LocalBoundedContextRegistration WithCommandsHandler(Type handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }

       public LocalBoundedContextRegistration WithProjection(object projection,string fromBoundContext)
       {
           RegisterProjections(projection, fromBoundContext);
           return this;
       }
 
        public LocalBoundedContextRegistration WithProjection(Type projection,string fromBoundContext)
       {
           RegisterProjections(projection, fromBoundContext);
           return this;
       }

        public LocalBoundedContextRegistration WithProjection<TListener>(string fromBoundContext)
       {
           RegisterProjections(typeof(TListener), fromBoundContext);
           return this;
       }

      

        public LocalListeningCommandsDescriptor ListeningCommands(params Type[] types)
        {
            return new LocalListeningCommandsDescriptor(types, this);
        }

        public LocalPublishingEventsDescriptor PublishingEvents(params Type[] types)
        {
            return new LocalPublishingEventsDescriptor(types, this);
        }

        public LocalBoundedContextRegistration WithProcess(object process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public LocalBoundedContextRegistration WithProcess(Type process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public LocalBoundedContextRegistration WithProcess<TProcess>()
            where TProcess : IProcess
        {
            return WithProcess(typeof(TProcess));
        }

        public LocalBoundedContextRegistration WithEventStore(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            AddDescriptor(new EventStoreDescriptor(configureEventStore));
            return this;
        }
    }

    public class LocalListeningCommandsDescriptor
    {
        private readonly LocalBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;

        public LocalListeningCommandsDescriptor(Type[] types, LocalBoundedContextRegistration registration)
        {
            m_Types = types;
            m_Registration = registration;
        }

        public RoutedFromDescriptor On(params string[] listenEndpoint)
        {
            if(listenEndpoint.Length==0)
                throw new ArgumentException("Endpoint list is empty","listenEndpoint");
            return new RoutedFromDescriptor(m_Registration, m_Types, listenEndpoint);
        }

        
    }

    public class RoutedFromDescriptor
    {
        private readonly LocalBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;
        private readonly string[] m_ListenEndpoint;

        public RoutedFromDescriptor(LocalBoundedContextRegistration registration, Type[] types, string[] listenEndpoint)
        {
            m_ListenEndpoint = listenEndpoint;
            m_Types = types;
            m_Registration = registration;
        }

        public LocalBoundedContextRegistration RoutedFrom(string publishEndpoint)
        {
            m_Registration.AddCommandsRoute(m_Types, publishEndpoint);
            foreach (var endpoint in m_ListenEndpoint)
            {
                m_Registration.AddSubscribedCommands(m_Types, endpoint);
            }
            return m_Registration;
        }

        public LocalBoundedContextRegistration RoutedFromSameEndpoint( )
        {
            return RoutedFrom(m_ListenEndpoint.First());
        }

        public LocalBoundedContextRegistration NotRouted()
        {
            foreach (var endpoint in m_ListenEndpoint)
            {
                m_Registration.AddSubscribedCommands(m_Types, endpoint);
            }
            return m_Registration;
        }
    }

    public class LocalPublishingEventsDescriptor
    {
        private readonly Type[] m_Types;
        private readonly LocalBoundedContextRegistration m_Registration;

        public LocalPublishingEventsDescriptor(Type[] types, LocalBoundedContextRegistration registration)
        {
            m_Registration = registration;
            m_Types = types;
        }

        public RoutedToDescriptor To(string publishEndpoint)
        {
            return new RoutedToDescriptor(m_Registration, m_Types, publishEndpoint);
        }
    }

    public class RoutedToDescriptor  
    {
        private readonly LocalBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;
        private readonly string m_PublishEndpoint;

        public RoutedToDescriptor(LocalBoundedContextRegistration registration, Type[] types, string publishEndpoint)
        {
            m_PublishEndpoint = publishEndpoint;
            m_Types = types;
            m_Registration = registration;
        }

        public LocalBoundedContextRegistration RoutedTo(string listenEndpoint)
        {
            m_Registration.AddEventsRoute(m_Types, m_PublishEndpoint);
            m_Registration.AddSubscribedEvents(m_Types, listenEndpoint);
            return m_Registration;
        }   
        
        public LocalBoundedContextRegistration RoutedToSameEndpoint()
        {
            return RoutedTo(m_PublishEndpoint);
        }  

        public LocalBoundedContextRegistration NotRouted()
        {
            m_Registration.AddEventsRoute(m_Types, m_PublishEndpoint);
            return m_Registration;
        }
    }
}