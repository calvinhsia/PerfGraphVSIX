using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{

    [TestClass]
    public class TestCompile : BaseTestClass
    {
        [TestMethod]
        public void TestCompileCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
////Ref: c:\progam files \...myAsm.dll
////Ref: System.dll
////Ref: System.linq.dll
////Ref: System.core.dll
////Ref: <%= asmMemSpectBase.Location %>
using System;

namespace DoesntMatter
{
public class foo {}
    public class SomeClass
    {
        public static string DoMain(object [] args)
        {
            var x = 1;
            var y = 100 / x;
            return ""did main "" + y.ToString() +"" "";
        }
    }
}
";
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, strCodeToExecute);
            var res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            Assert.AreEqual("did main 100 ", res);
        }

        [TestMethod]
        public void TestCompileIncludeCodeFile()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
////Ref: c:\progam files \...myAsm.dll
////Ref: System.dll
////Ref: System.linq.dll
////Ref: System.core.dll
////Ref: <%= asmMemSpectBase.Location %>
//Include: TBase.cs
using System;

namespace DoesntMatter
{
public class foo {}
    public class SomeClass:BaseClass
    {
        public int NumberOfIterations = 97;

        public static string DoMain(object [] args)
        {
            var x = 1;
            var y = new SomeClass();
            return ""did main "" + y.BaseMethod() +"" NumIter= "" + y.NumberOfIterations.ToString();
        }
    }
}
";
            var strCodeToExecuteBaseClass = @"
using System;

namespace DoesntMatter
{
    public class BaseClass
    {
        public int NumberOfIterations = 98;
        public string BaseMethod()
        {
            return ""In Base Method"";
        }
    }
}
";

            var codeExecutor = new CodeExecutor(this);
            var tempFile1 = Path.Combine(Environment.CurrentDirectory, //C:\Users\calvinh\Source\repos\PerfGraphVSIX\Tests\bin\Debug
                "T1.cs");

            File.WriteAllText(tempFile1, strCodeToExecute);
            var tempFile2 = Path.Combine(Environment.CurrentDirectory, //C:\Users\calvinh\Source\repos\PerfGraphVSIX\Tests\bin\Debug
                "TBase.cs");
            File.WriteAllText(tempFile2, strCodeToExecuteBaseClass);

            var res = codeExecutor.CompileAndExecute(null, tempFile1, CancellationToken.None);
            LogMessage($"Got output {res}");
            Assert.AreEqual("did main In Base Method NumIter= 97", res);
        }



        [TestMethod]
        public void TestCompileCodeReturnTask()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
////Ref: c:\progam files \...myAsm.dll
////Ref: System.dll
////Ref: System.linq.dll
////Ref: System.core.dll
////Ref: <%= asmMemSpectBase.Location %>
using System;
using System.Threading.Tasks;

namespace DoesntMatter
{
public class foo {}
    public class SomeClass
    {
        async Task<string> DoWaitAsync()
        {
            await Task.Delay(100);
            return ""did delay"";
        }

        public static async Task<string> DoMain(object [] args)
        {
            var x = 1;
            var y = 100 / x;
            
            return ""did main "" + y.ToString() +"" "";
        }
    }
}
";
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, strCodeToExecute);
            var res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            if (res is Task<string> task)
            {
                task.Wait();
                Assert.AreEqual("did main 100 ", task.Result);
            }
            else
            {
                Assert.Fail();
            }
        }



        [TestMethod]
        public void TestCompilePerfGraphCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//Ref: %PerfGraphVSIX%
using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;

//using Microsoft.VisualStudio.Threading;

namespace DoesntMatter
{
    public class MyClass
    {
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        ILogger logger;
        public MyClass()
        {
            _tcs = new TaskCompletionSource<int>();
        }

        async Task<string> DoWaitAsync()
        {
//            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await Task.Delay(100);
            return ""did delay"";
        }

        public string DoIt(object[] args)
        {
            logger = args[1] as ILogger;
            logger.LogMessage(""in doit"");
            logger.LogMessage(""Logger Asm =  "" + logger.GetType().Assembly.Location);
            logger.LogMessage(""This   Asm =  "" + this.GetType().Assembly.Location); // null for in memory
            var x = 1;
            var y = 100 / x;
            var str = DoWaitAsync().GetAwaiter().GetResult();

            return ""did main "" + y.ToString() +"" ""+ str;
        }
        public static string DoMain(object[] args)
        {
            var oMyClass = new MyClass();
            return oMyClass.DoIt(args);
        }
    }
}
";
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, strCodeToExecute);
            var res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            LogMessage(res as string);
            Assert.AreEqual("did main 100 did delay", res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in doit")).FirstOrDefault());

            //            Assert.Fail(res);
        }

        [TestMethod]
        public void TestCompileIterationCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//  %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//  %VSRoot% will be changed to the fullpath to VS: e.g. ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview""

