using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using Castle.Core.Logging;

namespace Inceptum.DataBus 
{
    internal class Feed<TData, TContext> : IFeed<TData>, IDisposable, IObserver<TData>
    {
        private volatile int _count;
        private readonly Subject<TData> _subject = new Subject<TData>();
        private readonly Subject<FeedStatus> _statusSubject = new Subject<FeedStatus>();
        private readonly Subject<Exception> _errorsSubject;
        private TData _latestValue;
        private volatile bool _hasLatestValue;
        private IDisposable _subscribtion;
        private readonly IFeedProvider<TData, TContext> _feedProvider;
        private readonly TContext _context;
        //TODO: obtain from IFeedProvider
        //private readonly long _retryPeriod;
        private int _status = (int) FeedStatus.NotSubscribed;
        private IDisposable _retryTask;
        private readonly ILogger _logger;
        private readonly string _channelName;
        private readonly object _syncRoot = new object();
        private int _subscriptionAttempt = 0;
        private readonly IFeedResubscriptionPolicy _resubscriptionPolicy;

        public ILogger Logger
        {
            get { return _logger; }
        }

        public TData LatestValue
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get { return _latestValue; }
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set
            {
                _latestValue = value;
                _hasLatestValue = true;
            }
        }


        internal IFeedProvider<TData, TContext> FeedProvider
        {
            get { return _feedProvider; }
        }

        public bool HasLatestValue
        {
            get { return _hasLatestValue; }
        }


        public IObservable<FeedStatus> StatusFlow
        {
            get
            {
                var curStatus = Status;
                return Observable.Defer(() => new[] {curStatus}.ToObservable().Merge(_statusSubject));
            }
        }

        public FeedStatus Status
        {
            get { return (FeedStatus) _status; }
            set
            {
                var statusInt = (int) value;
                if (statusInt != Interlocked.Exchange(ref _status, statusInt))
                {
                    _statusSubject.OnNext(value);
                }

                var feedStatus = (FeedStatus)statusInt;
                if (feedStatus == FeedStatus.Available)
                {
                    var attempt = Interlocked.Exchange(ref _subscriptionAttempt, 0);
                    if (attempt==0)
                    {
                        Logger.Info("Subscribed for feed on channel '{0}', Context: {1} ({2}) ", _channelName, _context, typeof(TContext));
                    }else
                    {
                        Logger.Info("Subscription for feed on channel '{0}', Context: {1} ({2}) is restored after {3} attempts", _channelName, _context, typeof(TContext), attempt);
                    }
                }

                if (feedStatus == FeedStatus.NotSubscribed )
                 {
                     Logger.Info("Unsubscribed from feed on channel '{0}', Context: {1} ({2}) ", _channelName, _context, typeof(TContext));
                 }
            }
        }

        public Feed(IFeedProvider<TData, TContext> feedProvider, TContext context, Subject<Exception> errorsSubject,  long retryPeriod, string channelName, ILogger logger)
        {

            _channelName = channelName;
            _logger = logger ?? NullLogger.Instance;
           
//            _retryPeriod = retryPeriod;
            if (feedProvider == null)
            {
                throw new ArgumentNullException("feedProvider");
            }
            if (errorsSubject == null)
            {
                throw new ArgumentNullException("errorsSubject");
            }
            _context = context;
            _feedProvider = feedProvider;
            _errorsSubject = errorsSubject;
            _resubscriptionPolicy = feedProvider.GetResubscriptionPolicy(_context) ?? new TimeoutFeedResubscriptionPolicy(retryPeriod);
        }

