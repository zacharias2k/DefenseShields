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

                if (Session.Enforced.Debug == 1)
                    Log.Line($"unPackedData is: {unPackedData}\nServEnforced are: {Session.Enforced}");

                if (!unPackedData.Debug.Equals(-1)) return;

                Session.Enforced.BaseScaler = 30;
                Session.Enforced.Nerf = 0f;
                Session.Enforced.Efficiency = 100f;
                Session.Enforced.StationRatio = 2;
                Session.Enforced.LargeShipRatio = 3;
                Session.Enforced.SmallShipRatio = 1;
                Session.Enforced.DisableVoxelSupport = 0;
                Session.Enforced.DisableGridDamageSupport = 0;
                Session.Enforced.Debug = 0;

                Log.Line($"invalid config file regenerating, [Debug] value was: {unPackedData.Debug}");
                if (!unPackedData.BaseScaler.Equals(-1)) Session.Enforced.BaseScaler = unPackedData.BaseScaler;
                if (!unPackedData.Nerf.Equals(-1f)) Session.Enforced.Nerf = unPackedData.Nerf;
                if (!unPackedData.Efficiency.Equals(-1f)) Session.Enforced.Efficiency = unPackedData.Efficiency;
                if (!unPackedData.StationRatio.Equals(-1)) Session.Enforced.StationRatio = unPackedData.StationRatio;
                if (!unPackedData.LargeShipRatio.Equals(-1)) Session.Enforced.LargeShipRatio = unPackedData.LargeShipRatio;
                if (!unPackedData.LargeShipRatio.Equals(5)) Session.Enforced.LargeShipRatio = 3; // temporary remove.
                if (!unPackedData.SmallShipRatio.Equals(-1)) Session.Enforced.SmallShipRatio = unPackedData.SmallShipRatio;
                if (!unPackedData.DisableVoxelSupport.Equals(-1)) Session.Enforced.DisableVoxelSupport = unPackedData.DisableVoxelSupport;
                if (!unPackedData.DisableGridDamageSupport.Equals(-1)) Session.Enforced.DisableGridDamageSupport = unPackedData.DisableGridDamageSupport;
                if (!unPackedData.Debug.Equals(-1)) Session.Enforced.Debug = unPackedData.Debug;

                unPackedData = null;
                unPackCfg.Close();
                unPackCfg.Dispose();
                MyAPIGateway.Utilities.DeleteFileInGlobalStorage("DefenseShields.cfg");
                var newCfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var newData = MyAPIGateway.Utilities.SerializeToXML(Session.Enforced);
                newCfg.Write(newData);
                newCfg.Flush();
                newCfg.Close();

                if (Session.Enforced.Debug == 1)
                    Log.Line($"wrote modified config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
            else
            {
                Session.Enforced.BaseScaler = 30;
                Session.Enforced.Nerf = 0f;
                Session.Enforced.Efficiency = 100f;
                Session.Enforced.StationRatio = 2;
                Session.Enforced.LargeShipRatio = 3;
                Session.Enforced.SmallShipRatio = 1;
                Session.Enforced.DisableVoxelSupport = 0;
                Session.Enforced.DisableGridDamageSupport = 0;
                Session.Enforced.Debug = 0;

                var cfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var data = MyAPIGateway.Utilities.SerializeToXML(Session.Enforced);
                cfg.Write(data);
                cfg.Flush();
                cfg.Close();

                if (Session.Enforced.Debug == 1)
                    Log.Line($"wrote new config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
        }

        public static void ReadConfigFile()
        {
            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg");

            if (Session.Enforced.Debug == 1) Log.Line($"Reading config, file exists? {dsCfgExists}");

            if (!dsCfgExists) return;

            var cfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShields.cfg");
            var data = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(cfg.ReadToEnd());
            Session.Enforced = data;

            if (Session.Enforced.Debug == 1) Log.Line($"Writing settings to mod:\n{data}");
        }
    }
}
