namespace DefenseShields.Support
{
    using System.Threading;
    using VRage;

    internal class DsAutoResetEvent
    {
        private readonly FastResourceLock _lock = new FastResourceLock();
        private int _waiters;

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
