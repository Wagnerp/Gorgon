﻿#region MIT.
// 
// Gorgon.
// Copyright (C) 2012 Michael Winsor
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
// Created: Wednesday, October 3, 2012 9:16:10 PM
// 
#endregion

using System;
using Gorgon.Graphics;

namespace Gorgon.Animation
{
    /// <summary>
    /// A key frame that manipulates a GorgonColor data type.
    /// </summary>
    public class GorgonKeyGorgonColor
		: IGorgonKeyFrame
	{
		#region Variables.
        // The value for the key frame.
	    private GorgonColor _value = GorgonColor.White;
        #endregion

        #region Properties.
	    /// <summary>
	    /// Property to set or return the value to store in the key frame.
	    /// </summary>
	    public ref GorgonColor Value => ref _value;

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
	    } = typeof(GorgonColor);
		#endregion

        #region Methods.
	    /// <summary>
	    /// Function to clone an object.
	    /// </summary>
	    /// <returns>The cloned object.</returns>
	    public IGorgonKeyFrame Clone()
	    {
	        return new GorgonKeyGorgonColor(Time, Value);
	    }
        #endregion

		#region Constructor/Destructor.
		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonKeyGorgonColor" /> struct.
		/// </summary>
		/// <param name="time">The time for the key frame.</param>
		/// <param name="value">The value to apply to the key frame.</param>
		public GorgonKeyGorgonColor(float time, GorgonColor value)
		{
			Time = time;
			Value = value;
		}

        #endregion
    }
}
