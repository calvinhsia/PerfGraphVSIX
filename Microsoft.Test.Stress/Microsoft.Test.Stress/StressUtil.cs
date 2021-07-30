using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace Microsoft.Test.Stress
{
    public class StressUtil
    {
        public const string PropNameNumIterations = "NumIterations";
        public const string PropNameCurrentIteration = "IterationNumber"; // range from 0 - #Iter -  1
        public const string PropNameMinimumIteration = "MinimumIteration";
        public const string PropNameListFileResults = "DictListFileResults";
        public const string PropNameMeasurementHolder = "MeasurementHolder";
        internal const string PropNameRecursionPrevention = "RecursionPrevention";
        public const string PropNameVSHandler = "VSHandler";
        public const string PropNameLogger = "Logger";


        /// <summary>
        /// Iterate the test method the desired number of times
        /// The call can be made from the TestInitialize or the beginning of the TestMethod
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="stressUtilOptions">If passed, includes all options: all other optional parameters are ignored</param>
        /// <param name="NumIterations">The number of iterations (defaults to 71)</param>
        public static async Task DoIterationsAsync(
            object test,
            StressUtilOptions stressUtilOptions = null,
            int NumIterations = 71)
        {
            MeasurementHolder measurementHolder = null;
            try
            {
                if (stressUtilOptions == null)
                {
                    stressUtilOptions = new StressUtilOptions()
                    {
                        NumIterations = NumIterations
                    };
                }
                if (!await stressUtilOptions.SetTestAsync(test)) // are we recurring?
                {
                    return;
                }

                measurementHolder = new MeasurementHolder(
                    stressUtilOptions.testContext,
                    stressUtilOptions,
                    SampleType.SampleTypeIteration);

                var baseDumpFileName = string.Empty;
                stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = 0;
                stressUtilOptions.testContext.Properties[PropNameMeasurementHolder] = measurementHolder;
                var utilFileName = typeof(StressUtil).Assembly.Location;
                var verInfo = FileVersionInfo.GetVersionInfo(utilFileName);
                /*
InternalName:     Microsoft.Test.Stress.dll
OriginalFilename: Microsoft.Test.Stress.dll
FileVersion:      1.1.29.55167
FileDescription:  Microsoft.Test.Stress
Product:          Microsoft.Test.Stress
ProductVersion:   1.1.29+g7fd76485e3
Debug:            False
Patched:          False
PreRelease:       False
PrivateBuild:     False
SpecialBuild:     False
Language:         Language Neutral
                 */
                stressUtilOptions.logger.LogMessage($"{utilFileName} {verInfo.OriginalFilename}  FileVersion:{verInfo.FileVersion}  ProductVesion:{verInfo.ProductVersion}");
                measurementHolder.dictTelemetryProperties["StressLibVersion"] = verInfo.FileVersion;

                for (int iteration = 0; iteration < stressUtilOptions.NumIterations; iteration++)
                {
                    if (stressUtilOptions.actExecuteBeforeEveryIterationAsync != null)
                    {
                        await stressUtilOptions.actExecuteBeforeEveryIterationAsync(iteration + 1, measurementHolder);
                    }
                    var result = stressUtilOptions._theTestMethod.Invoke(test, parameters: null);
                    if (stressUtilOptions._theTestMethod.ReturnType.Name == "Task")
                    {
                        var resultTask = (Task)result;
                        await resultTask;
                    }

                    var res = await measurementHolder.TakeMeasurementAsync($"Iter {iteration + 1,3}/{stressUtilOptions.NumIterations}", DoForceGC: true);
                    stressUtilOptions.logger.LogMessage(res);
                    stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameCurrentIteration]) + 1;
                }
                // note: if a leak is found an exception will be throw and this will not get called
                // increment one last time, so test methods can check for final execution after measurements taken
                stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameCurrentIteration]) + 1;
                DoIterationsFinished(measurementHolder, exception: null);

            }
            catch (Exception ex)
            {
                DoIterationsFinished(measurementHolder, ex);
                throw;
            }
        }

        private static void DoIterationsFinished(MeasurementHolder measurementHolder, Exception exception)
        {
            if (measurementHolder != null)
            {
                if (measurementHolder.testContext != null)
                {
                    measurementHolder.testContext.Properties[PropNameMeasurementHolder] = null;
                }
                if (exception != null)
                {
                    measurementHolder.Logger.LogMessage(exception.ToString());
                    if (exception is LeakException)
                    {
                        measurementHolder.dictTelemetryProperties["LeakException"] = exception.Message;
                    }
                    else
                    {
                        measurementHolder.dictTelemetryProperties["TestException"] = exception.ToString();
                    }
                }
                measurementHolder.dictTelemetryProperties["TestPassed"] = exception == null;
                measurementHolder.Dispose(); // write test results
            }
        }


        /// <summary>
        /// Iterate the test method the desired number of times and with the specified options.
        /// The call must be made after preparing the target process(es) for iteration.
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="stressUtilOptions">If passed, includes all options: all other optional parameters are ignored</param>
        /// <param name="numIterations">The number of iterations (defaults to 71)</param>
        public static void DoIterations(object test, StressUtilOptions stressUtilOptions = null, int numIterations = 71)
        {
            AsyncPump.Run(async () =>
            {
                await DoIterationsAsync(test, stressUtilOptions, numIterations);
            });
        }

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
        public static IVSHandler CreateVSHandler(ILogger logger, int delayMultiplier = 1)
        {
            var vsHandlerFileName = "VSHandler" + (IntPtr.Size == 8 ? "64" : "32") + ".dll";
            var dirVSHandler = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Path.GetFileNameWithoutExtension(vsHandlerFileName));
            Directory.CreateDirectory(dirVSHandler); //succeeds if it exists already
            vsHandlerFileName = Path.Combine(dirVSHandler, vsHandlerFileName); //now full path
            if (!File.Exists(vsHandlerFileName))
            {
                //                var zipVSHandlerRes = IntPtr.Size == 8 ? Properties.Resources.VSHandler64 : Properties.Resources.VSHandler32;
                var resName = Path.GetFileNameWithoutExtension(vsHandlerFileName) + ".zip";
                var zipVSHandlerRes = GetResource(resName);
                var tempZipFile = Path.Combine(dirVSHandler, resName);
                File.WriteAllBytes(tempZipFile, zipVSHandlerRes);
                logger?.LogMessage($"Extracting zip {tempZipFile}");
                using (var archive = ZipFile.Open(tempZipFile, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var ndx = entry.FullName.IndexOf('/'); // subdir separator == '/'
                        string destfilename;
                        if (ndx > 0)
                        {
                            var subfolder = entry.FullName.Substring(0, ndx);
                            Directory.CreateDirectory(Path.Combine(dirVSHandler, subfolder));
                            destfilename = Path.Combine(dirVSHandler, subfolder, entry.Name);
                        }
                        else
                        {
                            destfilename = Path.Combine(dirVSHandler, entry.Name);
                        }
                        if (!File.Exists(destfilename) || new FileInfo(destfilename).LastWriteTime != entry.LastWriteTime)
                        {
                            entry.ExtractToFile(destfilename, overwrite: true);
                        }
                    }
                }
            }
            logger?.LogMessage($"Found VSHandler at {vsHandlerFileName}");
            Assembly asm = Assembly.LoadFrom(vsHandlerFileName);
            var typ = asm.GetType("Microsoft.Test.Stress.VSHandler");
            var vsHandler = (IVSHandler)Activator.CreateInstance(typ);
            vsHandler.Initialize(logger, delayMultiplier);
            var _additionalDirs = dirVSHandler;
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
        public static byte[] GetResource(string resourceName)
        {
            resourceName = $"Microsoft.Test.Stress.Resources.{resourceName}";
            //            return Properties.Resources.ClrObjExplorer; //keeps giving exception Could not load file or assembly 'Microsoft.Test.Stress.resources, Version=1.1.0.0, Culture=en-US, PublicKeyToken=207cdcbbae19dd71' or one of its dependencies. The system cannot find the file specified.
            var strm = typeof(StressUtil).Assembly.GetManifestResourceStream(resourceName);
            if (strm == null)
            {
                var resnames = typeof(StressUtil).Assembly.GetManifestResourceNames();
                throw new Exception($"Resource '{resourceName}' not found.\n Valid resources are " + string.Join(",", resnames));
            }
            using (var reader = new StreamReader(strm))
            {
                using (var ms = new MemoryStream())
                {
                    reader.BaseStream.CopyTo(ms);
                    var d = ms.ToArray();
                    //                    return Properties.Resources.ClrObjExplorer; keeps giving exception Could not load file or assembly 'Microsoft.Test.Stress.resources, Version=1.1.0.0, Culture=en-US, PublicKeyToken=207cdcbbae19dd71' or one of its dependencies. The system cannot find the file specified.

                    return d;
                }
            }
        }
    }
    public class LeakException : Exception
    {
        public List<LeakAnalysisResult> lstLeakResults;

        public LeakException(string message, List<LeakAnalysisResult> lstRegResults) : base(message)
        {
            this.lstLeakResults = lstRegResults;
        }
    }

}
