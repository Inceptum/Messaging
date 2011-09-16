using Sonic.Jms;

namespace Inceptum.Messaging
{
    public interface IMessageSerializer<TMessage>
    {
        Message Serialize(TMessage message, Session sendSession);
        TMessage Deserialize(Message message);
    }
}