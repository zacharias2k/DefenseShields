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

                if (Session.ServerEnforcedValues.Debug == 1)
                    Log.Line($"unPackedData is: {unPackedData}\nServerEnforcedValues are: {Session.ServerEnforcedValues}");

                if (!unPackedData.Debug.Equals(-1)) return;

                Session.ServerEnforcedValues.BaseScaler = 30;
                Session.ServerEnforcedValues.Nerf = 0f;
                Session.ServerEnforcedValues.Efficiency = 100f;
                Session.ServerEnforcedValues.StationRatio = 2;
                Session.ServerEnforcedValues.LargeShipRatio = 5;
                Session.ServerEnforcedValues.SmallShipRatio = 1;
                Session.ServerEnforcedValues.DisableVoxelSupport = 0;
                Session.ServerEnforcedValues.DisableGridDamageSupport = 0;
                Session.ServerEnforcedValues.Debug = 1;

                Log.Line($"invalid config file regenerating, [Debug] value was: {unPackedData.Debug}");
                if (!unPackedData.BaseScaler.Equals(-1)) Session.ServerEnforcedValues.BaseScaler = unPackedData.BaseScaler;
                if (!unPackedData.Nerf.Equals(-1f)) Session.ServerEnforcedValues.Nerf = unPackedData.Nerf;
                if (!unPackedData.Efficiency.Equals(-1f)) Session.ServerEnforcedValues.Efficiency = unPackedData.Efficiency;
                if (!unPackedData.StationRatio.Equals(-1)) Session.ServerEnforcedValues.StationRatio = unPackedData.StationRatio;
                if (!unPackedData.LargeShipRatio.Equals(-1)) Session.ServerEnforcedValues.LargeShipRatio = unPackedData.LargeShipRatio;
                if (!unPackedData.SmallShipRatio.Equals(-1)) Session.ServerEnforcedValues.SmallShipRatio = unPackedData.SmallShipRatio;
                if (!unPackedData.DisableVoxelSupport.Equals(-1)) Session.ServerEnforcedValues.DisableVoxelSupport = unPackedData.DisableVoxelSupport;
                if (!unPackedData.DisableGridDamageSupport.Equals(-1)) Session.ServerEnforcedValues.DisableGridDamageSupport = unPackedData.DisableGridDamageSupport;
                if (!unPackedData.Debug.Equals(-1)) Session.ServerEnforcedValues.Debug = unPackedData.Debug;

                unPackedData = null;
                unPackCfg.Close();
                unPackCfg.Dispose();
                MyAPIGateway.Utilities.DeleteFileInGlobalStorage("DefenseShields.cfg");
                var newCfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var newData = MyAPIGateway.Utilities.SerializeToXML(Session.ServerEnforcedValues);
                newCfg.Write(newData);
                newCfg.Flush();
                newCfg.Close();

                if (Session.ServerEnforcedValues.Debug == 1)
                    Log.Line($"wrote modified config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
            else
            {
                Session.ServerEnforcedValues.BaseScaler = 30;
                Session.ServerEnforcedValues.Nerf = 0f;
                Session.ServerEnforcedValues.Efficiency = 100f;
                Session.ServerEnforcedValues.StationRatio = 2;
                Session.ServerEnforcedValues.LargeShipRatio = 5;
                Session.ServerEnforcedValues.SmallShipRatio = 1;
                Session.ServerEnforcedValues.DisableVoxelSupport = 0;
                Session.ServerEnforcedValues.DisableGridDamageSupport = 0;
                Session.ServerEnforcedValues.Debug = 1;

                var cfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var data = MyAPIGateway.Utilities.SerializeToXML(Session.ServerEnforcedValues);
                cfg.Write(data);
                cfg.Flush();
                cfg.Close();

                if (Session.ServerEnforcedValues.Debug == 1)
                    Log.Line($"wrote new config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
        }

        public static void ReadConfigFile()
        {
            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg");

            if (Session.ServerEnforcedValues.Debug == 1) Log.Line($"Reading config, file exists? {dsCfgExists}");

            if (!dsCfgExists) return;

            var cfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShields.cfg");
            var data = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(cfg.ReadToEnd());
            Session.ServerEnforcedValues = data;

            if (Session.ServerEnforcedValues.Debug == 1) Log.Line($"Writing settings to mod:\n{data}");
        }
    }
}
