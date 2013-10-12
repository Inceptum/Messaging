using Sonic.Jms;
using QueueConnection = Sonic.Jms.QueueConnection;
using QueueSession = Sonic.Jms.Ext.QueueSession;

namespace Inceptum.Messaging.Sonic
{
    internal class QueueProcessingGroup : ProcessingGroupBase<QueueSession>
    {
        public QueueProcessingGroup(QueueConnection connection, string jailedTag, MessageFormat messageFormat)
            : base(connection, jailedTag, messageFormat)
        {
        }

        public override Contract.Destination CreateTemporaryDestination()
        {
            return "queue://" + Session.createTemporaryQueue().getQueueName();
        }

        protected override Destination CreateDestination(string name)
        {
            return Session.createQueue(name.Substring(8));
        }

        protected override QueueSession CreateSession()
        {
            return (QueueSession) Connection.createQueueSession(false, SessionMode.AUTO_ACKNOWLEDGE);
        }
    }
}