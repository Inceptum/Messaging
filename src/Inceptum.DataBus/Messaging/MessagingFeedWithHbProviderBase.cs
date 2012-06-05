/*using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Inceptum.Core.Messaging;

namespace Inceptum.DataBus.Messaging
{
    internal class HbFeedResubscriptionPolicy<TData, THeartbeatMessage> : IFeedResubscriptionPolicy where THeartbeatMessage : class
    {
        private readonly HeartbeatsHandler<TData, THeartbeatMessage> m_HbHandler;
        private readonly long m_Timeout;

        public HbFeedResubscriptionPolicy(HeartbeatsHandler<TData, THeartbeatMessage> hbHandler,long timeout)
        {
            m_Timeout = timeout;
            m_HbHandler = hbHandler;
        }

        public IDisposable InitResubscription(Action doSubscribe, Exception exception)
        {
            return m_HbHandler.WaitForHb(doSubscribe,m_Timeout);
        }
    }

    public abstract class MessagingFeedWithHbProviderBase<TData,  TContext, TInitResponse, THeartbeatMessage> :
        MessagingFeedWithHbProviderBase<TData, TData, TContext, TInitResponse, THeartbeatMessage> 
        where THeartbeatMessage : class
    {
        protected MessagingFeedWithHbProviderBase(IMessagingEngine messagingEngine, long retryTimeout = 120000)
            : base(messagingEngine,retryTimeout)
        {
        }

    }

    /// <summary>
    /// Base class for messaging feed providers providing feeds that require reinitialization on hearbeat loss.
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <typeparam name="TInitRequest">The type of the init request.</typeparam>
    /// <typeparam name="TInitResponse">The type of the init response.</typeparam>
    /// <typeparam name="THeartbeatMessage">The type of heartbeat message</typeparam>
    public abstract class MessagingFeedWithHbProviderBase<TData, TMessage, TContext,TInitRequest, TInitResponse, THeartbeatMessage> 
        : MessagingFeedWithInitializationBase<TData, TMessage, TContext,TInitRequest, TInitResponse>
        where THeartbeatMessage : class 
    {
        private const string DEFAULT_HEARTBEAT_QUEUE_NAME = "HEARTBEAT_QUEUE";
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly Dictionary<object, HeartbeatsHandler<TData, THeartbeatMessage>> m_HbHandlersCache = new Dictionary<object, HeartbeatsHandler<TData, THeartbeatMessage>>();
        private readonly long m_RetryTimeout;

        protected MessagingFeedWithHbProviderBase(IMessagingEngine messagingEngine, long retryTimeout = 120000)
            : base(messagingEngine)
        {
            m_RetryTimeout = retryTimeout;
        }


        protected override IDisposable InitializeFeed(Subject<TData> dataFeed, TInitResponse response, TContext context)
        {
            return m_MessagingEngine.Subscribe<THeartbeatMessage>(GetEndpoint(context).HbEndpoint, message => processHeartBeat(context,message,dataFeed));
        }

        Dictionary<object, Handler> m_HbHandlers=new Dictionary<object, Handler>();


        private void processHeartBeat(TContext context,THeartbeatMessage message, Subject<TData> dataFeed)
        {
            
            var heartBeatKey = GetHeartBeatKey(context);
            
        }

        protected abstract HeartbeatInfo GetHeartbeatInfoFromResponse(TInitResponse response, TContext context);


        protected virtual object GetHeartBeatKey(TContext context)
        {
            return context;
        }

        protected virtual string HeartBeatQueue
        {
            get { return DEFAULT_HEARTBEAT_QUEUE_NAME; }
        }


        public override void Dispose()
        {
            foreach (var handler in m_HbHandlersCache.Values)
            {
                handler.Dispose();
            }
            m_HbHandlersCache.Clear();
            lock (_heartbeatSubscriber)
            {

                _heartbeatSubscriber.UnsubscribeAll();
            }
            base.Dispose();
        }
    }
}*/