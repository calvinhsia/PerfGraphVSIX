using Microsoft.VisualStudio.StressTest;
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
using System.Windows;

namespace PerfGraphVSIX
{
    /// <summary>
    /// Given a path to a single file (the main file), compile and execute it. 
    /// A source line starting with '//Ref:' means add a reference to the asm. (possibly quoted) e.g. //Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
    ///    Some special macros like '%VSROOT%' and %PerfGraphVSIX% can be used
    /// A source line starting with "//Include:" means include the file (in same dir as main file)
    /// </summary>
    public class CodeExecutor
    {
        public const string DoMain = "DoMain"; // not domain
        public const string VSRootSubstitution = "%VSRoot%";
        public const string refPathPrefix = "//Ref:";
        public const string includePathPrefix = "//Include:";
        bool _fDidAddAssemblyResolver;
        readonly ILogger _logger;


        int _hashOfPriorCodeToExecute;
        CompilerResults _resCompile;
        HashSet<string> _lstRefDirs = new HashSet<string>();


        public CodeExecutor(ILogger logger)
        {
            this._logger = logger;
        }
        public object CompileAndExecute(ITakeSample itakeSample, string pathFileToExecute, CancellationToken token)
        {
            object result = string.Empty;
            var lstFilesToCompile = new HashSet<string>();
            var hashofCodeToExecute = 0;
            //            _logger.LogMessage($"Compiling code");
            try
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
                    void AddFileToCompileList(string fileToCompile)
                    {
                        if (lstFilesToCompile.Contains(fileToCompile))
                        {
                            return;
                        }
                        lstFilesToCompile.Add(fileToCompile);
                        var strCodeToExecute = File.ReadAllText(fileToCompile);
                        hashofCodeToExecute += strCodeToExecute.GetHashCode();
                        var srcLines = strCodeToExecute.Split("\r\n".ToArray());
                        foreach (var srcline in srcLines.Where(s => s.StartsWith(refPathPrefix) || s.StartsWith(includePathPrefix)))
                        {
                            if (srcline.StartsWith(refPathPrefix))
                            {
                                var refAsm = srcline.Replace(refPathPrefix, string.Empty).Trim();
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
                                    compParams.ReferencedAssemblies.Add(refAsm);
                                    refAsm = typeof(ILogger).Assembly.Location;
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
                            else if (srcline.StartsWith(includePathPrefix))
                            {
                                var include = srcline.Replace(includePathPrefix, string.Empty).Trim();
                                if (include.StartsWith("\"") && include.EndsWith("\""))
                                {
                                    include = include.Replace("\"", string.Empty);
                                }
                                include = Path.Combine(Path.GetDirectoryName(fileToCompile), include);
                                //_logger.LogMessage($"Adding Include file {include}");
                                AddFileToCompileList(include);
                            }
                        }
                    }
                    AddFileToCompileList(pathFileToExecute);
                    if (_resCompile != null && _hashOfPriorCodeToExecute == hashofCodeToExecute) // if we can use prior compile results
                    {
                        _logger.LogMessage($"Using prior compiled assembly for {pathFileToExecute}");
                    }
                    else
                    {
                        //                        compParams.ReferencedAssemblies.Add(typeof(DependencyObject).Assembly.Location); // C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll  c:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF  c:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF
                        compParams.ReferencedAssemblies.Add(typeof(PerfGraphToolWindowControl).Assembly.Location);
                        compParams.GenerateInMemory = true; // in memory cannot be unloaded
                        var resCompile = cdProvider.CompileAssemblyFromFile(compParams, lstFilesToCompile.ToArray());
                        if (resCompile.Errors.HasErrors || resCompile.Errors.HasWarnings)
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
                var didGetMain = false;
                foreach (var clas in asmCompiled.GetExportedTypes())
                {
                    var mainMethod = clas.GetMethod(DoMain);
                    if (mainMethod != null)
                    {
                        if (!mainMethod.IsStatic)
                        {
                            throw new InvalidOperationException("DoMain must be static");
                        }

                        didGetMain = true;
                        if (!_fDidAddAssemblyResolver)
                        {
                            _fDidAddAssemblyResolver = true;
                            //                          _logger.LogMessage("Register for AssemblyResolve");
                            AppDomain.CurrentDomain.AssemblyResolve += (o, e) =>
                              {
                                  Assembly asm = null;
                                  //                                  _logger.LogMessage($"AssmblyResolve {e.Name}  Requesting asm = {e.RequestingAssembly}");
                                  var requestName = e.Name.Substring(0, e.Name.IndexOf(","));
                                  if (requestName == nameof(PerfGraphVSIX))
                                  {
                                      asm = this.GetType().Assembly;
                                  }
                                  else if (requestName == nameof(Microsoft.VisualStudio.StressTest))
                                  {
                                      asm = typeof(ILogger).Assembly;
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
                                                  try
                                                  {
                                                      asm = Assembly.Load(fname);
                                                  }
                                                  catch (Exception)
                                                  {
                                                      asm = Assembly.LoadFrom(fname);
                                                  }
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
                        object[] parms = new object[]
                        {
                            pathFileToExecute,
                            _logger,
                            token,
                            itakeSample,
                            PerfGraphToolWindowCommand.Instance?.g_dte,
                        };
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
                if (!didGetMain)
                {
                    throw new InvalidOperationException($"Couldn't find static Main in {pathFileToExecute}");
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
