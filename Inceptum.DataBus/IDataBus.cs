namespace Inceptum.DataBus
{
    public interface IDataBus
    {
        IChannel<TData> Channel<TData>(string channelName);
    }
}