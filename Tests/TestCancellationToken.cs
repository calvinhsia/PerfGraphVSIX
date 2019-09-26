using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class TestCancellationToken : BaseTestClass
    {
        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        // uses reflection to find any referenced linked tokensources or registered callbacks
        void ProcessCancellationToken(CancellationToken token)
        {
            LogTestMessage($"Processing tkn {token}");
            var tks = token.GetType().GetField("m_source", bFlags).GetValue(token);
            /*
        private int CallbackCount
        {
            get
            {
                SparselyPopulatedArray<CancellationCallbackInfo>[] callbackLists = m_registeredCallbacksLists;
                if (callbackLists == null)
                    return 0;

                int count = 0;
                foreach(SparselyPopulatedArray<CancellationCallbackInfo> sparseArray in callbackLists)
                {
                    if(sparseArray != null)
                    {
                        SparselyPopulatedArrayFragment<CancellationCallbackInfo> currCallbacks = sparseArray.Head;
                        while (currCallbacks != null)
                        {
                            for (int i = 0; i < currCallbacks.Length; i++)
                                if (currCallbacks[i] != null)
                                    count++;

                            currCallbacks = currCallbacks.Next;
                        }
                    }
                }
                return count;
            }
        }                     private volatile SparselyPopulatedArray<CancellationCallbackInfo>[] m_registeredCallbacksLists;
*/
            // array of SparselyPopulatedArray
            if (tks.GetType().GetField("m_registeredCallbacksLists", bFlags).GetValue(tks) is Array reglist)
            {
                //var elemType = reglist.GetType().GetElementType(); // System.Threading.SparselyPopulatedArray`1[System.Threading.CancellationCallbackInfo]

                foreach (var oSparselyPopulatedArray in reglist)
                {
                    if (oSparselyPopulatedArray != null) // SparselyPopulatedArray
                    {
                        /*
                                             private readonly SparselyPopulatedArrayFragment<CancellationCallbackInfo> m_head;
                                             private volatile SparselyPopulatedArrayFragment<CancellationCallbackInfo> m_tail;
                        */
                        var m_head = oSparselyPopulatedArray.GetType().GetField("m_head", bFlags).GetValue(oSparselyPopulatedArray);
                        /*
                                internal readonly T[] m_elements; // The contents, sparsely populated (with nulls).
                                internal volatile int m_freeCount; // A hint of the number of free elements.
                                internal volatile SparselyPopulatedArrayFragment<T> m_next; // The next fragment in the chain.
                                internal volatile SparselyPopulatedArrayFragment<T> m_prev; // The previous fragment in the chain.
                         */

                        var curCallbacks = m_head;
                        while (curCallbacks != null)
                        {
                            var m_elements = curCallbacks.GetType().GetField("m_elements", bFlags).GetValue(curCallbacks) as Array; //CancellationCallbackInfo[]
                            foreach (var cancellationCallbackInfo in m_elements)
                            {
                                if (cancellationCallbackInfo?.GetType().GetField("Callback", bFlags).GetValue(cancellationCallbackInfo) is Delegate callback)
                                {
                                    var invocationList = callback.GetInvocationList();
                                    LogTestMessage($"Got invocationlist len = {invocationList.Length}");
                                    foreach (var targ in invocationList)
                                    {
                                        LogTestMessage($"  inv list target = {targ.Target}");
                                        var obj = targ.Target;
                                        //                                    _objectTracker.AddObjectToTrack(obj, ObjTracker.ObjSource.FromTextView, description: desc);
                                    }
                                }
                            }

                            curCallbacks = curCallbacks.GetType().GetField("")


                        }


                        //                        var m_tail = oSparselyPopulatedArray.GetType().GetField("m_tail", bFlags).GetValue(oSparselyPopulatedArray);



                        "".ToString();
                    }

                }
                for (int i = 0; i < reglist.Length; i++)
                {
                    //                    var arrelem = reglist[i];
                }
                LogTestMessage($"m_registeredCallbacksLists elemtype {elemType} {elemType.IsArray}");
                //   var sparseArray = reglist.GetType().getv

            }
            var linkedList = tks.GetType().GetField("m_linkingRegistrations", bFlags).GetValue(tks);
        }

        private class Someclass
        {
            public Someclass(CancellationToken token)
            {
                token.Register(Handlecancel);
                token.Register(() =>
                {

                });
            }
            void Handlecancel()
            {
            }
        }
        [TestMethod]
        public void TestCts()
        {
            using (var cts = new CancellationTokenSource())
            {

                //var newctsDoesNotLeak = new CancellationTokenSource(); // by itself (not from linked).. doesn't leak mem or handles

                //var myevent = CreateEvent(IntPtr.Zero, false, false, $"aa{i}"); // leaks kernel handles, this is used internally in CTS
                //CloseHandle(myevent); // must close else leaks kernel handles


                //var timer = new Timer((st) =>
                //{
                //}, state: 0, dueTime: 0, period: 1000);  //this leaks mem. 
                //timer.Dispose(); // must dispose else leaks. Not a CTS leak, but used internally



                //var mre = new ManualResetEvent(initialState: false);// leaks mem and handles. Not a CTS leak (used internally by CTS)




                //var cts1 = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[] { cts.Token });
                //cts1.Dispose(); // must dispose else leaks CTS Leak Type No. 1. Calling Cancel has no effect on the leak: still must call dispose



                var tk = cts.Token;
                var cancellationTokenRegistration = tk.Register(() =>
                {

                });
                var x = new Someclass(tk);
                var x2 = new Someclass(tk);
                var x3 = new Someclass(tk);
                ProcessCancellationToken(tk);
            }
            //cancellationTokenRegistration.Dispose(); // must dispose else leaks. CTS Leak Type No. 2




            //var newcts = new CancellationTokenSource();
            //newcts.CancelAfter(TimeSpan.FromMinutes(10)); // this leaks mem. 
            //newcts.Dispose(); // must dispost else leaks CTS Leak Type No. 3


            //var newcts = new CancellationTokenSource();
            //var handle = newcts.Token.WaitHandle; // this internally lazily instantiates a ManualResetEvent
            //newcts.Dispose(); // must dispose, else leaks mem and handles. CTS Leak Type No. 4


            //var newcts = new CancellationTokenSource();
            //var linked = CancellationTokenSource.CreateLinkedTokenSource(newcts.Token);
            //newcts.Dispose(); // this does not leak: disposing the original cts means the linked won't leak. However, not necessarily recommended


        }
    }
}
