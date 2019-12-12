using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = new VSHandler(logger);
            logger.LogMessage($"Username" + Environment.GetEnvironmentVariable("Username"));
            logger.LogMessage($"UserDomain " + Environment.GetEnvironmentVariable("userdomain"));
            logger.LogMessage($"VS Path {VSHandler.GetVSFullPath()}");

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
