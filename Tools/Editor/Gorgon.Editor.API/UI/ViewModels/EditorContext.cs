﻿#region MIT
// 
// Gorgon.
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
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// Created: February 23, 2020 4:07:56 PM
// 
#endregion

using Gorgon.Editor.PlugIns;

namespace Gorgon.Editor.UI
{
    /// <summary>
    /// A view model for setting the context for an editor operation.
    /// </summary>
    /// <typeparam name="T">The type of parameters to inject into the view model. Must implement <see cref="IViewModelInjection{T}"/></typeparam>
    public abstract class EditorContext<T>
        : ViewModelBase<T, IHostContentServices>, IEditorContext
        where T : class, IViewModelInjection<IHostContentServices>
    {
        /// <summary>Property to return the context name.</summary>
        /// <remarks>This value is used as a unique ID for the context.</remarks>
        public abstract string Name
        {
            get;
        }
    }
}
