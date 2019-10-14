using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
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
}
