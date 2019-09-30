using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace PerfGraphVSIX
{
    /// <summary>
    /// Class to track objects with weak references
    /// You can get the count of objects grouped by object type
    /// If the type is a particular type, like Microsoft.VisualStudio.Text.Implementation.TextBuffer
    ///   it uses reflection to get the IsClosed property. A Closed TextBuffer still in memory is likely a leak.
    /// If the type has a bool field or property named "*disposed*" then it's considered a leak if disposed
    /// 
    /// </summary>
    public class ObjTracker
    {
        readonly Dictionary<int, ObjWeakRefData> _dictObjsToTrack = new Dictionary<int, ObjWeakRefData>();
        ConcurrentQueue<object> _queue = new ConcurrentQueue<object>();
        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        internal readonly PerfGraphToolWindowControl _perfGraph;

        public ObjTracker(PerfGraphToolWindowControl perfGraph)
        {
            this._perfGraph = perfGraph;
            perfGraph.btnClearObjects.Click += (o, e) =>
             {
                 var tsk = perfGraph.AddStatusMsgAsync($"Clearing the tracking of {_dictObjsToTrack.Count} tracked objects.");
                 _dictObjsToTrack.Clear();
                 var newq = new ConcurrentQueue<object>();
                 Interlocked.Exchange(ref _queue, newq);
             };
        }

        /// <summary>
        /// Called from native code from any thread. minimize any memory allocations
        /// We'll queue the objs on any random thread, then dequeue to HashSet when needed
        /// </summary>
        public void AddObjectToTrack(object obj, ObjSource objSource, string description = null)
        {
            if (obj != null)
            {
                _queue.Enqueue(new ObjWeakRefData(obj, objSource, description));
            }
        }

        public (Dictionary<string, int>, List<ObjWeakRefData>) GetCounts()
        {
            while (_queue.TryDequeue(out var obj))
            {
                var o = obj as ObjWeakRefData;
                if (o._wr.TryGetTarget(out var _)) // has it been GC'd?
                {
                    if (!_dictObjsToTrack.ContainsKey(o._hashCodeTarget))
                    {
                        _dictObjsToTrack[o._hashCodeTarget] = o; // still alive
                    }
                }
            }
            var dictLiveObjs = new Dictionary<string, int>(); // classname + desc, count
            var lstDeadObjs = new List<ObjWeakRefData>();
            var lstLeakedObjs = new List<ObjWeakRefData>();
            foreach (var wkrefData in _dictObjsToTrack.Values)
            {
                if (wkrefData._wr.TryGetTarget(out var objTracked))
                { // the obj is still in memory. Has it been closed or disposed?
                    if (_perfGraph.TrackTextViews && wkrefData._objSource == ObjSource.FromTextView ||
                        _perfGraph.TrackProjectObjects && wkrefData._objSource == ObjSource.FromProject
                        )
                    {
                        if (string.IsNullOrEmpty(_perfGraph.ObjectTrackerFilter) ||
                            Regex.IsMatch(wkrefData.Descriptor, _perfGraph.ObjectTrackerFilter.Trim(), RegexOptions.IgnoreCase))
                        {
                            ProcessSpecialTypes(objTracked);
                            if (wkrefData.HasBeenClosedOrDisposed())
                            {
                                lstLeakedObjs.Add(wkrefData);
                            }
                            else
                            {
                                dictLiveObjs.TryGetValue(wkrefData.Descriptor, out var cnt);
                                dictLiveObjs[wkrefData.Descriptor] = ++cnt;
                            }
                        }
                    }
                }
                else
                {
                    lstDeadObjs.Add(wkrefData);
                }
            }
            foreach (var entry in lstDeadObjs)
            {
                _dictObjsToTrack.Remove(entry._hashCodeTarget);
            }
            return (dictLiveObjs, lstLeakedObjs);
        }

        /// <summary>
        /// Some tracked objects have references to other objs which we want to track. e.g. a TextView has a property bag with ref to EditorOptions. A workspace has a CancellationTokenSource
        /// Some of these property bags or CTS will change after our initial tracking, so we need to refresh these
        /// </summary>
        /// <param name="objTracked"></param>
        private void ProcessSpecialTypes(object objTracked)
        {
            if (objTracked is CancellationToken ctoken)
            {
                var (nReg, nLinked) = ProcessCancellationToken(ctoken, (s) =>
                {
                });
                var tsk = _perfGraph.AddStatusMsgAsync($"# disposeToken callback Registrations = {nReg} Linked = {nLinked}");
                //var tks = disposeToken.GetType().GetField("m_source", bFlags).GetValue(disposeToken);
                //var reglist = tks.GetType().GetField("m_registeredCallbacksLists", bFlags).GetValue(tks);
                //var elemType = reglist.GetType().GetElementType();

                //var linkedList = tks.GetType().GetField("m_linkingRegistrations", bFlags).GetValue(tks);

            }
            else if (objTracked is ITextView textView)
            {
                if (_perfGraph.TrackTextViews)
                {
                    var propBag = textView.GetType().GetField("_properties", bFlags).GetValue(textView);
                    var propList = propBag.GetType().GetField("properties", bFlags).GetValue(propBag) as HybridDictionary;
                    foreach (var val in propList.Values)
                    {
                        if (val != null)
                        {
                            var valType = val.GetType();
                            foreach (var fld in valType.GetFields(bFlags))
                            {
                                var valFld = fld.GetValue(val);
                                if (fld.FieldType.BaseType?.FullName == "System.Delegate")
                                {
                                    "".ToString();
                                }
                                if (fld.FieldType.BaseType?.FullName == "System.MulticastDelegate")
                                {
                                    HandleEvent(val, fld.Name, "");
                                    "".ToString();
                                }
                                else if (fld.FieldType.BaseType?.FullName == "System.Object")
                                {
                                    //                            TryAddObjectVisited(valFld);
                                    "".ToString();
                                }
                            }
                            if (valType.Name == "EditorOptions")
                            {
                                HandleEvent(val, "OptionChanged", "TextView.EditorOptions+=");
                            }
                        }
                    }
                    HandleEvent(textView.TextBuffer, "Changed", "TextBuffer+=");
                    HandleEvent(textView.TextBuffer, "ChangedLowPriority", "TextBuffer+=");
                    HandleEvent(textView.TextBuffer, "ChangedHighPriority", "TextBuffer+=");
                    HandleEvent(textView.TextBuffer, "ReadOnlyRegionsChanged", "TextBuffer+=");

                    HandleEvent(textView, "Closed", "TextView.Closed+=");
                }
            }
        }

        // uses reflection to find any referenced linked tokensources or registered callbacks
        public (int, int) ProcessCancellationToken(CancellationToken token, Action<string> logger)
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

        // doesn't need to be done if GetCounts is called regularly
        public void Cleanup()
        {
            var lstGCObjs = new List<ObjWeakRefData>(); // create new outside lock
            lock (_dictObjsToTrack)
            {
                foreach (var entry in _dictObjsToTrack.Values)
                {
                    if (!entry._wr.TryGetTarget(out var _)) // if it doesn't get obj, it has been GC'd.
                    {
                        lstGCObjs.Add(entry);
                    }
                }
                foreach (var entry in lstGCObjs)
                {
                    _dictObjsToTrack.Remove(entry._hashCodeTarget);
                }
            }
        }

        internal void HandleEvent(object objInstance, string eventName, string desc)
        {
            var eventField = objInstance.GetType().GetField(eventName, bFlags)?.GetValue(objInstance) as Delegate;
            var invocationList = eventField?.GetInvocationList();
            if (invocationList != null)
            {
                foreach (var targ in invocationList)
                {
                    var obj = targ.Target;
                    AddObjectToTrack(obj, ObjSource.FromTextView, description: desc);
                }
            }
        }
    }

}
