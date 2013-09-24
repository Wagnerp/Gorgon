﻿#region MIT.
// 
// Gorgon.
// Copyright (C) 2013 Michael Winsor
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
// Created: Monday, September 23, 2013 10:28:11 AM
// 
#endregion

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Markup;
using System.Xml.Linq;
using GorgonLibrary.Editor.Properties;
using GorgonLibrary.IO;

namespace GorgonLibrary.Editor
{
    /// <summary>
    /// Interface used to import content files or load/save the current editor file.
    /// </summary>
    static class FileManagement
    {
        #region Constants.
        private const string MetaDataRootName = "Gorgon.Editor.MetaData";           // Name of the root node in the meta data.
        private const string MetaDataWriterPlugIn = "WriterPlugIn";                 // Name of the node that contains meta data for the plug-in writer..
        private const string MetaDataTypeName = "TypeName";                         // Name of attribute/element with type name information.
        private const string MetaDataFile = ".gorgon.editor.metadata";              // Meta data file name.
        private const string MetaDataFilePath = "/" + MetaDataFile;                 // Meta data file path.
        #endregion

        #region Classes.
		/// <summary>
		/// A case insensitive string comparer.
		/// </summary>
	    private class StringOrdinalCaseInsensitiveComparer
			: IEqualityComparer<string>
		{
			#region IEqualityComparer<string> Members
			/// <summary>
			/// Determines whether the specified objects are equal.
			/// </summary>
			/// <param name="x">The first object of type string to compare.</param>
			/// <param name="y">The second object of type string to compare.</param>
			/// <returns>
			/// true if the specified objects are equal; otherwise, false.
			/// </returns>
			public bool Equals(string x, string y)
			{
				return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
			}

			/// <summary>
			/// Returns a hash code for this instance.
			/// </summary>
			/// <param name="obj">The object.</param>
			/// <returns>
			/// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
			/// </returns>
			public int GetHashCode(string obj)
			{
				return obj.ToUpperInvariant().GetHashCode();
			}
			#endregion
		}

        /// <summary>
        /// A case insensitive comparer for file extensions.
        /// </summary>
        private class FileExtensionComparer
            : IEqualityComparer<GorgonFileExtension>
        {
            #region IEqualityComparer<FileExtension> Members
            /// <summary>
            /// Determines whether the specified objects are equal.
            /// </summary>
            /// <param name="x">The first object of type string to compare.</param>
            /// <param name="y">The second object of type string to compare.</param>
            /// <returns>
            /// true if the specified objects are equal; otherwise, false.
            /// </returns>
            public bool Equals(GorgonFileExtension x, GorgonFileExtension y)
            {
                return GorgonFileExtension.Equals(ref x, ref y);
            }

            /// <summary>
            /// Returns a hash code for this instance.
            /// </summary>
            /// <param name="obj">The object.</param>
            /// <returns>
            /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
            /// </returns>
            public int GetHashCode(GorgonFileExtension obj)
            {
                return obj.GetHashCode();
            }
            #endregion
        }
        #endregion

        #region Variables.
        private readonly static HashSet<string> _blockedFiles;
        private readonly static Dictionary<GorgonFileExtension, GorgonFileSystemProvider> _readerFiles;
        private readonly static Dictionary<GorgonFileExtension, FileWriterPlugIn> _writerFiles;
        private readonly static XElement _metaDataRootNode = new XElement(MetaDataRootName);
        private static XDocument _metaDataXML;
        #endregion

        #region Properties.
        /// <summary>
        /// Property to set or return whether the file has changed or not.
        /// </summary>
        public static bool FileChanged
        {
            get;
            set;
        }

        /// <summary>
        /// Property to set or return the name of the file.
        /// </summary>
        public static string Filename
        {
            get;
            set;
        }

        /// <summary>
        /// Property to set or return the path to the file.
        /// </summary>
        public static string FilePath
        {
            get;
            set;
        }
        #endregion

        #region Methods.
        /// <summary>
        /// Function to retrieve the meta data for the scratch area files.
        /// </summary>
        /// <returns>The XML document containing the metadata, or NULL (Nothing in VB.Net) if no meta data was found.</returns>
        private static XDocument GetMetaData()
        {
            if (_metaDataXML != null)
            {
                return _metaDataXML;
            }

            _metaDataXML = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), _metaDataRootNode);

            // If we have no files, then there's no metadata.
            if ((ScratchArea.ScratchFiles == null)
                || ((ScratchArea.ScratchFiles.RootDirectory.Directories.Count == 0)
                    && (ScratchArea.ScratchFiles.RootDirectory.Files.Count == 0)))
            {
                return _metaDataXML;
            }

            var file = ScratchArea.ScratchFiles.GetFile(MetaDataFilePath);

            if (file == null)
            {
                return _metaDataXML;
            }

            XDocument metaDataFile;

            using (var metaStream = file.OpenStream(false))
            {
                metaDataFile = XDocument.Load(metaStream);
            }

            // If this file is invalid, then do not return it.
            if (!metaDataFile.Descendants(_metaDataRootNode.Name).Any())
            {
                return _metaDataXML;
            }

