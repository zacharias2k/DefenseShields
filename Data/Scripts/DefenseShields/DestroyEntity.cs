using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace DefenseShields.Destroy
{
    class DestroyEntity : Station.DefenseShields
    {
        #region Close flagged grids
        public override void GridClose()
        {
            try
            {
                if (_gridcount == 599)
                {
                    _gridCloseHash.Clear();
                    return;
                }
                if (_gridcount == -1)
                {
                    Logging.WriteLine(String.Format("{0} pre-1stloop {1} {2}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), _gridcount, _gridCloseHash.Count));
                    foreach (var grident in _gridCloseHash)
                    {
                        var grid = grident as IMyCubeGrid;
                        if (grid == null) return;
                        var gridpos = grid.GetPosition();
                        MyVisualScriptLogicProvider.CreateExplosion(gridpos, 30, 9999);
                        var vel = grid.Physics.LinearVelocity;
                        vel.SetDim(0, (int)((float)vel.GetDim(0) * -1.5f));
                        vel.SetDim(1, (int)((float)vel.GetDim(1) * -1.5f));
                        vel.SetDim(2, (int)((float)vel.GetDim(2) * -1.5f));
                        grid.Physics.LinearVelocity = vel;
                    }
                }
                if (_gridcount == -1 || _gridcount == 599) return;

                foreach (var grident in _gridCloseHash)
                {
                    var grid = grident as IMyCubeGrid;
                    if (grid == null) return;
                    if (_gridcount == 59 || _gridcount == 179 || _gridcount == 299 || _gridcount == 419)
                    {
                        var gridpos = grid.GetPosition();
                        MyVisualScriptLogicProvider.CreateExplosion(gridpos, _gridcount / 2f, _gridcount * 2);
                        return;
                    }
                    if (_gridcount == 598)
                    {
                        grid.Close();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(String.Format("{0} - Exception in gridClose", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff")));
                Logging.WriteLine(String.Format("{0} - {1}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ex));
            }
        }
        #endregion

        #region Kill flagged players
        public override void PlayerKill()
        {
            try
            {
                if (_playercount != 479) return;
                foreach (var identityId in _playerKillList)
                {
                    if (identityId != null)
                    {
                        var playerid = (long) identityId;
                        {
                            var playerentid = MyVisualScriptLogicProvider.GetPlayersEntityId(playerid);
                            var player = MyAPIGateway.Entities.GetEntityById(playerentid);
                            var playerent = (IMyCharacter)player;
                            var playerpos = playerent.GetPosition();
                            MyVisualScriptLogicProvider.CreateExplosion(playerpos, 10, 1000);
                            playerent.Kill();
                        }
                    }
                }
                _playerKillList.Clear();
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
