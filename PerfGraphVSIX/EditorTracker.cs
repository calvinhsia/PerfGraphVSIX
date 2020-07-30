using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

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

        //[ImportingConstructor]
        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        //EditorTracker(ITextDocumentFactoryService textDocumentFactoryService)
        //{
        //    this.textDocumentFactoryService = textDocumentFactoryService;
        //}

        [ImportingConstructor]
        public EditorTracker([Import] ITextBufferFactoryService _textBufferFactoryService,
                              [Import] IProjectionBufferFactoryService _projectionBufferFactoryService,
                              [Import] ITextDocumentFactoryService textDocumentFactoryService)
        {
            this.textDocumentFactoryService = textDocumentFactoryService;
            _textBufferFactoryService.TextBufferCreated += (o, e) =>
            {
                try
                {
                    _objectTracker?.AddObjectToTrack(e.TextBuffer, ObjSource.FromTextBufferFactoryService, description: e.TextBuffer.ContentType.DisplayName);
                }
                catch (Exception)
                {
                }
            };
            _projectionBufferFactoryService.ProjectionBufferCreated += (o, e) =>
            {
                try
                {
                    _objectTracker?.AddObjectToTrack(e.TextBuffer, ObjSource.FromProjectionBufferFactoryService, description: e.TextBuffer.ContentType.DisplayName);
                }
                catch (Exception)
                {
                }
            };
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

        internal void Initialize(PerfGraphToolWindowControl perfGraph, ObjTracker objTracker)
        {
            _objectTracker = objTracker;
            _perfGraph = perfGraph;
        }

        public void TextViewCreated(ITextView textView)
        {
            try
            {
                if (_perfGraph == null || !_perfGraph.TrackTextViews)
                {
                    return;
                }
                if (TryGetFileName(textView, out var filename))
                {
                }
                var instData = new TextViewInstanceData(textView, filename, _nSerialNo++);
                _hashViews.Add(instData);
                _objectTracker.AddObjectToTrack(textView, ObjSource.FromTextView, description: filename);
            }
            catch (Exception)
            {
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
