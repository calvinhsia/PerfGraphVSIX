//using Microsoft.Diagnostics.Runtime;
using EnvDTE;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class DumpAnalyzer
    {
        private readonly ILogger logger;
        public DumpAnalyzer(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Given a dump file, output 2 dictionaries: CLR Type name=>Count   and String=>Count
        /// e.g. if the dump has 44 copies of the string "foobar", then dictStrings["foobar"]=44
        ///    if the dump has 12 instances of Microsoft.VisualStudio.Type.Foobar, then  dictTypes["Microsoft.VisualStudio.Type.Foobar"] =12
        /// </summary>
        /// <param name="dumpFile"></param>
        /// <param name="dictTypes"></param>
        /// <param name="dictStrings"></param>
        public void AnalyzeDump(string dumpFile, out Dictionary<string, int> dictTypes, out Dictionary<string, int> dictStrings)
        {
            //  "C:\Users\calvinh\AppData\Local\Temp\VSDbg\ClrObjExplorer\ClrObjExplorer.exe" 
            //  /s \\calvinhw8\c$\Users\calvinh\Documents;srv*C:\Users\calvinh\AppData\Local\Temp\Symbols*;\\ddelementary\public\CalvinH\VsDbgTestDumps\VSHeapAllocDetourDump;\\ddrps\symbols;http://symweb/ m "\\calvinhw8\c$\Users\calvinh\Documents\devenvNav2files700.dmp"
            //            var symPath = @"http://symweb";
            dictTypes = new Dictionary<string, int>();
            dictStrings = new Dictionary<string, int>();

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
                logger.LogMessage($"Exception analyzing dump {ex}");
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
        public void GetDiff(
            StringBuilder sb,
            string pathDumpBase,
            string pathDumpCurrent,
            int TotNumIterations,
            int NumIterationsBeforeTotalToTakeBaselineSnapshot)
        {
            _dictTypeDiffs = new Dictionary<string, Tuple<int, int>>();
            _dictStringDiffs = new Dictionary<string, Tuple<int, int>>();

            AnalyzeDump(pathDumpBase, out var dictTypesBaseline, out var dictStringsBaseline);
            AnalyzeDump(pathDumpCurrent, out var dictTypesCurrent, out var dictStringsCurrent);
            sb.AppendLine($"2 dumps were made: 1 at iteration # {TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot}, the other after iteration {TotNumIterations}");
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

        static string _ClrObjExplorerExe = null;
        public Dictionary<string, Tuple<int, int>> _dictTypeDiffs;
        public Dictionary<string, Tuple<int, int>> _dictStringDiffs;

        public string GetClrObjExplorerPath()
        {
            if (_ClrObjExplorerExe == null)
            {
                var clrObjDir = Path.Combine(DumperViewerMain.EnsureResultsFolderExists(), @"..\ClrObjExplorer");
                //                logger.LogMessage($"Looking for ClrObjExplorer in {clrObjDir}");
                try
                {
                    var testClrObjExpPath = Path.Combine(clrObjDir, "ClrObjExplorer.exe");
                    if (Directory.Exists(clrObjDir))
                    {
                        if (File.Exists(testClrObjExpPath))
                        {
                            _ClrObjExplorerExe = testClrObjExpPath;
                        }
                        else
                        {
                            logger.LogMessage($"Deleting existing {clrObjDir}");
                            Directory.Delete(clrObjDir, recursive: true);
                        }
                    }
                    if (string.IsNullOrEmpty(_ClrObjExplorerExe))
                    {
                        logger.LogMessage($"Creating {clrObjDir}");
                        Directory.CreateDirectory(clrObjDir);
                        var tempZipFile = Path.Combine(clrObjDir, "clrobj.zip");
                        //                        logger.LogMessage($"Unzip to {tempZipFile}");
                        var zipArray = Properties.Resources.ClrObjExplorer;
                        File.WriteAllBytes(tempZipFile, zipArray);
                        //                        logger.LogMessage($"Extracting zip {tempZipFile}");
                        ZipFile.ExtractToDirectory(tempZipFile, clrObjDir);
                        logger.LogMessage($"Done Extracting zip {tempZipFile}");
                        File.Delete(tempZipFile);
                        _ClrObjExplorerExe = Path.Combine(clrObjDir, "ClrObjExplorer.exe");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogMessage(ex.ToString());
                    // use existing ClrObjExplorer: user may have left it open examining a prior trace (Unauth exce if in use) or it could be 
                }
                //                logger.LogMessage($"Found ClrObjExplorer at {_ClrObjExplorerExe }");
            }
            return _ClrObjExplorerExe;
        }

        public void StartClrObjExplorer(string _DumpFileName)
        {
            var args = $"/m \"{_DumpFileName}\"";
            System.Diagnostics.Process.Start(GetClrObjExplorerPath(), args);
        }
    }
}
