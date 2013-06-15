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
        private readonly string m_BoundContext;

        public CommitDispatcher(ICqrsEngine cqrsEngine,string boundContext)
        {
            m_BoundContext = boundContext;
            m_CqrsEngine = cqrsEngine;
        }

        public void Dispose()
        {
        }

        public void Dispatch(Commit commit)
        {
            foreach (EventMessage @event in commit.Events)
            {
                m_CqrsEngine.PublishEvent(@event.Body,m_BoundContext);
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
        private BoundContext[] m_BoundContexts;
        private readonly BoundContextRegistration[] m_Registrations;
        public event OnInitizlizedDelegate Initialized;

        public EventDispatcher EventDispatcher
        {
            get { return m_EventDispatcher; }
        }

        public CqrsEngine(params BoundContextRegistration[] registrations):
            this(new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
            new InMemoryEndpointResolver(),
            registrations
            )
        {
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver,params BoundContextRegistration[] registrations)
        {
            m_Registrations = registrations;
            m_EndpointResolver = endpointResolver;
            m_MessagingEngine = messagingEngine;
         }

        public void Init()
        {
            m_BoundContexts = m_Registrations.Select(r => r.Apply(new BoundContext())).ToArray();

            foreach (var boundContext in m_BoundContexts)
            {
                foreach (var eventsSubscription in boundContext.EventsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(eventsSubscription.Key);
                    BoundContext context = boundContext;
                    subscribe(endpoint, @event => EventDispatcher.Dispacth(@event, context.Name), t => { }, eventsSubscription.Value.ToArray());
                }
            }

            foreach (var boundContext in m_BoundContexts)
            {
                foreach (var commandsSubscription in boundContext.CommandsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(commandsSubscription.Key);
                    BoundContext context = boundContext;
                    subscribe(endpoint, command => m_CommandDispatcher.Dispacth(command,context.Name), t=>{throw new InvalidOperationException("Unknown command received: "+t);}, commandsSubscription.Value.ToArray());
                }
            }

            var uselessCommandsWirings = m_CommandDispatcher.KnownBoundContexts.Where(kc => m_BoundContexts.All(bc => bc.Name != kc)).ToArray();
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

        public void SendCommand<T>(T command,string boundContext )
        {
            var context = m_BoundContexts.FirstOrDefault(bc => bc.Name == boundContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundContext),"boundContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(typeof (T), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}'",boundContext,typeof(T)));
            }
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(endpoint));
        }
 

        public  void PublishEvent(object @event,string boundContext)
        {
            var context = m_BoundContexts.FirstOrDefault(bc => bc.Name == boundContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found", boundContext), "boundContext");
            string endpoint;
            if (!context.EventRoutes.TryGetValue(@event.GetType(), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", boundContext, @event.GetType()));
            }
            m_MessagingEngine.Send(@event, m_EndpointResolver.Resolve(endpoint));
        }

        public bool IsInitialized { get; private set; }

        public ICqrsEngine WireEventsListener(object eventListener)
        {
            EventDispatcher.Wire(eventListener);
            return this;
        }

        public ICqrsEngine WireCommandsHandler(object commandsHandler, string boundContext)
        {
            //TODO: check that same object is not wired for more then one BC
            m_CommandDispatcher.Wire(commandsHandler,boundContext);
            return this;
        }
    }


}