using System;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    internal class GenericMessageListener : MessageListener
    {
        private readonly Action<Message> m_OnMessageImpl;

        public GenericMessageListener(Action<Message> onMessage)
        {
            m_OnMessageImpl = onMessage;
        }

        #region MessageListener Members

        public void onMessage(Message message)
        {
            m_OnMessageImpl(message);
        }

        #endregion
    }
}
