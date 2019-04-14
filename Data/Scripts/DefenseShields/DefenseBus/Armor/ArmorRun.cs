using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;

namespace DefenseSystems
{
    class Armors
    {
        internal Bus Bus;
        private bool _isServer;
        private bool _isDedicated;
        private bool _mpActive;
        private bool _inited;
        private bool _firstLoop = true;

        internal Armors(Bus bus)
        {
            Bus = bus;
            Run();
        }

        internal void Run(bool run = true)
        {
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;
            if (run)
            {
                _inited = true;
            }
            else
            {
                _inited = false;
            }
        }

        internal Controllers.Status Status()
        {
            var a = Bus.ActiveController;
            var state = a.State;
            var prevOnline = state.Value.Online;
            Bus.Starting = !prevOnline || _firstLoop;

            state.Value.Online = true;
            return Controllers.Status.Active;
        }

        internal Controllers.Status ClientStatus()
        {
            return Controllers.Status.Active;
        }

    }
}
