using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    public interface IStressUtil
    {
        Task DoSampleAsync(string desc);

        Task CreateDumpAsync(int pid, MemoryAnalysisType memoryAnalysisType, string desc);
    }
}
