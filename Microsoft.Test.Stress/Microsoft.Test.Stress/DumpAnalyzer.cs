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
        /// <param name="dictTypes"></param>
        /// <param name="dictStrings"></param>
        /// <param name="typeStatistics"></param>
        public void AnalyzeDump(string dumpFile, string typesToReportStatisticsOn, out Dictionary<string, int> dictTypes,
            out Dictionary<string, int> dictStrings, out TypeStatistics typeStatistics)
        {
            //  "C:\Users\calvinh\AppData\Local\Temp\VSDbg\ClrObjExplorer\ClrObjExplorer.exe" 
            //  /s \\calvinhw8\c$\Users\calvinh\Documents;srv*C:\Users\calvinh\AppData\Local\Temp\Symbols*;\\ddelementary\public\CalvinH\VsDbgTestDumps\VSHeapAllocDetourDump;\\ddrps\symbols;http://symweb/ m "\\calvinhw8\c$\Users\calvinh\Documents\devenvNav2files700.dmp"
            //            var symPath = @"http://symweb";
            dictTypes = new Dictionary<string, int>();
            dictStrings = new Dictionary<string, int>();

            Regex typesToReportStatisticsOnRegex = null;
            if (typesToReportStatisticsOn != null)
            {
                typesToReportStatisticsOnRegex = new Regex(typesToReportStatisticsOn, RegexOptions.Compiled);
                typeStatistics = new TypeStatistics();
            }
            else
            {
                typeStatistics = null;
            }

            try
            {
                //                logger.LogMessage($"in {nameof(AnalyzeDump)} {dumpFile}");
                using (var dataTarget = DataTarget.LoadCrashDump(dumpFile))
                {
                    if (dataTarget.ClrVersions.Count != 1)
                    {
                        throw new InvalidOperationException($"Expected 1 ClrVersion in process. Found {dataTarget.ClrVersions.Count} ");
                    }
                    var dacLocation = dataTarget.ClrVersions[0].LocalMatchingDac;
                    //                    logger.LogMessage($"Got Dac {dacLocation}");
                    var runtime = dataTarget.ClrVersions[0].CreateRuntime();
                    //                  logger.LogMessage($"Got runtime {runtime}");
                    var nObjCount = 0;
                    var lstStrings = new List<ClrObject>();
                    var markedObjects = new HashSet<ulong>();
                    foreach (var obj in EnumerateRootedObjects(runtime.Heap))
                    {
                        var typ = obj.Type.Name;
                        if (typ == "System.String")
                        {
                            lstStrings.Add(obj);
                        }
                        if (!dictTypes.ContainsKey(typ))
                        {
                            dictTypes[typ] = 1;
                        }
                        else
                        {
                            dictTypes[typ]++;
                        }
                        nObjCount++;

                        if (typesToReportStatisticsOnRegex?.IsMatch(typ) == true)
                        {
                            CalculateTypeStatisticsPhase1(obj, typesToReportStatisticsOnRegex, markedObjects, typeStatistics);
                        }
                    }
                    if (typesToReportStatisticsOnRegex != null)
                    {
                        // Phase 1 calculated the total retained bytes. Now figure out which of the marked objects are rooted by
                        // objects of other types as well to calculate the exclusive retained bytes.
                        CalculateTypeStatisticsPhase2(runtime.Heap, typesToReportStatisticsOnRegex, markedObjects, typeStatistics);
                    }

                    logger?.LogMessage($"Total Object Count = {nObjCount:n0} TypeCnt = {dictTypes.Count} {dumpFile}");
                    var maxLength = 1024;
                    var strValue = string.Empty;
                    foreach (var str in lstStrings)
                    {
                        if (str.Type.IsString)
                        {
                            var addrToUse = str.Address + (uint)IntPtr.Size; // skip clsid
                            byte[] buff = new byte[IntPtr.Size];
                            if (runtime.ReadMemory(
                                addrToUse,
                                buff,
                                IntPtr.Size,
                                out var bytesRead
                                ))
                            {
                                var len = BitConverter.ToUInt32(buff, 0);
                                if (maxLength > 0)
                                {
                                    len = Math.Min(len, (uint)maxLength);
                                }
                                buff = new byte[len * 2];
                                if (runtime.ReadMemory(
                                    addrToUse + (uint)IntPtr.Size, // skip clsid, len
                                    buff,
                                    buff.Length,
                                    out bytesRead
                                    ))
                                {
                                    var enc = new UnicodeEncoding();
                                    strValue = enc.GetString(buff, 0, buff.Length);
                                    if (!dictStrings.ContainsKey(strValue))
                                    {
                                        dictStrings[strValue] = 1;
                                    }
                                    else
                                    {
                                        dictStrings[strValue]++;
                                    }
                                }
                            }
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

            AnalyzeDump(pathDumpBase, typesToReportStatisticsOn, out var dictTypesBaseline, out var dictStringsBaseline, out baselineTypeStatistics);
            AnalyzeDump(pathDumpCurrent, typesToReportStatisticsOn, out var dictTypesCurrent, out var dictStringsCurrent, out currentTypeStatistics);
            sb.AppendLine($"2 dumps were made: 1 at iteration # {TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot}, the other after iteration {TotNumIterations}");
            if (baselineTypeStatistics != null && currentTypeStatistics != null)
            {
                sb.AppendLine($"Statistics on types matching '{ typesToReportStatisticsOn }'");
                sb.AppendLine($"Inclusive retained bytes 1st/2nd dump: { baselineTypeStatistics.InclusiveRetainedBytes }/{ currentTypeStatistics.InclusiveRetainedBytes }");
                sb.AppendLine($"Exclusive retained bytes 1st/2nd dump: { baselineTypeStatistics.ExclusiveRetainedBytes }/{ currentTypeStatistics.ExclusiveRetainedBytes }");
                sb.AppendLine($"Retained bytes took { baselineTypeStatistics.MemoryProfilingStopwatch.Elapsed.Seconds } seconds to calculate in 1st dump, { currentTypeStatistics.MemoryProfilingStopwatch.Elapsed.Seconds } seconds in 2nd dump");
            }
            sb.AppendLine($"Below are 2 lists: the counts of Types and Strings in each dump. The 1st column is the number in the 1st dump, the 2nd is the number found in the 2nd dump and the 3rd column is the Type or String");
            sb.AppendLine($"For example if # iterations  = 11, 2 dumps are taken after iterations 7 and 11., '17  56  System.Guid' means there were 17 instances of System.Guid in the 1st dump and 56 in the 2nd");
            sb.AppendLine($"TypesAndStrings { Path.GetFileName(pathDumpBase)} {Path.GetFileName(pathDumpCurrent)}  {nameof(NumIterationsBeforeTotalToTakeBaselineSnapshot)}= {NumIterationsBeforeTotalToTakeBaselineSnapshot}");
            sb.AppendLine();
            sb.AppendLine("Types:");
            AnalyzeDiff(dictTypesBaseline, dictTypesCurrent, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot,
                (key, baseCnt, currentCnt) =>
                {
                    _dictTypeDiffs[key] = Tuple.Create(baseCnt, currentCnt);
                    var msg = string.Format("{0,5} {1,5} {2}", baseCnt, currentCnt, key); // can't use "$" because can contain embedded "{"
                    sb.AppendLine(msg);
                });
            sb.AppendLine();
            sb.AppendLine("Strings:");
            AnalyzeDiff(dictStringsBaseline, dictStringsCurrent, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot,
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
        public void AnalyzeDiff(
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

            foreach (ClrRoot root in heap.EnumerateRoots())
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
                        foreach (var reference in obj.EnumerateObjectReferences())
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

                    foreach (var reference in obj.EnumerateObjectReferences())
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

            foreach (ClrRoot root in heap.EnumerateRoots())
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
                            foreach (var reference in obj.EnumerateObjectReferences())
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
