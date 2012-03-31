using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Sonic.Jms;
using QueueConnection = Sonic.Jms.QueueConnection;
using Session = Sonic.Jms.Ext.Session;

namespace Inceptum.Messaging.Transports
{
    internal static class ProcessingGroup
    {
        public static IProcessingGroup Create(QueueConnection connection, bool isQueueGroup, string jailedSelector)
        {
            if (isQueueGroup) 
                return new QueueProcessingGroup(connection,jailedSelector);
            return new TopicProcessingGroup(connection, jailedSelector);
        }
    }


    internal abstract class ProcessingGroup<TSession> : IProcessingGroup
        where TSession : class,Session
    {
        private readonly QueueConnection m_Connection;
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();
        private TSession m_Session;
        private readonly string m_JailedTag;

        protected TSession Session
        {
            get { return m_Session; }
        }

        protected QueueConnection Connection
        {
            get { return m_Connection; }
        }

        protected CompositeDisposable Subscriptions
        {
            get { return m_Subscriptions; }
        }

        protected ProcessingGroup(QueueConnection connection, string jailedTag)
        {
            m_JailedTag = jailedTag;
            if (connection == null) throw new ArgumentNullException("connection");
            m_Connection = connection;
            JailedSelector = MessagingEngine.JAILED_PROPERTY_NAME + " = \'" + m_JailedTag + "\'";
        }

        protected string JailedSelector
        {
            get ; private set;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(string destination, BinaryMessage message)
        {
            ensureSessionIsCreated();
            send(destination, message);
        }

        private void send(string destination, BinaryMessage message, Action<Message> tuneMessage = null)
        {
            send(createDestination(destination), message,tuneMessage);
        }
        private void send(Destination destination, BinaryMessage message, Action<Message> tuneMessage = null)
        {
            
           
            var bytesMessage = m_Session.createBytesMessage();
            bytesMessage.writeBytes(message.Bytes??new byte[0]);
            bytesMessage.setStringProperty(MessagingEngine.JAILED_PROPERTY_NAME, m_JailedTag);
            bytesMessage.setJMSType(message.Type);
            if (tuneMessage != null)
                tuneMessage(bytesMessage);
            var producer = m_Session.createProducer(destination);

            using (Disposable.Create(producer.close))
            {
                producer.send(bytesMessage, DeliveryMode.PERSISTENT, DefaultMessageProperties.DEFAULT_PRIORITY,
                              MessagingEngine.MESSAGE_LIFESPAN);
            }
            //TODO: destroy session
        }


                
        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable Subscribe(string destination, Action<BinaryMessage> callback, string messageType)
        {
            ensureSessionIsCreated();

            return subscribe(createDestination(destination), message => callback(new BinaryMessage(message)), messageType);
        }


        private IDisposable subscribe(Destination destination, Action<Message> callback, string messageType)
        {
            IDisposable subscription = null;
            var selectors = new []
                              {
                                  messageType!=null?"JMSType = '"+messageType+"'":null,
                                  m_JailedTag!=null?JailedSelector:null
                              }.Where(x=>x!=null).ToArray();

            var consumer = selectors.Length==0?m_Session.createConsumer(destination) : m_Session.createConsumer(destination, string.Join(" AND ",selectors));
            consumer.setMessageListener(new GenericMessageListener(callback));
             
            subscription = Disposable.Create(() =>
            {
                lock (this)
                {
                    consumer.close();
                    // ReSharper disable AccessToModifiedClosure
                    // Closure.
                    m_Subscriptions.Remove(subscription);
                    // ReSharper restore AccessToModifiedClosure
                    if (m_Subscriptions.Count == 0)
                    {
                        m_Session.close();
                        m_Session = null;
                    }
                }
            });
            m_Subscriptions.Add(subscription);

            return subscription;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            ensureSessionIsCreated();
            var temporaryQueue = m_Session.createTemporaryQueue();

            IDisposable subscription = Disposable.Empty;
            subscription = subscribe(temporaryQueue, m =>
                                                {
                                                    try
                                                    {
                                                        callback(new BinaryMessage(m));
                                                    }
                                                    finally
                                                    {
// ReSharper disable AccessToModifiedClosure
                                                        subscription.Dispose();
// ReSharper restore AccessToModifiedClosure
                                                        temporaryQueue.delete();                                                            
                                                    }
                                                }, null);
            m_Subscriptions.Add(subscription);
            send(destination, message, m => m.setJMSReplyTo(temporaryQueue));
            return subscription;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            ensureSessionIsCreated();
            //TODO: implement in more appropriate way. Processing should not freeze session thread. Response producer is created and destroyed for each message it is also not good idea
            var subscription = subscribe(createDestination(destination), request =>
                                                        {

                                                            var jmsCorrelationId = request.getJMSCorrelationID();
                                                            var responseBytes = handler(new BinaryMessage(request));
                                                            lock (this)
                                                            {
                                                                send(request.getJMSReplyTo(), responseBytes,
                                                                     message => message.setJMSCorrelationID(jmsCorrelationId));
                                                            }
                                                        }, messageType);
            return subscription;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            m_Subscriptions.Dispose();
            if (m_Session != null)
            {
                m_Session.close();
                m_Session = null;
            }
        }

        public  Destination CreateTempDestination()
        {
            ensureSessionIsCreated();
            return Session.createTemporaryQueue();
        }

        private void ensureSessionIsCreated()
        {
            if (m_Session == null)
            {
                m_Session = CreateSession();
                //TODO: need to enable for prod environments
                m_Session.setFlowControlDisabled(true);
            }
        }

        protected abstract Destination CreateDestination(string name);
        private Destination createDestination(string name)
        {
            ensureSessionIsCreated();
            return CreateDestination(name);
        }
        protected abstract TSession CreateSession();
    }

}