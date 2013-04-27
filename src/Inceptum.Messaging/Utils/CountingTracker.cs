using System;
using System.Threading;

namespace Inceptum.Core.Utils
{
    public class CountingTracker
    {
        private readonly ManualResetEvent m_IsEmpty = new ManualResetEvent(true);
        private int m_Count;

        private class DisposableTrack:IDisposable
        {
            private readonly Action m_Dispose;

            public DisposableTrack(Action dispose)
            {
                m_Dispose = dispose;
            }

            public void Dispose()
            {
                m_Dispose();
            }
        }

        public IDisposable Track()
        {
            Interlocked.Increment(ref m_Count);
            m_IsEmpty.Reset();
            return new DisposableTrack(decrement);
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
