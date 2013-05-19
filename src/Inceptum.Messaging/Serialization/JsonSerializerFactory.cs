namespace Inceptum.Messaging.Serialization
{
    public class JsonSerializerFactory : ISerializerFactory
    {
        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            return new JsonSerializer<TMessage>();
        }
    }
}