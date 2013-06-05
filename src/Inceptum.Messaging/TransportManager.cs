using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging
{

    internal class TransportManager : IDisposable
    {
        private class ResolvedTransport : IDisposable
        {
            class ProcessingGroupWrapper: IDisposable
            {
                public string TransportId { get; private set; }
                public string Name { get; private set; }
                public IProcessingGroup ProcessingGroup { get; private set; }
                public event Action OnFailure;

                public ProcessingGroupWrapper(string transportId, string name)
                {
                    TransportId = transportId;
                    Name = name;
                }

                public void SetProcessingGroup(IProcessingGroup processingGroup)
                {
                    ProcessingGroup = processingGroup;
                }

                public void ReportFailure()
                {
                    if (OnFailure == null) 
                        return;

                    foreach (var handler in OnFailure.GetInvocationList())
                    {
                        try
                        {
                            handler.DynamicInvoke();
                        }
                        catch (Exception)
                        {
                            //TODO: log
                        }
                    }
                }

                public void Dispose()
                {
                    if(ProcessingGroup!=null)
                        ProcessingGroup.Dispose();
                }
            }
            private readonly List<string> m_KnownIds = new List<string>();
            private readonly TransportInfo m_TransportInfo;
            private readonly Action m_ProcessTransportFailure;
            private readonly ITransportFactory m_Factory;
            private readonly List<ProcessingGroupWrapper> m_ProcessingGroups=new List<ProcessingGroupWrapper>();
            public ResolvedTransport(TransportInfo transportInfo, Action processTransportFailure,ITransportFactory factory)
            {
                m_Factory = factory;
                m_ProcessTransportFailure = processTransportFailure;
                m_TransportInfo = transportInfo;
            }

            public IEnumerable<string> KnownIds
            {
                get { return m_KnownIds.ToArray(); }
            }

            private ITransport Transport { get; set; }


            private void addId(string transportId)
            {
                if (string.IsNullOrEmpty(transportId)) throw new ArgumentNullException("transportId");
                if (!m_KnownIds.Contains(transportId))
                    m_KnownIds.Add(transportId);
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public IProcessingGroup GetProcessingGroup(string transportId,string name, Action onFailure)
            {
                addId(transportId);
                var transport = Transport ?? (Transport = m_Factory.Create(m_TransportInfo, processTransportFailure));

                var processingGroup = m_ProcessingGroups.FirstOrDefault(g => g.TransportId == transportId && g.Name  == name);
               
                if (processingGroup==null)
                {
                    processingGroup = new ProcessingGroupWrapper(transportId,name);
                    processingGroup.SetProcessingGroup(transport.CreateProcessingGroup(() => processProcessingGroupFailure(processingGroup)));
                    m_ProcessingGroups.Add(processingGroup);
                }
                if(onFailure!=null)
                    processingGroup.OnFailure += onFailure;
                return processingGroup.ProcessingGroup;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            private void processTransportFailure()
            {
                foreach (var processinGroup in m_ProcessingGroups)
                {
                    processProcessingGroupFailure(processinGroup);
                }
                m_ProcessTransportFailure();
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            private void processProcessingGroupFailure(ProcessingGroupWrapper processingGroup)
            {
                m_ProcessingGroups.Remove(processingGroup);
                processingGroup.ReportFailure();
            }
 

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Dispose()
            {
                if (Transport == null) 
                    return;
                foreach (var processinGroupWrapper in m_ProcessingGroups)
                {
                    processinGroupWrapper.Dispose();
                }
                //TODO: dispose processing groups
                Transport.Dispose();
                Transport = null;
            }
        }


        private readonly Dictionary<TransportInfo, ResolvedTransport> m_Transports = new Dictionary<TransportInfo, ResolvedTransport>();
        private readonly ITransportResolver m_TransportResolver;
        readonly ManualResetEvent m_IsDisposed=new ManualResetEvent(false);
        private readonly ITransportFactory[] m_TransportFactories;


        public TransportManager(ITransportResolver transportResolver, params ITransportFactory[] transportFactories)
        {
            m_TransportFactories = transportFactories;
            if (transportResolver == null) throw new ArgumentNullException("transportResolver");
            m_TransportResolver = transportResolver;
        }

        #region IDisposable Members

        public void Dispose()
        {
            m_IsDisposed.Set();
            lock (m_Transports)
            {
                foreach (var transport in m_Transports.Values.Distinct())
                {
                    transport.Dispose();
                }
                m_Transports.Clear();
            }
        }

        #endregion

        public event TrasnportEventHandler TransportEvents;

        public IProcessingGroup GetProcessingGroup(string transportId, string name, Action onFailure=null)
        {
            if (m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException(string.Format("Can not create transport {0}. TransportManager instance is disposed",transportId));


            var transportInfo = m_TransportResolver.GetTransport(transportId);
            
            if (transportInfo == null)
                throw new ConfigurationErrorsException(string.Format("Transport '{0}' is not resolvable", transportId));
            var factory = m_TransportFactories.FirstOrDefault(f => f.Name == transportInfo.Messaging);
            if(factory==null)
                throw new ConfigurationErrorsException(string.Format("Can not create transport '{0}', {1} messaging is not supported", transportId,transportInfo.Messaging));

            ResolvedTransport transport;

            if (!m_Transports.TryGetValue(transportInfo, out transport)  )
            {
                lock (m_Transports)
                {
                    if (!m_Transports.TryGetValue(transportInfo, out transport))
                    {
                        transport = new ResolvedTransport(transportInfo,()=> ProcessTransportFailure(transportInfo),factory);
                        if (m_Transports.ContainsKey(transportInfo))
                            m_Transports.Remove(transportInfo);
                        m_Transports.Add(transportInfo, transport);
                    }
                }
             
            }

            try
            {
                return transport.GetProcessingGroup(transportId,name,onFailure);
            }
            catch (Exception e)
            {
                throw new TransportException(string.Format("Failed to create transport with id '{0}' and parameters {1}", transportId, transportInfo), e);
            }
        }

        internal virtual void ProcessTransportFailure(TransportInfo transportInfo)
        {
            ResolvedTransport transport;
            lock (m_Transports)
            {
                if(!m_Transports.TryGetValue(transportInfo, out transport))
                    return;
                m_Transports.Remove(transportInfo);
            }

            var handler = TransportEvents;
            if (handler == null) return;

            lock (transport)
            {
                foreach (var transportId in transport.KnownIds)
                {
                    handler(transportId, Contract.TransportEvents.Failure);
                }
            }
        }
    }
}
