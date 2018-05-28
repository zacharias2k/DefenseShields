using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    internal static class DsUtilsStatic
    {
        public static void GetRealPlayers(Vector3D center, float radius, List<long> realPlayers)
        {
            List<IMyIdentity> realPlayersIdentities = new List<IMyIdentity>();
            MyAPIGateway.Players.GetAllIdentites(realPlayersIdentities, p => !string.IsNullOrEmpty(p?.DisplayName));
            var pruneSphere = new BoundingSphereD(center, radius);
            var pruneList = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList);

            foreach (var ent in pruneList)
            {
                if (ent == null || !(ent is IMyCubeGrid || ent is IMyCharacter)) continue;

                IMyPlayer player = null;

                if (ent is IMyCharacter)
                {
                    player = MyAPIGateway.Players.GetPlayerControllingEntity(ent);
                    if (player == null) continue;
                }
                else
                {
                    var playerTmp = MyAPIGateway.Players.GetPlayerControllingEntity(ent);

                    if (playerTmp?.Character != null) player = playerTmp;
                }

                if (player == null) continue;
                if (realPlayersIdentities.Contains(player.Identity)) realPlayers.Add(player.IdentityId);
            }
        }

        public static void PrepConfigFile()
        {
            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg");
            if (dsCfgExists)
            {
                var unPackCfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShields.cfg");
                var unPackedData = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(unPackCfg.ReadToEnd());
                if (!unPackedData.Debug.Equals(-1)) return;

                DefenseShields.ServerEnforcedValues.BaseScaler = 30;
                DefenseShields.ServerEnforcedValues.Nerf = 0f;
                DefenseShields.ServerEnforcedValues.Efficiency = 100f;
                DefenseShields.ServerEnforcedValues.StationRatio = 2;
                DefenseShields.ServerEnforcedValues.LargeShipRatio = 5;
                DefenseShields.ServerEnforcedValues.SmallShipRatio = 1;
                DefenseShields.ServerEnforcedValues.DisableVoxelSupport = 0;
                DefenseShields.ServerEnforcedValues.DisableGridDamageSupport = 0;
                DefenseShields.ServerEnforcedValues.Debug = 1;

                Log.Line($"invalid config file regenerating, [Debug] value was: {unPackedData.Debug}");
                if (!unPackedData.BaseScaler.Equals(-1)) DefenseShields.ServerEnforcedValues.BaseScaler = unPackedData.BaseScaler;
                if (!unPackedData.Nerf.Equals(-1f)) DefenseShields.ServerEnforcedValues.Nerf = unPackedData.Nerf;
                if (!unPackedData.Efficiency.Equals(-1f)) DefenseShields.ServerEnforcedValues.Efficiency = unPackedData.Efficiency;
                if (!unPackedData.StationRatio.Equals(-1)) DefenseShields.ServerEnforcedValues.StationRatio = unPackedData.StationRatio;
                if (!unPackedData.LargeShipRatio.Equals(-1)) DefenseShields.ServerEnforcedValues.LargeShipRatio = unPackedData.LargeShipRatio;
                if (!unPackedData.SmallShipRatio.Equals(-1)) DefenseShields.ServerEnforcedValues.SmallShipRatio = unPackedData.SmallShipRatio;
                if (!unPackedData.DisableVoxelSupport.Equals(-1)) DefenseShields.ServerEnforcedValues.DisableVoxelSupport = unPackedData.DisableVoxelSupport;
                if (!unPackedData.DisableGridDamageSupport.Equals(-1)) DefenseShields.ServerEnforcedValues.DisableGridDamageSupport = unPackedData.DisableGridDamageSupport;
                if (!unPackedData.Debug.Equals(-1)) DefenseShields.ServerEnforcedValues.Debug = unPackedData.Debug;

                unPackedData = null;
                unPackCfg.Close();
                unPackCfg.Dispose();
                MyAPIGateway.Utilities.DeleteFileInGlobalStorage("DefenseShields.cfg");
                var newCfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var newData = MyAPIGateway.Utilities.SerializeToXML(DefenseShields.ServerEnforcedValues);
                newCfg.Write(newData);
                newCfg.Flush();
                newCfg.Close();
            }
            else
            {
                DefenseShields.ServerEnforcedValues.BaseScaler = 30;
                DefenseShields.ServerEnforcedValues.Nerf = 0f;
                DefenseShields.ServerEnforcedValues.Efficiency = 100f;
                DefenseShields.ServerEnforcedValues.StationRatio = 2;
                DefenseShields.ServerEnforcedValues.LargeShipRatio = 5;
                DefenseShields.ServerEnforcedValues.SmallShipRatio = 1;
                DefenseShields.ServerEnforcedValues.DisableVoxelSupport = 0;
                DefenseShields.ServerEnforcedValues.DisableGridDamageSupport = 0;
                DefenseShields.ServerEnforcedValues.Debug = 1;

                var cfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var data = MyAPIGateway.Utilities.SerializeToXML(DefenseShields.ServerEnforcedValues);
                cfg.Write(data);
                cfg.Flush();
                cfg.Close();
            }
        }

        public static void ReadConfigFile()
        {
            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg");
            if (!dsCfgExists) return;

            var cfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShields.cfg");
            var data = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(cfg.ReadToEnd());
            DefenseShields.ServerEnforcedValues = data;
        }
    }
}
