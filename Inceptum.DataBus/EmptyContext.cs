namespace Inceptum.DataBus
{
    public sealed class EmptyContext
    {
        public static readonly EmptyContext Value = new EmptyContext();

        private EmptyContext()
        {
        }
    }
}