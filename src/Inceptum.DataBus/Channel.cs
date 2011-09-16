using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Castle.Core.Internal;
using Castle.Core.Logging;


namespace Inceptum.DataBus
{
    internal class Channel<TData, TContext> : IDisposable
    {
        private readonly Dictionary<TContext, WeakReference> _feeds = new Dictionary<TContext, WeakReference>();
        private readonly List<IFeedProvider<TData, TContext>> _feedProviders = new List<IFeedProvider<TData, TContext>>();
        private readonly Subject<Exception> _errorsSubject = new Subject<Exception>();
        private readonly long _defaultRetryPeriod;
        private readonly ILogger _logger;


        public IObservable<Exception> Errors
        {
            get { return _errorsSubject; }
        }

        public string Name { get; private set; }

        public Channel(string name, long defaultRetryPeriod, ILogger logger)
        {
            _defaultRetryPeriod = defaultRetryPeriod;
            _logger = logger ?? NullLogger.Instance;
            Name = name;
        }

        internal void AddFeedProvider(IFeedProvider<TData, TContext> feedProvider)
        {
            if (_feedProviders.Contains(feedProvider))
                throw new DataBusException(string.Format("FeedProvider {1}  is already registered in channel '{0}' [{2}]", Name, feedProvider.GetType().Name, typeof (TContext).Name));
            _feedProviders.Add(feedProvider);
        }

        public IFeed<TData> Feed(TContext c)
        {
            WeakReference reference;
            Feed<TData, TContext> feed=null;
            lock (_feeds)
            {
                _feeds.Where(p => !p.Value.IsAlive).Select(p => p.Key).ToArray().ForEach(k => _feeds.Remove(k));
                if (_feeds.TryGetValue(c, out reference))
                {
                    feed = reference.Target as Feed<TData, TContext>;
                }

                if(feed==null)
                {
                    feed = createFeed(c);
                    if(reference!=null)
                    {
                        _feeds.Remove(c);
                    }
                    _feeds.Add(c, new WeakReference(feed));
                }
            }
            return feed;
        }


        private Feed<TData, TContext> createFeed(TContext context)
        {
            var feedProviders = from provider in _feedProviders where provider.CanProvideFor(context) select provider;
            IFeedProvider<TData, TContext> feedProvider;
            switch (feedProviders.Count())
            {
                case 0:
                    throw new DataBusException(string.Format("Channel '{0}' does not have feed for context {1} ({2})", Name, context, typeof (TContext).Name));
                case 1:
                    feedProvider = feedProviders.First();
                    break;
                default:
                    throw new DataBusException(string.Format("Channel '{0}' has more then one feed for context {1} ({2})", Name, context, typeof (TContext).Name));
            }
            return new Feed<TData, TContext>(feedProvider, context, _errorsSubject, _defaultRetryPeriod, Name, _logger);
        }


        public void Dispose()
        {
            foreach (var feed in _feeds)
            {
                var feed1 = (feed.Value.Target as Feed<TData, TContext>);
                if (feed1 != null)
                {
                    feed1.Dispose();
                }
            }
        }


        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2})", typeof (TData), Name, typeof (TContext));
        }

        public void RemoveFeedProvider(IFeedProvider<TData, TContext> feedProvider)
        {
            if (_feedProviders.Contains(feedProvider))
            {
                var feeds = from reference in _feeds
                            let f = reference.Value.Target as Feed<TData, TContext>
                            where f != null && f.FeedProvider == feedProvider
                            select new {feed = f, context = reference.Key};

                foreach (var feed in feeds.ToArray())
                {
                    if (feed.feed.FeedProvider == feedProvider)
                    {
                        _feeds.Remove(feed.context);
                        feed.feed.Dispose();
                    }
                }
                _feedProviders.Remove(feedProvider);
            }
        }
    }
}