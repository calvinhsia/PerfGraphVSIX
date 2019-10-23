//using Microsoft.Diagnostics.Runtime;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumperViewer
{
    internal class DumpAnalyzer
    {
        private readonly ILogger _Logger;

        public DumpAnalyzer(ILogger logger)
        {
            this._Logger = logger;
        }

        internal void AnalyzeDump()
        {
            //  "C:\Users\calvinh\AppData\Local\Temp\VSDbg\ClrObjExplorer\ClrObjExplorer.exe" 
            //  /s \\calvinhw8\c$\Users\calvinh\Documents;srv*C:\Users\calvinh\AppData\Local\Temp\Symbols*;\\ddelementary\public\CalvinH\VsDbgTestDumps\VSHeapAllocDetourDump;\\ddrps\symbols;http://symweb/ m "\\calvinhw8\c$\Users\calvinh\Documents\devenvNav2files700.dmp"
//            var symPath = @"http://symweb";
            try
            {
                //using (var dataTarget = DataTarget.LoadCrashDump(this._dumperViewer._DumpFileName))
                //{
                //    if (dataTarget.ClrVersions.Count !=1)
                //    {
                //        throw new InvalidOperationException($"Expected 1 ClrVersion in process. Found {dataTarget.ClrVersions.Count} ");
                //    }
                //    var dacLocation = dataTarget.ClrVersions[0].LocalMatchingDac;
                //    _dumperViewer.LogMessage($"Got Dac {dacLocation}");
                //    var runtime = dataTarget.ClrVersions[0].CreateRuntime();
                //    _dumperViewer.LogMessage($"Got runtime {runtime}");


                //}

                //var dataTarget = DataTarget.AttachToProcess(_dumperViewer._procTarget.Id, msecTimeout: 5000, AttachFlag.NonInvasive);
                //_dumperViewer.LogMessage($"Got dt {dataTarget}");
                //var runtime = dataTarget.ClrVersions[0].CreateRuntime();
                //_dumperViewer.LogMessage($"Got runtime {runtime}");
                //var nObjCount = 0;
                //var dict = new Dictionary<string, int>();
                //foreach (var obj in runtime.Heap.EnumerateObjects())
                //{
                //    var typ = obj.Type.Name;
                //    if (!dict.ContainsKey(typ))
                //    {
                //        dict[typ] = 1;
                //    }
                //    else
                //    {
                //        dict[typ]++;
                //    }

                //    nObjCount++;
                //}
                //foreach (var entry in dict.OrderByDescending(kvp=>kvp.Value))
                //{
                //    _dumperViewer.LogMessage($"  {entry.Value,10:n0}  {entry.Key}");
                //}
                //dataTarget.Dispose();
                //_dumperViewer.LogMessage($"Got runtime {runtime} nObjs = {nObjCount:n0}");

            }
            catch (Exception ex)
            {
                _Logger.LogMessage($"Exception analyzing dump {ex.ToString()}");
            }


        }

        internal void StartClrObjectExplorer(string _DumpFileName)
        {
            var exeNameClrObj = Path.Combine(
               Path.GetDirectoryName( this.GetType().Assembly.Location),
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
