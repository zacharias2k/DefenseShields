
namespace DefenseShields
{
    using Support;
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using System;
    using VRage.Game.Entity;
    using VRage.Game;
    using Sandbox.Game.Entities;

    public partial class Session
    {
        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        private void AuthorDebug()
        {
            // server[1] + fullReport[1] + c1 & c2 + resetTime
            var authorsFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(AuthorPlayerId);
            if (authorsFaction == null || authorsFaction.PrivateInfo == string.Empty)
            {
                LogStats = false;
                return;
            }
            int serverSide;
            int fullReport;
            int resetTime;
            int col1;
            int col2;
            var server = int.TryParse(authorsFaction.PrivateInfo[0].ToString(), out serverSide);
            var report = int.TryParse(authorsFaction.PrivateInfo[1].ToString(), out fullReport);
            var c1 = int.TryParse(authorsFaction.PrivateInfo[2].ToString(), out col1);
            var c2 = int.TryParse(authorsFaction.PrivateInfo[3].ToString(), out col2);
            var reset = int.TryParse(authorsFaction.PrivateInfo.Substring(4), out resetTime);

            if (server && report && c1 && c2 && reset)
            {
                int column;
                if (col1 == 0) column = col2;
                else column = 10 + col2;

                LogStats = true;
                LogColumn = column;
                LogServer = serverSide == 1;
                LogFullReport = fullReport == 1;
                LogTime = resetTime;
            }
        }

        public void GenerateReport()
        {
            if (LogServer && !MpActive || !LogServer && MpActive)
            {
                if (LogFullReport) Log.CleanLine($"{EventLog[0]} {EventLog[1]} {EventLog[2]} {EventLog[3]} {EventLog[4]} {EventLog[5]} {EventLog[6]} {EventLog[7]} {EventLog[8]} {EventLog[9]} {EventLog[10]} {EventLog[11]} {EventLog[12]} {EventLog[13]} {EventLog[14]} {EventLog[15]}");
                else Log.CleanLine(EventLog[LogColumn]);
            }
            else if (LogServer && DedicatedServer)
            {
                NetworkReport.Report = EventLog;
                var data = new DataReport(0, NetworkReport);
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
                MyAPIGateway.Multiplayer.SendMessageTo(PACKET_ID, bytes, AuthorSteamId);
            }
        }

        public void ReceiveReport()
        {
            AuthorDebug();
            if (LogFullReport) Log.CleanLine($"{NetworkReport.Report[0]} {NetworkReport.Report[1]} {NetworkReport.Report[2]} {NetworkReport.Report[3]} {NetworkReport.Report[4]} {NetworkReport.Report[5]} {NetworkReport.Report[6]} {NetworkReport.Report[7]} {NetworkReport.Report[8]} {NetworkReport.Report[9]} {NetworkReport.Report[10]} {NetworkReport.Report[11]} {NetworkReport.Report[12]} {NetworkReport.Report[13]} {NetworkReport.Report[14]} {NetworkReport.Report[15]}");
            else Log.CleanLine(NetworkReport.Report[LogColumn]);
        }

        public MyEntity3DSoundEmitter AudioReady(MyEntity entity)
        {
            if (Tick - SoundTick < 600 && Tick > 600) return null;
            SoundTick = Tick;

            SoundEmitter.StopSound(false);
            SoundEmitter.Entity = entity;
            SoundEmitter.CustomVolume = MyAPIGateway.Session.Config.GameVolume * 0.75f;
            return SoundEmitter;
        }

        private void PlayerConnected(long id)
        {
            try
            {
                if (Players.ContainsKey(id))
                {
                    if (Enforced.Debug >= 2) Log.Line($"Player id({id}) already exists");
                    return;
                }
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerConnected: {ex}"); }
        }

        private void PlayerDisconnected(long l)
        {
            try
            {
                IMyPlayer removedPlayer;
                Players.TryRemove(l, out removedPlayer);
                PlayerEventId++;
                if (removedPlayer.SteamUserId == AuthorSteamId)
                {
                    AuthorPlayerId = 0;
                    LogStats = false;
                }
                if (Enforced.Debug >= 3) Log.Line($"Removed player, new playerCount:{Players.Count}");
            }
            catch (Exception ex) { Log.Line($"Exception in PlayerDisconnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                Players[id] = player;
                PlayerEventId++;
                if (player.SteamUserId == AuthorSteamId) AuthorPlayerId = player.IdentityId;
                if (Enforced.Debug >= 3) Log.Line($"Added player: {player.DisplayName}, new playerCount:{Players.Count}");
            }
            return false;
        }

        #region Events
        private void OnEntityRemove(MyEntity myEntity)
        {
            var warhead = myEntity as IMyWarhead;
            if (warhead != null)
            {
                if (!warhead.IsFunctional && (warhead.IsArmed || (warhead.DetonationTime <= 0 && warhead.IsCountingDown)) && warhead.CustomData.Length != 0)
                {
                    var blastRatio = warhead.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 1 : 5;
                    var epicCenter = warhead.PositionComp.WorldAABB.Center;
                    if (Enforced.Debug >= 2 && EmpStore.Count == 0) Log.Line($"====================================================================== [WarHead EventStart]");
                    EmpStore.Enqueue(new WarHeadBlast(blastRatio, epicCenter, warhead.CustomData));
                }
            }
        }
        #endregion
    }
}
