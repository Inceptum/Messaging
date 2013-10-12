using System;
using System.Threading;

namespace Inceptum.DataBus.Tests.Messaging
{
    internal class ValueWaiter<T>
    {
        private readonly AutoResetEvent m_Ev = new AutoResetEvent(false);
        private T m_Value;

        public T Value
        {
            get { return m_Value; }
            set
            {
                m_Value = value;
                SetTime = DateTime.Now;
                m_Ev.Set();
            }
        }

        public DateTime? SetTime { get; set; }

        public void SetValue(T value)
        {
            Value = value;
        }

        public bool WaitForValue(int timeout)
        {
            return m_Ev.WaitOne(timeout);
        }

        public static implicit operator T(ValueWaiter<T> waiter)
        {
            return waiter.m_Value;
        }
    }
}