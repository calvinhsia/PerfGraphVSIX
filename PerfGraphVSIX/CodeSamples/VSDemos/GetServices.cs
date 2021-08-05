//Desc: sample to demonstrate Getting VS services and monitoring selection events
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//Include: ..\Util\MyCodeBaseClass.cs


using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

/* This sample allows you to edit/compile/run code inside the VS process from within the same instance of VS
 * You can access VS Services, JTF, etc with the same code as you would from e.g. building a VS component
 * but the Edit/Build/Run cycle is much smaller and faster
 * Intellisense mostly works. Debugging is mostly via logging or output window pane.
 * */
namespace MyCodeToExecute
{
    public class MySimpleSample : MyCodeBaseClass, IVsSelectionEvents, IVsRunningDocTableEvents
    {
        public bool UseOutputPane { get; set; } = false;

        public IVsOutputWindowPane _OutputPane;

        public static async Task DoMain(object[] args)
        {
            var oMySimpleSample = new MySimpleSample(args);
            await oMySimpleSample.DoInitializeAsync();
        }
        MySimpleSample(object[] args) : base(args) { }
        async Task DoInitializeAsync()
        {
            CloseableTabItem tabItemTabProc = GetTabItem();
            var strxaml =
$@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={
    System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        >
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""75"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <CheckBox Margin=""15,0,0,10"" Content=""Output To Debug OutputPane""  IsChecked=""{{Binding UseOutputPane}}""  
                ToolTip=""Output to Debug OutputPane 'PerfGraphVSIX'""/>
        <TextBox xml:space=""preserve"" >
IVsMonitorSelection until this tab is closed.
Output to Debug OutputPane or LogStatus
</TextBox>
        </StackPanel>
        <Grid Name=""gridUser"" Grid.Row = ""1""></Grid>
    </Grid>
";
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);
            var grid = (Grid)(XamlReader.Load(xamlreader));
            tabItemTabProc.Content = grid;

            grid.DataContext = this;
            var gridUser = (Grid)grid.FindName("gridUser");
            _OutputPane = await GetOutputPaneAsync();
            _OutputPane.Clear();
            uint cookieSelectionEvents = 0;
            uint cookieRdt = 0;
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                LogMessage("Here in MySimpleSample " + DateTime.Now.ToString("MM/dd/yy hh:mm:ss"));
                await TaskScheduler.Default; // switch to background thread
                LogMessage("Logger message from MySimpleSample");

                var SolutionService = await _asyncServiceProvider.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
                LogMessage($"{nameof(SolutionService)} {SolutionService}");

                var oleMenuCommandService = await _asyncServiceProvider.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
                LogMessage($"{nameof(oleMenuCommandService)} = {oleMenuCommandService}");

                var VsShellMonitorSelection = await _asyncServiceProvider.GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
                LogMessage($"{nameof(VsShellMonitorSelection)} = {VsShellMonitorSelection}");
                VsShellMonitorSelection.AdviseSelectionEvents(this, out cookieSelectionEvents);

                IVsRunningDocumentTable vsRunningDocumentTable = await _asyncServiceProvider.GetServiceAsync(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
                //vsRunningDocumentTable.AdviseRunningDocTableEvents(this, out cookieRdt);
                void UnSubscribe()
                {
                    VsShellMonitorSelection.UnadviseSelectionEvents(cookieSelectionEvents);
                    vsRunningDocumentTable.UnadviseRunningDocTableEvents(cookieRdt);
                }
                tabItemTabProc.TabItemClosed += async (o, e) =>
                {
                    UnSubscribe();
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _perfGraphToolWindowControl.TabControl.SelectedIndex = 0;
                };
            });
        }
        async Task LogMessage(string msg, params object[] args)
        {
            if (UseOutputPane)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _OutputPane.OutputString(string.Format(msg, args) + Environment.NewLine);
            }
            else
            {
                _logger.LogMessage(msg, args);
            }
        }
        int IVsRunningDocTableEvents.OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            LogMessage($"Entering {nameof(IVsRunningDocTableEvents.OnAfterFirstDocumentLock)}");
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            LogMessage($"Entering {nameof(IVsRunningDocTableEvents.OnBeforeLastDocumentUnlock)}");
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnAfterSave(uint docCookie)
        {
            LogMessage($"Entering {nameof(IVsRunningDocTableEvents.OnAfterSave)}");
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            LogMessage($"Entering {nameof(IVsRunningDocTableEvents.OnAfterAttributeChange)}");
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            LogMessage($"Entering {nameof(IVsRunningDocTableEvents.OnBeforeDocumentWindowShow)}");
            return VSConstants.S_OK;
        }

        int IVsRunningDocTableEvents.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            LogMessage($"Entering {nameof(IVsRunningDocTableEvents.OnAfterDocumentWindowHide)}");
            return VSConstants.S_OK;
        }


        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            LogMessage($"{nameof(OnCmdUIContextChanged)} UICtxCookie={dwCmdUICookie} fActive= {fActive}");
            return VSConstants.S_OK;
        }
        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            LogMessage($"{nameof(OnElementValueChanged)} ElementId={elementid}  ValOld={varValueOld} ValNew={varValueNew}");
            return VSConstants.S_OK;
        }
        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            LogMessage($"{nameof(OnSelectionChanged)}");
            return VSConstants.S_OK;
        }
    }
}
