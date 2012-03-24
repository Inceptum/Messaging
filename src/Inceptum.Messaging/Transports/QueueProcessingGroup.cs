using Sonic.Jms;
using QueueConnection = Sonic.Jms.QueueConnection;
using QueueSession = Sonic.Jms.Ext.QueueSession;

namespace Inceptum.Messaging.Transports
{
    internal class QueueProcessingGroup : ProcessingGroup<QueueSession>
    {
        public QueueProcessingGroup(QueueConnection connection, string jailedTag)
            : base(connection,jailedTag)
        {

        }

        protected override Destination CreateDestination(string name)
        {
            return Session.createQueue(name.Substring(8));
        }

        protected override QueueSession CreateSession()
        {
            return (QueueSession)Connection.createQueueSession(false, SessionMode.AUTO_ACKNOWLEDGE);
        }

        
    }
}