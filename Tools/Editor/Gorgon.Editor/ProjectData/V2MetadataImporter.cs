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
// Created: October 30, 2018 12:48:54 PM
// 
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Gorgon.Diagnostics;
using Gorgon.Editor.Metadata;
using Gorgon.IO;

namespace Gorgon.Editor.ProjectData;

/// <summary>
/// Handles importing of metadata from v2 of Gorgon's file structure.
/// </summary>
internal class V2MetadataImporter
{
    #region Constants.
    // The name of the root node in the metadata.
    private const string RootNodeName = "Gorgon.Editor.MetaData";
    // The name of the file node in the metadata.
    private const string FileNodeName = "File";
    // The name of the file path attribute.
    private const string FilePathAttr = "FilePath";

    /// <summary>
    /// The name of the v2 metadata file.
    /// </summary>
    public const string V2MetadataFilename = ".gorgon.editor.metadata";
    #endregion

    #region Variables.
    // The file containing the metadata.
    private readonly string _file;
    // The log interface for debug messages.
    private readonly IGorgonLog _log;
    #endregion

    #region Methods.
    /// <summary>
    /// Function to import the files in the metadata.
    /// </summary>
    /// <param name="project">The project to update.</param>
    /// <param name="rootNode">The root node of the metadata.</param>
    private void GetFiles(IProject project, XElement rootNode)
    {
        _log.Print("Importing file list.", LoggingLevel.Verbose);

        IEnumerable<XElement> fileNodes = rootNode.Descendants(FileNodeName);

        foreach (XElement fileNode in fileNodes)
        {
            string filePath = fileNode.Attribute(FilePathAttr)?.Value;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            string dirPath = Path.GetDirectoryName(filePath).FormatDirectory('/');

            // Add directories.
            if ((!string.IsNullOrWhiteSpace(dirPath)) && (!project.ProjectItems.ContainsKey(dirPath)))
            {
                project.ProjectItems.Add(dirPath, new ProjectItemMetadata());
                continue;
            }

            var metadata = new ProjectItemMetadata()
            {
                PlugInName = null
            };

            project.ProjectItems.Add(filePath, metadata);
        }
    }

    /// <summary>
    /// Function to perform the import of the metadata.
    /// </summary>
    /// <param name="project">The project to update.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="project"/>, or the <paramref name="contentPlugIns"/> parameter is <b>null</b>.</exception>
    public void Import(IProject project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (!File.Exists(_file))
        {
            return;
        }

        _log.Print("Importing v2 Gorgon Editor file metadata...", LoggingLevel.Simple);

        var document = XDocument.Load(_file);

        XElement rootNode = document.Element(RootNodeName);

        if (rootNode is null)
        {
            _log.Print("No root node found.  Not a v2 Gorgon Editor metadata file.", LoggingLevel.Verbose);
            return;
        }

        GetFiles(project, rootNode);

        try
        {
            // Delete the metadata file, we don't need it anymore.
            File.Delete(_file);
        }
        catch
        {
            // Do nothing if we can't delete the metadata file.
        }
        _log.Print("Imported v2 Gorgon Editor metadata.", LoggingLevel.Simple);
    }
    #endregion

    #region Constructor/Finalizer.
    /// <summary>Initializes a new instance of the V2MetadataImporter class.</summary>
    /// <param name="metadataFile">The file containing the v2 metadata.</param>
    /// <param name="log">The log interface for debug messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="metadataFile"/> parameter is <b>null</b>.</exception>
    public V2MetadataImporter(string metadataFile, IGorgonLog log)
    {
        _log = log ?? GorgonLog.NullLog;
        _file = metadataFile ?? throw new ArgumentNullException(nameof(metadataFile));            
    }
    #endregion
}
