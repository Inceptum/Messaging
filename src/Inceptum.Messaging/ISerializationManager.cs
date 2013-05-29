using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Inceptum.Messaging
{
    public interface ISerializationManager
    {
        byte[] Serialize<TMessage>(TMessage message);
        TMessage Deserialize<TMessage>(byte[] message);
        void RegisterSerializer(Type targetType, object serializer);
        void RegisterSerializerFactory(ISerializerFactory serializerFactory);
    }

    public static class SerializationManagerExtensions
    {
        static readonly Dictionary<Type, Func<ISerializationManager,byte[],  object>> m_Deserializers = new Dictionary<Type, Func<ISerializationManager,byte[],  object>>();
        static readonly Dictionary<Type, Func<ISerializationManager , object,byte[]>> m_Serializers = new Dictionary<Type, Func<ISerializationManager,  object,byte[]>>();
        public static byte[] SerializeObject(this ISerializationManager manager, object message)
        {
            if (message == null)
                return null;
             Func<ISerializationManager, object, byte[]> serialize;
             lock (m_Serializers)
            {
                var type = message.GetType();
                if (!m_Serializers.TryGetValue(type, out serialize))
                {
                    serialize = CreateSerializer(type);
                    m_Serializers.Add(type, serialize);
                }
            }
            return serialize(manager, message);
        }

        public static object Deserialize(this ISerializationManager manager, byte[] message, Type type)
        {
            Func<ISerializationManager, byte[], object> deserialize;
            lock (m_Deserializers)
            {
                if (!m_Deserializers.TryGetValue(type, out deserialize))
                {
                    deserialize = CreateDeserializer(type);
                    m_Deserializers.Add(type, deserialize);
                }
            }
            return deserialize(manager, message);
        }

        private static Func<ISerializationManager,byte[],object> CreateDeserializer(Type type)
        {
            var manger = Expression.Parameter(typeof(ISerializationManager), "manger");
            var message = Expression.Parameter(typeof(byte[]), "message");
            var call = Expression.Call(manger, "Deserialize", new[] { type }, message);
            var convert=Expression.Convert(call, typeof(object));
            var lambda = (Expression<Func<ISerializationManager,byte[], object>>)Expression.Lambda(convert,manger,message);
            return lambda.Compile();
        }

        private static Func<ISerializationManager, object, byte[]> CreateSerializer(Type type)
        {
            var manger = Expression.Parameter(typeof(ISerializationManager), "manger");
            var message = Expression.Parameter(typeof(object), "message");
            var call = Expression.Call(manger, "Serialize", new[] { type }, Expression.Convert(message,type));
            var lambda = (Expression<Func<ISerializationManager, object, byte[]>>)Expression.Lambda(call, manger, message);
            return lambda.Compile();
        }

    }
}
