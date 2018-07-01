using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    class EllipsoidOxygenProvider : IMyOxygenProvider
    {
        private MatrixD _detectMatrixOutsideInv;
        private double _o2Level;

        public EllipsoidOxygenProvider(MatrixD matrix)
        {
            _detectMatrixOutsideInv = matrix;
        }

        public void UpdateOxygenProvider(MatrixD matrix, double o2Level)
        {
            _detectMatrixOutsideInv = matrix;
            _o2Level = o2Level;
        }

        public float GetOxygenForPosition(Vector3D worldPoint)
        {
            var inShield = CustomCollision.PointInShield(worldPoint, _detectMatrixOutsideInv);
            if (inShield)
            {
                return (float)_o2Level;
            }
            return 0f;
        }

        public bool IsPositionInRange(Vector3D worldPoint)
        {
            return CustomCollision.PointInShield(worldPoint, _detectMatrixOutsideInv);
        }
    }
}
