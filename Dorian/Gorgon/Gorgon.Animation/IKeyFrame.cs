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
// Created: Sunday, September 23, 2012 11:35:35 AM
// 
#endregion

using System;

namespace GorgonLibrary.Animation
{
	/// <summary>
	/// An animation key frame.
	/// </summary>
	public interface IKeyFrame
		: ICloneable<IKeyFrame>
	{
		#region Properties.
		/// <summary>
		/// Property to return the time at which the key frame is stored.
		/// </summary>
		float Time
		{
			get;
		}

		/// <summary>
		/// Property to return the type of data for this key frame.
		/// </summary>
		Type DataType
		{
			get;
		}
		#endregion

		#region Methods.
		/// <summary>
		/// Function to retrieve key frame data from data chunk.
		/// </summary>
		/// <param name="chunk">Chunk to read.</param>
		void FromChunk(GorgonLibrary.IO.GorgonChunkReader chunk);

		/// <summary>
		/// Function to send the key frame data to the data chunk.
		/// </summary>
		/// <param name="chunk">Chunk to write.</param>
		void ToChunk(GorgonLibrary.IO.GorgonChunkWriter chunk);
		#endregion
	}
}
