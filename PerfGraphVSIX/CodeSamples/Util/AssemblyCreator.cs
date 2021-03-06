﻿
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Performance.ResponseTime
{
    public class AssemblyCreator
    {
        /// <summary>
        /// We want to create an assembly that will be loaded in an exe (perhaps 64 bit) that will load and call a target method (could be static or non-static, public, non-public)
        /// Taking a process dump of a 64 bit process from a 32 bit process doesn't work. Even from 32 bit task manager.
        /// This code emits an Asm that can be made into a 64 bit executable
        /// The goal is to call a static method in 32 bit PerfWatson in a static class MemoryDumpHelper with the signature:
        ///           public static void CollectDump(int procid, string pathOutput, bool FullHeap)
        /// The generated asm can be saved as an exe on disk, then started from 32 bit code. 
        ///  A little wrinkle: in order to enumerate the types in the DLL, the Appdomain AsemblyResolver needs to find the dependencies
        /// The 64 bit process will then load the 32 bit PW IL (using the assembly resolver, then invoke the method via reflection)
        /// the parameters are pased to the 64 bit exe on the commandline.
        /// This code logs output to the output file (which is the dump file when called with logging false)
        /// The code generates a static Main (string[] args) method.
        ///  see https://github.com/calvinhsia/CreateDump
        /// </summary>
        /// <param name="targPEFile"></param>
        /// <param name="portableExecutableKinds"></param>
        /// <param name="imageFileMachine"></param>
        /// <param name="AdditionalAssemblyPaths">a single full path or ';' separted fullpaths for additional dirs to load dependencies</param>
        /// <param name="logOutput"></param>
        /// <param name="CauseException"></param>
        /// <returns></returns>
        public Type CreateAssembly(
                string targPEFile,
                PortableExecutableKinds portableExecutableKinds,
                ImageFileMachine imageFileMachine,
                string AdditionalAssemblyPaths,
                bool logOutput = false,
                bool CauseException = false
            )
        {
            var typeName = Path.GetFileNameWithoutExtension(targPEFile);
            var aName = new AssemblyName(typeName);
            // the Appdomain DefineDynamicAssembly has an overload for Dir
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                aName,
                AssemblyBuilderAccess.RunAndSave,
                dir: Path.GetDirectoryName(targPEFile));
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(aName.Name + ".exe");
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);
            var statTarg32bitDll = typeBuilder.DefineField("targ32bitDll", typeof(string), FieldAttributes.Static);
            var statAddDirs = typeBuilder.DefineField("_additionalDirs", typeof(string), FieldAttributes.Static);
            var statStringBuilder = typeBuilder.DefineField("_StringBuilder", typeof(StringBuilder), FieldAttributes.Static);
            var statLogOutputFile = typeBuilder.DefineField("_logOutputFile", typeof(string), FieldAttributes.Static);
            MethodBuilder AsmResolveMethodBuilder = null;
            if (!string.IsNullOrEmpty(AdditionalAssemblyPaths))
            {
                /* we define the Resolve method:
                    private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
                    {
                        Assembly asm = null;
                        var requestName = args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll"; // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                        var split = _additionalDirs.Split(new[] { ';' });
                        foreach (var dir in split)
                        {
                            var trypath = Path.Combine(dir, requestName);
                            if (File.Exists(trypath))
                            {
                                asm = Assembly.LoadFrom(trypath);
                                if (asm != null)
                                {
                                    break;
                                }
                            }
                        }
                        return asm;
                    }
                 * */
                AsmResolveMethodBuilder = typeBuilder.DefineMethod(
                    "CurrentDomain_AssemblyResolve",
                    MethodAttributes.Static,
                    returnType: typeof(Assembly),
                    parameterTypes: new Type[] { typeof(object), typeof(ResolveEventArgs) }
                    );
                {
                    var il = AsmResolveMethodBuilder.GetILGenerator();
                    var locAsm = il.DeclareLocal(typeof(Assembly));
                    var locStrRequestAsmName = il.DeclareLocal(typeof(string));
                    var locStrArrSplit = il.DeclareLocal(typeof(string[]));
                    var locStrTemp = il.DeclareLocal(typeof(string));
                    var locLoopIndex = il.DeclareLocal(typeof(int));

                    //var requestName = args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll"; // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, typeof(ResolveEventArgs).GetProperty("Name").GetMethod); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, typeof(ResolveEventArgs).GetProperty("Name").GetMethod); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    il.Emit(OpCodes.Ldstr, ",");
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("IndexOf", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("Substring", new Type[] { typeof(Int32), typeof(Int32) }));
                    il.Emit(OpCodes.Ldstr, ".dll");
                    il.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Stloc, locStrRequestAsmName); // Microsoft.VisualStudio.Telemetry

                    if (logOutput)
                    {
                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Ldstr, "Resolve Request ");
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("Append", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Ldloc, locStrRequestAsmName);
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }

                    // 

                    // var arr = _additionalDirs.Split(new[] { ';' })
                    il.Emit(OpCodes.Ldsfld, statAddDirs);
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Newarr, typeof(Char));
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldc_I4, 59); // ';'
                    il.Emit(OpCodes.Stelem_I2);
                    il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("Split", new Type[] { typeof(char[]) }));
                    il.Emit(OpCodes.Stloc, locStrArrSplit);


                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stloc, locLoopIndex);
                    var labLoopIndex = il.DefineLabel();
                    var labStartLoop = il.DefineLabel();
                    {
                        il.MarkLabel(labStartLoop);
                        il.Emit(OpCodes.Ldloc, locStrArrSplit);
                        il.Emit(OpCodes.Ldloc, locLoopIndex);
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Ldloc, locStrRequestAsmName);
                        il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                        il.Emit(OpCodes.Stloc, locStrTemp);

                        if (logOutput)
                        {
                            il.Emit(OpCodes.Ldsfld, statStringBuilder);
                            il.Emit(OpCodes.Ldloc, locStrTemp);
                            il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Pop);
                        }

                        il.Emit(OpCodes.Ldloc, locStrTemp);
                        il.Emit(OpCodes.Call, typeof(File).GetMethod("Exists", new Type[] { typeof(string) }));

                        var labFileNotExist = il.DefineLabel();
                        il.Emit(OpCodes.Brfalse, labFileNotExist);
                        {
                            il.Emit(OpCodes.Ldloc, locStrTemp);
                            il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Ret);
                        }
                        il.MarkLabel(labFileNotExist);

                        il.Emit(OpCodes.Ldloc, locLoopIndex);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Add);
                        il.Emit(OpCodes.Stloc, locLoopIndex);
                    }
                    il.MarkLabel(labLoopIndex);
                    il.Emit(OpCodes.Ldloc, locLoopIndex);
                    il.Emit(OpCodes.Ldloc, locStrArrSplit);
                    il.Emit(OpCodes.Ldlen);
                    il.Emit(OpCodes.Conv_I4);
                    il.Emit(OpCodes.Blt, labStartLoop);

                    // return asm
                    il.Emit(OpCodes.Ldloc, locAsm);
                    il.Emit(OpCodes.Ret);
                }
            }
            // the main method gets these params:
            // args[0] = TargAsmWIthCodeToRun
            // args[1] = TargTypeName
            // args[2] = TargMethodName
            // args[3...] = any parameters to pass to the method. If there are 3 params, then these are args[3-6]

            //"C:\Users\calvinh\Documents\MyTestAsm.exe" "C:\Users\calvinh\source\repos\CreateDump\UnitTestProject1\bin\Debug\CreateAsm.dll" TargetStaticClass MyStaticMethodWith3Param 28284 "C:\Users\calvinh\Documents\MyTestAsm.log" true
            /* We define the static Main method
                    try
                    {
                        if (!string.IsNullOrEmpty(additionalDirs))
                        {
                            _additionalDirs = additionalDirs;
                            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                        }

                        var targAsm = Assembly.LoadFrom(fullPathAsmName);
                        sb.AppendLine($"Attempting to invoke via reflection: {typeName} {methodName}");
                        foreach (var type in targAsm.GetTypes())
                        {
                            if (type.Name == typeName)
                            {
                                var methinfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                                var parms = methinfo.GetParameters();
                                sb.AppendLine($"{typeName} {methodName} parms.Length={parms.Length}");
                                foreach (var parm in parms)
                                {
                                    sb.AppendLine($"{parm.Name} {parm.ParameterType}");
                                }
                                object typInstance = null;
                                if (!type.IsAbstract) // static class
                                {
                                    typInstance = Activator.CreateInstance(type);
                                }
                                var oparms = new object[parms.Length];
                                if (targArgs == null && parms.Length > 0 || targArgs != null && targArgs.Length != parms.Length)
                                {
                                    throw new ArgumentException($"Method {typeName}{methodName} requires #parms= {parms.Length}, but only {targArgs?.Length} provided");
                                }
                                for (int i = 0; i < parms.Length; i++)
                                {
                                    var pname = parms[i].ParameterType.Name;
                                    if (pname == "String")
                                    {
                                        oparms[i] = targArgs[i];
                                    }
                                    else if (pname == "Int32")
                                    {
                                        oparms[i] = int.Parse(targArgs[i]);
                                    }
                                    else if (pname == "Boolean")
                                    {
                                        oparms[i] = bool.Parse(targArgs[i]);
                                    }
                                }
                                methinfo.Invoke(typInstance, oparms);
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        sb.AppendLine($"LoaderException: {ex.LoaderExceptions[0]}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"Exception: {typeName} {methodName} {ex.ToString()}");
                    }
                    AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

             */
            int argOffset = 3;
            var mainMethodBuilder = typeBuilder.DefineMethod(
                "Main",
                MethodAttributes.Public | MethodAttributes.Static,
                returnType: null,
                parameterTypes: new Type[] { typeof(string[]) });
            {
                var il = mainMethodBuilder.GetILGenerator();
                var labEnd = il.DefineLabel();
                var locStrTemp = il.DeclareLocal(typeof(string));
                var locdtNow = il.DeclareLocal(typeof(DateTime));
                var locAsmTarg32 = il.DeclareLocal(typeof(Assembly));
                var locTypeArr = il.DeclareLocal(typeof(Type[]));
                var locIntLoopIndex = il.DeclareLocal(typeof(Int32));
                var locTypeCurrent = il.DeclareLocal(typeof(Type));
                var locStrTypeName = il.DeclareLocal(typeof(string));
                var locMIMethod = il.DeclareLocal(typeof(MethodInfo));
                var locObjInstance = il.DeclareLocal(typeof(object));
                var locObjArrArgsToPass = il.DeclareLocal(typeof(object[]));
                var locIntParmLoopIndex = il.DeclareLocal(typeof(Int32));
                var locParameterInfoArr = il.DeclareLocal(typeof(ParameterInfo[]));
                var locStrParameterName = il.DeclareLocal(typeof(string));

                il.BeginExceptionBlock();
                {
                    il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(new Type[0]));
                    il.Emit(OpCodes.Stsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldc_I4_5); //Environment.SpecialFolder.MyDocuments
                    il.Emit(OpCodes.Call, typeof(Environment).GetMethod("GetFolderPath", new Type[] { typeof(Environment.SpecialFolder) }));
                    il.Emit(OpCodes.Ldstr, "MyTestAsm.log");
                    il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
                    il.Emit(OpCodes.Stsfld, statLogOutputFile);

                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "InMyTestAsm!!!");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                    il.Emit(OpCodes.Call, typeof(Environment).GetProperty("CommandLine").GetMethod);
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);

                    il.Emit(OpCodes.Ldstr, AdditionalAssemblyPaths);
                    il.Emit(OpCodes.Stsfld, statAddDirs);

                    if (CauseException)
                    {
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString", new Type[0]));
                    }

                    //targ32bitDll = args[0];
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Stsfld, statTarg32bitDll);
                    if (AsmResolveMethodBuilder != null)
                    {
                        //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                        il.Emit(OpCodes.Call, typeof(AppDomain).GetProperty("CurrentDomain").GetMethod);
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Ldftn, AsmResolveMethodBuilder);
                        il.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                        il.Emit(OpCodes.Callvirt, typeof(AppDomain).GetEvent("AssemblyResolve").GetAddMethod());

                        if (logOutput)
                        {
                            il.Emit(OpCodes.Ldsfld, statStringBuilder);
                            il.Emit(OpCodes.Ldstr, "Asm ResolveEvents events subscribed");
                            il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Pop);
                        }
                    }

                    //var asmprog32 = Assembly.LoadFrom(args[0]);
                    il.Emit(OpCodes.Ldsfld, statTarg32bitDll);
                    il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Stloc, locAsmTarg32);

                    //foreach (var type in asmprog32.GetExportedTypes())
                    il.Emit(OpCodes.Ldloc, locAsmTarg32);
                    il.Emit(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetTypes"));
                    il.Emit(OpCodes.Stloc, locTypeArr); // type[]

                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stloc, locIntLoopIndex); // loop index
                    var labIncLoop = il.DefineLabel();
                    var labBreakLoop = il.DefineLabel();
                    il.Emit(OpCodes.Br, labIncLoop);
                    {
                        var labStartLoop = il.DefineLabel();
                        il.MarkLabel(labStartLoop);

                        il.Emit(OpCodes.Ldloc, locTypeArr); // type[]
                        il.Emit(OpCodes.Ldloc, locIntLoopIndex);// loop index
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Stloc, locTypeCurrent);
                        il.Emit(OpCodes.Ldloc, locTypeCurrent);
                        il.Emit(OpCodes.Callvirt, typeof(MemberInfo).GetProperty("Name").GetMethod);
                        il.Emit(OpCodes.Stloc, locStrTypeName);

                        if (logOutput)
                        {
                            il.Emit(OpCodes.Ldsfld, statStringBuilder);
                            il.Emit(OpCodes.Ldloc, locStrTypeName);
                            il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Pop);
                        }

                        //if (type.Name == args[1])
                        var labNotOurType = il.DefineLabel();
                        il.Emit(OpCodes.Ldloc, locStrTypeName);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Ldelem_Ref);

                        il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                        il.Emit(OpCodes.Brfalse, labNotOurType);
                        {
                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "GotOurType");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }

                            //var methCollectDump = type.GetMethod(args[2]);
                            il.Emit(OpCodes.Ldloc, locTypeCurrent);
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldc_I4_2);
                            il.Emit(OpCodes.Ldelem_Ref);
                            il.Emit(OpCodes.Ldc_I4, 8 + 32 + 16 + 4); // static ==8, nonpublic == 32, public == 16, instance=4
                            il.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("GetMethod", new Type[] { typeof(string), typeof(BindingFlags) }));
                            il.Emit(OpCodes.Stloc, locMIMethod);

                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "GotOurMethod");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }

                            il.Emit(OpCodes.Ldloc, locMIMethod);
                            il.Emit(OpCodes.Callvirt, typeof(MethodInfo).GetMethod("GetParameters"));
                            il.Emit(OpCodes.Stloc, locParameterInfoArr);

                            //var argsToPass = new object[parms.length] 
                            il.Emit(OpCodes.Ldloc, locParameterInfoArr);
                            il.Emit(OpCodes.Ldlen);
                            il.Emit(OpCodes.Conv_I4);
                            il.Emit(OpCodes.Newarr, typeof(Object));
                            il.Emit(OpCodes.Stloc, locObjArrArgsToPass);


                            // for (i = 0 ; i < params.length)
                            il.Emit(OpCodes.Ldc_I4_0);
                            il.Emit(OpCodes.Stloc, locIntParmLoopIndex);
                            var labIncParamLoop = il.DefineLabel();
                            var labStartParamLoop = il.DefineLabel();
                            {
                                il.Emit(OpCodes.Br, labIncParamLoop);
                                il.MarkLabel(labStartParamLoop);

                                il.Emit(OpCodes.Ldloc, locParameterInfoArr);
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex);
                                il.Emit(OpCodes.Ldelem_Ref);
                                il.Emit(OpCodes.Callvirt, typeof(ParameterInfo).GetProperty("ParameterType").GetMethod);
                                il.Emit(OpCodes.Callvirt, typeof(Type).GetProperty("Name").GetMethod);
                                il.Emit(OpCodes.Stloc, locStrParameterName);

                                if (logOutput)
                                {
                                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                    il.Emit(OpCodes.Ldloc, locStrParameterName);
                                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                    il.Emit(OpCodes.Pop);
                                }
                                // if (name =="String")
                                il.Emit(OpCodes.Ldloc, locStrParameterName);
                                il.Emit(OpCodes.Ldstr, "String");
                                il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                                var labNotString = il.DefineLabel();
                                il.Emit(OpCodes.Brfalse, labNotString);

                                // obj[i] = args[i+argOffset]
                                il.Emit(OpCodes.Ldloc, locObjArrArgsToPass); //obj[]
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex); //i
                                il.Emit(OpCodes.Ldarg_0); // targargs
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex); //i
                                il.Emit(OpCodes.Ldc_I4, argOffset);
                                il.Emit(OpCodes.Add);
                                il.Emit(OpCodes.Ldelem_Ref);
                                il.Emit(OpCodes.Stelem_Ref);
                                var labContParmLoop = il.DefineLabel();
                                il.Emit(OpCodes.Br, labContParmLoop);

                                il.MarkLabel(labNotString);
                                // if(name == "Int32")
                                il.Emit(OpCodes.Ldloc, locStrParameterName);
                                il.Emit(OpCodes.Ldstr, "Int32");
                                il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                                var labNotInt32 = il.DefineLabel();
                                il.Emit(OpCodes.Brfalse, labNotInt32);

                                // obj[i]=int.Parse(args[i+argOffset])
                                il.Emit(OpCodes.Ldloc, locObjArrArgsToPass); //obj[]
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex); //i
                                il.Emit(OpCodes.Ldarg_0); // targargs
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex); //i
                                il.Emit(OpCodes.Ldc_I4, argOffset);
                                il.Emit(OpCodes.Add);
                                il.Emit(OpCodes.Ldelem_Ref);
                                il.Emit(OpCodes.Call, typeof(Int32).GetMethod("Parse", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Box, typeof(Int32));
                                il.Emit(OpCodes.Stelem_Ref);
                                il.Emit(OpCodes.Br, labContParmLoop);

                                il.MarkLabel(labNotInt32);
                                // if(name == "Boolean")
                                il.Emit(OpCodes.Ldloc, locStrParameterName);
                                il.Emit(OpCodes.Ldstr, "Boolean");
                                il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", new Type[] { typeof(string), typeof(string) }));
                                il.Emit(OpCodes.Brfalse, labContParmLoop);

                                // obj[i]=bool.Parse(args[i+argOffset])
                                il.Emit(OpCodes.Ldloc, locObjArrArgsToPass); //obj[]
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex); //i
                                il.Emit(OpCodes.Ldarg_0); // targargs
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex); //i
                                il.Emit(OpCodes.Ldc_I4, argOffset);
                                il.Emit(OpCodes.Add);
                                il.Emit(OpCodes.Ldelem_Ref);
                                il.Emit(OpCodes.Call, typeof(Boolean).GetMethod("Parse", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Box, typeof(Boolean));
                                il.Emit(OpCodes.Stelem_Ref);


                                il.MarkLabel(labContParmLoop);
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex);
                                il.Emit(OpCodes.Ldc_I4_1);
                                il.Emit(OpCodes.Add);
                                il.Emit(OpCodes.Stloc, locIntParmLoopIndex);

                                il.MarkLabel(labIncParamLoop);
                                il.Emit(OpCodes.Ldloc, locIntParmLoopIndex);
                                il.Emit(OpCodes.Ldloc, locParameterInfoArr);
                                il.Emit(OpCodes.Ldlen);
                                il.Emit(OpCodes.Conv_I4);
                                il.Emit(OpCodes.Clt); // compare if <. Pushes 1  else 0
                                il.Emit(OpCodes.Brtrue, labStartParamLoop);
                            }

                            if (logOutput)
                            {
                                // before we invoke, we need to flush our log
                                il.Emit(OpCodes.Ldsfld, statLogOutputFile);
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("ToString", new Type[0]));
                                il.Emit(OpCodes.Call, typeof(File).GetMethod("AppendAllText", new Type[] { typeof(string), typeof(string) }));

                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("Clear"));
                                il.Emit(OpCodes.Pop);
                            }

                            // if (!type.IsAbstract) // if it's not static we need to instantiate
                            il.Emit(OpCodes.Ldloc, 5); // type
                            il.Emit(OpCodes.Callvirt, typeof(Type).GetProperty("IsAbstract").GetMethod);
                            var labIsStatic = il.DefineLabel();
                            il.Emit(OpCodes.Brtrue, labIsStatic);
                            {
                                if (logOutput)
                                {
                                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                    il.Emit(OpCodes.Ldstr, "isNotStatic");
                                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                    il.Emit(OpCodes.Pop);
                                }

                                il.Emit(OpCodes.Ldloc, locTypeCurrent); // type
                                il.Emit(OpCodes.Call, typeof(Activator).GetMethod("CreateInstance", new Type[] { typeof(Type) }));
                                il.Emit(OpCodes.Stloc, locObjInstance);
                            }

                            il.MarkLabel(labIsStatic);

                            //methCollectDump.Invoke(memdumpHelper, argsToPass);
                            il.Emit(OpCodes.Ldloc, locMIMethod); // method
                            il.Emit(OpCodes.Ldloc, locObjInstance); // instance
                            il.Emit(OpCodes.Ldloc, locObjArrArgsToPass); // args
                            il.Emit(OpCodes.Callvirt, typeof(MethodBase).GetMethod("Invoke", new Type[] { typeof(object), typeof(object[]) }));
                            il.Emit(OpCodes.Pop);

                            if (logOutput)
                            {
                                il.Emit(OpCodes.Ldsfld, statStringBuilder);
                                il.Emit(OpCodes.Ldstr, "back from call");
                                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                                il.Emit(OpCodes.Pop);
                            }

                            //break;
                            il.Emit(OpCodes.Br, labBreakLoop);
                        }
                        il.MarkLabel(labNotOurType);

                        // increment count
                        il.Emit(OpCodes.Ldloc, locIntLoopIndex);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Add);
                        il.Emit(OpCodes.Stloc, locIntLoopIndex);

                        il.MarkLabel(labIncLoop);
                        il.Emit(OpCodes.Ldloc, locIntLoopIndex);
                        il.Emit(OpCodes.Ldloc, locTypeArr);
                        il.Emit(OpCodes.Ldlen);
                        il.Emit(OpCodes.Conv_I4);
                        il.Emit(OpCodes.Blt, labStartLoop);
                    }
                    il.MarkLabel(labBreakLoop);
                }
                var labAfterExceptionBlock = il.DefineLabel();
                il.BeginCatchBlock(typeof(ReflectionTypeLoadException));
                {
                    il.Emit(OpCodes.Call, typeof(ReflectionTypeLoadException).GetProperty("LoaderExceptions").GetMethod);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ldelem_Ref);
                    il.Emit(OpCodes.Call, typeof(Exception).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Stloc, locStrTemp);
                    il.Emit(OpCodes.Leave, labAfterExceptionBlock);
                }
                il.BeginCatchBlock(typeof(Exception)); // exception is on eval stack
                {
                    il.Emit(OpCodes.Call, typeof(Exception).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Stloc, locStrTemp);

                    if (logOutput)
                    {
                        il.Emit(OpCodes.Ldsfld, statStringBuilder);
                        il.Emit(OpCodes.Call, typeof(DateTime).GetProperty("Now").GetMethod);
                        il.Emit(OpCodes.Stloc, locdtNow);
                        il.Emit(OpCodes.Ldloca, locdtNow);
                        il.Emit(OpCodes.Callvirt, typeof(DateTime).GetMethod("ToString", new Type[0]));
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));

                        il.Emit(OpCodes.Ldstr, "Exception thrown");
                        il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Ldloc, locStrTemp);
                        il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                        il.Emit(OpCodes.Pop);
                    }
                    il.Emit(OpCodes.Leave, labAfterExceptionBlock);
                }
                il.EndExceptionBlock();
                il.MarkLabel(labAfterExceptionBlock);
                if (AsmResolveMethodBuilder != null)
                {
                    //AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
                    il.Emit(OpCodes.Call, typeof(AppDomain).GetProperty("CurrentDomain").GetMethod);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, AsmResolveMethodBuilder);
                    il.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    il.Emit(OpCodes.Callvirt, typeof(AppDomain).GetEvent("AssemblyResolve").GetRemoveMethod());

                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Ldstr, "Asm ResolveEvents events Unsubscribed");
                    il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("AppendLine", new Type[] { typeof(string) }));
                    il.Emit(OpCodes.Pop);
                }
                if (logOutput)
                {
                    il.Emit(OpCodes.Ldsfld, statLogOutputFile);
                    il.Emit(OpCodes.Ldsfld, statStringBuilder);
                    il.Emit(OpCodes.Call, typeof(StringBuilder).GetMethod("ToString", new Type[0]));
                    il.Emit(OpCodes.Call, typeof(File).GetMethod("AppendAllText", new Type[] { typeof(string), typeof(string) }));
                }
                il.MarkLabel(labEnd);
                il.Emit(OpCodes.Ret);
            }
            var type = typeBuilder.CreateType();

            assemblyBuilder.SetEntryPoint(mainMethodBuilder, PEFileKinds.WindowApplication);
            assemblyBuilder.Save($"{typeName}.exe", portableExecutableKinds, imageFileMachine);
            return type;
        }
    }
}
