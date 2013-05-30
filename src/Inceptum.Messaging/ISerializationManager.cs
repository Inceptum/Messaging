using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Inceptum.Messaging
{
    public interface ISerializationManager
    {
        byte[] Serialize<TMessage>(string format,TMessage message);
        TMessage Deserialize<TMessage>(string format, byte[] message);
        void RegisterSerializer(string format, Type targetType, object serializer);
        void RegisterSerializerFactory(ISerializerFactory serializerFactory);
    }

    public static class SerializationManagerExtensions
    {
        static readonly Dictionary<Type, Func<ISerializationManager, string, byte[], object>> m_Deserializers = new Dictionary<Type, Func<ISerializationManager, string, byte[], object>>();
        static readonly Dictionary<Type, Func<ISerializationManager, string, object, byte[]>> m_Serializers = new Dictionary<Type, Func<ISerializationManager, string, object, byte[]>>();
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

    }
}
