using Inceptum.Messaging.Contract;

namespace Inceptum.DataBus.Messaging
{



    /// <summary>
    /// Messaging feed provider
    /// </summary>
    public class MessagingFeedProvider<TData, TContext> : MessagingFeedProviderBase<TData, TContext>
    {
        private readonly Endpoint m_Endpoint;

        /// <summary>
        /// Creates new instance of MessagingFeedProvider
        /// </summary>
        /// <param name="messagingEngine">Messaging engine</param>
        /// <param name="endpoint">Endpoint</param>
        public MessagingFeedProvider(IMessagingEngine messagingEngine, Endpoint endpoint)
            : base(messagingEngine)
        {
            m_Endpoint = endpoint;
        }

        /// <summary>
        /// Returns feed provider endpoint
        /// </summary>
        protected override Endpoint GetEndpoint(TContext context)
        {
            return m_Endpoint;
        }
    }
}