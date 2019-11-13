﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LeakTestDatacollector
{
    public class Logger : ILogger
    {
        public static List<string> _lstLoggedStrings = new List<string>();
        private readonly TestContext testContext;
        private string logFilePath;

        public Logger(TestContext testContext)
        {
            this.testContext = testContext;
        }

        public void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
            str = string.Format(dt + str, args);
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

            testContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);

            if (string.IsNullOrEmpty(logFilePath))
            {
                //   logFilePath = @"c:\Test\StressDataCollector.log";
                logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TestStressDataCollector.log"); //can't use the Test deployment folder because it gets cleaned up
            }
            File.AppendAllText(logFilePath, msgstr + Environment.NewLine);

        }
    }
}