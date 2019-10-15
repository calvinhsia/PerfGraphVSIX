using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
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
            var res = codeExecutor.CompileAndExecute(strCodeToExecute, CancellationToken.None);
            Assert.AreEqual("did main 100 ", res);
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
            logger = args[0] as ILogger;
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
            var res = codeExecutor.CompileAndExecute(strCodeToExecute, CancellationToken.None);
            LogMessage(res);
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
        Action<string> actTakeSample;
        public MyClass(object[] args)
        {
            logger = args[0] as ILogger;
            _CancellationToken = (CancellationToken)args[1]; // value type
            actTakeSample = args[3] as Action<string>;
        }

        private void DoSomeWork()
        {
            logger.LogMessage(""in DoSomeWorkAsync"");
//            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); // in tests, this won't work
            logger.LogMessage(""Logger Asm =  "" + logger.GetType().Assembly.Location);
            logger.LogMessage(""This   Asm =  "" + this.GetType().Assembly.Location); // null for in memory
            logger.LogMessage(""Starting iterations "" + NumberOfIterations.ToString());
            for (int i = 0; i < NumberOfIterations && !_CancellationToken.IsCancellationRequested; i++)
            {
                DoSample();
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
            DoSample();
        }

        void DoSample()
        {
            if (actTakeSample != null)
            {
                actTakeSample(string.Empty);
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
            var res = codeExecutor.CompileAndExecute(strCodeToExecute, CancellationToken.None, (s) =>
            {
                LogMessage($"In callback {s}");
            });
            LogMessage(res);
            Assert.AreEqual("did main", res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Iter 6   Start 1 left to do")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Done all 7 iterations")).FirstOrDefault());
        }


        [TestMethod]
        public void TestCompileVSCode()
        {

            var codeExecutor = new CodeExecutor(this);
            var res = codeExecutor.CompileAndExecute(CodeExecutor.sampleVSCodeToExecute, CancellationToken.None, (s)=>
            {
                LogMessage("In callback {s}");
            });
            LogMessage(res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in DoSomeWorkAsync")).FirstOrDefault());


            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Registering for solution events")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("UnRegistering for solution events")).FirstOrDefault());
            //            Assert.Fail(res);The SVsSolution service is unavailable.
        }


        [TestMethod]
        public void TestCompileVSCodeRunMulti()
        {
            var codeExecutor = new CodeExecutor(this);
            var res = codeExecutor.CompileAndExecute(CodeExecutor.sampleVSCodeToExecute, CancellationToken.None, (s) =>
            {
                LogMessage("In callback {s}");
            });
            LogMessage(res);

            res = codeExecutor.CompileAndExecute(CodeExecutor.sampleVSCodeToExecute, CancellationToken.None, (s) =>
            {
                LogMessage("In callback {s}");
            });
            LogMessage(res);

        }


    }
}
