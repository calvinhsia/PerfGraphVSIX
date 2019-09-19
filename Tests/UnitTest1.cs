using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    public class BaseTestClass
    {
        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void TestInitialize()
        {
            LogTestMessage($"Starting test {TestContext.TestName}");
        }
        public void LogTestMessage(string str)
        {
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId} {str}";
            this.TestContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
        }
    }

    [TestClass]
    public class UnitTest1 : BaseTestClass
    {
        class Basecontainer
        {
            object _obj;
            internal Basecontainer(object obj)
            {
                this._obj = obj;
            }
        }
        class MyContainer : Basecontainer
        {
            WeakReference<object> wr;
            public MyContainer(object obj) : base(obj)
            {
                wr = new WeakReference<object>(obj);
            }
            public object GetTarget()
            {
                object ret = null;
                wr.TryGetTarget(out ret);
                return ret;
            }
            public override string ToString()
            {
                return $"{GetTarget().ToString()}";
            }
        }


        [TestMethod]
        public async Task TestQueueThreads()
        {
            int nThreads = 100;
            int nIter = 1000;
            int cnt = 0;
            int nDequeued = 0;
            ObservableCollection<MyContainer> coll = new ObservableCollection<MyContainer>();
            try
            {
                var cts = new CancellationTokenSource();
                var doneEvent = new ManualResetEventSlim();
                var queue = new ConcurrentQueue<MyContainer>();
                var taskDrain = Task.Run(async () =>
                {
                    LogTestMessage($"Starting drain");
                    while (!doneEvent.IsSet || queue.Count > 0)
                    {
                        await Task.Yield();
                        while (queue.TryDequeue(out var mycont))
                        {
                            LogTestMessage($"Deq {mycont}");
                            nDequeued++;
                            coll.Add(mycont);
                        }
                    }
                    LogTestMessage($"done drain");
                });

                var tasks = new Task<int>[nThreads];
                for (int iThread = 0; iThread < nThreads; iThread++)
                {
                    var x = iThread;
                    tasks[iThread] = Task.Run(() =>
                    {
                        LogTestMessage($"starting {x}");
                        for (int i = 0; i < nIter; i++)
                        {
                            queue.Enqueue(new MyContainer(Interlocked.Increment(ref cnt)));
                        }
                        return 0;
                    }
                    );
                }
                await Task.Run(() => Task.WaitAll(tasks));
                doneEvent.Set();
                cts.Cancel();
                await taskDrain;
                Assert.AreEqual(nIter * nThreads, coll.Count, $" should be equal");

            }
            catch (Exception ex)
            {
                LogTestMessage($"got exception {ex.ToString()}");
                throw;
            }
            LogTestMessage($"expect {nThreads * nIter} cnt = {cnt} CollSize={coll.Count}");
            Assert.AreEqual(nIter * nThreads, cnt, $" should be equal");
            Assert.AreEqual(nIter * nThreads, coll.Count, $" should be equal");
        }



        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public async Task TestObsCollThreads()
        {
            ObservableCollection<MyContainer> coll = new ObservableCollection<MyContainer>();
            int nThreads = 100;
            int nIter = 100000;
            int cnt = 0;
            try
            {
                var tasks = new Task<int>[nThreads];
                for (int iThread = 0; iThread < nThreads; iThread++)
                {
                    var x = iThread;
                    tasks[iThread] = Task.Run(() =>
                    {
                        //                        LogTestMessage($"start {x}");
                        for (int i = 0; i < nIter; i++)
                        {
                            Interlocked.Increment(ref cnt);
                            coll.Add(new MyContainer(cnt));
                        }
                        return 0;
                    }
                    );
                }
                await Task.Run(() => Task.WaitAll(tasks));
            }
            catch (Exception ex)
            {
                LogTestMessage($"got expected exception {ex.ToString()}");
                throw;
            }
            Assert.Fail("Should not get here");
        }

        [TestMethod]
        public async Task TestObsColl()
        {
            ObservableCollection<MyContainer> coll = new ObservableCollection<MyContainer>();
            int nIter = 1000;
            int cnt = 0;
            await Task.Run(() =>
            {
                for (int i = 0; i < nIter; i++)
                {
                    coll.Add(new MyContainer(cnt++));
                }

            });
            Assert.AreEqual(nIter, coll.Count, $" should be equal");

        }
    }
}
