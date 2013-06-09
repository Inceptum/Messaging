using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class LocalBoundContextRegistration : BoundContextRegistration
    {
        public LocalBoundContextRegistration(string name)
            : base(name)
        {
        }

        public LocalListeningCommandsDescriptor ListeningCommands(params Type[] types)
        {
            return new LocalListeningCommandsDescriptor(types, this);
        }

        public LocalPublishingEventsDescriptor PublishingEvents(params Type[] types)
        {
            return new LocalPublishingEventsDescriptor(types, this);
        }
    }




    public class LocalListeningCommandsDescriptor
    {
        private readonly LocalBoundContextRegistration m_Registration;
        private readonly Type[] m_Types;

        public LocalListeningCommandsDescriptor(Type[] types, LocalBoundContextRegistration registration)
        {
            m_Types = types;
            m_Registration = registration;
        }

        public RoutedFromDescriptor On(Endpoint listenEndpoint)
        {
            return new RoutedFromDescriptor(m_Registration, m_Types, listenEndpoint);
        }

        public LocalBoundContextRegistration Locally()
        {
            m_Registration.AddCommandsLocalRoute(m_Types);
            return m_Registration;
        }
    }

    public class RoutedFromDescriptor
    {
        private readonly LocalBoundContextRegistration m_Registration;
        private readonly Type[] m_Types;
        private readonly Endpoint m_ListenEndpoint;

        public RoutedFromDescriptor(LocalBoundContextRegistration registration, Type[] types, Endpoint listenEndpoint)
        {
            m_ListenEndpoint = listenEndpoint;
            m_Types = types;
            m_Registration = registration;
        }

        public LocalBoundContextRegistration RoutedFrom(Endpoint publishEndpoint)
        {
            m_Registration.AddCommandsRoute(m_Types, publishEndpoint);
            m_Registration.AddSubscribedCommands(m_Types, m_ListenEndpoint);
            return m_Registration;
        }
        public LocalBoundContextRegistration RoutedToSameEndpoint( )
        {
            return RoutedFrom(m_ListenEndpoint);
        }
    }

    public class LocalPublishingEventsDescriptor
    {
        private readonly Type[] m_Types;
        private readonly LocalBoundContextRegistration m_Registration;

        public LocalPublishingEventsDescriptor(Type[] types, LocalBoundContextRegistration registration)
        {
            m_Registration = registration;
            m_Types = types;
        }

        public RoutedToDescriptor To(Endpoint publishEndpoint)
        {
            return new RoutedToDescriptor(m_Registration, m_Types, publishEndpoint);
        }


        public LocalBoundContextRegistration Locally()
        {

            m_Registration.AddEventsLocalRoute(m_Types);
            return m_Registration;
        }
    }

    public class RoutedToDescriptor  
    {
        private readonly LocalBoundContextRegistration m_Registration;
        private readonly Type[] m_Types;
        private readonly Endpoint m_PublishEndpoint;

        public RoutedToDescriptor(LocalBoundContextRegistration registration, Type[] types, Endpoint publishEndpoint)
        {
            m_PublishEndpoint = publishEndpoint;
            m_Types = types;
            m_Registration = registration;
        }

        public LocalBoundContextRegistration RoutedTo(Endpoint listenEndpoint)
        {
            m_Registration.AddEventsRoute(m_Types, listenEndpoint);
            m_Registration.AddSubscribedEvents(m_Types, m_PublishEndpoint);
            return m_Registration;
        }   
        
        public LocalBoundContextRegistration RoutedToSameEndpoint()
        {
            return RoutedTo(m_PublishEndpoint);
        }
    }
}