namespace PerfGraphVSIX
{
    using DumperViewer;
    using EnvDTE;
    using Microsoft;
    using Microsoft.Build.Utilities;
    using Microsoft.VisualStudio.PlatformUI;
    using Microsoft.VisualStudio.ProjectSystem.Properties;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Events;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Threading;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms.DataVisualization.Charting;
    using System.Windows.Forms.Integration;
    using System.Windows.Media;
    using Task = System.Threading.Tasks.Task;

    public partial class PerfGraphToolWindowControl : UserControl, INotifyPropertyChanged, ILogger, IStressUtil
    {
        internal EditorTracker _editorTracker;
        internal OpenFolderTracker _openFolderTracker;

        internal ObjTracker _objTracker;

        JoinableTask _tskDoPerfMonitoring;
        CancellationTokenSource _ctsPcounter;
        TaskCompletionSource<int> _tcsPcounter;

        CodeExecutor _codeExecutor;
        CancellationTokenSource _ctsExecuteCode;
        readonly FileSystemWatcher _fileSystemWatcher;

        string _LastStatMsg;
        readonly Chart _chart;

        public ObservableCollection<UIElement> OpenedViews { get; set; } = new ObservableCollection<UIElement>();
        public ObservableCollection<UIElement> LeakedViews { get; set; } = new ObservableCollection<UIElement>();
        public ObservableCollection<UIElement> CreatedObjs { get; set; } = new ObservableCollection<UIElement>();
        public ObservableCollection<LeakedObject> LeakedObjs { get; set; } = new ObservableCollection<LeakedObject>();

        private string _CntCreatedObjs;
        public string CntCreatedObjs { get { return _CntCreatedObjs; } set { _CntCreatedObjs = value; RaisePropChanged(); } }
        private string _CntLeakedObjs;
        public string CntLeakedObjs { get { return _CntLeakedObjs; } set { _CntLeakedObjs = value; RaisePropChanged(); } }

        private int _UpdateInterval = 1000;
        /// <summary>
        /// PerfCounters updated periodically. Safe to change without stopping the monitoring
        /// </summary>
        public int UpdateInterval { get { return _UpdateInterval; } set { _UpdateInterval = value; RaisePropChanged(); } }
        public int NumDataPoints { get; set; } = 100;

        public bool SetMaxGraphTo100 { get; set; } = false;


        public string CodeSampleDirectory
        {
            get
            {
                var dirDev = @"C:\Users\calvinh\Source\Repos\PerfGraphVSIX\PerfGraphVSIX\CodeSamples";
                if (Directory.Exists(dirDev)) //while developing, use the source folder 
                {
                    return dirDev;
                }
                return Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "CodeSamples"); // runtime as a vsix: C:\Users\calvinh\AppData\Local\Microsoft\VisualStudio\16.0_7f0e2dbcExp\Extensions\Calvin Hsia\PerfGraphVSIX\1.0\CodeSamples
            }
        }
        private ObservableCollection<string> _LstCodeSamples = new ObservableCollection<string>();
        public ObservableCollection<string> LstCodeSamples { get { return _LstCodeSamples; } set { _LstCodeSamples = value; RaisePropChanged(); } }

        public int NumberOfIterations { get; set; } = 7;
        public int DelayMultiplier { get; set; } = 1;

        public string TipString { get; } = $"PerfGraphVSIX https://github.com/calvinhsia/PerfGraphVSIX.git Version={typeof(PerfGraphToolWindowControl).Assembly.GetName().Version}\r\n" +
            $"{System.Reflection.Assembly.GetExecutingAssembly().Location}   CurDir={Environment.CurrentDirectory}";

        public FontFamily FontFamilyMono { get; set; } = new FontFamily("Consolas");

        public string LastStatMsg { get { return _LastStatMsg; } set { _LastStatMsg = value; RaisePropChanged(); } }

        public string ObjectTrackerFilter { get; set; } = ".*";

