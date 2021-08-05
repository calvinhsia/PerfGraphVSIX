using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

            /*
             * When using DTE to talk to VS, we send a DTE command to VS, but VS may be busy and throws an exception
             *     System.Runtime.InteropServices.COMException: The message filter indicated that the application is busy. (Exception from HRESULT: 0x8001010A (RPC_E_SERVERCALL_RETRYLATER))
             *     To mitigate, can use MessageFilter.
             * To get MessageFilter to work: 
             *   we need a private thread on which we can run all DTE operations
             *   we need to set STA on that thread: so can't be a ThreadPool thread
             *   all async/continuations need to be run on the thread
             *   AsyncPump.Run allows this, but the problem is that then the TestInitialize and the TestMethod need to run in the same AsyncPump.Run
             *   Attempt to do with a Dispatcher, but deadlocks.
             */
            await Task.Yield();
            //AsyncPump.Run(async () =>
            //{
            //    await DoItAsync();
            //});
            //await Task.Run(async () =>
            //{
            //    await DoItAsync();
            //});

            var tcs = new TaskCompletionSource<int>(0);
            System.Windows.Threading.Dispatcher dispatcher;
            Thread thd = null;
            thd = new Thread(() =>
            {
                try
                {
                    LogMessage("Starting on thread");
                    AsyncPump.Run(async () =>
                    {
                        dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                        LogMessage($"Got dispatcher {dispatcher?.ToString()}");
                        await DoItAsync();
                        tcs.SetResult(0);
                    });
                }
                catch (Exception ex)
                {
                    LogMessage(ex.ToString());
                    tcs.SetException(ex);
                }
            })
            {
                Name = "MyThread"
            };
            thd.SetApartmentState(ApartmentState.STA);
            thd.Start();
            await tcs.Task;

            async Task DoItAsync()
            {
                try
                {
                    EnvDTE._DTE dte = null;
                    LogMessage("Starting test");
                    //                    dispatcher.Invoke(() =>
                    //                    {
                    var vsHandler = new VSHandlerCreator().CreateVSHandler(this);
                    await vsHandler.StartVSAsync();
                    dte = (EnvDTE._DTE)await vsHandler.EnsureGotDTE(timeout: TimeSpan.FromSeconds(10));
                    //tsk.Wait();
                    //dte = (EnvDTE._DTE)tsk.Result;
                    Assert.IsNotNull(dte);
                    var tmpFile = @"c:\t.txt";// constant file that won't screw up my MRU too badly
                    if (!File.Exists(tmpFile))
                    {
                        tmpFile = Path.GetTempFileName();
                        File.WriteAllText(tmpFile, "test");
                    }
                    // we loop faster than VS can respond causing IOleMessageFilter:RetryRejectedCall, where we say wait and try again
                    LogMessage("registering filter");
                    MessageFilter.RegisterMessageFilter(this); //System.InvalidOperationException: Failed to set the specified COM apartment state.
                    for (int i = 0; i < 10; i++)
                    {
                        LogMessage("Open file");
                        dte.ItemOperations.OpenFile(tmpFile);
                        await Task.Yield(); // see if await breaks filter (should continue on same thread)
                        LogMessage("close file");
                        dte.ActiveWindow.Close();
                    }
                    //var itmOperations = dte.ItemOperations.OpenFile(@"c:\t.txt");

                    //var actWindow = dte.ActiveWindow.Caption;

                    //Assert.IsNotNull(actWindow);
                    //Assert.AreEqual(actWindow, "t.txt");

                    MessageFilter.RevokeMessageFilter();
                    dte.Quit();
                    //                    });
                    Assert.IsTrue(true);

                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
                await Task.Yield();
            }
        }

        [TestMethod]
        public void TestVSTyping()
        {
            AsyncPump.Run(async () =>
            {
                var tcs = new TaskCompletionSource<int>(0);
                Thread thd = null;
                thd = new Thread(() =>
                {
                    try
                    {
                        AsyncPump.Run(async () =>
                        {
                            MessageFilter.RegisterMessageFilter(this); //System.InvalidOperationException: Failed to set the specified COM apartment state.
                            var vsHandler = new VSHandlerCreator().CreateVSHandler(this);
                            await vsHandler.StartVSAsync();
                            var dte = (EnvDTE._DTE)await vsHandler.EnsureGotDTE(timeout: TimeSpan.FromSeconds(10));

                            var filename = @"c:\t.cs"; // constant file that won't screw up my MRU too badly
                            if (!File.Exists(filename))
                            {
                                filename = Path.GetTempPath() + ".cs";
                            }
                            await Task.Delay(TimeSpan.FromMilliseconds(5000)); // let VS idle a bit so startup tasks complete
                            File.WriteAllText(filename,
                                @"
using System;
public class myclass
{
    public void Main()
    {


    }
}
");

                            dte.ItemOperations.OpenFile(filename);
                            var kb = new KeyboardAutomationService();
                            kb.TypeKey(KeyboardKey.Down);
                            kb.TypeKey(KeyboardKey.Down);
                            kb.TypeKey(KeyboardKey.Down);
                            kb.TypeKey(KeyboardKey.Down);
                            kb.TypeKey(KeyboardKey.Down);
                            kb.TypeKey(KeyboardKey.Down);
                            for (int i = 0; i < 10; i++)
                            {
                                kb.TypeText($"\"{i}\"."); //  "1".ToString();
                                await Task.Delay(TimeSpan.FromMilliseconds(500)); // for intellisense to 
                                kb.TypeKey('t');
                                await Task.Delay(TimeSpan.FromMilliseconds(100));
                                kb.TypeKey('o');
                                await Task.Delay(TimeSpan.FromMilliseconds(100));
                                kb.TypeKey('s');
                                await Task.Delay(TimeSpan.FromMilliseconds(100));
                                kb.TypeKey(KeyboardKey.Tab);
                                kb.TypeText("();");
                                kb.TypeKey(KeyboardKey.Enter);
                                await Task.Delay(TimeSpan.FromMilliseconds(100));
                            }

                            //kb.TypeKey(KeyboardModifier.Alt, new[] { 't', 'o' }); // ToolsOptions
                            //await Task.Delay(TimeSpan.FromMilliseconds(1500));
                            //kb.TypeKey(KeyboardKey.Escape);

                            dte.ActiveWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
                            dte.Quit();
                            MessageFilter.RevokeMessageFilter();
                            tcs.SetResult(0);
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage(ex.ToString());
                        tcs.SetException(ex);
                    }
                })
                {
                    Name = "MyThread"
                };
                thd.SetApartmentState(ApartmentState.STA);
                thd.Start();
                await tcs.Task;

            });

        }

    }
}
