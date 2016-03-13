using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.InMemory
{
    internal class InMemorySession : IMessagingSession
    {
        private readonly EventLoopScheduler m_Scheduler = new EventLoopScheduler(ts => new Thread(ts) {Name = "inmemory transport"});
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();
        private readonly InMemoryTransport m_Transport;
        private bool m_IsDisposed;

        public InMemorySession(InMemoryTransport queues)
        {
            m_Transport = queues;
        }

        public Destination CreateTemporaryDestination()
        {
            var name = Guid.NewGuid().ToString();
            m_Transport.CreateTemporary(name);
            return name;
        }


        public void Send(string destination, BinaryMessage message, int ttl)
        {
            m_Transport[destination].OnNext(message);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            var subject = m_Transport[destination];
            var subscribe = subject.Where(m => m.Type == messageType || messageType == null).ObserveOn(m_Scheduler)
                .Subscribe(message => callback(message, b =>
                {
                    if (!b)
                    {
                        ThreadPool.QueueUserWorkItem(state => subject.OnNext(message));
                    }
                }));
            m_Subscriptions.Add(subscribe);
            return subscribe;
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            var replyTo = Guid.NewGuid().ToString();
            var responseTopic = m_Transport.CreateTemporary(replyTo);

            var request = new RequestHandle(callback, responseTopic.Dispose, cb => Subscribe(replyTo, (binaryMessage, acknowledge) => cb(binaryMessage), null));
            message.Headers["ReplyTo"] = replyTo;
            Send(destination, message, 0);
            return request;
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            var subscription = Subscribe(destination, (request, acknowledge) =>
            {
                string replyTo;
                request.Headers.TryGetValue("ReplyTo", out replyTo);
                if (replyTo == null)
                    return;

                var response = handler(request);
                string correlationId;
                if (request.Headers.TryGetValue("ReplyTo", out correlationId))
                    response.Headers["CorrelationId"] = correlationId;
                Send(replyTo.ToString(), response, 0);
            }, messageType);
            return subscription;
        }


        public void Dispose()
        {
            if (m_IsDisposed)
                return;
            var finishedProcessing = new ManualResetEvent(false);
            m_Subscriptions.Dispose();
            m_Scheduler.Schedule(() => finishedProcessing.Set());
            finishedProcessing.WaitOne();
            m_Scheduler.Dispose();
            m_IsDisposed = true;
        }
    }
}