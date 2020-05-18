//Include: ExecCodeBase.cs
// this will demonstate leak detection
// 
//Ref: MapFileDict.dll

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;

namespace MyCodeToExecute
{

    public class MyClass : BaseExecCodeClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 7);
            }
        }
        public MyClass(object[] args) : base(args)
        {
            //ShowUI = false;
            //NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
        }

        public override async Task DoInitializeAsync()
        {
            await base.DoInitializeAsync();
            IVsAppCommandLine appCommandLine = await asyncServiceProvider.GetServiceAsync(typeof(SVsAppCommandLine)) as IVsAppCommandLine;
            var hasRootSuffix = 0;
            var rootSuffix = string.Empty;
            appCommandLine.GetOption("rootsuffix", out hasRootSuffix, out rootSuffix);
            logger.LogMessage(string.Format("Root suf {0} {1}", hasRootSuffix, rootSuffix));
//            await OpenASolutionAsync(@"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln");
            await OpenASolutionAsync(@"C:\Users\calvinh\source\repos\WebApp2\WebApp2.sln");
            //            await OpenASolutionAsync(@"C:\Users\calvinh\source\repos\DetourSample\DetourSharedBase.sln");

            await IterateSolutionItemsAsync(async (proj, item, nLevel) =>
            {
                await Task.Yield();
                var fName = string.Empty;
                if (item.FileCount == 1 && item.Name != "OutputPane.cs") // misc files proj
                {
                    try
                    {
                        fName = item.FileNames[0];
                    }
                    catch (ArgumentException ex)
                    {
                        _OutputPane.OutputString(string.Format("ArgEx '{0}' {1}\n", item.Name, ex));
                    }
                }
                _OutputPane.OutputString(string.Format("Item {0} {1} {2} {3} {4}\n", new string(' ', 2 * nLevel), proj.Name, item.Name, fName, item.Kind));
                return true;
            });
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            await Task.Yield();
            var lstWindows = new List<Window>();
            await IterateSolutionItemsAsync(async (proj, item, nLevel) =>
            {
                var fName = string.Empty;
                if (item.FileCount == 1 && item.Name != "OutputPane.cs") // misc files proj
                {
                    try
                    {
                        fName = item.FileNames[0];
                        if (fName.EndsWith("Default.aspx") && proj.Name == "WebApp2") // || fName.EndsWith("vb") || fName.EndsWith("cpp"))
                        {
                            //                            if (fName.Contains("xaml"))
                            //                            if (item.FileNames[0] == @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost\MainWindow.xaml")
                            var w = item.Open(EnvDTE.Constants.vsViewKindDesigner);
                            w.Visible = true;
                            await Task.Delay(TimeSpan.FromSeconds(5));

                            lstWindows.Add(w);
//                            _OutputPane.OutputString(string.Format("Opening'{0}' {1} {2}\n", item.Name, item.Kind, w));
                        }
                        else
                        {
//                            logger.LogMessage(string.Format("reject '{0}' '{1}'", proj.Name, item.FileNames[0]));
                        }
                    }
                    catch (ArgumentException ex)
                    {
//                        _OutputPane.OutputString(string.Format("ArgEx '{0}' {1}\n", item.Name, ex));
                    }
                }
                //                _OutputPane.OutputString(string.Format("Item {0} {1} {2} {3} {4}\n", new string(' ', 2 * nLevel), proj.Name, item.Name, fName, item.Kind));
                return lstWindows.Count < 3 && !token.IsCancellationRequested;
            });
            await Task.Delay(TimeSpan.FromSeconds(10));
            foreach (var win in lstWindows)
            {
                win.Close();
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
        }
    }
}
