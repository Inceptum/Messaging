using System.Collections.Generic;

namespace Inceptum.AppHosting
{
    internal interface IApplicationHost
    {
        void Start();
        void Stop();
    }
}