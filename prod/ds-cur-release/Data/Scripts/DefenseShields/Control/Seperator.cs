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
            string internalName)
            : base(block, internalName, "")
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
