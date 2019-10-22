using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DumperViewer
{
    public class DumperViewer
    {
        private readonly string[] args;
        private int _pid;
        private List<string> regexes = new List<string>();
        private int _IterNum;
        internal ILogger _logger;

        [STAThread]
        public static void Main(string[] args)
        {
            var oDumper = new DumperViewer(args);
            oDumper.DoMain();
        }

        private void DoMain()
        {
            AsyncPump.Run(async () =>
            {
                await DoitAsync();
            }
            );
        }

        public DumperViewer(string[] args)
        {
            this.args = args;
        }

        private async Task DoitAsync()
        {
            if (args.Length == 0)
            {
                await Task.Delay(1);
                DoShowHelp();
                return;
            }
            int iArg = 0;
            var argsGood = true;
            var extraErrInfo = string.Empty;
            try
            {
                while (iArg < args.Length)
                {
                    var curArg = args[iArg];
                    if (curArg.Length > 1 && "-/".IndexOf(curArg[0]) == 0)
                    {
                        switch (curArg[1].ToString().ToLower())
                        {
                            case "p":
                                this._pid = int.Parse(args[iArg + 1]);
                                break;
                            case "r":
                                var splitRegExes = args[iArg + 1].Split(new[] { '|' });
                                foreach (var split in splitRegExes)
                                {
                                    this.regexes.Add(split);
                                }
                                break;
                            case "i":
                                this._IterNum = int.Parse(args[iArg + 1]);
                                break;
                        }
                    }
                    else
                    {
                        argsGood = false;
                    }
                }
            }
            catch (Exception ex)
            {
                argsGood = false;
                extraErrInfo = ex.ToString();
            }
            if (!argsGood)
            {
                DoShowHelp(extraErrInfo);
            }
        }

        private void DoShowHelp(string extrainfo = "")
        {
            var owin = new Window();
            var tb = new TextBlock()
            {
                Text = $@"DumperViewer: 
{extrainfo}

Create a dump of the speficied process, analyze the dump for the counts of the specified types, 
Command line:
DumpViewer -p 1234 -t .*TextBuffer.*
-p <pid>   Process id of process. Will take a dump of the specified process (e.g. Devenv). Devenv cannot take a dump of itself because it will result in deadlock 
        (creating a dump involves freezing threads) and continue executing launch DumperViewer
        (The creation of the dump and the subsequent analysis needs to be fast)
-i  <n>    iteration #. The dump and the results will be tagged with the iteration prefix. E.G. iterating 700 times will yield dump0.dmp, dump1.dmp,...dump700.dmp.
-t <regex><|regex>  a '|' separated list of regular expressions that specify what types the caller is 'interested in'. e.g. '.*textbuffer.*'|'.*codelens.*'
     can be '|' separated or there can be multiple '-t' arguments

-d <dumpname>  Show the WPF UI treeview for the dump (ClrObjectExplorer)


"
            };

            owin.Content = tb;
            owin.ShowDialog();
        }
    }
}