            _metaDataXML = metaDataFile;

            return _metaDataXML;
        }
        
        /// <summary>
        /// Function to retrieve the file types that can be read by the available file system providers.
        /// </summary>
        /// <param name="provider">File system provider to evaluate.</param>
        private static void GetReaderFileTypes(GorgonFileSystemProvider provider)
        {
            foreach (var extension in provider.PreferredExtensions)
            {
                _readerFiles[extension] = provider;
            }
        }

        /// <summary>
        /// Function to retrieve the file types that can be written by the available file system writers.
        /// </summary>
        /// <param name="plugIn">File system writer plug-in.</param>
        private static void GetWriterFileTypes(FileWriterPlugIn plugIn)
        {
            foreach (var extension in plugIn.FileExtensions)
            {
                _writerFiles[extension] = plugIn;
            }
        }

        /// <summary>
        /// Function to reset the file data.
        /// </summary>
        private static void ResetFile()
        {
            ScratchArea.DestroyScratchArea();
            ScratchArea.InitializeScratch();
            _metaDataXML = null;
        }

		/// <summary>
		/// Function to return whether or not a file is in the blocked list.
		/// </summary>
		/// <param name="file">File to check.</param>
		/// <returns>TRUE if blocked, FALSE if not.</returns>
	    public static bool IsBlocked(GorgonFileSystemFileEntry file)
		{
			return _blockedFiles.Contains(file.Name);
		}

        /// <summary>
        /// Function to initialize the file types available to the application.
        /// </summary>
        public static void InitializeFileTypes()
        {
            // Get reader extensions
            foreach (var readerProvider in ScratchArea.ScratchFiles.Providers)
            {
                GetReaderFileTypes(readerProvider);
            }

            // Get writer extensions
            foreach (var writerPlugIn in PlugIns.WriterPlugIns)
            {
                GetWriterFileTypes(writerPlugIn.Value);
            }
        }

        /// <summary>
        /// Function to retrieve the list of file writer extensions.
        /// </summary>
        /// <returns>A list of writable file name extensions.</returns>
        public static IEnumerable<GorgonFileExtension> GetWriterExtensions()
        {
            return _writerFiles.Keys;
        }

        /// <summary>
        /// Function to retrieve the list of available reader extensions.
        /// </summary>
        /// <returns>A list of readable file name extensions.</returns>
        public static IEnumerable<GorgonFileExtension> GetReaderExtensions()
        {
            return _readerFiles.Keys;
        }

        /// <summary>
        /// Function to assign the specified writer plug-in as current in the meta data.
        /// </summary>
        /// <param name="plugIn">Plug-in to assign.</param>
        public static void SetWriterPlugIn(FileWriterPlugIn plugIn)
        {
            XDocument tempMetaData = GetMetaData();

            if (tempMetaData == null)
            {
                return;
            }

            XElement rootNode = tempMetaData.Descendants(MetaDataRootName).FirstOrDefault();
            
            // If we cannot find the root node in the metadata, then leave.
            if (rootNode == null)
            {
                return;
            }

            XElement writerNode = tempMetaData.Descendants(MetaDataWriterPlugIn).FirstOrDefault();

            // If passing NULL, then remove the setting from the metadata.
            if (plugIn == null)
            {
                if (writerNode == null)
                {
                    return;
                }

                writerNode.Remove();
            }
            else
            {
                if (writerNode == null)
                {
                    rootNode.Add(new XElement(MetaDataWriterPlugIn,
                                              new XAttribute(MetaDataTypeName, plugIn.GetType().FullName)));
                }
                else
                {
                    XAttribute type = writerNode.Attribute(MetaDataTypeName);

                    if (type == null)
                    {
                        writerNode.Add(new XAttribute(MetaDataTypeName, plugIn.GetType().FullName));
                    }
                    else
                    {
                        type.Value = plugIn.GetType().FullName;
                    }
                }
            }
        }

        /// <summary>
        /// Function to find a writer plug-in for a given file name extension.
        /// </summary>
        /// <param name="fileExtension">Full file name or extension of the file to write.</param>
        /// <returns>The plug-in used to write the file.</returns>
        public static FileWriterPlugIn GetWriterPlugIn(string fileExtension = null)
        {
            XDocument tempMetaData = GetMetaData();
            FileWriterPlugIn result = null;

            // If we have meta-data, then use that to determine which file writer is used.
            if (tempMetaData != null)
            {
                // Read the meta data for the file.
                var plugInElement = tempMetaData.Descendants(MetaDataWriterPlugIn).FirstOrDefault();

                if (plugInElement != null)
                {
                    // Ensure this is properly formed.
                    if ((plugInElement.HasAttributes)
                        && (plugInElement.Attribute(MetaDataTypeName) != null)
                        && (!string.IsNullOrWhiteSpace(plugInElement.Attribute(MetaDataTypeName).Value)))
                    {
                        result = (from plugIn in PlugIns.WriterPlugIns
                                  where string.Equals(plugIn.Value.GetType().FullName,
                                                      plugInElement.Attribute(MetaDataTypeName).Value,
                                                      StringComparison.OrdinalIgnoreCase)
                                  select plugIn.Value).FirstOrDefault();
                    }
                }

                if (result != null)
                {
                    return result;
                }
            }

            // We did not find a file writer in the meta data, try to derive which plug-in to use from the extension.
            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                // We didn't give an extension, try and take it from the file name.
                fileExtension = Path.GetExtension(Path.GetExtension(Filename));
            }

            // If we passed in a full file name, then get its extension.
            if ((!string.IsNullOrWhiteSpace(fileExtension)) && (fileExtension.IndexOf('.') > 0))
            {
                fileExtension = Path.GetExtension(fileExtension);
            }
            
            var extension = new GorgonFileExtension(fileExtension);
            
            // Try to find the plug-in.
            _writerFiles.TryGetValue(extension, out result);

            return result;
        }

        /// <summary>
        /// Function to create a new file.
        /// </summary>
        public static void New()
        {
            // Initialize the scratch area.
            ResetFile();

            Filename = "Untitled";
            FilePath = string.Empty;

            Program.Settings.LastEditorFile = string.Empty;

            FileChanged = false;
        }

        /// <summary>
        /// Function to save the editor file.
        /// </summary>
        /// <param name="path">Path to the new file.</param>
        /// <param name="plugIn">The plug-in used to save the file.</param>
        public static void Save(string path, FileWriterPlugIn plugIn)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(Resources.GOREDIT_PARAMETER_MUST_NOT_BE_EMPTY, "path");
            }

            // We don't have a writer plug-in, at this point, that's not good.
            if (plugIn == null)
            {
                throw new IOException(string.Format(Resources.GOREDIT_NO_WRITER_PLUGIN, path));
            }

            // Write the meta data file to the file system.
            XDocument metaData = GetMetaData();
            using (var metaDataStream = ScratchArea.ScratchFiles.OpenStream(MetaDataFilePath, true))
            {
                metaData.Save(metaDataStream);
            }

            // Write the file.
            if (!plugIn.Save(path))
            {
                return;
            }

            Filename = Path.GetFileName(path);
            FilePath = path;
            Program.Settings.LastEditorFile = path;

            // Remove all changed items.
            FileChanged = false;
        }

        /// <summary>
        /// Function to open the editor file.
        /// </summary>
        /// <param name="path">Path to the editor file.</param>
        public static void Open(string path)
        {
            var packFileSystem = new GorgonFileSystem();

            FileChanged = false;

            // Add the new file system as a mount point.
            packFileSystem.Providers.LoadAllProviders();

            if (!packFileSystem.Providers.Any(item => item.CanReadFile(path)))
            {
                throw new FileLoadException(string.Format(Resources.GOREDIT_NO_PROVIDERS_TO_READ_FILE,
                                                          Path.GetFileName(path)));
            }

            packFileSystem.Mount(path);

            try
            {
                // Remove our previous scratch data.
                ResetFile();

                // At this point we should have a clean scratch area, so all files will exist in the packed file.
                // Unpack the file structure so we can work with it.
                var directories = packFileSystem.FindDirectories("*", true);
                var files = packFileSystem.FindFiles("*", true);

                // Create our directories.
                foreach (var directory in directories)
                {
                    ScratchArea.ScratchFiles.CreateDirectory(directory.FullPath);
                }

                // Copy our files.
                foreach (var file in files)
                {
                    using (var inputStream = packFileSystem.OpenStream(file, false))
                    {
                        using (var outputStream = ScratchArea.ScratchFiles.OpenStream(file.FullPath, true))
                        {
                            inputStream.CopyTo(outputStream);
                        }
                    }
                }
                
                FilePath = string.Empty;
                Filename = Path.GetFileName(path);
                Program.Settings.LastEditorFile = path;

                // If we can't write the file, then leave the editor file path as blank.
                // If the file path is blank, then the Save As function will be triggered if we attempt to save so we 
                // can save it in a format that we DO understand.  This is of course assuming we have any plug-ins loaded
                // that will allow us to save.
                if (GetWriterPlugIn(path) == null)
                {
                    FilePath = path;
                }
            }
            catch
            {
                // We have a problem, reset whatever changes we've made.
                ResetFile();
                throw;
            }
            finally
            {
                // At this point we don't need this file system any more.  We'll 
                // be using our scratch system instead.
                packFileSystem.Clear();
            }
        }
        #endregion

        #region Constructor/Destructor.
        /// <summary>
        /// Initializes the <see cref="FileManagement"/> class.
        /// </summary>
        static FileManagement()
        {
            _readerFiles = new Dictionary<GorgonFileExtension, GorgonFileSystemProvider>(new FileExtensionComparer());
            _writerFiles = new Dictionary<GorgonFileExtension, FileWriterPlugIn>(new FileExtensionComparer());

            // Create default metadata.
            _metaDataXML = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), _metaDataRootNode);

	        _blockedFiles = new HashSet<string>(new[]
	                                            {
		                                            MetaDataFile
	                                            }, new StringOrdinalCaseInsensitiveComparer());

            Filename = "Untitled";
            FilePath = string.Empty;
        }
        #endregion
    }
}
