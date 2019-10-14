using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System.Linq;
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
            return ""did main "" + y.ToString() +"" ""+ args[1];
        }
    }
}
";
            var res = new CodeExecutor(this).CompileAndExecute(strCodeToExecute);
            Assert.AreEqual("did main 100 p1", res);
        }



        [TestMethod]
        public void TestCompilePerfGraphCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//Ref: PerfGraphVSIX
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
        CancellationTokenSource _cts;
        ILogger logger;
        public MyClass()
        {
            _tcs = new TaskCompletionSource<int>();
            _cts = new CancellationTokenSource();
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

            return ""did main "" + y.ToString() +"" ""+ args[1] + "" ""+ str;
        }
        public static string DoMain(object[] args)
        {
            var oMyClass = new MyClass();
            return oMyClass.DoIt(args);
        }
    }
}
";
            var res = new CodeExecutor(this).CompileAndExecute(strCodeToExecute);
            LogMessage(res);
            Assert.AreEqual("did main 100 p1 did delay", res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in doit")).FirstOrDefault());

            //            Assert.Fail(res);
        }

        [TestMethod]
        public void TestCompileVSCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//Ref: PerfGraphVSIX
//Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
////Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.9.0.dll
//Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
////Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.12.0.dll

//Ref: ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll""
//Ref: ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll""
//Ref: ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll""


//Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref:""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies\envdte.dll""


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;

//using Microsoft.VisualStudio.Threading;

namespace MyCustomCode
{
    public class MyClass
    {
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationTokenSource _cts;
        ILogger logger;
        public EnvDTE.DTE g_dte;
        public MyClass()
        {
            _tcs = new TaskCompletionSource<int>();
            _cts = new CancellationTokenSource();
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

            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
            var str = DoWaitAsync().GetAwaiter().GetResult();
            var sln = @""C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln"";

            if (g_dte != null)
            {
                logger.LogMessage(""Opening solution "" + sln);
                g_dte.Solution.Open(sln);
            }


            return ""did main "";
        }

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            _tcs.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
            _tcs.TrySetResult(0);
        }

        public static string DoMain(object[] args)
        {
            var oMyClass = new MyClass();
            return oMyClass.DoIt(args);
        }
    }
}
";
            var res = new CodeExecutor(this).CompileAndExecute(strCodeToExecute);
            LogMessage(res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in doit")).FirstOrDefault());

            //            Assert.Fail(res);
        }


    }
}
