using Microsoft.Test.Stress;
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
        public string refPathPrefix = "//Ref:";
        public string includePathPrefix = "//Include:";
        public string pragmaPrefix = "//Pragma:";
        bool _fDidAddAssemblyResolver;
        readonly ILogger _logger;


        int _hashOfPriorCodeToExecute;
        Assembly _priorCompiledAssembly;
        HashSet<string> _lstRefDirs = new HashSet<string>();


        public CodeExecutor(ILogger logger)
        {
            this._logger = logger;
        }
        public object CompileAndExecute(
            ITakeSample itakeSample,
            string pathFileToExecute,
            CancellationToken token,
            bool fExecuteToo = true) // for tests, we want to compile and not execute
        {
            object result = string.Empty;
            var lstFilesToCompile = new HashSet<string>();
            var IsCSharp = true;
            if (Path.GetExtension(pathFileToExecute).ToLower() == ".vb")
            {
                IsCSharp = false;
                refPathPrefix = refPathPrefix.Replace("//", "'");
                includePathPrefix = includePathPrefix.Replace("//", "'");
                pragmaPrefix = pragmaPrefix.Replace("//", "'"); ;
            }
            var hashofCodeToExecute = 0;
            var GenerateInMemory = true;
            var UseCSC = true;
            var verbose = false;
            var showWarnings = false;
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

                Assembly asmCompiled = null;
                using (var cdProvider = CodeDomProvider.CreateProvider("C#"))
                {
                    var compParams = new CompilerParameters();
                    _lstRefDirs = new HashSet<string>();
                    _lstRefDirs.Add(Path.GetDirectoryName(pathFileToExecute));// add the dir of the source file as a ref dir
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
                        foreach (var srcline in srcLines.Where(
                            s => s.StartsWith(refPathPrefix) ||
                            s.StartsWith(pragmaPrefix) ||
                            s.StartsWith(includePathPrefix)))
                        {
                            if (srcline.StartsWith(pragmaPrefix)) ////Pragma: GenerateInMemory=false
                            {
                                var splitPragma = srcline.Substring(pragmaPrefix.Length).Split(new[] { '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                switch (splitPragma[0].ToLower())
                                {
                                    case "generateinmemory":
                                        GenerateInMemory = bool.Parse(splitPragma[1]);
                                        _logger.LogMessage($"Pragma {nameof(GenerateInMemory)}  = {GenerateInMemory} {Path.GetFileName(fileToCompile)}");
                                        break;
                                    case "usecsc":
                                        UseCSC = bool.Parse(splitPragma[1]);
                                        _logger.LogMessage($"Pragma {nameof(UseCSC)}  = {UseCSC} {Path.GetFileName(fileToCompile)}");
                                        break;
                                    case "verbose":
                                        verbose = bool.Parse(splitPragma[1]);
                                        break;
                                    case "showwarnings":
                                        showWarnings = true;
                                        break;
                                    default:
                                        throw new InvalidOperationException($"Unknown Pragma {srcline}");
                                }
                            }
                            else if (srcline.StartsWith(refPathPrefix))
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
                                    if (string.IsNullOrEmpty(dir))
                                    {
                                        var temp = Path.Combine(Path.GetDirectoryName(pathFileToExecute), refAsm);
                                        if (File.Exists(temp))
                                        {
                                            refAsm = temp;
                                        }
                                    }
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
                    if (_priorCompiledAssembly != null && _hashOfPriorCodeToExecute == hashofCodeToExecute) // if we can use prior compile results
                    {
                        _logger.LogMessage($"No Compilation required: Using prior compiled assembly for {pathFileToExecute}");
                        asmCompiled = _priorCompiledAssembly;
                    }
                    else
                    {
                        if (!UseCSC)
                        {
                            //                        compParams.ReferencedAssemblies.Add(typeof(DependencyObject).Assembly.Location); // C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll  c:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF  c:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF
                            compParams.ReferencedAssemblies.Add(typeof(PerfGraphToolWindowControl).Assembly.Location);
                            if (GenerateInMemory)
                            {
                                compParams.GenerateInMemory = true; // in memory cannot be unloaded
                            }
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
                            asmCompiled = resCompile.CompiledAssembly;
                        }
                        else
                        {// C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Current\bin\Roslyn\csc.exe
                            var roslynExe = Path.Combine(vsRoot, @"MSBuild\Current\Bin\Roslyn", (IsCSharp ? "csc.exe" : "vbc.exe"));
                            if (!File.Exists(roslynExe))
                            {
                                throw new FileNotFoundException(roslynExe);
                            }
                            var sb = new StringBuilder();
                            // Csc /target:library -out:asm.exe -r:<filelist>
                            var outfile = Path.ChangeExtension(Path.GetTempFileName(), ".dll");
                            var refs = string.Empty;
                            if (compParams.ReferencedAssemblies?.Count > 0)
                            {
                                foreach (var refd in compParams.ReferencedAssemblies)
                                {
                                    refs += $@"-r:""{refd}"" ";
                                }
                            }
                            var srcFiles = string.Empty;
                            foreach (var srcfile in lstFilesToCompile)
                            {
                                srcFiles += " " + srcfile;
                            }
                            // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/reference-compiler-option
                            var args = $@"{srcFiles} /target:library /nologo /out:""{outfile}"" {refs}";
                            if (verbose)
                            {
                                _logger.LogMessage($@"Compile line: ""{roslynExe}"" " + args.ToString());
                            }
                            using (var proc = VSHandler.CreateProcess(roslynExe, args, sb))
                            {
                                proc.Start();
                                proc.BeginOutputReadLine();
                                proc.BeginErrorReadLine();
                                proc.WaitForExit();
                            }
                            //                            _logger.LogMessage("{0}", sb.ToString());
                            if (!File.Exists(outfile) || sb.ToString().Contains(": error"))
                            {
                                throw new InvalidOperationException(sb.ToString());
                            }
                            if (showWarnings && sb.ToString().Contains("warning"))
                            {
                                _logger.LogMessage(sb.ToString());
                            }
                            asmCompiled = Assembly.LoadFrom(outfile);
                        }
                    }
                }
                _hashOfPriorCodeToExecute = hashofCodeToExecute;
                _priorCompiledAssembly = asmCompiled;
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
                                  else if (requestName == nameof(Microsoft.Test.Stress))
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
                                          _logger.LogMessage($"AssemblyResolver: Couldn't resolve {e.Name}");
                                      }
                                  }
                                  return asm;
                              };
                        }
                        //                        _logger.LogMessage($"mainmethod rettype = {mainMethod.ReturnType.Name}");
                        if (fExecuteToo)
                        {
                            // Types we pass must be very simple for compilation: e.g. don't want to bring in all of WPF...
                            object[] parms = new object[]
                            {
                            pathFileToExecute,
                            _logger,
                            token,
                            itakeSample,
                            PerfGraphToolWindowCommand.Instance?.g_dte,
                            PerfGraphToolWindowCommand.Instance?.package
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
                        }
                        break;
                    }
                }
                if (!didGetMain)
                {
                    throw new InvalidOperationException($"Couldn't find static {DoMain} in {pathFileToExecute}");
                }
            }
            catch (Exception ex)
            {
                result = ex.ToString();
                _hashOfPriorCodeToExecute = 0;
                _priorCompiledAssembly = null;
            }
            return result;
        }
    }
}
