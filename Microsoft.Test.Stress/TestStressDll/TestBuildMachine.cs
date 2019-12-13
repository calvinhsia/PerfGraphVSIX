using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStressDll
{
    [TestClass]
    public class TestBuildMachine
    {
        public TestContext TestContext { get; set; }
        ILogger logger;
        VSHandler _VSHandler;
        [TestInitialize]
        [Ignore]
        public async Task TestInitialize()
        {
            await Task.Yield();
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = new VSHandler(logger);
            logger.LogMessage($"Computername=" + Environment.GetEnvironmentVariable("Computername"));
            logger.LogMessage($"TEMP=" + Environment.GetEnvironmentVariable("TEMP"));
            logger.LogMessage($"LOCALAPPDATA=" + Environment.GetEnvironmentVariable("LOCALAPPDATA"));

            logger.LogMessage($"Username=" + Environment.GetEnvironmentVariable("Username"));
            logger.LogMessage($"UserDomain=" + Environment.GetEnvironmentVariable("userdomain"));
            logger.LogMessage($"ProgramFiles(x86)=" + Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            logger.LogMessage($"Path=" + Environment.GetEnvironmentVariable("path"));
            logger.LogMessage($"VS Path={VSHandler.GetVSFullPath()}");

            /*
             
TestContext Messages:
09:01:11:287  5 Computername=fv-az683
09:01:11:287  5 TEMP=C:\Users\VSSADM~1\AppData\Local\Temp
09:01:11:287  5 LOCALAPPDATA=C:\Users\VssAdministrator\AppData\Local
09:01:11:287  5 Username=VssAdministrator
09:01:11:287  5 UserDomain=fv-az683
09:01:11:287  5 ProgramFiles(x86)=C:\Program Files (x86)
09:01:11:287  5 Path=C:\agents\2.162.0\externals\git\cmd;C:/hostedtoolcache/windows\Python\3.6.8\x64;C:/hostedtoolcache/windows\Python\3.6.8\x64\Scripts;C:\Program Files\Mercurial\;C:\ProgramData\kind;C:\vcpkg;C:\cf-cli;C:\Program Files (x86)\NSIS\;C:\Program Files\Mercurial\;C:\Program Files\Boost\1.69.0;C:\Program Files\dotnet;C:\mysql-5.7.21-winx64\bin;C:\Program Files\Java\zulu-8-azure-jdk_8.40.0.25-8.0.222-win_x64\bin;C:\npm\prefix;C:\Program Files (x86)\sbt\bin;C:\Rust\.cargo\bin;C:\hostedtoolcache\windows\Ruby\2.5.5\x64\bin;C:\Go1.12.7\bin;C:\Program Files\Git\bin;C:\Program Files\Git\usr\bin;C:\Program Files\Git\mingw64\bin;C:\hostedtoolcache\windows\Python\3.7.5\x64\Scripts;C:\hostedtoolcache\windows\Python\3.7.5\x64;C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin;C:\windows\system32;C:\windows;C:\windows\System32\Wbem;C:\windows\System32\WindowsPowerShell\v1.0\;C:\windows\System32\OpenSSH\;C:\ProgramData\Chocolatey\bin;C:\Program Files\Docker;C:\Program Files\PowerShell\6\;C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\;C:\Program Files\dotnet\;C:\Program Files\Microsoft SQL Server\130\Tools\Binn\;C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\;C:\Program Files\Microsoft Service Fabric\bin\Fabric\Fabric.Code;C:\Program Files\Microsoft SDKs\Service Fabric\Tools\ServiceFabricLocalClusterManager;C:\Program Files\Git\cmd;C:\Program Files\Git\mingw64\bin;C:\Program Files\Git\usr\bin;c:\tools\php;C:\Program Files (x86)\sbt\bin;C:\Program Files (x86)\Subversion\bin;C:\Program Files\nodejs\;C:\ProgramData\chocolatey\lib\maven\apache-maven-3.6.2\bin;C:\Program Files\CMake\bin;C:\Strawberry\c\bin;C:\Strawberry\perl\site\bin;C:\Strawberry\perl\bin;C:\Program Files\OpenSSL\bin;C:\Users\VssAdministrator\.dotnet\tools;C:\Program Fil
09:01:11:303  5 VS Path=C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\devenv.exe
09:01:11:318  5 # of devenv = 0
09:01:11:318  5 StartVSAsync
09:01:13:104  5 Started VS PID= 7132
09:01:13:104 10 EnsureGotDTE
09:01:13:104 10 Latest devenv PID= 7132 starttime = 12/12/2019 9:01:12 PM
09:01:43:454 10 Couldn't get DTE in 30 secs             */


            var vsprocs = Process.GetProcessesByName("devenv");
            logger.LogMessage($"# of devenv = {vsprocs.Length}");
            foreach (var devenv in vsprocs)
            {
                logger.LogMessage($" {devenv.MainModule.FileName}");
            }

            await _VSHandler.StartVSAsync();
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.vsProc.Id}");
        }
        [TestMethod]
        public async Task TestBuildMachineDTE()
        {
            await Task.Yield();

        }
        [TestCleanup]
        public async Task Cleanup()
        {
            await _VSHandler.ShutDownVSAsync();
        }
    }
}
