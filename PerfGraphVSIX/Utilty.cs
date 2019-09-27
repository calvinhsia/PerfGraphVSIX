using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    class Utilty
    {
        static readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        // uses reflection to find any referenced linked tokensources or registered callbacks
        public static (int, int) ProcessCancellationToken(CancellationToken token, Action<string> logger)
        {
            int nRegCallbacks = 0;
            int nLinkedTokens = 0;
            logger($"Processing tkn {token}");
            var tks = token.GetType().GetField("m_source", bFlags).GetValue(token) as CancellationTokenSource;
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
                        var m_head = oSparselyPopulatedArray.GetType().GetField("m_head", bFlags).GetValue(oSparselyPopulatedArray); // SparselyPopulatedArrayFragment<CancellationCallbackInfo>
                        /*
                                internal readonly T[] m_elements; // The contents, sparsely populated (with nulls).
                                internal volatile int m_freeCount; // A hint of the number of free elements.
                                internal volatile SparselyPopulatedArrayFragment<T> m_next; // The next fragment in the chain.
                                internal volatile SparselyPopulatedArrayFragment<T> m_prev; // The previous fragment in the chain.
                         */

                        var curCallbacks = m_head; //SparselyPopulatedArrayFragment<CancellationCallbackInfo>
                        while (curCallbacks != null)
                        {
                            var m_elements = curCallbacks.GetType().GetField("m_elements", bFlags).GetValue(curCallbacks) as Array; //CancellationCallbackInfo[]
                            foreach (var cancellationCallbackInfo in m_elements)
                            {
                                if (cancellationCallbackInfo?.GetType().GetField("Callback", bFlags).GetValue(cancellationCallbackInfo) is Delegate callback)
                                {
                                    var invocationList = callback.GetInvocationList();
                                    logger($"Got invocationlist len = {invocationList.Length}");
                                    foreach (var targ in invocationList)
                                    {
                                        nRegCallbacks++;
                                        logger($"  inv list target = {targ.Target}");
                                        var objTarg = targ.Target;
                                        if (objTarg != null)
                                        {

                                        }
                                        //                                    _objectTracker.AddObjectToTrack(obj, ObjTracker.ObjSource.FromTextView, description: desc);
                                    }
                                }
                            }

                            curCallbacks = curCallbacks.GetType().GetField("m_next", bFlags).GetValue(curCallbacks);
                        }
                        //                        var m_tail = oSparselyPopulatedArray.GetType().GetField("m_tail", bFlags).GetValue(oSparselyPopulatedArray);
                    }

                }

            }
            var linkedList = tks.GetType().GetField("m_linkingRegistrations", bFlags).GetValue(tks);
            return (nRegCallbacks, nLinkedTokens);
        }
    }
}
