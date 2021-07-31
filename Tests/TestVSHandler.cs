using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class TestVSHandler : BaseTestClass
    {
        [TestMethod]
        public void TestVSHandlerSettingsForLeakDetection()
        {
            var vsHandler = new VSHandlerCreator().CreateVSHandler(this);
            LogMessage(vsHandler.DoVSRegEdit("read local HKCU General DelayTimeThreshold dword"));

            LogMessage(vsHandler.DoVSRegEdit("read local HKCU General MaxNavigationHistoryDepth dword"));

        }

        [TestMethod]
        public async Task TestVSHandlerGetDTEObj()
        {
            AsyncPump.Run(async () =>
            {
                try
                {
                    MessageFilter.RegisterMessageFilter();
                    var vsHandler = new VSHandlerCreator().CreateVSHandler(this);
                    await vsHandler.StartVSAsync();
                    var dte = (EnvDTE._DTE)await vsHandler.EnsureGotDTE(timeout: TimeSpan.FromSeconds(10));

                    Assert.IsNotNull(dte);

                    var itmOperations = dte.ItemOperations.OpenFile(@"c:\t.txt");

                    var actWindow = dte.ActiveWindow.Caption;

                    Assert.IsNotNull(actWindow);
                    Assert.AreEqual(actWindow, "t.txt");

                    MessageFilter.RevokeMessageFilter();
                    dte.Quit();
                    Assert.IsTrue(true);

                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }

            });
            await Task.Yield();
        }

    }
}
