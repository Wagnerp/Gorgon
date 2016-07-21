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
// Created: July 20, 2016 10:40:09 PM
// 
#endregion

using System;
using Gorgon.Diagnostics;
using Gorgon.Graphics.Imaging;
using Gorgon.Graphics.Properties;
using DXGI = SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;

namespace Gorgon.Graphics
{
	/// <summary>
	/// Extension methods used to create textures from images.
	/// </summary>
	public static class GorgonImageTextureExtensions
	{
		/// <summary>
		/// Function to create a <see cref="GorgonTexture"/> from a <see cref="GorgonImage"/>.
		/// </summary>
		/// <param name="image">The image used to create the texture.</param>
		/// <param name="name">The name of the texture.</param>
		/// <param name="graphics">The graphics interface used to create the texture.</param>
		/// <param name="info">[Optional] Defines parameters for creating the <see cref="GorgonTexture"/>.</param>
		/// <param name="log">[Optional] The log interface used for debugging.</param>
		/// <returns>A new <see cref="GorgonTexture"/> containing the data from the <paramref name="image"/>.</returns>
		/// <exception cref="ArgumentNullException">Thrown when the <paramref name="image"/>, <paramref name="graphics"/> or the <paramref name="name"/> parameter is <b>null</b>.</exception>
		/// <exception cref="ArgumentException">Thrown when the <paramref name="name"/> parameter is empty.</exception>
		/// <remarks>
		/// <para>
		/// A <see cref="GorgonImage"/> is useful to holding image data in memory, but it cannot be sent to the GPU for use as a texture. This method allows an application to convert the 
		/// <see cref="GorgonImage"/> into a <see cref="GorgonTexture"/>. 
		/// </para>
		/// <para>
		/// The resulting <see cref="GorgonTexture"/> will inherit the <see cref="ImageType"/> (converted to the appropriate <see cref="TextureType"/>), width, height (for 2D/3D images), depth (for 3D images), 
		/// mip map count, array count (for 1D/2D images), and depth count (for 3D images). If the <see cref="GorgonImage"/> being converted has an <see cref="ImageType"/> of <see cref="ImageType.ImageCube"/> 
		/// then the resulting texture will be set to a <see cref="TextureType.Texture2D"/>, and it will have its <see cref="GorgonTextureInfo.IsCubeMap"/> flag set to <b>true</b>.
		/// </para>
		/// <para>
		/// The <paramref name="info"/> parameter, when defined, will allow users to control how the texture is bound to the GPU pipeline, and what its intended usage is going to be, as well as any multisample 
		/// information required to create the texture as a multisample texture. If this parameter is omitted, then the following defaults will be used:
		/// <list type="bullet">
		///		<item>
		///			<term>Binding</term>
		///			<description><see cref="TextureBinding.ShaderResource"/></description>
		///		</item>
		///		<item>
		///			<term>Usage</term>
		///			<description><c>Default</c></description>
		///		</item>
		///		<item>
		///			<term>Multisample info</term>
		///			<description><see cref="GorgonMultiSampleInfo.NoMultiSampling"/></description>
		///		</item>
		/// </list>
		/// </para>
		/// </remarks>
		public static GorgonTexture ToTexture(this IGorgonImage image,
		                                      string name,
											  GorgonGraphics graphics,
		                                      GorgonImageToTextureInfo info = null,
											  IGorgonLog log = null)
		{
			if (image == null)
			{
				throw new ArgumentNullException(nameof(image));
			}

			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (graphics == null)
			{
				throw new ArgumentNullException(nameof(graphics));
			}

			return new GorgonTexture(name, graphics, image, info ?? new GorgonImageToTextureInfo(), log);
		}

		/// <summary>
		/// Function to convert a texture to an image.
		/// </summary>
		/// <param name="texture">The texture to convert to an image.</param>
		/// <returns>A new <see cref="GorgonImage"/> containing the texture data.</returns>
		/// <exception cref="ArgumentNullException">Thrown when the <paramref name="texture"/> parameter is <b>null</b>.</exception>
		/// <exception cref="ArgumentException">Thrown when the <paramref name="texture"/> has a <see cref="GorgonTextureInfo.Usage"/> set to <c>Immutable</c>.</exception>
		public static IGorgonImage ToImage(this GorgonTexture texture)
		{
			if (texture == null)
			{
				throw new ArgumentNullException(nameof(texture));
			}

			if (texture.Info.Usage == D3D11.ResourceUsage.Immutable)
			{
				throw new ArgumentException(string.Format(Resources.GORGFX_ERR_TEXTURE_IMMUTABLE), nameof(texture));
			}

			// TODO: Convert to staging if necessary.
			// TODO: Convert to image.

			throw new NotImplementedException("This is not done yet");
			return null;
		}
	}
}