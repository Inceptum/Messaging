using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class CqrsEngine : ICommandSender
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

        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }


        public CqrsEngine(Func<Type, object> dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, params IRegistration[] registrations)
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
                    m_Subscription.Add(m_MessagingEngine.Subscribe(endpoint, @event => context.EventDispatcher.Dispacth(@event), t => { }, eventsSubscription.Value.ToArray()));

                }
            }

            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var commandsSubscription in boundedContext.CommandsSubscriptions)
                {
                    var endpoint = m_EndpointResolver.Resolve(commandsSubscription.Endpoint);
                    BoundedContext context = boundedContext;
                    CommandSubscription commandSubscription = commandsSubscription;
                    m_Subscription.Add(m_MessagingEngine.Subscribe(
                        endpoint,
                        (command, acknowledge) => context.CommandDispatcher.Dispatch(command, commandSubscription.Types[command.GetType()], acknowledge,endpoint),
                        (type, acknowledge) =>
                                 {
                                     throw new InvalidOperationException("Unknown command received: " + type); 
                                     //acknowledge(0, true);
                                 }, 
                        commandSubscription.Types.Keys.ToArray()));
                }
            }

            foreach (var boundedContext in BoundedContexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(this, boundedContext.EventsPublisher));
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var boundedContext in BoundedContexts.Where(b => b != null))
                {

                    if (boundedContext.Processes != null)
                    {
                        foreach (var process in boundedContext.Processes)
                        {
                            process.Dispose();
                        }
                    }

                    boundedContext.Dispose();

                }

                if (m_Subscription != null)
                    m_Subscription.Dispose();
            }
        }

        public void SendCommand<T>(T command,string boundedContext,CommandPriority priority=CommandPriority.Normal )
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(Tuple.Create(typeof (T),priority), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}",boundedContext,typeof(T),priority));
            }
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(endpoint));
        }

        public void ReplayEvents(string boundedContext, params Type[] types)
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
                if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
            if (!context.CommandRoutes.TryGetValue(Tuple.Create(typeof (ReplayEventsCommand),CommandPriority.Normal), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(ReplayEventsCommand), CommandPriority.Normal));
            }

            var ep = m_EndpointResolver.Resolve(endpoint);

            Destination tmpDestination;
            if (context.GetTempDestination(ep.TransportId, () => m_MessagingEngine.CreateTemporaryDestination(ep.TransportId,"EventReplay"), out tmpDestination))
            {
                var replayEndpoint = new Endpoint { Destination = tmpDestination, SerializationFormat = ep.SerializationFormat, SharedDestination = true, TransportId = ep.TransportId };
                var knownEventTypes = context.EventsSubscriptions.SelectMany(e => e.Value).ToArray();
                m_Subscription.Add(m_MessagingEngine.Subscribe(replayEndpoint, @event => context.EventDispatcher.Dispacth(@event), t => { }, "EventReplay",knownEventTypes));
            }
            SendCommand(new ReplayEventsCommand { Destination = tmpDestination.Publish, From = DateTime.MinValue, SerializationFormat = ep.SerializationFormat, Types = types }, boundedContext);
        }

        internal void PublishEvent(object @event,string endpoint)
        {
            PublishEvent(@event, m_EndpointResolver.Resolve(endpoint));
        }

        internal void PublishEvent(object @event,Endpoint endpoint)
        {
            m_MessagingEngine.Send(@event, endpoint);
        }

        internal object ResolveDependency(Type type)
        {
            return m_DependencyResolver(type);
        }
    }
}