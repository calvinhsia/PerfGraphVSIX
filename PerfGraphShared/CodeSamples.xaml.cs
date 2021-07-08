using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PerfGraphVSIX.UserControls
{
    /// <summary>
    /// Interaction logic for CodeSamples.xaml
    /// </summary>
    public partial class CodeSamples : TreeView
    {
        private readonly string _codeSampleDirectory;
        private readonly string _targetToSelect;

        public CodeSamples(string codeSampleDirectory, string targetToSelect)
        {
            InitializeComponent();
            this._codeSampleDirectory = codeSampleDirectory;
            this._targetToSelect = targetToSelect;
//            ToolTip = @"Dbl-click a code sample to open in editor. 
//The selected one will be run with the 'ExecCode' button. Create new files in the same folder as desired. 
//The selection changes to be the most recent edited when the directory changes content.
//Ctrl-Enter on highlighted item will execute it too.
//";
            MaxHeight = 200;
            MaxWidth = 200;
            HorizontalAlignment = HorizontalAlignment.Left;
            AddItems(this, new DirectoryInfo(codeSampleDirectory));
        }
        bool AddItems(ItemsControl ctrl, DirectoryInfo dirInfo)
        {
            var containsTarget = false;
            foreach (var directory in dirInfo.GetDirectories())
            {
                var node = new TreeViewItem() { Header = directory.Name };
                ctrl.Items.Add(node);
                if (AddItems(node, directory))
                {
                    node.IsExpanded = true;
                }
            }
            foreach (var file in dirInfo.GetFiles())
            {
                if (".vb|.cs".Contains(System.IO.Path.GetExtension(file.Name).ToLower()))
                {
                    var tvItem = new MyFileTreeviewItem() { Header = file.Name, FullFileName = file.FullName };
                    ctrl.Items.Add(tvItem);
                    if (file.Name == _targetToSelect)
                    {
                        containsTarget = true;
                        tvItem.IsSelected = true;
                    }
                }
            }
            return containsTarget;
        }
        public string GetSelectedFile()
        {
            var selected = string.Empty;
            if (this.SelectedItem is MyFileTreeviewItem itm)
            {
                selected = itm.FullFileName;
            }
            return selected;
        }

        class MyFileTreeviewItem : TreeViewItem
        {
            public string FullFileName { get; set; }
            //            public new object ToolTip => GetTip();
            public MyFileTreeviewItem()
            {
                var ttip = new ToolTip();
                this.ToolTip = ttip;
                ttip.Opened += (o, e) =>
                {
                    ttip.Content = GetTip();
                };
                this.MouseDoubleClick += (o, e) =>
                {
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                    PerfGraphToolWindowCommand.Instance.g_dte.ExecuteCommand("File.OpenFile", $@"""{FullFileName}""");
                };
            }
            protected override void OnPreviewKeyDown(KeyEventArgs e)
            {
                base.OnPreviewKeyDown(e);
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                if (e.Key == Key.Enter)
                {
                    var isCtrlKeyDown = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control);
                    try
                    {
                        if (isCtrlKeyDown)
                        {
                            PerfGraphToolWindowControl.g_PerfGraphToolWindowControl.BtnExecCode_Click(this, new RoutedEventArgs(Button.ClickEvent));
                        }
                        else
                        {
                            PerfGraphToolWindowCommand.Instance.g_dte.ExecuteCommand("File.OpenFile", FullFileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _ =PerfGraphToolWindowControl.g_PerfGraphToolWindowControl.AddStatusMsgAsync("Exception " + ex.ToString());

                    }
                    e.Handled = true;
                }
            }
            public string GetTip()
            {
                var tip = FullFileName;
                var cmnt = System.IO.Path.GetExtension(FullFileName).ToLower() == ".cs" ? "//" : "'";
                var desc = $"{cmnt}Desc:";
                var txt = string.Join(
                    Environment.NewLine,
                    File.ReadAllLines(FullFileName)
                    .Where(lin => lin.StartsWith(desc))
                    .Select(lin => lin.Substring(desc.Length)));
                if (!string.IsNullOrEmpty(txt))
                {
                    tip = txt + Environment.NewLine + tip;
                }
                return tip;
            }
        }
    }
}
