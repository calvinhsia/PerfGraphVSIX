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
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

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

        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.DoSomeWorkAsync();
        }
        public MyClass(object[] args)
        {
            _tcs = new TaskCompletionSource<int>();
            logger = args[0] as ILogger;
            _CancellationToken = (CancellationToken)args[1]; // value type
            g_dte= args[2] as EnvDTE.DTE;
            actTakeSample = args[3] as Action<string>;
        }
        private async Task DoSomeWorkAsync()
        {
//            logger.LogMessage(""in DoSomeWorkAsync"");
            try
            {
                if (nTimes++ == 0)
                {
                    logger.LogMessage(""Registering solution events"");
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
                }
                // Keep in mind that the UI will be unresponsive if you have no await and no main thread idle time

                for (int i = 0; i < NumberOfIterations && !_CancellationToken.IsCancellationRequested; i++)
                {
                    var desc = string.Format(""Iter {0}/{1}"", i, NumberOfIterations - 1);
                    DoSample(desc);
                    await Task.Delay(1000); // wait one second to allow UI thread to catch  up
//                    logger.LogMessage(desc);
                    await OpenASolutionAsync();
                    if (_CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    await CloseTheSolutionAsync();
//                    logger.LogMessage(""End of Iter {0}"", i);
                }
                var msg = ""Cancelled"";
                if (!_CancellationToken.IsCancellationRequested)
                {
                    msg = string.Format(""Done all {0} iterations"", NumberOfIterations);
                }
                DoSample(msg);
            }
            finally
            {
                logger.LogMessage(""UnRegistering solution events"");
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
            }
        }

        void DoSample(string desc)
        {
            if (actTakeSample != null)
            {
                actTakeSample(desc);
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
//            logger.LogMessage(""SolutionEvents_OnAfterCloseSolution"");
            _tcs.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
//            logger.LogMessage(""SolutionEvents_OnAfterBackgroundSolutionLoadComplete"");
            _tcs.TrySetResult(0);
        }
    }
}
";

        public const string DoMain = "DoMain"; // not domain
        public const string VSRootSubstitution = "%VSRoot%";
        public const string refPathPrefix = "//Ref:";
        bool _fDidAddAssemblyResolver;
        readonly ILogger _logger;


        int _hashOfPriorCodeToExecute;
        CompilerResults _resCompile;
        HashSet<string> _lstRefDirs = new HashSet<string>();


        public CodeExecutor(ILogger logger)
        {
            this._logger = logger;
        }
        public object CompileAndExecute(string strCodeToExecute, CancellationToken token, Action<string> actTakeSample = null)
        {
            object result = string.Empty;
            var hashofCodeToExecute = strCodeToExecute.GetHashCode();
//            _logger.LogMessage($"Compiling code");
            try
            {
                if (_resCompile != null && _hashOfPriorCodeToExecute == hashofCodeToExecute) // if we can use prior compile results
                {
                    _logger.LogMessage($"Using prior compiled assembly");
                }
                else
                {
                    _lstRefDirs.Clear();
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
                        _lstRefDirs = new HashSet<string>();
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
                                refAsm = this.GetType().Assembly.Location;
                                var dir = System.IO.Path.GetDirectoryName(refAsm);
                                if (!_lstRefDirs.Contains(dir))
                                {
                                    _lstRefDirs.Add(dir);
                                }
                            }
                            else
                            {
                                if (refAsm.Contains(VSRootSubstitution))
                                {
                                    refAsm = refAsm.Replace(VSRootSubstitution, vsRoot);
                                }
                                var dir = System.IO.Path.GetDirectoryName(refAsm);
                                //                                _logger.LogMessage($"AddRef {refAsm}");
                                if (!string.IsNullOrEmpty(refAsm))
                                {
                                    if (!System.IO.File.Exists(refAsm))
                                    {
                                        throw new System.IO.FileNotFoundException($"Couldn't find {refAsm}");
                                    }
                                    else
                                    {
                                        if (!_lstRefDirs.Contains(dir))
                                        {
                                            _lstRefDirs.Add(dir);
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
                                _logger.LogMessage(err.ToString());
                                nErrors++;
                            }
                            strb.AppendLine($"# errors = {nErrors}");
                            throw new InvalidOperationException(strb.ToString());
                        }
                        _resCompile = resCompile;
                        _hashOfPriorCodeToExecute = hashofCodeToExecute;
                    }
                }
                var asmCompiled = _resCompile.CompiledAssembly;
                foreach (var clas in asmCompiled.GetExportedTypes())
                {
                    var mainMethod = clas.GetMethod(DoMain);
                    if (mainMethod != null)
                    {
                        if (!mainMethod.IsStatic)
                        {
                            throw new InvalidOperationException("DoMain must be static");
                        }

                        if (!_fDidAddAssemblyResolver)
                        {
                            _fDidAddAssemblyResolver = true;
  //                          _logger.LogMessage("Register for AssemblyResolve");
                            AppDomain.CurrentDomain.AssemblyResolve += (o, e) =>
                              {
                                  Assembly asm = null;
                                  _logger.LogMessage($"AssmblyResolve {e.Name}  Requesting asm = {e.RequestingAssembly}");
                                  var requestName = e.Name.Substring(0, e.Name.IndexOf(","));
                                  if (requestName == nameof(PerfGraphVSIX))
                                  {
                                      asm = this.GetType().Assembly;
                                  }
                                  else
                                  {
                                      foreach (var refDir in _lstRefDirs)
                                      {
                                          foreach (var ext in new[] { ".dll", ".exe" })
                                          {
                                              var fname = Path.Combine(refDir, requestName) + ext;
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
                                      if (asm == null)
                                      {
                                          _logger.LogMessage($"Couldn't resolve {e.Name}");
                                      }
                                  }
                                  return asm;
                              };
                        }
//                        _logger.LogMessage($"mainmethod rettype = {mainMethod.ReturnType.Name}");
                        // Types we pass must be very simple for compilation: e.g. don't want to bring in all of WPF...
                        object[] parms = new object[4];
                        parms[0] = _logger;
                        parms[1] = token;
                        parms[2] = PerfGraphToolWindowCommand.Instance?.g_dte;
                        parms[3] = actTakeSample;
                        var res = mainMethod.Invoke(null, new object[] { parms });
                        if (res is string strres)
                        {
                            result = strres;
                        }
                        if (res is Task task)
                        {
                            result = res;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                result = ex.ToString();
                _hashOfPriorCodeToExecute = 0;
                _resCompile = null;
            }
            return result;
        }
    }
}
