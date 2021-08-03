//Include: ..\Util\LeakBaseClass.cs
//Desc: This demonstrates different ways that Cancellation Token Source leaks
//Desc: Run this sample to see a graph of the leak, and how the stresslib identifies the leaking objects.

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
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 13);
            }
        }
        public MyClass(object[] args) : base(args)
        {
            //ShowUI = false;
            //NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
        }

        CancellationTokenSource _cts;

        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
            _cts = new CancellationTokenSource();
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            var numPerIter = 100000;
            await Task.Run(async () =>
            {
                await Task.Yield();
                for (int i = 0; i < numPerIter; i++)
                {
                    // this shows 4 different ways to leak with CTS:
                    var newcts = new CancellationTokenSource();
                    var linked = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[] { _cts.Token });
                    //newcts.Dispose(); // no effect because no registered callbacks on it.
                    //linked.Dispose(); // must dispose the linked else leaks the lifetime of _cts


                    // 2nd way:
                    //var tk = _cts.Token;
                    //var cancellationTokenRegistration = tk.Register(() =>
                    //{

                    //});
                    //cancellationTokenRegistration.Dispose(); // must dispose else leaks. CTS Leak Type No. 2


                    // 3rd way:
                    //var newcts = new CancellationTokenSource();
                    //newcts.CancelAfter(TimeSpan.FromMinutes(10)); // this leaks mem. 
                    //newcts.Dispose(); // must dispose else leaks CTS Leak Type No. 3. This leak is unrecoverable (til the timer finishes)


                    // 4th way:
                    //var newcts = new CancellationTokenSource();
                    //var handle = newcts.Token.WaitHandle; // this internally lazily instantiates a ManualResetEvent
                    //newcts.Dispose(); // must dispose, else leaks mem and handles. CTS Leak Type No. 4


                    if (i % 10000 == 0 && token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            });
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            _cts.Dispose();
        }
    }
}
