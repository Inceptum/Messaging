using System;
using System.Collections.Generic;

namespace Inceptum.Messaging
{
    public class ProcessingGroupInfo
    {
        public int ConcurrencyLevel { get; set; } 
    }
    public class TransportInfo
    {
        public TransportInfo(string broker, string login, string password, string jailStrategyName, string messaging="Sonic")
        {
            if (string.IsNullOrEmpty((broker ?? "").Trim())) throw new ArgumentException("broker should be not empty string", "broker");
            if (string.IsNullOrEmpty((login ?? "").Trim())) throw new ArgumentException("login should be not empty string", "login");
            if (string.IsNullOrEmpty((password ?? "").Trim())) throw new ArgumentException("password should be not empty string", "password");
            Broker = broker;
            Login = login;
            Password = password;
            JailStrategyName = jailStrategyName;
            Messaging = messaging;
            ProcessingGroups=new Dictionary<string, ProcessingGroupInfo>();
        }

        public string Broker { get; private set; }
        public string Login { get; private set; }
        public string Password { get; private set; }
        public string JailStrategyName { get; private set; }
        public Dictionary<string, ProcessingGroupInfo> ProcessingGroups { get;  set; }



        public JailStrategy JailStrategy { get; internal set; }

        public string Messaging { get; private set; }


        protected bool Equals(TransportInfo other)
        {
            return string.Equals(Broker, other.Broker) && string.Equals(Login, other.Login) && string.Equals(Password, other.Password) && string.Equals(Messaging, other.Messaging) && string.Equals(JailStrategyName, other.JailStrategyName);
        }

    
        public static bool operator ==(TransportInfo left, TransportInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TransportInfo left, TransportInfo right)
        {
            return !Equals(left, right);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TransportInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Broker != null ? Broker.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Login != null ? Login.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Messaging != null ? Messaging.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (JailStrategyName != null ? JailStrategyName.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("Broker: {0}, Login: {1}", Broker, Login);
        }
    }
}
