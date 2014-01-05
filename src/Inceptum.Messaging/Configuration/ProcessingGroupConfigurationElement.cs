using System.Configuration;

namespace Inceptum.Messaging.Configuration
{
    public class ProcessingGroupConfigurationElement : NamedConfigurationElement
    {
        [ConfigurationProperty("concurrencyLevel", IsRequired = true, IsKey = false)]
        public int ConcurrencyLevel
        {
            get { return (int)this["concurrencyLevel"]; }
            set { this["concurrencyLevel"] = value; }
        }

        [ConfigurationProperty("queueCapacity", IsRequired = false, IsKey = false, DefaultValue = 1024)]
        public int QueueCapacity
        {
            get { return (int)this["queueCapacity"]; }
            set { this["queueCapacity"] = value; }
        }

    }
}