using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            using (var compileHelper = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None))
            {
                var res = compileHelper.ExecuteTheCode();
                Assert.AreEqual("did main 100 ", res.ToString());
            }
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

            using (var compileHelper = codeExecutor.CompileTheCode(null, tempFile1, CancellationToken.None))
            {
                var res = compileHelper.ExecuteTheCode();
                LogMessage($"Got output {res}");
                Assert.AreEqual("did main In Base Method NumIter= 97", res.ToString());
            }
        }

        [TestMethod]
        public async Task TestCompileCodeReturnTask()
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
            using (var compileHelper = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None))
            {
                var res = compileHelper.ExecuteTheCode();
                if (res is Task<string> task)
                {
                    var result = await task;
                    Assert.AreEqual("did main 100 ", result);
                }
                else
                {
                    Assert.Fail();
                }
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
            using (var compileHelper = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None))
            {
                var res = compileHelper.ExecuteTheCode();
                LogMessage(res as string);
                Assert.AreEqual("did main 100 did delay", res);
                Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in doit")).FirstOrDefault());
            }
            //            Assert.Fail(res);
        }

        [TestMethod]
        public void TestCompileIterationCode()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//  %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//  %VSRoot% will be changed to the fullpath to VS: e.g. ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview""
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll

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
                new StressUtilOptions() { 
                    NumIterations = -1,
                    lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(System.Diagnostics.Process.GetCurrentProcess(), IsForStress: false)
                },
                SampleType.SampleTypeIteration);


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
            using (var compileHelper = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None))
            {
                var res = compileHelper.ExecuteTheCode();
                LogMessage(res as string);
                Assert.AreEqual("did main", res);
                Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Iter 6   Start 1 left to do")).FirstOrDefault());
                Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Done all 7 iterations")).FirstOrDefault());
            }
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
            var res = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None);
            LogMessage(res.ToString());

            // compile again and should get msg "using prior"
            res = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None);
            LogMessage(res.ToString());
            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Using prior compiled assembly")).FirstOrDefault());
        }

        [TestMethod]
        public void TestCompilePragma()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//Pragma: verbose=true

//Pragma: GenerateInMemory=false

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
            var res = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None);
            LogMessage(res.ToString());

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Pragma GenerateInMemory  = False")).FirstOrDefault());
        }
        [TestMethod]
        public void TestCompileCSC()
        {
            var strCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//Ref: %PerfGraphVSIX%
//Pragma: useCSC=true

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;


namespace DoesntMatter
{
public class foo {}
    public class SomeClass
    {
        public static string DoMain(object [] args)
        {
            ILogger logger;
            var x = 1;
            var y = 100 / x;
            var zz = $""{x}  {y}"";
            return ""did main "" + y.ToString() +"" "";
        }
    }
}
";
            var codeExecutor = new CodeExecutor(this);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, strCodeToExecute);
            var res = codeExecutor.CompileTheCode(null, tempFile, CancellationToken.None);
            LogMessage(res.ToString());

        }

        [TestMethod]
        public async Task TestCompileVB()
        {
            await Task.Yield();
            var vbfile = @"C:\Users\calvinh\source\repos\PerfGraphVSIX\PerfGraphVSIX\CodeSamples\Cartoon.vb";

            var codeExecutor = new CodeExecutor(this);
            var res = codeExecutor.CompileTheCode(null, vbfile, CancellationToken.None);
            LogMessage("{0}", res.ToString());



        }
    }
}
