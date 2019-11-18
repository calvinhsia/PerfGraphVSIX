//using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StressTestUtility
{
    public class DumpAnalyzer
    {
        private readonly ILogger logger;
        public DumpAnalyzer(ILogger logger)
        {
            this.logger = logger;
        }

        public (Dictionary<string, int> dictTypes, Dictionary<string, int> dictStrings) AnalyzeDump(string dumpFile)
        {
            //  "C:\Users\calvinh\AppData\Local\Temp\VSDbg\ClrObjExplorer\ClrObjExplorer.exe" 
            //  /s \\calvinhw8\c$\Users\calvinh\Documents;srv*C:\Users\calvinh\AppData\Local\Temp\Symbols*;\\ddelementary\public\CalvinH\VsDbgTestDumps\VSHeapAllocDetourDump;\\ddrps\symbols;http://symweb/ m "\\calvinhw8\c$\Users\calvinh\Documents\devenvNav2files700.dmp"
            //            var symPath = @"http://symweb";
            var dictTypes = new Dictionary<string, int>();
            var dictStrings = new Dictionary<string, int>();

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
                    foreach (var obj in runtime.Heap.EnumerateObjects())
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
                    }
                    logger.LogMessage($"Total Object Count = {nObjCount:n0} TypeCnt = {dictTypes.Count} {dumpFile}");
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
                logger.LogMessage($"Exception analyzing dump {ex.ToString()}");
            }
            return (dictTypes, dictStrings);
        }

        public StringBuilder GetDiff(string pathDumpBase, string pathDumpCurrent, int TotNumIterations, int NumIterationsBeforeTotalToTakeBaselineSnapshot)
        {
            var (dictTypes, dictStrings) = AnalyzeDump(pathDumpBase);
            var resCurrent = AnalyzeDump(pathDumpCurrent);
            var sb = new StringBuilder();
            sb.AppendLine($"2 dumps were made: 1 at iteration # {TotNumIterations-NumIterationsBeforeTotalToTakeBaselineSnapshot}, the other after iteration {TotNumIterations}");
            sb.AppendLine($"Below are the counts of Types and Strings in each dump. The 1st column is the number in the 1st dump, the 2nd is the number found in the 2nd dump and the last is the Type or String");
            sb.AppendLine($"TypesAndStrings {pathDumpBase} {pathDumpCurrent}  {nameof(NumIterationsBeforeTotalToTakeBaselineSnapshot)}= {NumIterationsBeforeTotalToTakeBaselineSnapshot}");
            AnalyzeDiff(sb, dictTypes, resCurrent.dictTypes, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot);
            AnalyzeDiff(sb, dictStrings, resCurrent.dictStrings, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot);
            logger.LogMessage($"analyzed types and strings {pathDumpBase} {pathDumpCurrent}");
            //            var fname = DumperViewerMain.GetNewFileName(measurementHolder.TestName, "");
            return sb;
        }

        private void AnalyzeDiff(StringBuilder sb, Dictionary<string, int> dictBase, Dictionary<string, int> dictCurrent, int TotNumIterations, int NumIterationsBeforeTotalToTakeBaselineSnapshot)
        {
            foreach (var entryCurrent in dictCurrent.Where(e => e.Value >= TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot - 1).OrderBy(e => e.Value))
            {
                if (dictBase.ContainsKey(entryCurrent.Key))
                {
                    var baseCnt = dictBase[entryCurrent.Key];
                    if (baseCnt > TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
                    {
                        if (baseCnt + NumIterationsBeforeTotalToTakeBaselineSnapshot <= entryCurrent.Value)
                        {
                            var msg = string.Format("{0,5} {1,5} {2}", baseCnt, entryCurrent.Value, entryCurrent.Key); // can't use "$" because can contain embedded "{"
                            sb.AppendLine(msg);
                            //                        logger.LogMessage("{0}", msg); // can't use "$" because can contain embedded "{"
                        }
                    }
                }
            }
        }

        public void StartClrObjectExplorer(string _DumpFileName)
        {
            var exeNameClrObj = Path.Combine(
               Path.GetDirectoryName(this.GetType().Assembly.Location),
               "ClrObjExplorer.exe");
            if (!File.Exists(exeNameClrObj))
            {
                throw new FileNotFoundException($"{exeNameClrObj}");
            }
            var args = $"/m \"{_DumpFileName}\"";
            Process.Start(exeNameClrObj, args);
        }
    }
}
