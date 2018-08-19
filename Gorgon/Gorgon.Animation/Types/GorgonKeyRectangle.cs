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
// Created: August 18, 2018 8:13:55 PM
// 
#endregion

using System;
using DX = SharpDX;

namespace Gorgon.Animation
{
    /// <summary>
    /// A key used to update a rectangle value in an object.
    /// </summary>
    public class GorgonKeyRectangle
        : IGorgonKeyFrame
    {
        #region Variables.
        // The value for the key.
        private DX.RectangleF _value;
        #endregion

        #region Properties.
        /// <summary>
        /// Property to return the value for the key frame.
        /// </summary>
        public ref DX.RectangleF Value => ref _value;

        /// <summary>
        /// Property to return the time for the key frame in the animation.
        /// </summary>
        public float Time
        {
            get;
        }

        /// <summary>
        /// Property to return the type of data for this key frame.
        /// </summary>
        public Type DataType
        {
            get;
        } = typeof(DX.RectangleF);
        #endregion

        #region Methods.
        /// <summary>
        /// Function to clone an object.
        /// </summary>
        /// <returns>The cloned object.</returns>
        public IGorgonKeyFrame Clone()
        {
            return new GorgonKeyRectangle(Time, Value);
        }
        #endregion

        #region Constructor/Finalizer.
        /// <summary>
        /// Initializes a new instance of the <see cref="GorgonKeyRectangle" /> class.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <param name="value">The value.</param>
        public GorgonKeyRectangle(float time, DX.Size2F value)
        {
            Time = time;
            Value = new DX.RectangleF(0, 0, value.Width, value.Height);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GorgonKeyRectangle" /> class.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <param name="value">The value.</param>
        public GorgonKeyRectangle(float time, DX.RectangleF value)
        {
            Time = time;
            Value = value;
        }
        #endregion
    }
}
