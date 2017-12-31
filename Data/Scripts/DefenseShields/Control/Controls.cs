using System;
using System.Text;
using Sandbox.ModAPI;

namespace DefenseShields
{
    #region Controls Class
    public class RefreshCheckbox<T> : Control.Checkbox<T>
    {
        public RefreshCheckbox(IMyTerminalBlock block,
            string internalName,
            string title,
            bool defaultValue = true) : base(block, internalName, title, defaultValue)
        {
        }
        public override void Setter(IMyTerminalBlock block, bool newState)
        {
            base.Setter(block, newState);
            var shield = block.GameLogic.GetAs<Station.DefenseShields>();
            if (shield == null) { return; }
            shield.Sink.Update();
            block.RefreshCustomInfo();
        }
    }

    public class RangeSlider<T> : Control.Slider<T>
    {

        public RangeSlider(
            IMyTerminalBlock block,
            string internalName,
            string title,
            float min = 50.0f,
            float max = 300.0f,
            float standard = 10.0f)
            : base(block, internalName, title, min, max, standard)
        {
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            try
            {
                builder.Clear();
                var distanceString = Getter(block).ToString("0") + "m";
                builder.Append(distanceString);
                block.RefreshCustomInfo();
            }
            catch (Exception ex)
            {
            }
        }

        public void SetterOutside(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            var shield = block.GameLogic.GetAs<Station.DefenseShields>();
            if (shield == null) { return; }
            shield.Sink.Update();
        }

        public override void Setter(IMyTerminalBlock block, float value)
        {
            base.Setter(block, value);
            //var message = new shieldNetwork.MessageSync() { Value = value, EntityId = block.EntityId };
            //shieldNetwork.MessageUtils.SendMessageToAll(message);
            var shield = block.GameLogic.GetAs<Station.DefenseShields>();
            if (shield == null) { return; }
            shield.Sink.Update();
        }
    }
    #endregion  
}
