﻿#region MIT.
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
// Created: Thursday, September 15, 2011 2:31:53 PM
// 
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using GorgonLibrary.Diagnostics;
using GorgonLibrary.Native;

namespace GorgonLibrary.IO
{
	/// <summary>
	/// Status for the stream.
	/// </summary>
	public enum StreamStatus
	{
		/// <summary>
		/// Buffer can be read from and written to.
		/// </summary>
		ReadWrite = 0,
		/// <summary>
		/// Buffer can be read from.
		/// </summary>
		ReadOnly = 1,
		/// <summary>
		/// Buffer can be written to.
		/// </summary>
		WriteOnly = 2
	}

	/// <summary>
	/// A generic data stream.
	/// </summary>
	/// <remarks>This will hold generic byte data in unmanaged memory.  It is similar to the <see cref="System.IO.MemoryStream">MemoryStream</see> object, except that MemoryStream uses an array of bytes as a backing store.
	/// <para>Because this stream uses unmanaged memory, it is imperative that you call the Dispose method when you're done with the object.</para>
	/// </remarks>
	public unsafe class GorgonDataStream
		: Stream
	{
		#region Variables.
        private static byte[] _buffer = null;                   // Buffer for read operations.
		private bool _disposed = false;							// Flag to indicate that the object was disposed.
		private IntPtr _data = IntPtr.Zero;						// Pointer to the data held by the stream.
		private int _pointerPosition = 0;						// Position in the buffer.
		private int _length = 0;								// Number of bytes in the buffer.
		private IntPtr _pointerOffset = IntPtr.Zero;			// Pointer offset.
		private GCHandle _handle = default(GCHandle);			// Handle to a pinned array.
		private bool _ownsPointer = true;						// Flag to indicate that we own this pointer.
        private void* _dataPointer = null;                      // Pointer to the data in memory.
		#endregion

		#region Properties.
		/// <summary>
		/// Property to set or return the status of the stream.
		/// </summary>
		protected StreamStatus StreamStatus
		{
			get;
			set;
		}

		/// <summary>
		/// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
		/// </summary>
		/// <returns>true if the stream supports reading; otherwise, false.</returns>
		public override bool CanRead
		{
			get 
			{
				return ((StreamStatus == StreamStatus.ReadOnly) || (StreamStatus == StreamStatus.ReadWrite));
			}
		}

		/// <summary>
		/// When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
		/// </summary>
		/// <returns>true if the stream supports seeking; otherwise, false.</returns>
		public override bool CanSeek
		{
			get 
			{
				return true;
			}
		}

		/// <summary>
		/// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
		/// </summary>
		/// <returns>true if the stream supports writing; otherwise, false.</returns>
		public override bool CanWrite
		{
			get 
			{
				return ((StreamStatus == StreamStatus.WriteOnly) || (StreamStatus == StreamStatus.ReadWrite));
			}
		}

		/// <summary>
		/// When overridden in a derived class, gets the length in bytes of the stream.
		/// </summary>
		/// <returns>A long value representing the length of the stream in bytes.</returns>
		///   
		/// <exception cref="T:System.NotSupportedException">A class derived from Stream does not support seeking. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public override long Length
		{
			get 
			{
				return _length;
			}
		}

		/// <summary>
		/// When overridden in a derived class, gets or sets the position within the current stream.
		/// </summary>
		/// <returns>The current position within the stream.</returns>
		///   
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">Thrown when the value is less than zero or greater than <see cref="P:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
		public override long Position
		{
			get
			{
				return _pointerPosition;
			}
			set
			{
				if (_data == IntPtr.Zero)
				{
					_pointerOffset = IntPtr.Zero;
					_pointerPosition = 0;
					return;
				}
				
#if DEBUG
				// Limit to the bounds of an integer.
				if ((value > Int32.MaxValue) || (value < 0))
				{
					throw new ArgumentOutOfRangeException("The property cannot be less than 0 or greater than " + Int32.MaxValue.ToString());
				}
#endif
				
				_pointerPosition = (int)value;
				_pointerOffset = _data + _pointerPosition;
                _dataPointer = _pointerOffset.ToPointer();
			}
		}

		/// <summary>
		/// Property to return the base pointer address to the allocated unmanaged memory.
		/// </summary>
		public IntPtr BasePointer
		{
			get
			{
				return _data;
			}
		}

        /// <summary>
        /// Property to return the unsafe pointer to the data offset by the position within the stream.
        /// </summary>
        public void* PositionPointerUnsafe
        {
            get
            {
                return _pointerOffset.ToPointer();
            }
        }

		/// <summary>
		/// Property to return the pointer address of the current position in the stream.
		/// </summary>
		public IntPtr PositionPointer
		{
			get
			{
				return _pointerOffset;
			}
		}

		/// <summary>
		/// Property to return the pointer to the allocated unmanaged memory.
		/// </summary>
		public void* UnsafePointer
		{
			get
			{
				return _dataPointer;
			}
		}
		#endregion

		#region Methods.
		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream"/> and optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected override void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (_ownsPointer)
				{
					// Destroy the buffer if we own it.
					if ((_data != IntPtr.Zero) && (!_handle.IsAllocated))
						Marshal.FreeHGlobal(_data);

					// Unpin any array that we've captured.
					if (_handle.IsAllocated)
						_handle.Free();
				}

				_data = IntPtr.Zero;
				_pointerOffset = IntPtr.Zero;
                _dataPointer = null;
				_disposed = true;
			}

