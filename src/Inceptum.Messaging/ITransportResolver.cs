namespace Inceptum.Messaging
{
    public interface ITransportResolver
    {
        TransportInfo GetTransport(string transportId);
    }
}
