using System;
using Sandbox.Game;
using VRage.Game.ModAPI;
using DefenseShields.Support;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseShields
{
    internal class DestroyEntity : DefenseShields
    {
        #region Close flagged grids
        public static void GridClose(int _gridcount)
        {
            try
            {
                if (_gridcount == -1 || _gridcount == 0)
                {
                    foreach (var grident in DestroyGridHash)
                    {
                        var grid = grident as IMyCubeGrid;
                        if (grid == null) continue;

                        if (_gridcount == -1)
                        {
                            /*
                            var vel = grid.Physics.LinearVelocity;
                            vel.SetDim(0, (int)((float)vel.GetDim(0) * 1.0f));
                            vel.SetDim(1, (int)((float)vel.GetDim(1) * 1.0f));
                            vel.SetDim(2, (int)((float)vel.GetDim(2) * 1.0f));
                            grid.Physics.LinearVelocity = vel;
                            */
                            var vel = grid.Physics.LinearVelocity;
                            vel.SetDim(0, (int) 0f);
                            vel.SetDim(1, (int) 0f);
                            vel.SetDim(2, (int) 0f);
                            grid.Physics.LinearVelocity = vel;
                        }
                        else
                        {
                            var gridpos = grid.GetPosition();
                            //MyVisualScriptLogicProvider.CreateExplosion(gridpos, 30, 9999);
                        }
                    }
                }
                if (_gridcount < 59) return;

                foreach (var grident in DestroyGridHash)
                {
                    var grid = grident as IMyCubeGrid;
                    if (grid == null) continue;
                    Log.Line($"passed continue check - l:{_gridcount} grids:{DestroyGridHash.Count}");
                    if (_gridcount == 59 || _gridcount == 179 || _gridcount == 299 || _gridcount == 419)
                    {
                        Log.Line($"inside grid destory {_gridcount} {DestroyGridHash.Count}");
                        var gridpos = grid.GetPosition();
                        //MyVisualScriptLogicProvider.CreateExplosion(gridpos, _gridcount / 2f, _gridcount * 2);
                    }
                    if (_gridcount == 599)
                    {
                        Log.Line($"{DateTime.Now:MM-dd-yy_HH-mm-ss-fff} closing {grid.DisplayName} in loop {_gridcount}");
                        grid.Close();
                    }
                }
                if (_gridcount == 599) DestroyGridHash.Clear();
            }
            catch (Exception ex)
            {
                Log.Line($" Exception in gridClose");
                Log.Line($" {ex}");
            }
        }
        #endregion

        #region Kill flagged players
        public static void PlayerKill(int _playercount)
        {
            try
            {
                if (_playercount != 479) return;
                foreach (var ent in DestroyPlayerHash)
                {
                    if (!(ent is IMyCharacter)) continue;
                    var playerent = (IMyCharacter)ent;
                    var playerpos = playerent.GetPosition();
                    //MyVisualScriptLogicProvider.CreateExplosion(playerpos, 10, 1000);

                    //playerent.Kill();
                }
                DestroyPlayerHash.Clear();
            }
            catch (Exception ex)
            {
                Log.Line($" Exception in playerKill");
                Log.Line($" {ex}");
            }
        }
        #endregion
        /*MyExplosionInfo exp = new MyExplosionInfo(_damage, _damage, new BoundingSphereD(position.Translation, _range), _type, true)
        {
            Direction = position.Forward,
            ExplosionFlags = _flags,
            OwnerEntity = _ownerEntity as MyEntity,
            VoxelExplosionCenter = position.Translation
        };*/
    }
}
