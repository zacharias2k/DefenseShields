namespace DefenseShields
{
    using System;
    using global::DefenseShields.Support;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Entity;
    using VRage.Utils;
    using VRageMath;
    using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

    public partial class DefenseShields
    {
        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;

            var renderId = MyGrid.Render.GetRenderObjectID();
            var percent = DsState.State.ShieldPercent;
            var reInforce = DsState.State.ReInforce;
            var hitAnim = !reInforce && DsSet.Settings.HitWaveAnimation;
            var refreshAnim = !reInforce && DsSet.Settings.RefreshAnimation;

            var activeVisible = DetermineVisualState(reInforce);

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
                if (_shapeChanged || _updateRender || lod != prevlod)
                {
                    Icosphere.CalculateTransform(ShieldShapeMatrix, lod);
                    if (!GridIsMobile) Icosphere.ReturnPhysicsVerts(DetectionMatrix, ShieldComp.PhysicsOutside);
                }
                Icosphere.ComputeEffects(ShieldShapeMatrix, _localImpactPosition, _shellPassive, _shellActive, prevlod, percent, activeVisible, refreshAnim);
            }
            if (hitAnim && sphereOnCamera && IsWorking) Icosphere.Draw(renderId);

            _updateRender = false;
            _shapeChanged = false;
        }

        public void DrawShieldDownIcon()
        {
            if (_tick % 60 != 0 && !_isDedicated) HudCheck();
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(MyCube.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            if (!enemy && DsSet.Settings.SendToHud && !config.MinimalHud && Session.Instance.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible) UpdateIcon();
        }

        public void HitParticleStop()
        {
            if (_effect == null) return;
            _effect.Stop();
            _effect.Close(false, true);
            _effect = null;
        }

        private static MyStringId GetHudIcon1FromFloat(float percent)
        {
            if (percent >= 99) return Session.Instance.HudIconHealth100;
            if (percent >= 90) return Session.Instance.HudIconHealth90;
            if (percent >= 80) return Session.Instance.HudIconHealth80;
            if (percent >= 70) return Session.Instance.HudIconHealth70;
            if (percent >= 60) return Session.Instance.HudIconHealth60;
            if (percent >= 50) return Session.Instance.HudIconHealth50;
            if (percent >= 40) return Session.Instance.HudIconHealth40;
            if (percent >= 30) return Session.Instance.HudIconHealth30;
            if (percent >= 20) return Session.Instance.HudIconHealth20;
            if (percent > 0) return Session.Instance.HudIconHealth10;
            return Session.Instance.HudIconOffline;
        }

        private static MyStringId GetHudIcon2FromFloat(float fState)
        {
            var oneTenth = fState * 0.1;
            if (oneTenth > -0.1 && oneTenth < 0.1) return MyStringId.NullOrEmpty;
            return oneTenth > 0 ? Session.Instance.HudHealthHpIcons[(int)Math.Floor(MathHelper.Clamp(oneTenth, 0, 10))] : Session.Instance.HudHealthHpIcons[(int)Math.Floor(MathHelper.Clamp(-oneTenth * 0.1, 0, 9)) + 10];
        }

        private static MyStringId GetHudIcon3FromInt(int heat, bool flash)
        {
            if (heat == 100 && flash) return Session.Instance.HudIconHeat100;
            if (heat == 90) return Session.Instance.HudIconHeat90;
            if (heat == 80) return Session.Instance.HudIconHeat80;
            if (heat == 70) return Session.Instance.HudIconHeat70;
            if (heat == 60) return Session.Instance.HudIconHeat60;
            if (heat == 50) return Session.Instance.HudIconHeat50;
            if (heat == 40) return Session.Instance.HudIconHeat40;
            if (heat == 30) return Session.Instance.HudIconHeat30;
            if (heat == 20) return Session.Instance.HudIconHeat20;
            if (heat == 10) return Session.Instance.HudIconHeat10;
            return MyStringId.NullOrEmpty;
        }

        private bool DetermineVisualState(bool reInforce)
        {
            var viewCheck = _count == 0 || _count == 19 || _count == 39;
            if (viewCheck) _viewInShield = CustomCollision.PointInShield(MyAPIGateway.Session.Camera.WorldMatrix.Translation, DetectMatrixOutsideInv);

            if (reInforce)
                _hideShield = false;
            else if (viewCheck && _hideColor && !_supressedColor && _viewInShield)
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

            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(MyCube.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            var drawIcon = !enemy && DsSet.Settings.SendToHud && !config.MinimalHud && Session.Instance.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible;
            if (drawIcon) UpdateIcon();

            var clearView = !GridIsMobile || !_viewInShield;
            var activeInvisible = DsSet.Settings.ActiveInvisible;
            var activeVisible = !reInforce && ((!activeInvisible && clearView) || enemy);

            var visible = !reInforce ? DsSet.Settings.Visible : 1;
            CalcualteVisibility(visible, activeVisible);

            return activeVisible;
        }

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

            MyParticlesManager.TryCreateParticleEffect(6667, out _effect, ref matrix, ref pos, _shieldEntRendId, true); 
            if (_effect == null) return;
            var playerDist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            if (playerDist < 15) playerDist = 20;
            var radius = playerDist * 0.15d;
            var scale = (playerDist + (playerDist * 0.001)) / playerDist * 0.03;
            if (ImpactSize < 150)
            {
                scale = scale * 0.3;
                radius = radius * 9;
            }
            else if (ImpactSize > 12000) scale = 0.1;
            else if (ImpactSize > 3600) scale = scale * (ImpactSize / 3600);
            if (scale > 0.1) scale = 0.1;

            _effect.UserRadiusMultiplier = (float)radius;
            _effect.UserEmitterScale = (float)scale;
            _effect.Velocity = MyGrid.Physics.LinearVelocity;
            _effect.Play();
        }

        private void EmpParticleStart()
        {
            _effect.Stop();
            var pos = EmpDetonation;
            var matrix = MatrixD.CreateTranslation(pos);

            MyParticlesManager.TryCreateParticleEffect(6667, out _effect, ref matrix, ref pos, _shieldEntRendId, true); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (_effect == null) return;
            var playerDist = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos);
            var radius = playerDist * 0.15d;
            var scale = (playerDist + (playerDist * 0.001)) / playerDist * 0.03 * (EmpSize * 0.0008);
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

        private void CalcualteVisibility(long visible, bool activeVisible)
        {
            if (visible != 2)
                HitCoolDown = -11;
            else if (visible == 2 && WorldImpactPosition != Vector3D.NegativeInfinity)
                HitCoolDown = -10;
            else if (visible == 2 && HitCoolDown > -11)
                HitCoolDown++;

            if (HitCoolDown > 59) HitCoolDown = -11;

            // ifChecks: #1 FadeReset - #2 PassiveFade - #3 PassiveSet - #4 PassiveReset
            if (visible == 2 && !(visible != 0 && HitCoolDown > -1) && HitCoolDown != -11) 
            {
                ResetShellRender(false);
            }
            else if (visible != 0 && HitCoolDown > -1) 
            {
                ResetShellRender(true);
            }
            else if (visible != 0 && HitCoolDown == -11 && !_hideShield)
            {
                _hideShield = true;
                ResetShellRender(false, false);
            }
            else if ((visible == 0 || (!activeVisible && HitCoolDown == -10)) && _hideShield)
            {
                _hideShield = false;
                ResetShellRender(false);
            }
        }

        private void ResetShellRender(bool fade, bool updates = true)
        {
            _shellPassive.Render.UpdateRenderObject(false);
            _shellPassive.Render.Transparency = fade ? (HitCoolDown + 1) * 0.0166666666667f : 0f;
            if (updates) _shellPassive.Render.UpdateRenderObject(true);
        }

        private void HudCheck()
        {
            var playerEnt = MyAPIGateway.Session.ControlledObject?.Entity as MyEntity;
            if (playerEnt?.Parent != null) playerEnt = playerEnt.Parent;
            if (playerEnt == null || (DsState.State.Online && !CustomCollision.PointInShield(playerEnt.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv)) || (!DsState.State.Online && !CustomCollision.PointInShield(playerEnt.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv)))
            {
                if (Session.Instance.HudComp != this) return;
                ProtectCache protectedEnt = null;
                if (playerEnt != null) ProtectedEntCache.TryGetValue(playerEnt, out protectedEnt);
                if (protectedEnt != null && protectedEnt.Relation != Ent.Protected) return;

                Session.Instance.HudComp = null;
                Session.Instance.HudShieldDist = double.MaxValue;
                return;
            }

            var distFromShield = Vector3D.DistanceSquared(playerEnt.PositionComp.WorldVolume.Center, DetectionCenter);
            if (Session.Instance.HudComp != this && distFromShield <= Session.Instance.HudShieldDist)
            {
                Session.Instance.HudShieldDist = distFromShield;
                Session.Instance.HudComp = this;
            }
        }

        private void UpdateIcon()
        {
            // Moving average of the average of the two values, then moving average if the difference from the average.
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
            scale = 0.08 * scale;

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

            var maxHps = GridMaxPower - (consumptionRate * 0.05f);
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
    }
}
