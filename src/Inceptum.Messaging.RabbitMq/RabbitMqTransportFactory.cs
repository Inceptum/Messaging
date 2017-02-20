using System;
using System.Linq;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.RabbitMq
{
    /// <summary>
    /// Implementation of <see cref="ITransportFactory"/> interface for RabbitMQ
    /// </summary>
    public class RabbitMqTransportFactory : ITransportFactory
    {
        /// <summary>
        /// Creates new instance of <see cref="RabbitMqTransportFactory"/> with RabbitMQ native automatic recovery disabled
        /// </summary>
        public RabbitMqTransportFactory()
            : this(true, default (TimeSpan?))
        {

        }

        /// <summary>
        /// Creates new instance of <see cref="RabbitMqTransportFactory"/> with RabbitMQ native automatic recovery enabled
        /// </summary>
        /// <param name="automaticRecoveryInterval">TimeStamp to enable auto recovery for underlying RabbitMQ client. Use TimeStamp.FromSeconds(5).</param>
        public RabbitMqTransportFactory(TimeSpan automaticRecoveryInterval)
             : this(true, automaticRecoveryInterval)
        {

        }

        /// <summary>
        /// Creates new instance of <see cref="RabbitMqTransportFactory"/>
        /// </summary>
        /// <param name="shuffleBrokers">True to shuffle brokers, False to iterate brokers in default order</param>
        /// <param name="automaticRecoveryInterval">Interval for automatic recover if set to null automaitc recovery is disabled, 
        /// if set to some value automatic recovery is enabled and NetworkRecoveryInterval of RabbitMQ client is set provided valie
        /// </param>
        internal RabbitMqTransportFactory(bool shuffleBrokers, TimeSpan? automaticRecoveryInterval = default(TimeSpan?))
        {
            m_ShuffleBrokers = shuffleBrokers;
            m_AutomaticRecoveryInterval = automaticRecoveryInterval;
        }

        private readonly bool m_ShuffleBrokers;
        private readonly TimeSpan? m_AutomaticRecoveryInterval;

        public string Name
        {
            get { return "RabbitMq"; }
        }

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            var brokers = transportInfo.Broker.Split(',').Select(b => b.Trim()).ToArray();
            return new RabbitMqTransport(
                brokers,
                transportInfo.Login,
                transportInfo.Password,
                m_ShuffleBrokers,
                m_AutomaticRecoveryInterval
            );
        }
    }
}