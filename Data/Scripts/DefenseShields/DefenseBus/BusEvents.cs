using System;
using DefenseSystems.Support;
using VRage.Game.Entity;

namespace DefenseSystems
{
    public class BusEvents
    {
        public event Action<MyEntity, Bus.LogicState> OnBusSplit;

        public void Split(MyEntity type, Bus.LogicState state)
        {
            Log.Line("[Bus Has Split--]");
            OnBusSplit?.Invoke(type, state);
        }
    }
}
