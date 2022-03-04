using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Test.Stress;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text;

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
                ) + $"{Thread.CurrentThread.ManagedThreadId,2} ";
            str = string.Format(dt + str, args);
            var msgstr = $" {str}";

            this.TestContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);
        }
        void AddLstLoggedStringToDesktopLog(List<string> lstLoggedStrings)
        {
            var sb = new StringBuilder();
            foreach (var logline in lstLoggedStrings)
            {
                sb.AppendLine(logline);
            }
            var desktoplogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TestStressDataCollector.log"); //can't use the Test deployment folder because it gets cleaned up

            File.WriteAllText(desktoplogFilePath, sb.ToString());

        }

        [TestCleanup]
        public void Cleanup()
        {
            var logger = TestContext.Properties[StressUtil.PropNameLogger] as Logger;
            if (logger != null)
            {
                AddLstLoggedStringToDesktopLog(logger._lstLoggedStrings);
            }
            else
            {
                AddLstLoggedStringToDesktopLog(_lstLoggedStrings);
            }
        }


    }
}