////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
////Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll""
////Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll""
////Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll""
////Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll""
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll

////Ref:""%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll""

//Ref: %PerfGraphVSIX%


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Task = System.Threading.Tasks.Task;

namespace MyCustomCode
{
    public class MyClass
    {
        int NumberOfIterations = 7;
        int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationToken _CancellationToken;
        ILogger logger;
        public MyClass(object[] args)
        {
            logger = args[1] as ILogger;
            _CancellationToken = (CancellationToken)args[2]; // value type
        }

        void foo()
        {
            var odumper = new DumperViewerMain(null)
                {
                    _logger = logger
                };
        }

        private void DoSomeWork()
        {
            logger.LogMessage(""in DoSomeWorkAsync"");
//            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); // in tests, this won't work
            logger.LogMessage(""Logger Asm =  "" + logger.GetType().Assembly.Location);
            logger.LogMessage(""This   Asm =  "" + this.GetType().Assembly.Location); // null for in memory
            logger.LogMessage(""Starting iterations "" + NumberOfIterations.ToString());
            var measurementHolder = new MeasurementHolder(
                ""testTODOTODO"",
                PerfCounterData.GetPerfCountersForStress(),
                SampleType.SampleTypeIteration,
                NumTotalIterations: NumberOfIterations,
                logger: logger);


            for (int i = 0; i < NumberOfIterations && !_CancellationToken.IsCancellationRequested; i++)
            {
                logger.LogMessage(""Iter {0}   Start {1} left to do"", i, NumberOfIterations - i);
                if (_CancellationToken.IsCancellationRequested)
                {
                    break;
                }
                logger.LogMessage(""Iter {0} end"", i);
            }
            if (_CancellationToken.IsCancellationRequested)
            {
                logger.LogMessage(""Cancelled"");
            }
            else
            {
                logger.LogMessage(""Done all {0} iterations"", NumberOfIterations);
            }
        }


        public static string DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);

            oMyClass.DoSomeWork();
            return ""did main"";
        }
    }
}
";
            var codeExecutor = new CodeExecutor(this);

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, strCodeToExecute);
            var res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            LogMessage(res as string);
            Assert.AreEqual("did main", res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Iter 6   Start 1 left to do")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Done all 7 iterations")).FirstOrDefault());
        }

        public const string sampleVSCodeToExecute = @"
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview""

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll""
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll""
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll""
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll""
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll

//Ref:""%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll""

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
                    PerfCounterData.GetPerfCountersForStress(),
                    SampleType.SampleTypeIteration,
                    NumTotalIterations: 4,
                    logger: logger);


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
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, sampleVSCodeToExecute);
            var res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            if (res is string resString)
            {
                Assert.Fail(resString);
            }
            var task = res as Task;
            if (task.IsFaulted)
            {
                LogMessage($"Faulted task {task.Exception.ToString()}");
                Assert.IsTrue(task.Exception.ToString().Contains(" The SVsSolution service is unavailable."));
            }
            //            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in DoSomeWorkAsync")).FirstOrDefault());


            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Registering solution events")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("UnRegistering solution events")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("The SVsSolution service is unavailable")).FirstOrDefault());
        }


        [TestMethod]
        public void TestCompileVSCodeRunMulti()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
////Ref: c:\progam files \...myAsm.dll
////Ref: System.dll
////Ref: System.linq.dll
////Ref: System.core.dll
////Ref: <%= asmMemSpectBase.Location %>
using System;

namespace DoesntMatter
{
public class foo {}
    public class SomeClass
    {
        public static string DoMain(object [] args)
        {
            var x = 1;
            var y = 100 / x;
            return ""did main "" + y.ToString() +"" "";
        }
    }
}
";
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, strCodeToExecute);
            var res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            LogMessage(res.ToString());

            res = codeExecutor.CompileAndExecute(null, tempFile, CancellationToken.None);
            LogMessage(res.ToString());

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Using prior compiled assembly")).FirstOrDefault());
        }



    }
}
