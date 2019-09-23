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

        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        public void TextViewCreated(ITextView textView)
        {
            if (TryGetFileName(textView, out var filename))
            {
            }
            var instData = new TextViewInstanceData(textView, filename, _nSerialNo++);
            _hashViews.Add(instData);
            var hashVisitedObjs = new HashSet<object>();
            bool TryAddObjectVisited(object obj)
            {
                var fDidAdd = false;
                if (!hashVisitedObjs.Contains(obj))
                {
                    hashVisitedObjs.Add(obj);
                    _objectTracker.AddObjectToTrack(obj);
                    fDidAdd = true;
                }
                return fDidAdd;
            }
            void AddMemsOfObject(object obj, int nLevel = 1)
            {
                if (TryAddObjectVisited(obj))
                {
                    foreach (var fld in obj.GetType().GetFields(bFlags))
                    {
                        var valFld = fld.GetValue(obj);
                        if (valFld != null)
                        {
                            Debug.WriteLine($"{new string(' ', nLevel)} {obj.GetType().Name} {fld.Name} {fld.FieldType.BaseType?.Name}  {valFld.GetType().Name}");
                            if (valFld is EventHandler evHandler)
                            {
                                "".ToArray();
                            }
                            if (valFld is Object)
                            {
                                AddMemsOfObject(valFld, nLevel + 1);
                            }
                        }
                    }
                }
            }
            AddMemsOfObject(textView);
            if (textView.GetType().Name == "________WpfTextView")
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
                            TryAddObjectVisited(valFld);
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

        void HandleEvent(object objInstance, string eventName, string desc)
        {
            var eventField = objInstance.GetType().GetField(eventName, bFlags)?.GetValue(objInstance) as Delegate;
            var invocationList = eventField?.GetInvocationList();
            if (invocationList != null)
            {
                foreach (var targ in invocationList)
                {
                    var obj = targ.Target;
                    _objectTracker.AddObjectToTrack(obj, description: desc);
                }
            }
        }

        internal (Dictionary<string, int>, List<TextViewInstanceData>) GetCounts()
        {
            var dictOpen = new Dictionary<string, int>();
            var lstLeaked = new List<TextViewInstanceData>();
            var lstDeadViews = new List<TextViewInstanceData>();
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

        internal void SetObjectTracker(ObjTracker objTracker)
        {
            _objectTracker = objTracker;
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
