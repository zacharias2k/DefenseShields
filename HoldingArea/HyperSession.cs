using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;

namespace Hyperdrive
{
    #region Session+protection Class
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase
    {
        private uint _tick;

        internal bool SessionInit;
        public bool Enabled = true;
        public static readonly bool MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
        public static readonly bool IsServer = MyAPIGateway.Multiplayer.IsServer;
        public static readonly bool DedicatedServer = MyAPIGateway.Utilities.IsDedicated;

        private int _count = -1;
        private int _lCount;
        private int _eCount;

        public static Session Instance { get; private set; }
        public readonly MyModContext MyModContext = new MyModContext();
        public readonly List<Hyperdrives> Components = new List<Hyperdrives>();


        public void Init()
        {
            try
            {
                Log.Init("debugdevelop.log");
                Log.Line($"Logging Started");
                SessionInit = true;
            }
            catch (Exception ex) { Log.Line($"Exception in SessionInit: {ex}"); }
        }

        public override void Draw()
        {
            if (DedicatedServer) return;
            try { }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = (uint)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds / MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS;
                if (!SessionInit)
                {
                    if (DedicatedServer) Init();
                    else if (MyAPIGateway.Session != null) Init();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}"); }
        }

        public string ModPath()
        {
            var modPath = ModContext.ModPath;
            return modPath;
        }

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
            Log.Line("Logging stopped.");
            Log.Close();
        }
    }
    #endregion
}
