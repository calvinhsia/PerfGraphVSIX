using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Tests
{
    public class BaseTestClass : ILogger
    {
        public TestContext TestContext { get; set; }

        public List<string> _lstLoggedStrings;

        [TestInitialize]
        public void TestInitialize()
        {
            _lstLoggedStrings = new List<string>();
            LogMessage($"Starting test {TestContext.TestName}");

        }
        public void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
            str = string.Format(dt + str, args);
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

            this.TestContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);
        }
    }
}