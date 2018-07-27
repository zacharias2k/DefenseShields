using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields.Support
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), false)]
    public class RadarGridComponent : MyGameLogicComponent
    {
        private static Random _random = new Random();
        private MyResourceDistributorComponent _distributor;
        private MyCubeGrid _grid;
        public override void OnAddedToContainer()
        {
            _grid = Entity as MyCubeGrid;
            if (_grid == null)
                return;

            base.OnAddedToContainer();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (!_grid.CubeBlocks.Any())
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                return;
            }
            var ob = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_Cockpit>(OB);
            ob.EntityId = _random.Next(int.MinValue, int.MaxValue);
            ob.Min = Vector3I.MinValue;
            var blk = ((IMyCubeGrid)_grid).AddBlock(ob, false);
            _distributor = (blk.FatBlock as MyShipController)?.GridResourceDistributor;
            //((IMyCubeGrid)_grid).RazeBlock(blk.Position);
            _grid.RazeBlocksClient(new List<Vector3I>() { blk.Position });
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            var g = Entity as MyCubeGrid;
            if (g == null)
                return;

            MyAPIGateway.Utilities.ShowMessage(g.DisplayName, _distributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId).ToString() ?? "null");
        }

        private const string OB = @"<?xml version=""1.0"" encoding=""utf-16""?>
<MyObjectBuilder_Cockpit xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <SubtypeName>LargeBlockCockpit</SubtypeName>
  <Owner>0</Owner>
  <CustomName>Control Stations</CustomName>
</MyObjectBuilder_Cockpit> ";
    }
}
