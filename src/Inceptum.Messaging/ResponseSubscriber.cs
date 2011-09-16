/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Castle.Core.Logging;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="data"></param>
    /// <param name="error"></param>
//    public delegate void ResponseSubscriptionHandler(TransportMessage message, object data, Exception error);

    /// <summary>
    /// 
    /// </summary>
    internal class ResponseSubscriber
    {
        private class Request
        {
            public DateTime? ExpireTime { get; private set; }
            public string Queue{ get; private set; }
            public Action<Message, Exception> Handler { get; private set; }

            public Request(DateTime? expireTime, string subject, Action<Message,Exception> handler)
            {
                ExpireTime = expireTime;
                Queue = subject;
                Handler = handler;
            }
        }

        //TODO: init logger
        private static readonly ILogger _logger = NullLogger.Instance;
        private static readonly bool _isInfoEnabled = _logger.IsInfoEnabled;
        private static readonly bool _isDebugEnabled = _logger.IsDebugEnabled;

        private readonly Dictionary<string, MessageConsumer> _listeners = new Dictionary<string, MessageConsumer>();
        private readonly Session _session;
        private readonly object _syncLock = new object();

        private readonly List<Request> _requests = new List<Request>();
        protected readonly Queue Queue;

        internal void ProcessTimeouts()
        {
            IEnumerable<Request> timeoutedRequests;

            lock (_syncLock)
            {
                timeoutedRequests = _requests.Where(r => r.ExpireTime.HasValue&& r.ExpireTime< DateTime.Now).ToArray();

                foreach (var request in timeoutedRequests)
                {
                    UnSubscribeSubject(request.Queue);
                    _requests.Remove(request);
                }
            }

            foreach (var timeoutedRequest in timeoutedRequests)
            {
                timeoutedRequest.Handler(null, new TimeoutException("Transport response timeout."));
                Thread.Sleep(0);    
            }
        }

        public ResponseSubscriber(Session session, Queue queue)
        {
            Queue = queue;
            _session = session;
        }

        public void SubscribeOnResponse(string replySubject, Action<Message, Exception> responseHandler, long timeout)
        {
            lock (_syncLock)
            {
                DateTime? expireDate = null;
                if (timeout != -1)
                {
                    expireDate = DateTime.Now.AddSeconds(timeout);
                }
                _requests.Add(new Request(expireDate, replySubject,responseHandler));
                SubscribeSubject(replySubject);
            }
        }

        /// <summary>
        ///  method should process recieved message
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="subject">Subject</param>
        /// <param name="closure">Closure is any Callback object</param>
        protected void ProcessMessage( Message message, string subject, object closure)
        {
            Request request ;
            lock (_syncLock)
            {
                // Unsubscribing from subject

                UnSubscribeSubject(subject);

                request = _requests.FirstOrDefault(r => r.Queue==subject);
                if (request==null)
                    return;
                _requests.Remove(request);
            }

            request.Handler(message, null);
        }

        public Queue GetReplyQueue()
        {
            lock (_session)
            {
                return _session.createTemporaryQueue();
            }
        }
 

        /// <summary>
        ///  Creates Tibrv Listener 
        /// </summary>
        private MessageConsumer createListener(Queue queue, Session session, string subject )
        {
            Listener listener = new Listener(queue, onMessageReceived, transport, subject, closure);

            if (_isInfoEnabled)
                _logger.Info("Subscribed on subject=" + subject);

            return listener;
        }

        /// <summary>
        /// Unsubscribes from Tibrv
        /// </summary>
        /// <param name="subject">subject</param>
        protected virtual void UnSubscribeSubject(string subject)
        {
            // looks for listener
            lock (_listeners)
            {
                Listener listener;
                _listeners.TryGetValue(subject, out listener);

                if (listener != null)
                {
                    stopSubscription(listener, subject);
                }
            }
        }

        protected virtual void SubscribeSubject(string subject )
        {
            lock (_listeners)
            {
                if (!_listeners.ContainsKey(subject))
                {
                    _listeners.Add(subject, createListener(Queue, _session, subject));
                }
            }
        }

        /// <summary>
        /// Stops all subscription
        /// </summary>
        public virtual void StopSubscriptions()
        {
            lock (_listeners)
            {
                var subscribers = new List<KeyValuePair<string, Listener>>(_listeners);

                foreach (KeyValuePair<string, Listener> subscriber in subscribers)
                {
                    stopSubscription(subscriber.Value, subscriber.Key);
                }
            }
        }

        /// <summary>
        /// Removes listener
        /// </summary>
        private void stopSubscription(Listener listener, string subject)
        {
            _listeners.Remove(subject);

            listener.SafeDestroy();

            if (_isInfoEnabled)
                _logger.Info("Unsubscribed from subject=" + subject);
        }

        /// <summary>
        /// Processes recieved message
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="msgRecieved"></param>
        private void onMessageReceived(object listener, MessageReceivedEventArgs msgRecieved)
        {
            TransportMessage message = null;
            try
            {
                message = new TransportMessage(msgRecieved.Message);
                if (_isDebugEnabled)
                    _logger.Debug(String.Format("Recieved subject={0} message={1}", message.SendSubject, message));
                ProcessMessage(message, ((Listener) listener).Subject, msgRecieved.Closure);
            }
            catch (Exception ex)
            {
                const string desc = "Unhandled error during message processing.";
                _logger.Error(desc, ex);
                OnErrorProcessingMessage(message, ex);
            }
        }

        /// <summary>
        /// Processes error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        protected virtual void OnErrorProcessingMessage(TransportMessage message, Exception ex)
        {
            // do nothing here 
        }

        public void ProcessTransportFail(Exception error)
        {
            cancelRequests(r => true, error);
        }

      
        private void cancelRequests(Func<Request, bool> predicate, Exception error)
        {
            lock (_syncLock)
            {
                foreach (var request in _requests.Where(predicate).ToArray())
                {
                    try
                    {
                        UnSubscribeSubject(request.Queue);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn("Failed to cancel subscription", ex);
                    }
                    request.Handler( null, error);
                    _requests.Remove(request);
                }
            }
        }
    }
}*/