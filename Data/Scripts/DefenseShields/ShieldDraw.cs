using System;
using DefenseShields.Support;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void ShellVisibility(bool forceInvisible = false)
        {
            if (forceInvisible)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _shellActive.Render.UpdateRenderObject(false);
                return;
            }

            if (!ShieldPassiveHide) _shellPassive.Render.UpdateRenderObject(true);
            _shellActive.Render.UpdateRenderObject(true);
            _shellActive.Render.UpdateRenderObject(false);
        }

        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            var renderId = Shield.CubeGrid.Render.GetRenderObjectID();
            var config = MyAPIGateway.Session.Config;
            var drawIcon = !enemy && SendToHud && !config.MinimalHud && Session.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible;
            if (drawIcon)
            {
                UpdateIcon();
            }

            var passiveVisible = !ShieldPassiveHide || enemy;
            var activeVisible = !ShieldActiveHide || enemy;
            CalcualteVisibility(passiveVisible, activeVisible);

            var impactPos = WorldImpactPosition;
            _localImpactPosition = Vector3D.NegativeInfinity;
            if (impactPos != Vector3D.NegativeInfinity && BulletCoolDown < 0)
            {
                BulletCoolDown = 0;
                HitParticleStart();
                var cubeBlockLocalMatrix = Shield.CubeGrid.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                _localImpactPosition = localPosition;
            }
            WorldImpactPosition = Vector3D.NegativeInfinity;

            if (Shield.IsWorking)
            {
                var prevlod = _prevLod;
                var lod = CalculateLod(_onCount);
                if (_shapeChanged || _updateRender || lod != prevlod) Icosphere.CalculateTransform(_shieldShapeMatrix, lod);
                Icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, _shellPassive, _shellActive, prevlod, ShieldComp.ShieldPercent, passiveVisible, activeVisible);
            }
            if (sphereOnCamera && Shield.IsWorking) Icosphere.Draw(renderId);

            _updateRender = false;
            _shapeChanged = false;
        }

        private int CalculateLod(int onCount)
        {
            var lod = 4;

            if (onCount > 9) lod = 2;
            else if (onCount > 3) lod = 3;

            _prevLod = lod;
            return lod;
        }

        private void HitParticleStart()
        {
            var pos = WorldImpactPosition;
            var matrix = MatrixD.CreateTranslation(pos);

            MyParticlesManager.TryCreateParticleEffect(6667, out _effect, ref matrix, ref pos, _shieldEntRendId, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (_effect == null) return;
            var playerDist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            var radius = playerDist * 0.15d;
            var scale = (playerDist + playerDist * 0.001) / playerDist * 0.03;
            if (ImpactSize < 150)
            {
                scale = scale * 0.3;
                radius = radius * 9;
            }
            else if (ImpactSize > 12000) scale = 0.1;
            else if (ImpactSize > 3600) scale = scale * (ImpactSize / 3600);
            if (scale > 0.1) scale = 0.1;
            //Log.Line($"D:{playerDist} - R:{radius} - S:{scale} - I:{ImpactSize} - {MyAPIGateway.Session.IsCameraUserControlledSpectator} = {MyAPIGateway.Session.CameraTargetDistance} - {Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos)}");
            _effect.UserRadiusMultiplier = (float)radius;
            _effect.UserEmitterScale = (float)scale;
            _effect.Play();
        }

        public void HitParticleStop()
        {
            if (_effect == null) return;
            _effect.Stop();
            _effect.Close(false, true);
            _effect = null;
        }

        private void CalcualteVisibility(bool passiveVisible, bool activeVisible)
        {
            if (WorldImpactPosition != Vector3D.NegativeInfinity) HitCoolDown = -10;
            else if (HitCoolDown > -11) HitCoolDown++;
            if (HitCoolDown > 59) HitCoolDown = -11;
            var passiveSet = !passiveVisible && !_hideShield && HitCoolDown == -11;
            var passiveReset = passiveVisible && _hideShield || _hideShield && !passiveVisible && !activeVisible && _hideShield && HitCoolDown == -10;
            var passiveFade = HitCoolDown > -1 && !passiveVisible && !activeVisible;
            var fadeReset = !passiveFade && !activeVisible && HitCoolDown != -11;

            if (fadeReset)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Render.Transparency = 0f;
                _shellPassive.Render.UpdateRenderObject(true);
            }

            if (passiveFade)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Render.Transparency = (HitCoolDown + 1) * 0.0166666666667f;
                _shellPassive.Render.UpdateRenderObject(true);
            }
            else if (passiveSet)
            {
                _hideShield = true;
                _shellPassive.Render.UpdateRenderObject(false);
                _shellPassive.Render.Transparency = 0f;
            }
            else if (passiveReset)
            {
                _shellPassive.Render.UpdateRenderObject(false);
                _hideShield = false;
                _shellPassive.Render.Transparency = 0f;
                _shellPassive.Render.UpdateRenderObject(true);
            }
        }

        private void HudCheck()
        {
            var playerEnt = MyAPIGateway.Session.ControlledObject?.Entity;
            if (playerEnt?.Parent != null) playerEnt = playerEnt.Parent;
            if (playerEnt == null || ShieldComp.ShieldActive && !FriendlyCache.Contains(playerEnt) || !ShieldComp.ShieldActive && !CustomCollision.PointInShield(playerEnt.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
            {
                if (Session.HudComp != this) return;

                Session.HudComp = null;
                Session.HudShieldDist = double.MaxValue;
                return;
            }

            var distFromShield = Vector3D.DistanceSquared(playerEnt.WorldVolume.Center, DetectionCenter);
            if (Session.HudComp != this && distFromShield <= Session.HudShieldDist)
            {
                Session.HudShieldDist = distFromShield;
                Session.HudComp = this;
            }
        }

        private void UpdateIcon()
        {
            //Moving average of the average of the two values, then moving average if the difference from the average.
            var position = new Vector3D(_shieldIconPos.X, _shieldIconPos.Y, 0);
            var fov = MyAPIGateway.Session.Camera.FovWithZoom;
            double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;

            var cameraWorldMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            const double scaler = 0.08;
            scale = scaler * scale;

            var icon2FSelect = GetIconMeterfloat();

            var icon1 = GetHudIcon1FromFloat(ShieldComp.ShieldPercent);
            var icon2 = GetHudIcon2FromFloat(icon2FSelect);
            var showIcon2 = DsStatus.State.Online;
            Color color;
            var p = ShieldComp.ShieldPercent;
            if (p > 0 && p < 10 && _lCount % 2 == 0) color = Color.Red;
            else color = Color.White;
            MyTransparentGeometry.AddBillboardOriented(icon1, color, origin, left, up, (float)scale, BlendTypeEnum.LDR); // LDR for mptest, SDR for public
            if (showIcon2 && icon2 != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(icon2, Color.White, origin, left, up, (float)scale * 1.11f, BlendTypeEnum.LDR);
        }

        private float GetIconMeterfloat()
        {
            var dps = 1f;
            if (_damageCounter > 1) dps = _damageCounter / Session.Enforced.Efficiency;

            var healing = _shieldChargeRate / Session.Enforced.Efficiency - dps;
            var damage = dps - _shieldChargeRate;

            if (healing > 0 && _damageCounter > 1) return healing;
            else return -damage;
        }

        public static MyStringId GetHudIcon1FromFloat(float percent)
        {
            if (percent >= 99) return HudIconHealth100;
            if (percent >= 90) return HudIconHealth90;
            if (percent >= 80) return HudIconHealth80;
            if (percent >= 70) return HudIconHealth70;
            if (percent >= 60) return HudIconHealth60;
            if (percent >= 50) return HudIconHealth50;
            if (percent >= 40) return HudIconHealth40;
            if (percent >= 30) return HudIconHealth30;
            if (percent >= 20) return HudIconHealth20;
            if (percent > 0) return HudIconHealth10;
            return HudIconOffline;
        }

        public static MyStringId GetHudIcon2FromFloat(float fState)
        {
            if (fState > 0)
            {
                if (fState <= 1) return HudIconHeal100;
                if (fState <= 10) return HudIconHeal90;
                if (fState <= 20) return HudIconHeal80;
                if (fState <= 30) return HudIconHeal70;
                if (fState <= 40) return HudIconHeal60;
                if (fState <= 50) return HudIconHeal50;
                if (fState <= 60) return HudIconHeal40;
                if (fState <= 70) return HudIconHeal30;
                if (fState <= 80) return HudIconHeal20;
                if (fState <= 90) return HudIconHeal10;
                if (fState > 90) return MyStringId.NullOrEmpty;
            }

            if (fState <= -99) return HudIconDps100;
            if (fState <= -90) return HudIconDps90;
            if (fState <= -80) return HudIconDps80;
            if (fState <= -70) return HudIconDps70;
            if (fState <= -60) return HudIconDps60;
            if (fState <= -50) return HudIconDps50;
            if (fState <= -40) return HudIconDps40;
            if (fState <= -30) return HudIconDps30;
            if (fState <= -20) return HudIconDps20;
            if (fState < -10) return HudIconDps10;
            return MyStringId.NullOrEmpty;
        }

        public void DrawShieldDownIcon()
        {
            if (_tick % 60 != 0 && !Session.DedicatedServer) HudCheck();
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Shield.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            if (!enemy && SendToHud && !config.MinimalHud && Session.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible) UpdateIcon();
        }
    }
}
