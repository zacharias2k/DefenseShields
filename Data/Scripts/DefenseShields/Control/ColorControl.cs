using System;
using System.Reflection;
using System.Text;
using DefenseShields.Support;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRageMath;

namespace DefenseShields.Control
{

    public class ColorControl<T> : BaseControl<T>
    {
        public Color DefaultValue;

        public ColorControl(
            IMyTerminalBlock block,
            string internalName,
            string title,
            Color defaultValue = default(Color))
            : base(block, internalName, title)
        {
            DefaultValue = defaultValue;
            Color temp;
            if (!MyAPIGateway.Utilities.GetVariable<Color>(block.EntityId.ToString() + InternalName, out temp))
            {
                MyAPIGateway.Utilities.SetVariable<Color>(block.EntityId.ToString() + InternalName, defaultValue);
            }
            CreateUi();
        }

        public override void OnCreateUi()
        {
            var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>(InternalName);
            button.Enabled = Enabled;
            button.Getter = Getter;
            button.Setter = Setter;
            button.Visible = ShowControl;
            button.Title = VRage.Utils.MyStringId.GetOrCompute(Title);
            MyAPIGateway.TerminalControls.AddControl<T>(button);
        }

        public virtual void OnAction(IMyTerminalBlock block)
        {
        }

        public virtual Color Getter(IMyTerminalBlock block)
        {

            Color value = DefaultValue;
            MyAPIGateway.Utilities.GetVariable<Color>(block.EntityId.ToString() + InternalName, out value);
            return value;
        }

        public virtual void Setter(IMyTerminalBlock block, Color color)
        {
            try
            {
                MyAPIGateway.Utilities.SetVariable<Color>(block.EntityId.ToString() + InternalName, color);
            }
            catch (Exception ex) { Log.Line($"Exception in Checkbox Setter: {ex}"); }
        }
    }
}