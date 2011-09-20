using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using DataBusTests;
 
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.DataBus.Tests
{
    [TestFixture]
    public class FeedTests
    {
        [Test]
        public void NeverRequestFeedForTheSameContextTwiceTest()
        {
            DataBus db = new DataBus();
            MyFeedProvider feedProvider = new MyFeedProvider();
            db.RegisterFeedProvider("MyChannel", feedProvider);
            AutoResetEvent ev = new AutoResetEvent(false);
            AutoResetEvent ev1 = new AutoResetEvent(false);
            int[] c = {0, 0};
            var subscribtion1 = db.Channel<string>("MyChannel").Feed(1).Subscribe(s =>
                                                                                      {
                                                                                          Console.WriteLine("subscribtion1\t" + s);
                                                                                          if (++c[0] == 5) ev.Set();
                                                                                      });
            var subscribtion2 = db.Channel<string>("MyChannel").Feed(1).Subscribe(s =>
                                                                                      {
                                                                                          Console.WriteLine("subscribtion2\t" + s);
                                                                                          if (++c[1] == 5) ev1.Set();
                                                                                      });
            using (subscribtion1)
            {
                using (subscribtion2)
                {
                    Assert.IsTrue(ev.WaitOne(1000), "Subscribtion handler was not called");
                    Assert.IsTrue(ev1.WaitOne(1000), "Subscribtion handler was not called");
                }
            }

            Assert.AreNotEqual(0, feedProvider.SubscribeCounters[1], "Subscriber was  not subscribed to the feed ");
            Assert.AreNotEqual(0, feedProvider.UnsubscribeCounters[1], "Subscriber was  not unsubscribed from the feed ");
            Assert.AreEqual(1, feedProvider.SubscribeCounters[1], "Subscriber was subscribed more then 1 time to the same feed ");
            Assert.AreEqual(1, feedProvider.UnsubscribeCounters[1], "Subscriber was unsubscribed more then 1 time from the same feed ");
        }

        [Test]
        public void RetrySubscribeTest()
        {
            var feedProvider = MockRepository.GenerateMock<IFeedProvider<long, string>>();
            feedProvider.Expect(provider => provider.CanProvideFor("context")).IgnoreArguments().Return(true);
            feedProvider.Expect(provider => provider.OnFeedLost("context")).IgnoreArguments().Return(new long[0]);

            int[] createFeedCallsCount = new[] {0};
            int[] handlerCallsCount = new[] {0};
            Subject<long> feed = null;

            Func<string, IObservable<long>> createFeed = c =>
                                                             {
                                                                 createFeedCallsCount[0]++;
                                                                 feed = new Subject<long>();
                                                                 return feed;
                                                             };
            feedProvider.Expect(provider => provider.CreateFeed("context")).Do(createFeed);

            var db = new DataBus(300);
            db.RegisterFeedProvider("feed", feedProvider);
            using (db.Channel<long>("feed").Feed("context").Subscribe(l => handlerCallsCount[0]++))
            {
                feed.OnNext(1);
                feed.OnError(new Exception());
                Thread.Sleep(500);
                feed.OnNext(2);
                Thread.Sleep(100);
            }

            Assert.Greater(createFeedCallsCount[0], 0, "Feed was not created");
            Assert.AreEqual(2, createFeedCallsCount[0], "Feed was not recreated after error");
            Assert.AreEqual(2, handlerCallsCount[0], "Handler was not called for values produced after recreation of feed");
        }

        [Test]
        public void StatusTest()
        {
            Subject<long> feedSource = null;
            var feedProvider = MockRepository.GenerateMock<IFeedProvider<long, string>>();
            feedProvider.Expect(provider => provider.CanProvideFor("context")).IgnoreArguments().Return(true);
            feedProvider.Expect(provider => provider.OnFeedLost("context")).IgnoreArguments().Return(new long[0]);
            feedProvider.Expect(provider => provider.CreateFeed("context")).IgnoreArguments()
                .Return(
                    Observable.Defer(() =>
                                         {
                                             feedSource = new Subject<long>();
                                             return feedSource;
                                         }
                        )
                );
            using (var db = new DataBus(200))
            {
                db.RegisterFeedProvider("feed", feedProvider);
                var feed = db.Channel<long>("feed").Feed("context");

                var retrievedStatuses = new List<FeedStatus>();
                using (feed.StatusFlow.Subscribe(retrievedStatuses.Add))
                {
                    using (feed.Subscribe())
                    {
                        feedSource.OnError(new Exception());
                        Assert.AreEqual(FeedStatus.NotAvailable, feed.Status, "Status was not set to NotAvailable after feed source produced an error");
                        Thread.Sleep(300);
                    }
                }

//                Assert.AreEqual(retrievedStatuses.Count, "Statuses was not reported correctly via StatusFlow");

                Assert.That(retrievedStatuses, Is.EqualTo(new[] { FeedStatus.NotSubscribed, FeedStatus.Subscribing, FeedStatus.Available, FeedStatus.NotAvailable, FeedStatus.Subscribing, FeedStatus.Available, FeedStatus.NotSubscribed }), "Statuses was not reported correctly via StatusFlow");
/*
                Assert.AreEqual(FeedStatus.NotSubscribed, retrievedStatuses[0], "Statuses was not reported correctly via StatusFlow");
                Assert.AreEqual(FeedStatus.Subscribing, retrievedStatuses[1], "Statuses was not reported correctly via StatusFlow");
                Assert.AreEqual(FeedStatus.Available, retrievedStatuses[2], "Statuses was not reported correctly via StatusFlow");
                Assert.AreEqual(FeedStatus.NotAvailable, retrievedStatuses[3], "Statuses was not reported correctly via StatusFlow");
                Assert.AreEqual(FeedStatus.Subscribing, retrievedStatuses[4], "Statuses was not reported correctly via StatusFlow");
                Assert.AreEqual(FeedStatus.Available, retrievedStatuses[5], "Statuses was not reported correctly via StatusFlow");
                Assert.AreEqual(FeedStatus.NotSubscribed, retrievedStatuses[6], "Statuses was not reported correctly via StatusFlow");
*/
            }
        }        
        
        
        
        [Test]
        public void DeferredSubscriptionStatusTest()
        {
            Subject<long> feedSource = new Subject<long>();
            Action notefySubscribed=() => { };
            var feedProvider = MockRepository.GenerateMock<IFeedProvider<long, string>>();
            feedProvider.Expect(provider => provider.CanProvideFor("context")).IgnoreArguments().Return(true);
            feedProvider.Expect(provider => provider.OnFeedLost("context")).IgnoreArguments().Return(new long[0]);
            feedProvider.Expect(provider => provider.CreateFeed("context")).IgnoreArguments()
                .Return(
                
                    DeferredObservable.CreateWithDisposable<long>((observer, action) => 
                                                              {
                                                                  notefySubscribed = action;
                                                                  return feedSource.Subscribe(observer);
                                                              })
                );

            using (var db = new DataBus(200))
            {
                db.RegisterFeedProvider("feed", feedProvider);
                var feed = db.Channel<long>("feed").Feed("context");

                var retrievedStatuses = new List<FeedStatus>();
                using (feed.StatusFlow.Subscribe(retrievedStatuses.Add))
                {
                    using (feed.Subscribe())
                    {
                        Assert.AreEqual(FeedStatus.Subscribing, feed.Status, "Status is not Subscribing while subscriptions is started and not yet reported as finished");
                        feedSource.OnNext(1);
                        Assert.AreEqual(FeedStatus.Subscribing, feed.Status, "Status is not Subscribing while subscriptions is started and not yet reported as finished");
                        notefySubscribed();
                        Assert.AreEqual(FeedStatus.Available, feed.Status, "Status was not set to Available after subscription is reported as finished");
                        var f = feedSource;
                        feedSource = new Subject<long>();
                        f.OnError(new Exception("test"));
                        Assert.AreEqual(FeedStatus.NotAvailable, feed.Status, "Status was not set to NotAvailable after feed source produced an error");
                        Thread.Sleep(500);
                    }
                }

                Assert.That(retrievedStatuses.ToArray(), Is.EqualTo(new[]
                                                                        {
                                                                            FeedStatus.NotSubscribed, 
                                                                            FeedStatus.Subscribing, 
                                                                            FeedStatus.Available, 
                                                                            FeedStatus.NotAvailable, 
                                                                            FeedStatus.Subscribing, 
                                                                            FeedStatus.NotSubscribed
                                                                        }), "Statuses was not reported correctly via StatusFlow");
            }
        }


        [Test]
        public void StatusFlowRepeatsCurrentStatusOnSubscribeTest()
        {
            var feedProvider = MockRepository.GenerateMock<IFeedProvider<long, string>>();
            feedProvider.Expect(provider => provider.CanProvideFor("context")).IgnoreArguments().Return(true);
            var feedSource = new Subject<long>();
            feedProvider.Expect(provider => provider.CreateFeed("context")).IgnoreArguments()
                .Return(Observable.Defer(() => feedSource));
            var db = new DataBus(200);
            db.RegisterFeedProvider("feed", feedProvider);
            var feed = db.Channel<long>("feed").Feed("context");

            FeedStatus? latestStatusFlowValue = null;
            ManualResetEvent statusHandlerCalled = new ManualResetEvent(false);
            using (feed.Subscribe())
            {
                feedSource.OnNext(1);
                using (feed.StatusFlow.Subscribe(status =>
                                                     {
                                                         latestStatusFlowValue = status;
                                                         statusHandlerCalled.Set();
                                                         Console.WriteLine(status);
                                                     }))
                {
                    Assert.IsTrue(statusHandlerCalled.WaitOne(200), "Value was not pushed on subscrbe to StatusFlow");
                    Assert.AreEqual(FeedStatus.Available, latestStatusFlowValue, "Wrong value was pushed on subscrbe to StatusFlow");
                }
            }
        }


        [Test]
        public void Subscribtion_LatestValueTest()
        {
            Subject<string> subject = new Subject<string>();
            using (DataBus db = new DataBus())
            {
                db.RegisterFeedProvider<string, string>("channel", context => subject);


                var feed = db.Channel<string>("channel").Feed("aaa");

                using (feed.Subscribe(Console.WriteLine))
                {
                    Assert.IsFalse(feed.HasLatestValue, "Feed has last value before first data recieve from feedprovider generated observable");

                    subject.OnNext("First value");
                    Assert.IsTrue(feed.HasLatestValue, "Feed DOES NOT have last value before first data recieve from feedprovider generated observable");
                    Assert.AreEqual("First value", feed.LatestValue, "Last value does not match most recent value produced by feedprovider generated observable");
                    subject.OnNext("Second value");
                    Assert.AreEqual("Second value", feed.LatestValue, "Last value does not match most recent value produced by feedprovider generated observable");
                }
            }
        }
    }
}