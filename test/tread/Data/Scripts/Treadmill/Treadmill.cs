using System.Collections.Generic;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender.Import;
using VRage.Game.ModAPI;

namespace Eikester.Treadmill
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Cockpit), false,
        new string[]
        {
            "Eikester_Treadmill",
            "Eikester_Treadmill_SB"
        }
    )]
    public class Treadmill : MyGameLogicComponent
    {
        IMyCockpit cockpit;
        static MyDefinitionId defId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        float maxOutput = 2f;
        
        public float treadmillCounter = 0f;
        public float treadmillUVOffset = 0.0125f;

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            
            cockpit = (IMyCockpit)Entity;
            AddResourceSourceComponent();
        }
        
        public override void UpdateAfterSimulation()
        {
            try
			{
				var source = cockpit.Components.Get<MyResourceSourceComponent>();
				if (source != null)
				{
					source.SetRemainingCapacityByType(defId, IsControlled ? maxOutput : 0f);
				}

				UpdateTreadmill();
			}
			catch
			{
				
			}
        }

        bool IsControlled
        {
            get
            {
                return cockpit.IsUnderControl && cockpit.IsFunctional && cockpit.IsWorking;
            }
        }

        void UpdateTreadmill()
        {
            if (!IsControlled)
                return;

            MatrixD matrix = cockpit.WorldMatrix;
            matrix *= MatrixD.CreateFromAxisAngle(matrix.Right, MathHelper.ToRadians(5));

            treadmillCounter += treadmillUVOffset;

            if (treadmillCounter > 1)
                treadmillCounter = 0;

            Vector2 offset = new Vector2(treadmillCounter, 0);

            MyTransparentGeometry.AddBillboardOriented(
                MyStringId.GetOrCompute("Treadmill"),
                Color.White.ToVector4(),
                cockpit.GetPosition() + (cockpit.WorldMatrix.Up * -0.948f) + (cockpit.WorldMatrix.Forward * -0.2f),
				matrix.Backward,
                matrix.Right,
                0.6f,
				0.25f,
                Vector2.Zero + offset);
        }

        public void AddResourceSourceComponent()
        {
            MyResourceSourceComponent source = new MyResourceSourceComponent();
            MyResourceSourceInfo info = new MyResourceSourceInfo();
            info.ResourceTypeId = defId;
            info.DefinedOutput = maxOutput;
            source.Init(MyStringHash.GetOrCompute("Battery"), info);
            source.Enabled = true;
            source.SetMaxOutput(maxOutput);

            cockpit.Components.Add(source);
        }
    }
}
