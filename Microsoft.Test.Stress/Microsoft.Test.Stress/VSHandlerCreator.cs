using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class VSHandlerCreator
    {
        private bool _addedAsmResolveHandler;

        /// <summary>
        /// Need a VS Handler that's built against the right VSSdk for ENVDTE interop (Dev17, Dev16)
        /// Problem: We want one "Microsoft.Stress.Test.dll" but it needs to work for both 32 and 64 bits. This means depending on 2 different SDKs Dev16 and Dev17.
        /// Solution: factor out VSHandler, create a VSHandler32 and VSHandler64, each dependant on their own SDKs. Also cannot have "Microsoft.Test.Stress.dll" reference VSHandler* at all
        ///   so we use Reflection to instantiate and cast to the desired interface.
        /// 
        /// Problem: VSHandler depends on Microsoft.Stress.Test.dll, so needs to be built after, but also needs to be published in the Microsoft.Stress.Test.nuspec
        /// Solution: remove the dependency from VSHandler to Microsoft.Stress.Test.dll by putting the definitions in another assembly on which both depend
        /// Problem: multiple different Test projects in this repo need to get the VSHandler.
        /// One solution is to Xcopy the VSHandler to each test dir. (yuk)
        /// Other way is to search up folders for the VSHandler output under Microsoft.Test.Stress folder.
        /// Problem: Tests (not in this repo) need to get the VSHandler via Nuget
        /// Solution: modify the nuspec.
        /// </summary>
        public IVSHandler CreateVSHandler(ILogger logger, int delayMultiplier = 1)
        {
            var vsHandlerFileName = "VSHandler" + (IntPtr.Size == 8 ? "64" : "32") + ".dll";
            var dirVSHandler = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Path.GetFileNameWithoutExtension(vsHandlerFileName));

            vsHandlerFileName = Path.Combine(dirVSHandler, vsHandlerFileName); //now full path
            var resourceName = Path.GetFileNameWithoutExtension(vsHandlerFileName) + ".zip";

            var zipFile = Path.Combine(dirVSHandler, resourceName);
            if (!Directory.Exists(dirVSHandler) ||
                !File.Exists(vsHandlerFileName) ||
                !File.Exists(zipFile) ||
                (new FileInfo(vsHandlerFileName).LastWriteTime != new FileInfo(zipFile).LastWriteTime))
            {
                ZipUtil.UnzipResource(resourceName, dirVSHandler);
            }
            logger?.LogMessage($"Found VSHandler at {vsHandlerFileName}");
            Assembly asm = Assembly.LoadFrom(vsHandlerFileName);
            var typ = asm.GetType("Microsoft.Test.Stress.VSHandler");
            var vsHandler = (IVSHandler)Activator.CreateInstance(typ);
            vsHandler.Initialize(logger, delayMultiplier);
            var _additionalDirs = dirVSHandler;
            if (!_addedAsmResolveHandler)
            {
                _addedAsmResolveHandler = true;
                AppDomain.CurrentDomain.AssemblyResolve += (o, e) =>
                {
                    Assembly asmResolved = null;
                    var requestName = e.Name.Substring(0, e.Name.IndexOf(",")) + ".dll"; // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
                    var split = _additionalDirs.Split(new[] { ';' });
                    foreach (var dir in split)
                    {
                        var trypath = Path.Combine(dir, requestName);
                        if (File.Exists(trypath))
                        {
                            asmResolved = Assembly.LoadFrom(trypath);
                            if (asmResolved != null)
                            {
                                break;
                            }
                        }
                    }
                    return asmResolved;
                };
            }
            return vsHandler;

#if false
            var thisasmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); // C:\Users\calvinh\source\repos\Stress\TestStress\bin\Debug
            var seg = IntPtr.Size == 8 ? "64" : "32";
            var partPath = Path.Combine(
                $"VSHandler{seg}",
                $"VSHandler{seg}.dll");
            var pathHandler = Path.Combine( // in subfolder called VSHandler32\VSHandler32.dll so dependent assemblies are separate for 32 and 64 bit
                thisasmDir, partPath);
            // sometimes tests run from TestDeployment folder (no bin\debug)
            // "C:\Users\calvinh\source\repos\Stress\TestResults\Deploy_calvinh 2021-07-08 14_02_38\Out\Microsoft.Test.Stress.dll"
            // C:\Users\calvinh\source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\Deploy_calvinh 2021-07-08 14_02_38\Out\VSHandler32\VSHandler32.dll
            if (!File.Exists(pathHandler))
            {
                logger?.LogMessage($"Could not find {pathHandler} . Searching for built folder");
                // could be repo tests, so we find them in sibling folder
                var pathParts = thisasmDir.Split(Path.DirectorySeparatorChar);
                var targetfolder = @"Microsoft.Test.Stress";

                if (pathParts.Length > 7)
                {
                    var upone = Path.GetDirectoryName(thisasmDir);
                    int max = 4;
                    while (max > 0)
                    {
                        if (Directory.Exists(Path.Combine(upone, targetfolder)))
                        {
                            // "C:\Users\calvinh\source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\bin\Debug\VSHandler32\VSHandler32.dll"
                            if (pathParts[pathParts.Length - 2] == "bin")
                            {
                                pathHandler = Path.Combine(upone, targetfolder, targetfolder, pathParts[pathParts.Length - 2], pathParts[pathParts.Length - 1], partPath); // add "bin\Debug" 
                            }
                            else
                            {
                                pathHandler = Path.Combine(upone, targetfolder, targetfolder, @"bin\debug", partPath); // add "bin\Debug" 
                            }
                            break;
                        }
                        upone = Path.GetDirectoryName(upone);
                        max--;
                    }
                }
                if (!File.Exists(pathHandler))
                {
                    throw new FileNotFoundException(pathHandler);
                }
            }
            logger?.LogMessage($"Found VSHandler at {pathHandler}");
            Assembly asm = Assembly.LoadFrom(pathHandler);
            var typ = asm.GetType("Microsoft.Test.Stress.VSHandler");
            var vsHandler = (IVSHandler)Activator.CreateInstance(typ);
            vsHandler.Initialize(logger, delayMultiplier);
            return vsHandler;
#endif
        }
    }
}
