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
        string somestring = "somestring1";
        string somestring2 = "somestring2";
        string somestring3 = "somestring3";
        public event EventHandler Myevent;
        class BigStuffWithLongNameSoICanSeeItBetter
        {
            byte[] arr = new byte[1024 * 1024];
            public BigStuffWithLongNameSoICanSeeItBetter(MyClass obj)
            {
                //obj.Myevent += Obj_Myevent; // this form always leaks, even if Obj_Myevent is empty
                var x = 2;
                obj.Myevent += (o, e) =>
                  {
                     var y = arr; // this line causes leak because ref to local member lifted in closure
                  };
            }

            private void Obj_Myevent(object sender, EventArgs e)
            {
                throw new NotImplementedException();
            }
        }
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 19, Sensitivity: .4, delayBetweenIterationsMsec: 800);
            }
        }
        public MyClass(object[] args) : base(args)
        {
        }

        public override async Task DoInitializeAsync()
        {
        }

        public override async Task DoIterationBodyAsync()
        {
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                var x = new BigStuffWithLongNameSoICanSeeItBetter(this);
            }
        }
        public override async Task DoCleanupAsync()
        {
        }
    }
}
