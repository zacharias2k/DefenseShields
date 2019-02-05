namespace DefenseShields
{
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;

    public partial class DefenseShields
    {
        #region Shield Support Blocks
        public void GetModulationInfo()
        {
            var update = false;
            if (ShieldComp.Modulator != null && ShieldComp.Modulator.ModState.State.Online)
            {
                var modEnergyRatio = ShieldComp.Modulator.ModState.State.ModulateEnergy * 0.01f;
                var modKineticRatio = ShieldComp.Modulator.ModState.State.ModulateKinetic * 0.01f;
                if (!DsState.State.ModulateEnergy.Equals(modEnergyRatio) || !DsState.State.ModulateKinetic.Equals(modKineticRatio) || !DsState.State.EmpProtection.Equals(ShieldComp.Modulator.ModSet.Settings.EmpEnabled) || !DsState.State.ReInforce.Equals(ShieldComp.Modulator.ModSet.Settings.ReInforceEnabled)) update = true;
                DsState.State.ModulateEnergy = modEnergyRatio;
                DsState.State.ModulateKinetic = modKineticRatio;
                if (DsState.State.Enhancer)
                {
                    DsState.State.EmpProtection = ShieldComp.Modulator.ModSet.Settings.EmpEnabled;
                    DsState.State.ReInforce = ShieldComp.Modulator.ModSet.Settings.ReInforceEnabled;
                }

                if (update) ShieldChangeState();
            }
            else
            {
                if (!DsState.State.ModulateEnergy.Equals(1f) || !DsState.State.ModulateKinetic.Equals(1f) || DsState.State.EmpProtection || DsState.State.ReInforce) update = true;
                DsState.State.ModulateEnergy = 1f;
                DsState.State.ModulateKinetic = 1f;
                DsState.State.EmpProtection = false;
                DsState.State.ReInforce = false;
                if (update) ShieldChangeState();

            }
        }

        public void GetEnhancernInfo()
        {
            var update = false;
            if (ShieldComp.Enhancer != null && ShieldComp.Enhancer.EnhState.State.Online)
            {
                if (!DsState.State.EnhancerPowerMulti.Equals(2) || !DsState.State.EnhancerProtMulti.Equals(1000) || !DsState.State.Enhancer) update = true;
                DsState.State.EnhancerPowerMulti = 2;
                DsState.State.EnhancerProtMulti = 1000;
                DsState.State.Enhancer = true;
                if (update) ShieldChangeState();
            }
            else
            {
                if (!DsState.State.EnhancerPowerMulti.Equals(1) || !DsState.State.EnhancerProtMulti.Equals(1) || DsState.State.Enhancer) update = true;
                DsState.State.EnhancerPowerMulti = 1;
                DsState.State.EnhancerProtMulti = 1;
                DsState.State.Enhancer = false;
                if (!DsState.State.Overload) DsState.State.ReInforce = false;
                if (update) ShieldChangeState();
            }
        }
        #endregion

        internal void AddShieldHit(long attackerId, float amount, MyStringHash damageType, IMySlimBlock block, bool reset, Vector3D? hitPos = null)
        {
            lock (ShieldHit)
            {
                ShieldHit.Amount += amount;
                ShieldHit.DamageType = damageType.String;

                if (block != null && !hitPos.HasValue && ShieldHit.HitPos == Vector3D.Zero)
                {
                    if (block.FatBlock != null) ShieldHit.HitPos = block.FatBlock.PositionComp.WorldAABB.Center;
                    else block.ComputeWorldCenter(out ShieldHit.HitPos);
                }
                else if (hitPos.HasValue) ShieldHit.HitPos = hitPos.Value;

                if (attackerId != 0) ShieldHit.AttackerId = attackerId;
                if (amount > 0) _lastSendDamageTick = _tick;
                if (reset) ShieldHitReset(true);
            }
        }

        internal void AddEmpBlastHit(long attackerId, float amount, MyStringHash damageType, Vector3D hitPos)
        {
            ShieldHit.Amount += amount;
            ShieldHit.DamageType = damageType.String;
            ShieldHit.HitPos = hitPos;
            ShieldHit.AttackerId = attackerId;
            _lastSendDamageTick = _tick;
        }

        internal void SendShieldHits()
        {
            while (ProtoShieldHits.Count != 0)
                Session.Instance.PacketizeToClientsInRange(Shield, new DataShieldHit(MyCube.EntityId, ProtoShieldHits.Dequeue()));
        }

        private void ShieldHitReset(bool enQueue)
        {
            if (enQueue)
            {
                if (_isServer)
                {
                    if (_mpActive) ProtoShieldHits.Enqueue(CloneHit());
                    if (!_isDedicated) AddLocalHit();
                }
            }
            _lastSendDamageTick = uint.MaxValue;
            _forceBufferSync = true;
            ShieldHit.AttackerId = 0;
            ShieldHit.Amount = 0;
            ShieldHit.DamageType = string.Empty;
            ShieldHit.HitPos = Vector3D.Zero;
        }

        private ShieldHitValues CloneHit()
        {
            var hitClone = new ShieldHitValues
            {
                Amount = ShieldHit.Amount,
                AttackerId = ShieldHit.AttackerId,
                HitPos = ShieldHit.HitPos,
                DamageType = ShieldHit.DamageType
            };

            return hitClone;
        }

        private void AddLocalHit()
        {
            ShieldHits.Add(new ShieldHit(MyEntities.GetEntityById(ShieldHit.AttackerId), ShieldHit.Amount, MyStringHash.GetOrCompute(ShieldHit.DamageType), ShieldHit.HitPos));
        }

        private void UserDebug()
        {
            var message = $"User({MyAPIGateway.Multiplayer.Players.TryGetSteamId(Shield.OwnerId)}) Debugging\n" +
                          $"On:{DsState.State.Online} - Active:{Session.Instance.ActiveShields.ContainsKey(this)} - Suspend:{DsState.State.Suspended}\n" +
                          $"Web:{Asleep} - Tick/LWoke:{_tick}/{LastWokenTick}\n" +
                          $"Mo:{DsState.State.Mode} - Su:{DsState.State.Suspended} - Wa:{DsState.State.Waking}\n" +
                          $"Np:{DsState.State.NoPower} - Lo:{DsState.State.Lowered} - Sl:{DsState.State.Sleeping}\n" +
                          $"PSys:{MyGridDistributor?.SourcesEnabled} - PNull:{MyGridDistributor == null}\n" +
                          $"MaxPower:{GridMaxPower} - AvailPower:{GridAvailablePower}\n" +
                          $"Access:{DsState.State.ControllerGridAccess} - EmitterWorking:{DsState.State.EmitterWorking}\n" +
                          $"ProtectedEnts:{ProtectedEntCache.Count} - ProtectMyGrid:{Session.Instance.GlobalProtect.ContainsKey(MyGrid)}\n" +
                          $"ShieldMode:{ShieldMode} - pFail:{_powerFail}\n" +
                          $"Sink:{_sink.CurrentInputByType(GId)} - PFS:{_powerNeeded}/{GridMaxPower}\n" +
                          $"Pow:{_power} HP:{DsState.State.Charge}: {ShieldMaxCharge}";

            if (!_isDedicated) MyAPIGateway.Utilities.ShowMessage(string.Empty, message);
            else Log.Line(message);
        }

        private void AbsorbClientShieldHits()
        {
            for (int i = 0; i < ShieldHits.Count; i++)
            {
                var hit = ShieldHits[i];
                var damageType = hit.DamageType;

                if (!WasOnline) continue;

                if (damageType == Session.Instance.MPExplosion)
                {
                    ImpactSize = hit.Amount;
                    WorldImpactPosition = hit.HitPos;
                    EnergyHit = true;
                    Absorb += hit.Amount * ConvToWatts;
                    UtilsStatic.CreateFakeSmallExplosion(WorldImpactPosition);
                    if (hit.Attacker != null)
                    {
                        hit.Attacker.Close();
                        hit.Attacker.InScene = false;
                    }
                    continue;
                }
                if (damageType == Session.Instance.MPKinetic)
                {
                    ImpactSize = hit.Amount;
                    WorldImpactPosition = hit.HitPos;
                    EnergyHit = false;
                    Absorb += hit.Amount * ConvToWatts;
                    continue;
                }
                if (damageType == Session.Instance.MPEnergy)
                {
                    ImpactSize = hit.Amount;
                    WorldImpactPosition = hit.HitPos;
                    EnergyHit = true;
                    Absorb += hit.Amount * ConvToWatts;
                    continue;
                }
                if (damageType == Session.Instance.MPEMP)
                {
                    ImpactSize = hit.Amount;
                    WorldImpactPosition = hit.HitPos;
                    EnergyHit = true;
                    Absorb += hit.Amount * ConvToWatts;
                    continue;
                }
            }
            ShieldHits.Clear();
        }
    }
}
