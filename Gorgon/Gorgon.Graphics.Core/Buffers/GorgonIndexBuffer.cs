﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2016 Michael Winsor
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
// Created: June 15, 2016 9:33:57 PM
// 
#endregion

using System;
using Gorgon.Core;
using D3D11 = SharpDX.Direct3D11;
using Gorgon.Diagnostics;
using Gorgon.Graphics.Core.Properties;
using Gorgon.Native;
using DX = SharpDX;
using DXGI = SharpDX.DXGI;

namespace Gorgon.Graphics.Core
{
	/// <summary>
	/// A buffer for indices used to look up vertices within a <see cref="GorgonVertexBuffer"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use a index buffer to send indices to the GPU. These indices are used for allowing smaller vertex buffers and providing a faster means of finding vertices to draw on the GPU.
	/// </para>
	/// <para>
	/// To send indices to the GPU using a index buffer, an application can upload a value type values, representing the indices, to the buffer using one of the 
	/// <see cref="O:Gorgon.Graphics.GorgonIndexBuffer.Update{T}(ref T)">Update&lt;T&gt;</see> overloads. For best performance, it is recommended to upload index data only once, or rarely. However, in 
	/// some scenarios, and with the correct <see cref="IGorgonIndexBufferInfo.Usage"/> flag, indices can be updated regularly for things like dynamic tesselation of surface.
	/// </para>
	/// <para> 
	/// <example language="csharp">
	/// For example, to send a list of indices to a index buffer:
	/// <code language="csharp">
	/// <![CDATA[
	/// GorgonGraphics graphics;
	/// ushort[] _indices = new ushort[100];
	/// GorgonIndexBuffer _indexBuffer;
	/// 
	/// void InitializeIndexBuffer()
	/// {
	///		_indices = ... // Fill your index array here.
	/// 
	///		// Create the index buffer large enough so that it'll hold all 100 indices.
	///     // Unlike other buffers, we're passing the number of indices instead of bytes. 
	///     // This is because we can determine the number of bytes by whether we're using 
	///     // 16 bit indices (we are) and the index count.
	///		_indexBuffer = new GorgonIndexBuffer("MyIB", graphics, new GorgonIndexBufferInfo
	///	                                                               {
	///		                                                              IndexCount = _indices.Length
	///                                                                });
	/// 
	///		// Copy our data to the index buffer.
	///		_indexBuffer.Update(_indices);
	/// }
	/// ]]>
	/// </code>
	/// </example>
	/// </para>
	/// </remarks>
	public sealed class GorgonIndexBuffer
		: GorgonBufferBase
	{
		#region Variables.
		// The information used to create the buffer.
		private readonly GorgonIndexBufferInfo _info;
		// The address returned by the lock on the buffer.
		private GorgonPointerAlias _lockAddress;
		// The size of an individual index.
		private readonly int _indexSize;
		#endregion

		#region Properties.
		/// <summary>
		/// Property to return the format of the buffer data when binding.
		/// </summary>
		internal DXGI.Format IndexFormat => Info.Use16BitIndices ? DXGI.Format.R16_UInt : DXGI.Format.R32_UInt;

		/// <summary>
		/// Property used to return the information used to create this buffer.
		/// </summary>
		public IGorgonIndexBufferInfo Info => _info;

		/// <summary>
		/// Property to return whether this index buffer is locked for reading/writing or not.
		/// </summary>
		public bool IsLocked
		{
			get;
			private set;
		}
		#endregion

		#region Methods.
		/// <summary>
		/// Function to initialize the buffer data.
		/// </summary>
		/// <param name="initialData">The initial data used to populate the buffer.</param>
		private void Initialize(IGorgonPointer initialData)
		{
			D3D11.CpuAccessFlags cpuFlags = D3D11.CpuAccessFlags.None;

			switch (_info.Usage)
			{
				case D3D11.ResourceUsage.Staging:
					cpuFlags = D3D11.CpuAccessFlags.Read | D3D11.CpuAccessFlags.Write;
					break;
				case D3D11.ResourceUsage.Dynamic:
					cpuFlags = D3D11.CpuAccessFlags.Write;
					break;
			}

			Log.Print($"{Name} Index Buffer: Creating D3D11 buffer. Size: {SizeInBytes} bytes", LoggingLevel.Simple);

			var desc  = new D3D11.BufferDescription
			{
				SizeInBytes = Info.IndexCount * _indexSize,
				Usage = _info.Usage,
				BindFlags = D3D11.BindFlags.IndexBuffer,
				OptionFlags = D3D11.ResourceOptionFlags.None,
				CpuAccessFlags = cpuFlags,
				StructureByteStride = 0
			};

			if ((initialData != null) && (initialData.Size > 0))
			{
				D3DResource = D3DBuffer = new D3D11.Buffer(Graphics.VideoDevice.D3DDevice(), new IntPtr(initialData.Address), desc);
			}
			else
			{
				D3DResource = D3DBuffer = new D3D11.Buffer(Graphics.VideoDevice.D3DDevice(), desc);
			}

			SizeInBytes = desc.SizeInBytes;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If the index buffer is locked when this method is called, it will automatically be unlocked and any lock pointer will be invalidated.
		/// </para>
		/// <para>
		/// Objects that override this method should be sure to call this base method or else a memory leak may occur.
		/// </para>
		/// </remarks>
		public override void Dispose()
		{
			// If we're locked, then unlock the buffer before destroying it.
			if ((IsLocked) && (_lockAddress != null) && (!_lockAddress.IsDisposed))
			{
				Unlock(ref _lockAddress);

				// Because the pointer is an alias, we don't really NEED to call this, but just for consistency we'll do so anyway.
				_lockAddress.Dispose();
			}

			base.Dispose();
		}

		/// <summary>
		/// Function to unlock a previously locked index buffer.
		/// </summary>
		/// <param name="lockPointer">The pointer returned by the <see cref="Lock"/> method.</param>
		/// <exception cref="ArgumentException">Thrown when the <paramref name="lockPointer"/> was not created by the <see cref="Lock"/> method on this instance.</exception>
		/// <remarks>
		/// <para>
		/// Use this to unlock this buffer when it was previously locked by the <see cref="Lock"/> method. Buffers that were previously locked must always call this method or else the data passed to the 
		/// buffer will not be updated on the GPU. If the buffer was not locked, then this method does nothing. 
		/// </para>
		/// <para>
		/// The <paramref name="lockPointer"/> passed to this method is passed by reference so that it will be invalidated back to the calling application to avoid issues with reuse of an invalid pointer. 
		/// </para>
		/// <para>
		/// If the <paramref name="lockPointer"/> is was not created by this instance, then an exception will be thrown.
		/// </para>
		/// <para>
		/// <note type="warning">
		/// <para>
		/// For performance reasons, exceptions raised by this method will only be done so when Gorgon is compiled as DEBUG.
		/// </para>
		/// </note>
		/// </para>
		/// </remarks>
		/// <seealso cref="Lock"/>
		public void Unlock(ref GorgonPointerAlias lockPointer)
		{
			if ((!IsLocked) || (lockPointer == null))
			{
				return;
			}

#if DEBUG
			if (lockPointer != _lockAddress)
			{
				throw new ArgumentException(Resources.GORGFX_ERR_BUFFER_LOCK_NOT_VALID, nameof(lockPointer));
			}
#endif

			Graphics.D3DDeviceContext.UnmapSubresource(D3DBuffer, 0);

			// Reset the lock pointer back to null so applications can't reuse it.
			lockPointer = null;
			IsLocked = false;
		}

		/// <summary>
		/// Function to lock a index buffer for reading or writing (depending on <see cref="IGorgonIndexBufferInfo.Usage"/>).
		/// </summary>
		/// <param name="mode">The type of access to the index buffer data.</param>
		/// <returns>A <see cref="GorgonPointerAlias"/> used to read or write the data in the index buffer.</returns>
		/// <exception cref="NotSupportedException">Thrown when if buffer does not have a <see cref="IGorgonIndexBufferInfo.Usage"/> of <c>Dynamic</c> or <c>Staging</c>.
		/// <para>-or-</para>
		/// <para>Thrown when if buffer does not have a <see cref="IGorgonIndexBufferInfo.Usage"/> of <c>Staging</c>, and the <paramref name="mode"/> is set to <c>Read</c> or <c>ReadWrite</c>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">Thrown when the buffer is already locked.</exception>
		/// <remarks>
		/// <para>
		/// This will lock the buffer so that the CPU can access the data within it. Because locks/unlocks can potentially be performance intensive, it is best practice to lock the buffer, do the work and 
		/// <see cref="Unlock"/> immediately. Holding a lock for a long time may cause performance issues.
		/// </para>
		/// <para>
		/// Unlike the <see cref="O:Gorgon.Graphics.GorgonIndexBuffer.Update{T}">Update&lt;T&gt;</see> methods, this allows the CPU to change portions of the buffer every frame with little performance 
		/// penalty (this, of course, is dependent upon drivers, hardware, etc...). It also allows reading from the buffer if it was created with a <see cref="IGorgonIndexBufferInfo.Usage"/> of 
		/// <c>Staging</c>.
		/// </para>
		/// <para>
		/// When the lock method returns, it returns a <see cref="GorgonPointerAlias"/> containing a pointer to the CPU memory that contains the index buffer data. Applications can use this to access the 
		/// index buffer data.
		/// </para>
		/// <para>
		/// The lock access is affected by the <paramref name="mode"/> parameter. A value of <c>Read</c> or <c>ReadWrite</c> will allow read access to the buffer data but only if the buffer has a 
		/// <see cref="IGorgonIndexBufferInfo.Usage"/> of <c>Staging</c>. Applications can use one of the <c>Write</c> flags to write to the buffer. For <c>Dynamic</c> index buffers, it is ideal to use 
		/// <c>WriteNoOverwrite</c> to inform the GPU that you will not be overwriting parts of the buffer still being used for rendering by the GPU. If this cannot be guaranteed (for example, writing to 
		/// the beginning of the buffer at the start of a frame), then applications should use the <c>WriteDiscard</c> to instruct the GPU that the contents of the buffer are now invalidated and it will be 
		/// refreshed with new data entirely.
		/// </para>
		/// <para>
		/// <note type="important">
		/// <para>
		/// When a index buffer is locked, it <b><u>must</u></b> be unlocked with a call to <see cref="Unlock"/>. Failure to do so will impair performance greatly, and will keep the contents of the buffer 
		/// on the GPU from being updated.
		/// </para>
		/// </note>
		/// </para>
		/// <para>
		/// <note type="warning">
		/// <para>
		/// For performance reasons, exceptions raised by this method will only be done so when Gorgon is compiled as DEBUG.
		/// </para>
		/// </note>
		/// </para>
		/// </remarks>
		public GorgonPointerAlias Lock(D3D11.MapMode mode)
		{
#if DEBUG
			if ((Info.Usage != D3D11.ResourceUsage.Dynamic) && (Info.Usage != D3D11.ResourceUsage.Staging))
			{
				throw new NotSupportedException(string.Format(Resources.GORGFX_ERR_BUFFER_LOCK_NOT_DYNAMIC, Name, Info.Usage));	
			}

			if ((Info.Usage != D3D11.ResourceUsage.Staging) && ((mode == D3D11.MapMode.Read) || (mode == D3D11.MapMode.ReadWrite)))
			{
				throw new NotSupportedException(string.Format(Resources.GORGFX_BUFFER_ERR_WRITE_ONLY, Name, Info.Usage));
			}

			if (IsLocked)
			{
				throw new InvalidOperationException(Resources.GORGFX_ERR_BUFFER_ALREADY_LOCKED);
			}
#endif

			mode = D3D11.MapMode.WriteDiscard;

			Graphics.D3DDeviceContext.MapSubresource(D3DBuffer, mode, D3D11.MapFlags.None, out DX.DataStream stream);

			if (_lockAddress == null)
			{
				_lockAddress = new GorgonPointerAlias(stream.DataPointer, stream.Length);
			}
			else
			{
				_lockAddress.AliasPointer(stream.DataPointer, stream.Length);
			}

			stream.Dispose();

			IsLocked = true;
			return _lockAddress;
		}

		/// <summary>
		/// Function to update the buffer with data.
		/// </summary>
		/// <param name="data">An index value used to populate the buffer.</param>
		/// <param name="bufferOffset">[Optional] The number of bytes within this buffer to start writing at.</param>
		/// <exception cref="ArgumentException">Thrown when the <paramref name="bufferOffset"/> plus the size of the data exceeds the size of the buffer.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the size, in bytes, of the <paramref name="data"/> parameter is larger than the total <see cref="GorgonResource.SizeInBytes"/> of the buffer.
		/// <para>-or-</para>
		/// <para>Thrown when the <paramref name="bufferOffset"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="NotSupportedException">Thrown when the <see cref="IGorgonIndexBufferInfo.Usage"/> is either <c>Immutable</c> or <c>Dynamic</c>.</exception>
		/// <remarks>
		/// <para>
		/// This method will throw an exception when the buffer is created with a <see cref="IGorgonIndexBufferInfo.Usage"/> of <c>Immutable</c> or <c>Dynamic</c>.
		/// </para>
		/// <para>
		/// <note type="warning">
		/// <para>
		/// For performance reasons, exceptions raised by this method will only be done so when Gorgon is compiled as DEBUG.
		/// </para>
		/// </note>
		/// </para>
		/// </remarks>
		public void Update(ref int data, int bufferOffset = 0)
		{
#if DEBUG
			if ((Info.Usage == D3D11.ResourceUsage.Dynamic) || (Info.Usage == D3D11.ResourceUsage.Immutable))
			{
				throw new NotSupportedException(Resources.GORGFX_ERR_BUFFER_IMMUTABLE_OR_DYNAMIC);
			}

			if (_indexSize > SizeInBytes)
			{
				throw new ArgumentOutOfRangeException(nameof(data));
			}
#endif

			Graphics.D3DDeviceContext.UpdateSubresource(ref data,
			                                            D3DResource,
			                                            0,
			                                            0,
			                                            0,
			                                            new D3D11.ResourceRegion
			                                            {
				                                            Left = bufferOffset,
				                                            Right = bufferOffset + _indexSize,
				                                            Top = 0,
				                                            Front = 0,
				                                            Bottom = 1,
				                                            Back = 1
			                                            });
		}

        /// <summary>
        /// Function to update the constant buffer data with data from native memory.
        /// </summary>
        /// <param name="data">The <see cref="GorgonPointer"/> to the native memory holding the data to copy into the buffer.</param>
        /// <param name="bufferOffset">[Optional] The number of bytes within this buffer to start writing at.</param>
		/// <param name="offset">[Optional] The offset, in bytes, to start copying from.</param>
        /// <param name="size">[Optional] The size, in bytes, to copy.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="data"/> parameter is <b>null</b>.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="offset"/>, or the <paramref name="bufferOffset"/> plus the size of the data in <paramref name="data"/> exceed the size of this buffer.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size, in bytes, of the <paramref name="data"/> parameter is larger than the total <see cref="GorgonResource.SizeInBytes"/> of the buffer.
        /// <para>-or-</para>
        /// <para>The <paramref name="size"/> parameter is less than 1, or larger than the buffer size.</para>
        /// <para>-or-</para>
        /// <para>The <paramref name="offset"/> or the <paramref name="bufferOffset"/> parameter is less than 0.</para>
        /// </exception>
        /// <exception cref="NotSupportedException">Thrown when the <see cref="IGorgonIndexBufferInfo.Usage"/> is either <c>Immutable</c> or <c>Dynamic</c>.</exception>
        /// <remarks>
        /// <para>
        /// Use this method to send a blob of byte data to the buffer. This allows for fine grained control over what gets sent to the buffer. 
        /// </para>
        /// <para>
        /// Because this is using native, unmanaged, memory, special care must be taken to ensure that the application does not attempt to read/write out of bounds of that memory region. Particular care must be 
        /// taken to ensure that <paramref name="offset"/>, <paramref name="bufferOffset"/> and <paramref name="size"/> do not exceed the bounds of the memory region.
        /// </para>
        /// <para>
        /// If the <paramref name="size"/> parameter is omitted (<b>null</b>), then the entire buffer size is used minus the <paramref name="offset"/>.
        /// </para>
        /// <para>
        /// This method will throw an exception when the buffer is created with a <see cref="IGorgonIndexBufferInfo.Usage"/> of <c>Immutable</c> or <c>Dynamic</c>.
        /// </para>
        /// <para>
        /// <note type="warning">
        /// <para>
        /// For performance reasons, exceptions raised by this method will only be done so when Gorgon is compiled as DEBUG.
        /// </para>
        /// </note>
        /// </para>
        /// </remarks>
        public void Update(IGorgonPointer data, int bufferOffset = 0, int offset = 0, int? size = null)
		{
			data.ValidateObject(nameof(data));

		    if (size == null)
		    {
		        size = ((int)data.Size) - offset;
		    }

#if DEBUG
            if ((Info.Usage == D3D11.ResourceUsage.Dynamic) || (Info.Usage == D3D11.ResourceUsage.Immutable))
			{
				throw new NotSupportedException(Resources.GORGFX_ERR_BUFFER_IMMUTABLE_OR_DYNAMIC);
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			if (size < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(size));
			}

			if (bufferOffset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(bufferOffset));
			}

			if (offset + size > data.Size)
			{
				throw new ArgumentException(string.Format(Resources.GORGFX_ERR_DATA_OFFSET_COUNT_IS_TOO_LARGE, offset, size));
			}

			if (bufferOffset + size > data.Size)
			{
				throw new ArgumentException(string.Format(Resources.GORGFX_ERR_DATA_OFFSET_COUNT_IS_TOO_LARGE, offset, size));
			}
#endif

			Graphics.D3DDeviceContext.UpdateSubresource(new DX.DataBox
			                                            {
				                                            DataPointer = new IntPtr(data.Address + offset),
				                                            SlicePitch = 0,
				                                            RowPitch = size.Value
			                                            },
			                                            D3DResource, 
														0,
														new D3D11.ResourceRegion
														{
															Left = bufferOffset,
															Right = bufferOffset + size.Value,
															Top = 0,
															Front = 0,
															Back = 1,
															Bottom = 1
														});
		}

        /// <summary>
        /// Function to update the buffer with data.
        /// </summary>
        /// <param name="data">The array of index data to populate the buffer with.</param>
        /// <param name="bufferOffset">[Optional] The number of bytes within this buffer to start writing at.</param>
		/// <param name="startIndex">[Optional] The offset within the array to start copying from.</param>
        /// <param name="count">[Optional] The number of elements to copy.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="data"/> parameter is <b>null</b>.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="startIndex"/>, or the <paramref name="bufferOffset"/> plus the <paramref name="count"/> exceeds the number of elements in the <paramref name="data"/> parameter.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size, in bytes, of the <paramref name="data"/> parameter, multiplied by the number of items to copy is larger than the total <see cref="GorgonResource.SizeInBytes"/> of the buffer.
        /// <para>-or-</para>
        /// <para>Thrown when the <paramref name="startIndex"/>, or the <paramref name="bufferOffset"/> is less than 0, or the <paramref name="count"/> is less than 1.</para>
        /// <para>-or-</para>
        /// <para>Thrown when the size of an index multiplied by the count (minus the offset) is larger than the buffer size.</para>
        /// </exception>
        /// <exception cref="NotSupportedException">Thrown when the <see cref="IGorgonIndexBufferInfo.Usage"/> is either <c>Immutable</c> or <c>Dynamic</c>.</exception>
        /// <remarks>
        /// <para>
        /// If the <paramref name="count"/> parameter is omitted (<b>null</b>), then the length of the <paramref name="data"/> parameter is used minus the <paramref name="startIndex"/>.
        /// </para>
        /// <para>
        /// This method will throw an exception when the buffer is created with a <see cref="IGorgonIndexBufferInfo.Usage"/> of <c>Immutable</c> or <c>Dynamic</c>.
        /// </para>
        /// <para>
        /// <note type="warning">
        /// <para>
        /// For performance reasons, exceptions raised by this method will only be done so when Gorgon is compiled as DEBUG.
        /// </para>
        /// </note>
        /// </para>
        /// </remarks>
        public void Update(int[] data, int bufferOffset = 0, int startIndex = 0, int? count = null)
		{
			data.ValidateObject(nameof(data));

		    if (count == null)
		    {
		        count = data.Length - startIndex;
		    }

#if DEBUG
			if ((Info.Usage == D3D11.ResourceUsage.Dynamic) || (Info.Usage == D3D11.ResourceUsage.Immutable))
			{
				throw new NotSupportedException(Resources.GORGFX_ERR_BUFFER_IMMUTABLE_OR_DYNAMIC);
			}

			if (startIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			if (bufferOffset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(bufferOffset));
			}

			if (count < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			if ((startIndex + count) > data.Length)
			{
				throw new ArgumentException(string.Format(Resources.GORGFX_ERR_DATA_OFFSET_COUNT_IS_TOO_LARGE, startIndex, count));
			}

			if ((bufferOffset + count * _indexSize) > SizeInBytes)
			{
				throw new ArgumentException(string.Format(Resources.GORGFX_ERR_DATA_OFFSET_COUNT_IS_TOO_LARGE, startIndex, count));
			}
#endif
			Graphics.D3DDeviceContext.UpdateSubresource(data,
			                                            D3DResource,
			                                            0,
			                                            0,
			                                            0,
			                                            new D3D11.ResourceRegion
			                                            {
				                                            Left = bufferOffset,
				                                            Right = bufferOffset + (count.Value * _indexSize),
				                                            Top = 0,
				                                            Front = 0,
				                                            Bottom = 1,
				                                            Back = 1
			                                            });
		}
		#endregion

		#region Constructor/Finalizer.
		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonIndexBuffer" /> class.
		/// </summary>
		/// <param name="name">Name of this buffer.</param>
		/// <param name="graphics">The <see cref="GorgonGraphics"/> object used to create and manipulate the buffer.</param>
		/// <param name="info">Information used to create the buffer.</param>
		/// <param name="initialData">[Optional] The initial data used to populate the buffer.</param>
		/// <param name="log">[Optional] The log interface used for debug logging.</param>
		/// <exception cref="ArgumentNullException">Thrown when the <paramref name="graphics"/>, <paramref name="name"/>, or <paramref name="info"/> parameters are <b>null</b>.</exception>
		/// <exception cref="ArgumentEmptyException">Thrown when the <paramref name="name"/> is empty.</exception>
		public GorgonIndexBuffer(string name, GorgonGraphics graphics, IGorgonIndexBufferInfo info, IGorgonPointer initialData = null, IGorgonLog log = null)
			: base(graphics, name, log)
		{
			if (info == null)
			{
				throw new ArgumentNullException(nameof(info));
			}

            BufferType = BufferType.Index;
		    
			_info = new GorgonIndexBufferInfo(info);
			_indexSize = _info.Use16BitIndices ? sizeof(ushort) : sizeof(uint);

			Initialize(initialData);
		}
		#endregion
	}
}
