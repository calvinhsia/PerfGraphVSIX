//Include: ..\Util\LeakBaseClass.cs
//Desc: This demonstrates ThreadPoolStarvation leak. After completion, the threadpool may slowly release threads as they're not needed
//Desc:    thus running it again quickly, the threadpool will already be large.
//Desc:    this will leak 1 Megabyte per second


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

namespace MyCodeToExecute
{
    public class MyClass : LeakBaseClass
    {
        TaskCompletionSource<int> _tcs;
        static int numIterations = 30;
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations);
            }
        }
        public MyClass(object[] args) : base(args)
        {
            //ShowUI = false;
            //NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
            _tcs = new TaskCompletionSource<int>();
        }

        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            int nmSecsPerSleep = 1;
            int numTasksPerIteration = 20;  // queue many tasks per iteration
            for (int i = 0; i < numTasksPerIteration; i++) // could use a timer to generate tasks too
            {
                Task.Run(async () => // spawn task with long loop on TP, but don't wait for it
                { // do something here that takes a while to complete.
                    while (!_tcs.Task.IsCompleted && !_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        await Task.Yield(); // pretend to be a good citizen
                        System.Threading.Thread.Sleep(nmSecsPerSleep); // we don't want to eat the CPU on the thread
                    }
                    _logger.LogMessage(string.Format("done task Iter={0} Lp={1}", iteration, i));
                });
            }
            if (iteration == numIterations - 1)
            {
                _tcs.SetResult(0); // tell all the tasks to complete
            }
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            _logger.LogMessage(string.Format("tcs set completion"));
        }
    }
}
