namespace DefenseShields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::DefenseShields.Support;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Interfaces.Terminal;
    using VRage.Game.Components;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false, "DSControlLCD")]
    public class Displays : MyGameLogicComponent
    {
        private int _count = -1;
        private ShieldGridComponent _shieldComp;

        private IMyTextPanel Display => (IMyTextPanel)Entity;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                if (!MyAPIGateway.Utilities.IsDedicated) NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                if (!MyAPIGateway.Utilities.IsDedicated) NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
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
                Display.ShowPublicTextOnScreen();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation10()
        {
            if (_count++ == 9) _count = 0;
            if (_count != 9) return;

            if (_shieldComp?.DefenseShields?.MyGrid != Display.CubeGrid)
            {
                Display.CubeGrid.Components.TryGet(out _shieldComp);
            }
            if (_shieldComp?.DefenseShields?.Shield == null || !_shieldComp.DefenseShields.Warming || !_shieldComp.DefenseShields.IsWorking)
            {
                if (Display.ShowText) Display.SetShowOnScreen(0);
                return;
            }
            _shieldComp.DefenseShields.Shield.RefreshCustomInfo();
            Display.WritePublicText(_shieldComp.DefenseShields.Shield.CustomInfo);
            if (!Display.ShowText) Display.ShowPublicTextOnScreen();
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

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }

        public override void Close()
        {
            try
            {
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

        public override void OnAddedToContainer()
        {
            if (Entity.InScene) OnAddedToScene();
        }

        private static bool HideControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeId != "DSControlLCD";
        }

        public static void RemoveControls()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyTextPanel>(out actions);

            var increaseFontSize = actions.First((x) => x.Id.ToString() == "IncreaseFontSize");
            increaseFontSize.Enabled = HideControls;
            var decreaseFontSize = actions.First((x) => x.Id.ToString() == "DecreaseFontSize");
            decreaseFontSize.Enabled = HideControls;
            var increaseChangeIntervalSlider = actions.First((x) => x.Id.ToString() == "IncreaseChangeIntervalSlider");
            increaseChangeIntervalSlider.Enabled = HideControls;
            var decreaseChangeIntervalSlider = actions.First((x) => x.Id.ToString() == "DecreaseChangeIntervalSlider");
            decreaseChangeIntervalSlider.Enabled = HideControls;

            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyTextPanel>(out controls);

            var customData = controls.First((x) => x.Id.ToString() == "CustomData");
            customData.Visible = HideControls;
            var title = controls.First((x) => x.Id.ToString() == "Title");
            title.Visible = HideControls;
            var showTextPanel = controls.First((x) => x.Id.ToString() == "ShowTextPanel");
            showTextPanel.Visible = HideControls;
            var showTextOnScreen = controls.First((x) => x.Id.ToString() == "ShowTextOnScreen");
            showTextOnScreen.Visible = HideControls;
            var fontSize = controls.First((x) => x.Id.ToString() == "FontSize");
            fontSize.Visible = HideControls;
            var backgroundColor = controls.First((x) => x.Id.ToString() == "BackgroundColor");
            backgroundColor.Visible = HideControls;
            var imageList = controls.First((x) => x.Id.ToString() == "ImageList");
            imageList.Visible = HideControls;
            var selectTextures = controls.First((x) => x.Id.ToString() == "SelectTextures");
            selectTextures.Visible = HideControls;
            var changeIntervalSlider = controls.First((x) => x.Id.ToString() == "ChangeIntervalSlider");
            changeIntervalSlider.Visible = HideControls;
            var selectedImageList = controls.First((x) => x.Id.ToString() == "SelectedImageList");
            selectedImageList.Visible = HideControls;
            var removeSelectedTextures = controls.First((x) => x.Id.ToString() == "RemoveSelectedTextures");
            removeSelectedTextures.Visible = HideControls;
        }
    }
}
