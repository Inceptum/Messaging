using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Inceptum.Messaging.Transports;

namespace Inceptum.Messaging.InMemoryTransport
{
    public class InMemoryTransportFactory : ITransportFactory
    {
        readonly Dictionary<TransportInfo,InMemoryTransport> m_Transports=new Dictionary<TransportInfo, InMemoryTransport>(); 
        public string Name { get { return "InMemory"; } }
        public ITransport Create(TransportInfo transportInfo, Action onFailure)
        {
            lock (m_Transports)
            {
                InMemoryTransport transport;
                if (!m_Transports.TryGetValue(transportInfo, out transport))
                {
                    transport=new InMemoryTransport();
                    m_Transports.Add(transportInfo,transport);
                }
                return transport;
            }
        }
    }

    internal class InMemoryTransport : ITransport
    {
        readonly QueuesCollection m_Queues = new QueuesCollection();

        public void Dispose()
        {

        }

        public IProcessingGroup CreateProcessingGroup(Action onFailure)
        {
            return new InMemoryProcessingGroup(m_Queues);
        }
    }

    class QueuesCollection
    {
        readonly Dictionary<string, ConcurrentQueue<BinaryMessage>> m_Queues = new Dictionary<string, ConcurrentQueue<BinaryMessage>>();

        public ConcurrentQueue<BinaryMessage> this[string name]
        {
            get
            {
                lock (m_Queues)
                {
                    ConcurrentQueue<BinaryMessage> queue;
                    if (!m_Queues.TryGetValue(name, out queue))
                    {
                        queue = new ConcurrentQueue<BinaryMessage>();
                        m_Queues.Add(name, queue);
                    }
                    return queue;
                }
            }
        }
    }

    internal class InMemoryProcessingGroup : IProcessingGroup
    {
        private readonly QueuesCollection m_Queues;
        Thread m_Thread;
        


        public InMemoryProcessingGroup(QueuesCollection queues)
        {
            m_Queues = queues;
            m_Thread = new Thread(worker);
        }

        private void worker(object o)
        {
            
        }



        public void Send(string destination, BinaryMessage message, int ttl)
        {
            m_Queues[destination].Enqueue(message);
        }

        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType)
        {
            throw new NotImplementedException();
        }

        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            throw new NotImplementedException();
        }

        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            throw new NotImplementedException();
        }



        public void Dispose()
        {
            throw new NotImplementedException();
        }
      
    }
}