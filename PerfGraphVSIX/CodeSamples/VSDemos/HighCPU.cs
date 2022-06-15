//Desc: Cause High CPU to be used, monitor telemetry

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll
//Include: ..\Util\MyCodeBaseClass.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using Microsoft.VisualStudio.Telemetry;
using Microsoft.VisualStudio.Telemetry.Notification;
using System.Text;

namespace MyCodeToExecute
{
    public class MyClass : MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.InitializeAsync();
        }

        public bool UseOutputPane { get; set; } = false;
        public bool ShowAllProperties { get; set; } = true;
        public string EventFilter { get; set; } = "vs/core/perf/cpuusage";

        MyClass(object[] args) : base(args) { }
        async Task InitializeAsync()
        {
            await Task.Yield();
            var strxaml =
$@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        >
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
            <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""25"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
                <CheckBox Margin=""15,0,0,10"" Content=""OutputPane""  IsChecked=""{{Binding UseOutputPane}}""  
                    ToolTip=""Output to Debug OutputPane 'PerfGraphVSIX'""/>
                <CheckBox Margin=""15,0,0,10"" Content=""ShowAllProperties""  IsChecked=""{{Binding ShowAllProperties}}"" 
                    ToolTip=""Show the properties of the Telemetry event""/>
                <Label Content=""Filter""/>
                <TextBox Text=""{{Binding EventFilter}}"" Width =""200""
                    ToolTip=""Filter the events""/>
                <Button Name=""btnUpdateFilter"" Content=""UpdateFilter"" Background=""LightGray""/>
                <Button Name = ""btnGo"" Margin = ""20,0,0,0"" Content = ""Go"" ToolTip = ""Start/Stop consuming CPU"" />
            </StackPanel>
            <Grid Name=""gridUser"" Grid.Row = ""1"">
        </Grid>
    </Grid>
";
            var grid = (Grid)XamlReader.Parse(strxaml);
            CloseableTabItem tabItemTabProc = GetTabItem();
            tabItemTabProc.Content = grid;
            grid.DataContext = this;
            var gridUser = (Grid)grid.FindName("gridUser");
            var btnUpdateFilter = (Button)grid.FindName("btnUpdateFilter");
            var btnGo = (Button)grid.FindName("btnGo");
            var IsGoing = false;
            Task taskEatCpu = null;
            CancellationTokenSource ctsEatCpu = null;
            btnGo.Click += async (_, __) =>
            {
                try
                {
                    if (!IsGoing)
                    {
                        IsGoing = true;
                        ctsEatCpu = new CancellationTokenSource();
                        var dtEatStartTime = DateTime.Now;
                        _logger.LogMessage($"Starting to eat CPU at {dtEatStartTime:hh:mm:ss}");
                        _perfGraphToolWindowControl.TabControl.SelectedIndex = 0; // select graph tab
                        taskEatCpu = Task.Run(async () => // start task on background thread
                        {
                            try
                            {
                                int cnt = 0;
                                var dtReport = DateTime.Now;
                                while (!ctsEatCpu.IsCancellationRequested)
                                {
                                    if ((DateTime.Now - dtReport).TotalSeconds > 30)
                                    {
                                        _logger.LogMessage($"eating cpu {cnt} ElapsedMinutes = {(DateTime.Now - dtEatStartTime).TotalMinutes:n2}");
                                        dtReport = DateTime.Now;
                                    }
                                    cnt++;
                                    ////await Task.Delay(TimeSpan.FromSeconds(1), ctsEatCpu.Token);
                                }
                                _logger.LogMessage($"Task stopped eating CPU");
                            }
                            catch (OperationCanceledException)
                            {
                            }
                        });
                        btnGo.Content = "Stop";
                    }
                    else
                    {
                        IsGoing = false;
                        ctsEatCpu.Cancel();
                        await taskEatCpu;
                        _logger.LogMessage($"Stopped eating CPU");
                        btnGo.Content = "Go";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(ex.ToString());
                }
            };
            var outputPane = await GetOutputPaneAsync();
            int subscriptionId = 0;
            tabItemTabProc.TabItemClosed += async (o, e) =>
            {
                _logger.LogMessage("tabitemclosed event");
                await TaskScheduler.Default;
                DoUnsubscribe();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            };
            btnUpdateFilter.Click += async (o, e) =>
             {
                 await TaskScheduler.Default;
                 DoUnsubscribe();
                 DoSubScribe();
             };
            btnUpdateFilter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void DoSubScribe()
            {
                var startcond = new EventMatch(this.EventFilter);
                subscriptionId = TelemetryNotificationService.Default.Subscribe(startcond, (telEvent) =>
                {
                    var output = new StringBuilder(telEvent.ToString());
                    switch (telEvent.Name)
                    {
                        case "vs/core/command":
                            output.Append(":" + telEvent.Properties["vs.core.command.name"]);
                            if (ShowAllProperties)
                            {
                                foreach (var item in telEvent.Properties)
                                {
                                    output.AppendLine($"  {item.Key}  {item.Value}");
                                }
                            }
                            break;
                        case "vs/core/perf/cpuusage":
                            if (ShowAllProperties)
                            {
                                foreach (var item in telEvent.Properties)
                                {
                                    output.AppendLine($"  {item.Key}  {item.Value}");
                                }
                            }
                            break;
                    }
                    if (UseOutputPane)
                    {
                        outputPane.OutputString(output.ToString() + Environment.NewLine);
                    }
                    else
                    {
                        _logger.LogMessage(output.ToString());
                    }
                }, singleNotification: false);
                _logger.LogMessage($"Subscribed to telemetry events. SubscriptionId={subscriptionId} EventFilter '{EventFilter}'");
            }
            void DoUnsubscribe()
            {
                if (subscriptionId != 0)
                {
                    TelemetryNotificationService.Default.Unsubscribe(subscriptionId);
                    _logger.LogMessage($"Unsubscribe subscriptionId={subscriptionId} Filter='{EventFilter}'");
                }
            }
        }
    }

    public class EventMatch : ITelemetryEventMatch
    {
        readonly string _eventFilter;
        public EventMatch(string eventFilter)
        {
            this._eventFilter = eventFilter;
        }
        public bool IsEventMatch(TelemetryEvent telemetryEvent)
        {
            if (!string.IsNullOrEmpty(_eventFilter))
            {
                return telemetryEvent.ToString().IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return true;
        }
    }
}
