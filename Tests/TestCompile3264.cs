using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class TestCompile3264 : BaseTestClass
    {

        bool ShouldRunTest()
        {
            //LogMessage($"{Process.GetCurrentProcess().MainModule.FileName}"); // C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\Extensions\TestPlatform\testhost.net472.x86.exe
            var curProcMainModule = Process.GetCurrentProcess().MainModule.FileName;
            bool Is32bitVSHost = curProcMainModule.Contains("2019");
            if (Is32bitVSHost && IntPtr.Size == 8 || (!Is32bitVSHost && IntPtr.Size == 4))
            {
                LogMessage($"We can't run code that references the VS SDK from a different VS version");
                return false;
            }
            return true;
        }
        [TestMethod]
        public async Task TestCompileAllCodeSamplesAsync()
        {
            if (!ShouldRunTest())
            {
                return;
            }
            await Task.Yield();
            int nCompiled = 0;
            int nErrors = 0;
            var thisasmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); // C:\Users\calvinh\source\repos\Stress\TestStress\bin\Debug
            var upone = thisasmDir;
            var pathtosamples = "";
            while (true)
            {
                upone = Path.GetDirectoryName(upone);
                pathtosamples = Path.Combine(upone, @"PerfGraphVSIX", "CodeSamples");
                if (Directory.Exists(pathtosamples))
                {
                    break;
                }
            }
            foreach (var codesample in Directory.EnumerateFiles(pathtosamples, "*.*", SearchOption.AllDirectories)
                        .Where(f => ".vb|.cs".Contains(Path.GetExtension(f).ToLower())))
            {
                //LogMessage($"Compiling {codesample}");
                {
                    if (!codesample.Contains(@"Util\"))
                    {
                        //                        if (codesample.Contains("AAr"))
                        {
                            //                            LogMessage($"Compiling {codesample}");
                            nCompiled++;
                            var codeExecutor = new CodeExecutor(logger: this);
                            using (var compileHelper = codeExecutor.CompileTheCode(null, codesample, CancellationToken.None))
                            {
                                if (!string.IsNullOrEmpty(compileHelper.CompileResults))
                                {
                                    //                                if (!(codesample.Contains("LeakBaseClass") && res.ToString().Contains("Couldn't find static Main")))
                                    {
                                        nErrors++;
                                        LogMessage($"Error Compiling '{Path.GetFileNameWithoutExtension(codesample)}' " + compileHelper.CompileResults);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            LogMessage($"#Compiled Fies= {nCompiled}  #Errors = {nErrors}");
            Assert.IsTrue(nCompiled > 35, $"Didn't compile all files: compiled only {nCompiled}");
            Assert.AreEqual(0, nErrors, $"# Files with Compile Errors = {nErrors}");
        }
        public const string sampleVSCodeToExecute = @"
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview""
//Pragma: verbose=true
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref32: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll""
//Ref32: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll""
//Ref32: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll""
//Ref32: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll""
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll
//Ref32:""%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll""

//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll
////Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll
////Ref64: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Threading.dll
//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll
//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Threading.dll

//Ref: %PerfGraphVSIX%

//to add .net refs:
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCustomCode
{
    public class MyClass
    {
        string SolutionToLoad = @""C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln"";
        int NumberOfIterations = 7;
        int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationToken _CancellationToken;
        JoinableTask _tskDoPerfMonitoring;
        ILogger logger;
        Action<string> actTakeSample;
        public EnvDTE.DTE g_dte;

        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.DoSomeWorkAsync();
        }
        public MyClass(object[] args)
        {
            _tcs = new TaskCompletionSource<int>();
            logger = args[1] as ILogger;
            _CancellationToken = (CancellationToken)args[2]; // value type
            g_dte= args[3] as EnvDTE.DTE;
        }
        private async Task DoSomeWorkAsync()
        {
//            logger.LogMessage(""in DoSomeWorkAsync"");
            try
            {
                if (nTimes++ == 0)
                {
                    logger.LogMessage(""Registering solution events"");
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;

                    //foreach (EnvDTE.Window win in g_dte.Windows)
                    //{
                    //    logger.LogMessage(""Win "" + win.Kind + "" ""+ win.ToString());
                    //    if (win.Kind == ""Document"") // ""Tool""
                    //    {
                    //        logger.LogMessage(""   "" + win.Document.Name);
                    //    }
                    //}
                    //g_dte.ExecuteCommand(""File.OpenFile"", @""C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs"");
                    // https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.sendkeys?view=netframework-4.8
                    //System.Windows.Forms.SendKeys.Send(""asdf""); // SendKeys.Send(""{ENTER}"");
                    //await Task.Delay(1000);



                    //var ox = new System.Windows.Window();
                    //var tb = new System.Windows.Controls.TextBox()
                    //{
                    //    AcceptsReturn = true
                    //};
                    //ox.Content = tb;
                    //ox.ShowDialog();


                    //g_dte.ExecuteCommand(""File.OpenFile"", @""C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs"");
                    //g_dte.ExecuteCommand(""File.NewFile"", ""temp.cs"");
                    //System.Windows.Forms.SendKeys.Send(""using System;{ENTER}"");
                    //await Task.Delay(1000);
                    //System.Windows.Forms.SendKeys.Send(""class testing {{}"");
                    //await Task.Delay(1000);
                    //Func<Task> undoAll = async () =>
                    //  {
                    //      var done = false;
                    //      logger.LogMessage(""Start undo loop"");
                    //      while (!done)
                    //      {
                    //          try
                    //          {
                    //              logger.LogMessage("" in undo loop"");
                    //              g_dte.ExecuteCommand(""Edit.Undo"");
                    //              await Task.Delay(100);
                    //          }
                    //          catch (Exception)
                    //          {
                    //              done = true;
                    //              logger.LogMessage(""Done undo loop"");
                    //          }
                    //      }
                    //  };
                    //await undoAll();
                    //g_dte.ExecuteCommand(""File.Close"", @"""");
                    //await Task.Delay(1000);




                }
                // Keep in mind that the UI will be unresponsive if you have no await and no main thread idle time
                var measurementHolder = new MeasurementHolder(
                    ""testTODOTODO"",
                    new StressUtilOptions() { NumIterations = -1},
                    SampleType.SampleTypeIteration);


                for (int i = 0; i < NumberOfIterations && !_CancellationToken.IsCancellationRequested; i++)
                {
                    var desc = string.Format(""Start of Iter {0}/{1}"", i + 1, NumberOfIterations);
                    DoSample(desc);
                    await Task.Delay(1000); // wait one second to allow UI thread to catch  up
//                    logger.LogMessage(desc);
                    await OpenASolutionAsync();
                    if (_CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    await CloseTheSolutionAsync();
//                    logger.LogMessage(""End of Iter {0}"", i);
                }
                var msg = ""Cancelled Code Execution"";
                if (!_CancellationToken.IsCancellationRequested)
                {
                    msg = string.Format(""Done all {0} iterations"", NumberOfIterations);
                }
                DoSample(msg);
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());
            }
            finally
            {
                logger.LogMessage(""UnRegistering solution events"");
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
            }
        }

        void DoSample(string desc)
        {
            if (actTakeSample != null)
            {
                actTakeSample(desc);
            }
        }

        async Task OpenASolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Open(SolutionToLoad);
            await _tcs.Task;
            if (!_CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        async Task CloseTheSolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();
            if (!_CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
//            logger.LogMessage(""SolutionEvents_OnAfterCloseSolution"");
            _tcs.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
//            logger.LogMessage(""SolutionEvents_OnAfterBackgroundSolutionLoadComplete"");
            _tcs.TrySetResult(0);
        }
    }
}
";

        [TestMethod]
        public void TestCompileVSCode()
        {
            if (!ShouldRunTest())
            {
                return;
            }
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, sampleVSCodeToExecute);
            using (var compileHelper = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None))
            {
                if (!string.IsNullOrEmpty(compileHelper.CompileResults))
                {
                    throw new InvalidOperationException(compileHelper.CompileResults);
                }
                var res = compileHelper.ExecuteTheCode();
                if (res is string resString)
                {
                    Assert.Fail(resString);
                }
                var task = res as Task;
                if (task.IsFaulted)
                {
                    LogMessage($"Faulted task {task.Exception}");
                    Assert.IsTrue(task.Exception.ToString().Contains(" The SVsSolution service is unavailable."));
                }
            }
            //            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in DoSomeWorkAsync")).FirstOrDefault());


            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Registering solution events")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("UnRegistering solution events")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("The SVsSolution service is unavailable")).FirstOrDefault());
        }

        [TestMethod]
        [Ignore]
        public void TestAsmVersions()
        {
            if (IntPtr.Size == 4)
            {
                return;
            }
            var targPath = @"C:\Program Files\Microsoft Visual Studio\2022\Preview\";
            var targAsm = "Microsoft.VisualStudio.Threading.dll";
            void searchForIt(string path)
            {
                var fileName = Path.Combine(path, targAsm);
                if (File.Exists(fileName))
                {
                    var asm = Assembly.LoadFrom(fileName);
                    TestContext.WriteLine($"{asm.FullName} {fileName}");
                }
                foreach (var subdir in Directory.GetDirectories(path))
                {
                    searchForIt(subdir);
                }

            }
            if (Directory.Exists(targPath))
            {
                searchForIt(targPath);
            }
        }
    }
}
