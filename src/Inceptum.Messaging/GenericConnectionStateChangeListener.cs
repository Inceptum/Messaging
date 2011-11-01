using System;
using Sonic.Jms.Ext;

namespace Inceptum.Messaging
{
    internal class GenericConnectionStateChangeListener : ConnectionStateChangeListener
    {
        private readonly Action<int> m_Handler;

        public GenericConnectionStateChangeListener(Action<int> handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            m_Handler = handler;
        }

        public void connectionStateChanged(int state)
        {
            m_Handler(state);
        }
    }
}
