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
// Created: Friday, February 17, 2012 6:11:50 AM
// 
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimMath;
using GorgonLibrary.Diagnostics;

namespace GorgonLibrary.Graphics.Renderers
{
	/// <summary>
	/// Default blending modes.
	/// </summary>
	public enum BlendingMode
	{
		/// <summary>
		/// No blending.
		/// </summary>
		None = 0,
		/// <summary>
		/// Modulated blending.
		/// </summary>
		Modulate = 1,
		/// <summary>
		/// Additive blending.
		/// </summary>
		Additive = 2,
		/// <summary>
		/// Inverted blending.
		/// </summary>
		Inverted = 3,
		/// <summary>
		/// Pre-multiplied alpha.
		/// </summary>
		PreMultiplied = 4,
		/// <summary>
		/// Custom blending.
		/// </summary>
		Custom = 32767
	}

	/// <summary>
	/// A renderable object.
	/// </summary>
	/// <remarks>This is the base object for any object that can be drawn to a render target.</remarks>
	public abstract class GorgonRenderable
		: GorgonNamedObject
	{
		#region Variables.
		private GorgonTexture2D _texture = null;															// Texture to use for the renderable.
		private GorgonRenderTargetBlendState _blendStates = GorgonRenderTargetBlendState.DefaultStates;		// Blending states.		
		private GorgonTextureSamplerStates _sampler = GorgonTextureSamplerStates.DefaultStates;				// Texture sampler.
		private GorgonRasterizerStates _raster = GorgonRasterizerStates.DefaultStates;						// Rasterizer states.
		private GorgonDepthStencilStates _depthStencil = GorgonDepthStencilStates.DefaultStates;			// Depth/stencil states.
		#endregion

		#region Properties.
		/// <summary>
		/// Property to set or return whether the renderable needs to adjust its dimensions.
		/// </summary>
		protected bool NeedsVertexUpdate
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return whether the texture coordinates need updating.
		/// </summary>
		protected bool NeedsTextureUpdate
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the vertex buffer binding for this renderable.
		/// </summary>
		protected internal virtual GorgonVertexBufferBinding VertexBufferBinding
		{
			get;
			protected set;
		}

		/// <summary>
		/// Property to set or return the index buffer for this renderable.
		/// </summary>
		protected internal virtual GorgonIndexBuffer IndexBuffer
		{
			get
			{
				return Gorgon2D.DefaultIndexBuffer;
			}
		}

		/// <summary>
		/// Property to return the type of primitive for the renderable.
		/// </summary>
		protected internal abstract PrimitiveType PrimitiveType
		{
			get;
		}

		/// <summary>
		/// Property to return a list of vertices to render.
		/// </summary>
		protected internal Gorgon2D.Vertex[] Vertices
		{
			get;
			private set;
		}

		/// <summary>
		/// Property to return the number of indices that make up this renderable.
		/// </summary>
		/// <remarks>This is only matters when the renderable uses an index buffer.</remarks>
		protected internal abstract int IndexCount
		{
			get;
		}

		/// <summary>
		/// Property to return the depth/stencil states.
		/// </summary>
		protected internal GorgonDepthStencilStates DepthStencilState
		{
			get
			{
				return _depthStencil;
			}
			set
			{
				_depthStencil = value;
			}
		}
		
		/// <summary>
		/// Property to set or return the border color for areas outside of the texture.
		/// </summary>
		public virtual GorgonColor BorderColor
		{
			get
			{
				return _sampler.BorderColor;
			}
			set
			{
				_sampler.BorderColor = value;
			}
		}

		/// <summary>
		/// Property to set or return the horizontal wrapping mode for areas outside of the texture.
		/// </summary>
		public virtual TextureAddressing HorizontalWrapping
		{
			get
			{
				return _sampler.HorizontalAddressing;
			}
			set
			{
				_sampler.HorizontalAddressing = value;
			}
		}

		/// <summary>
		/// Property to set or return the vertical wrapping mode for areas outside of the texture.
		/// </summary>
		public virtual TextureAddressing VerticalWrapping
		{
			get
			{
				return _sampler.VerticalAddressing;
			}
			set
			{
				_sampler.VerticalAddressing = value;
			}
		}

		/// <summary>
		/// Property to set or return the type of filtering for the texture.
		/// </summary>
		public virtual TextureFilter TextureFilter
		{
			get
			{
				return _sampler.TextureFilter;
			}
			set
			{
				_sampler.TextureFilter = value;
			}
		}


		/// <summary>
		/// Property to set or return a pre-defined blending mode for the renderable.
		/// </summary>
		/// <remarks>These modes are pre-defined blending states, to get more control over the blending, use the <see cref="P:GorgonLibrary.Graphics.Renderers.GorgonRenderable.SourceBlend">SourceBlend</see> 
		/// or the <see cref="P:GorgonLibrary.Graphics.Renderers.GorgonRenderable.DestinationBlend">DestinationBlend</see> property.</remarks>
		public virtual BlendingMode BlendingMode
		{
			get
			{
				if ((_blendStates.SourceBlend == BlendType.One) && (_blendStates.DestinationBlend == BlendType.Zero))
					return Renderers.BlendingMode.None;

				if (_blendStates.SourceBlend == BlendType.SourceAlpha) 
				{	
					if (_blendStates.DestinationBlend == BlendType.InverseSourceAlpha)
						return Renderers.BlendingMode.Modulate;
					if (_blendStates.DestinationBlend == BlendType.One)
						return Renderers.BlendingMode.Additive;
				}

				if ((_blendStates.SourceBlend == BlendType.One) && (_blendStates.DestinationBlend == BlendType.InverseSourceAlpha))
					return Renderers.BlendingMode.PreMultiplied;

				if ((_blendStates.SourceBlend == BlendType.InverseDestinationColor) && (_blendStates.DestinationBlend == BlendType.InverseSourceColor))
					return Renderers.BlendingMode.Inverted;
				
				return Renderers.BlendingMode.Custom;
			}
			set
			{
				switch (value)
				{
					case Renderers.BlendingMode.Additive:
						_blendStates.SourceBlend = BlendType.SourceAlpha;
						_blendStates.DestinationBlend = BlendType.One;
						break;
					case Renderers.BlendingMode.Inverted:
						_blendStates.SourceBlend = BlendType.InverseDestinationColor;
						_blendStates.DestinationBlend = BlendType.InverseSourceColor;
						break;
					case Renderers.BlendingMode.Modulate:
						_blendStates.SourceBlend = BlendType.SourceAlpha;
						_blendStates.DestinationBlend = BlendType.InverseSourceAlpha;
						break;
					case Renderers.BlendingMode.PreMultiplied:
						_blendStates.SourceBlend = BlendType.One;
						_blendStates.DestinationBlend = BlendType.InverseSourceAlpha;
						break;
					default:
						_blendStates.SourceBlend = BlendType.One;
						_blendStates.DestinationBlend = BlendType.Zero;
						break;
				}
			}
		}

		/// <summary>
		/// Property to set or return the culling mode.
		/// </summary>
		/// <remarks>Use this to make a renderable two-sided.</remarks>
		public CullingMode CullingMode
		{
			get
			{
				return _raster.CullingMode;
			}
			set
			{
				_raster.CullingMode = value;
			}
		}

		/// <summary>
		/// Property to set or return the write mask to mask out specific channels of color (or alpha).
		/// </summary>
		public ColorWriteMaskFlags WriteMask
		{
			get
			{
				return _blendStates.WriteMask;
			}
			set
			{
				_blendStates.WriteMask = value;				
			}
		}

		/// <summary>
		/// Property to set or return the alpha blending operation.
		/// </summary>
		public BlendOperation AlphaOperation
		{
			get
			{
				return _blendStates.AlphaOperation;
			}
			set
			{
				_blendStates.AlphaOperation = value;
			}
		}

		/// <summary>
		/// Property to set or return the blending operation.
		/// </summary>
		public BlendOperation BlendOperation
		{
			get
			{
				return _blendStates.BlendingOperation;
			}
			set
			{
				_blendStates.BlendingOperation = value;
			}
		}

		/// <summary>
		/// Property to set or return the source alpha blending function.
		/// </summary>
		public BlendType SourceAlphaBlend
		{
			get
			{
				return _blendStates.SourceAlphaBlend;
			}
			set
			{
#if DEBUG
				switch (value)
				{
					case BlendType.DestinationColor:
					case BlendType.InverseDestinationColor:
					case BlendType.InverseSecondarySourceColor:
					case BlendType.InverseSourceColor:
					case BlendType.SecondarySourceColor:
					case BlendType.SourceColor:
						throw new ArgumentException("Cannot use a color operation with an alpha blend function.");
				}
#endif
				_blendStates.SourceAlphaBlend = value;
			}
		}

		/// <summary>
		/// Property to set or return the destination alpha blending function.
		/// </summary>
		public BlendType DestinationAlphaBlend
		{
			get
			{
				return _blendStates.DestinationAlphaBlend;
			}
			set
			{
#if DEBUG
				switch (value)
				{
					case BlendType.DestinationColor:
					case BlendType.InverseDestinationColor:
					case BlendType.InverseSecondarySourceColor:
					case BlendType.InverseSourceColor:
					case BlendType.SecondarySourceColor:
					case BlendType.SourceColor:
						throw new ArgumentException("Cannot use a color operation with an alpha blend function.");
				}
#endif
				_blendStates.DestinationAlphaBlend = value;
			}
		}

		/// <summary>
		/// Property to set or return the blend factor.
		/// </summary>
		/// <remarks>This is only valid when the <see cref="P:GorgonLibrary.Graphics.Renderers.GorgonRenderable.SourceBlend">SourceBlend</see> or the <see cref="P:GorgonLibrary.Graphics.Renderers.GorgonRenderable.DestinationBlend">DestinationBlend</see> are set to BlendFactor.</remarks>
		public GorgonColor BlendFactor
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the source blending function.
		/// </summary>
		public BlendType SourceBlend
		{
			get
			{				
				return _blendStates.SourceBlend;
			}
			set
			{
				_blendStates.SourceBlend = value;
			}
		}

		/// <summary>
		/// Property to set or return the destination blending function.
		/// </summary>
		public BlendType DestinationBlend
		{
			get
			{
				return _blendStates.DestinationBlend;
			}
			set
			{
				_blendStates.DestinationBlend = value;
			}
		}

		/// <summary>
		/// Property to set or return the range of alpha values to reject on this renderable.
		/// </summary>
		/// <remarks>The alpha testing tests to see if an alpha value is between or equal to the values and rejects the pixel if it is not.
		/// <para>This value will not take effect until <see cref="P:GorgonLibrary.Graphics.Renderers.Gorgon2D.IsAlphaTestEnabled">IsAlphaTestEnabled</see> is set to TRUE.</para>
		/// <para>Typically, performance is improved when alpha testing is turned on with a range of 0.  This will reject any pixels with an alpha of 0.</para>
		/// <para>Be aware that the default shaders implement alpha testing.  However, a custom shader will have to make use of the GorgonAlphaTest constant buffer 
		/// in order to take advantage of alpha testing.</para>
		/// </remarks>
		public virtual GorgonMinMaxF AlphaTestValues
		{
			get;
			set;
		}

		/// <summary>
		/// Property to set or return the opacity (Alpha channel) of the renderable object.
		/// </summary>
		/// <remarks>This will only return the alpha value for the first vertex of the renderable and consequently will set all the vertices to the same alpha value.</remarks>
		public virtual float Opacity
		{
			get
			{
				return Vertices[0].Color.Alpha;
			}
			set
			{
				if (value != Vertices[0].Color.Alpha)
				{
					for (int i = 0; i < Vertices.Length; i++)
						Vertices[i].Color.Alpha = value;
				}
			}
		}

		/// <summary>
		/// Property to set or return the color for a renderable object.
		/// </summary>
		/// <remarks>This will only return the color for the first vertex of the renderable and consequently will set all the vertices to the same color.</remarks>
		public virtual GorgonColor Color
		{
			get
			{
				return Vertices[0].Color;
			}
			set
			{
				GorgonColor current = Vertices[0].Color;

				if (value != current)
				{
					for (int i = 0; i < Vertices.Length; i++)
						Vertices[i].Color = value;
				}
			}
		}

		/// <summary>
		/// Property to set or return a texture for the renderable.
		/// </summary>
		public virtual GorgonTexture2D Texture
		{
			get
			{
				return _texture;
			}
			set
			{
				if (_texture != value)
				{
					_texture = value;

					NeedsTextureUpdate = true;
				}
			}
		}

		/// <summary>
		/// Property to return the Gorgon 2D interface that created this object.
		/// </summary>
		public Gorgon2D Gorgon2D
		{
			get;
			private set;
		}
		#endregion

		#region Methods.

		/// <summary>
		/// Function to set up any additional information for the renderable.
		/// </summary>
		protected abstract void InitializeCustomVertexInformation();

		/// <summary>
		/// Function to initialize the vertex 
		/// </summary>
		/// <param name="vertexCount">Number of vertices in this renderable object.</param>
		protected void InitializeVertices(int vertexCount)
		{
			Vertices = new Gorgon2D.Vertex[vertexCount];

			for (int i = 0; i < Vertices.Length; i++)
			{
				Vertices[i].Color = new GorgonColor(1.0f, 1.0f, 1.0f, 1.0f);
				Vertices[i].Position = new Vector4(0, 0, 0, 1.0f);
				Vertices[i].UV = Vector2.Zero;
			}

			InitializeCustomVertexInformation();
		}		

		/// <summary>
		/// Function to draw the object.
		/// </summary>
		/// <remarks>Please note that this doesn't draw the object to the target right away, but queues it up to be 
		/// drawn when <see cref="M:GorgonLibrary.Graphics.Renderers.Gorgon2D.Render">Render</see> is called.
		/// </remarks>
		public virtual void Draw()
		{
			Gorgon2D.AddRenderable(this);
		}
		#endregion

		#region Constructor.
		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonRenderable"/> class.
		/// </summary>
		/// <param name="gorgon2D">Gorgon 2D interface.</param>
		/// <param name="name">The name.</param>
		/// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="name"/> parameter is NULL (Nothing in VB.Net).
		/// <para>-or-</para>
		/// <para>Thrown when the <paramref name="gorgon2D"/> parameter is NULL.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">Thrown when the <paramref name="name"/> parameter is an empty string.</exception>
		protected GorgonRenderable(Gorgon2D gorgon2D, string name)
			: base(name)
		{			
			GorgonDebug.AssertNull<Gorgon2D>(gorgon2D, "gorgon2D");
						
			Gorgon2D = gorgon2D;
			_raster.CullingMode = Graphics.CullingMode.Back;
			_blendStates.IsBlendingEnabled = true;
			_blendStates.SourceBlend = BlendType.SourceAlpha;
			_blendStates.DestinationBlend = BlendType.InverseSourceAlpha;
			_sampler = GorgonTextureSamplerStates.DefaultStates;
			_sampler.TextureFilter = Graphics.TextureFilter.Point;			
			VertexBufferBinding = gorgon2D.DefaultVertexBufferBinding;
			AlphaTestValues = GorgonMinMaxF.Empty;
		}
		#endregion
	}
}