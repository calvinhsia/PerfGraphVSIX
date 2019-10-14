using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static string DoMain(string[] args)
        {
            var x = 1;
            var y = 100 / x;
            return ""did main "" + y.ToString() +"" ""+ args[0];
        }
    }
}
";
            var res = CodeExecutor.CompileAndExecute(strCodeToExecute);
            Assert.AreEqual("did main 100 p1", res);
        }
        async Task<string> DoWaitAsync()
        {
            //            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await Task.Delay(100);
            return "did delay ";
        }
        string CallDoWai()
        {
            return DoWaitAsync().GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestCompileVSCode()
        {
            var x = CallDoWai();
            LogTestMessage($"got x {x}");
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
////Ref: System.dll
////Ref: System.linq.dll
////Ref: System.core.dll
////Ref: <%= asmMemSpectBase.Location %>
using System;
using System.Threading;
using System.Threading.Tasks;
//using Microsoft.VisualStudio.Threading;

namespace DoesntMatter
{
    public class MyClass
    {
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationTokenSource _cts;
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

        public string DoIt(string[] args)
        {
            var x = 1;
            var y = 100 / x;
            var str = DoWaitAsync().GetAwaiter().GetResult();
            return ""did main "" + y.ToString() +"" ""+ args[0] + "" ""+ str;
        }
        public static string DoMain(string[] args)
        {
            var oMyClass = new MyClass();
            return oMyClass.DoIt(args);
        }
    }
}
";
            var res = CodeExecutor.CompileAndExecute(strCodeToExecute);
            LogTestMessage(res);
            Assert.AreEqual("did main 100 p1 did delay", res);
            //            Assert.Fail(res);
        }
    }
}
