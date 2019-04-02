using System;
using DefenseSystems.Support;
using VRage.Game.Entity;

namespace DefenseSystems
{
    public class BusEvents
    {
        public event Action<MyEntity, DefenseBus.LogicState> OnCheckBus;
        public event Action<MyEntity, MyEntity> OnBusSplit;

        public void Split(MyEntity e1, MyEntity e2)
        {
            Log.Line("splitInvoke");
            OnBusSplit?.Invoke(e1, e2);
        }

        public void Check(MyEntity e1, DefenseBus.LogicState e2)
        {
            Log.Line("checkInvoke");
            OnCheckBus?.Invoke(e1, e2);
        }
    }
}
