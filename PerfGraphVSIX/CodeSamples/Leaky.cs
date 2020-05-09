//Include: ExecCodeBase.cs
// this will demonstate leak detection

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

namespace MyCodeToExecute
{

    public class MyClass : BaseExecCodeClass
    {
        class BigStuffWithLongNameSoICanSeeItBetter
        {
            byte[] arr = new byte[1024*1024];
        }
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 9, Sensitivity: 2.5, delayBetweenIterationsMsec: 800);
            }
        }
        string somestring = "somestring1";
        string somestring2 = "somestring2";
        string somestring3 = "somestring3";
        List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();
        public MyClass(object[] args) : base(args)
        {
        }

        public override async Task DoInitializeAsync()
        {
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 10; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
        }
        public override async Task DoCleanupAsync()
        {
        }
    }
}
