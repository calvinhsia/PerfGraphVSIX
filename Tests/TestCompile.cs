using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
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
        public void TestCompileVSCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//Ref: PerfGraphVSIX
////Ref: System.dll
////Ref: System.linq.dll
////Ref: System.core.dll
////Ref: <%= asmMemSpectBase.Location %>
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
            //            Assert.Fail(res);
        }
    }
}
