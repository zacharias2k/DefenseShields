using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    internal static class UtilsStatic
    {
        internal static Color White1 = new Color(255, 255, 255);
        internal static Color White2 = new Color(90, 118, 255);
        internal static Color White3 = new Color(47, 86, 255);
        internal static Color Blue1 = Color.Aquamarine;
        internal static Color Blue2 = new Color(0, 66, 255);
        internal static Color Blue3 = new Color(0, 7, 255, 255);
        internal static Color Blue4 = new Color(22, 0, 170);
        internal static Color Red1 = new Color(87, 0, 66);
        internal static Color Red2 = new Color(121, 0, 13);
        internal static Color Red3 = new Color(255, 0, 0);

        private static readonly Dictionary<float, float> DmgTable = new Dictionary<float, float>
        {
            [0.0000000001f] = 0.1f,
            [0.0000000002f] = 0.2f,
            [0.0000000003f] = 0.3f,
            [0.0000000004f] = 0.4f,
            [0.0000000005f] = 0.5f,
            [0.0000000006f] = 0.6f,
            [0.0000000007f] = 0.7f,
            [0.0000000008f] = 0.8f,
            [0.0000000009f] = 0.9f,
            [0.0000000010f] = 1,
            [0.0000000020f] = 2,
            [0.0000000030f] = 3,
            [0.0000000040f] = 4,
            [0.0000000050f] = 5,
            [0.0000000060f] = 6,
            [0.0000000070f] = 7,
            [0.0000000080f] = 8,
            [0.0000000090f] = 9,
            [0.0000000010f] = 10,
        };

        public static float GetDmgMulti(float damage)
        {
            float tableVal;
            DmgTable.TryGetValue(damage, out tableVal);
            return tableVal;
        }

        public static void GetRealPlayers(Vector3D center, float radius, List<long> realPlayers)
        {
            var realPlayersIdentities = new List<IMyIdentity>();
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

        public static Color GetShieldColorFromFloat(float percent)
        {
            if (percent > 90) return White1;
            if (percent > 80) return White2;
            if (percent > 70) return White3;
            if (percent > 60) return Blue1;
            if (percent > 50) return Blue2;
            if (percent > 40) return Blue3;
            if (percent > 30) return Blue4;
            if (percent > 20) return Red1;
            if (percent > 10) return Red2;
            return Red3;
        }

        public static Color GetAirEmissiveColorFromDouble(double percent)
        {
            if (percent >= 80) return Color.Green;
            if (percent > 10) return Color.Yellow;
            return Color.Red;
        }

        public static long ThereCanBeOnlyOne(IMyCubeBlock shield)
        {
            if (Session.Enforced.Debug == 1) Log.Line($"ThereCanBeOnlyOne start");
            var shieldBlocks = new List<MyCubeBlock>();
            foreach (var block in ((MyCubeGrid)shield.CubeGrid).GetFatBlocks())
            {
                if (block == null) continue;

                if (block.BlockDefinition.BlockPairName == "DS_Control" || block.BlockDefinition.BlockPairName == "DS_Control_Table")
                {
                    if (block.IsWorking) return block.EntityId;
                    shieldBlocks.Add(block);
                }
            }
            var shieldDistFromCenter = double.MinValue;
            var shieldId = long.MinValue;
            foreach (var s in shieldBlocks)
            {
                if (s == null) continue;

                var dist = Vector3D.DistanceSquared(s.PositionComp.WorldVolume.Center, shield.CubeGrid.WorldVolume.Center);
                if (dist > shieldDistFromCenter)
                {
                    shieldDistFromCenter = dist;
                    shieldId = s.EntityId;
                }
            }
            if (Session.Enforced.Debug == 1) Log.Line($"ThereCanBeOnlyOne complete, found shield: {shieldId}");
            return shieldId;
        }

        /*
        public static bool CheckShieldType(IMyFunctionalBlock shield, bool warning, bool takeAction = false)
        {
            var realPlayerIds = new List<long>();
            GetRealPlayers(shield.PositionComp.WorldVolume.Center, 500f, realPlayerIds);
            foreach (var id in realPlayerIds)
            {
                if (!warning && shield.BlockDefinition.SubtypeId == "DefenseShieldsST" && !shield.CubeGrid.Physics.IsStatic)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Station shields only allowed on stations", 5000, "Red", id);
                    warning = true;
                }
                else if (!warning && shield.BlockDefinition.SubtypeId == "DefenseShieldsLS" && shield.CubeGrid.Physics.IsStatic)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Large Ship Shields only allowed on ships, not stations", 5000, "Red", id);
                    warning = true;
                }
                else if (!warning && takeAction)
                {
                }
                else if (!warning)
                {
                    MyVisualScriptLogicProvider.ShowNotification("Control station in standby while active unit is functional", 5000, "Red", id);
                    warning = true;
                }
            }

            if (takeAction && warning)
            {
                warning = false;
                shield.Enabled = false;
            }
            return warning;
        }
        */

        public static bool DistanceCheck(IMyCubeBlock shield, int x, double range)
        {
            if (MyAPIGateway.Session.Player.Character == null) return false;

            var pPosition = MyAPIGateway.Session.Player.Character.GetPosition();
            var cPosition = shield.CubeGrid.PositionComp.GetPosition();
            var dist = Vector3D.DistanceSquared(cPosition, pPosition) <= (x + range) * (x + range);
            return dist;
        }

        public static void GetDefinitons()
        {
            try
            {
                var defintions = MyDefinitionManager.Static.GetAllDefinitions();
                foreach (var def in defintions)
                {
                    if (!(def is MyAmmoMagazineDefinition)) continue;
                    var ammoDef = def as MyAmmoMagazineDefinition;
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(ammoDef.AmmoDefinitionId);
                    if (!(ammo is MyMissileAmmoDefinition)) continue;
                    var shot = ammo as MyMissileAmmoDefinition;
                    if (Session.AmmoCollection.ContainsKey(shot.MissileModelName)) continue;
                    Session.AmmoCollection.Add(shot.MissileModelName, new AmmoInfo(shot.IsExplosive, shot.MissileExplosionDamage, shot.MissileExplosionRadius, shot.DesiredSpeed, shot.MissileMass, shot.BackkickForce));
                }
                if (Session.Enforced.Debug == 1) Log.Line($"Definitions Loaded");
            }
            catch (Exception ex) { Log.Line($"Exception in GetAmmoDefinitions: {ex}"); }
        }

        public static double CreateNormalFit(IMyCubeBlock shield, Vector3D gridHalfExtents)
        {
            var blockPoints = new Vector3D[8];
            var blocks = new List<IMySlimBlock>();

            var subGrids = MyAPIGateway.GridGroups.GetGroup(shield.CubeGrid, GridLinkTypeEnum.Mechanical);
            foreach (var grid in subGrids) grid.GetBlocks(blocks);

            var sqrt2 = Math.Sqrt(2);
            var sqrt3 = Math.Sqrt(3);
            const double percent = 0.1;
            var last = 0;
            var repeat = 0;
            for (int i = 0; i <= 10; i++)
            {
                var ellipsoidAdjust = MathHelper.Lerp(sqrt2, sqrt3, i * percent);

                var shieldSize = gridHalfExtents * ellipsoidAdjust;
                var mobileMatrix = MatrixD.CreateScale(shieldSize);
                mobileMatrix.Translation = shield.CubeGrid.PositionComp.LocalVolume.Center;
                var matrixInv = MatrixD.Invert(mobileMatrix * shield.CubeGrid.WorldMatrix);

                var c = 0;
                foreach (var block in blocks)
                {
                    BoundingBoxD blockBox;
                    block.GetWorldBoundingBox(out blockBox);

                    blockBox.GetCorners(blockPoints);

                    foreach (var point in blockPoints) if (!CustomCollision.PointInShield(point, matrixInv)) c++;
                }

                if (c == last) repeat++;
                else repeat = 0;

                if (c == 0)
                {
                    return MathHelper.Lerp(sqrt2, sqrt3, i * percent);
                }
                last = c;
                if (i == 10 && repeat > 2) return MathHelper.Lerp(sqrt2, sqrt3, ((10 - repeat) + 1) * percent);
            }
            return sqrt3;
        }

        public static double CreateExtendedFit(IMyCubeBlock shield, Vector3D gridHalfExtents)
        {
            var blockPoints = new Vector3D[8];
            var blocks = new List<IMySlimBlock>();

            var subGrids = MyAPIGateway.GridGroups.GetGroup(shield.CubeGrid, GridLinkTypeEnum.Mechanical);
            foreach (var grid in subGrids) grid.GetBlocks(blocks);

            var sqrt3 = Math.Sqrt(3);
            var sqrt5 = Math.Sqrt(5);
            const double percent = 0.1;
            var last = 0;
            var repeat = 0;
            for (int i = 0; i <= 10; i++)
            {
                var ellipsoidAdjust = MathHelper.Lerp(sqrt3, sqrt5, i * percent);

                var shieldSize = gridHalfExtents * ellipsoidAdjust;
                var mobileMatrix = MatrixD.CreateScale(shieldSize);
                mobileMatrix.Translation = shield.CubeGrid.PositionComp.LocalVolume.Center;
                var matrixInv = MatrixD.Invert(mobileMatrix * shield.CubeGrid.WorldMatrix);

                var c = 0;
                foreach (var block in blocks)
                {
                    BoundingBoxD blockBox;
                    block.GetWorldBoundingBox(out blockBox);

                    blockBox.GetCorners(blockPoints);

                    foreach (var point in blockPoints) if (!CustomCollision.PointInShield(point, matrixInv)) c++;
                }

                if (c == last) repeat++;
                else repeat = 0;

                if (c == 0)
                {
                    return MathHelper.Lerp(sqrt3, sqrt5, i * percent);
                }
                last = c;
                if (i == 10 && repeat > 2) return MathHelper.Lerp(sqrt3, sqrt5, ((10 - repeat) + 1) * percent);
            }
            return sqrt5;
        }

        public static int BlockCount(IMyCubeBlock shield)
        {
            var subGrids = MyAPIGateway.GridGroups.GetGroup(shield.CubeGrid, GridLinkTypeEnum.Mechanical);
            var blockCnt = 0;
            foreach (var grid in subGrids)
            {
                blockCnt += ((MyCubeGrid) grid).BlocksCount;
            }
            return blockCnt;
        }	
        public static void PrepConfigFile()
        {
            const int baseScaler = 30;
            const float nerf = 0f;
            const float efficiency = 100f;
            const int stationRatio = 1;
            const int largeShipRate = 2;
            const int smallShipRatio = 1;
            const int disableVoxel = 0;
            const int disableGridDmg = 0;
            const int debug = 0;
            const bool altRecharge = false;
            const int version = 57;

            var dsCfgExists = MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg");
            if (dsCfgExists)
            {
                var unPackCfg = MyAPIGateway.Utilities.ReadFileInGlobalStorage("DefenseShields.cfg");
                var unPackedData = MyAPIGateway.Utilities.SerializeFromXML<DefenseShieldsEnforcement>(unPackCfg.ReadToEnd());

                if (Session.Enforced.Debug == 1) Log.Line($"unPackedData is: {unPackedData}\nServEnforced are: {Session.Enforced}");

                if (unPackedData.Version == version) return;
                Log.Line($"outdated config file regenerating, file version: {unPackedData.Version} - current version: {version}");
                Session.Enforced.BaseScaler = !unPackedData.BaseScaler.Equals(-1) ? unPackedData.BaseScaler : baseScaler;
                Session.Enforced.Nerf = !unPackedData.Nerf.Equals(-1f) ? unPackedData.Nerf : nerf;
                Session.Enforced.Efficiency = !unPackedData.Efficiency.Equals(-1f) ? unPackedData.Efficiency : efficiency;
                Session.Enforced.StationRatio = !unPackedData.StationRatio.Equals(-1) ? unPackedData.StationRatio : stationRatio;
                Session.Enforced.LargeShipRatio = !unPackedData.LargeShipRatio.Equals(-1) ? unPackedData.LargeShipRatio : largeShipRate;
                Session.Enforced.SmallShipRatio = !unPackedData.SmallShipRatio.Equals(-1) ? unPackedData.SmallShipRatio : smallShipRatio;
                Session.Enforced.DisableVoxelSupport = !unPackedData.DisableVoxelSupport.Equals(-1) ? unPackedData.DisableVoxelSupport : disableVoxel;
                Session.Enforced.DisableGridDamageSupport = !unPackedData.DisableGridDamageSupport.Equals(-1) ? unPackedData.DisableGridDamageSupport : disableGridDmg;
                Session.Enforced.Debug = !unPackedData.Debug.Equals(-1) ? unPackedData.Debug : debug;
                Session.Enforced.AltRecharge = unPackedData.AltRecharge;
                Session.Enforced.Version = version;

                unPackedData = null;
                unPackCfg.Close();
                unPackCfg.Dispose();
                MyAPIGateway.Utilities.DeleteFileInGlobalStorage("DefenseShields.cfg");
                var newCfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var newData = MyAPIGateway.Utilities.SerializeToXML(Session.Enforced);
                newCfg.Write(newData);
                newCfg.Flush();
                newCfg.Close();

                if (Session.Enforced.Debug == 1) Log.Line($"wrote modified config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
            }
            else
            {
                Session.Enforced.BaseScaler = baseScaler;
                Session.Enforced.Nerf = nerf;
                Session.Enforced.Efficiency = efficiency;
                Session.Enforced.StationRatio = stationRatio;
                Session.Enforced.LargeShipRatio = largeShipRate;
                Session.Enforced.SmallShipRatio = smallShipRatio;
                Session.Enforced.DisableVoxelSupport = disableVoxel;
                Session.Enforced.DisableGridDamageSupport = disableGridDmg;
                Session.Enforced.Debug = debug;
                Session.Enforced.AltRecharge = altRecharge;
                Session.Enforced.Version = version;

                var cfg = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DefenseShields.cfg");
                var data = MyAPIGateway.Utilities.SerializeToXML(Session.Enforced);
                cfg.Write(data);
                cfg.Flush();
                cfg.Close();

                if (Session.Enforced.Debug == 1) Log.Line($"wrote new config file - file exists: {MyAPIGateway.Utilities.FileExistsInGlobalStorage("DefenseShields.cfg")}");
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

        private static double PowerCalculation(IMyEntity breaching, IMyCubeGrid grid)
        {
            var bPhysics = breaching.Physics;
            var sPhysics = grid.Physics;

            const double wattsPerNewton = (3.36e6 / 288000);
            var velTarget = sPhysics.GetVelocityAtPoint(breaching.Physics.CenterOfMassWorld);
            var accelLinear = sPhysics.LinearAcceleration;
            var velTargetNext = velTarget + accelLinear * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var velModifyNext = bPhysics.LinearVelocity;
            var linearImpulse = bPhysics.Mass * (velTargetNext - velModifyNext);
            var powerCorrectionInJoules = wattsPerNewton * linearImpulse.Length();

            return powerCorrectionInJoules * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        }
    }
}
