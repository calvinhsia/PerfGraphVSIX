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
        HashSet<WeakReference<ITextView>> _hashViews = new HashSet<WeakReference<ITextView>>();
        [ImportingConstructor]
        EditorTracker()
        {
        }
        public void TextViewCreated(ITextView textView)
        {
            _hashViews.Add(new WeakReference<ITextView>(textView));
            //if (_hashViews.Count > 100)
            //{
            //    Cleanup();
            //}
        }

        internal (Dictionary<string, int>, Dictionary<string, int>) GetCounts()
        {
            var dictOpen = new Dictionary<string, int>();
            var dictLeaked = new Dictionary<string, int>();
            var lstDeadViews = new List<WeakReference<ITextView>>();
            foreach (var entry in _hashViews)
            {
                if (!entry.TryGetTarget(out var view)) // if it doesn't get view, it has been GC'd.
                {
                    lstDeadViews.Add(entry);
                }
                else
                {
                    var dict = view.IsClosed ? dictLeaked : dictOpen;
                    var contentType = view.TextDataModel?.ContentType?.TypeName ?? "null";
                    dict.TryGetValue(contentType, out int cnt);
                    dict[contentType] = ++cnt;
                }
            }
            foreach (var entry in lstDeadViews)
            {
                _hashViews.Remove(entry);
            }
            return (dictOpen, dictLeaked);
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
