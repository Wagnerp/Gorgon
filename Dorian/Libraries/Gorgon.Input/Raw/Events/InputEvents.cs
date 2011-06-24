#region MIT.
// 
// Gorgon.
// Copyright (C) 2011 Michael Winsor
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
// Created: Friday, June 24, 2011 10:04:41 AM
// 
#endregion

using System;

namespace GorgonLibrary.Input.Raw
{
	/// <summary>
	/// Object representing event arguments for the raw input events.
	/// </summary>
	internal class RawInputEventArgs
		: EventArgs
	{
		#region Properties.
		/// <summary>
		/// Property to return the handle to the device that is receiving the event notification.
		/// </summary>
		public IntPtr Handle
		{
			get;
			private set;
		}

		/// <summary>
		/// Property to return the raw input data.
		/// </summary>
		public RawInputData Data
		{
			get;
			private set;
		}
		#endregion

		#region Constructor.
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="data">Raw input data to pass.</param>
		public RawInputEventArgs(RawInputData data)
		{
			Handle = data.Header.Device;
			Data = data;
		}
		#endregion
	}

	/// <summary>
	/// Delegate for a raw input event.
	/// </summary>
	/// <param name="sender">Object that sent the event.</param>
	/// <param name="e">Event arguments.</param>
	internal delegate void RawInputEventHandler(object sender, RawInputEventArgs e);
}
