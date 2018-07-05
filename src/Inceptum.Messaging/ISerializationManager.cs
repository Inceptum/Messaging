using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Inceptum.Messaging
{
    public interface ISerializationManager
    {
        byte[] Serialize<TMessage>(string format,TMessage message);
        TMessage Deserialize<TMessage>(string format, byte[] message);
        string GetMessageTypeString<TMessage>(string format);
        void RegisterSerializer(string format, Type targetType, object serializer);
        void RegisterSerializerFactory(ISerializerFactory serializerFactory);
    }

    public static class SerializationManagerExtensions
    {
        static readonly Dictionary<Type, Func<ISerializationManager, string, byte[], object>> m_Deserializers = new Dictionary<Type, Func<ISerializationManager, string, byte[], object>>();
        static readonly Dictionary<Type, Func<ISerializationManager, string, object, byte[]>> m_Serializers = new Dictionary<Type, Func<ISerializationManager, string, object, byte[]>>();
        static readonly Dictionary<Type, Func<ISerializationManager, string, string>> m_GetMessageTypes = new Dictionary<Type, Func<ISerializationManager, string, string>>();

        public static byte[] SerializeObject(this ISerializationManager manager, string format, object message)
        {
            if (message == null)
                return null;
            Func<ISerializationManager, string, object, byte[]> serialize;
            lock (m_Serializers)
            {
                var type = message.GetType();
                if (!m_Serializers.TryGetValue(type, out serialize))
                {
                    serialize = CreateSerializer(type);
                    m_Serializers.Add(type, serialize);
                }
            }
             return serialize(manager, format, message);
        }

        public static object Deserialize(this ISerializationManager manager, string format, byte[] message, Type type)
        {
            Func<ISerializationManager, string, byte[], object> deserialize;
            lock (m_Deserializers)
            {
                if (!m_Deserializers.TryGetValue(type, out deserialize))
                {
                    deserialize = CreateDeserializer(type);
                    m_Deserializers.Add(type, deserialize);
                }
            }
            return deserialize(manager, format, message);
        }

        public static string GetMessageTypeStringObject(this ISerializationManager manager, string format, Type type)
        {
            if (type == null)
                return null;
            Func<ISerializationManager, string, string> getMessageType;
            lock (m_Serializers)
            {
                if (!m_GetMessageTypes.TryGetValue(type, out getMessageType))
                {
                    getMessageType = CreateGetMessageType(type);
                    m_GetMessageTypes.Add(type, getMessageType);
                }
            }
            return getMessageType(manager, format);
        }

        private static Func<ISerializationManager, string, byte[], object> CreateDeserializer(Type type)
        {
            var format = Expression.Parameter(typeof(string), "format");
            var manger = Expression.Parameter(typeof(ISerializationManager), "manger");
            var message = Expression.Parameter(typeof(byte[]), "message");
            var call = Expression.Call(manger, "Deserialize", new[] { type }, format, message);
            var convert=Expression.Convert(call, typeof(object));
            var lambda = (Expression<Func<ISerializationManager, string, byte[], object>>)Expression.Lambda(convert, manger, format, message);
            return lambda.Compile();
        }

        private static Func<ISerializationManager, string, object, byte[]> CreateSerializer(Type type)
        {
            var format = Expression.Parameter(typeof(string), "format");
            var manger = Expression.Parameter(typeof(ISerializationManager), "manger");
            var message = Expression.Parameter(typeof(object), "message");
            var call = Expression.Call(manger, "Serialize", new[] { type },format, Expression.Convert(message,type));
            var lambda = (Expression<Func<ISerializationManager, string, object, byte[]>>)Expression.Lambda(call, manger, format,message);
            return lambda.Compile();
        }

        private static Func<ISerializationManager, string, string> CreateGetMessageType(Type type)
        {
            var format = Expression.Parameter(typeof(string), "format");
            var manger = Expression.Parameter(typeof(ISerializationManager), "manger");
            var call = Expression.Call(manger, "GetMessageTypeString", new[] { type }, format);
            var lambda = (Expression<Func<ISerializationManager, string, string>>)Expression.Lambda(call, manger, format);
            return lambda.Compile();
        }
    }
}
