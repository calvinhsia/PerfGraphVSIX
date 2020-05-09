//Include: ExecCodeBase.cs
// this will demonstate leak detection
// 
//Ref: MapFileDict.dll

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

    public class MyClass : BaseExecCodeClass
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
        public override async Task DoInitializeAsync()
        {
            logger.LogMessage("In MapFileDict");
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            var numPerIter = 4000000;
            bool fUseMapDict = true;
            await Task.Run(async () =>
            {
                if (!fUseMapDict)//|| true)
                {
                    dict = new Dictionary<int, DataClass>();
                }
                else
                {
                    if (mfd != null)
                    {
                        mfd.Dispose();
                    }
                    var mapfileType = MapMemTypes.MapMemTypeFileName;
                    mfd = new MapFileDict<int, DataClass>(ulInitialSize: 0, mapfileType: mapfileType);
                    mfd._MemMap.ChangeViewSize(65536 * 2);
                    dict = mfd;
                }
                // to test if your code leaks, put it here. Repeat a lot to magnify the effect
                for (int i = 0; i < numPerIter; i++)
                {
                    dict[iteration * numPerIter + i] = DataClass.MakeInstance((ulong)iteration);
                    if (i % 1000 == 0 && cts.IsCancellationRequested)
                    {
                        break;
                    }
                    //                logger.LogMessage(string.Format("loop {0}", i));
                }
                if (mfd != null)
                {
                    logger.LogMessage(mfd._MemMap._stats.ToString());
//                    await Task.Delay(TimeSpan.FromSeconds(20), cts);
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

        public static DataClass MakeInstance(ulong i)
        {
            var testInstance = new DataClass()
            {
                //str5 = "FOO" + i.ToString(), 
                int1 = (int)i,
                ulong4 = i,
                uint2 = (uint)i,
                long3 = 256 * (long)i,
                basenum = (long)i,
                str5 = makestring(i),
                float7 = (float)i,
                double8 = (double)i,
                dt9 = new DateTime((long)i)
            };
            return testInstance;
        }
        public static string makestring(ulong i)
        {
            if (i % 10 == 0)
            {
                return null; // test null strings too
            }
            if (i % 13 == 0)
            {
                return string.Empty; // test null strings too
            }
            return string.Format("Foo{0}", new string('0', (int)(i % 20000)));
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
