﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2015 Michael Winsor
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
// Created: Saturday, July 18, 2015 4:38:03 PM
// 
#endregion

using Gorgon.Native;

namespace Gorgon.Input;

/// <summary>
/// A representation of the Raw Input data received from <c>WM_INPUT</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is a Gorgon friendly representation of the data received from the <c>WM_INPUT</c> window message. The data from Raw Input is parsed and placed in an instance of this type and sent to the 
/// appropriate <see cref="GorgonRawKeyboard"/> device object to be turned into state for that device. 
/// </para>
/// <para>
/// This type is not intended for use by applications.
/// </para>
/// </remarks>
public readonly struct GorgonRawHIDData
{
    #region Variables.
    /// <summary>
    /// A pointer to the device data received from Raw Input.
    /// </summary>
    public readonly GorgonPtr<byte> HidData;

    /// <summary>
    /// The size of an individual HID input, in bytes, within the <see cref="HidData"/>.
    /// </summary>
    public readonly int HIDDataSize;
    #endregion

    #region Properties.
    /// <summary>
    /// Property to return the number of HID inputs contained within the data.
    /// </summary>
    public int ItemCount => ((HidData == GorgonPtr<byte>.NullPtr) || (HIDDataSize == 0)) ? 0 : HidData.Length / HIDDataSize;
    #endregion

    #region Constructor.
    /// <summary>Initializes a new instance of the <see cref="GorgonRawHIDData" /> struct.</summary>
    /// <param name="data">The device data received from raw input.</param>
    /// <param name="size">The size of a single HID value within the data.</param>
    public GorgonRawHIDData(in GorgonPtr<byte> data, int size)
    {
        HidData = data;
        HIDDataSize = size;
    }
    #endregion
}
