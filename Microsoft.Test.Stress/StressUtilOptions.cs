using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class StressUtilOptions
    {
        /// <summary>
        /// Specify the number of iterations to do. Some scenarios are large (open/close solution)
        /// Others are small (scroll up/down a file)
        /// </summary>
        public int NumIterations = 7;
        /// <summary>
        /// Defaults to 1.0 Some perf counter thresholds are large (e.g. 1 megabyte for Private bytes). 
        /// The actual threshold used is the Thresh divided by Sensitivity.
        /// Thus, to find smaller leaks, magnify them by setting this to 1,000,000. Or make the test less sensitive by setting this to .1 (for 10 Meg threshold)
        /// </summary>
        public double Sensitivity = 1.0f;
        /// <summary>
        /// Defaults to 1. All delays (e.g. between iterations, after GC, are multiplied by this factor.
        /// Running the test with instrumented binaries (such as under http://Toolbox/MemSpect  will slow down the target process
        /// </summary>
        public int DelayMultiplier = 1;
        /// <summary>
        /// '|' separated list of processes to monitor VS, use 'devenv' To monitor the current process, use ''. Defaults to 'devenv'
        /// </summary>
        public string ProcNamesToMonitor = "devenv";
        /// <summary>
        /// Show results automatically, like the Graph of Measurements, the Dump in ClrObjExplorer, the Diff Analysis
        /// </summary>
        public bool ShowUI = false;
        public List<PerfCounterDataSetting> lstperfCounterDataSettings = null;
        /// <summary>
        ///  Specifies the iteration # at which to take a baseline. 
        ///    <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/> is subtracted from <paramref name="NumIterations"/> to get the baseline iteration number
        /// e.g. 100 iterations, with <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/>=4 means take a baseline at iteartion 100-4==96;
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4;
        public TimeSpan timeBetweenIterations = TimeSpan.FromSeconds(0);

        private object theTest;
        internal VSHandler VSHandler;

        private bool? _isApexTest;
        internal void SetTest(object test)
        {
            theTest = test;
        }
        internal bool IsTestApexTest()
        {
            if (!_isApexTest.HasValue)
            {
                _isApexTest = false;
                var typ = theTest.GetType();
                var baseT = string.Empty;
                while (baseT != "Object")
                {
                    baseT = typ.BaseType.Name;
                    typ = typ.BaseType;
                    if (baseT == "ApexTest")
                    {
                        _isApexTest = true;
                        break;
                    }
                }
            }
            return _isApexTest.Value;
        }

    }
}
