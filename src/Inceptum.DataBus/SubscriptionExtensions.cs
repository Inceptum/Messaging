using System;
using Db.Aces.Platform.Transport.Tibco;

namespace Inceptum.DataBus
{
    public class DeferredSubscription: SafeCompositeDisposable
    {
        private volatile bool _finished ;
        private Action _callback;

        public void SetCallback(Action callback)
        {
            _callback = callback;
            if (_finished)
                _callback();
        }

        public void FinishSubscription()
        {
            if (!_finished)
            {
                _finished = true;
                var callback = _callback;
                if(callback!=null)
                    callback();
            }
        }
    }
}
