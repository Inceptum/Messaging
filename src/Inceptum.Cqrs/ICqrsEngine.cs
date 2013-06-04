namespace Inceptum.Cqrs
{
    public interface ICqrsEngine
    {
        void Init();
        CommandDispatcher CommandDispatcher { get; }

        EventDispatcher EventDispatcher { get; }
    }
}