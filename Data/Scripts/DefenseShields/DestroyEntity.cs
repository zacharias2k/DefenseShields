using System;
using Sandbox.Game;
using VRage.Game.ModAPI;

namespace DefenseShields.Destroy
{
    class DestroyEntity : Station.DefenseShields
    {
        #region Close flagged grids
        public static void GridClose(int _gridcount)
        {
            try
            {
                if (_gridcount == -1 || _gridcount == 0)
                {
                    foreach (var grident in _destroyGridHash)
                    {
                        var grid = grident as IMyCubeGrid;
                        if (grid == null) continue;

                        if (_gridcount == -1)
                        {

                            var vel = grid.Physics.LinearVelocity;
                            vel.SetDim(0, (int) ((float) vel.GetDim(0) * 0.05f));
                            vel.SetDim(1, (int) ((float) vel.GetDim(1) * 0.05f));
                            vel.SetDim(2, (int) ((float) vel.GetDim(2) * 0.05f));
                            grid.Physics.LinearVelocity = vel;
                            lock (_destroyGridHash)
                            {
                                Logging.WriteLine(String.Format("{0} - test _destroyGridHash", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                                _destroyGridHash.Remove(grid);
                                Logging.WriteLine(String.Format("{0} - test2 _destroyGridHash", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                            }
                        }
                    }
                }
                /*
                var vel = grid.Physics.LinearVelocity;
                vel.SetDim(0, (int)0f);
                vel.SetDim(1, (int)0f);
                vel.SetDim(2, (int)0f);
                grid.Physics.LinearVelocity = vel;
                */
                            /*
                        }
                        else
                        {
                            var gridpos = grid.GetPosition();
                            //MyVisualScriptLogicProvider.CreateExplosion(gridpos, 30, 9999);
                        }
                    }
                }
                if (_gridcount < 59) return;

                foreach (var grident in _destroyGridHash)
                {
                    var grid = grident as IMyCubeGrid;
                    if (grid == null) continue;
                    if (_gridcount == 59 || _gridcount == 179 || _gridcount == 299 || _gridcount == 419)
                    {
                        Logging.WriteLine(String.Format("{0} inside grid destory {1} {2}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), _gridcount, _destroyGridHash.Count));
                        var gridpos = grid.GetPosition();
                        //MyVisualScriptLogicProvider.CreateExplosion(gridpos, _gridcount / 2f, _gridcount * 2);
                    }
                    if (_gridcount == 599)
                    {
                        grid.Close();
                    }
                }
                if (_gridcount == 599) _destroyGridHash.Clear();
                */
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in gridClose", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ex));
            }
        }
        #endregion

        #region Kill flagged players
        public static void PlayerKill(int _playercount)
        {
            try
            {
                if (_playercount != 479) return;
                foreach (var ent in _destroyPlayerHash)
                {
                    if (!(ent is IMyCharacter)) continue;
                    var playerent = (IMyCharacter)ent;
                    var playerpos = playerent.GetPosition();
                    //MyVisualScriptLogicProvider.CreateExplosion(playerpos, 10, 1000);
                    //playerent.Kill();
                }
                _destroyPlayerHash.Clear();
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in playerKill", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ex));
            }
        }
        #endregion
    }
}