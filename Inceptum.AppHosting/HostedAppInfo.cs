using System;

namespace Inceptum.AppHosting
{
    [Serializable]
    public class HostedAppInfo
    {
        public HostedAppInfo(string name, string appType):
            this(name, appType, AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        public HostedAppInfo(string name, string appType, string baseDirectory)
        {
            Name = name;
            AppType = appType;
            BaseDirectory = baseDirectory;
        }

        public string Name { get; set; }
        public string AppType { get; set; }
        public string BaseDirectory { get; set; }
        public string ConfigFile { get; set; }
        public string Version { get; set; }
    }
}