using System;
using System.Collections.Generic;

namespace Inceptum.DataBus
{
    /// <summary>
    /// Contextless feed provider base
    /// </summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    public abstract class ContextlessFeedProviderBase<TData> : IFeedProvider<TData, EmptyContext>
    {
        public virtual bool CanProvideFor(EmptyContext context)
        {
            return true;
        }

        public IObservable<TData> CreateFeed(EmptyContext context)
        {
            return CreateFeed();
        }

        public abstract IEnumerable<TData> OnFeedLost(EmptyContext context);
        public virtual IFeedResubscriptionPolicy GetResubscriptionPolicy(EmptyContext context)
        {
            return null;
        }

        protected abstract IObservable<TData> CreateFeed();
    }
}