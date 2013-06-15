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

/*    public class CommitDispatcher : IDispatchCommits
    {
        private readonly ICqrsEngine m_CqrsEngine;
        private readonly string m_BoundedContext;

        public CommitDispatcher(ICqrsEngine cqrsEngine,string boundedContext)
        {
            m_BoundedContext = boundedContext;
            m_CqrsEngine = cqrsEngine;
        }

        public void Dispose()
        {
        }

        public void Dispatch(Commit commit)
        {
            foreach (EventMessage @event in commit.Events)
            {
                m_CqrsEngine.PublishEvent(@event.Body,m_BoundedContext);
            }
        }
    }*/

    class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string endpoint)
        {
            return new Endpoint("InMemory", endpoint, true, "json");
        }
    }

     
    public class CqrsEngine : ICqrsEngine, IDisposable
    {
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        private readonly IEndpointResolver m_EndpointResolver;
        internal List<BoundedContext> BoundedContexts { get; private set; }
        private readonly IRegistration[] m_Registrations;
        private readonly Func<Type,object> m_DependencyResolver;


        public CqrsEngine(params IRegistration[] registrations) :
            this(Activator.CreateInstance, new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
            new InMemoryEndpointResolver(),
            registrations
            )
        {
        }
        public CqrsEngine(Func<Type,object> dependencyResolver, params IRegistration[] registrations) :
            this(dependencyResolver,new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
            new InMemoryEndpointResolver(),
            registrations
            )
        {
        }


        public CqrsEngine(Func<Type, object> dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, params IRegistration[] registrations)
        {
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            m_EndpointResolver = endpointResolver;
            m_MessagingEngine = messagingEngine;
            BoundedContexts=new List<BoundedContext>();
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

            var uselessCommandsWirings = new string[0];//m_CommandDispatcher.KnownBoundedContexts.Where(kbc => m_BoundedContexts.All(bc => bc.Name != kbc)).ToArray());
            if(uselessCommandsWirings.Any())
                throw new ConfigurationErrorsException(string.Format("Command handlers registered for unknown bound contexts: {0}",string.Join(",",uselessCommandsWirings)));
 
        }

        private void subscribe(Endpoint endpoint, Action<object> callback, Action<string> unknownTypeCallback, params Type[] knownTypes)
        {
            m_Subscription.Add(m_MessagingEngine.Subscribe(endpoint, callback, unknownTypeCallback, knownTypes));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if (m_Subscription != null)
                m_Subscription.Dispose();
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