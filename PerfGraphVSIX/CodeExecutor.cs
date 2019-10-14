using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    internal class CodeExecutor
    {
        public const string DoMain = "DoMain"; // not domain
        public const string refPathPrefix = "//Ref:";
        static public string CompileAndExecute(string strCodeToExecute)
        {
            var result = string.Empty;
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
                        compParams.ReferencedAssemblies.Add(refAsm);
                    }
                    compParams.GenerateInMemory = true; // in memory cannot be unloaded
                    var resCompile = cdProvider.CompileAssemblyFromSource(compParams, strCodeToExecute);
                    if (resCompile.Errors.HasErrors)
                    {
                        var strb = new StringBuilder();
                        foreach (var err in resCompile.Errors)
                        {
                            strb.AppendLine(err.ToString());
                        }
                        throw new InvalidOperationException(strb.ToString());
                    }
                    var asm = resCompile.CompiledAssembly;
                    foreach (var clas in asm.GetExportedTypes())
                    {
                        var mainMethod = clas.GetMethod(DoMain);
                        if (mainMethod != null)
                        {
                            if (!mainMethod.IsStatic)
                            {
                                throw new InvalidOperationException("DoMain must be static");
                            }
                            // todo add asmresolve
                            var res = mainMethod.Invoke(null, new object[] { new string[] { "p1" } });
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
