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
// Created: September 24, 2018 12:48:59 PM
// 
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Gorgon.Editor.Properties;
using Gorgon.UI;

namespace Gorgon.Editor.Services
{
    /// <summary>
    /// A service used to show a dialog for opening an editor file.
    /// </summary>
    internal class EditorFileOpenDialogService
        : IEditorFileDialogService
    {
        #region Properties.
        /// <summary>
        /// Property to set or return a file filter.
        /// </summary>        
        public string FileFilter
        {
            get;
            set;
        }
        /// <summary>
        /// Property to set or return the initial directory.
        /// </summary>        
        public DirectoryInfo InitialDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Property to set or return the title for the dialog.
        /// </summary>
        /// <value>The dialog title.</value>
        public string DialogTitle
        {
            get;
            set;
        }

        /// <summary>
        /// Property to return the available file system providers.
        /// </summary>        
        public IFileSystemProviders Providers
        {
            get;
        }

        /// <summary>
        /// Property to return the settings for the application.
        /// </summary>
        /// <value>The settings.</value>
        public EditorSettings Settings
        {
            get;
        }
        #endregion

        #region Methods.
        /// <summary>
        /// Function to retrieve the parent form for the message box.
        /// </summary>
        /// <returns>The form to use as the owner.</returns>
        private static Form GetParentForm()
        {
            if (Form.ActiveForm != null)
            {
                return Form.ActiveForm;
            }

            if (Application.OpenForms.Count > 1)
            {
                return Application.OpenForms[Application.OpenForms.Count - 1];
            }

            return GorgonApplication.MainForm;
        }

        /// <summary>
        /// Function to return the dialog.
        /// </summary>
        /// <param name="allowMultiSelect"><b>true</b> to allow multiple file selection, or <b>false</b> to only allow single selection.</param>
        /// <returns>The open file dialog.</returns>
        private OpenFileDialog GetDialog(bool allowMultiSelect)
        {
            DirectoryInfo initialDirectory = InitialDirectory;

            if ((InitialDirectory == null) || (!InitialDirectory.Exists))
            {
                initialDirectory = new DirectoryInfo(Settings.LastOpenSavePath);
            }
                        
            if (!initialDirectory.Exists)
            {
                initialDirectory = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            }

            return new OpenFileDialog
            {
                Title = string.IsNullOrWhiteSpace(DialogTitle) ? Resources.GOREDIT_TEXT_OPEN_EDITOR_FILE : DialogTitle,
                ValidateNames = true,
                SupportMultiDottedExtensions = true,
                Multiselect = allowMultiSelect,
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = FileFilter ?? Providers.GetReaderDialogFilterString(),
                InitialDirectory = initialDirectory.FullName,
                RestoreDirectory = true
            };
        }

        /// <summary>
        /// Function to retrieve a single file name.
        /// </summary>
        /// <returns>The selected file path, or <b>null</b> if cancelled.</returns>
        public string GetFilename()
        {
            OpenFileDialog dialog = null;

            try
            {
                dialog = GetDialog(false);

                return dialog.ShowDialog(GetParentForm()) == DialogResult.Cancel ? null : dialog.FileName;
            }
            finally
            {
                dialog?.Dispose();
            }
        }

        /// <summary>
        /// Function to retrieve multiple file names.
        /// </summary>
        /// <returns>The list of file paths, or <b>null</b> if cancelled.</returns>
        public IReadOnlyList<string> GetFilenames()
        {
            OpenFileDialog dialog = null;

            try
            {
                dialog = GetDialog(false);

                return dialog.ShowDialog(GetParentForm()) == DialogResult.Cancel ? null : dialog.FileNames;
            }
            finally
            {
                dialog?.Dispose();
            }
        }
        #endregion

        #region Constructor/Finalizer.
        /// <summary>
        /// Initializes a new instance of the <see cref="EditorFileOpenDialogService"/> class.
        /// </summary>
        /// <param name="settings">The application settings.</param>
        /// <param name="providers">The providers used for opening/saving files.</param>
        public EditorFileOpenDialogService(EditorSettings settings, IFileSystemProviders providers)
        {
            Settings = settings;
            Providers = providers;
        }
        #endregion
    }
}
