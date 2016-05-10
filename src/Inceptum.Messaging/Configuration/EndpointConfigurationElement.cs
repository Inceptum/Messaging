using System.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Configuration
{
    public class EndpointConfigurationElement : NamedConfigurationElement
    {
        [ConfigurationProperty("transportId", IsRequired = true, IsKey = false)]
        public string TransportId
        {
            get { return (string) this["transportId"]; }
            set { this["transportId"] = value; }
        }

        [ConfigurationProperty("publish", IsRequired = false, IsKey = false)]
        public string Publish
        {
            get { return (string)this["publish"]; }
            set { this["publish"] = value; }
        }

        [ConfigurationProperty("subscribe", IsRequired = false, IsKey = false)]
        public string Subscribe
        {
            get { return (string)this["subscribe"]; }
            set { this["subscribe"] = value; }
        }

       
        [ConfigurationProperty("sharedDestination", IsRequired = false, IsKey = false, DefaultValue = false)]
        public bool SharedDestination
        {
            get { return (bool) this["sharedDestination"]; }
            set { this["sharedDestination"] = value; }
        }

        [ConfigurationProperty("serializationFormat", IsRequired = false, IsKey = false, DefaultValue = "protobuf")]
        public string SerializationFormat
        {
            get { return (string)this["serializationFormat"]; }
            set { this["serializationFormat"] = value; }
        }

        public Endpoint ToEndpoint()
        {
            return new Endpoint(TransportId, Publish, Subscribe, SharedDestination, SerializationFormat);
        }
    }
}