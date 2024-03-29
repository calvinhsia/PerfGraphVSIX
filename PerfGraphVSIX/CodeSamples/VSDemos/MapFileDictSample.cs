﻿//Include: ..\Util\LeakBaseClass.cs
//Desc: Demonstrate using a file on disk or system page file for backing store for an IDictionary<K,V> or IList<T>
// 
//Ref: ..\Util\MapFileDict.dll

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using MapFileDict;

namespace MyCodeToExecute
{

    public class MyClass : LeakBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 13);
            }
        }
        public MyClass(object[] args) : base(args)
        {
            ShowUI = false;
            NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
        }

        MapFileDict<int, DataClass> mfd;
        IDictionary<int, DataClass> dict;
        Random _random = new Random();

        public override async Task DoInitializeAsync()
        {
            // Sample demonstrates  putting lots of data in a dictionary.
            // The dictionary can be either a normal System.Collectsions.Generic.Dictionary<K,V>
            // or a MapFileDict<K,V>, because they both implement IDictionary<K,V>
            bool fUseMapDict = false;
            if (!fUseMapDict)
            {
                dict = new Dictionary<int, DataClass>();
            }
            else
            {
                var mapfileType = MapMemTypes.MapMemTypePageFile; // store data in system page file 
                mapfileType = MapMemTypes.MapMemTypeFileName; // store data in temp file on disk
                mfd = new MapFileDict<int, DataClass>(ulInitialSize: 0, mapfileType: mapfileType);
                //                mfd._MemMap.ChangeViewSize(65536 * 2); // can change default memory map view size
                dict = mfd;
            }
           _logger.LogMessage("Using " + dict.GetType().Name);
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            var numPerIter = 10000;
            await Task.Run(async () =>
            {
                dict.Clear();
                for (int i = 0; i < numPerIter; i++)
                {
                    dict[i] = DataClass.MakeInstance(i);

                    if (i % 10000 == 0 && cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
                if (mfd != null)
                {
                   _logger.LogMessage(mfd._MemMap._stats.ToString());
                    //                    await Task.Delay(TimeSpan.FromSeconds(10), cts);
                }
            });
        }
        public override async Task DoCleanupAsync()
        {
            if (mfd != null)
            {
                mfd.Dispose();
            }
        }
    }

    public class baseDataClass
    {
        public long basenum;
    }
    public class Derived : DataClass
    {
    }


    public class DataClass : baseDataClass
    {
        public static string xstatic;
        public int int1;
        public uint uint2;
        public long long3;
        public ulong ulong4;
        public string str5;
        public bool bool6;
        public float float7;
        public double double8;
        public DateTime dt9;

        public static DataClass MakeInstance(int i)
        {
            var testInstance = new DataClass()
            {
                //str5 = "FOO" + i.ToString(), 
                int1 = i,
                ulong4 = (ulong)i,
                uint2 = (uint)i,
                long3 = 256 * i,
                basenum = i,
                str5 = makestring(i),
                float7 = (float)i,
                double8 = (double)i,
                dt9 = new DateTime(i)
            };
            return testInstance;
        }
        public static string makestring(int i)
        {
            if (i % 10 == 0)
            {
                return null; // test null strings too
            }
            if (i % 13 == 0)
            {
                return string.Empty; // test null strings too
            }
            return string.Format("Foo{0}", new string('0', 10000 + i % 20000));
        }

        //public DateTime dt;
        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3} {4}", str5, basenum, int1, uint2, long3, bool6);
        }
        public override bool Equals(object obj)
        {
            var IsEqual = true;
            var right = obj as DataClass;
            if (right == null)
            {
                IsEqual = false;
            }
            else
                if (int1 != right.int1)
            {
                IsEqual = false;
            }
            else if (uint2 != right.uint2)
            {
                IsEqual = false;
            }
            else if (long3 != right.long3)
            {
                IsEqual = false;
            }
            else if (ulong4 != right.ulong4)
            {
                IsEqual = false;
            }
            else if (float7 != right.float7)
            {
                IsEqual = false;
            }
            else if (double8 != right.double8)
            {
                IsEqual = false;
            }
            else if (dt9 != right.dt9)
            {
                IsEqual = false;
            }
            else
            {
                if (str5 == null ^ right.str5 == null)
                {
                    IsEqual = false;
                }
                if (str5 != right.str5)
                {
                    IsEqual = false;
                }
            }
            return IsEqual;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
