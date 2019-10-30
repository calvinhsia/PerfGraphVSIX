using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;

namespace TestStress
{

    [TestClass]
    public class UnitTest1 : BaseStressTestClass
    {
        [TestInitialize]
        public void Initialize()
        {
            LogMessage($"{nameof(Initialize)}");
            var vsPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe";
            LogMessage($"Starting VS");
            _vsProc = Process.Start(vsPath);
            LogMessage($"Started VS PID= {_vsProc.Id}");

            var tskGetDTE = GetDTEAsync(_vsProc.Id, TimeSpan.FromSeconds(30));
            tskGetDTE.Wait();
            _vsDTE = tskGetDTE.Result;
            LogMessage($"done {nameof(Initialize)}");
            _vsDTE.Events.SolutionEvents.Opened += SolutionEvents_Opened;
            _vsDTE.Events.SolutionEvents.AfterClosing += SolutionEvents_AfterClosing;

        }

        private void SolutionEvents_AfterClosing()
        {
            LogMessage($"{nameof(SolutionEvents_AfterClosing)}");
            _tcsSolution.TrySetResult(0);
        }

        private void SolutionEvents_Opened()
        {
            LogMessage($"{nameof(SolutionEvents_Opened)}");
            _tcsSolution.TrySetResult(0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            LogMessage($"{nameof(Cleanup)}");
            _vsDTE.Quit();
            LogMessage($"done {nameof(Initialize)}");
        }
        TaskCompletionSource<int> _tcsSolution;

        [TestMethod]
        public async Task StressTest1()
        {
            for (int i = 0; i < 7; i++)
            {
                LogMessage($"{nameof(StressTest1)}");
                string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
                _tcsSolution = new TaskCompletionSource<int>();
                LogMessage($"{nameof(StressTest1)} Open solution");
                _vsDTE.Solution.Open(SolutionToLoad);
                await _tcsSolution.Task;
                LogMessage($"{nameof(StressTest1)} Opened solution");

                _tcsSolution = new TaskCompletionSource<int>();
                await Task.Delay(TimeSpan.FromSeconds(5));

                LogMessage($"{nameof(StressTest1)} close solution");
                _vsDTE.Solution.Close();
                await _tcsSolution.Task;
                LogMessage($"{nameof(StressTest1)} closed solution");

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

    }
}