        public IDisposable Subscribe(IObserver<TData> observer)
        {
            return Subscribe(observer, true);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable Subscribe(IObserver<TData> observer, bool repeateLastValue)
        {
            if (_count++ == 0)
            {
                subscribeToSource();
            }

            if (repeateLastValue && _hasLatestValue)
            {
                observer.OnNext(LatestValue);
            }

            return new CompositeDisposable(
                _subject.Subscribe(data => internalHandler(data, observer), observer.OnError, observer.OnCompleted),
                Disposable.Create(unsubscribe)
                );
        }

        private void subscribeToSource()
        {
            lock (_syncRoot)
            {
                switch (_subscriptionAttempt)
                {
                    case 0:
                        Logger.Info("Subscribing for feed on channel '{0}', Context: {1} ({2}) ", _channelName, _context, typeof(TContext));
                        break;
                    case 1:
                        Logger.Info("Resubscribing for feed on channel '{0}', Context: {1} ({2}). Attempt #{3} ", _channelName, _context, typeof(TContext),_subscriptionAttempt);
                        break;
                    default:
                        Logger.Debug("Resubscribing for feed on channel '{0}', Context: {1} ({2}). Attempt #{3} ", _channelName, _context, typeof(TContext), _subscriptionAttempt);
                        break;
                }
                Status = FeedStatus.Subscribing;
                var source = _feedProvider.CreateFeed(_context);
                var deferred = source as DeferredObservable<TData>;
                if (deferred == null)
                {
                    _subscribtion = source.Subscribe(this);
                    if (Status == FeedStatus.Subscribing)
                        Status = FeedStatus.Available;
                        return;
                }

                deferred.Subscribe(this, () =>
                                             {
                                                 if (Status == FeedStatus.Subscribing) Status = FeedStatus.Available;
                                             });
            }
        }

        private void unsubscribeFromSource()
        {
            foreach (var data in _feedProvider.OnFeedLost(_context) ?? new TData[0])
            {
                _subject.OnNext(data);
            }

            lock (_syncRoot)
            {
                if (_subscribtion != null)
                {
                    _subscribtion.Dispose();
                    _subscribtion = null;
                }
                if (_retryTask == null) 
                    return;

                _retryTask.Dispose();
                _retryTask = null;
            }
        }

        #region IObservable<TData>

        void IObserver<TData>.OnNext(TData value)
        {
            LatestValue = value;
            _subject.OnNext(value);
        }

        void IObserver<TData>.OnError(Exception exception)
        {

            if (Interlocked.Increment(ref _subscriptionAttempt)==1)
                Logger.WarnFormat(exception, "Feed lost. Channel '{0}', Context: {1} ({2}) ", _channelName, _context, typeof (TContext));
            else
                Logger.Debug("Failed to resubscribe for feed on channel '{0}', Context: {1} ({2}). Attempt #{3} ", _channelName, _context, typeof(TContext), _subscriptionAttempt);
            Status = FeedStatus.NotAvailable;
            unsubscribeFromSource();

            //Schedule source observable resubscription
            _retryTask = _resubscriptionPolicy.InitResubscription(subscribeToSource,exception); // Scheduler.TaskPool.Schedule(subscribeToSource, TimeSpan.FromMilliseconds(_retryPeriod));

        }

        void IObserver<TData>.OnCompleted()
        {
            Status = FeedStatus.NotAvailable;
            unsubscribeFromSource();
            //TODO: Previousely it was async, may cause lags. 
            subscribeToSource();
        }

        #endregion

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void unsubscribe()
        {
            if (--_count != 0) 
                return;

            Status = FeedStatus.NotSubscribed;
            unsubscribeFromSource();
        }

        /// <summary>
        /// Handles errors in subscribers
        /// </summary>
        /// <param name="data"></param>
        /// <param name="observer"></param>
        private void internalHandler(TData data, IObserver<TData> observer)
        {
            LatestValue = data;
            try
            {
                observer.OnNext(data);
            }
            catch (Exception e)
            {
                _errorsSubject.OnNext(e);
            }
        }

        public void Dispose()
        {
            _subject.OnCompleted();
            unsubscribeFromSource();
            Status = FeedStatus.NotSubscribed;
        }
    }
}