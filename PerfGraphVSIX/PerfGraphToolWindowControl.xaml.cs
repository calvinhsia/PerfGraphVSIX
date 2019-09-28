namespace PerfGraphVSIX
{
    using Microsoft.VisualStudio.PlatformUI;
    using Microsoft.VisualStudio.ProjectSystem.Properties;
    using Microsoft.VisualStudio.Shell;
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

    public partial class PerfGraphToolWindowControl : UserControl, INotifyPropertyChanged
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

        /// <summary>
        /// PerfCounters updated periodically. Safe to change without stopping the monitoring
        /// </summary>
        public int UpdateInterval { get; set; } = 1000;
        public int NumDataPoints { get; set; } = 100;

        public bool ScaleByteCounters { get; set; } = false;
        public bool SetMaxGraphTo100 { get; set; } = false;

        public string TipString { get; } = $"PerfGraphVSIX https://github.com/calvinhsia/PerfGraphVSIX.git Version={typeof(PerfGraphToolWindowControl).Assembly.GetName().Version}    {System.Reflection.Assembly.GetExecutingAssembly().Location}";

        public FontFamily FontFamilyMono { get; set; } = new FontFamily("Consolas");

        public string LastStatMsg { get { return _LastStatMsg; } set { _LastStatMsg = value; RaisePropChanged(); } }

        public string ObjectTrackerFilter { get; set; } = ".*";

        public bool TrackTextViews { get; set; } = true;
        public bool TrackProjectObjects { get; set; } = true;
        public bool TrackContainedObjects { get; set; } = true;

        public string CurrentDirectory { get { return Environment.CurrentDirectory; } }

        public string ExtensionDirectory { get { return this.GetType().Assembly.Location; } }

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
                      ThreadHelper.JoinableTaskFactory.Run(() => DoSampleAsync());
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
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenSolution += (o, e) =>
                {
                    if (this.TrackProjectObjects)
                    {
                        var task = AddStatusMsgAsync($"{nameof(Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenSolution)}");
                    }
                };
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

                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += (o, e) =>
                {
                    if (this.TrackProjectObjects)
                    {
                        var task = AddStatusMsgAsync($"{nameof(Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution)}");
                    }
                };

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

        async Task DoSampleAsync()
        {
            var sBuilder = new StringBuilder();
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
            await AddDataPointsAsync();
            AddStatusMsgAsync($"{sBuilder.ToString()}").Forget();
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
            }
            CntCreatedObjs = $"Created Objs: #Types = {CreatedObjs.Count} #Instances={nCntInstances}";
            CntLeakedObjs = $"Leaked Objs: {LeakedObjs.Count}";
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
            var tsk = AddStatusMsgAsync(msg, args);
        }

        const int statusTextLenThresh = 100000;
        int nTruncated = 0;

        async public Task AddStatusMsgAsync(string msg, params object[] args)
        {
            // we want to read the threadid 
            //and time immediately on current thread
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                //,Thread.CurrentThread.ManagedThreadId
                );
            var str = string.Format(dt + msg + "\r\n", args);
            this.LastStatMsg = str;
            if (txtStatus != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                // this action executes on main thread
                var len = txtStatus.Text.Length;

                if (len > statusTextLenThresh)
                {
                    txtStatus.Text = txtStatus.Text.Substring(statusTextLenThresh - 1000);
                    str += $"   Truncated Status History {++nTruncated} times\r\n";
                }
                txtStatus.AppendText(str);
                txtStatus.ScrollToEnd();
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