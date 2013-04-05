using System;
using System.Threading;

namespace Inceptum.Messaging.Transports
{
    internal class RequestHandle : IDisposable
    {
        private readonly IDisposable m_Subscription;
        private volatile bool m_IsDisposed;
        private readonly ManualResetEvent m_IsComplete = new ManualResetEvent(false);
        private readonly Action<BinaryMessage> m_Callback;
        private readonly Action m_FinishRequest;


        public DateTime DueDate { get; set; }
        public bool IsComplete
        {
            get { return Await(0); }
        }

        public RequestHandle(Action<BinaryMessage> callback, Action finishRequest, Func<Action<BinaryMessage>, IDisposable> subscriber)
        {
            m_FinishRequest = finishRequest;
            m_Callback = callback;
            m_Subscription = subscriber(acceptResponse);

        }

        public bool Await(int timeout)
        {
            return m_IsComplete.WaitOne(timeout);
        }

        private void acceptResponse(BinaryMessage message)
        {
            try
            {
                m_Callback(message);
            }
            finally
            {
                m_IsComplete.Set();
            }
        }

        public void Dispose()
        {
            if (m_IsDisposed)
                return;
            m_IsDisposed = true;
            try
            {
                m_Subscription.Dispose();
            }
            finally
            {
                m_FinishRequest();
            }
        }
    }
}