using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    [Export(typeof(OpenFolderTracker))]
    public class OpenFolderTracker
    {
        public IVsFolderWorkspaceService _vsFolderWorkspaceService;
        internal PerfGraph _perfGraph;
        internal ObjTracker _objTracker;
        [ImportingConstructor]
        public OpenFolderTracker(IVsFolderWorkspaceService vsFolderWorkspaceService)
        {
            _vsFolderWorkspaceService = vsFolderWorkspaceService;
        }

        internal void Initialize(PerfGraph perfGraph, ObjTracker objTracker)
        {
            this._perfGraph = perfGraph;
            this._objTracker = objTracker;
            this._vsFolderWorkspaceService.OnActiveWorkspaceChanged += OnActiveWorkspaceChangedAsync;
        }
        Task OnActiveWorkspaceChangedAsync(object sender, EventArgs e) // sender: {Microsoft.VisualStudio.Workspace.VSIntegration.WorkspaceExplorer}
        {
            var x = sender as IVsFolderWorkspaceService;
            var wrkspace = x.CurrentWorkspace;
            if (wrkspace != null)
            {
                var disposeToken = (wrkspace as Microsoft.VisualStudio.Workspace.IWorkspace2).DisposeToken;
                var (nReg, nLinked) = Utilty.ProcessCancellationToken(disposeToken, (s) =>
                {
                });
                var tsk = _perfGraph.AddStatusMsgAsync($"# disposeToken callback Registrations = {nReg} Linked = {nLinked}");
                //var tks = disposeToken.GetType().GetField("m_source", bFlags).GetValue(disposeToken);
                //var reglist = tks.GetType().GetField("m_registeredCallbacksLists", bFlags).GetValue(tks);
                //var elemType = reglist.GetType().GetElementType();

                //var linkedList = tks.GetType().GetField("m_linkingRegistrations", bFlags).GetValue(tks);
            }
            return Task.CompletedTask;

        }
    }
}
