using System.Threading;
using VRage;

namespace DefenseShields.Support
{
    internal class DsAutoResetEvent
    {
        private int _waiters;
        private readonly FastResourceLock _lock = new FastResourceLock();

        public void WaitOne()
        {
            _lock.AcquireExclusive();
            _waiters = 1;
            _lock.AcquireExclusive();
            _lock.ReleaseExclusive();
        }

        public void Set()
        {
            if (Interlocked.Exchange(ref _waiters, 0) > 0)
                _lock.ReleaseExclusive();
        }
    }
}
