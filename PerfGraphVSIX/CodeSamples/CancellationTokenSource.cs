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

        CancellationTokenSource _cts;

        public override async Task DoInitializeAsync()
        {
            _cts = new CancellationTokenSource();
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            var numPerIter = 100000;
            await Task.Run(async () =>
            {
                for (int i = 0; i < numPerIter; i++)
                {
                    var newcts = new CancellationTokenSource();
                    var linked = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[] { _cts.Token });
                    //newcts.Dispose(); // no effect because no registered callbacks on it.
                    //linked.Dispose(); // must dispose the linked else leaks the lifetime of _cts



                    //var tk = _cts.Token;
                    //var cancellationTokenRegistration = tk.Register(() =>
                    //{

                    //});
                    //cancellationTokenRegistration.Dispose(); // must dispose else leaks. CTS Leak Type No. 2


                    //var newcts = new CancellationTokenSource();
                    //newcts.CancelAfter(TimeSpan.FromMinutes(10)); // this leaks mem. 
                    //newcts.Dispose(); // must dispose else leaks CTS Leak Type No. 3. This leak is unrecoverable (til the timer finishes)

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
            _cts.Dispose();
        }
    }
}
