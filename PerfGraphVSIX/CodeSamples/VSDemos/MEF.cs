//Desc: MEF: use MEF to obtain components. Doesn't work yet

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll
//Ref: %VSRoot%\Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.Text.UI.dll
//Ref: %VSRoot%\Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.Text.Data.dll
//Ref: %VSRoot%\Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.CoreUtility.dll

//Include: ..\Util\MyCodeBaseClass.cs


using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using EnvDTE;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MyCodeToExecute
{
    public class MySimpleSample : MyCodeBaseClass
    {
        Guid _guidPane = new Guid("{CEEAB38D-8BC4-4675-9DFD-993BBE9996A5}");
        public IVsOutputWindowPane _OutputPane;

        public static async Task DoMain(object[] args)
        {
            var oMySimpleSample = new MySimpleSample(args);
            await oMySimpleSample.DoInitializeAsync();
        }
        MySimpleSample(object[] args) : base(args) { }
        async Task DoInitializeAsync()
        {
            IVsOutputWindow outputWindow = await _asyncServiceProvider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var crPane = outputWindow.CreatePane(
                ref _guidPane,
                "PerfGraphVSIX",
                fInitVisible: 1,
                fClearWithSolution: 0);
            outputWindow.GetPane(ref _guidPane, out _OutputPane);
            _OutputPane.Clear();
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default; // switch to background thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();//outputpane must be called from main thread
                _OutputPane.OutputString("Here in MySimpleSample " + DateTime.Now.ToString("MM/dd/yy hh:mm:ss"));
                await TaskScheduler.Default; // switch to background thread
            });
            var ComponentModel = (await _asyncServiceProvider.GetServiceAsync(typeof(SComponentModel))) as IComponentModel;
            _logger.LogMessage("CompModel: " + ComponentModel.ToString());
            var exportProvider = ComponentModel.DefaultExportProvider;
            var compService = ComponentModel.DefaultCompositionService;
            var xx = ComponentModel.GetService<MyMefComponent>();
            //            var MyMef = ComponentModel.GetService<MyMefComponent>();
        }
    }

    [Export(typeof(MyMefComponent))]
    [Export(typeof(ITextViewCreationListener))]
    [ContentType(StandardContentTypeNames.Any)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [TextViewRole(PredefinedTextViewRoles.PreviewTextView)]
    [TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TextViewRole(PredefinedTextViewRoles.CodeDefinitionView)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class MyMefComponent
    {
        internal ITextDocumentFactoryService textDocumentFactoryService;
        [ImportingConstructor]
        public MyMefComponent([Import] ITextBufferFactoryService _textBufferFactoryService,
                              [Import] IProjectionBufferFactoryService _projectionBufferFactoryService,
                              [Import] ITextDocumentFactoryService textDocumentFactoryService)
        {
            this.textDocumentFactoryService = textDocumentFactoryService;
            _textBufferFactoryService.TextBufferCreated += (o, e) =>
            {
                try
                {
                }
                catch (Exception)
                {
                }
            };
            _projectionBufferFactoryService.ProjectionBufferCreated += (o, e) =>
            {
                try
                {
                }
                catch (Exception)
                {
                }
            };
        }
        public void TextViewCreated(ITextView textView)
        {
            try
            {
            }
            catch (Exception)
            {
            }
        }


        public void Initialize(MySimpleSample mySimpleSample)
        {
            mySimpleSample._logger.LogMessage("From MyMefComponent");
        }

    }
}
