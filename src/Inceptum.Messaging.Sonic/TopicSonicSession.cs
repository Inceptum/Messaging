using Sonic.Jms;
using QueueConnection = Sonic.Jms.QueueConnection;
using Session = Sonic.Jms.Ext.Session;

namespace Inceptum.Messaging.Sonic
{
    internal class TopicSonicSession : SonicSessionBase<Session>
    {
        public TopicSonicSession(QueueConnection connection, string jailedTag, MessageFormat messageFormat)
            : base(connection, jailedTag, messageFormat)
        {
        }

        public override Contract.Destination CreateTemporaryDestination()
        {
            return "topic://" + Session.createTemporaryTopic().getTopicName();
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