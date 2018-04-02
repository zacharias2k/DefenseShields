using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    class GetBlocks
    {
        private readonly HashSet<IMySlimBlock> m_cubeBlocks = new HashSet<IMySlimBlock>();
        private readonly Dictionary<Vector3I, MyCube> m_cubes = new Dictionary<Vector3I, MyCube>(1024);

        //[ThreadStatic]
        //private static HashSet<IMySlimBlock> m_tmpQuerySlimBlocks;
        private HashSet<IMySlimBlock> m_tmpQuerySlimBlocks;
        public void GetBlocksIntersectingOBB(MyCubeGrid myCubeGrid, BoundingBoxD box, MatrixD boxTransform, HashSet<IMySlimBlock> blocks)
        {
            var PositionComp = myCubeGrid.PositionComp;
            var GridSizeR = myCubeGrid.GridSizeR;
            var Min = myCubeGrid.Min;
            var Max = myCubeGrid.Max;




            if (blocks == null)
            {
                //Debug.Fail("null blocks ! probably dead entity ?");
                return;
            }

            if (PositionComp == null)
            {
                return;
            }

            var obbWorld = MyOrientedBoundingBoxD.Create(box, boxTransform);
            var gridWorldAabb = PositionComp.WorldAABB;
            if (obbWorld.Contains(ref gridWorldAabb) == ContainmentType.Contains)
            {
                var test = new HashSet<IMySlimBlock>(myCubeGrid.GetBlocks());

                foreach (var slimBlock in test)
                {
                    if (slimBlock.FatBlock != null)
                    {
                        //Debug.Assert(!slimBlock.FatBlock.Closed);
                        if (slimBlock.FatBlock.Closed) //investigate why there is closed block in the grid/m_fatblock list
                            continue; //it is possible to have marked for close block there but not closed
                    }

                    blocks.Add(slimBlock);
                }

                return;
            }

            var compositeTransform = boxTransform * PositionComp.WorldMatrixNormalizedInv;
            var obb = MyOrientedBoundingBoxD.Create(box, compositeTransform);
            obb.Center *= GridSizeR;
            obb.HalfExtent *= GridSizeR;
            box = box.TransformFast(compositeTransform);
            Vector3D min = box.Min;
            Vector3D max = box.Max;
            Vector3I start = new Vector3I((int)Math.Round(min.X * GridSizeR), (int)Math.Round(min.Y * GridSizeR), (int)Math.Round(min.Z * GridSizeR));
            Vector3I end = new Vector3I((int)Math.Round(max.X * GridSizeR), (int)Math.Round(max.Y * GridSizeR), (int)Math.Round(max.Z * GridSizeR));

            Vector3I startIt = Vector3I.Min(start, end);
            Vector3I endIt = Vector3I.Max(start, end);

            startIt = Vector3I.Max(startIt, Min);
            endIt = Vector3I.Min(endIt, Max);
            if (startIt.X > endIt.X || startIt.Y > endIt.Y || startIt.Z > endIt.Z)
                return;
            Vector3 halfGridSize = new Vector3(0.5f);
            BoundingBoxD blockBB = new BoundingBoxD();

            if ((endIt - startIt).Size > m_cubeBlocks.Count)
            {
                //var grid = myCubeGrid as IMyCubeGrid;
                //var blockSet = new HashSet<IMySlimBlock>();
                //var test = new HashSet<IMySlimBlock>(myCubeGrid.GetBlocks());
                var test = myCubeGrid.GetBlocks().Cast<IMySlimBlock>();
                foreach (var slimBlock in test)
                {
                    var def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;
                    Matrix lm;
                    slimBlock.Orientation.GetMatrix(out lm);
                    var localBb = new BoundingBoxD(-def.Center, def.Size - def.Center);
                    var blockMin = localBb.Min;

                    //Debug.Assert(!slimBlock.FatBlock.Closed);
                    //if (slimBlock.FatBlock.Closed) //TODO:investigate why there is closed block in the grid/m_fatblock list
                        //continue; //it is possible to have marked for close block there but not closed
                    //Log.Line($"test");
                    //blockBB.Min = slimBlock.Min - halfGridSize;
                    blockBB.Min = blockMin - halfGridSize;
                    blockBB.Max = slimBlock.Max + halfGridSize;
                    if (obb.Intersects(ref blockBB))
                    {
                        blocks.Add(slimBlock);
                    }
                }
                return;
            }

            MyCube block;
            if (m_tmpQuerySlimBlocks == null)
                m_tmpQuerySlimBlocks = new HashSet<IMySlimBlock>();
            Vector3I_RangeIterator it = new Vector3I_RangeIterator(ref startIt, ref endIt);
            var pos = it.Current;
            for (; it.IsValid(); it.GetNext(out pos))
            {
                //System.Diagnostics.Debug.Assert(m_cubes != null, "m_cubes on MyCubeGrid are null!");
                if (m_cubes != null && m_cubes.TryGetValue(pos, out block) && block.CubeBlock != null)
                {
                    var slimBlock = (IMySlimBlock)block.CubeBlock;
                    if (m_tmpQuerySlimBlocks.Contains(slimBlock))
                        continue;

                    var def = (MyCubeBlockDefinition)slimBlock.BlockDefinition;
                    Matrix lm;
                    slimBlock.Orientation.GetMatrix(out lm);
                    var localBb = new BoundingBoxD(-def.Center, def.Size - def.Center);
                    var blockMin = localBb.Min;

                    //blockBB.Min = slimBlock.GetWorldBoundingBox() - halfGridSize;
                    blockBB.Min = blockMin - halfGridSize;
                    blockBB.Max = slimBlock.Max + halfGridSize;
                    if (obb.Intersects(ref blockBB))
                    {
                        m_tmpQuerySlimBlocks.Add(slimBlock);
                        blocks.Add(slimBlock);
                    }
                }
            }
            m_tmpQuerySlimBlocks.Clear();
        }
    }
}
