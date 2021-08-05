using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public enum LogVerbosity
    {
        Diagnostic,
        Minimal,
        None
    }
    public interface ILogger
    {
        void LogMessage(string msg, params object[] args);
    }
    /// <summary>
    /// MemSpect tracks native and managed allocations, as well as files, heapcreates, etc. Each allocation has ThreadId, call stack, size, additional info (like filename)
    /// When an object is freed (or Garbage collected) this info is discarded)
    /// Things are much slower with MemSpect, but can be much easier to diagnose. Tracking only native or only managed makes things faster.
    /// To use MemSpect here, environment variables need to be set before the target process starts the CLR.
    /// </summary>
    [Flags]
    public enum MemSpectModeFlags
    {
        MemSpectModeNone = 0,
        /// <summary>
        /// Track Native Allocations.
        /// </summary>
        MemSpectModeNative = 0x1,
        /// <summary>
        /// Tracks managed memory allocations
        /// </summary>
        MemSpectModeManaged = 0x2,
        MemSpectModeFull = MemSpectModeManaged | MemSpectModeNative,
    }
}
