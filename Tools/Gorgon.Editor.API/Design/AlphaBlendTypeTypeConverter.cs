﻿#region MIT.
// 
// Gorgon.
// Copyright (C) 2014 Michael Winsor
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
// Created: Thursday, November 20, 2014 12:57:34 AM
// 
#endregion

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using GorgonLibrary.Graphics;

namespace GorgonLibrary.Editor.Design
{
	/// <summary>
	/// Type converter for the alpha blend type enumeration.
	/// </summary>
	public class AlphaBlendTypeTypeConverter
		: BlendTypeTypeConverter
	{
		#region Methods.
		/// <summary>
		/// Gets a collection of standard values for the data type this validator is designed for.
		/// </summary>
		/// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext" /> that provides a format context.</param>
		/// <returns>
		/// A <see cref="T:System.ComponentModel.TypeConverter.StandardValuesCollection" /> that holds a standard set of valid values, or null if the data type does not support a standard set of values.
		/// </returns>
		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			return
				new StandardValuesCollection(
					((BlendType[])Enum.GetValues(typeof(BlendType))).Where(item =>
					                                                       item != BlendType.Unknown && item != BlendType.DestinationColor &&
					                                                       item != BlendType.InverseDestinationColor
					                                                       && item != BlendType.SourceColor && item != BlendType.InverseSourceColor &&
					                                                       item != BlendType.InverseSecondarySourceColor
					                                                       && item != BlendType.SecondarySourceColor).ToArray());
		}
		#endregion
	}
}
