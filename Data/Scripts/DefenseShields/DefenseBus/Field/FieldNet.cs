using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        internal void NetHits()
        {
            if (_isServer)
            {
                if (Bus.Tick - 1 > _lastSendDamageTick) ShieldHitReset(ShieldHit.Amount > 0 && ShieldHit.HitPos != Vector3D.Zero);
                if (ShieldHitsToSend.Count != 0) SendShieldHits();
                if (!_isDedicated && ShieldHits.Count != 0) AbsorbClientShieldHits();
            }
            else if (ShieldHits.Count != 0) AbsorbClientShieldHits();
        }
    }
}
