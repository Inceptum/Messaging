using System.Configuration;

namespace Inceptum.Messaging.Configuration
{
    public class TransportConfigurationElement : NamedConfigurationElement
    {
        [ConfigurationProperty("broker", IsRequired = true, IsKey = false)]
        public string Broker
        {
            get { return (string) this["broker"]; }
            set { this["broker"] = value; }
        }

        [ConfigurationProperty("login", IsRequired = true, IsKey = false)]
        public string Login
        {
            get { return (string) this["login"]; }
            set { this["login"] = value; }
        }


        [ConfigurationProperty("password", IsRequired = true, IsKey = false)]
        public string Password
        {
            get { return (string) this["password"]; }
            set { this["password"] = value; }
        }

        [ConfigurationProperty("jailStrategyName", IsRequired = false, IsKey = false, DefaultValue = "None")]
        public string JailStrategyName
        {
            get { return (string) this["jailStrategyName"]; }
            set { this["jailStrategyName"] = value; }
        }

        [ConfigurationProperty("messaging", IsRequired = false, IsKey = false, DefaultValue = "Sonic")]
        public string Messaging
        {
            get { return (string) this["messaging"]; }
            set { this["messaging"] = value; }
        }
    }
}