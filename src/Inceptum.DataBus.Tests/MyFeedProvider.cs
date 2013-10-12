using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Inceptum.DataBus;

namespace DataBusTests
{
    public class MyFeedProvider : IFeedProvider<string, int>
    {
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Action<string> _action;
        private readonly Dictionary<int, int> _subscribeCounters = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _unsubscribeCounters = new Dictionary<int, int>();

        private Thread _thread;

        public bool IsSubscribed
        {
            get { return !_stop.WaitOne(0); }
        }

        public bool CanProvideForWasCalled { get; set; }


        public void Subscribe(int c, Action<string> action)
        {
            _action = action;
            _stop.Reset();
            lock (_subscribeCounters)
            {
                _thread = new Thread(() => loop(c));
                if (!_subscribeCounters.ContainsKey(c))
                {
                    _subscribeCounters[c] = 0;
                }
                _subscribeCounters[c]++;
            }
            _thread.Start();
        }

        public IDictionary<int, int> SubscribeCounters
        {
            get { return _subscribeCounters; }
        }

        public IDictionary<int, int> UnsubscribeCounters
        {
            get { return _unsubscribeCounters; }
        }


        private void loop(int param)
        {
            int i = 0;
            while (!_stop.WaitOne(50))
            {
                _action(string.Format("Parameter: {0};   Action Call #{1}", param, i));
                i++;
            }
        }

        public bool CanProvideFor(int c)
        {
            CanProvideForWasCalled = true;
            return c == 1 || c > 100;
        }

        public IObservable<string> CreateFeed(int c)
        {
            return Observable.Create<string>(observer =>
                                                               {
                                                                   subscribe(observer, c);
                                                                   return Disposable.Create(unsubscribe(c));
                                                               });
        }

        public IEnumerable<string> OnFeedLost(int context)
        {
            return new string[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(int context)
        {
            return null;
        }

        private Action unsubscribe(int c)
        {
            return () =>
                       {
                           lock (_unsubscribeCounters)
                           {
                               if (!_unsubscribeCounters.ContainsKey(c))
                               {
                                   _unsubscribeCounters[c] = 0;
                               }
                               _unsubscribeCounters[c]++;
                           }
                           _stop.Set();
                       };
        }

        private void subscribe(IObserver<string> observer, int c)
        {
            _action = observer.OnNext;
            _stop.Reset();
            lock (_subscribeCounters)
            {
                _thread = new Thread(() => loop(c));
                if (!_subscribeCounters.ContainsKey(c))
                {
                    _subscribeCounters[c] = 0;
                }
                _subscribeCounters[c]++;
            }
            _thread.Start();
        }
    }
}
