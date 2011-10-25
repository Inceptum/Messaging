using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Sonic.Jms;

namespace Inceptum.Messaging
{
    public class SerializationManager : ISerializationManager
    {
        private readonly List<ISerializerFactory> m_SerializerFactories = new List<ISerializerFactory>();
        private readonly ReaderWriterLockSlim m_SerializerLock = new ReaderWriterLockSlim();
        private readonly Dictionary<Type, object> m_Serializers = new Dictionary<Type, object>();

        #region ISerializationManager Members

        public Message Serialize<TMessage>(TMessage message, Session sendSession)
        {
            return extractSerializer<TMessage>().Serialize(message, sendSession);
        }


        /// <summary>
        /// Deserializes the specified sonic message to application type.
        /// </summary>
        /// <typeparam name="TMessage">The type of the application message.</typeparam>
        /// <param name="message">The sonic message.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">Unknown business object type.</exception>
        public TMessage Deserialize<TMessage>(Message message)
        {
            Debug.Assert(message != null);
            return extractSerializer<TMessage>().Deserialize(message);
        }

        public void RegisterSerializerFactory(ISerializerFactory serializerFactory)
        {
            if (serializerFactory == null) throw new ArgumentNullException("serializerFactory");
            lock (m_SerializerFactories)
            {
                m_SerializerFactories.Add(serializerFactory);
            }
        }

        public void RegisterSerializer(Type targetType, object serializer)
        {
            Type serializerType = serializer.GetType();
            m_SerializerLock.EnterUpgradeableReadLock();
            try
            {
                object oldSerializer;
                if (m_Serializers.TryGetValue(targetType, out oldSerializer))
                {
                    throw new InvalidOperationException(String.Format("Can not register '{0}' as serializer for type '{1}'. '{1}' is already assigned with serializer '{2}'", serializerType, targetType, oldSerializer.GetType()));
                }

                m_SerializerLock.EnterWriteLock();
                try
                {
                    m_Serializers.Add(targetType, serializer);
                }
                finally
                {
                    m_SerializerLock.ExitWriteLock();
                }
            }
            finally
            {
                m_SerializerLock.ExitUpgradeableReadLock();
            }
        }

        #endregion

        private IMessageSerializer<TMessage> extractSerializer<TMessage>()
        {
            m_SerializerLock.EnterReadLock();
            try
            {
                object p;
                Type targetType = typeof (TMessage);
                if (m_Serializers.TryGetValue(targetType, out p))
                {
                    var serializer = p as IMessageSerializer<TMessage>;
                    if (serializer != null)
                        return serializer;
                }
            }
            finally
            {
                m_SerializerLock.ExitReadLock();
            }

            IMessageSerializer<TMessage>[] serializers;
            lock (m_SerializerFactories)
            {
                serializers = m_SerializerFactories.Select(f => f.Create<TMessage>()).Where(s => s != null).ToArray();
            }
            switch (serializers.Length)
            {
                case 1:
                    IMessageSerializer<TMessage> serializer = serializers[0];
                    RegisterSerializer(typeof (TMessage), serializer);
                    return serializer;
                case 0:
                    throw new ProcessingException(string.Format("Serializer for type {0} not found", typeof (TMessage)));
                default:
                    throw new ProcessingException(string.Format("More than one serializer is available for for type {0}", typeof (TMessage)));
            }
        }
    }
}