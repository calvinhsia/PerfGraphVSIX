using LeakTestDatacollector;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressVS64
    {
        public const string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
        bool Is32bitVSHost => Process.GetCurrentProcess().MainModule.FileName.Contains("2019");

        public TestContext TestContext { get; set; }
        ILogger logger;
        IVSHandler _VSHandler;
        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            if (!Is32bitVSHost)
            {
                _VSHandler = StressUtil.CreateVSHandler(logger);

                await _VSHandler.StartVSAsync();
                logger.LogMessage($"TestInit starting VS pid= {_VSHandler.VsProcess.Id}");
                await _VSHandler.EnsureGotDTE(TimeSpan.FromSeconds(60));
                await _VSHandler.DteExecuteCommandAsync("View.ErrorList");
            }
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (_VSHandler != null)
            {
                await _VSHandler.ShutDownVSAsync();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressOpenCloseSln64Async()
        {
            //LogMessage($"{Process.GetCurrentProcess().MainModule.FileName}"); // C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\Extensions\TestPlatform\testhost.net472.x86.exe
            if (IntPtr.Size == 4 || Is32bitVSHost)
            {
                throw new LeakException("throwing expected exception", null);
            }
            try
            {
                // the only change to existing test required: call to static method
                await StressUtil.DoIterationsAsync(this, stressUtilOptions: new StressUtilOptions()
                {
                    FailTestAsifLeaksFound = true,
                    NumIterations = 7,
                    LoggerLogOutputToDestkop = true,
                    actExecuteAfterEveryIterationAsync = async (nIter, measurementHolder) =>
                    {
                        await Task.Yield();
                        return true; //do the default action after iteration of checking iteration number and taking dumps, comparing.
                    }
                });

                await _VSHandler.OpenSolutionAsync(SolutionToLoad);

                await _VSHandler.CloseSolutionAsync();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
                throw;
            }
        }
    }
}