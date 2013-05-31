using System.Configuration;

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

        [ConfigurationProperty("destination", IsRequired = true, IsKey = false)]
        public string Destination
        {
            get { return (string) this["destination"]; }
            set { this["destination"] = value; }
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
    }
}