using System;
using System.Collections;
using System.Collections.Generic;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.Weblogic
{
    /// <summary>
    /// The implementaion is pretty clumsy, the custom dictionaries are intended just to be able to pass additional params to every jms message
    /// Hope this is temporary
    /// </summary>
    public abstract class WeblogicTransportFactoryAbstract : ITransportFactory
    {
        private readonly string m_Name;
        private readonly IDictionary<string, string> m_CustomHeaders;
        private readonly IDictionary<string, string> m_CustomSelectors;

        protected WeblogicTransportFactoryAbstract(string name, IDictionary<string, string> customHeaders, IDictionary<string, string> customSelectors)
        {
            m_Name = name;
            m_CustomHeaders = customHeaders;
            m_CustomSelectors = customSelectors;
        }

        public string Name { get { return m_Name; }}

        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            return new WeblogicTransport(transportInfo, onFailure, m_CustomHeaders, m_CustomSelectors);
        }
    }

    public class WeblogicTransportFactory : WeblogicTransportFactoryAbstract
    {
        public WeblogicTransportFactory() : base("Weblogic", null, null)
        {
        }
    }
}