			base.Dispose(disposing);
		}

		/// <summary>
		/// Function to determine if marshalling is required.
		/// </summary>
		/// <param name="type">Type to examine.</param>
		/// <returns>TRUE if the type requires marshalling, FALSE if not.</returns>
		private static bool UseMarshalling(Type type)
		{
			var members = type.GetMembers().Where(item => item.MemberType == MemberTypes.Property || item.MemberType == MemberTypes.Field);
			
			foreach (var member in members)
			{				
				FieldInfo field = member as FieldInfo;
				PropertyInfo property = member as PropertyInfo;
				Type memberType = null;
				bool isValid = false;

				if (field != null)
				{
					memberType = field.FieldType;
					isValid = !field.IsInitOnly && !field.IsStatic;
				}
				else
				{
					memberType = property.PropertyType;
					isValid = property.CanRead && property.CanWrite;
				}

				isValid = isValid && !memberType.IsPrimitive;

				if (isValid)
				{
					var marshalAttrib = member.GetCustomAttributes(typeof(MarshalAsAttribute), true) as IList<MarshalAsAttribute>;
					if ((marshalAttrib != null) && (marshalAttrib.Count > 0))
						return true;

					if (UseMarshalling(memberType))
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Function to marshal an enumerable type of value types into a Gorgon Data Stream.
		/// </summary>
		/// <typeparam name="T">Type of value to marshal.</typeparam>
		/// <param name="value">Values to marshal.</param>
		/// <returns>A data stream containing the marshalled data.</returns>
		/// <remarks>Use this to create and initialize a data stream with marshalled data.
		/// <para>Value types must include the System.Runtime.InteropServices.StructLayout attribute, and must use an explicit layout.</para>
		/// </remarks>
		/// <exception cref="System.ArgumentException">Thrown when the type of <paramref name="value"/> is not explicitly laid out with the System.Runtime.InteropServices.StructLayout attribute.</exception>
		public static GorgonDataStream EnumerableValueTypeToStream<T>(IEnumerable<T> value) where T : struct
		{
			GorgonDataStream result = null;
			Type type = typeof(T);
			int size = 0;
			int count = value.Count();

			if (value == null)
				return null;

			if (!type.IsExplicitLayout)
				throw new ArgumentException("The type '" + type.FullName + "' is not explicitly laid out using the StructLayout attribute.", "value");

			if (type.StructLayoutAttribute.Size <= 0)
				size = DirectAccess.SizeOf<T>() * count;
			else
				size = type.StructLayoutAttribute.Size * count;

			result = new GorgonDataStream(size);

			foreach (T item in value)
			{
				if (!UseMarshalling(type))
					result.Write<T>(item);
				else
					result.WriteMarshal<T>(item, false);
			}

			result.Position = 0;

			return result;
		}

		/// <summary>
		/// Function to marshal a value type into a Gorgon Data Stream.
		/// </summary>
		/// <typeparam name="T">Type of value to marshal.</typeparam>
		/// <param name="value">Value to marshal.</param>
		/// <returns>A data stream containing the marshalled data.</returns>
		/// <remarks>Use this to create and initialize a data stream with marshalled data.
		/// <para>Value types must include the System.Runtime.InteropServices.StructLayout attribute, and must use an explicit layout.</para>
		/// </remarks>
		/// <exception cref="System.ArgumentException">Thrown when the type of <paramref name="value"/> is not explicitly laid out with the System.Runtime.InteropServices.StructLayout attribute.</exception>
		public static GorgonDataStream ValueTypeToStream<T>(T value) where T : struct
		{
			GorgonDataStream result = null;			
			Type type = typeof(T);
			int size = 0;
			
			if (!type.IsExplicitLayout)
				throw new ArgumentException("The type '" + type.FullName + "' is not explicitly laid out using the StructLayout attribute.", "value");

			if (type.StructLayoutAttribute.Size <= 0)
				size = DirectAccess.SizeOf<T>();
			else
				size = type.StructLayoutAttribute.Size;

			result = new GorgonDataStream(size);

			if (!UseMarshalling(type))
				result.Write<T>(value);
			else
				result.WriteMarshal<T>(value, false);

			result.Position = 0;

			return result;
		}

		/// <summary>
		/// Function to retrieve the size of type, in bytes.
		/// </summary>
		/// <typeparam name="T">Type of data to find the size for.</typeparam>
		/// <returns>Number of bytes for the type.</returns>
		public static int SizeOf<T>() where T : struct
		{			
			return DirectAccess.SizeOf<T>();
		}

		/// <summary>
		/// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		public override void Flush()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
		/// </summary>
		/// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source.</param>
		/// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
		/// <param name="count">The maximum number of bytes to be read from the current stream.</param>
		/// <returns>
		/// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
		/// </returns>
		/// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length. </exception>
		///   
		/// <exception cref="T:System.ArgumentNullException">
		///   <paramref name="buffer"/> is null. </exception>
		///   
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		///   <paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
		///   
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public override int Read(byte[] buffer, int offset, int count)
		{
			int actualCount = count;

#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (buffer == null)
				throw new ArgumentNullException("buffer");

			if (offset < 0)
				throw new ArgumentOutOfRangeException("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException("count");

			if (offset + count > buffer.Length)
				throw new ArgumentException("The sum of the offset and count are larger than the array.", "count + offset");
#endif

			if ((actualCount + _pointerPosition) > _length)
				actualCount = (_length - _pointerPosition);

            DirectAccess.ReadArray<byte>(_dataPointer, buffer, offset, actualCount);

			Position += actualCount;

			return actualCount;
		}

		/// <summary>
		/// When overridden in a derived class, sets the position within the current stream.
		/// </summary>
		/// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
		/// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
		/// <returns>
		/// The new position within the current stream.
		/// </returns>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">Thrown when the offset moves the position outside of the boundaries of the stream.</exception>
		public override long Seek(long offset, SeekOrigin origin)
		{
#if DEBUG
			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			switch (origin)
			{
				case SeekOrigin.Begin:
					if (offset < 0)
						throw new ArgumentOutOfRangeException("offset", "Cannot seek before the beginning of the stream.");
					if (offset > _length)
						throw new ArgumentOutOfRangeException("offset", "Cannot seek beyond the end of the stream.");

					Position = offset;
					return Position;
				case SeekOrigin.End:
					if (offset < -_length)
						throw new ArgumentOutOfRangeException("offset", "Cannot seek before the beginning of the stream.");
					if (offset > 0)
						throw new ArgumentOutOfRangeException("offset", "Cannot seek beyond the end of the stream.");

					Position = _length + offset;
					return Position;
				default:
					long newPosition = _pointerPosition + offset;

					if (newPosition < 0)
						throw new ArgumentOutOfRangeException("offset", "Cannot seek before the beginning of the stream.");
					if (newPosition > _length)
						throw new ArgumentOutOfRangeException("offset", "Cannot seek beyond the end of the stream.");

					Position = newPosition;
					return Position;
			}
		}

		/// <summary>
		/// When overridden in a derived class, sets the length of the current stream.
		/// </summary>
		/// <param name="value">The desired length of the current stream in bytes.</param>
		/// <remarks>Calling this function will clear any data being held by the buffer, and reset the <see cref="P:GorgonLibrary.Data.GorgonDataStream.Position">position</see> to the beginning of the stream..</remarks>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public override void SetLength(long value)
		{
			if (!_ownsPointer)
				throw new NotSupportedException("The pointer is from an external source.  Cannot reallocate.");

			if (value < 0)
				value = 0;
			if (value > Int32.MaxValue)
				value = Int32.MaxValue;

			if ((_data != IntPtr.Zero) && (!_handle.IsAllocated))
				Marshal.FreeHGlobal(_data);
		
			if (_handle.IsAllocated)
				_handle.Free();

			_data = IntPtr.Zero;
			_pointerOffset = IntPtr.Zero;
            _dataPointer = null;
			_pointerPosition = 0;
			_length = 0;

			if (value == 0)
				return;

			_data = Marshal.AllocHGlobal((int)value);            
			_pointerOffset = _data;
            _dataPointer = _pointerOffset.ToPointer();
			_length = (int)value;
		}

		/// <summary>
		/// When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
		/// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		/// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length. </exception>
		///   
		/// <exception cref="T:System.ArgumentNullException">
		///   <paramref name="buffer"/> is null. </exception>
		///   
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		///   <paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
		///   
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public override void Write(byte[] buffer, int offset, int count)
		{
			int actualCount = count;

#if DEBUG
			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (buffer == null)
				throw new ArgumentNullException("buffer");

			if (offset < 0)
				throw new ArgumentOutOfRangeException("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException("count");

			if (offset + count > buffer.Length)
				throw new ArgumentException("The sum of the offset and count are larger than the array.", "count + offset");
#endif

			if ((actualCount + _pointerPosition) > _length)
				actualCount = (_length - _pointerPosition);

            DirectAccess.WriteArray<byte>(_dataPointer, buffer, offset, actualCount);

			Position += actualCount;
		}

		/// <summary>
		/// Writes a floating point value to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The floating point value to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteFloat(float value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			float* pointer = (float*)_dataPointer;
			*pointer = value;

			Position += 4;
		}

		/// <summary>
		/// Writes a double to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The double to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteDouble(double value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

            double* pointer = (double*)_dataPointer;
			*pointer = value;

			Position += sizeof(double);
		}

		/// <summary>
		/// Writes an unsigned 16 bit integer to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The unsigned 16 bit integer to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteUInt16(UInt16 value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			UInt16* pointer = (UInt16*)_dataPointer;
			*pointer = value;

			Position += 2;
		}

		/// <summary>
		/// Writes an unsigned 64 bit integer to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The unsigned 64 bit integer to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteUInt64(UInt64 value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			UInt64* pointer = (UInt64*)_dataPointer;
			*pointer = value;

			Position += 8;
		}

		/// <summary>
		/// Writes an unsigned integer to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The unsigned integer to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteUInt32(UInt32 value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			UInt32* pointer = (UInt32*)_dataPointer;
			*pointer = value;

			Position += 4;
		}

		/// <summary>
		/// Writes a 16 bit integer to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The 16 bit integer to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteInt16(Int16 value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			Int16* pointer = (Int16*)_dataPointer;
			*pointer = value;

			Position += 2;
		}

		/// <summary>
		/// Writes a 64 bit integer to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The 64 bit integer to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteInt64(Int64 value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			Int64* pointer = (Int64*)_dataPointer;
			*pointer = value;

			Position += 8;
		}

		/// <summary>
		/// Writes an integer to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The integer to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void WriteInt32(Int32 value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			Int32* pointer = (Int32*)_dataPointer;
			*pointer = value;

			Position += 4;
		}

        /// <summary>
        /// Function to write a character to the current position in the stream and advances the position by 2 bytes.
        /// </summary>
        /// <param name="value">The character to write to the stream.</param>
		/// <remarks>This function advances the position by 2 bytes because internally .NET defaults to UTF16 for a character, which is 2 bytes.</remarks>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <exception cref="System.ArgumentException">Thrown when the <paramref name="value"/> parameter is a surrogate character.  These are not supported.</exception>
        public void WriteChar(char value)
        {
#if DEBUG
            if (!CanWrite)
                throw new NotSupportedException("Buffer is read only.");

            if (_data == IntPtr.Zero)
                throw new ObjectDisposedException("GorgonDataStream");

            if (char.IsSurrogate(value))
            {
                throw new ArgumentException("Cannot write a single surrogate character to the stream.", "value");
            }
#endif
			WriteUInt16(Convert.ToUInt16(value));
        }

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="value">The byte to write to the stream.</param>
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing, or the stream is already closed. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public override void WriteByte(byte value)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= _length)
				return;

			byte* pointer = (byte*)_dataPointer;
			*pointer = value;

			Position++;
		}

		/// <summary>
		/// Function to write a floating point value to the stream and increment the position by the size of a float.
		/// </summary>
		/// <returns>
		/// The float, or -1 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public float ReadFloat()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return -1;

			float* pointer = (float*)_dataPointer;
			Position +=4;

			return *pointer;
		}

		/// <summary>
		/// Function to write a double to the stream and increment the position by the size of a double.
		/// </summary>
		/// <returns>
		/// The double, or -1 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public double ReadDouble()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return -1;

			double* pointer = (double*)_dataPointer;
			Position += sizeof(double);

			return *pointer;
		}

		/// <summary>
		/// Function to write an unsigned 16 bit integer to the stream and increment the position by the size of a 16 bit integer.
		/// </summary>
		/// <returns>
		/// The unsigned 16 bit integer, or 0 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public UInt16 ReadUInt16()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return 0;

			UInt16* pointer = (UInt16*)_dataPointer;
			Position+=2;

			return *pointer;
		}

		/// <summary>
		/// Function to write an unsigned 32 bit integer to the stream and increment the position by the size of a 32 bit integer.
		/// </summary>
		/// <returns>
		/// The unsigned 32 bit integer, or 0 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public UInt32 ReadUInt32()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return 0;

			UInt32* pointer = (UInt32*)_dataPointer;
			Position+=4;

			return *pointer;
		}

		/// <summary>
		/// Function to write an unsigned 64 bit integer to the stream and increment the position by the size of a 64 bit integer.
		/// </summary>
		/// <returns>
		/// The unsigned 64 bit integer, or 0 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public UInt64 ReadUInt64()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return 0;

			UInt64* pointer = (UInt64*)_dataPointer;
			Position+=8;

			return *pointer;
		}

		/// <summary>
		/// Function to write a 16 bit integer to the stream and increment the position by the size of a 16 bit integer.
		/// </summary>
		/// <returns>
		/// The 16 bit integer, or -1 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public Int16 ReadInt16()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return -1;

			Int16* pointer = (Int16*)_dataPointer;
			Position+=2;

			return *pointer;
		}

		/// <summary>
		/// Function to write a 32 bit integer to the stream and increment the position by the size of a 32 bit integer.
		/// </summary>
		/// <returns>
		/// The 32 bit integer, or -1 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public Int32 ReadInt32()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return -1;

			Int32* pointer = (Int32*)_dataPointer;
			Position+=4;

			return *pointer;
		}

		/// <summary>
		/// Function to write a 64 bit integer to the stream and increment the position by the size of a 64 bit integer.
		/// </summary>
		/// <returns>
		/// The 64 bit integer, or -1 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public Int64 ReadInt64()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return -1;

			Int64* pointer = (Int64*)_dataPointer;
			Position+=8;

			return *pointer;
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>
		/// The unsigned byte cast to an Int32, or -1 if at the end of the stream.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public override int ReadByte()
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");
#endif

			if (_pointerPosition >= Length)
				return -1;

			byte* pointer = (byte*)_dataPointer;
			Position++;

			return (int)(*pointer);
		}

		/// <summary>
		/// Function to write a list of value types to the stream.
		/// </summary>
		/// <typeparam name="T">Type of data to write.</typeparam>
		/// <param name="buffer">Array of data to write.</param>
		/// <param name="offset">Offset into the array to start at.</param>
		/// <param name="count">Number of elements in the array to read.</param>
		/// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length. </exception>
		///   
		/// <exception cref="T:System.ArgumentNullException">
		///   <paramref name="buffer"/> is null. </exception>
		///   
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		///   <paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
		///   
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
		public virtual void WriteRange<T>(T[] buffer, int offset, int count)
			where T : struct
		{
			int actualCount = count * DirectAccess.SizeOf<T>();

#if DEBUG
			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (buffer == null)
				throw new ArgumentNullException("buffer");

			if (offset < 0)
				throw new ArgumentOutOfRangeException("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException("count");

			if (offset + count > buffer.Length)
				throw new ArgumentException("The sum of the offset and count are larger than the array.", "count + offset");
#endif

			if ((actualCount + _pointerPosition) > _length)
				actualCount = (_length - _pointerPosition);

            DirectAccess.WriteArray<T>(_dataPointer, buffer, offset, actualCount);
			
			Position += actualCount;
		}

		/// <summary>
		/// Function to fill the stream with the specified value.
		/// </summary>
		/// <param name="value">Value to fill with.</param>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
		public void Fill(byte value)
		{			
			DirectAccess.FillMemory(BasePointer, value, (int)Length);
			Position = Length;
		}

		/// <summary>
		/// Function to fill the stream with zeroes.
		/// </summary>
		public void Zero()			
		{
			DirectAccess.ZeroMemory(BasePointer, (int)Length);
			Position = Length;
		}

		/// <summary>
		/// Function to write an array of value types to the stream.
		/// </summary>
		/// <typeparam name="T">Type of data to write.</typeparam>
		/// <param name="buffer">Array to read from.</param>
		/// <exception cref="T:System.ArgumentNullException">
		///   <paramref name="buffer"/> is null. </exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
		public void WriteRange<T>(T[] buffer)
			where T : struct
		{
			GorgonDebug.AssertNull<T[]>(buffer, "buffer");

			WriteRange<T>(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Function to write a value type to the stream.
		/// </summary>
		/// <typeparam name="T">Type of data to write.</typeparam>
		/// <param name="item">Value to write.</param>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when trying to write beyond the end of the stream.</exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void Write<T>(T item)
			where T : struct
		{
			int typeSize = DirectAccess.SizeOf<T>();

#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (typeSize + Position > _length)
                throw new EndOfStreamException("Cannot write beyond the end of the stream.");
#endif
            DirectAccess.WriteValue<T>(_dataPointer, ref item);
			Position += typeSize;
		}

		/// <summary>
		/// Function to read an array of value types from the stream.
		/// </summary>
		/// <param name="buffer">Array to write the value types into.</param>
		/// <param name="offset">Offset within the array to start writing at.</param>
		/// <param name="count">Number of elements to write.</param>
		/// <returns>
		/// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
		/// </returns>
		/// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length. </exception>
		///   
		/// <exception cref="T:System.ArgumentNullException">
		///   <paramref name="buffer"/> is null. </exception>
		///   
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		///   <paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
		///   
		/// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
		///   
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		///   
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
		public virtual int ReadRange<T>(T[] buffer, int offset, int count)
			where T : struct
		{
			int actualCount = count * DirectAccess.SizeOf<T>();

#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (buffer == null)
				throw new ArgumentNullException("buffer");

			if (offset < 0)
				throw new ArgumentOutOfRangeException("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException("count");

			if (offset + count > buffer.Length)
				throw new ArgumentException("The sum of the offset and count are larger than the array.", "count + offset");
#endif

			if ((actualCount + _pointerPosition) > _length)
				actualCount = (_length - _pointerPosition);

            DirectAccess.ReadArray<T>(_dataPointer, buffer, offset, actualCount);

			Position += actualCount;

			return actualCount;
		}

        /// <summary>
        /// Function to copy data from a stream into this stream.
        /// </summary>
        /// <param name="stream">The stream to copy from.</param>
        /// <param name="size">The size of the data to copy.</param>
        /// <exception cref="System.IO.IOException">Thrown when the <paramref name="stream"/> parameter is write-only.</exception>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when an attempt is made to read beyond the end of the stream parameter .
        /// <para>-or-</para>
        /// <para>Thrown when this stream cannot accomodate the <paramref name="size"/> parameter.</para>
        /// </exception>
        public void ReadFromStream(Stream stream, int size)
        {
            GorgonDebug.AssertNull<Stream>(stream, "stream");
#if DEBUG
            if (!stream.CanRead)
            {
                throw new System.IO.IOException("The stream is write-only.");
            }

            if (_length - _pointerPosition < size)
            {
                throw new EndOfStreamException("Cannot write beyond the end of the stream.");
            }

            if (size > stream.Length - stream.Position)
            {
                throw new EndOfStreamException("Cannot read beyond the end of the stream.");
            }
#endif   
            // Create a buffer that's under the Large Object Heap size requirement.
            if (_buffer == null)
            {
                _buffer = new byte[81920];
            }

            int blockSize = _buffer.Length;

            // Copy by block.
            while (size > 0)
            {
                if (blockSize > size)
                {
                    blockSize = size;
                }

                stream.Read(_buffer, 0, blockSize);
                Write(_buffer, 0, blockSize);

                size -= blockSize;
            }
        }

		/// <summary>
		/// Function to read an array of value types from the stream.
		/// </summary>
		/// <typeparam name="T">Type of data to write.</typeparam>
		/// <param name="count">Number of items to read.</param>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
		public T[] ReadRange<T>(int count)
			where T : struct
		{
			T[] result = new T[count];

			ReadRange<T>(result, 0, count);

			return result;
		}

		/// <summary>
		/// Function to read a value type from the 
		/// </summary>
		/// <returns>The value type within the stream.</returns>
		/// <remarks>At this time, this function will only support structures with primitive types in them, strings and other objects will not work.</remarks>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when trying to read beyond the end of the stream.</exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		public T Read<T>()
			where T : struct
		{
			int typeSize = DirectAccess.SizeOf<T>();

#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (typeSize + Position > _length)
				throw new EndOfStreamException("Cannot read beyond the end of the stream.");
#endif

			T result = default(T);
            DirectAccess.ReadValue<T>(_dataPointer, out result);
			Position += typeSize;

			return result;
		}

		/// <summary>
		/// Function to copy data from the stream into a pointer.
		/// </summary>
		/// <param name="pointer">Pointer to the data to write into.</param>
		/// <param name="size">Size, in bytes, to read from the stream.</param>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when trying to read beyond the end of the stream.</exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void CopyToPointer(IntPtr pointer, int size)
		{
#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (size + Position > _length)
                throw new EndOfStreamException("Cannot read beyond the end of the stream.");
#endif
            _pointerOffset.CopyTo(pointer, size);
			Position += size;
		}

        /// <summary>
        /// Function to copy data from the stream into a pointer.
        /// </summary>
        /// <param name="pointer">Pointer to the data to write into.</param>
        /// <param name="size">Size, in bytes, to read from the stream.</param>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when trying to read beyond the end of the stream.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public void CopyToPointer(void* pointer, int size)
        {
#if DEBUG
            if (!CanRead)
                throw new NotSupportedException("Buffer is write only.");

            if (_data == IntPtr.Zero)
                throw new ObjectDisposedException("GorgonDataStream");

            if (size + Position > _length)
                throw new EndOfStreamException("Cannot read beyond the end of the stream.");
#endif
            DirectAccess.MemoryCopy(_dataPointer, pointer, size);
            Position += size;
        }

		/// <summary>
		/// Function to write a string to the stream.
		/// </summary>
		/// <param name="value">String to write into the stream.</param>
		/// <param name="encoding">Encoding to use.</param>
        /// <remarks>This stores the length of the string prefixed to the string data.  The length is encoded as series of 7 bit byte values.
        /// <para>If the <paramref name="encoding"/> value is NULL (Nothing in VB.Net), then UTF-8 encoding will be used.</para>
        /// </remarks>
		public void WriteString(string value, Encoding encoding)
		{
            value.WriteToStream(this, encoding);
		}

		/// <summary>
		/// Function to write a string to the stream.
		/// </summary>
		/// <param name="value">String to write into the stream.</param>
        /// <remarks>This stores the length of the string prefixed to the string data.  The length is encoded as series of 7 bit byte values.</remarks>
		public void WriteString(string value)
		{
			WriteString(value, null);
		}

        /// <summary>
        /// Function to read a character with the specified encoding from the stream and increment the position by the size of a character.
        /// </summary>
        /// <returns>
        /// The character in the stream or the default character to indicate the end of stream.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public char ReadChar()
        {
            char result = default(char);
#if DEBUG
            if (!CanRead)
                throw new NotSupportedException("Buffer is write only.");

            if (_data == IntPtr.Zero)
                throw new ObjectDisposedException("GorgonDataStream");
#endif
            if (Position >= Length)
            {
                return result;
            }

			return Convert.ToChar(ReadUInt16());
        }

		/// <summary>
		/// Function to read a string from the stream.
		/// </summary>
		/// <returns>The string in the stream.</returns>
		public string ReadString()
		{
			return ReadString(null);
		}

		/// <summary>
		/// Function to read a string from the stream.
		/// </summary>
		/// <param name="encoding">Text encoding to use.</param>
		/// <returns>The string in the stream.</returns>
        /// <remarks>This method reads the length of the string from a series of 7 bit bytes before reading the string.</remarks>
		public string ReadString(Encoding encoding)
		{
            return GorgonIOExtensions.ReadString(this, encoding);
		}

		/// <summary>
		/// Function to copy the data from a pointer into the stream.		
		/// </summary>
		/// <param name="pointer">Pointer to the data to read.</param>
		/// <param name="size">Size, in bytes, to write to the stream.</param>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when trying to write beyond the end of the stream.</exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		public void CopyFromPointer(IntPtr pointer, int size)
		{
#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (size + Position > _length)
                throw new EndOfStreamException("Cannot write beyond the end of the stream.");
#endif

			_pointerOffset.CopyFrom(pointer, size);
			Position += size;
		}

        /// <summary>
        /// Function to copy the data from a pointer into the stream.		
        /// </summary>
        /// <param name="pointer">Pointer to the data to read.</param>
        /// <param name="size">Size, in bytes, to write to the stream.</param>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when trying to write beyond the end of the stream.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public void CopyFromPointer(void *pointer, int size)
        {
#if DEBUG
            if (!CanWrite)
                throw new NotSupportedException("Buffer is read only.");

            if (_data == IntPtr.Zero)
                throw new ObjectDisposedException("GorgonDataStream");

            if (size + Position > _length)
                throw new EndOfStreamException("Cannot write beyond the end of the stream.");
#endif
            DirectAccess.MemoryCopy(_dataPointer, pointer, size);
            Position += size;
        }

		/// <summary>
		/// Function to marshal a structure into the stream.
		/// </summary>
		/// <typeparam name="T">Type of data to marshal.</typeparam>
		/// <param name="data">Data to marshal.</param>
		/// <param name="deleteContents">TRUE to remove any pre-allocated data within the data, FALSE to leave alone.</param>
		/// <remarks>This method will marshal a structure (object or value type) into unmanaged memory.
		/// <para>Passing FALSE to <paramref name="deleteContents"/> may result in a memory leak if the data was previously initialized.</para>
		/// <para>For more information, see the <see cref="M:System.RunTime.InteropServices.Marshal.StructureToPtr">Marshal.StructureToPtr</see> method.</para>
		/// </remarks>
		/// <exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when an attempt to read beyond the length of the stream is made.</exception>
		public void WriteMarshal<T>(T data, bool deleteContents)
		{
			int dataSize = Marshal.SizeOf(typeof(T));

#if DEBUG
			if (!CanWrite)
				throw new NotSupportedException("Buffer is read only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (dataSize + Position > _length)
                throw new EndOfStreamException("Cannot write beyond the end of the stream.");
#endif

			_pointerOffset.MarshalFrom(data, deleteContents);

			Position += dataSize;
		}

		/// <summary>
		/// Function to marshal a structure from the stream.
		/// </summary>
		/// <typeparam name="T">Type of data to marshal.</typeparam>
		/// <returns>The data converted into a new value type or object.</returns>
		/// <remarks>This method will marshal unmanaged data back into a new structure (object or value type).
		/// <para>For more information, see the <see cref="M:System.RunTime.InteropServices.Marshal.PtrToStructure">Marshal.PtrToStructure</see> method.</para>
		/// </remarks>
		/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
		/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="System.IO.EndOfStreamException">Thrown when an attempt to read beyond the end of the stream was made.</exception>
		public T ReadMarshal<T>()
		{
			int dataSize = Marshal.SizeOf(typeof(T));

#if DEBUG
			if (!CanRead)
				throw new NotSupportedException("Buffer is write only.");

			if (_data == IntPtr.Zero)
				throw new ObjectDisposedException("GorgonDataStream");

			if (dataSize + Position > _length)
                throw new EndOfStreamException("Cannot write beyond the end of the stream.");
#endif

			T value = _pointerOffset.MarshalTo<T>();			
			Position += dataSize;

			return value;
		}
		#endregion

		#region Constructor/Destructor.
		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonDataStream"/> class.
		/// </summary>
		/// <param name="data">The data used to initialize the stream.</param>
		/// <param name="index">Index inside of the source array to start reading from.</param>
		/// <param name="count">Number of elements to read.</param>
		/// <param name="status">A flag indicating if the buffer is read only, write only or both.</param>
		/// <exception cref="System.ArgumentNullException">Thrown when the data parameter is NULL (Nothing in VB.Net).</exception>
		/// <remarks>The array elements should all be of the same type, and value types.
		/// <para>A pointer to the array will be held and released upon disposal of the stream, this may impact garbage collection performance.  
		/// Also, since the stream is holding a pointer, any changes to the <paramref name="data"/> parameter array elements will be reflected 
		/// in the stream.
		/// </para>
		/// </remarks>
		public GorgonDataStream(Array data, int index, int count, StreamStatus status)
		{
			if (data == null)
				throw new ArgumentNullException("data");

			if (index < 0)
				throw new ArgumentOutOfRangeException("index", "Cannot be less than zero.");

			if (count < 0)
				throw new ArgumentOutOfRangeException("index", "Cannot be less than zero.");

			if (index + count > data.LongLength)
				throw new ArgumentOutOfRangeException("int + count", "Index and count cannot be larger than the length of the source.");

			// Pin the array.
			_handle = GCHandle.Alloc(data, GCHandleType.Pinned);
			_data = Marshal.UnsafeAddrOfPinnedArrayElement(data, index);
			_length = count * Marshal.SizeOf(data.GetType().GetElementType());
			_pointerOffset = _data;
            _dataPointer = _pointerOffset.ToPointer();
			StreamStatus = status;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonDataStream"/> class.
		/// </summary>
		/// <param name="data">The data used to initialize the stream.</param>
		/// <param name="index">Index inside of the source array to start reading from.</param>
		/// <param name="count">Number of elements to read.</param>
		/// <exception cref="System.ArgumentNullException">Thrown when the data parameter is NULL (Nothing in VB.Net).</exception>
		/// <remarks>The array elements should all be of the same type, and value types.
		/// <para>A pointer to the array will be held and released upon disposal of the stream, this may impact garbage collection performance.  
		/// Also, since the stream is holding a pointer, any changes to the <paramref name="data"/> parameter array elements will be reflected 
		/// in the stream.
		/// </para>
		/// </remarks>
		public GorgonDataStream(Array data, int index, int count)
			: this(data, index, count, StreamStatus.ReadWrite)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonDataStream"/> class.
		/// </summary>
		/// <param name="data">The data used to initialize the stream.</param>
		/// <exception cref="System.ArgumentNullException">Thrown when the data parameter is NULL (Nothing in VB.Net).</exception>
		/// <remarks>The array elements should all be of the same type, and value types.
		/// <para>A pointer to the array will be held and released upon disposal of the stream, this may impact garbage collection performance.  
		/// Also, since the stream is holding a pointer, any changes to the <paramref name="data"/> parameter array elements will be reflected 
		/// in the stream.
		/// </para>
		/// </remarks>
		public GorgonDataStream(Array data)
			: this(data, 0, data.Length, StreamStatus.ReadWrite)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonDataStream"/> class.
		/// </summary>
		/// <param name="capacity">The capacity of the underlying buffer.</param>
		public GorgonDataStream(int capacity)
		{
			SetLength(capacity);
		}
		
		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonDataStream"/> class.
		/// </summary>
		/// <param name="source">The source pointer.</param>
		/// <param name="size">The size of the buffer (in bytes).</param>
		public GorgonDataStream(IntPtr source, int size)
		{
			_ownsPointer = false;
			_data = source;
			_pointerOffset = source;
            _dataPointer = _pointerOffset.ToPointer();
			_pointerPosition = 0;
			_length = size;
			StreamStatus = StreamStatus.ReadWrite;
		}

        /// <summary>
        /// Initializes a new instance of the <see cref="GorgonDataStream"/> class.
        /// </summary>
        /// <param name="source">The source pointer.</param>
        /// <param name="size">The size of the buffer (in bytes).</param>
        public GorgonDataStream(void *source, int size)
        {
            _ownsPointer = false;
            _dataPointer = source;
            _data = new IntPtr(_dataPointer);
            _pointerOffset = _data;
            _pointerPosition = 0;
            _length = size;
            StreamStatus = StreamStatus.ReadWrite;
        }

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="GorgonDataStream"/> is reclaimed by garbage collection.
		/// </summary>
		~GorgonDataStream()
		{
			Dispose(false);
		}
		#endregion
	}
}
