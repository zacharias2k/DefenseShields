﻿using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public class ShieldGridComponent : MyEntityComponentBase
    {
        private static List<ShieldGridComponent> gridShield = new List<ShieldGridComponent>();
        public DefenseShields DefenseShields;
        public DefenseShieldsSettings Settings;

        public ShieldGridComponent(DefenseShields defenseShields, DefenseShieldsSettings settings)
        {
            DefenseShields = defenseShields;
            Settings = settings;
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            if (Container.Entity.InScene)
            {
                gridShield.Add(this);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
                gridShield.Remove(this);
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            gridShield.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            gridShield.Remove(this);

            base.OnRemovedFromScene();
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public HashSet<IMyCubeGrid> GetSubGrids { get; set; } = new HashSet<IMyCubeGrid>();

        public Vector3D[] PhysicsOutside { get; set; } = new Vector3D[642];

        public Vector3D[] PhysicsOutsideLow { get; set; } = new Vector3D[162];

        public Vector3D[] PhysicsInside { get; set; } = new Vector3D[642];

        public int EmitterMode { get; set; } = -1;
        public Emitters EmitterPrime { get; set; }
        public Emitters EmitterBeta { get; set; }

        public O2Generators ActiveO2Generator { get; set; }

        public string ModulationPassword { get; set; }

        public bool EmittersWorking { get; set; } = true;

        public bool EmittersSuspended { get; set; }

        public bool O2Updated { get; set; }

        public float DefaultO2 { get; set; }

        public bool ShieldActive
        {
            get { return Settings.Settings.ShieldActive; }
            set { Settings.Settings.ShieldActive = value; }
        }

        public bool RaiseShield
        {
            get { return Settings.Settings.RaiseShield; }
            set { Settings.Settings.RaiseShield = value; }
        }

        public bool CheckEmitters { get; set; }

        public bool GridIsMoving { get; set; }

        public bool ComingOnline { get; set; }

        public bool Starting { get; set; }

        public bool Warming { get; set; }

        public bool EmitterEvent { get; set; }

        public float ShieldPercent { get; set; }

        public double ShieldVolume { get; set; }

        public double IncreaseO2ByFPercent
        {
            get { return Settings.Settings.IncreaseO2ByFPercent; }
            set { Settings.Settings.IncreaseO2ByFPercent = value; }
        }

        public double BoundingRange { get; set; }

        public double ShieldVelocitySqr { get; set; }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}
