using System;
using DefenseSystems.Support;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRageRender;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;


namespace DefenseSystems
{
    internal partial class Fields
    {
        public void Draw(int onCount, bool sphereOnCamera)
        {
            _onCount = onCount;
            var a = Bus.ActiveController;

            var renderId = Bus.Spine.Render.GetRenderObjectID();
            var percent = a.State.Value.ShieldPercent;
            var notBubble = a.State.Value.ProtectMode > 0;
            var hitAnim = !notBubble && a.Set.Value.HitWaveAnimation;
            var refreshAnim = !notBubble && a.Set.Value.RefreshAnimation;

            Vector3D impactPos;
            lock (HandlerImpact) impactPos = HandlerImpact.Active ? ComputeHandlerImpact() : WorldImpactPosition;
            var intersected = WorldImpactPosition != Vector3D.NegativeInfinity && impactPos != Vector3D.Zero;

            WorldImpactPosition = impactPos;
            var activeVisible = DetermineVisualState(notBubble);
            WorldImpactPosition = Vector3D.NegativeInfinity;

            var kineticHit = !EnergyHit;
            _localImpactPosition = Vector3D.NegativeInfinity;

            if (impactPos != Vector3D.NegativeInfinity && (kineticHit && KineticCoolDown < 0 || EnergyHit && EnergyCoolDown < 0))
            {
                if (_isServer && WebDamage && ShieldIsMobile)
                {
                    Vector3 pointVel;
                    var gridCenter = Bus.Spine.PositionComp.WorldAABB.Center;
                    Bus.Spine.Physics.GetVelocityAtPointLocal(ref gridCenter, out pointVel);
                    impactPos += (Vector3D)pointVel * Session.TwoStep;
                }

                if (kineticHit) KineticCoolDown = 0;
                else if (EnergyHit) EnergyCoolDown = 0;

                HitParticleStart(impactPos, intersected);

                var cubeBlockLocalMatrix = Bus.Spine.PositionComp.LocalMatrix;
                var referenceWorldPosition = cubeBlockLocalMatrix.Translation;
                var worldDirection = impactPos - referenceWorldPosition;
                var localPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlockLocalMatrix));
                _localImpactPosition = localPosition;
            }

            kineticHit = false;
            EnergyHit = false;

            if (a.State.Value.Online)
            {
                var prevlod = _prevLod;
                var lod = CalculateLod(_onCount);
                if (_shapeChanged || UpdateRender || lod != prevlod)
                {
                    UpdateRender = false;
                    _shapeChanged = false;

                    Icosphere.CalculateTransform(ShieldShapeMatrix, lod);
                    if (!ShieldIsMobile) Icosphere.ReturnPhysicsVerts(DetectionMatrix, PhysicsOutside);
                }
                Icosphere.ComputeEffects(ShieldShapeMatrix, _localImpactPosition, ShellPassive, ShellActive, prevlod, percent, activeVisible, refreshAnim);
            }
            else if (_shapeChanged) UpdateRender = true;

