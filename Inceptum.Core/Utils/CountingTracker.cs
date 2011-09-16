using System;
using System.Reactive.Disposables;
using System.Threading;

namespace Inceptum.Core.Utils
{
    public class CountingTracker
    {
        private readonly ManualResetEvent m_IsEmpty = new ManualResetEvent(true);
        private int m_Count;

        public IDisposable Track()
        {
            Interlocked.Increment(ref m_Count);
            m_IsEmpty.Reset();
            return Disposable.Create(decrement);
        }

        private void decrement()
        {
            if(Interlocked.Decrement(ref m_Count)==0)
                m_IsEmpty.Set();
        }

        public void WaitAll()
        {
            m_IsEmpty.WaitOne();
        }
    }
}