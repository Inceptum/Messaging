using System;
using System.Collections.Generic;
using Sonic.Jms;
using QueueConnectionFactory = Sonic.Jms.Cf.Impl.QueueConnectionFactory;

namespace Inceptum.Messaging
{
    public class TransportResolver : ITransportResolver
    {
        private readonly Dictionary<string, TransportInfo> m_Transports = new Dictionary<string, TransportInfo>();

        //TODO: need to register transports in some better way
        public TransportResolver(IDictionary<string, TransportInfo> transports)
        {
            if (transports == null) throw new ArgumentNullException("transports");
            m_Transports=new Dictionary<string, TransportInfo>(transports);
            //m_Transports.Add("tr", new TransportInfo("msk-mqesb1.office.finam.ru:2507", "ibank.backend", "mmm000")); 
        }

        #region ITransportResolver Members

        public TransportInfo GetTransport(string transportId)
        {
            TransportInfo transport;
            return m_Transports.TryGetValue(transportId, out transport) ? transport : null;
        }

        #endregion
    }

   
}