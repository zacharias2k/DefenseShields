using System;
using System.Text;
using DefenseShields.Support;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace DefenseShields.Control
{

    public class ButtonControl<T> : BaseControl<T>
    {
        public ButtonControl(
            IMyTerminalBlock block,
            string internalName,
            string title)
            : base(block, internalName, title)
        {
        }

        public override void OnCreateUi()
        {
            var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>(InternalName);
            button.Title = VRage.Utils.MyStringId.GetOrCompute(Title);
            button.Action = OnAction;
            button.Enabled = Enabled;
            button.Visible = ShowControl;
            MyAPIGateway.TerminalControls.AddControl<T>(button);
        }

        public virtual void OnAction(IMyTerminalBlock block)
        {
        }
    }
}