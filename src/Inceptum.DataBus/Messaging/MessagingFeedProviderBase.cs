using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Castle.Core.Logging;
using Db.Aces.Platform.Transport.Tibco;
using Inceptum.Core;
using Inceptum.Messaging.Contract;


namespace Inceptum.DataBus.Messaging
{
    public abstract class MessagingFeedProviderBase<TData, TContext> : MessagingFeedProviderBase<TData, TData, TContext>
    {
        protected MessagingFeedProviderBase(IMessagingEngine messagingEngine)
            : base(messagingEngine)
        {
        }
    }

    /// <summary>
    /// Base class for SONIC feed providers
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <typeparam name="TMessage">The type of message recieved from tibco</typeparam>
    public abstract class MessagingFeedProviderBase<TData, TMessage, TContext> : IFeedProvider<TData, TContext>, IDisposable
    {
        private readonly CompositeDisposable m_EngineSubscriptions;
        private readonly IMessagingEngine m_MessagingEngine;
        private ILogger m_Logger = NullLogger.Instance;

        public virtual ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value; }
        }

        protected MessagingFeedProviderBase(IMessagingEngine messagingEngine)
        {
            m_MessagingEngine = messagingEngine;
            m_EngineSubscriptions=new CompositeDisposable();
        }


        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }

        public virtual IEnumerable<TData> ExtractData(TMessage message, TContext context)
        {
            if (typeof(TData) == typeof(TMessage))
                return new[] { (TData)(object)message };

            if (typeof(IEnumerable<TData>).IsAssignableFrom(typeof(TMessage)))
                return (IEnumerable<TData>)message;

            throw new NotImplementedException(string.Format("Can not automatically extract IEnumerable<{0}> from instance of {1}. Implement ExtractData in corresponding feed provider", typeof(TData),
                                                            typeof(TMessage)));
        }




        public virtual IObservable<TData> CreateFeed(TContext context)
        {
            return DeferredObservable.CreateWithDisposable<TData>((observer, notefySubscribed) => subscribeObserver(observer, context, notefySubscribed));
        }

        private IDisposable subscribeObserver(IObserver<TData> observer, TContext context, Action notefySubscribed)
        {
            //Create a subject holding data
            var dataFeed = new Subject<TData>();
            var subscribtion = new SafeCompositeDisposable();
            subscribtion.Add(
                dataFeed.Subscribe(
                    observer.OnNext,
                    observer.OnError,
                    observer.OnCompleted
                    ));
            var subscribing = new MultipleAssignmentDisposable();
        	
			subscribing.Disposable = Scheduler
                .Default
        		.Schedule((() =>
        		           	{
        		           		if (subscribtion.IsDisposing || subscribtion.IsDisposed)
        		           			return;
        		           		subscribing.Disposable = Subscribe(dataFeed, context,  notefySubscribed);
        		           	}
        		          ));

            subscribtion.Add(subscribing);
            return subscribtion;
        }

        public virtual IEnumerable<TData> OnFeedLost(TContext context)
        {
            return new TData[0];
        }

        public virtual IFeedResubscriptionPolicy GetResubscriptionPolicy(TContext context)
        {
            return null;
        }

        protected virtual IDisposable Subscribe(Subject<TData> dataFeed, TContext context,  Action notifySubscribed)
        {
            var subscribeForFeedData = SubscribeForFeedData(dataFeed, context, GetEndpoint(context));
            notifySubscribed();
            return subscribeForFeedData;
        }

        protected IDisposable SubscribeForFeedData(Subject<TData> dataFeed, TContext context, Endpoint endpoint)
        {
            try
            {
                Logger.DebugFormat("Subscribing for context: {0};  Endpoint: {1}", GetContextLogRepresentationString(context), endpoint);

                //Subscribe data subject for messaging data flow
                IDisposable engineSubscription = m_MessagingEngine.Subscribe<TMessage>(endpoint, message => processMessage(dataFeed, message, context));
                m_MessagingEngine.SubscribeOnTransportEvents((trasnportId, @event) =>
                                                                 {
																	 if (trasnportId == endpoint.TransportId)
                                                                         dataFeed.OnError(new TransportException(string.Format("Transport {0} failed",trasnportId)));
                                                                 });
                m_EngineSubscriptions.Add(engineSubscription);
                //Unsubscribtion from messaging data flow 
                var subscription = Disposable.Create(() =>
                {
                    engineSubscription.Dispose();
                    m_EngineSubscriptions.Remove(engineSubscription);
                    Logger.DebugFormat("Unsubscribed from context: {0};  Endpoint: {1}", GetContextLogRepresentationString(context),endpoint);
                });
                return subscription;
            }
            catch (Exception ex)
            {
				Logger.Debug(string.Format("Initial subscription failed. Context: {0};   Endpoint: {1}", GetContextLogRepresentationString(context), endpoint), ex);
                dataFeed.OnError(ex);
                return Disposable.Empty;
            }
        }

        private void processMessage(Subject<TData> dataFeed, TMessage message, TContext context)
        {
            foreach (var data in ExtractData(message, context))
            {
                dataFeed.OnNext(data);
            }
        }

        protected virtual string GetContextLogRepresentationString(TContext context)
        {
            return context.ToString();
        }

        public virtual void Dispose()
        {
           m_EngineSubscriptions.Dispose();
        }


        /// <summary>
        /// Gets the subscription endpoint source.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected abstract Endpoint GetEndpoint(TContext context);        

		/// <summary>
		/// Can provide for
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
        public virtual bool CanProvideFor(TContext context)
        {
            return true;
        }
    }
}