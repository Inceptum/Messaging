namespace Inceptum.Cqrs.Configuration
{
    class NameDescriptor : IBoundedContextDescriptor
    {
        private string m_Name;

        public NameDescriptor(string name)
        {
            m_Name = name;
        }

        public void Create(BoundedContext boundedContext)
        {
            boundedContext.Name = m_Name;
        }
    }
}