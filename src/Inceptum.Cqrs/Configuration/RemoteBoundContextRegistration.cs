using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class RemoteBoundContextRegistration : BoundContextRegistration
    {
        public RemoteBoundContextRegistration(string name) : base(name)
        {
        }

        public RemoteListeningCommandsDescriptor ListeningCommands(params Type[] types)
        {
            return new RemoteListeningCommandsDescriptor(types, this);
        }

        public RemotePublishingEventsDescriptor PublishingEvents(params Type[] types)
        {
            return new RemotePublishingEventsDescriptor(types, this);
        }
    }



    public class RemoteListeningCommandsDescriptor
    {
        private readonly RemoteBoundContextRegistration m_Registration;
        private readonly Type[] m_Types;

        public RemoteListeningCommandsDescriptor(Type[] types, RemoteBoundContextRegistration registration)
        {
            m_Types = types;
            m_Registration = registration;
        }

        public RemoteBoundContextRegistration On(string publishEndpoint)
        {
            m_Registration.AddSubscribedCommands(m_Types, publishEndpoint);
            return m_Registration;
        }
    }

    public class RemotePublishingEventsDescriptor
    {
        private readonly Type[] m_Types;
        private readonly RemoteBoundContextRegistration m_Registration;

        public RemotePublishingEventsDescriptor(Type[] types, RemoteBoundContextRegistration registration)
        {
            m_Registration = registration;
            m_Types = types;
        }

        public RemoteBoundContextRegistration To(string listenEndpoint)
        {
            m_Registration.AddSubscribedEvents(m_Types, listenEndpoint);
            return m_Registration;
        }
    }
}