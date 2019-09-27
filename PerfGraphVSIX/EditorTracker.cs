using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    [Export(typeof(EditorTracker))]
    [Export(typeof(ITextViewCreationListener))]
    [ContentType(StandardContentTypeNames.Any)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [TextViewRole(PredefinedTextViewRoles.PreviewTextView)]
    [TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TextViewRole(PredefinedTextViewRoles.CodeDefinitionView)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    class EditorTracker : ITextViewCreationListener
    {
        internal ITextDocumentFactoryService textDocumentFactoryService;
        readonly HashSet<TextViewInstanceData> _hashViews = new HashSet<TextViewInstanceData>();

        [ImportingConstructor]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        EditorTracker(ITextDocumentFactoryService textDocumentFactoryService)
        {
            this.textDocumentFactoryService = textDocumentFactoryService;
        }

        int _nSerialNo;
        private ObjTracker _objectTracker;
        PerfGraphToolWindowControl _perfGraph;

        internal class TextViewInstanceData
        {
            public string _filename;
            public string _contentType;
            public WeakReference<ITextView> _wrView;
            public int _serialNo;
            public DateTime _dtCreated;
            public TextViewInstanceData(ITextView textView, string filename, int serialNo)
            {
                _wrView = new WeakReference<ITextView>(textView);
                _filename = filename;
                _serialNo = serialNo;
                _contentType = textView.TextDataModel?.ContentType?.TypeName ?? "null";
                _dtCreated = DateTime.Now;
            }
            public ITextView GetView()
            {
                if (_wrView.TryGetTarget(out ITextView ret)) // if it doesn't get view, it has been GC'd.
                {
                }
                return ret;
            }
        }

        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;

        public void TextViewCreated(ITextView textView)
        {
            try
            {
                if (!_perfGraph.TrackTextViews)
                {
                    return;
                }
                if (TryGetFileName(textView, out var filename))
                {
                }
                var instData = new TextViewInstanceData(textView, filename, _nSerialNo++);
                _hashViews.Add(instData);
                hashVisitedObjs = new HashSet<object>();
                //AddMemsOfObject(textView);

                if (_perfGraph.TrackContainedObjects)
                {
                    var propBag = textView.GetType().GetField("_properties", bFlags).GetValue(textView);
                    var propList = propBag.GetType().GetField("properties", bFlags).GetValue(propBag) as HybridDictionary;
                    foreach (var val in propList.Values)
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
                    HandleEvent(textView.TextBuffer, "Changed", "TextBuffer+=");
                    HandleEvent(textView.TextBuffer, "ChangedLowPriority", "TextBuffer+=");
                    HandleEvent(textView.TextBuffer, "ChangedHighPriority", "TextBuffer+=");
                    HandleEvent(textView.TextBuffer, "ReadOnlyRegionsChanged", "TextBuffer+=");

                    HandleEvent(textView, "Closed", "TextView.Closed+=");
                }
            }
            catch (Exception)
            {
            }
        }

        internal HashSet<object> hashVisitedObjs;
        //bool TryAddObjectVisited(object obj)
        //{
        //    var fDidAdd = false;
        //    if (!hashVisitedObjs.Contains(obj))
        //    {
        //        hashVisitedObjs.Add(obj);
        //        _objectTracker.AddObjectToTrack(obj);
        //        fDidAdd = true;
        //    }
        //    return fDidAdd;
        //}

        void AddMemsOfObject(object obj, int nLevel = 1)
        {
            var objTyp = obj.GetType();
            if (obj != null && objTyp.IsClass && objTyp.FullName != "System.String")
            {
                if (objTyp.IsArray)
                {
                    var elemtyp = objTyp.GetElementType();
                    if (elemtyp.IsPrimitive || elemtyp.Name == "String")
                    {
                        return;
                    }
                }
                if (hashVisitedObjs.Contains(obj))
                {
                    return;
                }
                hashVisitedObjs.Add(obj);
                if (objTyp.Module.Name != "mscorlib.dll")
                {
                    _objectTracker.AddObjectToTrack(obj, ObjTracker.ObjSource.FromTextView);
                }
                if (nLevel < 1000)
                {
                    var members = objTyp.GetMembers(bFlags).Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property);
                    foreach (var mem in members)
                    {
                        if (mem is FieldInfo fldInfo)
                        {
                            var valFld = fldInfo.GetValue(obj);
                            if (valFld != null)
                            {
                                var valFldType = valFld.GetType();
                                if (valFld.GetType().IsClass) // delegate or class (not value type or interfacer
                                {
                                    var name = objTyp.FullName;
                                    switch (name)
                                    {
                                        case "System.Reflection.Pointer":
                                        case "System.String":
                                            break;
                                        default:
                                            //                                                        LogTestMessage($"{new string(' ', nLevel)} {nLevel} {objTyp.Name} {fldInfo.Name} {fldInfo.FieldType.BaseType?.Name}  {valFld.GetType().Name}");
                                            if (valFld is EventHandler evHandler)
                                            {
                                                "".ToString();
                                            }
                                            if (valFld is Object)
                                            {
                                                AddMemsOfObject(valFld, nLevel + 1);
                                            }
                                            break;

                                    }
                                }
                            }
                        }
                        else if (mem is PropertyInfo propInfo)
                        {
                            "".ToString();
                            try
                            {
                                if (!propInfo.PropertyType.IsPrimitive)
                                {
                                    var valProp = propInfo.GetValue(obj); // dictionary.item[]
                                    AddMemsOfObject(valProp, nLevel + 1);
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }
        }

        void HandleEvent(object objInstance, string eventName, string desc)
        {
            var eventField = objInstance.GetType().GetField(eventName, bFlags)?.GetValue(objInstance) as Delegate;
            var invocationList = eventField?.GetInvocationList();
            if (invocationList != null)
            {
                foreach (var targ in invocationList)
                {
                    var obj = targ.Target;
                    _objectTracker.AddObjectToTrack(obj, ObjTracker.ObjSource.FromTextView, description: desc);
                }
            }
        }

        internal (Dictionary<string, int>, List<TextViewInstanceData>) GetCounts()
        {
            var dictOpen = new Dictionary<string, int>();
            var lstLeaked = new List<TextViewInstanceData>();
            var lstDeadViews = new List<TextViewInstanceData>();
            if (!_perfGraph.TrackTextViews)
            {
                _hashViews.Clear();
            }
            foreach (var entry in _hashViews)
            {
                var view = entry.GetView();
                if (view == null)
                {
                    lstDeadViews.Add(entry);
                }
                else
                {
                    var typView = view.GetType();
                    var IsClosedProp = typView.GetProperty("IsClosed");
                    var valIsClosedProp = IsClosedProp.GetValue(view);
                    if (!(bool)valIsClosedProp)
                    //                    if (!view.IsClosed)
                    {
                        dictOpen.TryGetValue(entry._contentType, out int cnt);
                        dictOpen[entry._contentType] = ++cnt;
                    }
                    else
                    {
                        lstLeaked.Add(entry);
                    }
                    //var dict = view.IsClosed ? dictLeaked : dictOpen;
                    //dict.TryGetValue(contentType, out int cnt);
                    //dict[contentType] = ++cnt;
                }
            }
            foreach (var entry in lstDeadViews)
            {
                _hashViews.Remove(entry);
            }
            return (dictOpen, lstLeaked.OrderByDescending(k => k._serialNo).ToList());
        }

        private bool TryGetFileName(ITextView textView, out string filePath)
        {
            if (this.textDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out var textDocument))
            {
                filePath = textDocument.FilePath;
                return true;
            }

            filePath = string.Empty;
            return false;
        }

        internal void Initialize(PerfGraphToolWindowControl perfGraph, ObjTracker objTracker)
        {
            _objectTracker = objTracker;
            _perfGraph = perfGraph;
        }

        //void Cleanup()
        //{
        //    lock (_hashViews)
        //    {
        //        var lstGCViews = new List<WeakReference<ITextView>>();
        //        foreach (var entry in _hashViews)
        //        {
        //            if (!entry.TryGetTarget(out var view)) // if it doesn't get view, it has been GC'd.
        //            {
        //                lstGCViews.Add(entry);
        //            }
        //        }
        //        foreach (var entry in lstGCViews)
        //        {
        //            _hashViews.Remove(entry);
        //        }
        //    }
        //}

    }
}
