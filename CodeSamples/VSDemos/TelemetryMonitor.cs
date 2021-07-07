//Desc: Monitor Telemetry Events. Like telemetry monitor, but highly customizable and 
//Desc: doesn't require a separate process like TelemetryMonitor

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
        public bool ShowAllProperties { get; set; } = false;
        public string EventFilter { get; set; }

        MyClass(object[] args) : base(args) { }
        async Task InitializeAsync()
        {
            await Task.Yield();
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
            <CheckBox Margin=""15,0,0,10"" Content=""OutputPane""  IsChecked=""{{Binding UseOutputPane}}""  
                ToolTip=""Output to Debug OutputPane 'PerfGraphVSIX'""/>
            <CheckBox Margin=""15,0,0,10"" Content=""ShowAllProperties""  IsChecked=""{{Binding ShowAllProperties}}"" 
                ToolTip=""Show the properties of the Telemetry event""/>
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
            CloseableTabItem tabItemTabProc = GetTabItem();
            tabItemTabProc.Content = grid;
            grid.DataContext = this;
            var gridUser = (Grid)grid.FindName("gridUser");
            var btnUpdateFilter = (Button)grid.FindName("btnUpdateFilter");
            var outputPane = await GetOutputPaneAsync();
            int subscriptionId = 0;
            tabItemTabProc.TabItemClosed += async (o, e) =>
            {
                _logger.LogMessage("tabitemclosed event");
                await TaskScheduler.Default;
                DoUnsubscribe();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _perfGraphToolWindowControl.TabControl.SelectedIndex = 0; // select main tab
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
