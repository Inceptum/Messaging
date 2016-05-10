using System.Configuration;

namespace Inceptum.Messaging.Configuration
{
    public class ProcessingGroupConfigurationElement : NamedConfigurationElement
    {
        [ConfigurationProperty("concurrencyLevel", IsRequired = true, IsKey = false)]
        public uint ConcurrencyLevel
        {
            get { return (uint)this["concurrencyLevel"]; }
            set { this["concurrencyLevel"] = value; }
        }

        [ConfigurationProperty("queueCapacity", IsRequired = false, IsKey = false, DefaultValue = (uint)1024)]
        public uint QueueCapacity
        {
            get { return (uint)this["queueCapacity"]; }
            set { this["queueCapacity"] = value; }
        }

    }
}