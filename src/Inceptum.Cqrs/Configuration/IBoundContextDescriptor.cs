namespace Inceptum.Cqrs.Configuration
{
    interface IBoundContextDescriptor
    {
        void Apply(BC boundContext);
    }
}