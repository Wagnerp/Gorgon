﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18052
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GorgonLibrary.IO.GorPack.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GorgonLibrary.IO.GorPack.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot read beyond the beginning of the stream..
        /// </summary>
        internal static string GORFS_BOS {
            get {
                return ResourceManager.GetString("GORFS_BOS", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A provider to mount a v1.x Gorgon pack file as a file system..
        /// </summary>
        internal static string GORFS_DESC {
            get {
                return ResourceManager.GetString("GORFS_DESC", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot read beyond the end of the stream..
        /// </summary>
        internal static string GORFS_EOS {
            get {
                return ResourceManager.GetString("GORFS_EOS", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Gorgon Packed Files (*.gorPack).
        /// </summary>
        internal static string GORFS_FILE_DESC {
            get {
                return ResourceManager.GetString("GORFS_FILE_DESC", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A file is corrupted or empty in the index table..
        /// </summary>
        internal static string GORFS_FILEINDEX_CORRUPT {
            get {
                return ResourceManager.GetString("GORFS_FILEINDEX_CORRUPT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The parameter must not be empty..
        /// </summary>
        internal static string GORFS_PARAMETER_MUST_NOT_BE_EMPTY {
            get {
                return ResourceManager.GetString("GORFS_PARAMETER_MUST_NOT_BE_EMPTY", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Gorgon 1.x BZip2 compressed packed file..
        /// </summary>
        internal static string GORFS_PLUGIN_DESC {
            get {
                return ResourceManager.GetString("GORFS_PLUGIN_DESC", resourceCulture);
            }
        }
    }
}
