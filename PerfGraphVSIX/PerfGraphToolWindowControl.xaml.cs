namespace PerfGraphVSIX
{
    using EnvDTE;
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
    using System.Diagnostics.CodeAnalysis;
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

    public partial class PerfGraphToolWindowControl : UserControl, INotifyPropertyChanged, ILogger
    {
        internal EditorTracker _editorTracker;
        internal OpenFolderTracker _openFolderTracker;

        internal ObjTracker _objTracker;
        JoinableTask _tskDoPerfMonitoring;
        CancellationTokenSource _ctsPcounter;
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

        public bool ScaleByteCounters { get; set; } = false;
        public bool SetMaxGraphTo100 { get; set; } = false;

        public string SolutionToLoad { get; set; }
        private string _CodeToRun = CodeExecutor.sampleVSCodeToExecute;
        public string CodeToRun { get { return _CodeToRun; } set { _CodeToRun = value; RaisePropChanged(); } }
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

        public static readonly List<PerfCounterData> _lstPerfCounterDefinitions = new List<PerfCounterData>()
        {
            {new PerfCounterData(PerfCounterType.ProcessorPctTime, "Process","% Processor Time","ID Process" )} ,
            {new PerfCounterData(PerfCounterType.ProcessorPrivateBytes, "Process","Private Bytes","ID Process") },
            {new PerfCounterData(PerfCounterType.ProcessorVirtualBytes, "Process","Virtual Bytes","ID Process") },
            {new PerfCounterData(PerfCounterType.ProcessorWorkingSet, "Process","Working Set","ID Process") },
            {new PerfCounterData(PerfCounterType.GCPctTime, ".NET CLR Memory","% Time in GC","Process ID") },
            {new PerfCounterData(PerfCounterType.GCBytesInAllHeaps, ".NET CLR Memory","# Bytes in all Heaps","Process ID" )},
            {new PerfCounterData(PerfCounterType.GCAllocatedBytesPerSec, ".NET CLR Memory","Allocated Bytes/sec","Process ID") },
            {new PerfCounterData(PerfCounterType.PageFaultsPerSec, "Process","Page Faults/sec","ID Process") },
            {new PerfCounterData(PerfCounterType.ThreadCount, "Process","Thread Count","ID Process") },
            {new PerfCounterData(PerfCounterType.KernelHandleCount, "Process","Handle Count","ID Process") },
            {new PerfCounterData(PerfCounterType.GDIHandleCount, "GetGuiResources","GDIHandles",string.Empty) },
            {new PerfCounterData(PerfCounterType.UserHandleCount, "GetGuiResources","UserHandles",string.Empty) },
        };

        public PerfGraphToolWindowControl()
        {
            var sln = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";

            if (System.IO.File.Exists(sln))
            {
                SolutionToLoad = sln;
            }
            this.InitializeComponent();
            try
            {
                AddStatusMsg($"Starting {TipString}");
                _objTracker = new ObjTracker(this);
                _editorTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<EditorTracker>();
                _editorTracker.Initialize(this, _objTracker);
                _openFolderTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<OpenFolderTracker>();
                _openFolderTracker.Initialize(this, _objTracker);

                txtUpdateInterval.LostFocus += (o, e) =>
                {
                    ResetPerfCounterMonitor();
                };

                btnDoSample.Click += (o, e) =>
                  {
                      ThreadHelper.JoinableTaskFactory.Run(() => DoSampleAsync("Manual"));
                  };


                lbPCounters.ItemsSource = _lstPerfCounterDefinitions.Select(s => s.perfCounterType);
                lbPCounters.SelectedIndex = 1;
                _lstPerfCounterDefinitions.Where(s => s.perfCounterType == PerfCounterType.ProcessorPrivateBytes).Single().IsEnabled = true;
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
                        await Task.Run(() =>
                        {
                            // run on threadpool thread
                            lock (_lstPerfCounterDefinitions)
                            {
                                foreach (var itm in _lstPerfCounterDefinitions)
                                {
                                    itm.IsEnabled = pctrEnum.HasFlag(itm.perfCounterType);
                                }
                                ResetPerfCounterMonitor();
                            }
                        });
                        AddStatusMsgAsync($"SelectionChanged done").Forget();
                        lbPCounters.IsEnabled = true;
                        el.Handled = true;
                    }
                    catch (Exception)
                    {
                    }
                };

                _chart = new Chart()
                {
                    //Width = 200,
                    //Height = 400,
                    //                    Dock = System.Windows.Forms.DockStyle.Fill
                };
                wfhost.Child = _chart;

                var t = Task.Run(() =>
                {
                    ResetPerfCounterMonitor();
                });
                var tsk = AddStatusMsgAsync($"PerfGraphVsix curdir= {Environment.CurrentDirectory}");
                chkShowStatusHistory.RaiseEvent(new RoutedEventArgs(CheckBox.CheckedEvent, this));

                //                _solutionEvents = new Microsoft.VisualStudio.Shell.Events.SolutionEvents();
                //Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenSolution += (o, e) =>
                //{
                //    if (this.TrackProjectObjects)
                //    {
                //        var task = AddStatusMsgAsync($"{nameof(Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenSolution)}");
                //    }
                //};
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
                                var task = AddStatusMsgAsync($"{nameof(Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenProject)} {proj.Name}   Context = {context}");
                                _objTracker.AddObjectToTrack(context, ObjSource.FromProject, description: proj.Name);
                                //var x = proj.Object as Microsoft.VisualStudio.ProjectSystem.Properties.IVsBrowseObjectContext;
                            }
                        }
                    }
                };

                //Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += (o, e) =>
                //{
                //    if (this.TrackProjectObjects)
                //    {
                //        var task = AddStatusMsgAsync($"{nameof(Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution)}");
                //    }
                //};

                //var ev = (Events2)PerfGraphToolWindowCommand.Instance.g_dte.Events;
                //_solutionEvents = ev.SolutionEvents;
                //_solutionEvents.AfterClosing += () =>
                //{
                //    var task = AddStatusMsgAsync($"{nameof(_solutionEvents.AfterClosing)}");
                //};
                //_solutionEvents.Opened += () =>
                //{
                //    var task = AddStatusMsgAsync($"{nameof(_solutionEvents.Opened)}");

                //};
                //_solutionEvents.ProjectAdded += (o) =>
                //{

                //};

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
        List<uint> _lstPCData; // list of samples from each selected counter
        void ResetPerfCounterMonitor()
        {
            _ctsPcounter?.Cancel();
            lock (_lstPerfCounterDefinitions)
            {
                _lstPCData = new List<uint>();
                _dataPoints.Clear();
                _bufferIndex = 0;
            }
            if (UpdateInterval > 0)
            {
                AddStatusMsgAsync($"{nameof(ResetPerfCounterMonitor)}").Forget();
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
            _tskDoPerfMonitoring = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    while (!_ctsPcounter.Token.IsCancellationRequested && UpdateInterval > 0)
                    {
                        await DoSampleAsync();
                        await Task.Delay(TimeSpan.FromMilliseconds(UpdateInterval), _ctsPcounter.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                AddStatusMsgAsync($"cancelling {nameof(DoPerfCounterMonitoring)}").Forget();
            });
        }

        async Task DoSampleAsync(string desc = "")
        {
            try
            {
                var sBuilder = new StringBuilder();
                if (!string.IsNullOrEmpty(desc))
                {
                    sBuilder.Append(desc + " ");
                }
                lock (_lstPerfCounterDefinitions)
                {
                    int idx = 0;
                    foreach (var ctr in _lstPerfCounterDefinitions.Where(pctr => pctr.IsEnabled))
                    {
                        var pcValueAsFloat = ctr.ReadNextValue();
                        uint pcValue = 0;
                        uint priorValue = 0;
                        if (idx < _lstPCData.Count)
                        {
                            priorValue = _lstPCData[idx];
                        }
                        else
                        {
                            _lstPCData.Add(0);
                        }
                        if (ctr.perfCounterType.ToString().Contains("Bytes") && !ctr.perfCounterType.ToString().Contains("PerSec") && this.ScaleByteCounters)
                        {
                            pcValue = (uint)(pcValueAsFloat * 100 / uint.MaxValue); // '% of 4G
                            int delta = (int)pcValue - (int)priorValue;
                            sBuilder.Append($"{ctr.PerfCounterName}= {pcValueAsFloat:n0}  {pcValue:n0}%  Δ = {delta:n0} ");
                        }
                        else
                        {
                            pcValue = (uint)pcValueAsFloat;
                            int delta = (int)pcValue - (int)priorValue;
                            sBuilder.Append($"{ctr.PerfCounterName}={pcValue:n0}  Δ = {delta:n0} ");
                        }
                        _lstPCData[idx] = pcValue;
                        idx++;
                    }
                }
                try
                {
                    await AddDataPointsAsync();
                }
                catch (Exception ex)
                {
                    sBuilder = new StringBuilder(ex.ToString());
                }
                AddStatusMsgAsync($"{sBuilder.ToString()}").Forget();
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Instance 'devenv#")) // user changed # of instance of devenv runnning
                {
                    await AddStatusMsgAsync($"Resetting perf counters due to devenv instances change");
                    lock (_lstPerfCounterDefinitions)
                    {
                        foreach (var ctr in _lstPerfCounterDefinitions)
                        {
                            ctr.ResetCounter();
                        }
                        _lstPCData = new List<uint>();
                        _dataPoints.Clear();
                        _bufferIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                await AddStatusMsgAsync($"Exception in {nameof(DoSampleAsync)}" + ex.ToString());
            }
        }

        async Task AddDataPointsAsync()
        {
            if (_dataPoints.Count == 0) // nothing yet
            {
                for (int i = 0; i < NumDataPoints; i++)
                {
                    _dataPoints[i] = new List<uint>(_lstPCData); // let all init points be equal, so y axis scales IsStartedFromZero
                }
            }
            else
            {
                _dataPoints[_bufferIndex++] = new List<uint>(_lstPCData);
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
            foreach (var entry in _lstPCData)
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

        //Microsoft.VisualStudio.Shell.Events.SolutionEvents _solutionEvents;


        public void AddStatusMsg(string msg, params object[] args)
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
        private async void BtnExecCode_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.UpdateInterval != 0)
            {
                this.UpdateInterval = 0;
                ResetPerfCounterMonitor();
            }
            if (_cts == null)
            {
                this.btnExecCode.Content = "Cancel Code Execution";
                await AddStatusMsgAsync("Starting Code Execution"); // https://social.msdn.microsoft.com/forums/vstudio/en-US/5066b6ac-fdf8-4877-a023-1a7550f2cdd9/custom-tool-hosting-an-editor-iwpftextviewhost-in-a-tool-window
                _cts = new CancellationTokenSource();
                if (_codeExecutor == null)
                {
                    _codeExecutor = new CodeExecutor(this);
                }
                var res = _codeExecutor.CompileAndExecute(this.CodeToRun, _cts.Token, actTakeSample: async (desc) =>
                {
                    await DoSampleAsync(desc);
                });
                if (res is Task task)
                {
                    //                   await AddStatusMsgAsync($"CompileAndExecute done: {res}");
                    await task;
                    //                    await AddStatusMsgAsync($"Task done: {res}");
                }
                else
                {
                    await AddStatusMsgAsync(res.ToString());
                }
                _cts = null;
                this.btnExecCode.Content = "ExecCode";
                this.btnExecCode.IsEnabled = true;
            }
            else
            {
                await AddStatusMsgAsync("cancelling Code Execution");
                _cts.Cancel();
                this.btnExecCode.IsEnabled = false;
            }
        }
        CodeExecutor _codeExecutor;
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationTokenSource _cts;
        private void BtnDoSample_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_cts == null)
            {
                _cts = new CancellationTokenSource();
                _ = DoSomeWorkAsync();
            }
            else
            {
                AddStatusMsg("cancelling iterations");
                _cts.Cancel();
            }
        }

        private async Task DoSomeWorkAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (this.UpdateInterval != 0)
            {
                this.UpdateInterval = 0;
                ResetPerfCounterMonitor();
            }
            if (nTimes++ == 0)
            {
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
            }
            for (int i = 0; i < NumberOfIterations && !_cts.IsCancellationRequested; i++)
            {
                await DoSampleAsync();
                LogMessage("Iter {0} Start {1} left to do", i, NumberOfIterations - i);
                await OpenASolutionAsync();
                if (_cts.IsCancellationRequested)
                {
                    break;
                }
                await CloseTheSolutionAsync();
                LogMessage("Iter {0} end", i);
            }
            if (_cts.IsCancellationRequested)
            {
                LogMessage("Cancelled");
            }
            else
            {
                LogMessage("Done all {0} iterations", NumberOfIterations);
            }
            await DoSampleAsync();
            _cts = null;
        }

        async Task OpenASolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            PerfGraphToolWindowCommand.Instance.g_dte.Solution.Open(SolutionToLoad);
            await _tcs.Task;
            if (!_cts.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        async Task CloseTheSolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            PerfGraphToolWindowCommand.Instance.g_dte.Solution.Close();
            if (!_cts.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            AddStatusMsg($"Solution {nameof(SolutionEvents_OnAfterCloseSolution)}");
            _tcs?.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
            AddStatusMsg($"Solution {nameof(SolutionEvents_OnAfterBackgroundSolutionLoadComplete)}");
            _tcs?.TrySetResult(0);
        }

        public void LogMessage(string msg, params object[] args)
        {
            AddStatusMsg(msg, args);
        }

        private void BtnExecCode_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var txt = System.Windows.Clipboard.GetText(TextDataFormat.Text);
            if (!string.IsNullOrEmpty(txt))
            {
                AddStatusMsg("Setting code from clipboard");
                CodeToRun = txt;
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