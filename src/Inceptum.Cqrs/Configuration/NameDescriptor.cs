namespace Inceptum.Cqrs.Configuration
{
    class NameDescriptor : IBoundContextDescriptor
    {
        private string m_Name;

        public NameDescriptor(string name)
        {
            m_Name = name;
        }

        public void Apply(BC boundContext)
        {
            boundContext.Name = m_Name;
        }
    }
}