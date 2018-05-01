using System;
using System.Text;
using DefenseShields.Support;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;


namespace DefenseShields.Control
{
    class Seperator<T> : BaseControl<T>
    {
        public Seperator(
            IMyTerminalBlock block,
            string internalName,
            string toolTip)
            : base(block, internalName, "", toolTip)
        {
            CreateUi();
        }

        public override void OnCreateUi()
        {
            var seperator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(InternalName);
            seperator.Visible = ShowControl;
        }

    }
}
