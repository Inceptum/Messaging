using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using WebLogic.Messaging;

namespace Inceptum.Messaging.Weblogic
{
    internal class WeblogicTransport : ITransport
    {
        private readonly object m_SyncRoot = new object();
        private volatile bool m_IsDisposed;
        private Action m_OnFailure;
        private readonly IDictionary<string, string> m_CustomHeaders;
        private readonly IDictionary<string, string> m_CustomSelectors;
        private readonly List<IMessagingSession> m_Sessions = new List<IMessagingSession>();
        private readonly string m_JailedTag;
        private readonly IConnection m_Connection;
        private readonly IContext m_Context;

        public WeblogicTransport(TransportInfo transportInfo, Action onFailure, IDictionary<string, string> customHeaders, IDictionary<string, string> customSelectors)
        {
            if (onFailure == null) throw new ArgumentNullException("onFailure");
            m_OnFailure = onFailure;
            m_CustomHeaders = customHeaders;
            m_CustomSelectors = customSelectors;
            m_JailedTag = (transportInfo.JailStrategy ?? JailStrategy.None).CreateTag();

            IDictionary<string, object> paramsMap = new Dictionary<string, object>();
            paramsMap[Constants.Context.PROVIDER_URL] = "t3://" + transportInfo.Broker;
            m_Context = ContextFactory.CreateContext(paramsMap);
            var connectionFactory = m_Context.LookupConnectionFactory("weblogic.jms.ConnectionFactory");
            //var connectionFactory = m_Context.LookupConnectionFactory("weblogic.jndi.WLInitialContextFactory");
            m_Connection = connectionFactory.CreateConnection(transportInfo.Login, transportInfo.Password);
            m_Connection.Exception += connectionExceptionHandler;
            m_Connection.Start();
        }

        private void connectionExceptionHandler(IConnection sender, ExceptionEventArgs args)
        {
            lock (m_SyncRoot)
            {
                m_OnFailure();
                Dispose();
            }
        }

        public IMessagingSession CreateSession(Action onFailure)
        {
            IMessagingSession group;
            lock (m_SyncRoot)
            {
                group = new WeblogicSession(m_Connection, m_JailedTag, m_CustomHeaders, m_CustomSelectors);
                m_Sessions.Add(group);
            }
            return group;
        }

        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired, out string error)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (m_IsDisposed) return;

            lock (m_SyncRoot)
            {
                if (m_IsDisposed) return;
                m_OnFailure = () => { };

                foreach (var session in m_Sessions)
                {
                    session.Dispose();
                }

                if (m_Connection != null)
                {
                    m_Connection.Close();
                }

                if (m_Context != null)
                {
                    m_Context.CloseAll();
                }

                m_IsDisposed = true;
            }
        }


        #endregion
    }
}