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
            var res = new CodeExecutor(this).CompileAndExecute(strCodeToExecute, CancellationToken.None);
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
            var res = new CodeExecutor(this).CompileAndExecute(strCodeToExecute, CancellationToken.None);
            LogMessage(res);
            Assert.AreEqual("did main 100 did delay", res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in doit")).FirstOrDefault());

            //            Assert.Fail(res);
        }

        [TestMethod]
        public void TestCompileVSCode()
        {
           
            var res = new CodeExecutor(this).CompileAndExecute(CodeExecutor.sampleVSCodeToExecute, CancellationToken.None);
            LogMessage(res);
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in doit")).FirstOrDefault());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("The SVsSolution service is unavailable")).FirstOrDefault());
            //            Assert.Fail(res);The SVsSolution service is unavailable.
        }


    }
}
