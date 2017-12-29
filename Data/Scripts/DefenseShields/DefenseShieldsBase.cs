using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DefenseShields.Base
{
    #region Session+protection Class

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class DefenseShieldsBase : MySessionComponentBase
    {
        public static bool IsInit;
        private static List<Station.DefenseShields> _bulletShields = new List<Station.DefenseShields>(); // check 
        public static bool ControlsLoaded;

        // Initialisation

        protected override void UnloadData()
        {
            Logging.WriteLine("Logging stopped.");
            Logging.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            if (IsInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player != null) Init();
        }

        public static void Init()
        {
            Logging.Init("debugdevelop.log");
            Logging.WriteLine($"{DateTime.Now:MM-dd-yy_HH-mm-ss-fff} - Logging Started");
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, CheckDamage);
            IsInit = true;
        }

        // Prevent damage by bullets fired from outside zone.

        public static void CheckDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type == MyDamageType.Deformation) // move below, modify match Type to 
            {
            }

            if (_bulletShields.Count == 0 || info.Type != MyDamageType.Bullet) return;

            var generator = _bulletShields[0];
            var ent = block as IMyEntity;
            switch (block)
            {
                case IMySlimBlock slimBlock:
                    ent = slimBlock.CubeGrid;
                    break;
                case IMyCharacter dude:
                    ent = dude;
                    break;
            }
            if (ent == null) return;
            var isProtected = false;
            foreach (var shield in _bulletShields)
                if (shield.InHash.Contains(ent))
                {
                    isProtected = true;
                    generator = shield;
                }
            if (!isProtected) return;
            if (!MyAPIGateway.Entities.TryGetEntityById(info.AttackerId, out var attacker)) return;
            if (generator.InHash.Contains(attacker)) return;
            info.Amount = 0f;
        }
    }
    #endregion
}
