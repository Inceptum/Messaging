using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading;
using Inceptum.DataBus.Messaging;
using Inceptum.Messaging.Contract;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.DataBus.Tests.Messaging
{
    public abstract class MessagingFeedWithHbProviderBaseMock<TData, TMessage, TContext, TInitRequest, TInitResponse, THeartbeatMessage>
        : MessagingFeedWithHbProviderBase<TData, TMessage, TContext, TInitRequest, TInitResponse, THeartbeatMessage>
    {
        protected MessagingFeedWithHbProviderBaseMock(IMessagingEngine transportEngine, long timeout)
            : base(transportEngine, timeout)
        {
        }

        protected override uint Tolerance
        {
            get { return 3; }
        }

        public DateTime? LastKnownUptimeDateImpl { get; set; }
        protected override DateTime? LastKnownUptimeDate
        {
            get { return LastKnownUptimeDateImpl; }
            set { LastKnownUptimeDateImpl = value; }
        }

        protected override Endpoint GetEndpoint(TContext context)
        {
            return new Endpoint("tr", "dest");
        }

        protected override Endpoint GetInitEndpoint(TContext context)
        {
            return new Endpoint("tr", "dest.init");
        }
        protected override Endpoint GetHbEndpoint(TContext context)
        {
            return new Endpoint("tr", "dest.HB");
        }

        public abstract TInitRequest GetInitRequestImpl();
        protected override TInitRequest GetInitRequest(TContext context)
        {
            return GetInitRequestImpl();
        }

        public abstract object GetHeartBeatKeyImpl(TContext context);

        public abstract long GetHeartbeatIntervalFromResponseImpl(TInitResponse response, TContext context);
        protected override long GetHeartbeatIntervalFromResponse(TInitResponse response, TContext context)
        {
            return GetHeartbeatIntervalFromResponseImpl(response, context);
        }

        public abstract long GetHeartbeatIntervalFromHeartbeatMessageImpl(THeartbeatMessage message, TContext context);
        protected override long GetHeartbeatIntervalFromHeartbeatMessage(THeartbeatMessage message, TContext context)
        {
            return GetHeartbeatIntervalFromHeartbeatMessageImpl(message, context);
        }

        public abstract DateTime? GetUptimeDateFromHeartbeatMessageImpl(THeartbeatMessage message, TContext context);
        protected override DateTime? GetUptimeDateFromHeartbeatMessage(THeartbeatMessage message, TContext context)
        {
            return GetUptimeDateFromHeartbeatMessageImpl(message, context);
        }

        public abstract TData ExtractInitialDataImpl(TInitResponse response, TContext context);
        protected override IEnumerable<TData> ExtractInitialData(TInitResponse response, TContext context)
        {
            return new[] { ExtractInitialDataImpl(response, context) };
        }
    }

    [TestFixture]
    public class MessagingFeedWithHbProviderBaseTests
    {
        [Test]
        public void HeartbeatsHaveNotAppearedTest()
        {
            MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object> feedProvider;
            var emulateHb = mockFeedProvider(out feedProvider);
            feedProvider.Expect(p => p.GetUptimeDateFromHeartbeatMessageImpl(new object(), "Context")).IgnoreArguments().Return(null).Repeat.Any();

            Exception ex = null;
            using (feedProvider.CreateFeed("Context").Subscribe(_ => { }, exception =>
            {
                ex = exception;
                if (ex as HeartbeatLostException == null) throw exception;
            }))
            {
                Assert.That(emulateHb.WaitForValue(200), Is.True);
                Thread.Sleep(1350);
            }

            Assert.IsNotNull(ex as HeartbeatLostException, "Exception was not thrown after [Tolerance]x[Hb interval] withoput HB messages");
        }

        [Test]
        public void HeartbeatsLostTest()
        {
            MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object> feedProvider;
            var emulateHb = mockFeedProvider(out feedProvider);
            feedProvider.Expect(p => p.GetUptimeDateFromHeartbeatMessageImpl(new object(), "Context")).IgnoreArguments().Return(null).Repeat.Any();

            Exception ex = null;
            using (feedProvider.CreateFeed("Context").Subscribe(_ => { }, exception =>
            {
                ex = exception;
                if (ex as HeartbeatLostException == null) throw exception;
            }))
            {
                Assert.That(emulateHb.WaitForValue(100), Is.True);
                emulateHb.Value(new object());
                Thread.Sleep(1350);
            }

            Assert.IsNotNull(ex as HeartbeatLostException, "Exception was not thrown after [Tolerance]x[Hb interval] withoput HB messages");
        }

        [Test]
        public void HeartbeatsUptimeNotChanged()
        {
            MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object> feedProvider;
            var emulateHb = mockFeedProvider(out feedProvider);
            var dummyHb = new object();
            feedProvider.LastKnownUptimeDateImpl = null;
            feedProvider.Expect(p => p.GetUptimeDateFromHeartbeatMessageImpl(dummyHb, "Context")).IgnoreArguments().Return(new DateTime(2012, 1, 1)).Repeat.Any();

            Exception ex = null;
            using (feedProvider.CreateFeed("Context").Subscribe(_ => { }, exception =>
            {
                ex = exception;
                if (ex as HeartbeatLostException == null) throw exception;
            }))
            {
                Assert.That(emulateHb.WaitForValue(100), Is.True);
                Assert.IsNull(feedProvider.LastKnownUptimeDateImpl);
                emulateHb.Value(new object());
                Assert.AreEqual(new DateTime(2012, 1, 1), feedProvider.LastKnownUptimeDateImpl);
                Thread.Sleep(100);
                emulateHb.Value(new object());
                Assert.AreEqual(new DateTime(2012, 1, 1), feedProvider.LastKnownUptimeDateImpl);
                Thread.Sleep(100);
            }
            Assert.IsNull(ex as HeartbeatLostException);
        }

        [Test]
        public void HeartbeatsUptimeChanged()
        {
            MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object> feedProvider;
            var emulateHb = mockFeedProvider(out feedProvider);
            var dummyHb = new object();
            feedProvider.LastKnownUptimeDateImpl = new DateTime(2012, 1, 1);
            feedProvider.Expect(p => p.GetUptimeDateFromHeartbeatMessageImpl(dummyHb, "Context")).IgnoreArguments().Return(new DateTime(2012, 1, 2)).Repeat.Once();

            Exception ex = null;
            using (feedProvider.CreateFeed("Context").Subscribe(_ => { }, exception =>
            {
                ex = exception;
                if (ex as HeartbeatLostException == null) throw exception;
            }))
            {
                Assert.That(emulateHb.WaitForValue(100), Is.True);
                Assert.AreEqual(new DateTime(2012, 1, 1), feedProvider.LastKnownUptimeDateImpl);
                emulateHb.Value(new object());
                Thread.Sleep(100);
            }

            Assert.IsNotNull(ex as HeartbeatLostException, "Exception was not thrown");
            Assert.AreEqual("Uptime date changed: was 01.01.2012 00:00:00, received 02.01.2012 00:00:00. Context Context", ex.Message);

        }

        private static ValueWaiter<Action<object>> mockFeedProvider(out MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object> feedProvider)
        {
            var initRequest = new object();
            var messagingEngine = MockRepository.GenerateMock<IMessagingEngine>();
            var dummyResponse = new object();

            messagingEngine.Expect(e => e.SendRequestAsync<object, object>(null, new Endpoint(), o => { }, exception => { })).
                IgnoreArguments()
                .WhenCalled(
                    invocation =>
                    {
                        var initSubscriptionCallback = (MulticastDelegate)(invocation.Arguments[2]);
                        initSubscriptionCallback.DynamicInvoke(dummyResponse);
                    }
                );


            var emulateHb = new ValueWaiter<Action<object>>();
            messagingEngine.Expect(e => e.Subscribe<int>(new Endpoint(), m => { })).IgnoreArguments().Return(Disposable.Empty).Repeat.Once();
            messagingEngine.Expect(e => e.Subscribe<object>(new Endpoint(), m => { })).IgnoreArguments().WhenCalled(invocation =>
            {
                var callback = (MulticastDelegate)(invocation.Arguments[1]);
                emulateHb.SetValue(hb => callback.DynamicInvoke(hb));
            }).Return(Disposable.Empty).Repeat.Once();
            feedProvider =
                MockRepository.GeneratePartialMock
                    <MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object>>(messagingEngine, 30000);
            feedProvider.Expect(p => p.GetHeartbeatIntervalFromResponseImpl(dummyResponse, "Context")).Return(100);
            feedProvider.Expect(p => p.GetHeartbeatIntervalFromHeartbeatMessageImpl(dummyResponse, "Context")).IgnoreArguments().Return(200).Repeat.Any();
            feedProvider.Expect(p => p.ExtractInitialDataImpl(dummyResponse, "Context")).Return(200);
            feedProvider.Expect(p => p.GetInitRequestImpl()).Return(initRequest);
            return emulateHb;
        }
    }
}
