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
// Created: December 23, 2018 3:38:25 PM
// 
#endregion

using System;

namespace Gorgon.Editor.Content
{
    /// <summary>
    /// Event parameters for the <see cref="OLDE_IContentFile.Renamed"/> event.
    /// </summary>
    public class ContentFileRenamedEventArgs
        : EventArgs
    {
        /// <summary>
        /// Property to return the new name of the node.
        /// </summary>
        public string NewName
        {
            get;
        }

        /// <summary>
        /// Property to return the old name of the node.
        /// </summary>
        public string OldName
        {
            get;
        }

        /// <summary>Initializes a new instance of the <see cref="T:Gorgon.Editor.Content.ContentFileRenamedEventArgs"/> class.</summary>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        public ContentFileRenamedEventArgs(string oldName, string newName)
        {
            OldName = oldName ?? string.Empty;
            NewName = newName ?? string.Empty;
        }
    }
}
