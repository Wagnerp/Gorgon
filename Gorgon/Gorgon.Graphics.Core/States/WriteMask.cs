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
// Created: May 29, 2018 8:51:27 AM
// 
#endregion

using System;

namespace Gorgon.Graphics.Core
{
    /// <summary>
    /// Defines the type of logical operations to perform while blending a render target.
    /// </summary>
    [Flags]
    public enum WriteMask
    {
        /// <summary>The red channel will be written.</summary>
        Red = SharpDX.Direct3D11.ColorWriteMaskFlags.Red,
        /// <summary>The green channel will be written.</summary>
        Green = SharpDX.Direct3D11.ColorWriteMaskFlags.Green,
        /// <summary>The blue channel will be written.</summary>
        Blue = SharpDX.Direct3D11.ColorWriteMaskFlags.Blue,
        /// <summary>The alpha channel will be written.</summary>
        Alpha = SharpDX.Direct3D11.ColorWriteMaskFlags.Alpha,
        /// <summary>All channels will be written.</summary>
        All = SharpDX.Direct3D11.ColorWriteMaskFlags.All
    }
}