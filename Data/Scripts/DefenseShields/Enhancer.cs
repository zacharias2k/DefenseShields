using System;
using System.Collections.Generic;
using DefenseShields.Control;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using Sandbox.Game;
using SpaceEngineers.Game;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeDamageEnhancer", "SmallDamageEnhancer")]
public class Enhancers : MyGameLogicComponent
{
    internal int RotationTime;
    public IMyUpgradeModule DamageMod => (IMyUpgradeModule)Entity;
    private MyEntitySubpart _subpartRotor;

    private void Damage_IsWorkingChanged(IMyCubeBlock obj) => NeedsUpdate = DamageMod.IsWorking ? MyEntityUpdateEnum.EACH_FRAME : MyEntityUpdateEnum.NONE;
    private uint _tick;

    public override void UpdateBeforeSimulation()

    {
        _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
        if (MyAPIGateway.Utilities.IsDedicated) return;
        BlockMoveAnimation();
    }

    private void BlockMoveAnimationReset()
    {
        if (Session.Enforced.Debug == 1) Log.Line($"Resetting BlockMovement - Tick:{_tick.ToString()}");
        _subpartRotor.Subparts.Clear();
        Entity.TryGetSubpart("DmgRotor", out _subpartRotor);
    }

    private void BlockMoveAnimation()
    {
        if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset();
        RotationTime -= 1;
        var rotationMatrix = MatrixD.CreateRotationY(0.05f * RotationTime);
        _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
    }
}
}
