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
// Created: December 17, 2018 10:01:27 PM
// 
#endregion

using System;
using System.IO;
using System.Threading;
using Gorgon.Core;
using Gorgon.Diagnostics;
using Gorgon.Editor.Metadata;
using Gorgon.Editor.Properties;
using Gorgon.Editor.Services;
using Gorgon.Editor.UI;
using Gorgon.IO;

namespace Gorgon.Editor.PlugIns
{
    /// <summary>
    /// A plug in type that performs a custom import for content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content importer plug in allows an application to import external content and convert it to a type that the associated <see cref="ContentPlugIn"/> can handle. Content importers should be 
    /// associated with the content produced by a <see cref="ContentPlugIn"/> in some way, but is not mandatory.
    /// </para>
    /// <para>
    /// Content plug ins are automatically run whenever content is imported into the host application.
    /// </para>
    /// <para>
    /// The content importer plug in provides an importer object that the host application calls to perform the import operation on file data. This content importer must implement the 
    /// <see cref="IEditorContentImporter"/>. The host application will pass along the required objects to manipulate the file system, access the graphics interface, and other sets of functionality.
    /// </para>
    /// <para>
    /// One use case for this would be to import content generated by an external application and convert it to your own custom content type. Another would be to handle versioning between content types 
    /// where a newer format for the content supercedes a previous format. The importer would read the old version, and then upgrade to the later version.
    /// </para>
    /// </remarks>
    /// <seealso cref="ContentPlugIn"/>
    public abstract class ContentImportPlugIn
        : EditorPlugIn
    {
        #region Constants.
        /// <summary>
        /// An attribute name for the file metadata to indicate that this item was imported.
        /// </summary>
        /// <remarks>
        /// Use this value to provide the original filename (and optionally, full path) for the original content being imported. This is stored in the <see cref="ProjectItemMetadata"/> for informational 
        /// purposes and should not be used by the plug in.
        /// </remarks>
        public const string ImportOriginalFileNameAttr = "ImportOriginalName";
        #endregion

        #region Variables.
        // Flag to indicate that the plugin is initialized.
        private int _initialized;
        // The content services passed in from the host application.
        private IHostContentServices _hostContentServices;

        // The factory used to create the importer.
        private readonly Lazy<IEditorContentImporter> _importerFactory;
        #endregion

        #region Properties.
        /// <summary>
        /// Property to return the services from the host application.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Plug in developers that implement a common plug in type based on this base type, should assign this value to allow access to the common content services supplied by the host application.
        /// </para>
        /// <para>
        /// This will be assigned during the initialization of the plug in.
        /// </para>
        /// </remarks>
        /// <seealso cref="IHostServices"/>
        protected IHostContentServices HostContentServices
        {
            get => _hostContentServices;
            private set => HostServices = _hostContentServices = value;
        }

        /// <summary>
        /// Property to return the file system used to hold temporary file data.
        /// </summary>
        /// <remarks>
        /// Importer plug ins can use this to write temporary working data, which is deleted after the project unloads, for use during the import process.
        /// </remarks>
        protected IGorgonFileSystemWriter<Stream> TemporaryFileSystem
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the file system used by the project.
        /// </summary>
        /// <remarks>
        /// Importer plug ins can use this to read in dependencies and other content files from the project.
        /// </remarks>
        protected IGorgonFileSystem ProjectFileSystem
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the type of this plug in.
        /// </summary>
        /// <remarks>
        /// The <see cref="PlugIns.PlugInType"/> returned for this property indicates the general plug in functionality. 
        /// </remarks>
        /// <seealso cref="PlugIns.PlugInType"/>
        public sealed override PlugInType PlugInType => PlugInType.ContentImporter;
        #endregion

        #region Methods.
        /// <summary>
        /// Function to provide custom initialization for the plugin.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method provides the means to perform custom initialization for an implementing content plug in type. Plug in developers can override this method to perform their own one-time setup for a 
        /// custom plug in.
        /// </para>
        /// <para>
        /// This method will only be called once when the plug in is loaded. It cannot be called after it has been called during load.
        /// </para>
        /// </remarks>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Function to provide clean up for the plugin.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method provides the means to perform custom cleanup for an implementing content plug in type. Plug in developers can override this method to perform their own one-time cleanup for a 
        /// custom plug in.
        /// </para>
        /// <para>
        /// This method will only be called once when the plug in is unloaded. It cannot be called after it has been called during unload.
        /// </para>
        /// </remarks>
        protected virtual void OnShutdown()
        {
        }

        /// <summary>
        /// Function to open a content object from this plugin.
        /// </summary>
        /// <returns>A new <see cref="IEditorContentImporter"/> object.</returns>
        /// <remarks>
        /// <para>
        /// This method creates an instance of the custom content importer. The application will use the object returned to perform the actual import process.
        /// </para>
        /// </remarks>
        protected abstract IEditorContentImporter OnCreateImporter();

        /// <summary>
        /// Function to determine if the content plugin can open the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file to evaluate.</param>
        /// <returns><b>true</b> if the plugin can open the file, or <b>false</b> if not.</returns>
        /// <remarks>
        /// <para>
        /// This method is used to determine if the file specified by the <paramref name="filePath"/> passed to the method can be opened by this plug in. If the method returns <b>true</b>, then the host 
        /// application will convert the file using the importer produced by this plug in. Otherwise, if the method returns <b>false</b>, then the file is skipped.
        /// </para>
        /// <para>
        /// The <paramref name="filePath"/> is a path to the file on the project virtual file system.
        /// </para>
        /// <para>
        /// Implementors may use whatever method they desire to determine if the file can be opened (e.g. checking file extensions, examining file headers, etc...).
        /// </para>
        /// </remarks>
        protected abstract bool OnCanOpenContent(string filePath);

        /// <summary>
        /// Function to allow custom plug ins to implement custom actions when a project is created/opened.
        /// </summary>
        protected virtual void OnProjectOpened()
        {

        }

        /// <summary>
        /// Function to allow custom plug ins to implement custom actions when a project is closed.
        /// </summary>
        protected virtual void OnProjectClosed()
        {

        }

        /// <summary>
        /// Function called when a project is loaded/created.
        /// </summary>
        /// <param name="projectFileSystem">The file system used by the project.</param>
        /// <param name="tempFileSystem">The file system used to hold temporary working data.</param>
        public void ProjectOpened(IGorgonFileSystem projectFileSystem, IGorgonFileSystemWriter<Stream> tempFileSystem)
        {
            ProjectFileSystem = projectFileSystem;
            TemporaryFileSystem = tempFileSystem;
            OnProjectOpened();
        }

        /// <summary>
        /// Function called when a project is unloaded.
        /// </summary>
        public void ProjectClosed()
        {
            OnProjectClosed();
            TemporaryFileSystem = null;
        }

        /// <summary>
        /// Function to determine if the content plugin can open the specified file.
        /// </summary>
        /// <param name="filePath">The file to evaluate.</param>
        /// <returns><b>true</b> if the plugin can open the file, or <b>false</b> if not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="filePath"/> parameter is <b>null</b>.</exception>
        /// <exception cref="ArgumentEmptyException">Thrown when the <paramref name="filePath"/> parameter is empty.</exception>
        /// <remarks>
        /// <para>
        /// This method is used to determine if the file specified by the <paramref name="filePath"/> passed to the method can be opened by this plug in. If the method returns <b>true</b>, then the host 
        /// application will convert the file using the importer produced by this plug in. Otherwise, if the method returns <b>false</b>, then the file is skipped.
        /// </para>
        /// <para>
        /// The <paramref name="filePath"/> is a path to the file on the project virtual file system. Passing a physical file system path to this method will not work.
        /// </para>
        /// <para>
        /// This method calls the <see cref="OnCanOpenContent(string)"/> method to perform the file validation.
        /// </para>
        /// </remarks>
        /// <seealso cref="OnCanOpenContent(string)"/>
        public bool CanOpenContent(string filePath)
        {
#pragma warning disable IDE0046 // Convert to conditional expression
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            return string.IsNullOrWhiteSpace(filePath) ? throw new ArgumentEmptyException(nameof(filePath)) : OnCanOpenContent(filePath);
#pragma warning restore IDE0046 // Convert to conditional expression
        }


        /// <summary>
        /// Function to retrieve a content import
        /// </summary>        
        /// <returns>A new <see cref="IEditorContent"/> object.</returns>
        /// <exception cref="GorgonException">Thrown if the <see cref="OnCreateImporter"/> method returns <b>null</b>.</exception>
        /// <remarks>
        /// <para>
        /// This method creates a new instance, or retrieves an existing instance of the <see cref="IEditorContentImporter"/> used to perform the file import.
        /// </para>
        /// <para>
        /// This method calls the <see cref="OnCreateImporter()"/> method to perform the creation of the importer object.
        /// </para>
        /// </remarks>
        /// <seealso cref="OnCreateImporter()"/>.
        public IEditorContentImporter GetImporter()
        {
            IEditorContentImporter result = _importerFactory.Value;

            return result ?? throw new GorgonException(GorgonResult.CannotCreate, string.Format(Resources.GOREDIT_ERR_NO_CONTENT_IMPORTER_FROM_PLUGIN, Name));
        }

        /// <summary>
        /// Function to perform any required clean up for the plugin.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method provides the means to perform clean up of internal resources for the plug in.
        /// </para>
        /// <para>
        /// Content plug ins inheriting from this base type can provide custom initialization by implementing the <see cref="OnShutdown"/> method.        
        /// </para>
        /// <para>
        /// This method will only be called during unloading of the plug in (typically when the host application shuts down).
        /// </para>
        /// <para>
        /// <note type="important">
        /// <para>
        /// This method is for external use, and is only used by the host application. Do not call this method.
        /// </para>
        /// </note>
        /// </para>
        /// </remarks>
        public void Shutdown()
        {
            if (Interlocked.Exchange(ref _initialized, 0) == 0)
            {
                return;
            }

            OnShutdown();
            ProjectClosed();

            HostServices = null;
        }

        /// <summary>
        /// Function to perform any required initialization for the plugin.
        /// </summary>
        /// <param name="hostServices">The services passed from the host application to the plug in.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="hostServices"/> parameter is <b>null</b>.</exception> 
        /// <remarks>
        /// <para>
        /// This method provides the means to initialize the content importer plug in and passes functionality from the host application to the plug in.
        /// </para>
        /// <para>
        /// Content plug ins inheriting from this base type can provide custom initialization by implementing the <see cref="OnInitialize"/> method.
        /// </para>
        /// <para>
        /// This method will only be called once when the plug in is loaded. It cannot and should not be called after it has been called during load. 
        /// </para>
        /// <para>
        /// <note type="important">
        /// <para>
        /// This method is for external use, and is only used by the host application. Do not call this method.
        /// </para>
        /// </note>
        /// </para>
        /// </remarks>
        public void Initialize(IHostContentServices hostServices)
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

            HostContentServices = hostServices ?? throw new ArgumentNullException(nameof(hostServices));
            HostServices.Log.Print($"Initializing {Name}...", LoggingLevel.Simple);
            OnInitialize();
        }
        #endregion

        #region Constructor/Finalizer.
        /// <summary>Initializes a new instance of the <see cref="ContentImportPlugIn"/> class.</summary>
        /// <param name="description">Optional description of the plugin.</param>
        protected ContentImportPlugIn(string description)
            : base(description) => _importerFactory = new Lazy<IEditorContentImporter>(OnCreateImporter, true);
        #endregion
    }
}
