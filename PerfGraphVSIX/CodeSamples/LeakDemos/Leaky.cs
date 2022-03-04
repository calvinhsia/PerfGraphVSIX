//Include: ..\Util\LeakBaseClass.cs
//Desc: This will demonstrate leak detection
//Desc: It has a big class that consumes lots of memory

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
using System.Collections.Concurrent;
using System.Collections.Immutable;
//Ref64: %VSRoot%\Common7\IDE\PrivateAssemblies\System.Collections.Immutable.dll
//Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll

namespace MyCodeToExecute
{

    public class MyClass : LeakBaseClass
    {
        class BigStuffWithLongNameSoICanSeeItBetter
        {
            ConcurrentBag<string> bag = new ConcurrentBag<string>();
            ConcurrentDictionary<string, string> dict = new ConcurrentDictionary<string, string>();
            ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
            ConcurrentStack<string> stack = new ConcurrentStack<string>();
            ImmutableDictionary<string, string> immdict = ImmutableDictionary<string,string>.Empty;
            ImmutableArray<string> immarray = ImmutableArray<string>.Empty;
            ImmutableHashSet<string> immhash = ImmutableHashSet<string>.Empty;
            ImmutableList<string> immlist= ImmutableList<string>.Empty;
            public BigStuffWithLongNameSoICanSeeItBetter()
            {
                for (int i = 0; i < 100; i++)
                {
                    bag.Add($"Bag {i }");
                    dict[$"dictkey{i}"] = $"dictval{i}";
                    queue.Enqueue($"q{i}");
                    stack.Push($"stack{i}");
                    immdict = immdict.Add($"dictkey{i}", $"dictval{i}");
                    immarray = immarray.Add($"arr{i}");
                    immhash = immhash.Add($"hash{i}");
                    immlist = immlist.Add($"list{i}");

                }
            }
            byte[] arr = new byte[1024 * 1024];
        }
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 19, Sensitivity: 2.5, delayBetweenIterationsMsec: 800);
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
            await Task.Delay(TimeSpan.FromSeconds(4));
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
        }
        public override async Task DoCleanupAsync()
        {
        }
    }
}
