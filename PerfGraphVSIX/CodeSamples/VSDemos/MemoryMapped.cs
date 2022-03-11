//Desc: Show Memory consumption of MemoryMapped Files

//Include: ..\Util\MyCodeBaseClass.cs
//Include: ..\Util\CloseableTabItem.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Settings;
using System.IO.MemoryMappedFiles;

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

        MyClass(object[] args) : base(args) { }

        public string TxtPath { get; set; } = @"c:\users\calvinh\source\repos\vsdbg";
        public bool CreateView { get; set; } = false;
        public bool TouchMemory { get; set; } = false;

        async Task InitializeAsync()
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
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""28"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <TextBox Text=""{{Binding TxtPath}}"" Width=""400"" Height=""20"" ToolTip="""" />
            <CheckBox Margin=""15,2,0,10"" Content=""TouchMemory""  IsChecked=""{{Binding TouchMemory}}"" 
                ToolTip=""""/>

            <CheckBox Margin=""15,0,0,10"" Content=""CreateView""  IsChecked=""{{Binding CreateView}}"" 
                ToolTip=""""/>
            <Button Content=""Go"" Name=""btnGo""/>

        </StackPanel>
        <Grid Name=""gridUser"" Grid.Row = ""1""></Grid>
    </Grid>
";
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);
            var grid = (Grid)(XamlReader.Load(xamlreader));
            tabItemTabProc.Content = grid;

            grid.DataContext = this;
            var btnGo = (Button)grid.FindName("btnGo");
            var lstFiles = new List<MyMap>();
            btnGo.Click += (o, e) =>
            {
                try
                {
                    _perfGraphToolWindowControl.TabControl.SelectedIndex = 1; // select graph tab
                    var totSize = 0L;
                    foreach (var file in Directory.EnumerateFiles(TxtPath, "*.*",SearchOption.AllDirectories))
                    {
                        var finfo = new FileInfo(file);
                        if (finfo.Length > 0)
                        {
                            var m = new MyMap(finfo, TouchMemory);
                            totSize += m.Length;
                            lstFiles.Add(m);
                        }
                    }
                    var ow = new Window()
                    {
                        Content = $"{lstFiles.Count:n0}    {totSize:n0}  TouchMemory= {TouchMemory} OK?"
                    };
                    ow.ShowDialog();
                    foreach (var f in lstFiles)
                    {
                        f.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(ex.ToString());
                }

            };

        }
    }
    class MyMap : IDisposable
    {
        private readonly FileInfo _FileInfo;
        private readonly MemoryMappedFile? _mappedFile;
        private readonly MemoryMappedViewAccessor? _mapView;
        public long Length = 0;
        public MyMap(FileInfo finfo, bool TouchMemory)
        {
            _FileInfo = finfo;
            try
            {
                _mappedFile = MemoryMappedFile.CreateFromFile(finfo.FullName);
                _mapView = _mappedFile.CreateViewAccessor(offset: 0, size: _FileInfo.Length, MemoryMappedFileAccess.Read);
                Length = _FileInfo.Length;
                if (TouchMemory)
                {
                    for (int pos = 0; pos < Length; pos += 65536)
                    {
                        if (pos + 8 >= Length)
                        {
                            break;
                        }
                        var dat = _mapView.ReadUInt64(position: pos);
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        public void Dispose()
        {
            _mapView?.Dispose();
            _mappedFile?.Dispose();
        }
    }
}
