using Sonic.Jms;
using QueueConnection = Sonic.Jms.QueueConnection;
using Session = Sonic.Jms.Ext.Session;

namespace Inceptum.Messaging.Sonic
{
    internal class TopicProcessingGroup : ProcessingGroupBase<Session>
    {
        public TopicProcessingGroup(QueueConnection connection, string jailedTag, MessageFormat messageFormat)
            : base(connection, jailedTag, messageFormat)
        {
        }

        protected override Destination CreateDestination(string name)
        {
            return Session.createTopic(name.Substring(8));
        }

        protected override Session CreateSession()
        {
            return (Session) Connection.createSession(false, SessionMode.AUTO_ACKNOWLEDGE);
        }
    }
}