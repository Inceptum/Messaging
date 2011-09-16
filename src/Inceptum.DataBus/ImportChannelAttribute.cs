using System;

namespace Inceptum.DataBus
{
    /// <summary>
    /// Indicates that a property should be injected with the channel. This class cannot be inherited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ImportChannelAttribute : Attribute
    {
        private readonly string _name;

        public ImportChannelAttribute(string name)
        {
            _name = name;
        }

        public ImportChannelAttribute()
        {
        }

        public string Name
        {
            get { return _name; }
        }
    }
}