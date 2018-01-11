using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public class BlockAnimation : Station.DefenseShields
    {
        /*
        private static readonly List<MyEntitySubpart> _subpartsArms = new List<MyEntitySubpart>();
        private static readonly List<MyEntitySubpart> _subpartsReflectors = new List<MyEntitySubpart>();
        private static List<Matrix> _matrixArmsOff = new List<Matrix>();
        private static List<Matrix> _matrixArmsOn = new List<Matrix>();
        private static List<Matrix> _matrixReflectorsOff = new List<Matrix>();
        private static List<Matrix> _matrixReflectorsOn = new List<Matrix>();

        private IMyFunctionalBlock _fblock;
        private MyCubeBlock _cblock;


        public static MyEntitySubpart _subpartRotor;
        */
        #region Block Animation

        public void BlockAnimationReset()
        {
            //Log.Line($" Resetting BlockAnimation in loop {Count}");
            _subpartRotor.Subparts.Clear();
            _subpartsArms.Clear();
            _subpartsReflectors.Clear();
            BlockAnimationInit();
        }

        public void BlockAnimationInit()
        {
            try
            {
                _animStep = 0f;

                _matrixArmsOff = new List<Matrix>();
                _matrixArmsOn = new List<Matrix>();
                _matrixReflectorsOff = new List<Matrix>();
                _matrixReflectorsOn = new List<Matrix>();

                _worldMatrix = Entity.WorldMatrix;
                _worldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;

                Entity.TryGetSubpart("Rotor", out _subpartRotor);

                for (var i = 1; i < 9; i++)
                {
                    MyEntitySubpart temp1;
                    _subpartRotor.TryGetSubpart("ArmT" + i.ToString(), out temp1);
                    _matrixArmsOff.Add(temp1.PositionComp.LocalMatrix);
                    var temp2 = temp1.PositionComp.LocalMatrix.GetOrientation();
                    switch (i)
                    {
                        case 1:
                        case 5:
                            temp2 *= Matrix.CreateRotationZ(0.98f);
                            break;
                        case 2:
                        case 6:
                            temp2 *= Matrix.CreateRotationX(-0.98f);
                            break;
                        case 3:
                        case 7:
                            temp2 *= Matrix.CreateRotationZ(-0.98f);
                            break;
                        case 4:
                        case 8:
                            temp2 *= Matrix.CreateRotationX(0.98f); ;
                            break;
                    }
                    temp2.Translation = temp1.PositionComp.LocalMatrix.Translation;
                    _matrixArmsOn.Add(temp2);
                    _subpartsArms.Add(temp1);
                }

                for (var i = 0; i < 4; i++)
                {
                    MyEntitySubpart temp3;
                    _subpartsArms[i].TryGetSubpart("Reflector", out temp3);
                    _subpartsReflectors.Add(temp3);
                    _matrixReflectorsOff.Add(temp3.PositionComp.LocalMatrix);
                    var temp4 = temp3.PositionComp.LocalMatrix * Matrix.CreateFromAxisAngle(temp3.PositionComp.LocalMatrix.Forward, -(float)Math.PI / 3);
                    temp4.Translation = temp3.PositionComp.LocalMatrix.Translation;
                    _matrixReflectorsOn.Add(temp4);
                }
            }
            catch (Exception ex)
            {
                Log.Line($" Exception in BlockAnimation");
                Log.Line($"{ex}");
            }
        }
        
        public void Animate()
        {
            Log.Line($"step 1.5");
            _worldMatrix = Entity.WorldMatrix;
            Log.Line($"step 2");

            _worldMatrix.Translation += Entity.WorldMatrix.Up * 0.35f;
            Log.Line($"step 3");

            //Animations
            if (_fblock.Enabled && _fblock.IsFunctional && _cblock.IsWorking)
            {
                //Color change for on =-=-=-=-
                _subpartRotor.SetEmissiveParts("Emissive", Color.White, 1);
                _time += 1;
                Matrix temp1 = Matrix.CreateRotationY(0.1f * _time);
                temp1.Translation = _subpartRotor.PositionComp.LocalMatrix.Translation;
                _subpartRotor.PositionComp.LocalMatrix = temp1;
                Log.Line($"step 4");

                if (_animStep < 1f)
                {
                    _animStep += 0.05f;
                }
            }
            else
            {
                //Color change for off =-=-=-=-
                _subpartRotor.SetEmissiveParts("Emissive", Color.Black + new Color(15, 15, 15, 5), 0);
                if (_animStep > 0f)
                {
                    _animStep -= 0.05f;
                }
            }

            for (var i = 0; i < 8; i++)
            {
                if (i < 4)
                {
                    _subpartsReflectors[i].PositionComp.LocalMatrix =
                        Matrix.Slerp(_matrixReflectorsOff[i], _matrixReflectorsOn[i], _animStep);
                }

                _subpartsArms[i].PositionComp.LocalMatrix =
                    Matrix.Slerp(_matrixArmsOff[i], _matrixArmsOn[i], _animStep);
            }
        }

        #endregion
    }
}
