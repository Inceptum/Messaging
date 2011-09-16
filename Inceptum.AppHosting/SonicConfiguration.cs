/*using System;
using System.Collections.Generic;

namespace Inceptum.AppHosting
{
    /// <summary>
    /// 
    /// </summary>
    public class SonicConfiguration  
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SonicConfiguration"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        /// <param name="jailed">The environment.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="servicePort">The service port.</param>
        /// <param name="queues">The queues.</param>
        /// <exception cref="ArgumentNullException"></exception>
        ///   
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public SonicConfiguration(string host, int port, bool jailed, string userName, string password, int servicePort, Dictionary<string, string> queues)
        {
            m_Queues = queues;
            if (string.IsNullOrEmpty(host)) throw new ArgumentNullException("host", "Host is null or empty");
            if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException("port", "Port should be in range from 0 to 65535");
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException("userName", "UserName is null or empty");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException("password", "Password is null or empty");
            if (servicePort < 0 || servicePort > 65535) throw new ArgumentOutOfRangeException("servicePort", "ServicePort should be in range from 0 to 65535");

            m_Host = host;
            m_Port = port;

            m_Jailed = jailed;
            m_UserName = userName;
            m_Password = password;
            m_ServicePort = servicePort;
        }

        /// <summary>
        /// Gets the queues.
        /// </summary>
        public Dictionary<string, string> Queues
        {
            get { return m_Queues; }
            
        }

        private readonly string m_Host;

        private readonly int m_Port;

        private readonly string m_UserName;

        private readonly string m_Password;
        private readonly int m_ServicePort;

        private readonly bool m_Jailed;
        private Dictionary<string, string> m_Queues;

        ///<summary>
        /// Sonic broker host
        ///</summary>
        public string Host
        {
            get { return m_Host; }
        }

        ///<summary>
        /// Sonic broker port
        ///</summary>
        public int Port
        {
            get { return m_Port; }
        }

        ///<summary>
        /// User name
        ///</summary>
        public string UserName
        {
            get { return m_UserName; }
        }

        ///<summary>
        /// Password
        ///</summary>
        public string Password
        {
            get { return m_Password; }
        }

        ///<summary>
        /// Current application environment
        ///</summary>
        public bool Jailed
        {
            get { return m_Jailed; }
        }

        /// <summary>
        /// Service port
        /// </summary>
        public int ServicePort
        {
            get { return m_ServicePort; }
        }
    }
}*/