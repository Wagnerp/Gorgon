﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2018 Michael Winsor
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// Created: September 4, 2018 12:46:15 PM
// 
#endregion

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gorgon.Core;
using Gorgon.Diagnostics;
using Gorgon.Editor.Content;
using Gorgon.Editor.Metadata;
using Gorgon.Editor.Plugins;
using Gorgon.Editor.ProjectData;
using Gorgon.Editor.Properties;
using Gorgon.Editor.Services;
using Gorgon.Editor.UI;

namespace Gorgon.Editor.ViewModels
{
    /// <summary>
    /// The view model for the project editor interface.
    /// </summary>
    internal class ProjectVm
        : ViewModelBase<ProjectVmParameters>, IProjectVm
    {
        #region Variables.
        // The project data for the view model.
        private IProject _projectData;
        // The message display service.
        private IMessageDisplayService _messageService;
        // The busy state service.
        private IBusyStateService _busyService;
        // The current project state.
        private ProjectState _state = ProjectState.New;
        // The file explorer view model.
        private IFileExplorerVm _fileExplorer;
        // The current clipboard handler context.
        private IClipboardHandler _clipboardContext;
        // The application project manager.
        private IProjectManager _projectManager;
        // The file for the project.
        private FileInfo _projectFile;
        // The content plugin service.
        private IContentPluginService _contentPlugins;
        // The currently active content.
        private IEditorContent _currentContent;        
        #endregion

        #region Properties.
        /// <summary>Property to return the current content for the project.</summary>
        public IEditorContent CurrentContent
        {
            get => _currentContent;
            set
            {
                if (_currentContent == value)
                {
                    return;
                }

                OnPropertyChanging();
                _currentContent = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Property to set or return the current file writer plugin used to write the project out to a file.
        /// </summary>
        public FileWriterPlugin WriterPlugin
        {
            get => _projectData.Writer;
            set
            {
                if (_projectData.Writer == value)
                {
                    return;
                }

                OnPropertyChanging();
                _projectData.AssignWriter(value);                
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Property to set or return the active clipboard handler context.
        /// </summary>
        public IClipboardHandler ClipboardContext
        {
            get => _clipboardContext;
            set
            {
                if (_clipboardContext == value)
                {
                    return;
                }

                OnPropertyChanging();
                _clipboardContext = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Property to set or return whether to show external items that are not included in the project file system.
        /// </summary>
        public bool ShowExternalItems
        {
            get => _projectData.ShowExternalItems;
            set
            {
                if (_projectData.ShowExternalItems == value)
                {
                    return;
                }

                OnPropertyChanging();
                _projectData.ShowExternalItems = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Property to set or return the file explorer view model for use with the file explorer subview.
        /// </summary>
        public IFileExplorerVm FileExplorer
        {
            get => _fileExplorer;
            set
            {
                if (_fileExplorer == value)
                {
                    return;
                }

                UnassignEvents();

                OnPropertyChanging();
                _fileExplorer = value;
                OnPropertyChanged();

                AssignEvents();
            }
        }

        /// <summary>
        /// Property to set or return the current state of the project.
        /// </summary>
        public ProjectState ProjectState
        {
            get => _state;
            set
            {
                if ((_state == value) || ((_state == ProjectState.New) && (value != ProjectState.Unmodified)))
                {
                    return;
                }

                OnPropertyChanging();
                _state = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Property to set or return the title for the project.
        /// </summary>
        public string ProjectTitle => string.IsNullOrWhiteSpace(ProjectFile?.Name) ? Resources.GOREDIT_NEW_PROJECT : Path.GetFileNameWithoutExtension(ProjectFile.Name);

        /// <summary>
        /// Property to set or return the file information for the project if it was opened from a file.
        /// </summary>
        public FileInfo ProjectFile
        {
            get => _projectFile;
            set
            {
                if ((_projectFile == value) 
                    || ((value != null) 
                        && (_projectFile != null) 
                        && (string.Equals(value.FullName, _projectFile.FullName, StringComparison.OrdinalIgnoreCase))))
                {
                    return;
                }

                OnPropertyChanging();
                _projectFile = value;
                OnPropertyChanged();
                NotifyPropertyChanged(nameof(ProjectTitle));
            }
        }
        #endregion

        #region Methods.
        /// <summary>
        /// Handles the ProgressDeactivated event of the FileExplorer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void FileExplorer_ProgressDeactivated(object sender, EventArgs e) => HideProgress();

        /// <summary>
        /// Function called when the progress panel is shown or updated.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event parameters.</param>
        private void FileExplorer_ProgressUpdated(object sender, ProgressPanelUpdateArgs e) => UpdateProgress(e);

        /// <summary>
        /// Function called to deactivate the wait panel.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event parameters.</param>
        private void FileExplorer_WaitPanelDeactivated(object sender, EventArgs e) => HideWaitPanel();

        /// <summary>
        /// Function called to activate the wait panel.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event parameters.</param>
        private void FileExplorer_WaitPanelActivated(object sender, WaitPanelActivateArgs e) => ShowWaitPanel(e.Message, e.Title);

        /// <summary>
        /// Handles the FileSystemChanged event of the FileExplorer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void FileExplorer_FileSystemChanged(object sender, EventArgs e)
        {
            // Indicate that the file system has changes now.
            // This will only be triggered if a change is made to a file that is included in our project.  Excluded files don't matter as they don't get saved.
            if (_state != ProjectState.Unmodified)
            {
                return;
            }

            ProjectState = ProjectState.Modified;
        }

        /// <summary>
        /// Function to assign events for the child view models.
        /// </summary>
        private void AssignEvents()
        {
            if (FileExplorer == null)
            {
                return;
            }

            FileExplorer.WaitPanelActivated += FileExplorer_WaitPanelActivated;
            FileExplorer.WaitPanelDeactivated += FileExplorer_WaitPanelDeactivated;
            FileExplorer.ProgressUpdated += FileExplorer_ProgressUpdated;
            FileExplorer.ProgressDeactivated += FileExplorer_ProgressDeactivated;
            FileExplorer.FileSystemChanged += FileExplorer_FileSystemChanged;
        }

        /// <summary>
        /// Function to unassign events from the child view models.
        /// </summary>
        private void UnassignEvents()
        {
            if (FileExplorer == null)
            {
                return;
            }

            FileExplorer.ProgressUpdated -= FileExplorer_ProgressUpdated;
            FileExplorer.ProgressDeactivated -= FileExplorer_ProgressDeactivated;
            FileExplorer.WaitPanelActivated -= FileExplorer_WaitPanelActivated;
            FileExplorer.WaitPanelDeactivated -= FileExplorer_WaitPanelDeactivated;
            FileExplorer.FileSystemChanged -= FileExplorer_FileSystemChanged;
        }

        /// <summary>
        /// Function to determine whether the content can be opened or not.
        /// </summary>
        /// <param name="node">The node being opened.</param>
        /// <returns><b>true</b> if the node can be opened, <b>false</b> if not.</returns>
        private bool CanOpenContent(IFileExplorerNodeVm node) => (node != null) && (node.IsContent);

        /// <summary>
        /// Function to open a file node as content.
        /// </summary>
        /// <param name="node">The node to open.</param>
        private async void DoOpenContent(IFileExplorerNodeVm node)
        {
            try
            {
                // TODO: Check for unsaved content.

                if ((!(node is IContentFile file)) || (file.ContentPlugin == null))
                {
                    _messageService.ShowError(string.Format(Resources.GOREDIT_ERR_NO_PLUGIN_FOR_CONTENT, node.FullPath));
                    return;
                }

                // Close the current content. It should be saved at this point.
                CurrentContent?.Close();
                CurrentContent = null;

                ShowWaitPanel(string.Format(Resources.GOREDIT_TEXT_OPENING, node.Name));

                // Create a content object.
                IEditorContent content = await file.ContentPlugin.OpenContentAsync(file, Program.Log);

                if (content == null)
                {
                    return;
                }

                // TODO: Open the file.

                // Load the content.
                CurrentContent = content;
            }
            catch (Exception ex)
            {
                _messageService.ShowError(ex, string.Format(Resources.GOREDIT_ERR_CANNOT_OPEN_CONTENT, node.FullPath));
            }
            finally
            {
                HideWaitPanel();
            }
        }
        
        /// <summary>
        /// Function used to initialize the view model with dependencies.
        /// </summary>
        /// <param name="projectData">The project backing data store.</param>
        /// <param name="messageService">The message display service.</param>
        /// <param name="busyService">The busy state indicator service.</param>
        /// <exception cref="ArgumentMissingException">Thrown if any argument is <b>null</b>.</exception>
        protected override void OnInitialize(ProjectVmParameters injectionParameters)
        {
            _projectManager = injectionParameters.ProjectManager ?? throw new ArgumentMissingException(nameof(ProjectVmParameters.ProjectManager), nameof(injectionParameters));
            _projectData = injectionParameters.Project ?? throw new ArgumentMissingException(nameof(ProjectVmParameters.Project), nameof(injectionParameters));
            _messageService = injectionParameters.MessageDisplay ?? throw new ArgumentMissingException(nameof(ProjectVmParameters.MessageDisplay), nameof(injectionParameters));
            _busyService = injectionParameters.BusyState ?? throw new ArgumentMissingException(nameof(ProjectVmParameters.BusyState), nameof(injectionParameters));            
            _contentPlugins = injectionParameters.ContentPlugins ?? throw new ArgumentMissingException(nameof(ProjectVmParameters.ContentPlugins), nameof(injectionParameters));

            FileExplorer.OpenContentFile = new EditorCommand<IFileExplorerNodeVm>(DoOpenContent, CanOpenContent);
        }

        /// <summary>
        /// Function to persist the project data to a file.
        /// </summary>
        /// <param name="projectTitle">The title for the project.</param>
        /// <param name="path">A path to the file that will hold the project data.</param>
        /// <param name="writer">The plug in used to write the project data.</param>
        /// <param name="progressCallback">The callback method that reports the saving progress to the UI.</param>
        /// <param name="cancelToken">The token used for cancellation of the operation.</param>        
        /// <returns>A task for asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="projectTitle"/>, <paramref name="path"/>, or the <paramref name="writer"/> parameter is <b>null</b>.</exception>
        /// <exception cref="ArgumentEmptyException">Thrown when the <paramref name="projectTitle"/> or the <paramref name="path"/> parameter is empty.</exception>
        public Task PersistProjectAsync(string projectTitle, string path, FileWriterPlugin writer, Action<int, int, bool> progressCallback, CancellationToken cancelToken)
        {
            if (projectTitle == null)
            {
                throw new ArgumentNullException(nameof(projectTitle));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (string.IsNullOrWhiteSpace(projectTitle))
            {
                throw new ArgumentEmptyException(nameof(projectTitle)); 
            }
             
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentEmptyException(nameof(path));
            } 
 
            return Task.Run(() =>
            {
                Program.Log.Print("Saving files...", LoggingLevel.Verbose);
                _projectData.AssignWriter(writer);
                _projectManager.SaveProject(_projectData, path, writer, progressCallback, cancelToken);
            }, cancelToken);
        }

        /// <summary>
        /// Function called when the associated view is loaded.
        /// </summary>
        public override void OnLoad()
        {
            _busyService.SetBusy();

            try
            {
                AssignEvents();                
            }
            catch (Exception ex)
            {
                _messageService.ShowError(ex);
            }
            finally
            {
                _busyService.SetIdle();
            }
        }

        /// <summary>
        /// Function called when the associated view is unloaded.
        /// </summary>
        public override void OnUnload()
        {
            // TODO: This should probably be placed in a command.
            if (_projectData != null)
            {
                _projectManager.CloseProject(_projectData);
            }

            HideWaitPanel();
            HideProgress();
            UnassignEvents();

            CurrentContent?.Close();
        }
        #endregion
    }
}
