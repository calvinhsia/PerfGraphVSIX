using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;

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

        class MyBigContainer : MyContainer
        {
            int[] _bigArray;
            public MyBigContainer(object obj) : base(obj)
            {
                _bigArray = new int[10000];
            }
        }

        class MyBigData : IDisposable
        {
            int[] _bigArray;
            object _obj;
            public MyBigData(object obj)
            {
                _bigArray = new int[10000];
                _obj = obj;
            }
            bool _fDisposed { get; set; } = false;
            public void Dispose()
            {
                if (!_fDisposed)
                {
                    _fDisposed = true;
                }
            }

            public override string ToString()
            {
                return $"{_obj}, IsDisposed={_fDisposed}";
            }
        }

        [TestMethod]
        public async Task TestObjTrackerWithDisposable()
        {
            int nThreads = 10;
            int nIter = 1000;
            int cnt = 0;
            var rand = new Random(1);
            ConcurrentBag<object> hashHardRefs = new ConcurrentBag<object>();
            var objTracker = new ObjTracker();
            try
            {
                var cts = new CancellationTokenSource();
                var doneEvent = new ManualResetEventSlim();
                var tasks = new Task[nThreads];
                for (int iThread = 0; iThread < nThreads; iThread++)
                {
                    tasks[iThread] = Task.Run(() =>
                    {
                        for (int i = 0; i < nIter; i++)
                        {
                            var obj = new MyBigData(cnt);
                            Interlocked.Increment(ref cnt);
                            objTracker.AddObjectToTrack(obj);
                            if (rand.Next(100) < 50)
                            {
                                obj.Dispose();
                            }
                            if (rand.Next(100) < 50)
                            {
                                hashHardRefs.Add(obj);
                            }
                        }
                    }
                    );
                }
                await Task.Run(() => Task.WaitAll(tasks));
                doneEvent.Set();
                var res = objTracker.GetCounts();
                LogTestMessage($"Got results: live: {res.Item1.Count}  Leaked: {res.Item2.Count}  HardRefsCount={hashHardRefs.Count}");
                foreach (var live in res.Item1)
                {
                    LogTestMessage($"  Live {live.Value,3} {live.Key}");
                }
                foreach (var leak in res.Item2)
                {
                    LogTestMessage($"  Leaked {leak._serialNo} {leak.descriptor}");
                }
                cts.Cancel();
                Assert.AreEqual(1, res.Item1.Count, "only 1 leaking type");
                Assert.IsTrue(res.Item1["MyBigData"] > 1000, "# live > 1000");
                Assert.IsTrue(res.Item2.Count > 1000, "# leaked > 1000");
                //                    Assert.AreEqual(nIter * nThreads, coll.Count, $" should be equal");

            }
            catch (Exception ex)
            {
                LogTestMessage($"got exception {ex.ToString()}");
                throw;
            }
            //LogTestMessage($"expect {nThreads * nIter} cnt = {cnt} CollSize={coll.Count}");
            //Assert.AreEqual(nIter * nThreads, cnt, $" items added should be equal");
            //Assert.IsTrue(nIter * nThreads > coll.Count, $" coll count should be < because GC");
            //Assert.AreEqual(1, coll.Count, "GC collected all but hardref");
        }



        [TestMethod]
        public async Task TestObjTracker()
        {
            int nThreads = 10;
            int nIter = 100;
            int cnt = 0;
            object hardref = null;

            var objTracker = new ObjTracker();
            try
            {
                var cts = new CancellationTokenSource();
                var doneEvent = new ManualResetEventSlim();
                var tasks = new Task[nThreads];
                for (int iThread = 0; iThread < nThreads; iThread++)
                {
                    tasks[iThread] = Task.Run(() =>
                    {
                        for (int i = 0; i < nIter; i++)
                        {
                            var obj = new MyBigData(cnt);
                            Interlocked.Increment(ref cnt);
                            Interlocked.CompareExchange(ref hardref, obj, null);
                            objTracker.AddObjectToTrack(obj);
                        }
                    }
                    );
                }
                await Task.Run(() => Task.WaitAll(tasks));
                doneEvent.Set();
                var res = objTracker.GetCounts();
                cts.Cancel();
                Assert.AreEqual(1, res.Item1.Count, "only 1 left in hardref");
                //                    Assert.AreEqual(nIter * nThreads, coll.Count, $" should be equal");

            }
            catch (Exception ex)
            {
                LogTestMessage($"got exception {ex.ToString()}");
                throw;
            }
            //LogTestMessage($"expect {nThreads * nIter} cnt = {cnt} CollSize={coll.Count}");
            //Assert.AreEqual(nIter * nThreads, cnt, $" items added should be equal");
            //Assert.IsTrue(nIter * nThreads > coll.Count, $" coll count should be < because GC");
            //Assert.AreEqual(1, coll.Count, "GC collected all but hardref");
        }



        [TestMethod]
        public async Task TestWeakObsCollObject()
        {
            int nThreads = 100;
            int nIter = 1000;
            int cnt = 0;
            int nDequeued = 0;
            object hardref = null;
            var coll = new ObservableCollection<WeakReference<object>>();
            try
            {
                var cts = new CancellationTokenSource();
                var doneEvent = new ManualResetEventSlim();
                var queue = new ConcurrentQueue<WeakReference<object>>();
                var taskDrain = Task.Run(async () =>
                {
                    LogTestMessage($"Starting drain");
                    while (!doneEvent.IsSet || !queue.IsEmpty)
                    {
                        while (queue.TryDequeue(out var item))
                        {
                            //                            LogTestMessage($"Deq {item}");
                            nDequeued++;
                            coll.Add(item);
                        }
                        await Task.Yield();
                    }
                    LogTestMessage($"done drain");
                });

                var tasks = new Task<int>[nThreads];
                for (int iThread = 0; iThread < nThreads; iThread++)
                {
                    var x = iThread;
                    tasks[iThread] = Task.Run(() =>
                    {
                        for (int i = 0; i < nIter; i++)
                        {
                            var obj = new MyContainer(Interlocked.Increment(ref cnt));
                            Interlocked.CompareExchange(ref hardref, obj, null);
                            queue.Enqueue(new WeakReference<object>(obj));
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

                var lstToDel = new ObservableCollection<WeakReference<object>>();
                foreach (var itm in coll)
                {
                    if (!itm.TryGetTarget(out var _))
                    {
                        lstToDel.Add(itm);
                    }
                }
                LogTestMessage($"Removing {lstToDel.Count} items");
                foreach (var itm in lstToDel)
                {
                    coll.Remove(itm);
                }
            }
            catch (Exception ex)
            {
                LogTestMessage($"got exception {ex.ToString()}");
                throw;
            }
            LogTestMessage($"expect {nThreads * nIter} cnt = {cnt} CollSize={coll.Count}");
            Assert.AreEqual(nIter * nThreads, cnt, $" items added should be equal");
            Assert.IsTrue(nIter * nThreads > coll.Count, $" coll count should be < because GC");
            Assert.IsTrue(coll.Count >= 1, "GC collected some but not hardref");
        }



        [TestMethod]
        public async Task TestWeakObsColl()
        {
            int nThreads = 100;
            int nIter = 1000;
            int cnt = 0;
            int nDequeued = 0;
            var coll = new ObservableCollection<WeakReference<MyBigContainer>>();
            try
            {
                var cts = new CancellationTokenSource();
                var doneEvent = new ManualResetEventSlim();
                var queue = new ConcurrentQueue<WeakReference<MyBigContainer>>();
                var taskDrain = Task.Run(async () =>
                {
                    LogTestMessage($"Starting drain");
                    while (!doneEvent.IsSet || !queue.IsEmpty)
                    {
                        while (queue.TryDequeue(out var item))
                        {
                            //                            LogTestMessage($"Deq {item}");
                            nDequeued++;
                            coll.Add(item);
                        }
                        await Task.Yield();
                    }
                    LogTestMessage($"done drain");
                });

                var tasks = new Task<int>[nThreads];
                for (int iThread = 0; iThread < nThreads; iThread++)
                {
                    var x = iThread;
                    tasks[iThread] = Task.Run(() =>
                    {
                        for (int i = 0; i < nIter; i++)
                        {
                            queue.Enqueue(new WeakReference<MyBigContainer>(new MyBigContainer(Interlocked.Increment(ref cnt))));
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

                var lstToDel = new ObservableCollection<WeakReference<MyBigContainer>>();
                foreach (var itm in coll)
                {
                    if (!itm.TryGetTarget(out var _))
                    {
                        lstToDel.Add(itm);
                    }
                }
                LogTestMessage($"Removing {lstToDel.Count} items");
                foreach (var itm in lstToDel)
                {
                    coll.Remove(itm);
                }
            }
            catch (Exception ex)
            {
                LogTestMessage($"got exception {ex.ToString()}");
                throw;
            }
            LogTestMessage($"expect {nThreads * nIter} cnt = {cnt} CollSize={coll.Count}");
            Assert.AreEqual(nIter * nThreads, cnt, $" items added should be equal");
            Assert.IsTrue(nIter * nThreads > coll.Count, $" coll count should be < because GC");
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
            Assert.AreEqual(nIter * nThreads, cnt, $" items added should be equal");
            Assert.AreEqual(nIter * nThreads, coll.Count, $" should be equal");
        }



        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public async Task TestObsCollThreads()
        {
            ObservableCollection<MyContainer> coll = new ObservableCollection<MyContainer>();
            int nThreads = 250;
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
