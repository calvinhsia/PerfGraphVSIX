using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.Test.Stress
{

    [Serializable]
    public class StressUtilOptions
    {
        /// <summary>
        /// Specify the number of iterations to do. Some scenarios are large (open/close solution)
        /// Others are small (scroll up/down a file). Should be >= 5 iterations for best results.
        /// To determine a good value: Run the tests and examine the measurement graphs. If there is a lot of noise in the graphs, but the trend line slope indicates a leak, 
        /// increase the # iterations to get a better R² value, which indicates how well the trend line matches the data.
        /// </summary>
        public int NumIterations = 71;
        /// <summary>
        /// Defaults to 1.0 Some perf counter thresholds are large (e.g. 1 megabyte for Private bytes). 
        /// The actual threshold used is the per counter Threshold divided by Sensitivity.
        /// Thus, to find smaller leaks, magnify them by setting this to 1,000,000. Or make the test less sensitive by setting this to .1 (for 10 Meg threshold)
        /// </summary>
        public double Sensitivity = 1.0f;
        /// <summary>
        /// Defaults to 1. All delays (e.g. between iterations, after GC, are multiplied by this factor.
        /// Running the test with instrumented binaries (such as under http://Toolbox/MemSpect  will slow down the target process
        /// </summary>
        public int DelayMultiplier = 1;
        /// <summary>
        /// The percent of iterations for which outlier measurements are ignored
        /// If there are a few outliers, they will be ignored for calculating the slope and R²
        /// </summary>
        public int pctOutliersToIgnore = 5;
        /// <summary>
        /// '|' separated list of processes to monitor VS, use 'devenv' To monitor the current process, use ''. Defaults to 'devenv'
        /// Set this to '' when measuring leaks in current process (like the testhost proess)
        /// </summary>
        public string ProcNamesToMonitor = "devenv";
        private bool _ShowUI = false;
        /// <summary>
        /// Show results automatically, like the Graph of Measurements, the Dump in ClrObjExplorer, the Diff Analysis
        /// </summary>
        public bool ShowUI
        {
            get
            {
                if (IsRunningOnBuildMachine())
                {
                    _ShowUI = false;
                }
                return _ShowUI;
            }
            set
            {
                _ShowUI = value;
            }
        }

        public static bool IsRunningOnBuildMachine()
        {
            /*On build machine:
             * Computername=fv-az683
             * UserDomain=fv-az683
             */
            if (Environment.GetEnvironmentVariable("Computername") == Environment.GetEnvironmentVariable("UserDomain"))
            {
                return true;
            }
            return false;
        }

        public List<PerfCounterOverrideThreshold> PerfCounterOverrideSettings = null;
        /// <summary>
        ///  Specifies the iteration # at which to take a baseline. 
        ///   NumIterationsBeforeTotalToTakeBaselineSnapshot is subtracted from NumIterations to get the baseline iteration number
        /// e.g. 100 iterations, with NumIterationsBeforeTotalToTakeBaselineSnapshot ==4 means take a baseline at iteartion 100-4==96;
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4;
        /// <summary>
        /// It's hard to tell if a test is working. Test output doesn't appear til after the test has completed.
        /// To see interim results, set this true so output is appended to a file "TestStressDataCollector.log". Note: this file is never truncated so watch it's size.
        /// </summary>
        public bool LoggerLogOutputToDestkop = false;
        /// <summary>
        /// Apex tests use .Net remoting which has a default lease lifetime of 5 minutes: need to add delay.
        ///  Dochttps://docs.microsoft.com/en-us/dotnet/api/system.runtime.remoting.lifetime.lifetimeservices?view=netframework-4.8 
        /// Src: https://referencesource.microsoft.com/#mscorlib/system/runtime/remoting/lifetimeservices.cs,be0b61af7bd01e98
        ///             //Gets or sets the initial lease time span for an AppDomain (default 5 min. Can only be set once per appdomain, subsequent attemps throw System.Runtime.Remoting.RemotingException: 'LeaseTime' can only be set once within an AppDomain.
        ///             LifetimeServices.LeaseTime = TimeSpan.FromSeconds(10);
        ///             LifetimeServices.
        ///             
        ///             Gets or sets the time interval between each activation of the lease manager to clean up expired leases. (default 10 seconds)
        ///             LifetimeServices.LeaseManagerPollTime = TimeSpan.FromSeconds(10); 
        ///             
        /// Children of "--> 1ec80ddc Microsoft.VisualStudio.Editor.Implementation.VsTextViewAdapter GCRoots"
        ///             --> 1ec80ddc Microsoft.VisualStudio.Editor.Implementation.VsTextViewAdapter GCRoots
        ///              --> 03916da8 System.Threading.TimerQueue StaticVar static var System.Threading.TimerQueue.s_queue PathLen= 9
        ///               -- > 03916da8 System.Threading.TimerQueue.m_timers (#instances = 1)
        ///               --> 0445a808 System.Threading.TimerQueueTimer.m_timerCallback (#instances = 130)
        ///               --> 0445a7ac System.Threading.TimerCallback._target (#instances = 34)
        ///               --> 0445a4bc System.Runtime.Remoting.Lifetime.LeaseManager.leaseToTimeTable (#instances = 1)
        ///               --> 0445a4e4 System.Collections.Hashtable.buckets (#instances = 3225)
        ///               --> 1de643d4 System.Collections.Hashtable+bucket[]  (#instances = 3228)
        ///               --> 1e040eb0 System.Runtime.Remoting.Lifetime.Lease.managedObject (#instances = 135)
        ///               --> 298198f0 Microsoft.Test.Apex.VisualStudio.Editor.VisualStudioTextEditorTestExtension.&lt;VsTextView>k__BackingField (#instances = 22)
        ///               --> 1ec80ddc Microsoft.VisualStudio.Editor.Implementation.VsTextViewAdapter  (#instances = 27)
        /// </summary>
        public int SecsBetweenIterations = 0;


        /// <summary>
        /// We don't want an old VS session: we want to find the devenv process that was started by the test: +/- timeSpan seconds
        /// </summary>
        public int SecsToWaitForDevenv = 60;


        internal object theTest;
        internal VSHandler VSHandler;

        internal bool? _isApexTest;

        /// <summary>
        /// used internally, but has to be public for user dynamically compiled ExecCode: we don't know the assembly name and it's not signed.
        /// </summary>
        [XmlIgnore]
        public ILogger logger;


        internal TestContextWrapper testContext;
        internal MethodInfo _theTestMethod;
        [XmlIgnore]
        public List<PerfCounterData> lstPerfCountersToUse; // public for vsix

        internal void ReadOptionsFromFile(string fileNameOptions)
        {
            StressUtilOptions fileOptions;
            var xmlSerializer = new XmlSerializer(typeof(StressUtilOptions));
            using (var sr = new StreamReader(fileNameOptions))
            {
                fileOptions = (StressUtilOptions)xmlSerializer.Deserialize(sr);
            }
            // the file settings override the passed in values, if any
            PerfCounterOverrideSettings = fileOptions.PerfCounterOverrideSettings;
            var mems = typeof(StressUtilOptions).GetMembers(BindingFlags.Public | BindingFlags.Instance);
            foreach (var mem in mems)
            {
                if (mem is FieldInfo fldInfo)
                {
                    if (fldInfo.Name != nameof(logger) && fldInfo.Name != nameof(PerfCounterOverrideSettings))
                    {
                        var newval = fldInfo.GetValue(fileOptions);
                        //                        logger.LogMessage($"Override Setting {fldInfo.Name}  from {fldInfo.GetValue(this)} to {newval}");
                        fldInfo.SetValue(this, newval);
                    }
                }
            }
        }


        internal void WriteOptionsToFile(string fileNameOptions)
        {
            var xmlSerializer = new XmlSerializer(typeof(StressUtilOptions));
            var settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = " "
            };
            using (var strm = File.Create(fileNameOptions))
            {
                using (var writer = XmlWriter.Create(strm, settings))
                {
                    xmlSerializer.Serialize(writer, this);
                }
            }
        }


        /// <summary>
        /// if we're being called from a test, pass the test in. Else being called from a dynamic asm in devenv process from vsix (in which case SetTest is not called)
        /// </summary>
        /// <returns>false if recurring</returns>
        internal async Task<bool> SetTest(object test)
        {
            theTest = test;
            var testType = test.GetType();
            var methGetContext = testType.GetMethod($"get_TestContext");
            if (methGetContext == null)
            {
                throw new InvalidOperationException("can't get TestContext from test. Test must have 'public TestContext TestContext { get; set; }' (perhaps inherited)");
            }
            var val = methGetContext.Invoke(test, null);
            testContext = new TestContextWrapper(val);

            if (testContext.Properties[StressUtil.PropNameRecursionPrevention] != null)
            {
                return false; // we're recurring
            }
            testContext.Properties[StressUtil.PropNameRecursionPrevention] = 1;

            _theTestMethod = testType.GetMethod(testContext.TestName);
            if (logger == null)
            {
                var loggerFld = testType.GetField("logger");
                if (loggerFld != null)
                {
                    logger = loggerFld.GetValue(test) as ILogger;
                }
                if (logger == null)
                {
                    logger = new Logger(testContext);
                    if (logger is Logger mylogger)
                    {
                        mylogger.LogOutputToDesktopFile = LoggerLogOutputToDestkop;
                    }
                }
            }
            logger.LogMessage($@"TestName = {testContext.TestName} 
                                        IsApexTest={IsTestApexTest()}
                                        NumIterations = {NumIterations}
                                        Sensitivity = {Sensitivity}
                                        CurDir = '{Environment.CurrentDirectory}'
                                        TestDeploymentDir = '{testContext.TestDeploymentDir}'
                                        TestRunDirectory = '{testContext.TestRunDirectory}'  
                                        TestResultsDirectory='{testContext.TestResultsDirectory}' 
                                        TestRunResultsDirectory='{testContext.TestRunResultsDirectory}'
                ");

            testContext.Properties[StressUtil.PropNameLogger] = logger;
            /*
             * probs: the curdir is not empty, so results will be overwritten (might have ClrObjExplorer or WinDbg open with a result dump)
             *       The Test*dirs are all deleted after the run.
             *       Can use a Runsettings   
             *               <DeleteDeploymentDirectoryAfterTestRunIsComplete>False</DeleteDeploymentDirectoryAfterTestRunIsComplete>
             *              https://docs.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2019
                                    CurDir = 'C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestStress\bin\Debug'
                                    TestDeploymentDir = 'C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59\Out'
                                    TestRunDirectory = 'C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59'  
                                    TestResultsDirectory='C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59\In' 
                                    TestRunResultsDirectory='C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59\In\CALVINH2'
                apex local:
                    CurDir = 'C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34\Out'
                    TestRunDirectory = 'C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34'  
                    TestResultsDirectory='C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34\In' 
                    TestRunResultsDirectory='C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34\In\calvinhW7'

             * */

            var optFile = Path.Combine(testContext.TestDeploymentDir, "Assets", $"{testContext.TestName}.settings.xml");
            if (File.Exists(optFile))
            {
                logger.LogMessage($"Reading settings from {optFile}");
                ReadOptionsFromFile(optFile);
                if (logger is Logger mylogger) // since logger was already instantiated, apply logger options after reading from file
                {
                    mylogger.LogOutputToDesktopFile = LoggerLogOutputToDestkop;
                }
            }

            VSHandler theVSHandler = null;
            if (string.IsNullOrEmpty(ProcNamesToMonitor))
            {
                lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(Process.GetCurrentProcess(), IsForStress: true);
            }
            else
            {
                var vsHandlerFld = testType.GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.FieldType.Name == nameof(VSHandler)).FirstOrDefault();
                if (vsHandlerFld != null)
                {
                    theVSHandler = vsHandlerFld.GetValue(test) as VSHandler;
                    if (theVSHandler._DelayMultiplier > DelayMultiplier) //take the longer of the delaymultipliers
                    {
                        DelayMultiplier = theVSHandler._DelayMultiplier;
                    }
                }
                if (theVSHandler == null)
                {
                    theVSHandler = testContext.Properties[StressUtil.PropNameVSHandler] as VSHandler;
                    if (theVSHandler == null)
                    {
                        theVSHandler = new VSHandler(logger, DelayMultiplier);
                        testContext.Properties[StressUtil.PropNameVSHandler] = theVSHandler;
                    }
                }
                await theVSHandler?.EnsureGotDTE(TimeSpan.FromSeconds(SecsToWaitForDevenv * DelayMultiplier)); // ensure we get the DTE. Even for Apex tests, we need to Tools.ForceGC
                VSHandler = theVSHandler;
                lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(VSHandler.vsProc, IsForStress: true);
            }
            if (PerfCounterOverrideSettings != null)
            {
                foreach (var userSettingItem in PerfCounterOverrideSettings) // for each user settings // very small list: linear search
                {
                    var pCounterToModify = lstPerfCountersToUse.Where(p => p.perfCounterType == userSettingItem.perfCounterType).FirstOrDefault();
                    if (pCounterToModify != null)
                    {
                        pCounterToModify.thresholdRegression = userSettingItem.regressionThreshold;
                    }
                }
            }
            return true; // not recurring
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
