using System;
using System.Collections.Generic;

namespace Inceptum.DataBus
{
    internal class AnonymousFeedProvider<TData, TContext> : IFeedProvider<TData, TContext>
    {
        private readonly Func<TContext, bool> _canProvideFor;
        private readonly Func<TContext, IObservable<TData>> _feedProvider;

        public AnonymousFeedProvider(Func<TContext, IObservable<TData>> feedProvider, Func<TContext, bool> canProvideFor)
        {
            if (feedProvider == null)
                throw new ArgumentNullException("feedProvider");
            _canProvideFor = canProvideFor ?? (c => true);
            _feedProvider = feedProvider;
        }


        public bool CanProvideFor(TContext context)
        {
            return _canProvideFor(context);
        }

        public IObservable<TData> CreateFeed(TContext context)
        {
            return _feedProvider(context);
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(TContext context)
        {
            return null;
        }

        public IEnumerable<TData> OnFeedLost(TContext context)
        {
            return new TData[0];
        }
    }
}