﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    /// <summary>
    /// Because we need to work with Dev16 (32bit) and Dev17 (64 bit) we use an interface that each VSHandler (32 or 64) implements 
    /// </summary>
    public interface IVSHandler
    {
        void Initialize(ILogger logger, int delayMultiplier = 1);
        string GetVSFullPath();
        Process VsProcess { get; }
        int DelayMultiplier { get; set; }
        Task<object> EnsureGotDTE(TimeSpan timeout, int TargetProcessid = 0); // 32 or 64 bit version is different
        string DoVSRegEdit(string regeditparam, string vsPath="");
        void PrepareVSSettingsForLeakDetection();
        Task DteExecuteCommandAsync(string dteCommand, int TimeoutSecs = 60);
        Task<Process> StartVSAsync(MemSpectModeFlags flags = MemSpectModeFlags.MemSpectModeNone, string MemSpectDllPath = "");
        Task OpenSolutionAsync(string solutionPath);
        Task CloseSolutionAsync();
        Task ShutDownVSAsync();
    }
}
