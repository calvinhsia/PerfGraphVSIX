using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace PerfGraphVSIX
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(PerfGraphToolWindow))]
    [Guid(PerfGraphToolWindowPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class PerfGraphToolWindowPackage : AsyncPackage
    {
        /// <summary>
        /// PerfGraphToolWindowPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "ce00f7a9-fdc9-4b45-8f95-0a9e24cf4480";

        public static IComponentModel ComponentModel { get; private set; }

        private bool fDidShowToolWindow = false;

        // https://github.com/microsoft/VSSDK-Analyzers/blob/master/doc/VSSDK003.md#solution
        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            IVsAsyncToolWindowFactory res = null;
            if (!fDidShowToolWindow)
            {
                if (toolWindowType == typeof(PerfGraphToolWindow).GUID)
                {
                    res = this;
                }
            }
            return res;
        }
        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            if (toolWindowType == typeof(PerfGraphToolWindow))
            {
                return PerfGraphToolWindow.CaptionString + " Loading";
            }
            return base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            EnvDTE.DTE dte = (EnvDTE.DTE)await GetServiceAsync(typeof(EnvDTE.DTE));
            
            PerfGraphToolWindowCommand.Instance.g_dte = dte ?? throw new InvalidOperationException(nameof(dte));

            return "foo";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PerfGraphToolWindowPackage"/> class.
        /// </summary>
        public PerfGraphToolWindowPackage()
        {

        }


        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await Task.Yield();
            await PerfGraphToolWindowCommand.InitializeAsync(this);
            ComponentModel = (await this.GetServiceAsync(typeof(SComponentModel))) as IComponentModel;
            await TaskScheduler.Default;
            _ = DumperViewerMain.SendTelemetryAsync($"{Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion}");
            //            await InitializeToolWindowAsync(typeof(PerfGraphToolWindow), id: 0, cancellationToken: cancellationToken);

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            var task = this.JoinableTaskFactory.RunAsync(async delegate
             {
                 var cts = new CancellationToken();
                 await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                 ToolWindowPane window = await this.ShowToolWindowAsync(typeof(PerfGraphToolWindow), id: 0, create: true, cancellationToken: cts);
                 if ((null == window) || (null == window.Frame))
                 {
                     throw new NotSupportedException("Cannot create tool window");
                 }

                 IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                 fDidShowToolWindow = true;
                 Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
             });

        }


    }
}
