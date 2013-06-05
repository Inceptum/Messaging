using System;
using System.Collections.Generic;
using System.ComponentModel;
using EventStore;
using EventStore.Dispatcher;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HideObjectMembersFromIntellisense
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override Boolean Equals(Object obj)
        {
            return base.Equals(obj);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override Int32 GetHashCode()
        {
            return base.GetHashCode();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override String ToString()
        {
            return base.ToString();
        }

#pragma warning disable 0108
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Type GetType()
        {
            return base.GetType();
        }
#pragma warning restore 0108
    }



    public class Configurator : HideObjectMembersFromIntellisense 
    {
        internal List<BoundContext> LocalBoundContexts { get; set; }
        internal List<BoundContext> RemoteBoundContexts { get; set; }

        public Configurator()
        {
            LocalBoundContexts=new List<BoundContext>();
            RemoteBoundContexts=new List<BoundContext>();
        }

        public LocalBoundContextWrapper WithLocalBoundContext(string name)
        {
            return new LocalBoundContextWrapper(this, name);
        }

        public RemoteBoundContextWrapper WithRemoteBoundContext(string name)
        {
            return new RemoteBoundContextWrapper(this, name);
        }
 

    }

    public class LocalBoundContextWrapper : BoundContextWrapper
    {
        public LocalBoundContextWrapper(Configurator configurator, string name) : base(configurator, name, true)
        {
        }

        public LocalPublishingWrapper PublishingEvents(params Type[] types)
        {
            return new LocalPublishingWrapper(types, this);
        }

        public LocalListeningWrapper ListeningCommands(params Type[] types)
        {
            return new LocalListeningWrapper(types, this);
        }

        public LocalBoundContextWrapper WithEventStore(Func<IDispatchCommits, Wireup> wireUpEventStore)
        {
            BoundContext.SetEventStoreInitializer(wireUpEventStore);
            return this;
        }

    }

    public class LocalListeningWrapper : ListeningWrapper<LocalBoundContextWrapper>
    {
        public LocalListeningWrapper(Type[] types, LocalBoundContextWrapper boundContextWrapper)
            : base(types, boundContextWrapper)
        {
        }
        public RoutedFromWrapper On(Endpoint endpoint)
        {
            return new RoutedFromWrapper(endpoint, m_Types, m_BoundContextWrapper);
        }
    }

    public class RoutedFromWrapper
    {
         private LocalBoundContextWrapper m_BoundContextWrapper;
        private Type[] m_Types;
        private Endpoint m_PublishEndpoint;

        public RoutedFromWrapper(Endpoint publishEndpoint, Type[] types, LocalBoundContextWrapper boundContextWrapper)
        {
            m_PublishEndpoint = publishEndpoint;
            m_Types = types;
            m_BoundContextWrapper = boundContextWrapper;
        }

        public LocalBoundContextWrapper RoutedFrom(Endpoint endpoint)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_PublishEndpoint,endpoint, m_Types);
            return m_BoundContextWrapper;
        }

        public LocalBoundContextWrapper WithLocalBoundContext(string name)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.Configurator.WithLocalBoundContext(name);
        }

        public RemoteBoundContextWrapper WithRemoteBoundContext(string name)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.Configurator.WithRemoteBoundContext(name);
        }

        public LocalPublishingWrapper PublishingEvents(params Type[] types)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.PublishingEvents(types);
        }


        public LocalBoundContextWrapper WithEventStore(Func<IDispatchCommits, Wireup> wireUpEventStore)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.WithEventStore((wireUpEventStore));
        }

        public LocalListeningWrapper ListeningCommands(params Type[] types)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.ListeningCommands(types);
        }
    }

    public class RemoteListeningWrapper : ListeningWrapper<RemoteBoundContextWrapper>
    {
        public RemoteListeningWrapper(Type[] types, RemoteBoundContextWrapper boundContextWrapper) : base(types, boundContextWrapper)
        {
        }
        public RemoteBoundContextWrapper On(Endpoint endpoint)
        {
            m_BoundContextWrapper.BoundContext.AddCommandsRouting(m_Types, endpoint);
            return m_BoundContextWrapper;
        }
    }

    public class RemoteBoundContextWrapper : BoundContextWrapper
    {
        public RemoteBoundContextWrapper(Configurator configurator, string name)
            : base(configurator, name, false)
        {
        }

        public RemotePublishingWrapper PublishingEvents(params Type[] types)
        {
            return new RemotePublishingWrapper(types, this);
        }

        public RemoteListeningWrapper ListeningCommands(params Type[] types)
        {
            return new RemoteListeningWrapper(types, this);
        }

      
    }

    public class BoundContextWrapper : HideObjectMembersFromIntellisense
    {
        internal Configurator Configurator { get; set; }
        private readonly bool m_IsLocal;
        internal BoundContext BoundContext { get; set; }

        protected BoundContextWrapper(Configurator configurator, string name, bool isLocal)
        {
            m_IsLocal = isLocal;
            Configurator = configurator;
            BoundContext = new BoundContext(name,isLocal);
            if (m_IsLocal)
                Configurator.LocalBoundContexts.Add(BoundContext);
            else
                Configurator.RemoteBoundContexts.Add(BoundContext);
        }

  
        public LocalBoundContextWrapper WithLocalBoundContext(string name)
        {
           
            return Configurator.WithLocalBoundContext(name);
        }

        public RemoteBoundContextWrapper WithRemoteBoundContext(string name)
        {
        
            return Configurator.WithRemoteBoundContext(name);
        }
    }

    public class ListeningWrapper<T> : HideObjectMembersFromIntellisense
        where T: BoundContextWrapper
    {
        private readonly Endpoint m_Endpoint;
        protected readonly T m_BoundContextWrapper;
        protected Type[] m_Types;

        public ListeningWrapper(Type[] types, T boundContextWrapper)
        {
            m_Types = types;
            m_BoundContextWrapper = boundContextWrapper;
        }

    }

    public class PublishingWrapper<T> : HideObjectMembersFromIntellisense
         where T: BoundContextWrapper
    {
        internal readonly T BoundContextWrapper;
        protected readonly Type[] m_Types;

        public PublishingWrapper(Type[] types, T boundContextWrapper)
        {
            m_Types = types;
            BoundContextWrapper = boundContextWrapper;
        }
    }


    public class RemotePublishingWrapper : PublishingWrapper<RemoteBoundContextWrapper>
    {
        public RemotePublishingWrapper(Type[] types, RemoteBoundContextWrapper boundContextWrapper)
            : base(types, boundContextWrapper)
        {
        }

        public RemoteBoundContextWrapper To(Endpoint endpoint)
        {
            BoundContextWrapper.BoundContext.AddEventsRouting(m_Types, endpoint);
            return BoundContextWrapper;
        }
    } 
    
    public class LocalPublishingWrapper : PublishingWrapper<LocalBoundContextWrapper>
    {
        public LocalPublishingWrapper(Type[] types, LocalBoundContextWrapper boundContextWrapper)
            : base(types, boundContextWrapper)
        {
        }

        public RoutedToWrapper To(Endpoint endpoint)
        {
            return new RoutedToWrapper(endpoint, m_Types, BoundContextWrapper);
        }
    }

    public class RoutedToWrapper
    {
        private LocalBoundContextWrapper m_BoundContextWrapper;
        private Type[] m_Types;
        private Endpoint m_PublishEndpoint;

        public RoutedToWrapper(Endpoint publishEndpoint, Type[] types, LocalBoundContextWrapper boundContextWrapper)
        {
            m_PublishEndpoint = publishEndpoint;
            m_Types = types;
            m_BoundContextWrapper = boundContextWrapper;
        }

        public LocalBoundContextWrapper RoutedTo(Endpoint endpoint)
        {
            m_BoundContextWrapper.BoundContext.AddEventsRouting(m_PublishEndpoint,endpoint, m_Types);
            return m_BoundContextWrapper;
        }

        public LocalBoundContextWrapper WithLocalBoundContext(string name)
        {
            m_BoundContextWrapper.BoundContext.AddEventsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.Configurator.WithLocalBoundContext(name);
        }

        public RemoteBoundContextWrapper WithRemoteBoundContext(string name)
        {
            m_BoundContextWrapper.BoundContext.AddEventsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.Configurator.WithRemoteBoundContext(name);
        }

        public LocalPublishingWrapper PublishingEvents(params Type[] types)
        {
            m_BoundContextWrapper.BoundContext.AddEventsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.PublishingEvents(types);
        }


        public LocalBoundContextWrapper WithEventStore(Func<IDispatchCommits, Wireup> wireUpEventStore)
        {
            m_BoundContextWrapper.BoundContext.AddEventsRouting(m_Types, m_PublishEndpoint);
            return m_BoundContextWrapper.WithEventStore((wireUpEventStore));
        }
    }
}