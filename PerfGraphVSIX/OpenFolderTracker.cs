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
        internal PerfGraphToolWindowControl _perfGraph;
        internal ObjTracker _objTracker;
        [ImportingConstructor]
        public OpenFolderTracker(IVsFolderWorkspaceService vsFolderWorkspaceService)
        {
            _vsFolderWorkspaceService = vsFolderWorkspaceService;
        }

        internal void Initialize(PerfGraphToolWindowControl perfGraph, ObjTracker objTracker)
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
                _objTracker.AddObjectToTrack(wrkspace, ObjSource.FromProject, $"Open Folder {wrkspace.Location}");
            }
            return Task.CompletedTask;

        }
    }
}
