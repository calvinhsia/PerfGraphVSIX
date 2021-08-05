using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class Utility
    {
        /// <summary>
        /// Execute the given process
        /// </summary>
        /// <param name="exeName">Process to be executed</param>
        /// <param name="arguments">Process arguments</param>
        /// <param name="sb">stringbuild for result</param>
        /// <returns></returns>
        public static Process CreateProcess(string exeName, string arguments, StringBuilder sb)
        {
            ProcessStartInfo info = new ProcessStartInfo(exeName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = new Process();
            process.OutputDataReceived += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    sb.AppendLine(string.Format("{0}", e.Data));
                }
            };
            process.ErrorDataReceived += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    sb.AppendLine(string.Format("{0}", e.Data));
                }
            };
            info.CreateNoWindow = true;
            process.StartInfo = info;
            return process;
        }
        public static void SetEnvironmentForMemSpect(IDictionary<string, string> environment, MemSpectModeFlags memSpectModeFlags, string MemSpectDllPath)
        {
            /*
for all tests: 
    try to use fewer iterations, e.g. 7
    try to use smaller data to repro, e.g. a smaller solution
    make sure to set DelayMultiplier to e.g. 10
    make sure it doesn't close VS in the Cleanup... closing VS also closes the attached MemSpect, so just let VS be idle.
    you can configure what MemSpect tracks via the MemSpect.ini file in the same folder. (e.g. native heap, managed heap, etc.)

for apex tests, 
    set env before start process: 
            visualStudio = Operations.CreateHost<VisualStudioHost>();
            StressUtil.SetEnvironmentForMemSpect(visualStudio.Configuration.Environment, MemSpectModeFlags.MemSpectModeFull, @"C:\MemSpect\MemSpectDll.dll");
            visualStudio.Start();

Set COR_ENABLE_PROFILING=1
Set COR_PROFILER={01673DDC-46F5-454F-84BC-F2F34564C2AD}
Set COR_PROFILER_PATH=c:\MemSpect\MemSpectDll.dll
*/
            if (string.IsNullOrEmpty(MemSpectDllPath))
            {
                MemSpectDllPath = @"c:\MemSpect\MemSpectDll.dll"; // @"C:\VS\src\ExternalAPIs\MemSpect\MemSpectDll.dll"
            }
            if (!File.Exists(MemSpectDllPath))
            {
                throw new FileNotFoundException($@"Couldn't find MemSpectDll.Dll at {MemSpectDllPath}. See http://Toolbox/MemSpect and/or VS\src\ExternalAPIs\MemSpect\MemSpectDll.dll");
            }
            environment["COR_ENABLE_PROFILING"] = "1";
            environment["COR_PROFILER"] = "{01673DDC-46F5-454F-84BC-F2F34564C2AD}";
            environment["COR_PROFILER_PATH"] = MemSpectDllPath;
            if (memSpectModeFlags != MemSpectModeFlags.MemSpectModeFull)
            { //todo
                //var MemSpectInitFile = Path.Combine(Path.GetDirectoryName(pathMemSpectDll), "MemSpect.ini");
                // need to WritePrivateProfileString  "TrackClrObjects"  "fTrackHeap" "EnableAsserts"
            }
        }
    }
}
