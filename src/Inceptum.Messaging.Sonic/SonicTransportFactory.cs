using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.Sonic
{
    internal class SonicTransportConstants
    {
        internal const string JAILED_PROPERTY_NAME = "JAILED_TAG";
        internal const int MESSAGE_DEFAULT_LIFESPAN = 0;        
    }

    public class SonicTransportFactory : ITransportFactory
    {
        public string Name { get { return "Sonic"; } }
        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return new Transport(transportInfo, onFailure, MessageFormat.Binary);
        }
    }

    public class SonicTextTransportFactory : ITransportFactory
    {
        public string Name { get { return "SonicText"; } }
        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return new Transport(transportInfo, onFailure, MessageFormat.Text);
        }        
    }
}
