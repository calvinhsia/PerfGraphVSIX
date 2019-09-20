using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public class ObjWeakRefData
        {
            static int g_baseSerialNo = 0;
            internal WeakReference<object> _wr;

            public string descriptor { get; private set; }
            public int _serialNo;
            public DateTime _dtCreated;
            public ObjWeakRefData(object obj, string description)
            {
                _dtCreated = DateTime.Now;
                _wr = new WeakReference<object>(obj);
                _serialNo = g_baseSerialNo;
                Interlocked.Increment(ref g_baseSerialNo);
                descriptor = $"{obj.GetType().Name} {description}".Trim();
            }
            /// <summary>
            /// Certain known objects have a flag when they're finished: e.g. IsClosed or _disposed.
            /// </summary>
            /// <returns></returns>
            public bool HasBeenClosedOrDisposed()
            {
                var hasBeenClosedOrDisposed = false;
                if (_wr.TryGetTarget(out var obj))
                {
                    var typeName = obj.GetType().Name; //"MyBigData"
                    bool fDidGetSpecialType = false;
                    switch (typeName)
                    {
                        case "WpfTextView":
                        case "Microsoft.VisualStudio.Text.Implementation.TextBuffer":
                            var IsClosedProp = obj.GetType().GetProperty("IsClosed");
                            var valIsClosedProp = IsClosedProp.GetValue(obj);
                            hasBeenClosedOrDisposed = (bool)valIsClosedProp;
                            fDidGetSpecialType = true;
                            break;
                    }
                    if (!fDidGetSpecialType)
                    {
                        var mems = obj.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var mem in mems
                            .Where(m => m.Name.IndexOf("disposed", comparisonType: StringComparison.OrdinalIgnoreCase) > 0 &&
                                    m.MemberType.HasFlag(MemberTypes.Field) || m.MemberType.HasFlag(MemberTypes.Property)))
                        {
                            if (mem is PropertyInfo propinfo && propinfo.PropertyType.Name == "Boolean") // the setter has Void PropertyType
                            {
                                var valIsDisposed = propinfo.GetValue(obj);
                                hasBeenClosedOrDisposed = (bool)valIsDisposed;
                                break;
                            }
                            else if (mem is FieldInfo fieldinfo && fieldinfo.FieldType.Name == "Boolean" && !mem.Name.Contains("BackingField"))
                            {
                                var valIsDisposed = fieldinfo.GetValue(obj);
                                hasBeenClosedOrDisposed = (bool)valIsDisposed;
                                break;
                            }
                            else if (mem is MethodInfo methInfo && methInfo.ReturnType.Name == "Boolean")
                            {
                                var valIsDisposed = methInfo.Invoke(obj, parameters: null);
                                hasBeenClosedOrDisposed = (bool)valIsDisposed;
                                break;
                            }
                        }
                    }
                }
                return hasBeenClosedOrDisposed;
            }
        }

        HashSet<ObjWeakRefData> _hashObjs = new HashSet<ObjWeakRefData>();
        ConcurrentQueue<object> _queue = new ConcurrentQueue<object>();

        /// <summary>
        /// Called from native code from any thread. minimize any memory allocations
        /// We'll queue the objs on any random thread, then dequeue to HashSet when needed
        /// </summary>
        public void AddObjectToTrack(object obj, string description = null)
        {
            _queue.Enqueue(new ObjWeakRefData(obj, description));
        }

        public (Dictionary<string, int>, List<ObjWeakRefData>) GetCounts()
        {
            while (_queue.TryDequeue(out var obj))
            {
                var o = obj as ObjWeakRefData;
                if (o._wr.TryGetTarget(out var _)) // has it been GC'd?
                {
                    _hashObjs.Add(o); // still alive
                }
            }
            var dictLiveObjs = new Dictionary<string, int>(); // classname + desc, count
            var lstDeadObjs = new List<ObjWeakRefData>();
            var lstLeakedObjs = new List<ObjWeakRefData>();
            foreach (var itm in _hashObjs)
            {
                if (itm._wr.TryGetTarget(out var objdata))
                { // the obj is still in memory. Has it been closed or disposed?
                    if (itm.HasBeenClosedOrDisposed())
                    {
                        lstLeakedObjs.Add(itm);
                    }
                    else
                    {
                        dictLiveObjs.TryGetValue(itm.descriptor, out var cnt);
                        dictLiveObjs[itm.descriptor] = ++cnt;
                    }
                }
                else
                {
                    lstDeadObjs.Add(itm);
                }
            }
            foreach (var entry in lstDeadObjs)
            {
                _hashObjs.Remove(entry);
            }
            return (dictLiveObjs, lstLeakedObjs);
        }

        // doesn't need to be done if GetCounts is called regularly
        public void Cleanup()
        {
            var lstGCObjs = new List<ObjWeakRefData>(); // create new outside lock
            lock (_hashObjs)
            {
                foreach (var entry in _hashObjs)
                {
                    if (!entry._wr.TryGetTarget(out var _)) // if it doesn't get obj, it has been GC'd.
                    {
                        lstGCObjs.Add(entry);
                    }
                }
                foreach (var entry in lstGCObjs)
                {
                    _hashObjs.Remove(entry);
                }
            }
        }
    }

}
