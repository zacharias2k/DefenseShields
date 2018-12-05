using System;
using DefenseShields.Support;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
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

            if (DsSet.Settings.Visible == 0) _shellPassive.Render.UpdateRenderObject(true);
            _shellActive.Render.UpdateRenderObject(true);
            _shellActive.Render.UpdateRenderObject(false);
        }

        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(MyCube.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;
            var renderId = MyGrid.Render.GetRenderObjectID();
            var percent = DsState.State.ShieldPercent;
            var hitAnim = DsSet.Settings.HitWaveAnimation;
            var refreshAnim = DsSet.Settings.RefreshAnimation;
            var config = MyAPIGateway.Session.Config;
            var drawIcon = !enemy && DsSet.Settings.SendToHud && !config.MinimalHud && Session.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible;
            var viewCheck = _count == 0 || _count == 19 || _count == 39;
            if (viewCheck) _viewInShield = CustomCollision.PointInShield(MyAPIGateway.Session.Camera.WorldMatrix.Translation, DetectMatrixOutsideInv);
            var clearView = !GridIsMobile || !_viewInShield;
            if (viewCheck && _hideColor && !_supressedColor && _viewInShield)
            {
                _modelPassive = ModelMediumReflective;
                UpdatePassiveModel();
                _supressedColor = true;
                _hideShield = false;
            }
            else if (viewCheck && _supressedColor && _hideColor && !_viewInShield)
            {
                SelectPassiveShell();
                UpdatePassiveModel();
                _supressedColor = false;
                _hideShield = false;
            }
            if (drawIcon) UpdateIcon();

            var activeVisible = !DsSet.Settings.ActiveInvisible && clearView || enemy;
            CalcualteVisibility(DsSet.Settings.Visible, activeVisible);

            var impactPos = WorldImpactPosition;
            var webEffect = WebDamage && BulletCoolDown > -1 && WebCoolDown < 0;

            _localImpactPosition = Vector3D.NegativeInfinity;
            if (impactPos != Vector3D.NegativeInfinity && (BulletCoolDown < 0 || webEffect))
            {
                if (webEffect)
                {
                    WebCoolDown = 0;
                    HitParticleStart();
                }
                else
                {
                    if (WebDamage) WebCoolDown = 0;
                    BulletCoolDown = 0;
                    HitParticleStart();
                    var cubeBlockLocalMatrix = MyGrid.PositionComp.LocalMatrix;
                    var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                    var worldDirection = impactPos - referenceWorldPosition;
                    var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                    _localImpactPosition = localPosition;
                }
            }

            if (EmpDetonation != Vector3D.NegativeInfinity) EmpParticleStart();

            WorldImpactPosition = Vector3D.NegativeInfinity;
            EmpDetonation = Vector3D.NegativeInfinity;
            WebDamage = false;

            if (IsWorking)
            {
                var prevlod = _prevLod;
                var lod = CalculateLod(_onCount);
                if (_shapeChanged || _updateRender || lod != prevlod) Icosphere.CalculateTransform(_shieldShapeMatrix, lod);
                Icosphere.ComputeEffects(_shieldShapeMatrix, _localImpactPosition, _shellPassive, _shellActive, prevlod, percent, activeVisible, refreshAnim);
            }
            if (hitAnim && sphereOnCamera && IsWorking) Icosphere.Draw(renderId);

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
            if (playerDist < 15) playerDist = 20;
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
            _effect.Velocity = MyGrid.Physics.LinearVelocity;
            _effect.Play();
        }

        private void EmpParticleStart()
        {
            _effect.Stop(true);
            var pos = EmpDetonation;
            var matrix = MatrixD.CreateTranslation(pos);

            MyParticlesManager.TryCreateParticleEffect(6667, out _effect, ref matrix, ref pos, _shieldEntRendId, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (_effect == null) return;
            var playerDist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            var radius = playerDist * 0.15d;
            var scale = (playerDist + playerDist * 0.001) / playerDist * 0.03 * (EmpSize * 0.0008);
            if (scale > 0.3)
            {
                var scaler = EmpSize / 16755 * 0.05;
                scale = 0.3 + scaler;
            }
            _effect.UserRadiusMultiplier = (float)radius;
            _effect.UserEmitterScale = (float)scale;
            _effect.Velocity = MyGrid.Physics.LinearVelocity;
            _effect.UserColorMultiplier = new Vector4(255, 255, 255, 10);
            _effect.Play();
        }

        public void HitParticleStop()
        {
            if (_effect == null) return;
            _effect.Stop();
            _effect.Close(false, true);
            _effect = null;
        }

        private void CalcualteVisibility(long visible, bool activeVisible)
        {
            if (visible != 2) HitCoolDown = -11;
            else if (visible == 2 && WorldImpactPosition != Vector3D.NegativeInfinity) HitCoolDown = -10;
            else if (visible == 2 && HitCoolDown > -11) HitCoolDown++;
            if (HitCoolDown > 59) HitCoolDown = -11;

            var passiveSet = visible != 0 && !_hideShield && HitCoolDown == -11;
            var passiveReset = visible == 0 && _hideShield || _hideShield && visible != 0 && !activeVisible && _hideShield && HitCoolDown == -10;
            var passiveFade = HitCoolDown > -1 && visible != 0;
            var fadeReset = visible == 2 && !passiveFade && HitCoolDown != -11;

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
            var playerEnt = MyAPIGateway.Session.ControlledObject?.Entity as MyEntity;
            if (playerEnt?.Parent != null) playerEnt = playerEnt.Parent;
            if (playerEnt == null || DsState.State.Online && !CustomCollision.PointInShield(playerEnt.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv) || !DsState.State.Online && !CustomCollision.PointInShield(playerEnt.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
            {
                if (Session.HudComp != this) return;
                EntIntersectInfo entInfo;
                WebEnts.TryGetValue(playerEnt, out entInfo);
                if (entInfo != null && entInfo.Relation != Ent.Protected) return;

                Session.HudComp = null;
                Session.HudShieldDist = double.MaxValue;
                return;
            }

            var distFromShield = Vector3D.DistanceSquared(playerEnt.PositionComp.WorldVolume.Center, DetectionCenter);
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
            var percent = DsState.State.ShieldPercent;
            var heat = DsState.State.Heat;

            var icon1 = GetHudIcon1FromFloat(percent);
            var icon2 = GetHudIcon2FromFloat(icon2FSelect);
            var icon3 = GetHudIcon3FromInt(heat, _lCount % 2 == 0);
            var showIcon2 = DsState.State.Online;
            Color color;
            if (percent > 0 && percent < 10 && _lCount % 2 == 0) color = Color.Red;
            else color = Color.White;
            MyTransparentGeometry.AddBillboardOriented(icon1, color, origin, left, up, (float)scale, BlendTypeEnum.LDR); // LDR for mptest, SDR for public
            if (showIcon2 && icon2 != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(icon2, Color.White, origin, left, up, (float)scale * 1.11f, BlendTypeEnum.LDR);
            if (icon3 != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(icon3, Color.White, origin, left, up, (float)scale * 1.11f, BlendTypeEnum.LDR);
        }

        private float GetIconMeterfloat()
        {
            var consumptionRate = _shieldConsumptionRate;
            var hps = _shieldChargeRate;
            var dps = _runningDamage / 60;
            if (hps < 1) hps = 1;
            if (dps < 1) dps = 1;

            var maxHps = GridMaxPower - consumptionRate * 0.05f;
            var dpsScaledRate = dps * (consumptionRate / hps);
            var charging = hps > dps;
            var hpsOfMax = consumptionRate / maxHps * 100;
            var dpsOfMax = dpsScaledRate / maxHps * 100;

            float percentOfMax = 0;
            if (charging)
            {
                if (hps > 0.01) percentOfMax = hpsOfMax - dpsOfMax;
            }
            else
            {
                if (dps > 0.01) percentOfMax = dpsOfMax - hpsOfMax;
            }

            if (charging) return percentOfMax;
            return -percentOfMax;
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
            if (fState >= 0)
            {
                if (fState < 9) return MyStringId.NullOrEmpty;
                if (fState < 19) return HudIconHeal10;
                if (fState < 29) return HudIconHeal20;
                if (fState < 39) return HudIconHeal30;
                if (fState < 49) return HudIconHeal40;
                if (fState < 59) return HudIconHeal50;
                if (fState < 69) return HudIconHeal60;
                if (fState < 79) return HudIconHeal70;
                if (fState < 89) return HudIconHeal80;
                if (fState < 99) return HudIconHeal90;
                return HudIconHeal100;
            }

            if (fState > -9) return MyStringId.NullOrEmpty;
            if (fState > -19) return HudIconDps10;
            if (fState > -29) return HudIconDps20;
            if (fState > -39) return HudIconDps30;
            if (fState > -49) return HudIconDps40;
            if (fState > -59) return HudIconDps50;
            if (fState > -69) return HudIconDps60;
            if (fState > -79) return HudIconDps70;
            if (fState > -89) return HudIconDps80;
            if (fState > -99) return HudIconDps90;
            return HudIconDps100;
        }

        public static MyStringId GetHudIcon3FromInt(int heat, bool flash)
        {
            if (heat == 100 && flash) return HudIconHeat100;
            if (heat == 90) return HudIconHeat90;
            if (heat == 80) return HudIconHeat80;
            if (heat == 70) return HudIconHeat70;
            if (heat == 60) return HudIconHeat60;
            if (heat == 50) return HudIconHeat50;
            if (heat == 40) return HudIconHeat40;
            if (heat == 30) return HudIconHeat30;
            if (heat == 20) return HudIconHeat20;
            if (heat == 10) return HudIconHeat10;
            return MyStringId.NullOrEmpty;
        }

        public void DrawShieldDownIcon()
        {
            if (Tick % 60 != 0 && !Session.DedicatedServer) HudCheck();
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(MyCube.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            if (!enemy && DsSet.Settings.SendToHud && !config.MinimalHud && Session.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible) UpdateIcon();
        }
    }
}
