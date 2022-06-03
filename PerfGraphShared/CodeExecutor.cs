using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
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
        readonly ILogger _logger;

        public CodeExecutor(ILogger logger)
        {
            this._logger = logger;
        }
        public CompileHelper CompileTheCode(
            ITakeSample itakeSample, // pass this to executing code, which may not reference WPF asms
            string pathFileToExecute,
            CancellationToken token)
        {
            var compilerHelper = new CompileHelper(pathFileToExecute, _logger, itakeSample, token);
            try
            {
                compilerHelper.CompileTheCode();
            }
            catch (Exception ex)
            {
                compilerHelper.CompileResults = ex.ToString();
            }
            return compilerHelper;
        }

        public class CompileHelper : IDisposable
        {
            public const string DoMain = "DoMain"; // not domain
            public const string VSRootSubstitution = "%VSRoot%";
            public const string progfiles86 = @"C:\Program Files (x86)";
            public string RefPathPrefix => $"{CommentPrefix}Ref";
            public string IncludePathPrefix => $"{CommentPrefix}Include:";
            public string PragmaPrefix => $"{CommentPrefix}Pragma:";
            public string CommentPrefix; // for vb "'". For C# "//"
            bool _fDidAddAssemblyResolver;


            static int _hashOfPriorCodeToExecute;
            static Assembly _priorCompiledAssembly;
            HashSet<string> _lstRefDirs = new HashSet<string>();
            public MethodInfo mainMethod;
            public Assembly asmCompiled;
            public string pathFileToExecute;
            public ITakeSample itakeSample;
            public CancellationToken token;
            public readonly ILogger _logger;
            public bool verbose;
            public string CompileResults; // like err msg

            public CompileHelper(string pathFileToExecute, ILogger logger, ITakeSample itakeSample, CancellationToken token)
            {
                this.pathFileToExecute = pathFileToExecute;
                this.itakeSample = itakeSample;
                this.token = token;
                this._logger = logger;
            }

            // can be done on backgroun thread
            public void CompileTheCode()
            {
                var lstFilesToCompile = new HashSet<string>();
                var IsCSharp = true;
                if (Path.GetExtension(pathFileToExecute).ToLower() == ".vb")
                {
                    IsCSharp = false;
                    CommentPrefix = "'";
                }
                else
                {
                    CommentPrefix = "//";
                    IsCSharp = true;
                }
                var hashofCodeToExecute = 0;
                var GenerateInMemory = true;
                var UseCSC = true;
                verbose = false;
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

                    using (var cdProvider = CodeDomProvider.CreateProvider("C#"))
                    {
                        var compParams = new CompilerParameters();
                        _lstRefDirs = new HashSet<string>
                    {
                        Path.GetDirectoryName(pathFileToExecute)// add the dir of the source file as a ref dir
                    };
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
                                s => s.StartsWith(RefPathPrefix) ||
                                s.StartsWith(PragmaPrefix) ||
                                s.StartsWith(IncludePathPrefix)))
                            {
                                if (srcline.StartsWith(PragmaPrefix)) ////Pragma: GenerateInMemory=false
                                {
                                    var splitPragma = srcline.Substring(PragmaPrefix.Length).Split(new[] { '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    switch (splitPragma[0].ToLower())
                                    {
                                        case "generateinmemory":
                                            GenerateInMemory = bool.Parse(splitPragma[1]);
                                            if (verbose)
                                            {
                                                _logger.LogMessage($"Pragma {nameof(GenerateInMemory)}  = {GenerateInMemory} {Path.GetFileName(fileToCompile)}");
                                            }
                                            break;
                                        case "usecsc":
                                            UseCSC = bool.Parse(splitPragma[1]);
                                            if (verbose)
                                            {
                                                _logger.LogMessage($"Pragma {nameof(UseCSC)}  = {UseCSC} {Path.GetFileName(fileToCompile)}");
                                            }
                                            break;
                                        case "verbose":
                                            verbose = bool.Parse(splitPragma[1]);
                                            break;
                                        case "showwarnings":
                                            showWarnings = true;
                                            if (verbose)
                                            {
                                                _logger.LogMessage($"Pragma {nameof(showWarnings)} = {showWarnings} {Path.GetFileName(fileToCompile)}");
                                            }
                                            break;
                                        default:
                                            throw new InvalidOperationException($"Unknown Pragma {srcline}");
                                    }
                                }
                                else if (srcline.StartsWith(RefPathPrefix))
                                {
                                    // see what kind of ref it is: 32, 64 or both ("Ref32:","Ref64","Ref:")
                                    var refAsm = string.Empty;
                                    if (srcline.StartsWith(RefPathPrefix + ":"))
                                    {
                                        refAsm = srcline.Substring(RefPathPrefix.Length + 1).Trim(); ;
                                    }
                                    else if (srcline.StartsWith(RefPathPrefix + "32"))
                                    {
                                        if (IntPtr.Size == 4)
                                        {
                                            refAsm = srcline.Substring(RefPathPrefix.Length + 3).Trim(); ;
                                        }
                                    }
                                    else if (srcline.StartsWith(RefPathPrefix + "64"))
                                    {
                                        if (IntPtr.Size == 8)
                                        {
                                            refAsm = srcline.Substring(RefPathPrefix.Length + 3).Trim(); ;
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException($"Unknown reference type {srcline}");
                                    }
                                    if (!string.IsNullOrEmpty(refAsm))
                                    {

                                        if (refAsm.StartsWith("\"") && refAsm.EndsWith("\""))
                                        {
                                            refAsm = refAsm.Replace("\"", string.Empty);
                                        }
                                        if (refAsm.StartsWith("."))
                                        {
                                            refAsm = new FileInfo(Path.Combine(Path.GetDirectoryName(pathFileToExecute), refAsm)).FullName;
                                        }
                                        if (refAsm.Contains(progfiles86))// C:\Program Files (x86)\
                                        {
                                            var pfiles = Environment.GetEnvironmentVariable("ProgramFiles" + (IntPtr.Size == 8 ? "(x86)" : string.Empty));
                                            refAsm = refAsm.Replace(progfiles86, pfiles);
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
                                            refAsm = typeof(ILogger).Assembly.Location; // definitions
                                            compParams.ReferencedAssemblies.Add(refAsm);
                                            refAsm = typeof(StressUtil).Assembly.Location;
                                            compParams.ReferencedAssemblies.Add(refAsm);
                                        }
                                        else
                                        {
                                            if (refAsm.Contains(VSRootSubstitution))
                                            {
                                                refAsm = refAsm.Replace(VSRootSubstitution, vsRoot);
                                                var filename = Path.GetFileName(refAsm); //https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequest/336955?_a=files
                                                if (filename.Contains("Microsoft.VisualStudio.Threading.dll")) // %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Threading.16.0\Microsoft.VisualStudio.Threading.dll
                                                {
                                                    if (!File.Exists(refAsm)) // // %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Threading.17.x\Microsoft.VisualStudio.Threading.dll
                                                    {
                                                        var publicasms = Path.Combine(vsRoot, @"Common7\IDE\PublicAssemblies");
                                                        var vstfolders = Directory.GetDirectories(publicasms, "Microsoft.VisualStudio.Threading.*");
                                                        if (vstfolders.Length == 1)
                                                        {
                                                            refAsm = Path.Combine(vstfolders[0], filename);
                                                        }
                                                    }
                                                }
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
                                            compParams.ReferencedAssemblies.Add(refAsm);
                                        }
                                    }
                                }
                                else if (srcline.StartsWith(IncludePathPrefix))
                                {
                                    var include = srcline.Replace(IncludePathPrefix, string.Empty).Trim();
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
                                    // to support spaces in file paths, enclose them in quotes
                                    srcFiles += $@"""{srcfile}"" ";
                                }
                                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/reference-compiler-option
                                var args = $@"{srcFiles}/target:library /nologo /out:""{outfile}"" {refs}";
                                if (verbose)
                                {
                                    _logger.LogMessage($@"Compile line: ""{roslynExe}"" " + args);
                                }
                                using (var proc = Utility.CreateProcess(roslynExe, args, sb))
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
                    if (verbose)
                    {
                        _logger.LogMessage($"Looking for Static Main");
                    }
                    var didGetMain = false;
                    if (!_fDidAddAssemblyResolver)
                    {
                        _fDidAddAssemblyResolver = true;
                        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver;
                    }
                    foreach (var clas in asmCompiled.GetExportedTypes())
                    {
                        mainMethod = clas.GetMethod(DoMain);
                        if (mainMethod != null)
                        {
                            if (!mainMethod.IsStatic)
                            {
                                throw new InvalidOperationException("DoMain must be static");
                            }

                            didGetMain = true;
                            //                        _logger.LogMessage($"mainmethod rettype = {mainMethod.ReturnType.Name}");
                            break;
                        }
                    }
                    if (!didGetMain)
                    {
                        throw new InvalidOperationException($"Couldn't find static {DoMain} in {pathFileToExecute}");
                    }
                }
                catch (Exception)
                {
                    _hashOfPriorCodeToExecute = 0;
                    _priorCompiledAssembly = null;
                    throw;
                }
            }
            Assembly AssemblyResolver(object sender, ResolveEventArgs e)
            {
                Assembly asm = null;
                _logger.LogMessage($"AssmblyResolve {e.Name}  Requesting asm = {e.RequestingAssembly}");
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
            }
            // must be called on UI thread
            public object ExecuteTheCode()
            {
                object result = string.Empty;
                object[] parms = new object[]
                {
                            pathFileToExecute,
                            _logger,
                            token,
                            itakeSample,
                            PerfGraphToolWindowCommand.Instance?.g_dte,
                            PerfGraphToolWindowCommand.Instance?.package
                };
                if (verbose)
                {
                    _logger.LogMessage($"Calling Static Main");
                }
                var res = mainMethod.Invoke(null, new object[] { parms });
                if (verbose)
                {
                    _logger.LogMessage($"Static Main return= {res}");
                }
                if (res is string strres)
                {
                    result = strres;
                }
                if (res is System.Threading.Tasks.Task)
                {
                    result = res;
                }
                return result;
            }

            public void Dispose()
            {
                if (_fDidAddAssemblyResolver)
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolver; // Need resolver around til execution done
                    _fDidAddAssemblyResolver = false;
                }
            }
        }
    }
}
