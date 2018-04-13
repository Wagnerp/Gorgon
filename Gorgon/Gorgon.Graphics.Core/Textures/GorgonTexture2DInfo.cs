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
// Created: June 13, 2016 8:45:14 PM
// 
#endregion

using System;

namespace Gorgon.Graphics.Core
{
	/// <summary>
	/// Information used to create a texture object.
	/// </summary>
	public class GorgonTexture2DInfo 
		: IGorgonTexture2DInfo
	{
		#region Properties.
	    /// <summary>
	    /// Property to return the name of the texture.
	    /// </summary>
	    public string Name
	    {
	        get;
	    }

        /// <summary>
        /// Property to set or return the width of the texture, in pixels.
        /// </summary>
        public int Width
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the height of the texture, in pixels.
		/// </summary>
		/// <remarks>
		/// For textures that have a <see cref="TextureType"/> of <see cref="Core.TextureType.Texture1D"/>, this value is ignored.
		/// </remarks>
		public int Height
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the number of array levels for a 1D or 2D texture.
		/// </summary>
		/// <remarks>
		/// <para>
		/// When this value is greater than 0, the texture will be used as a texture array. If the texture is supposed to be a cube map, then this value should be a multiple of 6 (1 for each face in the cube).
		/// </para>
		/// <para>
		/// For textures that have a <see cref="TextureType"/> of <see cref="Core.TextureType.Texture3D"/>, this value is ignored.
		/// </para>
		/// <para>
		/// This value is defaulted to 1.
		/// </para>
		/// </remarks>
		public int ArrayCount
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return whether this 2D texture is a cube map.
		/// </summary>
		/// <remarks>
		/// <para>
		/// When this value is set to <b>true</b>, then the texture is defined as a cube map using the <see cref="ArrayCount"/> as the number of faces. Because of this, the <see cref="ArrayCount"/> value 
		/// must be a multiple of 6. If it is not, then the array count will be adjusted to meet the requirement.
		/// </para>
		/// <para>
		/// For textures that have a <see cref="TextureType"/> of <see cref="Core.TextureType.Texture1D"/> or <see cref="Core.TextureType.Texture3D"/>, this value is ignored.
		/// </para>
		/// <para>
		/// This value is defaulted to <b>false</b>.
		/// </para>
		/// </remarks>
		public bool IsCubeMap
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the format of the texture.
		/// </summary>
		public BufferFormat Format
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the number of mip-map levels for the texture.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If the texture is multisampled, this value must be set to 1.
		/// </para>
		/// <para>
		/// This value is defaulted to 1.
		/// </para>
		/// </remarks>
		public int MipLevels
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the multisample quality and count for this texture.
		/// </summary>
		/// <remarks>
		/// This value is defaulted to <see cref="GorgonMultisampleInfo.NoMultiSampling"/>.
		/// </remarks>
		public GorgonMultisampleInfo MultisampleInfo
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the intended usage flags for this texture.
		/// </summary>
		/// <remarks>
		/// This value is defaulted to <c>Default</c>.
		/// </remarks>
		public ResourceUsage Usage
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the flags to determine how the texture will be bound with the pipeline when rendering.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If the <see cref="Usage"/> property is set to <c>Staging</c>, then the texture must be created with a value of <see cref="TextureBinding.None"/> as staging textures do not 
		/// support bindings of any kind. If this value is set to anything other than <see cref="TextureBinding.None"/>, an exception will be thrown.
		/// </para>
		/// <para>
		/// This value is defaulted to <see cref="TextureBinding.ShaderResource"/>. 
		/// </para>
		/// </remarks>
		public TextureBinding Binding
		{
			get;
			set;
		}
		#endregion

		#region Constructor/Finalizer.
		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonTexture2DInfo"/> class.
		/// </summary>
		/// <param name="info">A <see cref="IGorgonTexture2DInfo"/> to copy settings from.</param>
		/// <param name="newName">[Optional] The new name for the texture.</param>
		/// <exception cref="ArgumentNullException">Thrown when the <paramref name="info"/> parameter is <b>null</b>.</exception>
		public GorgonTexture2DInfo(IGorgonTexture2DInfo info, string newName = null)
		{
		    if (info == null)
		    {
                throw new ArgumentNullException(nameof(info));
		    }

		    Name = string.IsNullOrEmpty(newName) ? info.Name : newName;
		    Format = info.Format;
			ArrayCount = info.ArrayCount;
			Binding = info.Binding;
			Height = info.Height;
			IsCubeMap = info.IsCubeMap;
			MipLevels = info.MipLevels;
			MultisampleInfo = info.MultisampleInfo;
			Usage = info.Usage;
			Width = info.Width;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonTexture2DInfo"/> class.
		/// </summary>
		/// <param name="name">[Optional] The name of the texture.</param>
		public GorgonTexture2DInfo(string name = null)
		{
		    Name = string.IsNullOrEmpty(name) ? GorgonGraphicsResource.GenerateName(GorgonTexture2D.NamePrefix) : name;
			Binding = TextureBinding.ShaderResource;
			Usage = ResourceUsage.Default;
			MultisampleInfo = GorgonMultisampleInfo.NoMultiSampling;
			MipLevels = 1;
			ArrayCount = 1;
		}
		#endregion
	}
}