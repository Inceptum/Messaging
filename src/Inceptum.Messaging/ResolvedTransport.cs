using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;

namespace Inceptum.Messaging
{
    internal class ResolvedTransport : IDisposable
    {
        private readonly List<string> m_KnownIds = new List<string>();
        private readonly TransportInfo m_TransportInfo;
        private readonly Action m_ProcessTransportFailure;
        private readonly ITransportFactory m_Factory;
        private readonly List<MessagingSessionWrapper> m_MessagingSessions = new List<MessagingSessionWrapper>();

        public ResolvedTransport(TransportInfo transportInfo, Action processTransportFailure, ITransportFactory factory)
        {
            m_Factory = factory;
            m_ProcessTransportFailure = processTransportFailure;
            m_TransportInfo = transportInfo;
        }

        internal MessagingSessionWrapper[] Sessions
        {
            get { return m_MessagingSessions.ToArray(); }
        }

        public IEnumerable<string> KnownIds
        {
            get { return m_KnownIds.ToArray(); }
        }

        internal ITransport Transport { get; set; }


        private void addId(string transportId)
        {
            if (String.IsNullOrEmpty(transportId)) throw new ArgumentNullException("transportId");
            if (!m_KnownIds.Contains(transportId))
                m_KnownIds.Add(transportId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IMessagingSession GetSession(string transportId, string name, Action onFailure)
        {
            addId(transportId);
            var transport = Transport ?? (Transport = m_Factory.Create(m_TransportInfo, Helper.CallOnlyOnce(processTransportFailure)));
            MessagingSessionWrapper messagingSession;

            lock (m_MessagingSessions)
            {
                messagingSession = m_MessagingSessions.FirstOrDefault(g => g.TransportId == transportId && g.Name == name);

                if (messagingSession == null)
                {
                    messagingSession = new MessagingSessionWrapper(transportId, name);
                    messagingSession.SetSession(transport.CreateSession(Helper.CallOnlyOnce(()=>processSessionFailure(messagingSession))));
                    m_MessagingSessions.Add(messagingSession);
                }
            }

            if (onFailure != null)
                messagingSession.OnFailure += onFailure;
            return messagingSession;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void processTransportFailure()
        {
            MessagingSessionWrapper[] messagingSessionWrappers;
            lock (m_MessagingSessions)
            {
                messagingSessionWrappers = m_MessagingSessions.ToArray();
            }

            foreach (var session in messagingSessionWrappers)
            {
                processSessionFailure(session);
            }

            m_ProcessTransportFailure();
        }

        private void processSessionFailure(MessagingSessionWrapper messagingSession)
        {
            lock (m_MessagingSessions)
            {
                m_MessagingSessions.Remove(messagingSession);
            }
            messagingSession.ReportFailure();
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if (Transport == null)
                return;

            MessagingSessionWrapper[] sessions;
            lock (m_MessagingSessions)
            {
                sessions = m_MessagingSessions.ToArray();
            }

            foreach (var session in sessions)
            {
                session.Dispose();
            }

            Transport.Dispose();
            Transport = null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired,out string error)
        {
            var transport = Transport ?? (Transport = m_Factory.Create(m_TransportInfo, processTransportFailure));
            return transport.VerifyDestination(destination, usage, configureIfRequired, out error);
        }

    }

    internal class MessagingSessionWrapper:IMessagingSession
    {
        public string TransportId { get; private set; }
        public string Name { get; private set; }
        private IMessagingSession MessagingSession { get; set; }
        public event Action OnFailure;


        public MessagingSessionWrapper(string transportId, string name)
        {
            TransportId = transportId;
            Name = name;
        }
        public void SetSession(IMessagingSession messagingSession)
        {
            MessagingSession = messagingSession;
        }

        public void ReportFailure()
        {
            if (OnFailure == null)
                return;

            foreach (var handler in OnFailure.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke();
                }
                catch (Exception)
                {
                    //TODO: log
                }
            }
        }



        public void Dispose()
        {
            if (MessagingSession == null)
                return;
            MessagingSession.Dispose();
            MessagingSession = null;
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            MessagingSession.Send(destination, message, ttl);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            return MessagingSession.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            return MessagingSession.RegisterHandler(destination, handler, messageType);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            return MessagingSession.Subscribe(destination,callback, messageType);
        }

        public Destination CreateTemporaryDestination()
        {
            return MessagingSession.CreateTemporaryDestination();
        }
    }
}