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
using DefenseShields.Support;

namespace DamageMod
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeDamageEnhancer", "SmallDamageEnhancer")]
    public class Enhancers : MyGameLogicComponent
    {
        private uint _tick;
        internal int RotationTime;
        internal bool init;
        public IMyUpgradeModule DamageMod => (IMyUpgradeModule)Entity;
        private MyEntitySubpart _subpartRotor;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                // Session.Instance.Components.Add(this);
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (MyAPIGateway.Utilities.IsDedicated) return;
                if (!init)
                {
                    if (_subpartRotor == null)
                    {
                        Entity?.TryGetSubpart("Rotor", out _subpartRotor);
                        Log.Line($"subpart is null, what is my name?  TELL ME MY NAME!");
                        return;
                    }
                    init = true;
                }
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                BlockMoveAnimation();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void BlockMoveAnimationReset()
        {
            _subpartRotor.Subparts.Clear();
            Entity.TryGetSubpart("Rotor", out _subpartRotor);
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
