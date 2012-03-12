using System;

namespace Inceptum.Messaging
{
    public class TransportInfo
    {
        public TransportInfo(string broker, string login, string password, string jailStrategyName)
        {
            if (string.IsNullOrEmpty((broker ?? "").Trim())) throw new ArgumentException("broker should be not empty string", "broker");
            if (string.IsNullOrEmpty((login ?? "").Trim())) throw new ArgumentException("login should be not empty string", "login");
            if (string.IsNullOrEmpty((password ?? "").Trim())) throw new ArgumentException("password should be not empty string", "password");
            Broker = broker;
            Login = login;
            Password = password;
            JailStrategyName = jailStrategyName;
        }

        public string Broker { get; private set; }
        public string Login { get; private set; }
        public string Password { get; private set; }
        public string JailStrategyName { get; private set; }

        public JailStrategy JailStrategy { get; internal set; }


        public bool Equals(TransportInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Broker, Broker) && Equals(other.Login, Login) && Equals(other.Password, Password) && Equals(other.JailStrategyName, JailStrategyName);
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
            if (obj.GetType() != typeof (TransportInfo)) return false;
            return Equals((TransportInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Broker != null ? Broker.GetHashCode() : 0);
                result = (result*397) ^ (Login != null ? Login.GetHashCode() : 0);
                result = (result*397) ^ (Password != null ? Password.GetHashCode() : 0);
                result = (result*397) ^ (JailStrategyName != null ? JailStrategyName.GetHashCode() : 0);
                return result;
            }
        }
    }
}
