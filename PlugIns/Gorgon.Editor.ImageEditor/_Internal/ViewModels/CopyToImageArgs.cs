﻿
// 
// Gorgon
// Copyright (C) 2020 Michael Winsor
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE
// 
// Created: February 12, 2020 6:29:10 PM
// 


using System.ComponentModel;

namespace Gorgon.Editor.ImageEditor.ViewModels;

/// <summary>
/// Arguments for the <see cref="IImageContent.CopyToImage"/> command
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="CopyToImageArgs"/> class.</remarks>
/// <param name="filePaths">The file paths.</param>
/// <param name="thumbnailDpi">The thumbnail dpi.</param>
internal class CopyToImageArgs(IReadOnlyList<string> filePaths, float thumbnailDpi)
        : CancelEventArgs
{
    /// <summary>
    /// Property to return the paths to the files being copied into the image.
    /// </summary>
    public IReadOnlyList<string> ContentFilePaths
    {
        get;
    } = filePaths;

    /// <summary>
    /// Property to return the DPI for the thumbnails to use when displaying in the image picker.
    /// </summary>
    public float ThumbnailDpi
    {
        get;
    } = thumbnailDpi;
}
