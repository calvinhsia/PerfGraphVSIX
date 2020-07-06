//Desc: Run code in 64 bit assembly
//Desc: Creates a 64 bit assembly that calls the code in a 32 bit asm and runs it as 64 bit
//Desc: Example: some code doesn't work in 32 bit assemblies (like taking a dump of a 64 bit process from 32 bit)
//Desc: The code allocates as much memory as it can before throwing an OutOfMemoryException and shows the result.
//Desc: on my 16G SurfaceBook with 25G Paging file, it allocates 2G in the 32 bit devenv process, and 35G in the 64 bit process. 
//Include: ..\Util\LeakBaseClass.cs
//Include: ..\Util\CreateAsm.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Text;
using Microsoft.Performance.ResponseTime;

namespace MyCodeToExecute
{
    /// <summary>
    ///  see https://github.com/calvinhsia/CreateDump
    /// </summary>
    public class MyClass : MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.DoInitializeAsync();
        }

        public bool UseOutputPane { get; set; } = false;
        public bool ShowAllProperties { get; set; } = false;
        public string EventFilter { get; set; }

        MyClass(object[] args) : base(args)
        {
        }
        public async Task DoInitializeAsync()
        {
            await Task.Yield();
            try
            {
                await TaskScheduler.Default;
                var outputLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyTestAsm.log");
                File.Delete(outputLogFile);
                //// we can call the method normally, from the current 32 bit devenv process and see the result
                MyClassThatRunsIn32and64bit.MyMainMethod(outputLogFile, "Executing normally", 32, true);
                var result = File.ReadAllText(outputLogFile);
                _logger.LogMessage(result);
                File.Delete(outputLogFile);

                // or we can call it via reflection (from the current 32 bit devenv process):
                //*
                var meth = typeof(MyClassThatRunsIn32and64bit)
                    .GetMethod(nameof(MyClassThatRunsIn32and64bit.MyMainMethod), BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                meth.Invoke(null, new object[] { // null for static method
                    outputLogFile, "Executing from 32 bit via reflection", 32, true });
                result = File.ReadAllText(outputLogFile);
                _logger.LogMessage(result);
                File.Delete(outputLogFile);

                // Or we can generate a 64 bit exe and run it
                var vsRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var addDir = Path.Combine(vsRoot, "PublicAssemblies") + ";" + Path.Combine(vsRoot, "PrivateAssemblies");

                // now we create an assembly, load it in a 64 bit process which will invoke the same method using reflection
                var asm64BitFile = Path.ChangeExtension(Path.GetTempFileName(), ".exe");

                var type = new AssemblyCreator().CreateAssembly
                    (
                        asm64BitFile,
                        PortableExecutableKinds.PE32Plus,
                        ImageFileMachine.AMD64,
                        AdditionalAssemblyPaths: addDir, // Microsoft.VisualStudio.Shell.Interop
                        logOutput: false // for diagnostics
                    );
                var args = $@"""{Assembly.GetExecutingAssembly().Location
                    }"" {nameof(MyClassThatRunsIn32and64bit)} {
                        nameof(MyClassThatRunsIn32and64bit.MyMainMethod)} ""{outputLogFile}"" ""Executing from 64 bit Asm"" ""64"" true";
                var p64 = Process.Start(
                    asm64BitFile,
                    args);
                p64.WaitForExit(30 * 1000);
                File.Delete(asm64BitFile);
                result = File.ReadAllText(outputLogFile);
                _logger.LogMessage(result);
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex}");
            }
        }
    }
    /// <summary>
    /// This class contains code that can be called from a 32 bit (e.g. current process) or 64 bit processs
    /// </summary>
    internal class MyClassThatRunsIn32and64bit
    {
        // arg1 is a file to write our results, arg2 and arg3 show we can pass simple types. e.g. Pass the name of a named pipe.
        internal static async Task MyMainMethod(string outLogFile, string desc, int intarg, bool boolarg)
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine($"\r\n  {desc} Executing {nameof(MyClassThatRunsIn32and64bit)}.{nameof(MyMainMethod)} Pid={Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().MainModule.FileName}");
                sb.AppendLine($"  IntPtr.Size = {IntPtr.Size} Intarg={intarg} BoolArg={boolarg}");
                if (IntPtr.Size == 8)
                {
                    sb.AppendLine("  We're in 64 bit land!!!");
                }
                else
                {
                    sb.AppendLine("  nothing exciting: 32 bit land");
                }
                long bytesAlloc = 0;
                var lst = new List<byte[]>();
                try
                {
                    var sizeToAlloc = 1024 * 1024;
                    while (true)
                    {
                        var x = new byte[sizeToAlloc];
                        lst.Add(x);
                        bytesAlloc += sizeToAlloc;
                    }
                }
                catch (OutOfMemoryException ex)
                {
                    lst = null;
                    sb.AppendLine($" {ex.Message} after allocating {bytesAlloc / 1024.0 / 1024 / 1024:n2} gigs");
                }
                //                sb.AppendLine($"Allocated {numAllocated} Gigs");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"in {nameof(MyMainMethod)} IntPtr.Size = {IntPtr.Size} Exception {ex}");
                if (IntPtr.Size == 8)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)); // delay so can observe mem use by other tools (taskman, procexp, etc)
                }
            }
            File.AppendAllText(outLogFile, sb.ToString());
        }
    }
}
