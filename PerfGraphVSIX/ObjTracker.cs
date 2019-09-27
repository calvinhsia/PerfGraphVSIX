using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

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
        readonly ConcurrentQueue<object> _queue = new ConcurrentQueue<object>();
        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        internal readonly PerfGraphToolWindowControl _perfGraph;

        public ObjTracker(PerfGraphToolWindowControl perfGraph)
        {
            this._perfGraph = perfGraph;
            perfGraph.btnClearObjects.Click += (o, e) =>
             {
                 var tsk = perfGraph.AddStatusMsgAsync($"Clearing the tracking of {_dictObjsToTrack.Count} tracked objects.");
                 _dictObjsToTrack.Clear();
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
                            Regex.IsMatch(wkrefData.Descriptor,_perfGraph.ObjectTrackerFilter.Trim(), RegexOptions.IgnoreCase))
                        {
                            wkrefData.ProcessSpecialTypes(objTracked, this);
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
