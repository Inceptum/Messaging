using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging
{
    internal class ResolvedTransport : IDisposable
    {
        private readonly List<string> m_KnownIds = new List<string>();
        private readonly TransportInfo m_TransportInfo;
        private readonly Action m_ProcessTransportFailure;
        private readonly ITransportFactory m_Factory;
        private readonly List<ProcessingGroupWrapper> m_ProcessingGroups = new List<ProcessingGroupWrapper>();

        public ResolvedTransport(TransportInfo transportInfo, Action processTransportFailure, ITransportFactory factory)
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
            if (String.IsNullOrEmpty(transportId)) throw new ArgumentNullException("transportId");
            if (!m_KnownIds.Contains(transportId))
                m_KnownIds.Add(transportId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IProcessingGroup GetProcessingGroup(string transportId, string name, Action onFailure)
        {
            addId(transportId);
            var transport = Transport ?? (Transport = m_Factory.Create(m_TransportInfo, processTransportFailure));
            ProcessingGroupWrapper processingGroup;

            lock (m_ProcessingGroups)
            {
                processingGroup = m_ProcessingGroups.FirstOrDefault(g => g.TransportId == transportId && g.Name == name);

                if (processingGroup == null)
                {
                    processingGroup = new ProcessingGroupWrapper(transportId, name);
                    processingGroup.SetProcessingGroup(transport.CreateProcessingGroup(() => processProcessingGroupFailure(processingGroup)));
                    m_ProcessingGroups.Add(processingGroup);
                }
            }

            if (onFailure != null)
                processingGroup.OnFailure += onFailure;
            return processingGroup;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void processTransportFailure()
        {
            ProcessingGroupWrapper[] processingGroupWrappers;
            lock (m_ProcessingGroups)
            {
                processingGroupWrappers = m_ProcessingGroups.ToArray();
            }

            foreach (var processingGroup in processingGroupWrappers)
            {
                processProcessingGroupFailure(processingGroup);
            }

            m_ProcessTransportFailure();
        }

        private void processProcessingGroupFailure(ProcessingGroupWrapper processingGroup)
        {
            lock (m_ProcessingGroups)
            {
                m_ProcessingGroups.Remove(processingGroup);
            }
            processingGroup.ReportFailure();
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if (Transport == null)
                return;

            ProcessingGroupWrapper[] processingGroupWrappers;
            lock (m_ProcessingGroups)
            {
                processingGroupWrappers = m_ProcessingGroups.ToArray();
            }

            foreach (var processingGroupWrapper in processingGroupWrappers)
            {
                processingGroupWrapper.Dispose();
            }

            Transport.Dispose();
            Transport = null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifyDestination(Destination destination, EndpointUsage usage, bool configureIfRequired,out string error)
        {
            var transport = Transport ?? (Transport = m_Factory.Create(m_TransportInfo, processTransportFailure));
            return transport.VerifyDestination(destination, usage, configureIfRequired, out error);
        }

    }

    internal class ProcessingGroupWrapper:IProcessingGroup
    {
        public string TransportId { get; private set; }
        public string Name { get; private set; }
        private IProcessingGroup ProcessingGroup { get; set; }
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
            if (ProcessingGroup == null)
                return;
            ProcessingGroup.Dispose();
            ProcessingGroup = null;
        }

        public void Send(string destination, BinaryMessage message, int ttl)
        {
            ProcessingGroup.Send(destination, message, ttl);
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            return ProcessingGroup.SendRequest(destination, message, callback);
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            return ProcessingGroup.RegisterHandler(destination, handler, messageType);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            return ProcessingGroup.Subscribe(destination,callback, messageType);
        }

        public Destination CreateTemporaryDestination()
        {
            return ProcessingGroup.CreateTemporaryDestination();
        }
    }
}