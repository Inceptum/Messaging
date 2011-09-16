using System;

namespace Inceptum.DataBus
{
    public interface IFeed<T> : IObservable<T>
    {
        T LatestValue { get; }
        bool HasLatestValue { get; }
        FeedStatus Status { get; }
        IDisposable Subscribe(IObserver<T> observer, bool repeateLastValue);
        IObservable<FeedStatus> StatusFlow { get; }
    }
}