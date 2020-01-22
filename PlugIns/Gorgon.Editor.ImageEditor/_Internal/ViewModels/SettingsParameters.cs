﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2019 Michael Winsor
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
// Created: April 20, 2019 5:20:34 PM
// 
#endregion

using System;
using Gorgon.Diagnostics;
using Gorgon.Editor.PlugIns;
using Gorgon.Editor.Rendering;
using Gorgon.Editor.Services;
using Gorgon.Editor.UI.ViewModels;
using Gorgon.PlugIns;

namespace Gorgon.Editor.ImageEditor
{
    /// <summary>
    /// Parameters to pass to the <see cref="ISettings"/> view model.
    /// </summary>
    internal class SettingsParameters
        : ViewModelInjection, IHostServices
    {
        #region Variables.
        // Services from the host/
        private readonly IHostContentServices _hostServices;
        #endregion

        #region Properties.
        /// <summary>
        /// Property to return the settings for the image editor plugin.
        /// </summary>
        public ImageEditorSettings Settings
        {
            get;
        }

        /// <summary>
        /// Property to return the content plug in service.
        /// </summary>
        public IContentPlugInService ContentPlugInService => _hostServices.ContentPlugInService;

        /// <summary>
        /// Property to return the codecs loaded into the system.
        /// </summary>
        public ICodecRegistry Codecs
        {
            get;
        }

        /// <summary>
        /// Property to return the plug in cache.
        /// </summary>
        public GorgonMefPlugInCache PlugInCache
        {
            get;
        }

        /// <summary>
        /// Property to return the file dialog service used to locate codec assemblies.
        /// </summary>
        public IFileDialogService CodecFileDialog
        {
            get;
        }

        /// <summary>Property to return the service that allows a conetnt plug in to access tool content plug ins.</summary>
        public IToolPlugInService ToolPlugInService => _hostServices.ToolPlugInService;

        /// <summary>Property to return the service used to browse through directories on the file system.</summary>
        public IFileSystemFolderBrowseService FolderBrowser => _hostServices.FolderBrowser;

        /// <summary>Property to return the clipboard handler for the view model.</summary>
        IClipboardService IHostServices.Clipboard => _hostServices.Clipboard;

        /// <summary>Property to return the graphics and the 2D renderer used by the host application.</summary>
        public IGraphicsContext GraphicsContext => _hostServices.GraphicsContext;
        #endregion

        #region Constructor/Finalizer.
        /// <summary>Initializes a new instance of the ImageContentVmParameters class.</summary>
        /// <param name="settings">The settings for the image editor.</param>
        /// <param name="codecs">The codecs loaded into the system.</param>
        /// <param name="codecDialog">The file dialog used to locate codec assemblies.</param>
        /// <param name="plugInCache">The cache for plug in assemblies.</param>
        /// <param name="hostServices">Common application services.</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <b>null</b>.</exception>
        public SettingsParameters(ImageEditorSettings settings, ICodecRegistry codecs, IFileDialogService codecDialog, GorgonMefPlugInCache plugInCache, IHostContentServices hostServices)            
            : base(hostServices)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
            CodecFileDialog = codecDialog ?? throw new ArgumentNullException(nameof(codecDialog));
            _hostServices = hostServices ?? throw new ArgumentNullException(nameof(hostServices));
            PlugInCache = plugInCache ?? throw new ArgumentNullException(nameof(plugInCache));            
        }
        #endregion
    }
}
