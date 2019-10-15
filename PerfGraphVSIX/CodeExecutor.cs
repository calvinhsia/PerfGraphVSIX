using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    internal class CodeExecutor
    {
        public const string sampleVSCodeToExecute = @"
// can add the fullpath to an assembly for reference like so:
//  %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//  %VSRoot% will be changed to the fullpath to VS: e.g. ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview""

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll""
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll""
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll""
//Ref: ""%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll""
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll

//Ref:""%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll""

//Ref: %PerfGraphVSIX%


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.VisualStudio.Shell;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCustomCode
{
    public class MyClass
    {
        string SolutionToLoad = @""C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln"";
        int NumberOfIterations = 7;
        int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationToken _CancellationToken;
        JoinableTask _tskDoPerfMonitoring;
        ILogger logger;
        Action<string> actTakeSample;
        public EnvDTE.DTE g_dte;
        public MyClass(object[] args)
        {
            _tcs = new TaskCompletionSource<int>();
            logger = args[0] as ILogger;
            _CancellationToken = (CancellationToken)args[1]; // value type
            g_dte= args[2] as EnvDTE.DTE;
            actTakeSample = args[3] as Action<string>;
        }

        async Task<string> DoWaitAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await Task.Delay(100);
            return ""did delay"";
        }

        public string DoIt()
        {
            logger.LogMessage(""in doit"");
            logger.LogMessage(""Logger Asm =  "" + logger.GetType().Assembly.Location);
            logger.LogMessage(""This   Asm =  "" + this.GetType().Assembly.Location); // null for in memory

            var t = DoSomeWorkAsync();
            return ""did main "";
        }

        private async Task DoSomeWorkAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (nTimes++ == 0)
            {
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
            }
            for (int i = 0; i < NumberOfIterations && !_CancellationToken.IsCancellationRequested; i++)
            {
                DoSample();
                logger.LogMessage(""Iter {0}   Start {1} left to do"", i, NumberOfIterations - i);
                await OpenASolutionAsync();
                if (_CancellationToken.IsCancellationRequested)
                {
                    break;
                }
                await CloseTheSolutionAsync();
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
            DoSample();
        }

        void DoSample()
        {
            if (actTakeSample != null)
            {
                actTakeSample(string.Empty);
            }
        }

        async Task OpenASolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Open(SolutionToLoad);
            await _tcs.Task;
            if (!_CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        async Task CloseTheSolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();
            if (!_CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }


        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            logger.LogMessage(""SolutionEvents_OnAfterCloseSolution"");
            _tcs.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
            logger.LogMessage(""SolutionEvents_OnAfterBackgroundSolutionLoadComplete"");
            _tcs.TrySetResult(0);
        }

        public static string DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            return oMyClass.DoIt();
        }
    }
}
";

        public const string DoMain = "DoMain"; // not domain
        public const string VSRootSubstitution = "%VSRoot%";
        public const string refPathPrefix = "//Ref:";
        bool fDidAddAssemblyResolver;
        readonly ILogger logger;
        public CodeExecutor(ILogger logger)
        {
            this.logger = logger;
        }
        public string CompileAndExecute(string strCodeToExecute, CancellationToken token, Action<string> actTakeSample = null)
        {
            var result = string.Empty;
            logger.LogMessage($"Compiling code");
            try
            {
                //logger.LogMessage($"Main file= { Process.GetCurrentProcess().MainModule.FileName}"); //  C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\Extensions\TestPlatform\testhost.x86.exe
                var curProcMainModule = Process.GetCurrentProcess().MainModule.FileName;
                var ndxCommon7 = curProcMainModule.IndexOf("common7", StringComparison.OrdinalIgnoreCase);
                if (ndxCommon7 <= 0)
                {
                    throw new InvalidOperationException("Can't find VSRoot");
                }
                var vsRoot = curProcMainModule.Substring(0, ndxCommon7 - 1); //"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"
                // this is old compiler. For new stuff: https://stackoverflow.com/questions/31639602/using-c-sharp-6-features-with-codedomprovider-roslyn
                using (var cdProvider = CodeDomProvider.CreateProvider("C#"))
                {
                    var compParams = new CompilerParameters();
                    var lstRefDirs = new HashSet<string>();
                    var srcLines = strCodeToExecute.Split("\r\n".ToArray());
                    foreach (var refline in srcLines.Where(s => s.StartsWith(refPathPrefix)))
                    {
                        var refAsm = refline.Replace(refPathPrefix, string.Empty).Trim();
                        if (refAsm.StartsWith("\"") && refAsm.EndsWith("\""))
                        {
                            refAsm = refAsm.Replace("\"", string.Empty);
                        }
                        if (refAsm == $"%{nameof(PerfGraphVSIX)}%")
                        {
                            refAsm = typeof(PerfGraphToolWindowControl).Assembly.Location;
                            var dir = System.IO.Path.GetDirectoryName(refAsm);
                            if (!lstRefDirs.Contains(dir))
                            {
                                lstRefDirs.Add(dir);
                            }
                        }
                        else
                        {
                            if (refAsm.Contains(VSRootSubstitution))
                            {
                                refAsm = refAsm.Replace(VSRootSubstitution, vsRoot);
                            }
                            var dir = System.IO.Path.GetDirectoryName(refAsm);
                            logger.LogMessage($"AddRef {refAsm}");
                            if (!string.IsNullOrEmpty(refAsm))
                            {
                                if (!System.IO.File.Exists(refAsm))
                                {
                                    throw new System.IO.FileNotFoundException($"Couldn't find {refAsm}");
                                }
                                else
                                {
                                    if (!lstRefDirs.Contains(dir))
                                    {
                                        lstRefDirs.Add(dir);
                                    }
                                }
                            }
                        }
                        compParams.ReferencedAssemblies.Add(refAsm);
                    }
                    compParams.ReferencedAssemblies.Add(typeof(PerfGraphToolWindowControl).Assembly.Location);
                    compParams.GenerateInMemory = true; // in memory cannot be unloaded
                    var resCompile = cdProvider.CompileAssemblyFromSource(compParams, strCodeToExecute);
                    if (resCompile.Errors.HasErrors)
                    {
                        var strb = new StringBuilder();
                        int nErrors = 0;
                        foreach (var err in resCompile.Errors)
                        {
                            strb.AppendLine(err.ToString());
                            logger.LogMessage(err.ToString());
                            nErrors++;
                        }
                        strb.AppendLine($"# errors = {nErrors}");
                        throw new InvalidOperationException(strb.ToString());
                    }
                    var asmCompiled = resCompile.CompiledAssembly;
                    foreach (var clas in asmCompiled.GetExportedTypes())
                    {
                        var mainMethod = clas.GetMethod(DoMain);
                        if (mainMethod != null)
                        {
                            if (!mainMethod.IsStatic)
                            {
                                throw new InvalidOperationException("DoMain must be static");
                            }

                            if (!fDidAddAssemblyResolver)
                            {
                                fDidAddAssemblyResolver = true;
                                AppDomain.CurrentDomain.AssemblyResolve += (o, e) =>
                                  {
                                      Assembly asm = null;
                                      logger.LogMessage($"AssmblyResolve {e.Name}  Requesting asm = {e.RequestingAssembly}");
                                      var requestName = e.Name.Substring(0, e.Name.IndexOf(","));
                                      foreach (var refDir in lstRefDirs)
                                      {
                                          foreach (var ext in new[] { ".dll", ".exe" })
                                          {
                                              var fname = Path.Combine(refDir, requestName, ext);
                                              if (File.Exists(fname))
                                              {
                                                  asm = Assembly.Load(fname);
                                                  break;
                                              }
                                          }
                                          if (asm != null)
                                          {
                                              break;
                                          }
                                      }
                                      return asm;
                                  };
                            }
                            // Types we pass must be very simple for compilation: e.g. don't want to bring in all of WPF...
                            object[] parms = new object[4];
                            parms[0] = logger;
                            parms[1] = token;
                            parms[2] = PerfGraphToolWindowCommand.Instance?.g_dte;
                            parms[3] = actTakeSample;
                            var res = mainMethod.Invoke(null, new object[] { parms });
                            if (res is string strres)
                            {
                                result = strres;
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result = ex.ToString();
            }
            return result;
        }
    }
}
