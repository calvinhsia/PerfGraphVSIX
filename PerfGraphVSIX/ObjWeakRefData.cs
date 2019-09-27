using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace PerfGraphVSIX
{
    public enum ObjSource
    {
        FromTextView,
        FromProject,
        FromTest
    }

    //public class CancellationTokenWeakRefData : ObjWeakRefData
    //{
    //    public CancellationTokenWeakRefData(object obj, ObjSource objSource, string description) : base(obj, objSource, description)
    //    {
    //    }
    //}

    //public class EventHandlerWeakRefData : ObjWeakRefData
    //{
    //    public EventHandlerWeakRefData(object obj, ObjSource objSource, string description) : base(obj, objSource, description)
    //    {
    //    }
    //}
    public class ObjWeakRefData
    {
        static int g_baseSerialNo = 0;
        internal WeakReference<object> _wr;
        internal int _hashCodeTarget;

        public string Descriptor { get; private set; }
        public int _serialNo;
        public DateTime _dtCreated;
        public readonly ObjSource _objSource;

        public ObjWeakRefData(object obj, ObjSource objSource, string description)
        {
            _dtCreated = DateTime.Now;
            _objSource = objSource;
            _wr = new WeakReference<object>(obj);
            _serialNo = g_baseSerialNo;
            _hashCodeTarget = obj.GetHashCode();
            Interlocked.Increment(ref g_baseSerialNo);
            Descriptor = $"{obj.GetType().FullName} {description}".Trim();
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
                    case "Microsoft.VisualStudio.Text.Editor.Implementation.WpfTextView":
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
                                (m.MemberType.HasFlag(MemberTypes.Field) || m.MemberType.HasFlag(MemberTypes.Property))))
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
        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;

        internal void ProcessSpecialTypes(object objTracked, ObjTracker objTracker)
        {
            if (objTracked is CancellationToken ctoken)
            {

            }
            else if (objTracked is ITextView textView)
            {
                if (objTracker._perfGraph.TrackTextViews)
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
                                    objTracker.HandleEvent(val, fld.Name, "");
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
                                objTracker.HandleEvent(val, "OptionChanged", "TextView.EditorOptions+=");
                            }
                        }
                    }
                    objTracker.HandleEvent(textView.TextBuffer, "Changed", "TextBuffer+=");
                    objTracker.HandleEvent(textView.TextBuffer, "ChangedLowPriority", "TextBuffer+=");
                    objTracker.HandleEvent(textView.TextBuffer, "ChangedHighPriority", "TextBuffer+=");
                    objTracker.HandleEvent(textView.TextBuffer, "ReadOnlyRegionsChanged", "TextBuffer+=");

                    objTracker.HandleEvent(textView, "Closed", "TextView.Closed+=");

                }
            }
        }
        //void HandleEvent(object objInstance, string eventName, string desc)
        //{
        //    var eventField = objInstance.GetType().GetField(eventName, bFlags)?.GetValue(objInstance) as Delegate;
        //    var invocationList = eventField?.GetInvocationList();
        //    if (invocationList != null)
        //    {
        //        foreach (var targ in invocationList)
        //        {
        //            var obj = targ.Target;
        //            _objectTracker.AddObjectToTrack(obj, ObjSource.FromTextView, description: desc);
        //        }
        //    }
        //}

        public override string ToString()
        {
            return $"{_serialNo} {Descriptor}";
        }
    }
}