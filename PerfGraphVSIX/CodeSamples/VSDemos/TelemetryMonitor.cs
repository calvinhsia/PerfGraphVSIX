//Desc: Monitor Telemetry Events

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll
//Include: ..\Util\MyCodeBaseClass.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
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
            try
            {
                await oMyClass.InitializeAsync();
            }
            catch (Exception ex)
            {
                var _logger = args[1] as ILogger;
                _logger.LogMessage(ex.ToString());
            }
        }

        public bool UseOutputPane { get; set; } = false;
        public string EventFilter { get; set; }

        MyClass(object[] args) : base(args) { }
        async Task InitializeAsync()
        {
            await Task.Yield();
            CloseableTabItem tabItemTabProc = GetTabItem();

            var ctsCancelMonitor = new CancellationTokenSource();
            tabItemTabProc.TabItemClosed += (o, e) =>
            {
                _logger.LogMessage("tabitemclosed event");
                ctsCancelMonitor.Cancel();
                _perfGraphToolWindowControl.TabControl.SelectedIndex = 0;
            };

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
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""25"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <CheckBox Margin=""15,0,0,10"" Content=""OutputPane""  IsChecked=""{{Binding UseOutputPane}}"" Name=""ChkBoxMonitor"" 
                ToolTip=""Output to OutputPane?""/>
            <Label Content=""Filter""/>
            <TextBox Text=""{{Binding EventFilter}}"" Width =""200""
                ToolTip=""Filter the events""/>
            <Button Name=""btnUpdateFilter"" Content=""UpdateFilter"" Background=""LightGray""/>
        </StackPanel>
        <Grid Name=""gridUser"" Grid.Row = ""1"">
        </Grid>
    </Grid>
";
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);
            var grid = (Grid)(XamlReader.Load(xamlreader));
            tabItemTabProc.Content = grid;
            grid.DataContext = this;
            var gridUser = (Grid)grid.FindName("gridUser");
            var btnUpdateFilter = (Button)grid.FindName("btnUpdateFilter");
            var outputPane = await GetOutputPaneAsync();

            JoinableTask taskSubscribe = null;
            btnUpdateFilter.Click += async (o, e) =>
             {
                 try
                 {
                     await TaskScheduler.Default;
                     ctsCancelMonitor.Cancel();
                     if (taskSubscribe != null)
                     {
                         await taskSubscribe;
                     }
                     ctsCancelMonitor = new CancellationTokenSource();
                     DoSubScribe();
                 }
                 catch (Exception ex)
                 {
                     _logger.LogMessage(ex.ToString());
                 }
             };
            btnUpdateFilter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void DoSubScribe()
            {
                var startcond = new EventMatch(this);
                var subscriptionId = TelemetryNotificationService.Default.Subscribe(startcond, (telEvent) =>
                {
                    var output = new StringBuilder(telEvent.ToString());
                    switch (telEvent.Name)
                    {
                        case "vs/core/command":
                            output.Append(":" + telEvent.Properties["vs.core.command.name"]);
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
                _logger.LogMessage($"Subscribed subscriptionId={subscriptionId} EventFilter '{EventFilter}'");
                taskSubscribe = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                  {
                      while (!ctsCancelMonitor.IsCancellationRequested)
                      {
                          await TaskScheduler.Default;
                          await Task.Delay(TimeSpan.FromSeconds(1));
                      }
                      _logger.LogMessage($"Unsubscribe subscriptionId={subscriptionId} Filter='{EventFilter}'");
                      TelemetryNotificationService.Default.Unsubscribe(subscriptionId);
                  });
            }
        }
    }

    public class EventMatch : ITelemetryEventMatch
    {
        readonly MyClass myClass;
        public EventMatch(MyClass myClass)
        {
            this.myClass = myClass;
        }
        /// <summary>
        /// Indicates whether the specified <see cref="TelemetryEvent"/> satisfies this filter.
        /// </summary>
        /// <param name="telemetryEvent">The <see cref="TelemetryEvent"/> to check against this filter.</param>
        /// <returns>true if this filter is satisfied; otherwise, false.</returns>
        public bool IsEventMatch(TelemetryEvent telemetryEvent)
        {
            if (!string.IsNullOrEmpty(myClass.EventFilter))
            {
                return telemetryEvent.ToString().IndexOf(myClass.EventFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return true;
        }
    }
}
