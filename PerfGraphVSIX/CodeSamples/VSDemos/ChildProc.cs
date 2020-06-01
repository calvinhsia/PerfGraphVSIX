//Desc: Show the child processes of Devenv in a treeview

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

        public double RefreshRate { get; set; } = 1;
        public bool Monitor { get; set; } = true;
        public bool OnlyChanges { get; set; } = false;

        MyClass(object[] args) : base(args) { }
        async Task InitializeAsync()
        {
            await Task.Yield();
            CloseableTabItem tabItemTabProc = null;
            int iTabItemIndex = 0;
            foreach (var tabitem in _perfGraphToolWindowControl.TabControl.Items)
            {
                if ((tabitem as CloseableTabItem)?.tabName == Path.GetFileNameWithoutExtension(_FileToExecute))
                {
                    tabItemTabProc = (CloseableTabItem)tabitem; // found an existing one. Need to close it
                    tabItemTabProc.CloseTabItem();
                    tabItemTabProc = null;
                    break;
                }
                iTabItemIndex++;
            }
            var ctsCancelMonitor = new CancellationTokenSource();
            if (tabItemTabProc == null)
            {
                tabItemTabProc = new CloseableTabItem(Path.GetFileNameWithoutExtension(_FileToExecute), "Monitor Child Processes");
                tabItemTabProc.TabItemClosed += (o, e) =>
                 {
                     //_logger.LogMessage("close event");
                     ctsCancelMonitor.Cancel();
                     _perfGraphToolWindowControl.TabControl.SelectedIndex = 0;
                 };
                _perfGraphToolWindowControl.TabControl.Items.Add(tabItemTabProc);
            }
            _perfGraphToolWindowControl.TabControl.SelectedIndex = iTabItemIndex; // select User output tab

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
        <Canvas Margin=""311,0,0,0.5"">
            <Ellipse Name=""Ball"" Width=""24"" Height=""24"" Fill=""Blue""
             Canvas.Left=""396"">
                <Ellipse.Triggers>
                    <EventTrigger RoutedEvent=""Ellipse.Loaded"">
                        <BeginStoryboard>
                            <Storyboard TargetName=""Ball"" RepeatBehavior=""Forever"">
                                <DoubleAnimation
                                Storyboard.TargetProperty=""(Canvas.Left)""
                                From=""96"" To=""300"" Duration=""0:0:1""
                                AutoReverse=""True"" />
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
<!--                    <EventTrigger RoutedEvent=""CheckBox.Checked"" SourceName=""ChkBoxMonitor"">
                      <PauseStoryboard BeginStoryboardName=""MyBeginStoryboard"" />
                    </EventTrigger>
                    <EventTrigger RoutedEvent=""CheckBox.Unchecked"" SourceName=""ChkBoxMonitor"">
                      <ResumeStoryboard BeginStoryboardName=""MyBeginStoryboard"" />
                    </EventTrigger>
-->

                </Ellipse.Triggers>
            </Ellipse>
        </Canvas>

        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""25"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <Label Content=""Refresh Rate""/>
            <TextBox Text=""{{Binding RefreshRate}}"" Width=""40"" Height=""20"" ToolTip=""Seconds. Refresh means check child proces. UI won't update UI if tree is same"" />
            <CheckBox Margin=""15,0,0,10"" Content=""Monitor""  IsChecked=""{{Binding Monitor}}"" Name=""ChkBoxMonitor"" 
                ToolTip=""Monitor Child Processes""/>
            <CheckBox Margin=""15,0,0,10"" Content=""Update if change in processes only""  IsChecked=""{{Binding OnlyChanges}}"" 
                ToolTip=""Update the tree only on tree node changes (procId and proc name)""/>
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

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                ChildProcTree childProcTree = new ChildProcTree();
                try
                {
                    gridUser.Children.Clear();
                    gridUser.Children.Add(childProcTree);
                    int hashLastTree = 0;
                    DateTime dtlastTree;
                    while (!ctsCancelMonitor.IsCancellationRequested)
                    {
                        await TaskScheduler.Default;
                        if (Monitor)
                        {
                            var devenvTree = ProcessEx.GetProcessTree(Process.GetCurrentProcess().Id);
                            var curHash = 0;
                            IterateTreeNodes(devenvTree, level: 0, func: (node, level) =>
                            {
                                curHash += node.ProcEntry.szExeFile.GetHashCode() + node.procId.GetHashCode();
                                return true;
                            });
                            if (curHash != hashLastTree)
                            {
                                dtlastTree = DateTime.Now;
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                childProcTree.Items.Clear();
                                childProcTree.AddNodes(childProcTree, devenvTree);
                                childProcTree.ToolTip = $"Refreshed {dtlastTree}";
                                await TaskScheduler.Default;
                                if (OnlyChanges)
                                {
                                    hashLastTree = curHash;
                                }
                            }
                        }
                        //_logger.LogMessage("in loop");
                        await Task.Delay(TimeSpan.FromSeconds(RefreshRate), ctsCancelMonitor.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(ex.ToString());
                }
                //_logger.LogMessage("Monitor done");
                //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                //childProcTree.Background = Brushes.Cornsilk;
            });
        }

        class ChildProcTree : TreeView
        {
            public ChildProcTree()
            {
                this.FontFamily = new FontFamily("Consolas");
                this.FontSize = 10;
            }
            public void AddNodes(ItemsControl itemsControl, List<ProcessEx.ProcNode> lstNodes)
            {
                foreach (var node in lstNodes)
                {
                    if (node.ProcEntry.szExeFile != "conhost.exe")
                    {
                        long memsize = 0;
                        try
                        {
                            var proc = Process.GetProcessById(node.procId);
                            memsize = proc.PrivateMemorySize64;
                        }
                        catch (ArgumentException) // process is not running
                        {

                        }
                        var spData = new StackPanel() { Orientation = Orientation.Horizontal };
                        var tbThrds = new TextBlock()
                        {
                            Margin = new Thickness(3, 0, 0, 0),
                            Text = $"Thds={node.ProcEntry.cntThreads,3}",
                            Background = Brushes.LightBlue
                        };
                        spData.Children.Add(tbThrds);
                        var tbMem = new TextBlock()
                        {
                            Text = $"Mem={memsize,12:n0}",
                            Margin = new Thickness(3, 0, 0, 0),
                            Background = Brushes.LightSalmon
                        };
                        spData.Children.Add(tbMem);
                        var tb = new TextBlock()
                        {
                            Text = $"{node.ProcEntry.th32ProcessID,-5} {node.ProcEntry.szExeFile}",
                            Margin = new Thickness(3, 0, 0, 0),
                        };
                        spData.Children.Add(tb);
                        var newItem = new TreeViewItem() { Header = spData };
                        newItem.IsExpanded = true;
                        itemsControl.Items.Add(newItem);
                        if (node.Children != null)
                        {
                            AddNodes(newItem, node.Children);
                        }
                    }
                }
            }
        }

        bool StopIter = false;
        void IterateTreeNodes(List<ProcessEx.ProcNode> nodes, int level, Func<ProcessEx.ProcNode, int, bool> func)
        {
            if (level == 0) // initialize recursion
            {
                StopIter = false;
            }
            foreach (var node in nodes)
            {
                if (func(node, level))
                {
                    if (node.Children != null)
                    {
                        IterateTreeNodes(node.Children, level + 1, func);
                    }
                }
                else
                {
                    StopIter = true;
                }
                if (StopIter) // if any recursive call has set it, break
                {
                    break;
                }
            }
        }

        public async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            await TaskScheduler.Default;
            var procToMonitor = "XDesProc.exe";
            var lstProcToMonitor = new List<ProcessEx.ProcNode>();
            _logger.LogMessage("Monitoring Child Processes " + procToMonitor);
            while (!token.IsCancellationRequested)
            {
                await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    var curlstProcToMonitor = new List<ProcessEx.ProcNode>();
                    var devenvTree = ProcessEx.GetProcessTree(Process.GetCurrentProcess().Id);
                    IterateTreeNodes(devenvTree, level: 0, func: (node, level) =>
                     {
                         if (node.ProcEntry.szExeFile.IndexOf(procToMonitor, StringComparison.OrdinalIgnoreCase) >= 0)
                         {
                             curlstProcToMonitor.Add(node);
                         }
                         return true;
                     });
                    if (curlstProcToMonitor.Count != lstProcToMonitor.Count ||
                    (
                        curlstProcToMonitor.Where(p => lstProcToMonitor.Where(q => q.procId != p.procId).Any()).Any() ||
                        lstProcToMonitor.Where(p => curlstProcToMonitor.Where(q => q.procId != p.procId).Any()).Any()
                    ))
                    {
                        int level = 0;
                        foreach (var node in curlstProcToMonitor)
                        {
                            _logger.LogMessage(string.Format("{0} {1} {2} {3}", new string(' ', level * 2), node.procId, node.ParentProcId, node.ProcEntry.szExeFile));
                        }
                        lstProcToMonitor.Clear();
                        lstProcToMonitor.AddRange(curlstProcToMonitor);
                        //IterateTreeNodes(devenvTree, level: 0, func: (node, level) =>
                        //  {
                        //      if (node.ProcEntry.szExeFile.IndexOf(procToMonitor, StringComparison.OrdinalIgnoreCase) >= 0)
                        //      {
                        //          _OutputPane.OutputString(string.Format("{0} {1} {2} {3}", new string(' ', level * 2), node.procId, node.ParentProcId, node.ProcEntry.szExeFile) + Environment.NewLine);
                        //          lstProcToMonitor.Add(node.ProcEntry.th32ProcessID);
                        //      }
                        //      return true;
                        //  });
                    }
                });
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
    }


    class ProcessEx
    {
        //inner enum used only internally
        [Flags]
        private enum SnapshotFlags : uint
        {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            Inherit = 0x80000000,
            All = 0x0000001F,
            NoHeaps = 0x40000000
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PROCESSENTRY32
        {
            const int MAX_PATH = 260;
            public UInt32 dwSize;
            public UInt32 cntUsage;
            public Int32 th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public UInt32 th32ModuleID;
            public UInt32 cntThreads;
            public Int32 th32ParentProcessID;
            public Int32 pcPriClassBase;
            public UInt32 dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szExeFile;
        }

        public class ProcNode
        {
            public ProcNode(PROCESSENTRY32 procEntry)
            {
                this.ProcEntry = procEntry;
            }

            public PROCESSENTRY32 ProcEntry;

            public int procId { get { return ProcEntry.th32ProcessID; } }
            public int ParentProcId { get { return ProcEntry.th32ParentProcessID; } }
            public List<ProcNode> Children;
            public override string ToString()
            {
                return string.Format("{0} {1} {2}", procId, ParentProcId, ProcEntry.szExeFile);
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr CreateToolhelp32Snapshot([In] UInt32 dwFlags, [In] UInt32 th32ProcessID);

        [DllImport("kernel32", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool Process32First([In] IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool Process32Next([In] IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle([In] IntPtr hObject);


        /// <summary>
        ///  in one pass, produce a tree of nodes
        /// </summary>
        /// <param name="RootPid">If 0, entire tree. Else tree rooted from specified pid, and list length will == 1 or 0</param>
        /// <returns></returns>
        public static List<ProcNode> GetProcessTree(int RootPid = 0)
        {
            var dictprocNodesById = new Dictionary<int, ProcNode>(); // index ProcId
            GetProcesses(p => { dictprocNodesById[p.th32ProcessID] = new ProcNode(p); return true; });

            var lstNodeRoots = new List<ProcNode>();
            foreach (var proc in dictprocNodesById.Values)
            {// every node must be added to result as either an orphan (root is parentless, so is an orphan) or a child. Pid=0 is "[System Process] or "Idle"
                if (proc.ParentProcId == 0) // 'System' with pid typically 4
                {
                    lstNodeRoots.Add(proc);
                }
                else
                { // the current node's parent is non-zero: find the parent. Add the cur node as a child to the parent node if found (else orphaned)
                    ProcNode parentNode;
                    if (!dictprocNodesById.TryGetValue(proc.ParentProcId, out parentNode))
                    { // parent proc doesn't exist. Must be orphan (can still have children)
                        lstNodeRoots.Add(proc);
                    }
                    else
                    { // found parent: add cur as child
                        if (parentNode.Children == null)
                        {
                            parentNode.Children = new List<ProcNode>();
                        }
                        parentNode.Children.Add(proc);
                    }
                }
            }
            if (RootPid != 0)
            {
                lstNodeRoots.Clear();
                ProcNode procNode;
                if (dictprocNodesById.TryGetValue(RootPid, out procNode))
                {
                    lstNodeRoots.Add(procNode);
                }
            }
            return lstNodeRoots;
        }

        public static void GetProcesses(Func<PROCESSENTRY32, bool> action)
        {
            IntPtr handleToSnapshot = IntPtr.Zero;
            try
            {
                PROCESSENTRY32 procEntry = new PROCESSENTRY32();
                procEntry.dwSize = (UInt32)Marshal.SizeOf(typeof(PROCESSENTRY32));
                handleToSnapshot = CreateToolhelp32Snapshot((uint)SnapshotFlags.Process, 0);
                if (Process32First(handleToSnapshot, ref procEntry))
                {
                    do
                    {
                        if (!action(procEntry))
                        {
                            break;
                        }
                    } while (Process32Next(handleToSnapshot, ref procEntry));
                }
                else
                {
                    throw new ApplicationException(string.Format("Failed with win32 error code {0}", Marshal.GetLastWin32Error()));
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Can't get the process.", ex);
            }
            finally
            {
                // Must clean up the snapshot object!
                CloseHandle(handleToSnapshot);
            }
        }
    }

}
