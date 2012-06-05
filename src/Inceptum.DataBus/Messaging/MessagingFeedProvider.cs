using Inceptum.Core.Messaging;

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
    /// Messaging feed provider
    /// </summary>
    public class MessagingFeedProvider<TData, TContext> : MessagingFeedProviderBase<TData, TContext>
    {
        private readonly DataBusMessagingEndpoint m_Endpoint;

        /// <summary>
        /// Creates new instance of MessagingFeedProvider
        /// </summary>
        /// <param name="messagingEngine">Messaging engine</param>
        /// <param name="endpoint">Endpoint</param>
        public MessagingFeedProvider(IMessagingEngine messagingEngine, DataBusMessagingEndpoint endpoint)
            : base(messagingEngine)
        {
            m_Endpoint = endpoint;
        }

        /// <summary>
        /// Returns feed provider endpoint
        /// </summary>
        protected override DataBusMessagingEndpoint GetEndpoint(TContext context)
        {
            return m_Endpoint;
        }
    }
}