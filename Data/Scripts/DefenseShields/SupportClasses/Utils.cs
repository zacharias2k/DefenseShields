using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.Entities;
using VRageMath;

namespace DefenseShields.Support
{
    internal class Work
    {
        internal List<DefenseShields> ShieldList;
        internal uint Tick;

        internal void DoIt(List<DefenseShields> s, uint t)
        {
            ShieldList = s;
            Tick = t;
        }
    }


    public struct MyImpulseData
    {
        public MyCubeGrid MyGrid;
        public Vector3D Direction;
        public Vector3D Position;

        public MyImpulseData(MyCubeGrid myGrid, Vector3D direction, Vector3D position)
        {
            MyGrid = myGrid;
            Direction = direction;
            Position = position;
        }
    }


    class FiniteFifoQueueSet<T1, T2>
    {
        private readonly T1[] _nodes;
        private int _emptySpot;
        private readonly Dictionary<T1, T2> _backingDict;

        public FiniteFifoQueueSet(int size)
        {
            _nodes = new T1[size];
            _backingDict = new Dictionary<T1, T2>(size + 1);
            _emptySpot = 0;
        }

        public void Enqueue(T1 key, T2 value)
        {
            try
            {
                _backingDict.Remove(_nodes[0]);
                _nodes[_emptySpot] = key;
                _backingDict.Add(key, value);
                _emptySpot++;
                if (_emptySpot >= _nodes.Length)
                {
                    _emptySpot = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Enqueue: {ex}"); }
        }

        public bool Contains(T1 value)
        {
            return _backingDict.ContainsKey(value);
        }

        public bool TryGet(T1 value, out T2 hostileEnt)
        {
            return _backingDict.TryGetValue(value, out hostileEnt);
        }
    }

    /*
    public class MyApplyImpulse
    {

        public static MyApplyImpulse GetOne(ref MyImpulseData data)
        {
            var instance = Session.ImpulsePool.Get();
            instance.Data = data;
            return instance;
        }

        public readonly Action Delegate;

        public MyApplyImpulse()
        {
            Delegate = DoWorkOnMainThread;
        }

        public MyImpulseData Data;

        private void DoWorkOnMainThread()
        {
            if (Data.MyGrid == null) return;
            Log.Line($"Doimpluse");
            Data.MyGrid.Physics.ApplyImpulse(Data.Direction, Data.Position);
            Session.ImpulsePool.Return(this);
        }
    }
    */
    public struct MyAddForceData
    {
        public MyCubeGrid MyGrid;
        public Vector3D Force;
        public float? MaxSpeed;
        public bool Immediate;

        public MyAddForceData(MyCubeGrid myGrid, Vector3D force, float? maxSpeed, bool immediate)
        {
            MyGrid = myGrid;
            Force = force;
            MaxSpeed = maxSpeed;
            Immediate = immediate;
        }
    }
    /*
    public class MyAddForce
    {
        public static MyAddForce GetOne(ref MyAddForceData data)
        {
            var instance = Session.ForcePool.Get();
            instance.Data = data;
            return instance;
        }

        public readonly Action Delegate;

        public MyAddForce()
        {
            Delegate = DoWorkOnMainThread;
        }

        public MyAddForceData Data;

        private void DoWorkOnMainThread()
        {
            if (Data.MyGrid == null) return;
            Log.Line($"DoForce");
            Data.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, Data.Force, null, Vector3D.Zero, Data.MaxSpeed, Data.Immediate);
            Session.ForcePool.Return(this);
        }
    }
    */
    public class DSUtils
    {
        public static float Mod(int x, int m)
        {
            return (x % m + m) % m;
        }

        public Stopwatch Sw { get; } = new Stopwatch();
        public double Last;
        public void StopWatchReport(string message, float log)
        {
            Sw.Stop();
            long ticks = Sw.ElapsedTicks;
            double ns = 1000000000.0 * ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;
            if (log <= -1) Log.Line($"{message} ms:{(float)ms} last-ms:{(float)Last} s:{(int)s}");
            else
            {
                if (ms >= log) Log.Line($"{message} ms:{(float)ms} last-ms:{(float)Last} s:{(int)s}");
            }
            Last = ms;
            Sw.Reset();
        }

        public static BoundingSphereD CreateFromPointsList(List<Vector3D> points)
        {
            Vector3D current;
            Vector3D Vector3D_1 = current = points[0];
            Vector3D Vector3D_2 = current;
            Vector3D Vector3D_3 = current;
            Vector3D Vector3D_4 = current;
            Vector3D Vector3D_5 = current;
            Vector3D Vector3D_6 = current;
            foreach (Vector3D Vector3D_7 in points)
            {
                if (Vector3D_7.X < Vector3D_6.X)
                    Vector3D_6 = Vector3D_7;
                if (Vector3D_7.X > Vector3D_5.X)
                    Vector3D_5 = Vector3D_7;
                if (Vector3D_7.Y < Vector3D_4.Y)
                    Vector3D_4 = Vector3D_7;
                if (Vector3D_7.Y > Vector3D_3.Y)
                    Vector3D_3 = Vector3D_7;
                if (Vector3D_7.Z < Vector3D_2.Z)
                    Vector3D_2 = Vector3D_7;
                if (Vector3D_7.Z > Vector3D_1.Z)
                    Vector3D_1 = Vector3D_7;
            }
            double result1;
            Vector3D.Distance(ref Vector3D_5, ref Vector3D_6, out result1);
            double result2;
            Vector3D.Distance(ref Vector3D_3, ref Vector3D_4, out result2);
            double result3;
            Vector3D.Distance(ref Vector3D_1, ref Vector3D_2, out result3);
            Vector3D result4;
            double num1;
            if (result1 > result2)
            {
                if (result1 > result3)
                {
                    Vector3D.Lerp(ref Vector3D_5, ref Vector3D_6, 0.5f, out result4);
                    num1 = result1 * 0.5f;
                }
                else
                {
                    Vector3D.Lerp(ref Vector3D_1, ref Vector3D_2, 0.5f, out result4);
                    num1 = result3 * 0.5f;
                }
            }
            else if (result2 > result3)
            {
                Vector3D.Lerp(ref Vector3D_3, ref Vector3D_4, 0.5f, out result4);
                num1 = result2 * 0.5f;
            }
            else
            {
                Vector3D.Lerp(ref Vector3D_1, ref Vector3D_2, 0.5f, out result4);
                num1 = result3 * 0.5f;
            }
            foreach (Vector3D Vector3D_7 in points)
            {
                Vector3D Vector3D_8;
                Vector3D_8.X = Vector3D_7.X - result4.X;
                Vector3D_8.Y = Vector3D_7.Y - result4.Y;
                Vector3D_8.Z = Vector3D_7.Z - result4.Z;
                double num2 = Vector3D_8.Length();
                if (num2 > num1)
                {
                    num1 = ((num1 + num2) * 0.5);
                    result4 += (1.0 - num1 / num2) * Vector3D_8;
                }
            }
            BoundingSphereD boundingSphereD;
            boundingSphereD.Center = result4;
            boundingSphereD.Radius = num1;
            return boundingSphereD;
        }

    }
    class RunningAverage
    {
        int _size;
        int[] _values = null;
        int _valuesIndex = 0;
        int _valueCount = 0;
        int _sum = 0;

        public RunningAverage(int size)
        {
            _size = Math.Max(size, 1);
            _values = new int[_size];
        }

        public int Add(int newValue)
        {
            // calculate new value to add to sum by subtracting the 
            // value that is replaced from the new value; 
            int temp = newValue - _values[_valuesIndex];
            _values[_valuesIndex] = newValue;
            _sum += temp;

            _valuesIndex++;
            _valuesIndex %= _size;

            if (_valueCount < _size)
                _valueCount++;

            return _sum / _valueCount;
        }
    }
}