            if (hitAnim && sphereOnCamera && a.State.Value.Online) Icosphere.Draw(renderId);
        }

        public void DrawShieldDownIcon()
        {
            var set = Bus.ActiveController.Set;
            if (Bus.Tick % 60 != 0) HudCheck();
            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(Bus.Spine.EntityId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            if (!enemy && set.Value.SendToHud && !config.MinimalHud && Session.Instance.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible) UpdateIcon();
        }

        private Vector3D ComputeHandlerImpact()
        {
            WebDamage = false;
            HandlerImpact.Active = false;
            if (HandlerImpact.HitBlock == null) return Bus.Spine.PositionComp.WorldAABB.Center;

            Vector3D originHit;
            HandlerImpact.HitBlock.ComputeWorldCenter(out originHit);

            var line = new LineD(HandlerImpact.Attacker.PositionComp.WorldAABB.Center, originHit);

            var testDir = Vector3D.Normalize(line.From - line.To);
            var ray = new RayD(line.From, -testDir);
            var matrix = ShieldShapeMatrix * Bus.Spine.WorldMatrix;
            var intersectDist = CustomCollision.IntersectEllipsoid(MatrixD.Invert(matrix), matrix, ray);
            var ellipsoid = intersectDist ?? line.Length;
            var shieldHitPos = line.From + (testDir * -ellipsoid);
            return shieldHitPos;
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
            var slot = (int)Math.Floor(fState * 10);

            if (slot < 0) slot = (slot * -1) + 10;

            return Session.Instance.HudHealthHpIcons[slot];
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

        private bool DetermineVisualState(bool notBubble)
        {
            var a = Bus.ActiveController;
            var set = a.Set;
            if (Bus.Tick60 || Session.Instance.HudIconReset) HudCheck();

            if (Bus.Tick20) _viewInShield = CustomCollision.PointInShield(MyAPIGateway.Session.Camera.WorldMatrix.Translation, DetectMatrixOutsideInv);
            if (notBubble)
                _hideShield = false;
            else if (Bus.Tick20 && _hideColor && !_supressedColor && _viewInShield)
            {
                _modelPassive = ModelLowReflective;
                UpdatePassiveModel();
                _supressedColor = true;
                _hideShield = false;
            }
            else if (Bus.Tick20 && _supressedColor && _hideColor && !_viewInShield)
            {
                SelectPassiveShell();
                UpdatePassiveModel();
                _supressedColor = false;
                _hideShield = false;
            }

            var enemy = false;
            var relation = MyAPIGateway.Session.Player.GetRelationTo(a.MyCube.OwnerId);
            if (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies) enemy = true;

            var config = MyAPIGateway.Session.Config;
            var drawIcon = !enemy && set.Value.SendToHud && !config.MinimalHud && Session.Instance.HudComp == this && !MyAPIGateway.Gui.IsCursorVisible;
            if (drawIcon) UpdateIcon();

            var clearView = !ShieldIsMobile || !_viewInShield;
            var activeInvisible = set.Value.ActiveInvisible;
            var activeVisible = !notBubble && ((!activeInvisible && clearView) || enemy);

            var visible = !notBubble ? set.Value.Visible : 1;

            CalcualteVisibility(visible, activeVisible);

            return activeVisible;
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
            ShellPassive.Render.UpdateRenderObject(false);
            ShellPassive.Render.Transparency = fade ? (HitCoolDown + 1) * 0.0166666666667f : 0f;
            if (updates) ShellPassive.Render.UpdateRenderObject(true);
        }

        internal void ShellVisibility(bool forceInvisible = false)
        {
            var a = Bus.ActiveController;
            var set = a.Set;
            var state = a.State;

            if (forceInvisible)
            {
                ShellPassive.Render.UpdateRenderObject(false);
                ShellActive.Render.UpdateRenderObject(false);
                return;
            }

            if (state.Value.Online && !state.Value.Lowered && !state.Value.Sleeping)
            {
                if (set.Value.Visible == 0) ShellPassive.Render.UpdateRenderObject(true);
                ShellActive.Render.UpdateRenderObject(true);
                ShellActive.Render.UpdateRenderObject(false);
            }
        }

        private int CalculateLod(int onCount)
        {
            var lod = 4;

            if (onCount > 9) lod = 2;
            else if (onCount > 3) lod = 3;

            _prevLod = lod;
            return lod;
        }

        private void HitParticleStart(Vector3D pos, bool intersected)
        {
            var a = Bus.ActiveController;
            var set = a.Set;

            var scale = 0.0075;
            var logOfPlayerDist = Math.Log(Vector3D.Distance(MyAPIGateway.Session.Camera.Position, pos));
            int radius;
            var size = ImpactSize <= 7500 ? ImpactSize : 7500;
            var baseScaler = size / 30;
            scale = scale * Math.Max(Math.Log(baseScaler), 1);

            var mainParticle = !intersected ? 1657 : 6667;
            var multiple = EnergyHit;

            Vector4 color;
            float mainAdjust = 1;
            if (EnergyHit)
            {
                var scaler = 8;
                if (_viewInShield && set.Value.DimShieldHits)
                {
                    multiple = false;
                    scaler = 3;
                }
                else mainAdjust = 0.4f;
                radius = (int)(logOfPlayerDist * scaler);
                color = new Vector4(255, 10, 0, 1f);
            }
            else
            {
                var scaler = 8;
                if (_viewInShield && set.Value.DimShieldHits)
                {
                    scaler = 3;
                }
                radius = (int)(logOfPlayerDist * scaler);
                color = new Vector4(255, 255, 255, 0.01f);
            }
            var vel = Bus.Spine.Physics.LinearVelocity;
            var matrix = MatrixD.CreateTranslation(pos);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, out _effect1, ref matrix, ref pos, ShieldEntRendId, true);
            if (_effect1 == null) return;
            var directedMatrix = _effect1.WorldMatrix;
            var shieldCenter = ShieldEnt.PositionComp.WorldAABB.Center;
            directedMatrix.Forward = Vector3D.Normalize(MyAPIGateway.Session.Camera.Position - shieldCenter);
            directedMatrix.Left = Vector3D.CalculatePerpendicularVector(directedMatrix.Forward);
            directedMatrix.Up = Vector3D.Cross(directedMatrix.Forward, directedMatrix.Left);

            _effect1.UserColorMultiplier = color;
            _effect1.UserRadiusMultiplier = radius * mainAdjust;
            _effect1.UserEmitterScale = (float)scale;
            _effect1.Velocity = vel;
            if (!EnergyHit) _effect1.WorldMatrix = directedMatrix;
            _effect1.Play();

            var magic = ((radius * 0.1f) - 2.5f);
            if (multiple)
            {
                MyParticlesManager.TryCreateParticleEffect(1657, out _effect2, ref matrix, ref pos, ShieldEntRendId, true);
                if (_effect2 == null) return;
                _effect2.UserColorMultiplier = color;
                _effect2.UserRadiusMultiplier = 2f + magic;
                _effect2.UserEmitterScale = 1f;
                _effect2.Velocity = vel;
                _effect2.WorldMatrix = directedMatrix;
                _effect2.Play();
            }
        }

        public void HudCheck()
        {
            var playerEnt = MyAPIGateway.Session.ControlledObject?.Entity as MyEntity;
            if (playerEnt?.Parent != null) playerEnt = playerEnt.Parent;
            if (playerEnt == null)
            {
                Session.Instance.HudIconReset = true;
                Session.Instance.HudComp = null;
                Session.Instance.HudShieldDist = double.MaxValue;
                return;
            }

            var playerPos = playerEnt.PositionComp.WorldAABB.Center;
            var lastOwner = Session.Instance.HudComp;

            if (!CustomCollision.PointInShield(playerPos, DetectMatrixOutsideInv))
            {
                if (Session.Instance.HudComp != this) return;

                Session.Instance.HudIconReset = true;
                Session.Instance.HudComp = null;
                Session.Instance.HudShieldDist = double.MaxValue;
                return;
            }

            var distFromShield = Vector3D.DistanceSquared(playerPos, DetectionCenter);

            var takeOverHud = lastOwner == null || lastOwner != this && distFromShield <= Session.Instance.HudShieldDist;
            var lastIsStale = !takeOverHud && lastOwner != this && !CustomCollision.PointInShield(playerPos, lastOwner.DetectMatrixOutsideInv);
            if (takeOverHud || lastIsStale)
            {
                Session.Instance.HudShieldDist = distFromShield;
                Session.Instance.HudComp = this;
                Session.Instance.HudIconReset = true;
            }
        }

        private void UpdateIcon()
        {
            var state = Bus.ActiveController.State;
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

            var percent = state.Value.ShieldPercent;
            var icon2FSelect = percent < 99 ? GetIconMeterfloat(state) : 0;
            var heat = state.Value.Heat;

            var icon1 = GetHudIcon1FromFloat(percent);
            var icon2 = GetHudIcon2FromFloat(icon2FSelect);
            var icon3 = GetHudIcon3FromInt(heat, Bus.Tick180);
            var showIcon2 = state.Value.Online;
            Color color;
            if (percent > 0 && percent < 10 && Bus.Count < 30) color = Color.Red;
            else color = Color.White;
            MyTransparentGeometry.AddBillboardOriented(icon1, color, origin, left, up, (float)scale, BlendTypeEnum.LDR);
            if (showIcon2 && icon2 != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(icon2, Color.White, origin, left, up, (float)scale * 1.11f, BlendTypeEnum.LDR);
            if (icon3 != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(icon3, Color.White, origin, left, up, (float)scale * 1.11f, BlendTypeEnum.LDR);
        }

        private float GetIconMeterfloat(ControllerState state)
        {
            if (_shieldPeakRate <= 0) return 0;
            var dps = _runningDamage;
            var hps = _runningHeal;
            var reduction = _expChargeReduction > 0 ? _shieldPeakRate / _expChargeReduction : _shieldPeakRate;
            if (hps > 0 && dps <= 0) return reduction / _shieldPeakRate;
            if (state.Value.ShieldPercent > 99 || (hps <= 0 && dps <= 0)) return 0;
            if (hps <= 0) return 0.0999f;

            if (hps > dps)
            {
                var healing = MathHelper.Clamp(dps / hps, 0, reduction / _shieldPeakRate);
                return healing;
            }
            var damage = hps / dps;
            return -damage;
        }
    }
}