        public bool TrackTextViews { get; set; } = true;
        public bool TrackTextBuffers { get; set; } = true;
        public bool TrackProjectObjects { get; set; } = true;
        public bool TrackContainedObjects { get; set; } = true;

        public List<PerfCounterData> LstPerfCounterData => throw new NotImplementedException();

        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public class LeakedObject
        {
            public int SerialNo { get; set; }
            public DateTime Created { get; set; }
            public string ClassName { get; set; }
        }

        public PerfGraphToolWindowControl()
        {
            this.InitializeComponent();
            try
            {
                LogMessage($"Starting {TipString}");

                async Task RefreshCodeToRunAsync()
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LstCodeSamples.Clear();
                    foreach (var file in Directory.GetFiles(CodeSampleDirectory, "*.cs").OrderByDescending(f => new FileInfo(f).LastWriteTime))
                    {
                        LstCodeSamples.Add(Path.GetFileName(file));
                    }
                    //_ctsExecuteCode?.Cancel();
                    lvCodeSamples.SelectedItem = LstCodeSamples[0];
                }
                _ = RefreshCodeToRunAsync();

                _fileSystemWatcher = new FileSystemWatcher(CodeSampleDirectory);
                FileSystemEventHandler h = new FileSystemEventHandler(
                            (o, e) =>
                            {
                                //                                LogMessage($"FileWatcher {e.ChangeType} '{e.FullPath}'");
                                _ = RefreshCodeToRunAsync();
                            }
                );
                _fileSystemWatcher.Changed += h;
                _fileSystemWatcher.Created += h;
                _fileSystemWatcher.Deleted += h;
                _fileSystemWatcher.EnableRaisingEvents = true;

                _objTracker = new ObjTracker(this);
                _editorTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<EditorTracker>();
                _editorTracker.Initialize(this, _objTracker);
                _openFolderTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<OpenFolderTracker>();
                _openFolderTracker.Initialize(this, _objTracker);

                txtUpdateInterval.LostFocus += (o, e) =>
                {
                    _ = ResetPerfCounterMonitorAsync();
                };

                btnDoSample.Click += (o, e) =>
                  {
                      ThreadHelper.JoinableTaskFactory.Run(() => DoSampleAsync(measurementHolderInteractiveUser, SampleType.SampleTypeNormal, "Manual"));
                  };


                lbPCounters.ItemsSource = PerfCounterData._lstPerfCounterDefinitionsForVSIX.Select(s => s.perfCounterType);
                lbPCounters.SelectedIndex = 1;
                PerfCounterData._lstPerfCounterDefinitionsForVSIX.Where(s => s.perfCounterType == PerfCounterType.GCBytesInAllHeaps).Single().IsEnabledForGraph = true;
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
                lbPCounters.SelectionChanged += async (ol, el) =>
                {
                    try
                    {
                        lbPCounters.IsEnabled = false;
                        // cancel the perf monitoring
                        _ctsPcounter?.Cancel();
                        // before we wait for cancel to finish we can do some work
                        PerfCounterType pctrEnum = PerfCounterType.None;
                        foreach (var itm in lbPCounters.SelectedItems)
                        {
                            pctrEnum |= (PerfCounterType)Enum.Parse(typeof(PerfCounterType), itm.ToString());
                        }
                        AddStatusMsgAsync($"Setting counters to {pctrEnum.ToString()}").Forget();
                        // wait for it to be done cancelling
                        if (_tskDoPerfMonitoring != null)
                        {
                            await _tskDoPerfMonitoring;
                        }
                        await Task.Run(async () =>
                        {
                            // run on threadpool thread
                            lock (PerfCounterData._lstPerfCounterDefinitionsForVSIX)
                            {
                                foreach (var itm in PerfCounterData._lstPerfCounterDefinitionsForVSIX)
                                {
                                    itm.IsEnabledForGraph = pctrEnum.HasFlag(itm.perfCounterType);
                                }
                            }
                            await ResetPerfCounterMonitorAsync();
                        });
                        AddStatusMsgAsync($"SelectionChanged done").Forget();
                        lbPCounters.IsEnabled = true;
                        el.Handled = true;
                    }
                    catch (Exception)
                    {
                    }
                };

                _chart = new Chart();
                wfhost.Child = _chart;

                _ = Task.Run(async () =>
                {
                    await ResetPerfCounterMonitorAsync();
                });
                var tsk = AddStatusMsgAsync($"PerfGraphVsix curdir= {Environment.CurrentDirectory}");
                chkShowStatusHistory.RaiseEvent(new RoutedEventArgs(CheckBox.CheckedEvent, this));

                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenProject += (o, e) =>
                {
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                    if (this.TrackProjectObjects)
                    {
                        var hier = e.Hierarchy;
                        if (hier.GetProperty((uint)Microsoft.VisualStudio.VSConstants.VSITEMID.Root,
                            (int)Microsoft.VisualStudio.Shell.Interop.__VSHPROPID.VSHPROPID_ExtObject,
                            out var extObject) == Microsoft.VisualStudio.VSConstants.S_OK)
                        {
                            var proj = extObject as EnvDTE.Project; // comobj or Microsoft.VisualStudio.ProjectSystem.VS.Implementation.Package.Automation.OAProject 
                            var name = proj.Name;
                            var context = proj as IVsBrowseObjectContext; // Microsoft.VisualStudio.ProjectSystem.VS.Implementation.Package.Automation.OAProject
                            if (context == null && proj != null)
                            {
                                context = proj.Object as IVsBrowseObjectContext; // {Microsoft.VisualStudio.Project.VisualC.VCProjectEngine.VCProjectShim}
                            }
                            if (context != null)
                            {
                                //                                var task = AddStatusMsgAsync($"{nameof(Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenProject)} {proj.Name}   Context = {context}");
                                _objTracker.AddObjectToTrack(context, ObjSource.FromProject, description: proj.Name);
                                //var x = proj.Object as Microsoft.VisualStudio.ProjectSystem.Properties.IVsBrowseObjectContext;
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }

        // use a circular buffer to store samples. 
        // dictionary of sample # (int from 0 to NumDataPoints) =>( List (PerfCtrValues in order)
        public Dictionary<int, List<uint>> _dataPoints = new Dictionary<int, List<uint>>();
        int _bufferIndex = 0;
        async Task ResetPerfCounterMonitorAsync()
        {
            _ctsPcounter?.Cancel();
            if (_tcsPcounter != null)
            {
                await _tcsPcounter.Task;
            }
            lock (PerfCounterData._lstPerfCounterDefinitionsForVSIX)
            {
                measurementHolderInteractiveUser = new MeasurementHolder(string.Empty, PerfCounterData._lstPerfCounterDefinitionsForVSIX, logger: this);
                _dataPoints.Clear();
                _bufferIndex = 0;
            }
            if (UpdateInterval > 0)
            {
                AddStatusMsgAsync($"{nameof(ResetPerfCounterMonitorAsync)}").Forget();
                DoPerfCounterMonitoring();
            }
            else
            {
                AddStatusMsgAsync($"UpdateInterval = 0 auto sampling turned off").Forget();
            }
        }

        void DoPerfCounterMonitoring()
        {
            _ctsPcounter = new CancellationTokenSource();
            _tcsPcounter = new TaskCompletionSource<int>();
            _tskDoPerfMonitoring = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    while (!_ctsPcounter.Token.IsCancellationRequested && UpdateInterval > 0)
                    {
                        await DoSampleAsync(measurementHolderInteractiveUser, SampleType.SampleTypeNormal);
                        await Task.Delay(TimeSpan.FromMilliseconds(UpdateInterval), _ctsPcounter.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                AddStatusMsgAsync($"cancelling {nameof(DoPerfCounterMonitoring)}").Forget();
                _tcsPcounter.SetResult(0);
            });
        }

        //used for interactive user, not for iteration tests
        MeasurementHolder measurementHolderInteractiveUser;
        public async Task DoSampleAsync(MeasurementHolder measurementHolder, SampleType sampleType, string descriptionOverride = "")
        {
            if (measurementHolder == null)
            {
                measurementHolder = measurementHolderInteractiveUser;
            }
            List<uint> lstPerfCtrCurrentMeasurements;
            try
            {
                var res = string.Empty;

                lock (measurementHolder.lstPerfCounterData)
                {
                    res = measurementHolder.TakeMeasurement(descriptionOverride, sampleType);
                    lstPerfCtrCurrentMeasurements = measurementHolder.GetLastMeasurements();
                }
                try
                {
                    await AddDataPointsAsync(lstPerfCtrCurrentMeasurements);
                }
                catch (Exception ex)
                {
                    res = ex.ToString();
                }
                AddStatusMsgAsync($"{res}").Forget();
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Instance 'devenv#")) // user changed # of instance of devenv runnning
                {
                    await AddStatusMsgAsync($"Resetting perf counters due to devenv instances change");
                    lock (measurementHolder.lstPerfCounterData)
                    {
                        foreach (var ctr in measurementHolder.lstPerfCounterData)
                        {
                            ctr.ResetCounter();
                        }
                        _dataPoints.Clear();
                        _bufferIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                await AddStatusMsgAsync($"Exception in {nameof(DoSampleAsync)}" + ex.ToString());
                _dataPoints.Clear();
                _bufferIndex = 0;
            }
        }

        async Task AddDataPointsAsync(List<uint> lstPerfCtrCurrentMeasurements)
        {
            if (_dataPoints.Count == 0) // nothing yet
            {
                for (int i = 0; i < NumDataPoints; i++)
                {
                    _dataPoints[i] = new List<uint>(lstPerfCtrCurrentMeasurements); // let all init points be equal, so y axis scales IsStartedFromZero
                }
            }
            else
            {
                _dataPoints[_bufferIndex++] = new List<uint>(lstPerfCtrCurrentMeasurements);
                if (_bufferIndex == _dataPoints.Count) // wraparound?
                {
                    _bufferIndex = 0;
                }
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DoGC();// do a GC.Collect on main thread for every sample (the graphing uses memory)
            // this needs to be done on UI thread
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            ChartArea chartArea = new ChartArea("ChartArea");
            chartArea.AxisY.LabelStyle.Format = "{0:n0}";
            chartArea.AxisY.LabelStyle.Font = new System.Drawing.Font("Consolas", 12);
            _chart.ChartAreas.Add(chartArea);
            int ndxSeries = 0;
            chartArea.AxisY.IsStartedFromZero = false;
            if (SetMaxGraphTo100)
            {
                _chart.ChartAreas[0].AxisY.Maximum = 100;
            }
            foreach (var entry in lstPerfCtrCurrentMeasurements)
            {
                var series = new Series
                {
                    ChartType = SeriesChartType.Line
                };
                _chart.Series.Add(series);
                for (int i = 0; i < _dataPoints.Count; i++)
                {
                    var ndx = _bufferIndex + i;
                    if (ndx >= _dataPoints.Count)
                    {
                        ndx -= _dataPoints.Count;
                    }
                    var dp = new DataPoint(i + 1, _dataPoints[ndx][ndxSeries]);
                    series.Points.Add(dp);
                }
                ndxSeries++;
            }
            _chart.DataBind();

            if (_editorTracker != null)
            {
                var (openedViews, lstLeakedViews) = _editorTracker.GetCounts();
                OpenedViews.Clear();
                LeakedViews.Clear();
                foreach (var dictEntry in openedViews)
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"{ dictEntry.Key,-15} {dictEntry.Value,3}", FontFamily = FontFamilyMono });
                    OpenedViews.Add(sp);
                }

                foreach (var entry in lstLeakedViews)
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"{ entry._contentType,-15} {entry._serialNo,3} {entry._dtCreated.ToString("hh:mm:ss")} {entry._filename}", FontFamily = FontFamilyMono });
                    LeakedViews.Add(sp);
                }
            }
            int nCntInstances = 0;
            if (_objTracker != null)
            {
                var (createdObjs, lstLeakedObjs) = _objTracker.GetCounts();
                CreatedObjs.Clear();
                LeakedObjs.Clear();
                foreach (var dictEntry in createdObjs.OrderByDescending(e => e.Value))
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"#Inst={dictEntry.Value,3} {dictEntry.Key,-15}", FontFamily = FontFamilyMono });
                    CreatedObjs.Add(sp);
                    nCntInstances += dictEntry.Value;
                }

