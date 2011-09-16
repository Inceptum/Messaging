using System;
using System.Collections.Generic;

namespace Inceptum.AppHosting
{
    public interface IApplicationBrowser
    {
        IEnumerable<HostedAppInfo> GetAvailabelApps();
    }
}