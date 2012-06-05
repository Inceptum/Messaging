using System;
using System.Collections.Generic;
using Db.Aces.Transport.Commands;
using Db.Aces.Transport;

namespace Db.Aces.Platform.Transport.Tibco
{
    public abstract class TibcoFeedWithInitializationBase<TData, TContext, TInitResponse>
        : TibcoFeedWithInitializationBase<TData, TData, TContext, TInitResponse>
    {
        protected TibcoFeedWithInitializationBase(ITransportEngine transportEngine) : base(transportEngine)
        {
        }
    }

    /// <summary>
    /// Base class for TIBCO feed reuiring initialization after subscribtion
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <typeparam name="TInitResponse">The type of the init response.</typeparam>
    /// <typeparam name="TMessage">The type of message recieved from tibco</typeparam>
    public abstract class TibcoFeedWithInitializationBase<TData, TMessage, TContext, TInitResponse>
        : TibcoFeedProviderBase<TData, TMessage, TContext>
    {
        public virtual long InitTimeout { get; private set; }

        protected TibcoFeedWithInitializationBase(ITransportEngine transportEngine) : base(transportEngine)
        {
            InitTimeout = 30000;
        }

        protected override sealed IDisposable Subscribe(Subject<TData> dataFeed, TContext context, SubscriptionData subscriptionData, Action notifySubscribed)
        {
            SafeCompositeDisposable subscribtion = new SafeCompositeDisposable();
            Logger.Debug("Sending init command. Context: {0};   Subscription data: {1}", GetContextLogRepresentationString(context), subscriptionData);
            sendInitCommand(context, subscriptionData, dataFeed, subscribtion, notifySubscribed);
            return subscribtion;
        }

        private void sendInitCommand(TContext context, SubscriptionData subscriptionData, Subject<TData> dataFeed, SafeCompositeDisposable subscribtion, Action notefySubscribed)
        {
            try
            {
                RequestCommandBase<TInitResponse> command = GetInitCommand(context);
                TransportEngine.SendRequestAsync(command, subscriptionData.TransportId, (sender, args) => initSubscribtionCallback(args, dataFeed, context, subscriptionData, subscribtion, notefySubscribed),
                                                 .001*InitTimeout);
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format("Initial subscription failed. Context: {0};   Subscription data: {1}", GetContextLogRepresentationString(context), subscriptionData), ex);
                dataFeed.OnError(ex);
            }
        }

        private void initSubscribtionCallback(ResponseArrivedEventArgs<TInitResponse> args, Subject<TData> dataFeed, TContext context, SubscriptionData subscriptionData, SafeCompositeDisposable subscription, Action notefySubscribed)
        {
            try
            {
                //Subscription was canceled before init command response received
                if (subscription.IsDisposing || subscription.IsDisposed)
                    return;

                var initResponse = args.Response;

                if (args.HasError)
                {
                    if (args.Error is TransportOutdatedException)
                    {
                        Logger.Info("Failover happend before init command response was recieved. Resending command. Context: {0};   Subscription data: {1}", GetContextLogRepresentationString(context), subscriptionData);
                        sendInitCommand(context, subscriptionData, dataFeed, subscription, notefySubscribed);
                        return;
                    }

                    throw new TransportException("Initial subscription failed",args.Error);
                }


                subscription.Add(SubscribeForFeedData(dataFeed, context, subscriptionData));
                var initializeFeedSubscription = InitializeFeed(dataFeed, initResponse, context, subscriptionData.TransportId);
                if (initializeFeedSubscription != null)
                {
                    subscription.Add(initializeFeedSubscription);
                }
                foreach (var data in ExtractInitialData(initResponse, context))
                {
                    dataFeed.OnNext(data);
                }
                notefySubscribed();
                Logger.Debug("Initial subscription successed. Context: {0}", GetContextLogRepresentationString(context));
            }
            catch (TimeoutException ex)
            {
                string errMessage = string.Format("Initial subscription has timed out. Context: {0};   Subscription data: {1}", GetContextLogRepresentationString(context), subscriptionData);
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
        /// <param name="transportId"></param>
        /// <returns></returns>
        protected virtual IDisposable InitializeFeed(Subject<TData> dataFeed, TInitResponse response, TContext context, string transportId)
        {
            return null;
        }

        /// <summary>
        /// If implemented in inheritor, gets the init command for TIBCO feed.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>Init command</returns>
        protected abstract RequestCommandBase<TInitResponse> GetInitCommand(TContext context);
    }
}