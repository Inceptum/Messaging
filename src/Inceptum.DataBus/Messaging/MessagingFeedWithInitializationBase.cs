using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Db.Aces.Platform.Transport.Tibco;
using Inceptum.Messaging.Contract;

namespace Inceptum.DataBus.Messaging
{

    /// <summary>
    /// Base class for messaging feed requiring initialization after subscribtion
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <typeparam name="TInitRequest">The type of the init request.</typeparam>
    /// <typeparam name="TInitResponse">The type of the init response.</typeparam>
     public abstract class MessagingFeedWithInitializationBase<TData, TContext, TInitRequest, TInitResponse>
        : MessagingFeedWithInitializationBase<TData,TData, TContext, TInitRequest, TInitResponse>
     {
         protected MessagingFeedWithInitializationBase(IMessagingEngine messagingEngine, long initTimeout = 30000)
             : base(messagingEngine, initTimeout)
        {
        }

       
     }
    /// <summary>
    /// Base class for messaging feed requiring initialization after subscribtion
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TMessage">The type of message recieved from tibco</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <typeparam name="TInitRequest">The type of the init request.</typeparam>
    /// <typeparam name="TInitResponse">The type of the init response.</typeparam>
    public abstract class MessagingFeedWithInitializationBase<TData, TMessage, TContext, TInitRequest, TInitResponse>
        : MessagingFeedProviderBase<TData, TMessage, TContext>
    {
        public long InitTimeout { get; private set; }

        protected MessagingFeedWithInitializationBase(IMessagingEngine messagingEngine, long initTimeout=30000)
            : base(messagingEngine)
        {
            InitTimeout = initTimeout;
        }

        protected virtual Endpoint GetInitEndpoint(TContext context)
        {
            var endpoint = GetEndpoint(context);
            return new Endpoint
                       {
                           Destination = endpoint.Destination+".init",
                           SharedDestination = false,
                           TransportId = endpoint.TransportId
                       };
        }

        protected override sealed IDisposable Subscribe(Subject<TData> dataFeed, TContext context,   Action notifySubscribed)
        {
            var subscribtion = new SafeCompositeDisposable();
            sendInitCommand(context,  GetInitEndpoint(context), dataFeed, subscribtion, notifySubscribed);
            return subscribtion;
        }

        private void sendInitCommand(TContext context, Endpoint endpoint, Subject<TData> dataFeed, SafeCompositeDisposable subscription, Action notefySubscribed)
        {
            try
            {
                Logger.DebugFormat("Sending init command. Context: {0};   Endpoint: {1}", GetContextLogRepresentationString(context), endpoint);
                TInitRequest initRequest = GetInitRequest(context);
                MessagingEngine.SendRequestAsync<TInitRequest,TInitResponse>(
                    initRequest,
                    endpoint,
                    response => initSubscribtionCallback(response, dataFeed, context, GetEndpoint(context), subscription, notefySubscribed),
                    exception =>{
                            if (subscription.IsDisposing || subscription.IsDisposed)
                                return;

                                    if (exception is TimeoutException)
                            {
                                string errMessage = string.Format("Initial subscription has timed out. Context: {0};   Endpoint: {1}",
                                                                  GetContextLogRepresentationString(context), endpoint);
                                Logger.Warn(errMessage);
                                dataFeed.OnError(new TimeoutException(errMessage, exception));
                                return;
                            }
                            
            
                            Logger.ErrorFormat(exception, "Unexpected error on feed itialization . Context: {0};   Endpoint: {1}", GetContextLogRepresentationString(context), endpoint);
                            dataFeed.OnError(exception);
                    }, InitTimeout);
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format("Initial subscription failed. Context: {0};   Endpoint: {1}", GetContextLogRepresentationString(context), endpoint), ex);
                dataFeed.OnError(ex);
            }
        }

        private void initSubscribtionCallback(TInitResponse initResponse, Subject<TData> dataFeed, TContext context, Endpoint endpoint, SafeCompositeDisposable subscription, Action notefySubscribed)
        {
            try
            {
                //Subscription was canceled before init command response received
                if (subscription.IsDisposing || subscription.IsDisposed)
                    return;


                string error = GetError(initResponse);
                if (error!=null)
                {
                    throw new TransportException("Initial subscription failed: "+ error); 
                }

                subscription.Add(SubscribeForFeedData(dataFeed, context, endpoint));
                var initializeFeedSubscription = InitializeFeed(dataFeed, initResponse, context);
                if (initializeFeedSubscription != null)
                {
                    subscription.Add(initializeFeedSubscription);
                }

                Logger.DebugFormat("Initial subscription successed. Context: {0}", GetContextLogRepresentationString(context));
                foreach (var data in ExtractInitialData(initResponse, context))
                {
                    dataFeed.OnNext(data);
                }
                notefySubscribed();
            }
            catch (TimeoutException ex)
            {
                string errMessage = string.Format("Initial subscription has timed out. Context: {0};   Endpoint: {1}", GetContextLogRepresentationString(context), endpoint);
                Logger.Debug(errMessage);
                dataFeed.OnError(new TimeoutException(errMessage,ex));
                return;
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format("Initial subscription failed. Context: {0}", GetContextLogRepresentationString(context)), ex);
                dataFeed.OnError(ex);
            }
        }

        /// <summary>
        /// Reads error from response. 
        /// </summary>
        /// <param name="initResponse">The init response.</param>
        /// <returns>Error text or null if there is no error</returns>
        protected virtual string GetError(TInitResponse initResponse)
        {
            return null;

        }

        /// <summary>
        /// If implemented in inheritor, extracts initial data from init command response. 
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="context">The context.</param>
        /// <returns>Collection of TData extracted from init response</returns>
        protected abstract IEnumerable<TData> ExtractInitialData(TInitResponse response, TContext context);


        /// <summary>
        /// If implemented in inheritor, initializes the feed once init response is received.
        /// </summary>
        /// <param name="dataFeed">The data feed.</param>
        /// <param name="response">The response.</param>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        protected virtual IDisposable InitializeFeed(Subject<TData> dataFeed, TInitResponse response, TContext context)
        {
            return null;
        }

        /// <summary>
        /// If implemented in inheritor, gets the init command for TIBCO feed.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>Init command</returns>
        protected abstract TInitRequest GetInitRequest(TContext context);
    }
}