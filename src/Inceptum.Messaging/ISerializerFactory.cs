namespace Inceptum.Messaging
{
    public interface ISerializerFactory
    {
        string SerializationFormat { get; }
        IMessageSerializer<TMessage> Create<TMessage>();
    }
}
