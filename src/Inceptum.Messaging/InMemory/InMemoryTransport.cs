using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.InMemory
{
    internal class InMemoryTransport : ITransport
    {
        private readonly List<InMemorySession> m_Sessions = new List<InMemorySession>();
        private readonly Dictionary<string, Subject<BinaryMessage>> m_Topics = new Dictionary<string, Subject<BinaryMessage>>();


        public Subject<BinaryMessage> this[string name]
        {
            get
            {
                lock (m_Topics)
                {
                    Subject<BinaryMessage> topic;
                    if (!m_Topics.TryGetValue(name, out topic))
                    {
                        topic = new Subject<BinaryMessage>();
                        m_Topics[name] = topic;
                    }
                    return topic;
                }
            }
        }

        public void Dispose()
        {
            lock (m_Sessions)
            {
                foreach (var session in m_Sessions)
                {
                    session.Dispose();
                }
                m_Sessions.Clear();
            }
        }

        public IMessagingSession CreateSession(Action onFailure)
        {
            var session = new InMemorySession(this);
            lock (m_Sessions)
            {
                m_Sessions.Add(session);
                return session;
            }
        }

        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            error = null;
            return true;
        }


        public IDisposable CreateTemporary(string name)
        {
            lock (m_Topics)
            {
                Subject<BinaryMessage> topic;
                if (m_Topics.TryGetValue(name, out topic))
                {
                    throw new ArgumentException("topic already exists", "name");
                }
                topic = new Subject<BinaryMessage>();
                m_Topics[name] = topic;
                return Disposable.Create(() =>
                {
                    lock (m_Topics)
                    {
                        m_Topics.Remove(name);
                    }
                });
            }
        }
    }
}