using Sonic.Jms;
using QueueConnection = Sonic.Jms.QueueConnection;
using Session = Sonic.Jms.Ext.Session;

namespace Inceptum.Messaging.Transports
{
    internal class TopicProcessingGroup : ProcessingGroup<Session>
    {
        public TopicProcessingGroup(QueueConnection connection, string jailedTag)
            : base(connection, jailedTag)
        {
        }

        protected override Destination CreateDestination(string name)
        {
            return Session.createTopic(name.Substring(8));
        }

        protected override  Session CreateSession()
        {
            return (Session)Connection.createSession(false, SessionMode.AUTO_ACKNOWLEDGE);
        }
    }
}