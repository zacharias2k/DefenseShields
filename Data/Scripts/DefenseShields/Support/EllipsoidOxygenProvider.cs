using DefenseShields.Support;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    class EllipsoidOxygenProvider : IMyOxygenProvider
    {
        private MatrixD _detectMatrixOutsideInv;

        public EllipsoidOxygenProvider(MatrixD matrix)
        {
            _detectMatrixOutsideInv = matrix;
        }

        public void UpdateMatrix(MatrixD matrix)
        {
            _detectMatrixOutsideInv = matrix;
        }

        public float GetOxygenForPosition(Vector3D worldPoint)
        {
            var inShield = CustomCollision.PointInShield(worldPoint, _detectMatrixOutsideInv);
            return inShield ? 1f : 0f;
        }

        public bool IsPositionInRange(Vector3D worldPoint)
        {
            return CustomCollision.PointInShield(worldPoint, _detectMatrixOutsideInv);
        }
    }
}
