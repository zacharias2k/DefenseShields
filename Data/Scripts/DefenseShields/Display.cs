using System;
using System.Collections.Generic;
using System.Linq;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false, "DSControlLCD")]
    public class Displays : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _lCount;
        public bool ServerUpdate;
        private readonly Dictionary<long, Displays> _displays = new Dictionary<long, Displays>();
        internal ShieldGridComponent ShieldComp;
        private IMyTextPanel Display => (IMyTextPanel)Entity;
        internal DSUtils Dsutil1 = new DSUtils();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                RemoveControls();
                Session.Instance.Displays.Add(this);
                _displays.Add(Entity.EntityId, this);
                Display.ShowPublicTextOnScreen();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            _tick = Session.Instance.Tick;
            Timing();
            if (_count == 29)
            {
                Display.CubeGrid.Components.TryGet(out ShieldComp);
                if (ShieldComp?.DefenseShields?.Shield == null || !ShieldComp.DefenseShields.Warming || !ShieldComp.DefenseShields.Shield.IsWorking)
                {
                    if (Display.ShowText) Display.SetShowOnScreen(0);
                    return;
                }
                Display.WritePublicText(ShieldComp.DefenseShields.Shield.CustomInfo);
                if (!Display.ShowText) Display.ShowPublicTextOnScreen();
            }
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                Session.Instance.Displays.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                if (_displays.ContainsKey(Entity.EntityId)) _displays.Remove(Entity.EntityId);
                if (Session.Instance.Displays.Contains(this)) Session.Instance.Displays.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }
        public override void OnAddedToContainer() { if (Entity.InScene) OnAddedToScene(); }

        private static bool HideControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeId != "DSControlLCD";
        }

        public static void RemoveControls()
        {
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyTextPanel>(out actions);

            var IncreaseFontSize = actions.First((x) => x.Id.ToString() == "IncreaseFontSize");
            IncreaseFontSize.Enabled = HideControls;
            var DecreaseFontSize = actions.First((x) => x.Id.ToString() == "DecreaseFontSize");
            DecreaseFontSize.Enabled = HideControls;
            var IncreaseChangeIntervalSlider = actions.First((x) => x.Id.ToString() == "IncreaseChangeIntervalSlider");
            IncreaseChangeIntervalSlider.Enabled = HideControls;
            var DecreaseChangeIntervalSlider = actions.First((x) => x.Id.ToString() == "DecreaseChangeIntervalSlider");
            DecreaseChangeIntervalSlider.Enabled = HideControls;

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyTextPanel>(out controls);

            var CustomData = controls.First((x) => x.Id.ToString() == "CustomData");
            CustomData.Visible = HideControls;
            var Title = controls.First((x) => x.Id.ToString() == "Title");
            Title.Visible = HideControls;
            var ShowTextPanel = controls.First((x) => x.Id.ToString() == "ShowTextPanel");
            ShowTextPanel.Visible = HideControls;
            var ShowTextOnScreen = controls.First((x) => x.Id.ToString() == "ShowTextOnScreen");
            ShowTextOnScreen.Visible = HideControls;
            var FontSize = controls.First((x) => x.Id.ToString() == "FontSize");
            FontSize.Visible = HideControls;
            var BackgroundColor = controls.First((x) => x.Id.ToString() == "BackgroundColor");
            BackgroundColor.Visible = HideControls;
            var ImageList = controls.First((x) => x.Id.ToString() == "ImageList");
            ImageList.Visible = HideControls;
            var SelectTextures = controls.First((x) => x.Id.ToString() == "SelectTextures");
            SelectTextures.Visible = HideControls;
            var ChangeIntervalSlider = controls.First((x) => x.Id.ToString() == "ChangeIntervalSlider");
            ChangeIntervalSlider.Visible = HideControls;
            var SelectedImageList = controls.First((x) => x.Id.ToString() == "SelectedImageList");
            SelectedImageList.Visible = HideControls;
            var RemoveSelectedTextures = controls.First((x) => x.Id.ToString() == "RemoveSelectedTextures");
            RemoveSelectedTextures.Visible = HideControls;
        }
    }
}
