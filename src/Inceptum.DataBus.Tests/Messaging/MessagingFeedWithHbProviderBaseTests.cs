using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading;
using Inceptum.Core.Messaging;
using Inceptum.DataBus.Messaging;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.DataBus.Tests.Messaging
{
    public abstract class MessagingFeedWithHbProviderBaseMock<TData, TMessage, TContext,TInitRequest, TInitResponse, THeartbeatMessage> 
        : MessagingFeedWithHbProviderBase<TData, TMessage, TContext,TInitRequest, TInitResponse, THeartbeatMessage> 
    {
        protected MessagingFeedWithHbProviderBaseMock(IMessagingEngine transportEngine, long timeout)
            : base(transportEngine,timeout)
        {
        }

        protected override uint Tolerance
        {
            get { return 3; }
        }

        protected override Endpoint GetEndpoint(TContext context)
        {
            return new Endpoint("tr","dest");
        }

        protected override Endpoint GetInitEndpoint(TContext context)
        {
            return new Endpoint("tr", "dest.init");
        }
        protected override Endpoint GetHbEndpoint(TContext context)
        {
            return new Endpoint("tr", "dest.HB");
        }

        protected override TInitRequest GetInitRequest(TContext context)
        {
            return GetInitRequestImpl();
        }

        public abstract TInitRequest GetInitRequestImpl();

        protected override long GetHeartbeatIntervalFromResponse(TInitResponse response, TContext context)
        {
            return GetHeartbeatIntervalFromResponseImpl( response, context);
        }
        public abstract long GetHeartbeatIntervalFromResponseImpl(TInitResponse response, TContext context);

        protected override long GetHeartbeatIntervalFromHeartbeatMessage(THeartbeatMessage message, TContext context)
        {
            return GetHeartbeatIntervalFromHeartbeatMessageImpl(message, context);
        }

        public abstract long GetHeartbeatIntervalFromHeartbeatMessageImpl(THeartbeatMessage message, TContext context);

        public abstract object GetHeartBeatKeyImpl(TContext context);
        public abstract TData ExtractInitialDataImpl(TInitResponse response, TContext context);


        protected override IEnumerable<TData> ExtractInitialData(TInitResponse response, TContext context)
        {
            return new[] {ExtractInitialDataImpl(response, context)};
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


            Exception ex = null;
            using (feedProvider.CreateFeed("Context").Subscribe(_ => { }, exception =>
                                                                              {
                                                                                  ex = exception;
                                                                                  if (ex as HeartbeatLostException == null) throw exception;
                                                                              }))
            {
                Assert.That(emulateHb.WaitForValue(100), Is.True);
                Thread.Sleep(1350);
            }

            Assert.IsNotNull(ex, "Exception was not thrown after [Tolerance]x[Hb interval] withoput HB messages");
        }      
        
        [Test]
        public void HeartbeatsLostTest()
        {
            MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object> feedProvider;
            var emulateHb = mockFeedProvider(out feedProvider);


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

            Assert.IsNotNull(ex, "Exception was not thrown after [Tolerance]x[Hb interval] withoput HB messages");
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
                            var initSubscriptionCallback = (MulticastDelegate) (invocation.Arguments[2]);
                            initSubscriptionCallback.DynamicInvoke(dummyResponse);
                        }
                );


            var emulateHb = new ValueWaiter<Action<object>>();
            messagingEngine.Expect(e => e.Subscribe<int>(new Endpoint(), m => { })).IgnoreArguments().Return(Disposable.Empty).Repeat.Once();
            messagingEngine.Expect(e => e.Subscribe<object>(new Endpoint(), m => { })).IgnoreArguments().WhenCalled(invocation =>
                                                                                                                        {
                                                                                                                            var callback = (MulticastDelegate) (invocation.Arguments[1]);
                                                                                                                            emulateHb.SetValue(hb => callback.DynamicInvoke(hb));
                                                                                                                        }).Return(Disposable.Empty).Repeat.Once();
            feedProvider =
                MockRepository.GeneratePartialMock
                    <MessagingFeedWithHbProviderBaseMock<int, int, string, object, object, object>>(messagingEngine, 30000);
            feedProvider.Expect(p => p.GetHeartbeatIntervalFromResponseImpl(dummyResponse, "Context")).Return(100);
            feedProvider.Expect(p => p.GetHeartbeatIntervalFromHeartbeatMessageImpl(dummyResponse, "Context")).IgnoreArguments().Return(200);
            feedProvider.Expect(p => p.GetInitRequestImpl()).Return(initRequest);
            return emulateHb;
        }
    }
}