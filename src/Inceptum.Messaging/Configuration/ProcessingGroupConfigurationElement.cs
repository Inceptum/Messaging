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
    }
}