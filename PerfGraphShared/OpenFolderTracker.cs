//using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
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
//        public IVsFolderWorkspaceService _vsFolderWorkspaceService;
        internal PerfGraphToolWindowControl _perfGraph;
        internal ObjTracker _objTracker;
        //[ImportingConstructor]
        //public OpenFolderTracker(IVsFolderWorkspaceService vsFolderWorkspaceService)
        //{
        //    _vsFolderWorkspaceService = vsFolderWorkspaceService;
        //}

        internal void Initialize(PerfGraphToolWindowControl perfGraph, ObjTracker objTracker)
        {
            this._perfGraph = perfGraph;
            this._objTracker = objTracker;

            /*
             * 2>C:\Program Files (x86)\Microsoft Visual Studio\2019\Priv\MSBuild\Current\Bin\Microsoft.Common.CurrentVersion.targets(2081,5): warning MSB3277: Found conflicts between different versions of "Microsoft.VisualStudio.Threading" that could not be resolved.  These reference conflicts are listed in the build log when log verbosity is set to detailed.
            2>CSC : error CS1705: Assembly 'Microsoft.VisualStudio.Workspace' with identity 'Microsoft.VisualStudio.Workspace, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' uses 'Microsoft.VisualStudio.Threading, Version=16.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' which has a higher version than referenced assembly 'Microsoft.VisualStudio.Threading' with identity 'Microsoft.VisualStudio.Threading, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
            2>C:\Users\calvinh\Source\Repos\PerfGraphVSIX\PerfGraphVSIX\ObjTracker.cs(188,105,188,117): error CS1705: Assembly 'Microsoft.VisualStudio.Workspace' with identity 'Microsoft.VisualStudio.Workspace, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' uses 'Microsoft.VisualStudio.Threading, Version=16.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' which has a higher version than referenced assembly 'Microsoft.VisualStudio.Threading' with identity 'Microsoft.VisualStudio.Threading, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

             */
            //this._vsFolderWorkspaceService.OnActiveWorkspaceChanged += OnActiveWorkspaceChangedAsync;
        }
        Task OnActiveWorkspaceChangedAsync(object sender, EventArgs e) // sender: {Microsoft.VisualStudio.Workspace.VSIntegration.WorkspaceExplorer}
        {
            //var x = sender as IVsFolderWorkspaceService;
            //var wrkspace = x.CurrentWorkspace;
            //if (wrkspace != null)
            //{
            //    _objTracker.AddObjectToTrack(wrkspace, ObjSource.FromProject, $"Open Folder {wrkspace.Location}");
            //}
            return Task.CompletedTask;

        }
    }
}
