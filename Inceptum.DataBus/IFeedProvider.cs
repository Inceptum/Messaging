using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Inceptum.DataBus
{
    /// <summary>
    /// Feed provider contract
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    public interface IFeedProvider<TData, TContext>
    {
        /// <summary>
        /// Determines whether this instance can provide feed for the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>
        /// 	<c>true</c> if this instance can provide feed for the specified context; otherwise, <c>false</c>.
        /// </returns>
        bool CanProvideFor(TContext context);

        /// <summary>
        /// Gets the feed for the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        IObservable<TData> CreateFeed(TContext context);

        /// <summary>
        /// Called when feed is lost.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>collection of objects to be sent to subscribers on feed lost (empty if nothing should be sent)</returns>
        IEnumerable<TData> OnFeedLost(TContext context);

        /// <summary>
        /// Gets resubscription policy. If null is returned, the default policy (retry onec per 2min) would be applyed.
        /// </summary>
        /// <returns>null if feed does not need specific policy or the policy instance</returns>
        IFeedResubscriptionPolicy GetResubscriptionPolicy(TContext context);
    }


    public static class DeferredObservable
    {
        public static DeferredObservable<TSource> CreateWithDisposable<TSource>(Func<IObserver<TSource>, Action,IDisposable> subscribe)
        {
            return new DeferredObservable<TSource>(subscribe);
        }
    }

    public class DeferredObservable<TSource> : IObservable<TSource>
    {
        private readonly Func<IObserver<TSource>, Action, IDisposable> _subscribe;

        public DeferredObservable(Func<IObserver<TSource>, Action, IDisposable> subscribe)
        {
            _subscribe = subscribe;
        }

        public IDisposable Subscribe(IObserver<TSource> observer)
        {
            return Observable.Create<TSource>(o => _subscribe(o, () => { })).Subscribe(observer);
        }
        public IDisposable Subscribe(IObserver<TSource> observer, Action callback)
        {
            return Observable.Create<TSource>(o => _subscribe(o, callback)).Subscribe(observer);
        }
    }
}