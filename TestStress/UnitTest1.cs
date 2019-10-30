using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

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
//            LogMessage($"{nameof(SolutionEvents_AfterClosing)}");
            _tcsSolution.TrySetResult(0);
        }

        private void SolutionEvents_Opened()
        {
//            LogMessage($"{nameof(SolutionEvents_Opened)}");
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
            LogMessage($"{nameof(StressTest1)}");
            for (int i = 0; i < 1; i++)
            {
                string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
                _tcsSolution = new TaskCompletionSource<int>();
                _vsDTE.Solution.Open(SolutionToLoad);
                await _tcsSolution.Task;

                _tcsSolution = new TaskCompletionSource<int>();
                await Task.Delay(TimeSpan.FromSeconds(5));

                _vsDTE.Solution.Close();
                await _tcsSolution.Task;

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            await IterationsFinishedAsync();
        }

    }
}
