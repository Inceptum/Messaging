using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Inceptum.Core.Utils
{
    public class SchedulingBackgroundWorker : IDisposable
    {
        private readonly ManualResetEvent m_StopEvent;
        private readonly AutoResetEvent m_ScheduleEvent = new AutoResetEvent(false);
        private readonly Action m_DoWork;
        private readonly Thread m_Thread;
        readonly List<DateTime> m_ScheduledDates = new List<DateTime>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SchedulingBackgroundWorker"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="doWork">The action to be exectued (has to be safe and never throw execptions).</param>
        /// <param name="start">if set to <c>true</c> [start].</param>
        public SchedulingBackgroundWorker(string name, Action doWork, bool start = true)
        {
            m_StopEvent = new ManualResetEvent(!start);
            if (doWork == null) throw new ArgumentNullException("doWork");
            m_DoWork = doWork;
            m_Thread = new Thread(worker){Name = name};
            if(start)
                m_Thread.Start();
        }


        public void Start()
        {
            if (m_StopEvent.WaitOne(0))
            {
                m_StopEvent.Reset();
                m_Thread.Start();
            }
        }
        
        private void worker()
        {
            var timeout = -1;
            bool stop = false;
            while (!stop)
            {
                if(WaitHandle.WaitAny(new WaitHandle[] {m_StopEvent, m_ScheduleEvent}, timeout)==0)
                        stop = true;
                timeout=doWorkIfRequired();
            }
        }

        private int doWorkIfRequired()
        {
            var now = DateTime.Now; 
            int timeout = -1;

            bool hasToDoWork=false;
            lock (m_ScheduledDates)
            {
                if (m_ScheduledDates.Count > 0)
                {

                    var next = m_ScheduledDates.Min();
                    if (next <= now )
                    {
                        hasToDoWork = true;
                        var passedDates = m_ScheduledDates.Where(d => d <= now).ToArray();
                        Array.ForEach(passedDates, d => m_ScheduledDates.Remove(d));
                        timeout = 0;
                    }
                    else
                        timeout = (int)(next - now).TotalMilliseconds;
                }
            }


            if (hasToDoWork)
                m_DoWork();

            return timeout;
        }


        public void Schedule(long ms)
        {
            var next = DateTime.Now.AddMilliseconds(ms);
            lock(m_ScheduledDates)
            {
                m_ScheduledDates.Add(next);
            }
            m_ScheduleEvent.Set();
        }

        public void Dispose()
        {
            m_StopEvent.Set();
        }
    }
}