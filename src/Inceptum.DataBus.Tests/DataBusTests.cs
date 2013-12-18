using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Inceptum.DataBus;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.DataBus.Tests
{
    [TestFixture]
    public class DataBusTests
    {
        [Test]
        [ExpectedException(typeof (DataBusException),
            ExpectedMessage = "Can not register feed provider resolving feeds of 'Int32' within context of 'String' for channel '[System.Int64] test (System.String)'")]
        public void RegisterFeedProvider_MismatchWithPreviouselyRegisteredChannel_FailureTest()
        {
            DataBus db = new DataBus();
            db.RegisterFeedProvider<long, string>("test", context => Observable.Empty<long>());
            db.RegisterFeedProvider<int, string>("test", context => Observable.Empty<int>());
        }

        [Test]
        [ExpectedException(typeof (ArgumentNullException), ExpectedMessage = "Value cannot be null.\r\nParameter name: feedResolver")]
        public void RegisterFeedProviderNullResolverFailureTest()
        {
            DataBus db = new DataBus();
            const Func<string, IObservable<int>> feedResolver = null;
            db.RegisterFeedProvider("test", feedResolver);
        }

        [Test]
        [ExpectedException(typeof (ArgumentException), ExpectedMessage = "'channelName' should be not empty string\r\nParameter name: channelName")]
        public void RegisterFeedProviderEmptyNameFailureTest()
        {
            DataBus db = new DataBus();
            db.RegisterFeedProvider<int, string>("", context => Observable.Empty<int>());
        }


        [Test]
        public void SubscribtionTest()
        {
            DataBus db = new DataBus();
            db.RegisterFeedProvider<string, string>("strings", context => Observable.Interval(TimeSpan.FromMilliseconds(50)).Take(5).Select(i => context + i));
            ManualResetEvent ev = new ManualResetEvent(false);
            Stopwatch sw = Stopwatch.StartNew();
            using(db.Channel<string>("strings").Feed("test").Subscribe(s =>
                                                                                      {
                                                                                          Console.WriteLine(sw.Elapsed.Milliseconds + " " + s);
                                                                                          ev.Set();
                                                                                      }
                ))
            {
                Assert.IsTrue(ev.WaitOne(400), "Handler was not called");    
            }
            
        }


        [Test]
        [ExpectedException(typeof (DataBusException), ExpectedMessage = "Channel 'MyChannel' does not have feed for context 2 (Int32)")]
        public void CanSubscribeReturnsFalseFailureTest()
        {
            DataBus db = new DataBus();
            MyFeedProvider feedProvider = new MyFeedProvider();
            db.RegisterFeedProvider("MyChannel", feedProvider);

            try
            {
                db.Channel<string>("MyChannel").Feed(2);
            }
            finally
            {
                Assert.IsTrue(feedProvider.CanProvideForWasCalled, "IFeedProvider.CanProvideFor was not called");
            }
        }

        [Test]
        [ExpectedException(typeof (DataBusException), ExpectedMessage = "FeedProvider MyFeedProvider  is already registered in channel 'MyChannel' [Int32]")]
        public void FeedProvidersRegisteredTwiceFailureTest()
        {
            DataBus db = new DataBus();
            MyFeedProvider feedProvider1 = new MyFeedProvider();
            db.RegisterFeedProvider("MyChannel", feedProvider1);
            db.RegisterFeedProvider("MyChannel", feedProvider1);


            db.Channel<string>("MyChannel").Feed(1);
        }


        [Test]
        [ExpectedException(typeof (DataBusException), ExpectedMessage = "Channel 'MyChannel' has more then one feed for context 1 (Int32)")]
        public void TwoFeedProvidersForSameContextFailureTest()
        {
            DataBus db = new DataBus();
            MyFeedProvider feedProvider1 = new MyFeedProvider();
            MyFeedProvider feedProvider2 = new MyFeedProvider();
            db.RegisterFeedProvider("MyChannel", feedProvider1);
            db.RegisterFeedProvider("MyChannel", feedProvider2);


            db.Channel<string>("MyChannel").Feed(1);
        }

        [Test]
        public void ExceptionHandlingTest()
        {
            DataBus db = new DataBus();
            db.RegisterFeedProvider<string, string>("channel", context => Observable.Interval(TimeSpan.FromMilliseconds(50)).Select(i => context + i));
            ManualResetEvent ev = new ManualResetEvent(false);

            DataBusUnhandledExceptionEventArgs eventArgs = null;
            db.UnhandledException += (sender, args) =>
                                         {
                                             eventArgs = args;
                                             ev.Set();
                                         };

            var ex = new Exception("message");
            using (db.Channel<string>("channel").Feed("test").Subscribe(s => { throw ex; }))
            {
                Assert.IsTrue(ev.WaitOne(500), "Exception was not handled");
                Assert.IsNotNull(eventArgs);
                Assert.AreEqual("channel", eventArgs.ChannelName, "Channel name was not captured.");
                Assert.AreEqual(ex, eventArgs.Exception, "Exception was not captured.");
            }
        }


        [Test]
        public void DisposeTest()
        {
            bool handlerWasCalled = false;
            var handler = new Action<long>(l =>
                                               {

                                                   handlerWasCalled = true;
                                               });

            var feedProvider = MockRepository.GenerateMock<IFeedProvider<long, string>>();
            feedProvider.Expect(provider => provider.CanProvideFor("context")).IgnoreArguments().Return(true);

            var db = new DataBus();
            var subject = new Subject<long>();
            feedProvider.Expect(provider => provider.CreateFeed("context")).IgnoreArguments().Return(subject);
            db.RegisterFeedProvider("channel1", feedProvider);
            db.Channel<long>("channel1").Feed("dummy").Subscribe(handler);

            db.Dispose();

            subject.OnNext(1);
            Assert.That(handlerWasCalled, Is.False,"Handler was called after databus was disposed");
        }
    }
}