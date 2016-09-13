using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.Transports;
using WebLogic.Messaging;
using System.Text;

namespace Inceptum.Messaging.Weblogic
{
    internal class WeblogicSession : IMessagingSession
    {
        private readonly IConnection m_Connection;
        private readonly string m_JailedTag;
        private readonly IDictionary<string, string> m_CustomSelectors;
        private readonly IDictionary<string, string> m_CustomHeaders;
        private readonly string m_JailedSelector;
        private readonly CompositeDisposable m_Subscriptions = new CompositeDisposable();
        private ISession m_Session;

        internal WeblogicSession(IConnection connection, string jailedTag, IDictionary<string, string> customHeaders = null, IDictionary<string, string> customSelectors = null)
        {
            if (connection == null) throw new ArgumentNullException("connection");
            m_Connection = connection;

            m_JailedTag = jailedTag;
            m_CustomSelectors = customSelectors ?? new Dictionary<string, string>();
            m_CustomHeaders = customHeaders ?? new Dictionary<string, string>();
            m_JailedSelector = TransportConstants.JAILED_PROPERTY_NAME + " = \'" + m_JailedTag + "\'";
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(string destination, BinaryMessage message, int ttl)
        {
            ensureSessionIsCreated();
            send(destination, message, ttl);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable Subscribe(string destination, Action<BinaryMessage, Action<bool>> callback, string messageType)
        {
            ensureSessionIsCreated();
            return subscribe(createDestination(destination), message => callback(convert(message), b => { }), messageType);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public RequestHandle SendRequest(string destination, BinaryMessage message, Action<BinaryMessage> callback)
        {
            throw new NotImplementedException("Weblogic JMS .NET client does not support temporary destinations");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IDisposable RegisterHandler(string destination, Func<BinaryMessage, BinaryMessage> handler, string messageType)
        {
            ensureSessionIsCreated();
            return subscribe(createDestination(destination), request =>
            {
                string jmsCorrelationId = request.JMSCorrelationID;
                BinaryMessage responseBytes = handler(convert(request));
                lock (this)
                {
                    send(request.JMSReplyTo, responseBytes, TransportConstants.MESSAGE_DEFAULT_LIFESPAN, message => message.JMSCorrelationID = jmsCorrelationId);
                }
            }, messageType);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            m_Subscriptions.Dispose();
            if (m_Session != null)
            {
                m_Session.Close();
                m_Session = null;
            }
        }

        private void send(string destination, BinaryMessage message, int ttl, Action<IMessage> tuneMessage = null)
        {
            send(createDestination(destination), message, ttl, tuneMessage);
        }

        private void send(IDestination destination, BinaryMessage message, int ttl, Action<IMessage> tuneMessage = null)
        {
            ITextMessage textMessage = m_Session.CreateTextMessage();
            textMessage.Text = Encoding.UTF8.GetString(message.Bytes ?? new byte[0]);
            IMessage sonicMessage = textMessage;

            sonicMessage.SetStringProperty(TransportConstants.JAILED_PROPERTY_NAME, m_JailedTag);
            sonicMessage.JMSType = message.Type;
            if (tuneMessage != null) tuneMessage(sonicMessage);

            foreach (var param in m_CustomHeaders)
            {
                sonicMessage.SetStringProperty(param.Key, param.Value);
            }

            IMessageProducer producer = m_Session.CreateProducer(destination);

            using (Disposable.Create(producer.Close))
            {
                producer.Send(sonicMessage, Constants.DeliveryMode.PERSISTENT, TransportConstants.MESSAGE_DEFAULT_PRIORITY, ttl);
            }
        }

        private BinaryMessage convert(IMessage sonicMessage)
        {
            var textMessage = sonicMessage as ITextMessage;
            if (textMessage != null)
            {
                string text = textMessage.Text;
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                return new BinaryMessage { Bytes = bytes, Type = textMessage.JMSType };
            }

            throw new InvalidCastException("Message of unsupported type was received. Only text messages are supported");
        }


        private IDisposable subscribe(IDestination destination, Action<IMessage> callback, string messageType)
        {
            IDisposable subscription = null;
            var selectors = new List<string>();
            selectors.Add(messageType != null ? "JMSType = '" + messageType + "'" : null);
            selectors.Add(m_JailedTag != null ? m_JailedSelector : null);
            selectors.AddRange(m_CustomSelectors.Select(param => param.Key + " = '" + param.Value + "'"));
            var selectorsArray = selectors.Where(x => x != null).ToArray();

            IMessageConsumer consumer = selectorsArray.Length == 0
                ? m_Session.CreateConsumer(destination)
                : m_Session.CreateConsumer(destination, string.Join(" AND ", selectorsArray));

            consumer.Message += (sender, args) => callback(args.Message);

            subscription = Disposable.Create(() =>
            {
                lock (this)
                {
                    consumer.Close();
                    // ReSharper disable AccessToModifiedClosure
                    m_Subscriptions.Remove(subscription);
                    // ReSharper restore AccessToModifiedClosure
                    if (m_Subscriptions.Count == 0)
                    {
                        m_Session.Close();
                        m_Session = null;
                    }
                }
            });
            m_Subscriptions.Add(subscription);
            return subscription;
        }

        public virtual Destination CreateTemporaryDestination()
        {
            throw new NotImplementedException("Weblogic JMS .NET client does not support temporary destinations");
        }

        private void ensureSessionIsCreated()
        {
            if (m_Session == null)
            {
                m_Session = createSession();
            }
        }

        private IDestination createDestination(string name)
        {
            ensureSessionIsCreated();

            return isQueue(name)
                ? (IDestination)m_Session.CreateQueue(name.Substring(8))
                : (IDestination)m_Session.CreateTopic(name.Substring(8));
        }

        private bool isQueue(string destination)
        {
            if (destination.StartsWith("queue://", true, CultureInfo.InvariantCulture))
            {
                return true;
            }

            if (destination.StartsWith("topic://", true, CultureInfo.InvariantCulture))
            {
                return false;
            }

            throw new InvalidOperationException("Wrong destination name: " + destination + ". Should start with 'queue://' or 'topic://'");
        }

        private ISession createSession()
        {
            return m_Connection.CreateSession(Constants.SessionMode.AUTO_ACKNOWLEDGE);
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(string destination, BinaryMessage message, int ttl, ReplyTo replyTo)
        {
            ensureSessionIsCreated();
            send(destination, message, ttl, tuneMessage =>
            {
                tuneMessage.JMSReplyTo = createDestination(replyTo.Destination);
                if (replyTo.CorrelationId != null)
                    tuneMessage.JMSCorrelationID = replyTo.CorrelationId;
            });
        }

    }
}