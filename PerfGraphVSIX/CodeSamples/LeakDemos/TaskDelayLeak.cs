//Include: ..\Util\LeakBaseClass.cs
//Desc: This demonstrates Leaks from Task.Delay(), especially when used with a Task.WaitAny(), as in execute a task with timeout
//Desc: If you use await Task.WaitAny(), the await will continue if any of the tasks in the list complete. 
//Desc: All the rest of the tasks will continue to execute.
//Desc: For example, a timeout could be await Task.WaitAny(mytask, Task.Delay(10000))
//Desc: If this is in a fast loop, the Task.Delay will accumulate in memory as a resource and will eventually release
//Desc:  after the timeout. If any of the tasks reference an object (e.g. 'this'), then they will be leaked until all timeouts elapsed
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
        public int timeoutmsecs = 120000;
        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
            _cts = new CancellationTokenSource();
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            var leaktype = 1;
            switch(leaktype)
            {
                case 1:
                    {
                        var numPerIter = 100000;
                        await Task.Run(async () =>
                        {
                            for (int i = 0; i < numPerIter; i++)
                            {
                                var cts = new CancellationTokenSource();
                                await Task.WhenAny(Task.Delay(0), Task.Delay(TimeSpan.FromMinutes(1), cts.Token)); // delay of zero returns a completed task
                                /* leaks these types in 9 and 13 iterations, numPerIter=100000:
900020 1300021 System.Threading.Tasks.Task+DelayPromise
900038 1300038 System.Threading.Timer
900038 1300038 System.Threading.TimerHolder
900038 1300039 System.Threading.TimerQueueTimer
900207 1300207 System.Threading.CancellationCallbackInfo
900344 1300344 System.Threading.SparselyPopulatedArray<System.Threading.CancellationCallbackInfo>[]
900815 1300815 System.Threading.CancellationTokenSource
900849 1300849 System.Threading.SparselyPopulatedArray<System.Threading.CancellationCallbackInfo>
900929 1300929 System.Threading.SparselyPopulatedArrayFragment<System.Threading.CancellationCallbackInfo>
900929 1300929 System.Threading.CancellationCallbackInfo[]
901647 1301670 System.Threading.ExecutionContext
                                 */
//                                cts.Cancel(); // to fix leak (or wait 1 minuite)
                                if (_CancellationTokenExecuteCode.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                        });
                    }
                    break;
                case 2:
                    {
                        /*
Children of '-- System.Threading.TimerQueueTimer 254398fc'
-- System.Threading.TimerQueueTimer 254398fc
 -> m_next = System.Threading.TimerQueueTimer 254396f8
 -> m_prev = System.Threading.TimerQueueTimer 25439d04
 -> m_timerCallback = System.Threading.TimerCallback 03b776e8 System.Threading.Tasks.Task+<>c System.Threading.Tasks.Task+<>c.<Delay>b__274_1 System.Threading.Tasks.Task+<>c System.Threading.Tasks.Task+<>c.<Delay>b__274_1
 -> m_state = System.Threading.Tasks.Task+DelayPromise 254398ac
  -> m_continuationObject = System.Action 254399a4 System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner.Run System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner.Run
   -> _target = System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner 25439994
    -> m_context = System.Threading.ExecutionContext 25439968
    -> m_stateMachine = MyCodeToExecute.MyClass+BigStuffWithLongNameSoICanSeeItBetter+<>c__DisplayClass4_0+<<DoTheDelay>b__1>d 2543988c
     -> <>4__this = MyCodeToExecute.MyClass+BigStuffWithLongNameSoICanSeeItBetter+<>c__DisplayClass4_0 254397fc
      -> <>4__this = MyCodeToExecute.MyClass+BigStuffWithLongNameSoICanSeeItBetter 254397ec
      -> tcs = System.Threading.Tasks.TaskCompletionSource<System.Int32> 2543980c
     -> <>t__builder.m_builder.m_task = System.Threading.Tasks.Task<System.Threading.Tasks.VoidTaskResult> 254399c4 WaitingForActivation WaitingForActivation
     -> <>u__1.m_task = System.Threading.Tasks.Task+DelayPromise 254398ac
  -> Timer = System.Threading.Timer 254398ec
 -> m_executionContext = System.Threading.ExecutionContext 25439930
                         */
                        var numPerIter = 10;
                        await Task.Run(async () =>
                        {
                            for (int i = 0; i < numPerIter; i++)
                            {
                                var _BigStuffWithLongNameSoICanSeeItBetter = new BigStuffWithLongNameSoICanSeeItBetter(this);
                                await _BigStuffWithLongNameSoICanSeeItBetter.DoTheDelay();
                                if (_CancellationTokenExecuteCode.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                        });
                    }
                    break;
            }
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            _cts.Dispose();
        }
        class BigStuffWithLongNameSoICanSeeItBetter : IDisposable
        {
            byte[] arr = new byte[1024 * 1024];
            MyClass myClass;
            DateTime birthday;
            public BigStuffWithLongNameSoICanSeeItBetter(MyClass myClass)
            {
                this.birthday = DateTime.Now; // so we can see the creation time in the dump
                this.myClass = myClass;
            }
            // https://devblogs.microsoft.com/pfxteam/keeping-async-methods-alive/
            // https://referencesource.microsoft.com/#mscorlib/system/threading/Tasks/Task.cs,34b191a243434f6a
            public void Dispose()
            {
                GC.SuppressFinalize(this);
            }
            //~BigStuffWithLongNameSoICanSeeItBetter()
            //{
            //     myClass._logger.LogMessage($"{nameof(BigStuffWithLongNameSoICanSeeItBetter)} Finalizer");
            //}
            public async Task DoTheDelayLeaks()
            {
                var tcs = new TaskCompletionSource<int>();
                var taskWork = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.010));
                    tcs.SetResult(0);
                });
                var cts = new CancellationTokenSource();
                //                var taskTimeout = Task.Run(async () => await Task.Delay(TimeSpan.FromSeconds(30), cts.Token)); // doesn't leak: doesn't root "this" to get myClass
                var taskTimeout = Task.Run(async () => await Task.Delay(TimeSpan.FromMilliseconds(myClass.timeoutmsecs), cts.Token)); //leaks: roots "this" to get myClass
                //var taskTimeout = Task.Delay(TimeSpan.FromSeconds(myClass.timeoutsecs));
                await Task.WhenAny(taskTimeout, tcs.Task);
                //cts.Cancel(); // without cancelling the task, the task will complete eventually (after timeoutsecs) and then the task references are eligible for GC
                // task.Dispose() throws if task not completed
            }
            public async Task DoTheDelay()
            {
                var taskWork = Task.Delay(1); // some work task that finishes relatively quickly
                var cts = new CancellationTokenSource();
                //                var taskTimeout = Task.Run(async () => await Task.Delay(TimeSpan.FromSeconds(30), cts.Token)); // doesn't leak: doesn't root "this" to get myClass
                var taskTimeout = Task.Run(async () => await Task.Delay(TimeSpan.FromMilliseconds(myClass.timeoutmsecs), cts.Token)); //leaks: roots "this" to get myClass
                await Task.WhenAny(taskTimeout, taskWork);
                //cts.Cancel(); // without cancelling the task, the task will complete eventually (after timeoutsecs) and then the task references are eligible for GC
            }
        }
    }
}
