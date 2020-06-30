namespace PerfGraphVSIX
{
    using EnvDTE;
    using Microsoft;
    using Microsoft.Build.Utilities;
    using Microsoft.VisualStudio.PlatformUI;
    using Microsoft.VisualStudio.ProjectSystem.Properties;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Events;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.Test.Stress;
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
    using Microsoft.VisualStudio.Utilities;
    using PerfGraphVSIX.UserControls;
    using Microsoft.VisualStudio.Telemetry;

    public partial class PerfGraphToolWindowControl : UserControl, INotifyPropertyChanged, ILogger, ITakeSample
    {
        public static PerfGraphToolWindowControl g_PerfGraphToolWindowControl;
        internal EditorTracker _editorTracker;
        internal OpenFolderTracker _openFolderTracker;
        public const string TelemetryEventBaseName = @"DevDivStress/PerfGraphVSIX";

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

        private int _UpdateInterval = 0;
        /// <summary>
        /// PerfCounters updated periodically. Safe to change without stopping the monitoring
        /// </summary>
        public int UpdateInterval { get { return _UpdateInterval; } set { _UpdateInterval = value; RaisePropChanged(); } }

        public bool DoFullGCPerSample { get; set; } = false;
        public int NumDataPoints { get; set; } = 100;

        public bool SetMaxGraphTo100 { get; set; } = false;
        public TabControl TabControl => tabControl; // so executing code can reference it


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

        public int NumberOfIterations { get; set; } = 7;
        public int DelayMultiplier { get; set; } = 1;

        public string TipString { get; } = $"PerfGraphVSIX https://devdiv.visualstudio.com/DefaultCollection/Engineering/_git/DevDivStress Version={typeof(PerfGraphToolWindowControl).Assembly.GetName().Version}\r\n" +
            $"{System.Reflection.Assembly.GetExecutingAssembly().Location}   CurDir={Environment.CurrentDirectory}";

        public FontFamily FontFamilyMono { get; set; } = new FontFamily("Consolas");

        public string LastStatMsg { get { return _LastStatMsg; } set { _LastStatMsg = value; RaisePropChanged(); } }

        public string ObjectTrackerFilter { get; set; } = ".*";

        public bool TrackTextViews { get; set; } = true;
        public bool TrackTextBuffers { get; set; } = true;
        public bool TrackProjectObjects { get; set; } = true;
        public bool TrackContainedObjects { get; set; } = true;

        public List<PerfCounterData> LstPerfCounterData;

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
            g_PerfGraphToolWindowControl = this;
            try
            {
#if DEBUG
                LogMessage($"Starting {TipString}");
#endif
                var tspanDesiredLeaseLifetime = TimeSpan.FromSeconds(2);
                var oldval = System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime;
                if (oldval == tspanDesiredLeaseLifetime)
                {
                    LogMessage($"System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime.TotalSeconds already set at {oldval.TotalSeconds} secs");
                }
                else
                {
                    try
                    {
                        System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime = tspanDesiredLeaseLifetime;
                        LogMessage($"Success Change System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime.TotalSeconds from {oldval.TotalSeconds} secs to {System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime.TotalSeconds}");
                    }
                    catch (System.Runtime.Remoting.RemotingException)
                    {
                        LogMessage($"Failed to Change System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime.TotalSeconds from {oldval.TotalSeconds} secs to {tspanDesiredLeaseLifetime.TotalSeconds} secs");
                    }
                }
                LstPerfCounterData = PerfCounterData.GetPerfCountersToUse(System.Diagnostics.Process.GetCurrentProcess(), IsForStress: false);
                async Task RefreshCodeToRunAsync()
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    FileInfo mostRecentFileInfo = null;
                    foreach (var file in Directory.GetFiles(CodeSampleDirectory, "*.*", SearchOption.AllDirectories)
                        .Where(f => ".vb|.cs".Contains(Path.GetExtension(f).ToLower()))
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime))
                    {
                        if (!file.Contains(@"\Util\"))// utility folder doesn't contain code with Main program
                        {
                            var finfo = new FileInfo(file);
                            if (mostRecentFileInfo == null || finfo.LastWriteTime > mostRecentFileInfo.LastWriteTime)
                            {
                                mostRecentFileInfo = finfo;
                            }
                        }
                    }
                    _codeSampleControl = new CodeSamples(CodeSampleDirectory, mostRecentFileInfo?.Name);
                    this.spCodeSamples.Children.Clear();
                    this.spCodeSamples.Children.Add(_codeSampleControl);
                }
                _ = Task.Run(() =>
                {
                    _ = RefreshCodeToRunAsync();
                });

                _fileSystemWatcher = new FileSystemWatcher(CodeSampleDirectory);
                FileSystemEventHandler h = new FileSystemEventHandler(
                            (o, e) =>
                            {
                                //                                LogMessage($"FileWatcher {e.ChangeType} '{e.FullPath}'");
                                _ = RefreshCodeToRunAsync();
                            }
                );
                // we don't handle Rename here: just save the newly renamed file to trigger the Changed event.
                _fileSystemWatcher.Changed += h;
                _fileSystemWatcher.Created += h;
                _fileSystemWatcher.Deleted += h;
                _fileSystemWatcher.EnableRaisingEvents = true;

                ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (PerfGraphToolWindowCommand.Instance.g_dte == null) // if the toolwindow was already opened, this is set in InitializeToolWindowAsync. 1st time opening set it here
                    {
                        EnvDTE.DTE dte = (EnvDTE.DTE)await PerfGraphToolWindowCommand.Instance.package.GetServiceAsync(typeof(EnvDTE.DTE));
                        PerfGraphToolWindowCommand.Instance.g_dte = dte; // ?? throw new InvalidOperationException(nameof(dte));
                    }
                    _objTracker = new ObjTracker(this);
                    _editorTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<EditorTracker>();

                    _editorTracker.Initialize(this, _objTracker);
                    _openFolderTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<OpenFolderTracker>();
                    _openFolderTracker.Initialize(this, _objTracker);
                    if (this.IsLeakTrackerServiceSupported())
                    {
                        this.inProcLeakTracerTabItem.Visibility = Visibility.Visible;
                        this.inProcLeakTracker.Content = new InProcLeakTracker();
                    }
                    await TaskScheduler.Default;
                    var telEvent = new TelemetryEvent(TelemetryEventBaseName + "Start");
                    TelemetryService.DefaultSession.PostEvent(telEvent);
                    await DoProcessAutoexecAsync();
                });

                txtUpdateInterval.LostFocus += (o, e) =>
                {
                    _ = ResetPerfCounterMonitorAsync();
                };

                btnDoSample.Click += (o, e) =>
                  {
                      ThreadHelper.JoinableTaskFactory.Run(async () =>
                          {
                              await WaitForInitializationCompleteAsync();
                              await DoSampleAsync(measurementHolderInteractiveUser, DoForceGC: true, descriptionOverride: "Manual");
                          }
                      );
                  };


                lbPCounters.ItemsSource = LstPerfCounterData.Select(s => s.perfCounterType);
                lbPCounters.SelectedIndex = 0;
                LstPerfCounterData.Where(s => s.perfCounterType == PerfCounterType.GCBytesInAllHeaps).Single().IsEnabledForGraph = true;
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
                        AddStatusMsgAsync($"Setting counters to {pctrEnum}").Forget();
                        // wait for it to be done cancelling
                        if (_tskDoPerfMonitoring != null)
                        {
                            await _tskDoPerfMonitoring;
                        }
                        await Task.Run(async () =>
                        {
                            // run on threadpool thread
                            lock (LstPerfCounterData)
                            {
                                foreach (var itm in LstPerfCounterData)
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


                txtStatus.ContextMenu = new ContextMenu();
                txtStatus.ContextMenu.AddMenuItem((o, e) =>
                {
                    txtStatus.Clear();

                }, "_Clear All", "Clear the current contents");

                _ = Task.Run(async () =>
                {
                    //await AddStatusMsgAsync("Waiting 15 seconds to initialize graph");
                    //await Task.Delay(TimeSpan.FromSeconds(15));// delay samples til VS started
                    await ResetPerfCounterMonitorAsync();
                });
#if DEBUG
                var tsk = AddStatusMsgAsync($"PerfGraphVsix curdir= {Environment.CurrentDirectory}");
#endif
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

        private async Task DoProcessAutoexecAsync()
        {
            await WaitForInitializationCompleteAsync();
            await TaskScheduler.Default;
            var autoexecFile = Path.Combine(CodeSampleDirectory, "AutoExec.Txt");
            if (File.Exists(autoexecFile))
            {
                var fileContents = File.ReadAllLines(autoexecFile);
                foreach (var line in fileContents.Where(p => !string.IsNullOrEmpty(p.Trim()) && !p.StartsWith("//")))
                {
                    var splt = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (splt.Length == 2)
                    {
                        if (Environment.GetEnvironmentVariable("USERNAME").ToLowerInvariant() == splt[0].ToLowerInvariant())
                        {
                            await AddStatusMsgAsync($"AutoExec {splt[1]}");
                            var codeFileToRun = Path.Combine(CodeSampleDirectory, splt[1].Trim());
                            await RunCodeAsync(codeFileToRun);
                        }
                    }
                }

            }
        }

        private bool IsLeakTrackerServiceSupported()
        {
            try
            {
                DoTryTypeLoadException();
                return true;
            }
            catch
            {
                LogMessage("Leak tracker service not supported. Inproc Leak Tracker will not be shown");
                return false;
            }
        }

        // Types get loaded before the method that uses them, so it can't be caught in the same method as the Catch: must be in a method below the Catch
        [MethodImpl(MethodImplOptions.NoInlining)] // and not in-lined
        private void DoTryTypeLoadException() => PerfGraphToolWindowPackage.ComponentModel.GetService<IMemoryLeakTrackerService>();

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
            lock (LstPerfCounterData)
            {
                measurementHolderInteractiveUser = new MeasurementHolder(
                    TestNameOrTestContext: MeasurementHolder.InteractiveUser,
                    new StressUtilOptions() { NumIterations = -1, logger = this, lstPerfCountersToUse = LstPerfCounterData },
                    sampleType: SampleType.SampleTypeNormal);
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
                        await DoSampleAsync(measurementHolderInteractiveUser, DoForceGC: DoFullGCPerSample);
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
        public async Task DoSampleAsync(MeasurementHolder measurementHolder, bool DoForceGC, string descriptionOverride = "")
        {
            var res = string.Empty;
            if (measurementHolder == null)
            {
                measurementHolder = measurementHolderInteractiveUser;
            }
            try
            {
                await TaskScheduler.Default;
                try
                {
                    res = await measurementHolder.TakeMeasurementAsync(descriptionOverride, DoForceGC, IsForInteractiveGraph: UpdateInterval != 0);
                    await AddDataPointsAsync(measurementHolder);
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
                    lock (measurementHolder.LstPerfCounterData)
                    {
                        foreach (var ctr in measurementHolder.LstPerfCounterData)
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

        async Task AddDataPointsAsync(MeasurementHolder measurementHolder)
        {
            var dictPerfCtrCurrentMeasurements = measurementHolder.GetLastMeasurements();
            if (_dataPoints.Count == 0) // nothing yet
            {
                for (int i = 0; i < NumDataPoints; i++)
                {
                    _dataPoints[i] = new List<uint>(dictPerfCtrCurrentMeasurements.Values); // let all init points be equal, so y axis scales IsStartedFromZero
                }
            }
            else
            {
                _dataPoints[_bufferIndex++] = new List<uint>(dictPerfCtrCurrentMeasurements.Values);
                if (_bufferIndex == _dataPoints.Count) // wraparound?
                {
                    _bufferIndex = 0;
                }
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (DoFullGCPerSample)
            {
                DoGC();// do a GC.Collect on main thread for every sample (the graphing uses memory)
            }
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
            foreach (var entry in dictPerfCtrCurrentMeasurements)
            {
                var series = new Series
                {
                    ChartType = SeriesChartType.Line,
                    Name = entry.Key.ToString()
                };
                _chart.Series.Add(series);
                if (UpdateInterval == 0) // if we're not doing auto update on timer, we're iterating or doing manual measurement
                {
                    series.MarkerSize = 10;
                    series.MarkerStyle = MarkerStyle.Circle;
                }
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
            _chart.Legends.Clear();
            _chart.Legends.Add(new Legend());
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
                    sp.Children.Add(new TextBlock() { Text = $"{ entry._contentType,-15} Ser#={entry._serialNo,3} {entry._dtCreated:hh:mm:ss} {entry._filename}", FontFamily = FontFamilyMono });
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
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await AddStatusMsgAsync(msg, args);
            });
        }

        const int statusTextLenThresh = 100000;
        int nTruncated = 0;
        private CodeSamples _codeSampleControl;

        // we can't use the output window because it will just accumulate and look like a leak
        async public Task AddStatusMsgAsync(string msg, params object[] args)
        {
            // we want to read the threadid 
            //and time immediately on current thread
            var dt = string.Format("[{0}],{1,3},",
                DateTime.Now.ToString("hh:mm:ss:fff")
                , System.Threading.Thread.CurrentThread.ManagedThreadId
                );
            string str;
            if (args.Length > 0)
            {
                str = string.Format(dt + msg, args);
            }
            else
            {
                str = dt + msg;
            }
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

        void BtnClrObjExplorer_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                btnClrObjExplorer.IsEnabled = false;
                await WaitForInitializationCompleteAsync();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                DoGC(); //must be on main thread
                await Task.Delay(TimeSpan.FromSeconds(1));
                await measurementHolderInteractiveUser.CreateDumpAsync(
                    System.Diagnostics.Process.GetCurrentProcess().Id,
                    MemoryAnalysisType.StartClrObjExplorer,
                    desc: "InteractiveDump");

                btnClrObjExplorer.IsEnabled = true;
            });
        }

        private async Task WaitForInitializationCompleteAsync()
        {
            while (measurementHolderInteractiveUser == null)
            {
                await AddStatusMsgAsync("waiting for initialization to complete");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        public void BtnExecCode_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await WaitForInitializationCompleteAsync();
                    if (this.UpdateInterval != 0)
                    {
                        this.UpdateInterval = 0;
                        await ResetPerfCounterMonitorAsync();
                    }
                    if (_ctsExecuteCode == null)
                    {
                        var codeFileToRun = _codeSampleControl.GetSelectedFile();

                        if (string.IsNullOrEmpty(codeFileToRun))
                        {
                            LogMessage($"No single Code file selected");
                            return;
                        }
                        codeFileToRun = Path.Combine(CodeSampleDirectory, codeFileToRun);
                        this.btnExecCode.Content = "Cancel Code Execution";
                        await RunCodeAsync(codeFileToRun);
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
                    _ctsExecuteCode = null;
                }

            });
        }

        private async Task RunCodeAsync(string codeFileToRun)
        {
            await AddStatusMsgAsync($"Starting Code Execution {Path.GetFileName(codeFileToRun)}"); // https://social.msdn.microsoft.com/forums/vstudio/en-US/5066b6ac-fdf8-4877-a023-1a7550f2cdd9/custom-tool-hosting-an-editor-iwpftextviewhost-in-a-tool-window

            await TaskScheduler.Default; // tpool

            var telEvent = new TelemetryEvent(TelemetryEventBaseName + "/ExecCode");
            telEvent.Properties[TelemetryEventBaseName.Replace("/", ".") + ".code"] = Path.GetFileName(codeFileToRun);
            TelemetryService.DefaultSession.PostEvent(telEvent);

            _ctsExecuteCode = new CancellationTokenSource();
            if (_codeExecutor == null)
            {
                _codeExecutor = new CodeExecutor(this);
            }
            var sw = Stopwatch.StartNew();
            using (var compileHelper = _codeExecutor.CompileTheCode(this, codeFileToRun, _ctsExecuteCode.Token))
            {
                if (!string.IsNullOrEmpty(compileHelper.CompileResults))
                {
                    await AddStatusMsgAsync("Result of CompileAndExecute\r\n{0}", compileHelper.CompileResults.ToString());
                }
                else
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var res = compileHelper.ExecuteTheCode();
                    if (res is Task task)
                    {
                        //                   await AddStatusMsgAsync($"CompileAndExecute done: {res}");
                        await task;
                        await AddStatusMsgAsync($"Done Code Execution {Path.GetFileNameWithoutExtension(codeFileToRun)}  {sw.Elapsed.TotalMinutes:n2} Mins");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(res.ToString()))
                        {
                            await AddStatusMsgAsync("Result of CompileAndExecute\r\n{0}", res.ToString());
                        }
                    }
                }
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _ctsExecuteCode = null;
            this.btnExecCode.Content = "ExecCode";
            this.btnExecCode.IsEnabled = true;
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