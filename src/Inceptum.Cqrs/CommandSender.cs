using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.CompilerServices;
using EventStore;
using EventStore.Dispatcher;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Serialization;
using Inceptum.Messaging.Transports;

namespace Inceptum.Cqrs
{
    class CommitDispatcher : IDispatchCommits
    {
        private readonly IEventPublisher m_EventPublisher;

        public CommitDispatcher(IEventPublisher eventPublisher)
        {
            m_EventPublisher = eventPublisher;
        }

        public void Dispose()
        {
        }

        public void Dispatch(Commit commit)
        {
            foreach (var @event in commit.Events)
            {
                m_EventPublisher.PublishEvent(@event.Body);
            }
        }
    }

    class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string endpoint)
        {
            return new Endpoint("InMemory", endpoint, true, "json");
        }
    }

     
    public class CommandSender : ICommandSender
    {
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        private readonly IEndpointResolver m_EndpointResolver;
        private readonly List<BoundedContext> m_BoundedContexts;
        internal List<BoundedContext> BoundedContexts {
            get { return m_BoundedContexts; }
        }
        private readonly IRegistration[] m_Registrations;
        private readonly Func<Type,object> m_DependencyResolver;

        private readonly bool m_HandleMessagingEngineLifeCycle = false;
        public CommandSender(params IRegistration[] registrations) :
            this(Activator.CreateInstance, new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
            new InMemoryEndpointResolver(),
            registrations
            )
        {
            m_HandleMessagingEngineLifeCycle = true;
        }
        public CommandSender(Func<Type,object> dependencyResolver, params IRegistration[] registrations) :
            this(dependencyResolver,new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
            new InMemoryEndpointResolver(),
            registrations
            )
        {
            m_HandleMessagingEngineLifeCycle = true;
        }


        public CommandSender(Func<Type, object> dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, params IRegistration[] registrations)
        {
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            m_EndpointResolver = endpointResolver;
            m_MessagingEngine = messagingEngine;
            m_BoundedContexts=new List<BoundedContext>();
            init();
            
        }

        private void init()
        {
            foreach (var registration in m_Registrations)
            {
                registration.Create(this);
            }
            foreach (var registration in m_Registrations)
            {
                registration.Process(this);
            }
 
            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var eventsSubscription in boundedContext.EventsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(eventsSubscription.Key);
                    BoundedContext context = boundedContext;
                    subscribe(endpoint, @event => context.EventDispatcher.Dispacth(@event), t => { }, eventsSubscription.Value.ToArray());
                }
            }

            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var commandsSubscription in boundedContext.CommandsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(commandsSubscription.Key);
                    BoundedContext context = boundedContext;
                    subscribe(endpoint, command =>context.CommandDispatcher.Dispacth(command), t=>{throw new InvalidOperationException("Unknown command received: "+t);}, commandsSubscription.Value.ToArray());
                }
            }

            foreach (var boundedContext in BoundedContexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(this, boundedContext.EventsPublisher));
            }
        }

        private void subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, params Type[] knownTypes)
        {
            m_Subscription.Add(m_MessagingEngine.Subscribe(endpoint, callback, unknownTypeCallback, knownTypes));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            foreach (var boundedContext in BoundedContexts)
            {
                if (boundedContext != null && boundedContext.Processes != null)
                {
                    foreach (var process in boundedContext.Processes)
                    {
                        process.Dispose();
                    }
                }
            }

            if (m_Subscription != null)
                m_Subscription.Dispose();
            
            //TODO: investigate why this code crashes in tests 
       /*     if(m_HandleMessagingEngineLifeCycle)
                m_MessagingEngine.Dispose();*/
        }

        public void SendCommand<T>(T command,string boundedContext )
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(typeof (T), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}'",boundedContext,typeof(T)));
            }
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(endpoint));
        }

        internal void PublishEvent(object @event,string endpoint)
        {
            m_MessagingEngine.Send(@event, m_EndpointResolver.Resolve(endpoint));
        }

        internal object ResolveDependency(Type type)
        {
            return m_DependencyResolver(type);
        }
    }
}