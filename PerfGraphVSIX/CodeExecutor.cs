using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    internal class CodeExecutor
    {
        public const string DoMain = "DoMain"; // not domain
        public const string refPathPrefix = "//Ref:";
        bool fDidAddAssemblyResolver;
        readonly ILogger logger;
        public CodeExecutor(ILogger logger)
        {
            this.logger = logger;
        }
        public string CompileAndExecute(string strCodeToExecute)
        {
            var result = string.Empty;
            logger.LogMessage($"Compiling code");
            try
            {
                using (var cdProvider = CodeDomProvider.CreateProvider("C#"))
                {
                    var compParams = new CompilerParameters();
                    var lstRefDirs = new List<string>();
                    var srcLines = strCodeToExecute.Split("\r\n".ToArray());
                    foreach (var refline in srcLines.Where(s => s.StartsWith(refPathPrefix)))
                    {
                        var refAsm = refline.Replace(refPathPrefix, string.Empty).Trim();
                        if (refAsm == nameof(PerfGraphVSIX))
                        {
                            refAsm = typeof(PerfGraphToolWindowControl).Assembly.Location;
                            lstRefDirs.Add(System.IO.Path.GetDirectoryName(refAsm));
                        }
                        else
                        {
                            if (refAsm.StartsWith("\"") && refAsm.EndsWith("\""))
                            {
                                refAsm = refAsm.Replace("\"", string.Empty);
                            }
                            var dir = System.IO.Path.GetDirectoryName(refAsm);
                            if (!string.IsNullOrEmpty(refAsm))
                            {
                                if (!System.IO.File.Exists(refAsm))
                                {
                                    throw new System.IO.FileNotFoundException($"Couldn't find {refAsm}");
                                }
                                else
                                {
                                    lstRefDirs.Add(dir);
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
                        foreach (var err in resCompile.Errors)
                        {
                            strb.AppendLine(err.ToString());
                            logger.LogMessage(err.ToString());
                        }
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
                            // todo add asmresolve
                            object[] parms = new object[2];
                            parms[0] = logger;
                            parms[1] = "p1";
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
