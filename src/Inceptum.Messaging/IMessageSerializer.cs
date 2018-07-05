namespace Inceptum.Messaging
{
    public interface IMessageTypeStringProvider
    {
        string GetMessageTypeString();
    }

    public interface IMessageSerializer<TMessage>
    {
        byte[] Serialize(TMessage message);
        TMessage Deserialize(byte[] message);
    }
}
