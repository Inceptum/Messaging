using System;
using System.Linq;
using System.Net.Mime;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Inceptum.Messaging.Transports;
using Sonic.Jms;
using Message = Sonic.Jms.Message;
using QueueConnection = Sonic.Jms.QueueConnection;
using Session = Sonic.Jms.Ext.Session;

namespace Inceptum.Messaging.Sonic
{
    internal abstract class SonicSessionBase<TSession> : IMessagingSession where TSession : class, Session
    {
        private readonly QueueConnection m_Connection;
        private readonly string m_JailedTag;
        private readonly MessageFormat m_MessageFormat;
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();
        private TSession m_Session;

        protected SonicSessionBase(QueueConnection connection, string jailedTag, MessageFormat messageFormat)
        {
            m_JailedTag = jailedTag;
            m_MessageFormat = messageFormat;
            if (connection == null) throw new ArgumentNullException("connection");
            m_Connection = connection;
            JailedSelector = SonicTransportConstants.JAILED_PROPERTY_NAME + " = \'" + m_JailedTag + "\'";
        }


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

        protected string JailedSelector { get; private set; }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(string destination, BinaryMessage message, int ttl)
        {
            ensureSessionIsCreated();
            send(destination, message, ttl);
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback,
            string messageType)
        {
            ensureSessionIsCreated();

            //NOTE: Sonic acknowledge management is not implemented
            return subscribe(createDestination(destination), message => callback(toBinaryMessage(message), b => { }),
                messageType);
        }

         
        [MethodImpl(MethodImplOptions.Synchronized)]
        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            ensureSessionIsCreated();
            TemporaryQueue temporaryQueue = m_Session.createTemporaryQueue();
            var request = new RequestHandle(callback, temporaryQueue.delete,
                cb => subscribe(temporaryQueue, m => cb(toBinaryMessage(m)), null));
            m_Subscriptions.Add(request);
            send(destination, message, SonicTransportConstants.MESSAGE_DEFAULT_LIFESPAN,
                m => m.setJMSReplyTo(temporaryQueue));
            return request;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler,
            string messageType)
        {
            ensureSessionIsCreated();
            //TODO: implement in more appropriate way. Processing should not freeze session thread. Response producer is created and destroyed for each message it is also not good idea
            IDisposable subscription = subscribe(createDestination(destination), request =>
            {
                string jmsCorrelationId = request.getJMSCorrelationID();
                BinaryMessage responseBytes = handler(toBinaryMessage(request));
                lock (this)
                {
                    send(request.getJMSReplyTo(), responseBytes, SonicTransportConstants.MESSAGE_DEFAULT_LIFESPAN,
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

        private void send(string destination, BinaryMessage message, int ttl, Action<Message> tuneMessage = null)
        {
            send(createDestination(destination), message, ttl, tuneMessage);
        }

        private void send(Destination destination, BinaryMessage message, int ttl, Action<Message> tuneMessage = null)
        {
            Message sonicMessage;
            switch (m_MessageFormat)
            {
                case MessageFormat.Text:
                    var textMessage = m_Session.createTextMessage();
                    textMessage.setText(Encoding.UTF8.GetString(message.Bytes ?? new byte[0]));
                    sonicMessage = textMessage;
                    break;
                case MessageFormat.Binary:
                default:
                    var bytesMessage = m_Session.createBytesMessage();
                    bytesMessage.writeBytes(message.Bytes ?? new byte[0]);
                    sonicMessage = bytesMessage;
                    break;
            }

            sonicMessage.setStringProperty(SonicTransportConstants.JAILED_PROPERTY_NAME, m_JailedTag);
            sonicMessage.setJMSType(message.Type);

            if (tuneMessage != null)
                tuneMessage(sonicMessage);
            MessageProducer producer = m_Session.createProducer(destination);

            using (Disposable.Create(producer.close))
            {
                producer.send(sonicMessage, DeliveryMode.PERSISTENT, DefaultMessageProperties.DEFAULT_PRIORITY, ttl);
            }
            //TODO: destroy session
        }

        private BinaryMessage toBinaryMessage(Message sonicMessage)
        {
            var bytesMessage = sonicMessage as BytesMessage;
            if (bytesMessage != null)
            {
                var bytes = new byte[bytesMessage.getBodyLength()];
                bytesMessage.readBytes(bytes);
                return new BinaryMessage {Bytes = bytes, Type = bytesMessage.getJMSType()};
            }

            var textMessage = sonicMessage as TextMessage;
            if (textMessage != null)
            {
                string text = textMessage.getText();
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                return new BinaryMessage {Bytes = bytes, Type = textMessage.getJMSType()};
            }

            throw new InvalidCastException("Message of unsupported type was received. Only binary and text messages are supported");
        }


        private IDisposable subscribe(Destination destination, Action<Message> callback, string messageType)
        {
            IDisposable subscription = null;
            string[] selectors = new[]
            {
                messageType != null ? "JMSType = '" + messageType + "'" : null,
                m_JailedTag != null ? JailedSelector : null
            }.Where(x => x != null).ToArray();

            MessageConsumer consumer = selectors.Length == 0
                ? m_Session.createConsumer(destination)
                : m_Session.createConsumer(destination, string.Join(" AND ", selectors));
            consumer.setMessageListener(new GenericMessageListener(callback));

            subscription = Disposable.Create(() =>
            {
                lock (this)
                {
                    consumer.close();
                    // ReSharper disable AccessToModifiedClosure
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

        public Destination CreateTempDestination()
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

        public abstract Contract.Destination CreateTemporaryDestination();

        protected abstract TSession CreateSession();
    }
}