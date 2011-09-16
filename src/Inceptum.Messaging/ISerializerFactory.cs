namespace Inceptum.Messaging
{
    public interface ISerializerFactory
    {
        IMessageSerializer<TMessage> Create<TMessage>();
    }
}