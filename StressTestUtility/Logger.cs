using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class Logger : ILogger
    {
        public List<string> _lstLoggedStrings = new List<string>();
        private readonly TestContextWrapper testContext;
        private string logFilePath;

        /// <summary>
        /// Pass in a TestContext. Or null
        /// </summary>
        /// <param name="testContext"></param>
        public Logger(TestContextWrapper testContext)
        {
            this.testContext = testContext;
        }

        public void LogMessage(string str, params object[] args)
        {
            try
            {
                var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
                str = string.Format(dt + str, args);
                var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

                testContext?.WriteLine(msgstr);
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
            catch (Exception)
            {
            }

        }
    }
}
