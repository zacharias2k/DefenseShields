using VRage.ModAPI;
using VRage.Voxels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseShields
{


    public class Voxels 
    {
        public bool DisplayMessage;
        public DateTime StartTime;
        public string Message;
        public string AsteroidName;

        /*This command is used to generate a sphere asteroid at the exact center of the specified co-ordinates, with multiple layers.
/createroidsphere <Name> <X> <Y> <Z> <Parts> <Material1> <Diameter1> <Material2> <Diameter2> <Material3> <Diameter3> ....
  <Name> - the base name of the asteroid file. A number will be added if it already exists.
  <X> <Y> <Z> - the center coordinate of where to place the asteroid.
  <Parts> - specify to break the sphere down into smaller chunks. Either 1=whole sphere, 2=hemispheres, 4 or 8 parts.
  <Material> - the material of the layer. An empty layer can be specified with 'none'. The following materials are available: {0}
  <Diameter> - the diameter of the layer.
  ... - Additional material and diameters can be specified for additional layers.
Note:
The larger the asteroid, the longer it will take to generate. More than 2000m can as much as an hour on some computers.
The flat faces on the inside of the multi part asteroids will seem to become invisible at a distance.
Examples:
  /createroidsphere sphere_solid_stone 1000 1000 1000 1 Stone_01 100
  /createroidsphere sphere_hollow_stone 2000 2000 2000 8 Stone_01 200 none 180
  /createroidsphere sphere_3_tricky_layers 3000 3000 3000 2 Stone_01 200 none 180 Stone_01 160 none 140 Stone_01 120 none 100 
  /createroidsphere sphere_layers 8000 8000 8000 2 Stone_01 200 Iron_01 180 Nickel_01 100 Cobalt_01 90 Magnesium_01 80 Silicon_01 70 
", materialNames);
                MyAPIGateway.Utilities.ShowMissionScreen("Create Asteroid Sphere:", null, " ", description.ToString(), null, "OK");
            }


            // As Asteroid volumes are cubic octrees, they are sized in 64, 128, 256, 512, 1024, 2048

            /*
            Sample calls...
            /createroidsphere test 200 200 200 1 Gold_01 200
            
            /createroidsphere sphere_solid_xx_stone_01a 2000 2000 2000 2 Stone_01 200 none 100
            /createroidsphere sphere_solid_xx_stone_01a 2000 2000 2000 2 Stone_01 200 Iron_01 180 Nickel_01 100 Cobalt_01 90 Magnesium_01 80 Silicon_01 70 Silver_01 60 Gold_01 50 Platinum_01 40 Uraninite_01 30
            /createroidsphere sphere_solid_xx_stone_01a 2000 2000 2000 4 Stone_01 200 Iron_02 190 Nickel_01 145 Cobalt_01 130 Magnesium_01 115 Silicon_01 100 Silver_01 85 Gold_01 70 Platinum_01 55 Uraninite_01 40
             
            344m, 38s.
            This call takes 58 seconds, for a 344 diameter sphere, with no freeze.
            /createroidsphere 200 200 200 344 0 1 Nickel_01 test 
             
            http://steamcommunity.com/sharedfiles/filedetails/?id=399791753
             
            This call takes, with a frozen game, and wont work if floating items occupy space.
            344m, 13s.
            400m, 20s.
            500m, 40s.
            600m, 1:06s.
            1000m, 4:53s.
            2000m, 40-50min.
            2700m, 1:36:50s.
        }
        */

        #region Voxel

        public static string CreateUniqueStorageName(string baseName)
        {
            long index = 0;
            var match = Regex.Match(baseName, @"^(?<Key>.+?)(?<Value>(\d+?))$", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                baseName = match.Groups["Key"].Captures[0].Value;
                long.TryParse(match.Groups["Value"].Captures[0].Value, out index);
            }

            var uniqueName = string.Format("{0}{1}", baseName, index);
            var currentAsteroidList = new List<IMyVoxelBase>();
            MyAPIGateway.Session.VoxelMaps.GetInstances(currentAsteroidList, v => v != null);

            while (currentAsteroidList.Any(a => a.StorageName.Equals(uniqueName, StringComparison.InvariantCultureIgnoreCase)))
            {
                index++;
                uniqueName = string.Format("{0}{1}", baseName, index);
            }

            return uniqueName;
        }

        public static IMyVoxelMap CreateNewAsteroid(string storageName, Vector3I size, Vector3D position)
        {
            //var cache = new MyStorageData();

            // new storage is created completely full
            // no geometry will be created because that requires full-empty transition
            var storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(size);

            // midspace's Note: The following steps appear redundant, as the storage space is created empty.
            /*
            // always ensure cache is large enough for whatever you plan to load into it
            cache.Resize(size);
            // range is specified using inclusive min and max coordinates
            // Choose a reasonable size of range you plan to work with, to avoid high memory usage
            // memory size in bytes required by cache is computed as Size.X * Size.Y * Size.Z * 2, where Size is size of the range.
            // min and max coordinates are inclusive, so if you want to read 8^3 voxels starting at coordinate [8,8,8], you
            // should pass in min = [8,8,8], max = [15,15,15]
            // For LOD, you should only use LOD0 or LOD1
            // When you write data inside cache back to storage, you always write to LOD0 (the most detailed LOD), LOD1 can only be read from.
            storage.ReadRange(cache, MyStorageDataTypeFlags.All, 0, Vector3I.Zero, size - 1);
            // resets all loaded content to empty
            cache.ClearContent(0);
            // write new data back to the storage
            storage.WriteRange(cache, MyStorageDataTypeFlags.Content, Vector3I.Zero, size - 1);
            */

            return MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(storageName, storage, position, 0);
        }
        #endregion

        /*
public static class Test
{
    public static double RoundUpToCube(this double value)
    {
        int baseVal = 1;
        while (baseVal < value)
            baseVal = baseVal * 2;
        return baseVal;
    }
}
*/
        public static string ProcessAsteroid(string asteroidName, Vector3I size, Vector3D position, Vector3D offset, Vector3I origin, List<AsteroidSphereLayer> layers)
        {
            var storeName = CreateUniqueStorageName(asteroidName);
            var storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(size);
            var voxelMap = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(storeName, storage, position - (Vector3D)origin - offset, 0);
            bool isEmpty = true;

            foreach (var layer in layers)
            {
                var radius = (float)(layer.Diameter - 2) / 2f;
                IMyVoxelShapeSphere sphereShape = MyAPIGateway.Session.VoxelMaps.GetSphereVoxelHand();
                sphereShape.Center = position;
                sphereShape.Radius = radius;
                if (layer.Material == 255)
                {
                    MyAPIGateway.Session.VoxelMaps.CutOutShape(voxelMap, sphereShape);
                    isEmpty = true;
                }
                else if (isEmpty)
                {
                    MyAPIGateway.Session.VoxelMaps.FillInShape(voxelMap, sphereShape, layer.Material);
                    isEmpty = false;
                }
                else
                {
                    MyAPIGateway.Session.VoxelMaps.PaintInShape(voxelMap, sphereShape, layer.Material);
                }
            }

            return storeName;
        }

        public class AsteroidSphereLayer
        {
            public int Index { get; set; }
            public string MaterialName { get; set; }
            public byte Material { get; set; }
            public double Diameter { get; set; }
        }
    }
}