using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Inceptum.Messaging.Serialization
{
    public class SerializationManager : ISerializationManager
    {
        private readonly List<ISerializerFactory> m_SerializerFactories = new List<ISerializerFactory>();
        private readonly ReaderWriterLockSlim m_SerializerLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<Tuple<string,Type>, object> m_Serializers = new Dictionary<Tuple<string, Type>, object>();

        #region ISerializationManager Members

        public byte[] Serialize<TMessage>(string format, TMessage message)
        {
            return ExtractSerializer<TMessage>(format).Serialize(message);
        }


        /// <summary>
        /// Deserializes the specified message to application type.
        /// </summary>
        /// <typeparam name="TMessage">The type of the application message.</typeparam>
        /// <param name="format">The format.</param>
        /// <param name="message">The  message.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">Unknown business object type.</exception>
        public TMessage Deserialize<TMessage>(string format, byte[] message)
        {
            return ExtractSerializer<TMessage>(format).Deserialize(message);
        }

        public void RegisterSerializerFactory(ISerializerFactory serializerFactory)
        {
            if (serializerFactory == null) throw new ArgumentNullException("serializerFactory");
            if(string.IsNullOrEmpty(serializerFactory.SerializationFormat))
                throw new ArgumentException("serializerFactory SerializationFormat should return not empty string", "serializerFactory");
            lock (m_SerializerFactories)
            {
                m_SerializerFactories.Add(serializerFactory);
            }
        }

        public void RegisterSerializer(string format, Type targetType, object serializer)
        {
            if (format == null) throw new ArgumentNullException("format");
            if (targetType == null) throw new ArgumentNullException("targetType");
            if (serializer == null) throw new ArgumentNullException("serializer");
            var key = Tuple.Create(format.ToLower(), targetType);
            Type serializerType = serializer.GetType();
            m_SerializerLock.EnterUpgradeableReadLock();
            try
            {
                object oldSerializer;
                if (m_Serializers.TryGetValue(key, out oldSerializer))
                {
                    throw new InvalidOperationException(
                        String.Format(
                            "Can not register '{0}' as {1} serializer for type '{2}'. '{2}' is already assigned with serializer '{3}'",
                            serializerType,format, targetType, oldSerializer.GetType()));
                }

                m_SerializerLock.EnterWriteLock();
                try
                {
                    m_Serializers.Add(key, serializer);
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

        private IMessageSerializer<TMessage> getSerializer<TMessage>(string format)
        {
            object p;
            Type targetType = typeof(TMessage);
            var key = Tuple.Create(format.ToLower(), targetType);
            if (m_Serializers.TryGetValue(key, out p))
            {
                return p as IMessageSerializer<TMessage>;
            }
            return null;
        }

        /// <summary>
        /// Extracts serializer for TMessage type
        /// NORE: this method is internal only for testing purposes.
        /// </summary>
        /// <typeparam name="TMessage">Type of message serializer should be extracted for</typeparam>
        /// <returns>Serializer for TMessage</returns>
        internal IMessageSerializer<TMessage> ExtractSerializer<TMessage>(string format)
        {
            m_SerializerLock.EnterReadLock();
            try
            {
                var messageSerializer = getSerializer<TMessage>(format);
                if (messageSerializer != null)
                    return messageSerializer;
            }
            finally
            {
                m_SerializerLock.ExitReadLock();
            }

            IMessageSerializer<TMessage>[] serializers;
            lock (m_SerializerFactories)
            {
                serializers = m_SerializerFactories.Where(f=>f.SerializationFormat.ToLower()==format.ToLower()).Select(f => f.Create<TMessage>()).Where(s => s != null).ToArray();
            }
            switch (serializers.Length)
            {
                case 1:
                    m_SerializerLock.EnterUpgradeableReadLock();
                    try
                    {
                        m_SerializerLock.EnterWriteLock();
                        try
                        {
                            // double check if no other threads have already registered serializer for TMessage
                            var messageSerializer = getSerializer<TMessage>(format);
                            if (messageSerializer != null)
                                return messageSerializer;

                            IMessageSerializer<TMessage> serializer = serializers[0];
                            RegisterSerializer(format,typeof (TMessage), serializer);
                            return serializer;
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
                case 0:
                    throw new ProcessingException(string.Format("{1} serializer for type {0} not found", typeof (TMessage),format));
                default:
                    throw new ProcessingException(
                        string.Format("More than one {1} serializer is available for for type {0}", typeof (TMessage),format));
            }
        }
    }
}