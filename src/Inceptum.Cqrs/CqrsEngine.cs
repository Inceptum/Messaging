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

    public class CommitDispatcher : IDispatchCommits
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
    }

    class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string endpoint)
        {
            return new Endpoint("InMemory", endpoint, true, "json");
        }
    }

    public class CqrsEngine : ICqrsEngine, IDisposable
    {
        
        private readonly EventDispatcher m_EventDispatcher = new EventDispatcher();
        private readonly CommandDispatcher m_CommandDispatcher = new CommandDispatcher();
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        private readonly IEndpointResolver m_EndpointResolver;
        private BoundedContext[] m_BoundedContexts;
        private readonly BoundedContextRegistration[] m_Registrations;
        public event OnInitizlizedDelegate Initialized;

        internal EventDispatcher EventDispatcher
        {
            get { return m_EventDispatcher; }
        }

        public CommandDispatcher CommandDispatcher
        {
            get { return m_CommandDispatcher; }
        }

        public CqrsEngine(params BoundedContextRegistration[] registrations):
            this(new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
            new InMemoryEndpointResolver(),
            registrations
            )
        {
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver,params BoundedContextRegistration[] registrations)
        {
            m_Registrations = registrations;
            m_EndpointResolver = endpointResolver;
            m_MessagingEngine = messagingEngine;
         }

        public void Init()
        {
            m_BoundedContexts = m_Registrations.Select(r => r.Apply(new BoundedContext())).ToArray();

            foreach (var boundedContext in m_BoundedContexts)
            {
                foreach (var eventsSubscription in boundedContext.EventsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(eventsSubscription.Key);
                    BoundedContext context = boundedContext;
                    subscribe(endpoint, @event => EventDispatcher.Dispacth(@event, context.Name), t => { }, eventsSubscription.Value.ToArray());
                }
            }

            foreach (var boundedContext in m_BoundedContexts)
            {
                foreach (var commandsSubscription in boundedContext.CommandsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(commandsSubscription.Key);
                    BoundedContext context = boundedContext;
                    subscribe(endpoint, command => m_CommandDispatcher.Dispacth(command,context.Name), t=>{throw new InvalidOperationException("Unknown command received: "+t);}, commandsSubscription.Value.ToArray());
                }
            }

            var uselessCommandsWirings = m_CommandDispatcher.KnownBoundedContexts.Where(kc => m_BoundedContexts.All(bc => bc.Name != kc)).ToArray();
            if(uselessCommandsWirings.Any())
                throw new ConfigurationException(string.Format("Command handlers registered for unknown bound contexts: {0} ",string.Join(",",uselessCommandsWirings)));
            IsInitialized = true;

            var onInitialized = Initialized;
            if (onInitialized != null)
            {
                onInitialized();
            }
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
            var context = m_BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(typeof (T), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}'",boundedContext,typeof(T)));
            }
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(endpoint));
        }
 

        public  void PublishEvent(object @event,string boundedContext)
        {
            var context = m_BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found", boundedContext), "boundedContext");
            string endpoint;
            if (!context.EventRoutes.TryGetValue(@event.GetType(), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", boundedContext, @event.GetType()));
            }
            m_MessagingEngine.Send(@event, m_EndpointResolver.Resolve(endpoint));
        }

        public bool IsInitialized { get; private set; }

        public ICqrsEngine WireEventsListener(object eventListener)
        {
            EventDispatcher.Wire(eventListener);
            return this;
        }

        public ICqrsEngine WireCommandsHandler(object commandsHandler, string boundedContext)
        {
            //TODO: check that same object is not wired for more then one BC
            m_CommandDispatcher.Wire(commandsHandler,boundedContext);
            return this;
        }
    }


}