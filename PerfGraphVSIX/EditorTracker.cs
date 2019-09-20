﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
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
        HashSet<textViewInstanceData> _hashViews = new HashSet<textViewInstanceData>();

        [ImportingConstructor]
        EditorTracker(ITextDocumentFactoryService textDocumentFactoryService)
        {
            this.textDocumentFactoryService = textDocumentFactoryService;
        }

        int _nSerialNo;
        internal class textViewInstanceData
        {
            public string _filename;
            public string _contentType;
            public WeakReference<ITextView> _wrView;
            public int _serialNo;
            public DateTime _dtCreated;
            public textViewInstanceData(ITextView textView, string filename, int serialNo)
            {
                _wrView = new WeakReference<ITextView>(textView);
                _filename = filename;
                _serialNo = serialNo;
                _contentType = textView.TextDataModel?.ContentType?.TypeName ?? "null";
                _dtCreated = DateTime.Now;
            }
            public ITextView GetView()
            {
                ITextView ret = null;
                if (_wrView.TryGetTarget(out ret)) // if it doesn't get view, it has been GC'd.
                {
                }
                return ret;
            }
        }
        public void TextViewCreated(ITextView textView)
        {
            PerfGraph.Instance._objTracker.AddObjectToTrack(textView);
            if (TryGetFileName(textView, out var filename))
            {

            }
            _hashViews.Add(new textViewInstanceData(textView, filename, _nSerialNo++));
            //if (_hashViews.Count > 100)
            //{
            //    Cleanup();
            //}
        }

        internal (Dictionary<string, int>, List<textViewInstanceData>) GetCounts()
        {
            var dictOpen = new Dictionary<string, int>();
            var lstLeaked = new List<textViewInstanceData>();
            var lstDeadViews = new List<textViewInstanceData>();
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
                    var valIsClosedProp =IsClosedProp.GetValue(view) ;
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
            return (dictOpen, lstLeaked.OrderByDescending(k=>k._serialNo).ToList());
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