                //foreach (var entry in lstLeakedObjs)
                //{
                //    //var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                //    //sp.Children.Add(new TextBlock() { Text = $"SerNo={entry._serialNo,3} {entry._dtCreated.ToString("hh:mm:ss")} {entry.Descriptor,-15} ", FontFamily = _fontFamily });
                //    LeakedObjs.Add(new LeakedObject()
                //    {
                //        SerialNo = entry._serialNo,
                //        Created = entry._dtCreated,
                //        ClassName=entry.Descriptor,
                //    });
                //}
                //var q = from entry in lstLeakedObjs
                //        select new
                //        {
                //            SerialNo=entry._serialNo,
                //            DtCreated = entry._dtCreated,
                //            entry.Descriptor
                //        };
                BrowLeakedObjects.Content = new BrowsePanel(
                        from entry in lstLeakedObjs
                        select new
                        {
                            SerialNo = entry._serialNo,
                            DtCreated = entry._dtCreated,
                            entry.Descriptor
                        },
                        new[] { 60, 130, 600 });
                CntLeakedObjs = $"Leaked Objs: {lstLeakedObjs.Count}";
            }
            CntCreatedObjs = $"Created Objs: #Types = {CreatedObjs.Count} #Instances={nCntInstances}";
        }

        void DoGC()
        {
            Dispatcher.VerifyAccess();
            //EnvDTE.DTE dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            PerfGraphToolWindowCommand.Instance.g_dte.ExecuteCommand("Tools.ForceGC");
        }

        public void LogMessage(string msg, params object[] args)
        {
            _ = AddStatusMsgAsync(msg, args);
        }

        const int statusTextLenThresh = 100000;
        int nTruncated = 0;

        // we can't use the output window because it will just accumulate and look like a leak
        async public Task AddStatusMsgAsync(string msg, params object[] args)
        {
            // we want to read the threadid 
            //and time immediately on current thread
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                //,Thread.CurrentThread.ManagedThreadId
                );
            var str = string.Format(dt + msg, args);
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine(str);
            }
            this.LastStatMsg = str;
            if (txtStatus != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                // this action executes on main thread
                var len = txtStatus.Text.Length;

                if (len > statusTextLenThresh)
                {
                    txtStatus.Text = txtStatus.Text.Substring(statusTextLenThresh - 1000);
                    str += $"   Truncated Status History {++nTruncated} times";
                }
                txtStatus.AppendText(str + "\r\n");
                txtStatus.ScrollToEnd();
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        async void BtnClrObjExplorer_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            btnClrObjExplorer.IsEnabled = false;
            await CreateDumpAsync(System.Diagnostics.Process.GetCurrentProcess().Id, MemoryAnalysisType.StartClrObjectExplorer, string.Empty);

            btnClrObjExplorer.IsEnabled = true;

            //var x = new DumpAnalyzer(this);
            //x.StartClrObjectExplorer(pathDumpFile);
        }

        public async Task CreateDumpAsync(int pid, MemoryAnalysisType memoryAnalysisType, string desc)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                DoGC(); //must be on main thread
                var pathDumpFile = DumperViewer.DumperViewerMain.GetNewDumpFileName(baseName: string.IsNullOrEmpty(desc) ? "devenv" : desc);
                LogMessage($"start clrobjexplorer {pathDumpFile}");
                var arglist = new List<string>()
                    {
                        "-p", pid.ToString(),
                        "-f",  "\"" + pathDumpFile + "\""
                    };
                if (memoryAnalysisType.HasFlag(MemoryAnalysisType.StartClrObjectExplorer))
                {
                    arglist.Add("-c");
                }
                var odumper = new DumperViewerMain(arglist.ToArray())
                {
                    _logger = this
                };
                await odumper.DoitAsync();
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void BtnExecCode_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                if (this.UpdateInterval != 0)
                {
                    this.UpdateInterval = 0;
                    await ResetPerfCounterMonitorAsync();
                }
                if (_ctsExecuteCode == null)
                {
                    this.tabControl.SelectedIndex = 0; // select graph tab
                    var CodeFileToRun = string.Empty;
                    if (lvCodeSamples.SelectedItem == null || lvCodeSamples.SelectedItems.Count != 1)
                    {
                        LogMessage($"No single Code file selected");
                        return;
                    }
                    CodeFileToRun = Path.Combine(CodeSampleDirectory, lvCodeSamples.SelectedItem.ToString());

                    this.btnExecCode.Content = "Cancel Code Execution";
                    await AddStatusMsgAsync($"Starting Code Execution {CodeFileToRun}"); // https://social.msdn.microsoft.com/forums/vstudio/en-US/5066b6ac-fdf8-4877-a023-1a7550f2cdd9/custom-tool-hosting-an-editor-iwpftextviewhost-in-a-tool-window
                    _ctsExecuteCode = new CancellationTokenSource();
                    if (_codeExecutor == null)
                    {
                        _codeExecutor = new CodeExecutor(this);
                    }
                    var sw = Stopwatch.StartNew();
                    var res = _codeExecutor.CompileAndExecute(this, CodeFileToRun, _ctsExecuteCode.Token);
                    if (res is Task task)
                    {
                        //                   await AddStatusMsgAsync($"CompileAndExecute done: {res}");
                        await task;
                        await AddStatusMsgAsync($"Done Code Execution {Path.GetFileNameWithoutExtension(CodeFileToRun)}  {sw.Elapsed.TotalMinutes:n2} Mins");
                    }
                    else
                    {
                        await AddStatusMsgAsync($"Result of CompileAndExecute\r\n" + res.ToString());
                    }
                    _ctsExecuteCode = null;
                    this.btnExecCode.Content = "ExecCode";
                    this.btnExecCode.IsEnabled = true;
                }
                else
                {
                    await AddStatusMsgAsync("cancelling Code Execution");
                    _ctsExecuteCode.Cancel();
                    this.btnExecCode.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
                this.btnExecCode.Content = "ExecCode";
                this.btnExecCode.IsEnabled = true;
            }
        }

        private void LvCodeSamples_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //            ThreadHelper.ThrowIfNotOnUIThread();
            Dispatcher.VerifyAccess();

            if (lvCodeSamples.SelectedItems.Count == 1)
            {
                var itm = lvCodeSamples.SelectedItems[0] as string;
                var pathFile = Path.Combine(
                    CodeSampleDirectory,
                    itm.ToString());
                pathFile = "\"" + pathFile + "\"";
                PerfGraphToolWindowCommand.Instance.g_dte.ExecuteCommand("File.OpenFile", pathFile);
            }
        }
    }


    // a textbox that selects all when focused:
    public class MyTextBox : TextBox
    {
        public MyTextBox()
        {
            this.SetResourceReference(TextBox.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            this.SetResourceReference(TextBox.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            this.GotFocus += (o, e) =>
            {
                this.SelectAll();
            };
        }
    }
}