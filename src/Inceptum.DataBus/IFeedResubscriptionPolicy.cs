using System;
using System.Reactive.Concurrency;

namespace Inceptum.DataBus
{
    public interface IFeedResubscriptionPolicy
    {
        IDisposable InitResubscription(Action subscribeToSource, Exception exception);
    }


    public class TimeoutFeedResubscriptionPolicy : IFeedResubscriptionPolicy
    {
        private readonly double _retryPeriod;

        public TimeoutFeedResubscriptionPolicy(double retryPeriod = 120000)
        {
            _retryPeriod = retryPeriod;
        }

        public IDisposable InitResubscription(Action doSubscribe, Exception exception)
        {
            return Scheduler.TaskPool.Schedule(TimeSpan.FromMilliseconds(_retryPeriod), doSubscribe);
        }
    }
}
