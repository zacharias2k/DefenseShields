using System;
using System.Collections.Generic;

namespace DefenseSystems.Support
{
    internal class FutureEvents
    {
        internal struct FutureAction
        {
            internal Action<object> Callback;
            internal object Arg1;

            internal FutureAction(Action<object> callBack, object arg1)
            {
                Callback = callBack;
                Arg1 = arg1;
            }
        }

        internal FutureEvents()
        {
            for (int i = 0; i < _maxDelay; i++) _callbacks[i] = new List<FutureAction>();
        }

        private const int _maxDelay = 1800;
        private readonly List<FutureAction>[] _callbacks = new List<FutureAction>[_maxDelay]; // and fill with list instances
        private int _offset = 0;

        internal void Schedule(Action<object> callback, object arg1, uint delay = 1)
        {
            if (delay <= 0) delay = 1;

            lock (_callbacks)
                _callbacks[(_offset + delay) % _maxDelay].Add(new FutureAction(callback, arg1));
        }

        internal void Tick()
        {
            lock (_callbacks)
            {
                foreach (var e in _callbacks[_offset]) e.Callback(e.Arg1);
                _callbacks[_offset].Clear();
                _offset = (_offset + 1) % _maxDelay;
            }
        }
    }
}
