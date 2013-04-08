using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.Sonic
{
    public class SonicTransportFactory : ITransportFactory
    {
        internal const string JAILED_PROPERTY_NAME = "JAILED_TAG";
        internal const int MESSAGE_DEFAULT_LIFESPAN = 0; 

        public string Name { get { return "Sonic"; } }
        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return new Transport(transportInfo, onFailure);
        }
    }
}
