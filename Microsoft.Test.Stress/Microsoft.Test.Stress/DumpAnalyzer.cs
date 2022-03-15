//using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Resources;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class DumpAnalyzer
    {
        /// <summary>
        /// Encapsulates memory profiling data for a specific type or set of types.
        /// </summary>
        public sealed class TypeStatistics
        {
            /// <summary>
            /// The overall size of all exclusively retained objects in bytes.
            /// </summary>
            public ulong ExclusiveRetainedBytes;

            /// <summary>
            /// The overall size of all retained objects in bytes, including objects also rooted by something
            /// else than only our types of interest.
            /// </summary>
            public ulong InclusiveRetainedBytes;

            /// <summary>
            /// Measures the time taken to calculate the profiling data.
            /// </summary>
            public Stopwatch MemoryProfilingStopwatch = new Stopwatch();
        }


        public class DumpDataAnalysisResult
        {
            public DumpDataAnalysisResult()
            {
                dictTypes = new Dictionary<string, int>();
                dictStrings = new Dictionary<string, int>();
                typeStatistics = null;
            }
            public Dictionary<string, int> dictTypes; // typename, count
            public Dictionary<string, int> dictStrings; // clr string, count
            public TypeStatistics typeStatistics;
        }

        private readonly ILogger logger;
        public string ClrObjExplorerExe = string.Empty;
        public DumpAnalyzer(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Given a dump file, output 2 dictionaries: CLR Type name=>Count   and String=>Count
        /// e.g. if the dump has 44 copies of the string "foobar", then dictStrings["foobar"]=44
        ///    if the dump has 12 instances of Microsoft.VisualStudio.Type.Foobar, then  dictTypes["Microsoft.VisualStudio.Type.Foobar"] =12
        /// Also outputs memory statistics on types matching typesToReportStatisticsOn if typesToReportStatisticsOn is not null.
        /// </summary>
        /// <param name="dumpFile"></param>
        /// <param name="typesToReportStatisticsOn"></param>
        public DumpDataAnalysisResult AnalyzeDump(string dumpFile, string typesToReportStatisticsOn)
        {
            //  "C:\Users\calvinh\AppData\Local\Temp\VSDbg\ClrObjExplorer\ClrObjExplorer.exe" 
            //  /s \\calvinhw8\c$\Users\calvinh\Documents;srv*C:\Users\calvinh\AppData\Local\Temp\Symbols*;\\ddelementary\public\CalvinH\VsDbgTestDumps\VSHeapAllocDetourDump;\\ddrps\symbols;http://symweb/ m "\\calvinhw8\c$\Users\calvinh\Documents\devenvNav2files700.dmp"
            //            var symPath = @"http://symweb";
            var dumpDataAnalysisResult = new DumpDataAnalysisResult();

            Regex typesToReportStatisticsOnRegex = null;
            if (!string.IsNullOrEmpty( typesToReportStatisticsOn))
            {
                typesToReportStatisticsOnRegex = new Regex(typesToReportStatisticsOn, RegexOptions.Compiled);
                dumpDataAnalysisResult.typeStatistics = new TypeStatistics();
            }
            else
            {
                dumpDataAnalysisResult.typeStatistics = null;
            }

            try
            {
                //                logger.LogMessage($"in {nameof(AnalyzeDump)} {dumpFile}");
                using (var dataTarget = DataTarget.LoadDump(dumpFile))
                {
                    if (dataTarget.ClrVersions.Length != 1)
                    {
                        throw new InvalidOperationException($"Expected 1 ClrVersion in process. Found {dataTarget.ClrVersions.Length} ");
                    }
                    var clrver = dataTarget.ClrVersions[0];
                    var dacFileName = string.Empty;
                    try
                    {
                        dacFileName = dataTarget.BinaryLocator.FindBinary(clrver.DacInfo.PlatformSpecificFileName, clrver.DacInfo.IndexTimeStamp, clrver.DacInfo.IndexFileSize);
                        if (string.IsNullOrEmpty(dacFileName))
                        {
                            dacFileName = clrver.DacInfo.LocalDacPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        IOException ioException = null;
                        if (ex is AggregateException aggregateException)
                        {
                            if (ex.InnerException is IOException exception)
                            {
                                ioException = exception;
                            }
                        }
                        if (ex is IOException ioexception)
                        {
                            ioException = ioexception;
                        }
                        if (ioException == null)
                        {
                            throw;
                        }
                        // System.IO.IOException: The process cannot access the file 'C:\Users\calvinh\AppData\Local\Temp\symbols\mscordacwks_x86_x86_4.6.26.00.dll\54c2e0d969b000\mscordacwks_x86_x86_4.6.26.00.dll' because it is being used by another process.
                        var m = Regex.Match(ioException.Message, @".*'(.*)'.*");
                        if (m.Success)
                        {
                            dacFileName = m.Groups[1].Value;
                        }
                        else
                        {
                            throw;
                        }
                    }
                    if (string.IsNullOrEmpty(dacFileName))
                    {
                        throw new InvalidOperationException($"Could not create or find dacFile");
                    }
                    logger?.LogMessage($"Got Dac {dacFileName}");
                    var runtime = dataTarget.ClrVersions[0].CreateRuntime();
                    logger?.LogMessage($"Got runtime {runtime}");
                    var nObjCount = 0;
                    var lstStrings = new List<string>();
                    var markedObjects = new HashSet<ulong>();
                    foreach (var obj in EnumerateRootedObjects(runtime.Heap))
                    {
                        var typ = obj.Type?.Name;
                        if (typ == "System.String")
                        {
                            lstStrings.Add(obj.AsString());
                        }
                        if (!dumpDataAnalysisResult.dictTypes.ContainsKey(typ))
                        {
                            dumpDataAnalysisResult.dictTypes[typ] = 1;
                        }
                        else
                        {
                            dumpDataAnalysisResult.dictTypes[typ]++;
                        }
                        nObjCount++;

                        if (typesToReportStatisticsOnRegex?.IsMatch(typ) == true)
                        {
                            CalculateTypeStatisticsPhase1(obj, typesToReportStatisticsOnRegex, markedObjects, dumpDataAnalysisResult.typeStatistics);
                        }
                    }
                    if (typesToReportStatisticsOnRegex != null)
                    {
                        // Phase 1 calculated the total retained bytes. Now figure out which of the marked objects are rooted by
                        // objects of other types as well to calculate the exclusive retained bytes.
                        CalculateTypeStatisticsPhase2(runtime.Heap, typesToReportStatisticsOnRegex, markedObjects, dumpDataAnalysisResult.typeStatistics);
                    }

                    logger?.LogMessage($"Total Object Count = {nObjCount:n0} TypeCnt = {dumpDataAnalysisResult.dictTypes.Count} {dumpFile}");
                    foreach (var strValue in lstStrings)
                    {
                        if (!dumpDataAnalysisResult.dictStrings.ContainsKey(strValue))
                        {
                            dumpDataAnalysisResult.dictStrings[strValue] = 1;
                        }
                        else
                        {
                            dumpDataAnalysisResult.dictStrings[strValue]++;
                        }
                        //                        logger.LogMessage($"STR {strValue}");
                    }

                    //foreach (var entry in dictTypes.OrderByDescending(kvp => kvp.Value))
                    //{
                    //    logger.LogMessage($"  {entry.Value,10:n0}  {entry.Key}");
                    //}
                }

                //var dataTarget = DataTarget.AttachToProcess(_dumperViewer._procTarget.Id, msecTimeout: 5000, AttachFlag.NonInvasive);
                //_dumperViewer.LogMessage($"Got dt {dataTarget}");
                //var runtime = dataTarget.ClrVersions[0].CreateRuntime();
                //_dumperViewer.LogMessage($"Got runtime {runtime}");
            }
            catch (Exception ex)
            {
                logger?.LogMessage($"Exception analyzing dump {ex}");
            }
            return dumpDataAnalysisResult;
        }
        /// <summary>
        /// given 2 dumps and a stringbuilder, add to the stringbuilder the diffs in both the ClrTypes and the Strings
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="pathDumpBase"></param>
        /// <param name="pathDumpCurrent"></param>
        /// <param name="TotNumIterations"></param>
        /// <param name="NumIterationsBeforeTotalToTakeBaselineSnapshot"></param>
        /// <param name="typesToReportStatisticsOn"></param>
        /// <param name="baselineTypeStatistics"></param>
        /// <param name="currentTypeStatistics"></param>
        public void GetDiff(
            StringBuilder sb,
            string pathDumpBase,
            string pathDumpCurrent,
            int TotNumIterations,
            int NumIterationsBeforeTotalToTakeBaselineSnapshot,
            string typesToReportStatisticsOn,
            out TypeStatistics baselineTypeStatistics,
            out TypeStatistics currentTypeStatistics)
        {
            _dictTypeDiffs = new Dictionary<string, Tuple<int, int>>();
            _dictStringDiffs = new Dictionary<string, Tuple<int, int>>();
            baselineTypeStatistics = null;
            currentTypeStatistics = null;
            var baselineResult = AnalyzeDump(pathDumpBase, typesToReportStatisticsOn);
            var currentResult = AnalyzeDump(pathDumpCurrent, typesToReportStatisticsOn);
            sb.AppendLine($"2 dumps were made: 1 at iteration # {TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot}, the other after iteration {TotNumIterations}");
            if (baselineResult.typeStatistics != null && currentResult.typeStatistics != null)
            {
                baselineTypeStatistics = baselineResult.typeStatistics;
                currentTypeStatistics = currentResult.typeStatistics;
                sb.AppendLine($"Statistics on types matching '{ typesToReportStatisticsOn }'");
                sb.AppendLine($"Inclusive retained bytes 1st/2nd dump: { baselineResult.typeStatistics.InclusiveRetainedBytes }/{ currentResult.typeStatistics.InclusiveRetainedBytes }");
                sb.AppendLine($"Exclusive retained bytes 1st/2nd dump: { baselineResult.typeStatistics.ExclusiveRetainedBytes }/{ currentResult.typeStatistics.ExclusiveRetainedBytes }");
                sb.AppendLine($"Retained bytes took { baselineResult.typeStatistics.MemoryProfilingStopwatch.Elapsed.Seconds } seconds to calculate in 1st dump, { currentResult.typeStatistics.MemoryProfilingStopwatch.Elapsed.Seconds } seconds in 2nd dump");
            }
            sb.AppendLine($"Below are 2 lists: the counts of Types and Strings in each dump. The 1st column is the number in the 1st dump, the 2nd is the number found in the 2nd dump and the 3rd column is the Type or String");
            sb.AppendLine($"For example if # iterations  = 11, 2 dumps are taken after iterations 7 and 11., '17  56  System.Guid' means there were 17 instances of System.Guid in the 1st dump and 56 in the 2nd");
            sb.AppendLine($"TypesAndStrings { Path.GetFileName(pathDumpBase)} {Path.GetFileName(pathDumpCurrent)}  {nameof(NumIterationsBeforeTotalToTakeBaselineSnapshot)}= {NumIterationsBeforeTotalToTakeBaselineSnapshot}");
            sb.AppendLine();

            sb.AppendLine("Types:");
            AnalyzeDiffInDicts(baselineResult.dictTypes, currentResult.dictTypes, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot,
                (key, baseCnt, currentCnt) =>
                {
                    _dictTypeDiffs[key] = Tuple.Create(baseCnt, currentCnt);
                    var msg = string.Format("{0,5} {1,5} {2}", baseCnt, currentCnt, key); // can't use "$" because can contain embedded "{"
                    sb.AppendLine(msg);
                });
            sb.AppendLine();
            sb.AppendLine("Strings:");
            AnalyzeDiffInDicts(baselineResult.dictStrings, currentResult.dictStrings, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot,
                (key, baseCnt, currentCnt) =>
                {
                    _dictStringDiffs[key] = Tuple.Create(baseCnt, currentCnt);
                    var msg = string.Format("{0,5} {1,5} {2}", baseCnt, currentCnt, key); // can't use "$" because can contain embedded "{"
                    sb.AppendLine(msg);
                });
            logger.LogMessage($"analyzed types and strings {pathDumpBase} {pathDumpCurrent}");
            //            var fname = DumperViewerMain.GetNewFileName(measurementHolder.TestName, "");
        }

        /// <summary>
        /// Given a stringbuilder and 2 dictionaries of the same type(e.g. 2 string->count dicts, or 2 ClrType->count dicts), but from 2 different iterations (base and current), 
        /// add to the stringbuilder the growth in the Type (or string)
        /// </summary>
        public void AnalyzeDiffInDicts(
            Dictionary<string, int> dictBase,
            Dictionary<string, int> dictCurrent,
            int TotNumIterations,
            int NumIterationsBeforeTotalToTakeBaselineSnapshot,
            Action<string, int, int> actionDiff)
        {
            foreach (var entryCurrent in dictCurrent
                .Where(e => e.Value >= TotNumIterations)// there must be at least NumIterations
                .OrderBy(e => e.Value))
            {
                if (dictBase.TryGetValue(entryCurrent.Key, out var baseCnt)) // if it's also in the basedump
                {
                    if (baseCnt >= TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot) // base must have grown at least 1 per iteration
                    {
                        if (baseCnt + NumIterationsBeforeTotalToTakeBaselineSnapshot <= entryCurrent.Value) // value has increased by at least 1 per iteration
                        {
                            actionDiff(entryCurrent.Key, baseCnt, entryCurrent.Value);
                        }
                    }
                }
            }
        }

        public Dictionary<string, Tuple<int, int>> _dictEventHandlerDiffs;
        public Dictionary<string, Tuple<int, int>> _dictTypeDiffs;
        public Dictionary<string, Tuple<int, int>> _dictStringDiffs;

        public string GetClrObjExplorerPath()
        {
            try
            {
                if (string.IsNullOrEmpty(ClrObjExplorerExe))
                {
                    var clrObjDir = Path.Combine(
                        Path.GetDirectoryName(this.GetType().Assembly.Location),
                        @"ClrObjExplorer");
                    ZipUtil.UnzipResource("ClrObjExplorer.zip", clrObjDir);

                    //                ZipFile.ExtractToDirectory(tempZipFile, clrObjDir);
                    ClrObjExplorerExe = Path.Combine(clrObjDir, "ClrObjExplorer" + (IntPtr.Size == 8 ? "64" : string.Empty) + ".exe");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());
                // use existing ClrObjExplorer: user may have left it open examining a prior trace (Unauth exce if in use) or it could be 
            }
            //                logger.LogMessage($"Found ClrObjExplorer at {_ClrObjExplorerExe }");
            return ClrObjExplorerExe;
        }

        public void StartClrObjExplorer(string _DumpFileName)
        {
            var args = string.Empty;
            if (!string.IsNullOrEmpty(_DumpFileName))
            {
                args = $"/m \"{_DumpFileName}\"";
            }
            System.Diagnostics.Process.Start(GetClrObjExplorerPath(), args);
        }

        /// <summary>
        /// Enumerates all objects reachable from GC roots in the given heap. Each object is returned exactly once.
        /// </summary>
        private IEnumerable<ClrObject> EnumerateRootedObjects(ClrHeap heap)
        {
            HashSet<ulong> visitedObjects = new HashSet<ulong>();
            Queue<ClrObject> objectQueue = new Queue<ClrObject>();

            foreach (IClrRoot root in heap.EnumerateRoots())
            {
                if (!visitedObjects.Contains(root.Object))
                {
                    ClrObject rootObj = heap.GetObject(root.Object);
                    objectQueue.Enqueue(rootObj);
                    visitedObjects.Add(root.Object);
                    if (rootObj.Type != null)
                    {
                        yield return rootObj;
                    }

                    while (objectQueue.Count > 0)
                    {
                        ClrObject obj = objectQueue.Dequeue();
                        if (obj.IsNull || obj.Type == null)
                        {
                            continue;
                        }
                        // Follow all references.
                        foreach (var reference in obj.EnumerateReferences())
                        {
                            if (!reference.IsNull && !visitedObjects.Contains(reference.Address))
                            {
                                objectQueue.Enqueue(reference);
                                visitedObjects.Add(reference.Address);
                                yield return reference;
                            }
                        }
                    }
                }
            }
        }

        private void CalculateTypeStatisticsPhase1(ClrObject rootObj, Regex typesToReportStatisticsOnRegex, HashSet<ulong> markedObjects, TypeStatistics typeStatistics)
        {
            if (!markedObjects.Contains(rootObj.Address))
            {
                // Start the stopwatch only if we're looking at an unmarked object. Measuring the single HashSet.Contains() would have more
                // overhead than the actual duration of the operation.
                typeStatistics.MemoryProfilingStopwatch.Start();

                Queue<ClrObject> objectQueue = new Queue<ClrObject>();
                objectQueue.Enqueue(rootObj);
                markedObjects.Add(rootObj.Address);

                while (objectQueue.Count > 0)
                {
                    ClrObject obj = objectQueue.Dequeue();
                    typeStatistics.InclusiveRetainedBytes += obj.Size;

                    foreach (var reference in obj.EnumerateReferences())
                    {
                        if (!reference.IsNull && !markedObjects.Contains(reference.Address))
                        {
                            // We follow references only as long as they point to BCL types or to types to report statistics on.
                            // Otherwise we would transitively walk pretty much the entire managed heap and the number we report
                            // would be meaningless.
                            string referenceType = reference.Type.Name;
                            if (referenceType.StartsWith("System.") || typesToReportStatisticsOnRegex.IsMatch(referenceType))
                            {
                                markedObjects.Add(reference.Address);
                                objectQueue.Enqueue(reference);
                            }
                        }
                    }
                }

                typeStatistics.MemoryProfilingStopwatch.Stop();
            }
        }

        private void CalculateTypeStatisticsPhase2(ClrHeap heap, Regex typesToReportStatisticsOnRegex, HashSet<ulong> markedObjects, TypeStatistics typeStatistics)
        {
            typeStatistics.MemoryProfilingStopwatch.Start();

            HashSet<ulong> visitedObjects = new HashSet<ulong>();
            Queue<ClrObject> objectQueue = new Queue<ClrObject>();

            // Start with exclusive = inclusive and walk the heap from roots looking for objects to subtract from this number.
            typeStatistics.ExclusiveRetainedBytes = typeStatistics.InclusiveRetainedBytes;

            foreach (IClrRoot root in heap.EnumerateRoots())
            {
                // Interested only in roots outside of our marked inclusive graph.
                if (!markedObjects.Contains(root.Object))
                {
                    ClrObject rootObj = heap.GetObject(root.Object);
                    objectQueue.Enqueue(rootObj);
                    visitedObjects.Add(root.Object);

                    while (objectQueue.Count > 0)
                    {
                        ClrObject obj = objectQueue.Dequeue();
                        if (obj.IsNull || obj.Type == null)
                        {
                            continue;
                        }

                        // We stop the walk when we see an object of a type we are reporting statistics on.
                        if (!typesToReportStatisticsOnRegex.IsMatch(obj.Type.Name))
                        {
                            if (markedObjects.Contains(obj.Address))
                            {
                                // Not an object of a type we are reporting statistics on but it is part of the inclusive object graph.
                                // This means that it must not be reported in the exclusive bytes.
                                typeStatistics.ExclusiveRetainedBytes -= obj.Size;
                                markedObjects.Remove(obj.Address);
                            }

                            // Follow all references.
                            foreach (var reference in obj.EnumerateReferences())
                            {
                                if (!reference.IsNull && !visitedObjects.Contains(reference.Address))
                                {
                                    visitedObjects.Add(reference.Address);
                                    objectQueue.Enqueue(reference);
                                }
                            }
                        }
                    }
                }
            }

            typeStatistics.MemoryProfilingStopwatch.Stop();
        }
    }
}
