using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StressTestUtility
{
    public enum LeakStatusCondition
    {
        Always,
        Never,
        Unknown,
        NoLeaks,
        SomeLeaks
    }

    [AttributeUsage(System.AttributeTargets.Method, AllowMultiple =false, Inherited =false)]
    public class MemSpectAttribute : Attribute
    {
        public MemSpectAttribute()
        {
            NumIterations = 3;
            UseMemSpect = false;
            TrackClrObjects = 1;
            Sensitivity = 1;
            WriteSnapshot = LeakStatusCondition.Never;
            DesiredVerbosity = LogVerbosity.Diagnostic;
            ShutDownTargetProcess = LeakStatusCondition.NoLeaks;
        }
        /// <summary>
        /// False, to run iterations and see log output for memory use per iteration
        /// True to check for leaks and get callstacks
        /// </summary>
        public bool UseMemSpect { get; set; }
        /// <summary>
        /// The test is run once normally, after which the the Cleannup runs the test "numIterations" times to test for leaks
        /// </summary>
        public int NumIterations { get; set; }

        /// <summary>
        /// Measurement is taken before and after all iterations. If any perfcounter exceeds this percent threshold, test fails
        /// </summary>
        public int PercentThreshold { get; set; }
        public double Sensitivity { get; set; }
        /// <summary>
        /// Normally leave at 1, but when testing infrastructure 0 is faster
        /// </summary>
        public int TrackClrObjects { get; set; }
        public LogVerbosity DesiredVerbosity { get; set; }
        /// <summary>
        /// if you say no, you can always attach to proc via MemSpect and create dump that way
        /// </summary>
        public LeakStatusCondition WriteSnapshot { get; set; }

        /// <summary>
        /// optionally shut down target process at end
        /// </summary>
        public LeakStatusCondition ShutDownTargetProcess { get; set; }

        public override string ToString()
        {
            return $"#Iter = {NumIterations}";
        }
    }
}
