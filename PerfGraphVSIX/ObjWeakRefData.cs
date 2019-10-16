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
        FromTest,
        FromTextBufferFactoryService,
        FromProjectionBufferFactoryService
    }

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
        /// todo: OnceInitializedOnceDisposed
        /// </summary>
        /// <returns></returns>
        public bool HasBeenClosedOrDisposed()
        {
            var hasBeenClosedOrDisposed = false;
            if (_wr.TryGetTarget(out var obj))
            {
                var typeName = obj.GetType().Name;
                bool fDidGetSpecialType = false;
                switch (typeName)
                {
                    case "Microsoft.VisualStudio.Text.Editor.Implementation.WpfTextView":
                    case "Microsoft.VisualStudio.Text.Implementation.TextBuffer":
                        hasBeenClosedOrDisposed = (bool)obj.GetType().GetProperty("IsClosed").GetValue(obj);
                        fDidGetSpecialType = true;
                        break;
                }
                if (!fDidGetSpecialType)
                {
                    var mems = obj.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var mem in mems
                        .Where(m => (m.MemberType.HasFlag(MemberTypes.Field) || m.MemberType.HasFlag(MemberTypes.Property)) &&
                                m.Name.IndexOf("disposed", comparisonType: StringComparison.OrdinalIgnoreCase) > 0 ))
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

        public override string ToString()
        {
            return $"{_serialNo} {Descriptor}";
        }
    }
}