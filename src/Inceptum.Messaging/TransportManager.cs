using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.InMemory;
using Inceptum.Messaging.Transports;
using Inceptum.Messaging.Utils;

namespace Inceptum.Messaging
{
    internal interface ITransportManager : IDisposable
    {
        event TransportEventHandler TransportEvents;
        IPrioritisedProcessingGroup GetProcessingGroup(string transportId, string name, Action onFailure = null);
    }

    internal class TransportManager : ITransportManager
    {
        private class ResolvedTransport : IDisposable
        {
            private class ProcessingGroupWrapper : IDisposable, IPrioritisedProcessingGroup
            {
                private readonly QueuedTaskScheduler m_TaskScheduler;
                private readonly Dictionary<int,TaskFactory> m_TaskFactories=new Dictionary<int, TaskFactory>();
                public string TransportId { get; private set; }
                public string Name { get; private set; }
                private IProcessingGroup ProcessingGroup { get;  set; }
                public event Action OnFailure;

                public ProcessingGroupWrapper(string transportId, string name, ProcessingGroupInfo processingGroupInfo)
                {
                    TransportId = transportId;
                    Name = name;
                    var threadCount = Math.Max(processingGroupInfo.ConcurrencyLevel, 1);
                    m_TaskScheduler = new QueuedTaskScheduler(threadCount);
                    m_TaskFactories = new Dictionary<int, TaskFactory>();
                }

                private TaskFactory getTaskFactory(int priority)
                {
                    if(priority<0)
                        throw new ArgumentException("priority should be >0","priority");
                    lock (m_TaskFactories)
                    {
                        TaskFactory factory;
                        if (!m_TaskFactories.TryGetValue(priority, out factory))
                        {
                            var scheduler = m_TaskScheduler.ActivateNewQueue(priority);
                            factory=new TaskFactory(scheduler);
                            m_TaskFactories.Add(priority,factory);
                        }
                        return factory;
                    }
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
                    ProcessingGroup.Send(destination,message, ttl);
                }

                public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
                {
                    return ProcessingGroup.SendRequest(destination, message, callback);
                }

                public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
                {
                    return ProcessingGroup.RegisterHandler(destination, handler, messageType);
                }

                public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType,int priority)
                {
                    var taskFactory = getTaskFactory(priority);
                    return ProcessingGroup.Subscribe(destination, (message, ack) => taskFactory.StartNew(() => callback(message, ack)), messageType);
                }

                public Destination CreateTemporaryDestination()
                {
                    return ProcessingGroup.CreateTemporaryDestination();
                }
            }

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
                if (string.IsNullOrEmpty(transportId)) throw new ArgumentNullException("transportId");
                if (!m_KnownIds.Contains(transportId))
                    m_KnownIds.Add(transportId);
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public IPrioritisedProcessingGroup GetProcessingGroup(string transportId, string name, Action onFailure)
            {
                addId(transportId);
                var transport = Transport ?? (Transport = m_Factory.Create(m_TransportInfo, processTransportFailure));
                ProcessingGroupWrapper processingGroup;

                lock (m_ProcessingGroups)
                {
                    processingGroup = m_ProcessingGroups.FirstOrDefault(g => g.TransportId == transportId && g.Name == name);

                    if (processingGroup == null)
                    {
                        ProcessingGroupInfo processingGroupInfo;
                        if (!m_TransportInfo.ProcessingGroups.TryGetValue(name, out processingGroupInfo))
                            processingGroupInfo = new ProcessingGroupInfo { ConcurrencyLevel = 1 };

                        processingGroup = new ProcessingGroupWrapper(transportId, name, processingGroupInfo);
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

                foreach (var processinGroupWrapper in processingGroupWrappers)
                {
                    processinGroupWrapper.Dispose();
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


        private readonly Dictionary<TransportInfo, ResolvedTransport> m_Transports = new Dictionary<TransportInfo, ResolvedTransport>();
        private readonly ITransportResolver m_TransportResolver;
        private readonly ManualResetEvent m_IsDisposed = new ManualResetEvent(false);
        private readonly ITransportFactory[] m_TransportFactories;


        public TransportManager(ITransportResolver transportResolver, params ITransportFactory[] transportFactories)
        {
            m_TransportFactories = transportFactories.Concat(new[] {new InMemoryTransportFactory()}).ToArray();
            if (transportResolver == null) throw new ArgumentNullException("transportResolver");
            m_TransportResolver = transportResolver;
        }

        public ITransportResolver TransportResolver
        {
            get { return m_TransportResolver; }
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

        public event TransportEventHandler TransportEvents;

        public IPrioritisedProcessingGroup GetProcessingGroup(string transportId, string name, Action onFailure = null)
        {
            ResolvedTransport transport = resolveTransport(transportId);

            try
            {
                return transport.GetProcessingGroup(transportId, name, onFailure);
            }
            catch (Exception e)
            {
                throw new TransportException(string.Format("Failed to create processing group {0} on transport {1}", name, transportId), e);
            }
        }

        private ResolvedTransport resolveTransport(string transportId)
        {
            if (m_IsDisposed.WaitOne(0))
                throw new ObjectDisposedException(string.Format("Can not create transport {0}. TransportManager instance is disposed", transportId));


            var transportInfo = m_TransportResolver.GetTransport(transportId);

            if (transportInfo == null)
                throw new ConfigurationErrorsException(string.Format("Transport '{0}' is not resolvable", transportId));
            var factory = m_TransportFactories.FirstOrDefault(f => f.Name == transportInfo.Messaging);
            if (factory == null)
                throw new ConfigurationErrorsException(string.Format("Can not create transport '{0}', {1} messaging is not supported", transportId,
                    transportInfo.Messaging));

            ResolvedTransport transport;

            if (!m_Transports.TryGetValue(transportInfo, out transport))
            {
                lock (m_Transports)
                {
                    if (!m_Transports.TryGetValue(transportInfo, out transport))
                    {
                        transport = new ResolvedTransport(transportInfo, () => ProcessTransportFailure(transportInfo), factory);
                        if (m_Transports.ContainsKey(transportInfo))
                            m_Transports.Remove(transportInfo);
                        m_Transports.Add(transportInfo, transport);
                    }
                }
            }
            return transport;
        }

        internal virtual void ProcessTransportFailure(TransportInfo transportInfo)
        {
            ResolvedTransport transport;
            lock (m_Transports)
            {
                if (!m_Transports.TryGetValue(transportInfo, out transport))
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

        public bool VerifyDestination(string transportId, Destination destination, EndpointUsage usage, bool configureIfRequired,out string error)
        {
            ResolvedTransport transport = resolveTransport(transportId);

            try
            {
                return transport.VerifyDestination(destination, usage, configureIfRequired,out error);
            }
            catch (Exception e)
            {
                throw new TransportException(string.Format("Destination {0} is not properly configured on transport {1}", destination, transportId), e);
            }
        }

    }
}