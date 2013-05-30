namespace Inceptum.Messaging.Serialization
{
    public class JsonSerializerFactory : ISerializerFactory
    {
        public string SerializationFormat {
            get { return "json"; }
        }

        public IMessageSerializer<TMessage> Create<TMessage>()
        {
            return new JsonSerializer<TMessage>();
        }
    }
}