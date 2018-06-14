﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2018 Michael Winsor
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
// Created: June 6, 2018 12:53:53 PM
// 
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using Gorgon.Core;
using DX = SharpDX;
using Gorgon.Diagnostics;
using Gorgon.Graphics;
using Gorgon.Graphics.Core;
using Gorgon.Graphics.Fonts;
using Gorgon.Math;
using Gorgon.Renderers.Properties;
using Gorgon.UI;

namespace Gorgon.Renderers
{
    /// <summary>
    /// TODO: Fill me in.
    /// </summary>
    public class Gorgon2D
        : IDisposable, IGorgonGraphicsObject
    {
        #region Variables.
        // The flag to indicate that the renderer is initialized.
        private bool _initialized;
        // The primary render target.
        private readonly GorgonRenderTarget2DView _primaryTarget;
        // The default vertex shader used by the renderer.
        private Gorgon2DShader<GorgonVertexShader> _defaultVertexShader = new Gorgon2DShader<GorgonVertexShader>();
        // The default pixel shader used by the renderer.
        private Gorgon2DShader<GorgonPixelShader> _defaultPixelShader = new Gorgon2DShader<GorgonPixelShader>();
        // The layout used to define a vertex to the vertex shader.
        private GorgonInputLayout _vertexLayout;
        // The renderer used to draw sprites.
        private SpriteRenderer _spriteRenderer;
        // The default texture to render.
        private GorgonTexture2DView _defaultTexture;
        // The buffer that holds the view and projection matrices.
        private GorgonConstantBufferView _viewProjection;
        // The buffer used to perform alpha testing.
        private GorgonConstantBufferView _alphaTest;
        // A factory used to create draw calls.
        private DrawCallFactory _drawCallFactory;
        // The currently active draw call.
        private GorgonDrawIndexCall _currentDrawCall;
        // The previously assigned batch state.
        private readonly Gorgon2DBatchState _lastBatchState = new Gorgon2DBatchState();
        // The last sprite that was put into the system.
        private BatchRenderable _lastRenderable;
        // The current alpha test data.
        private AlphaTestData _alphaTestData;
        // Flag to indicate that the begin method has been called.
        private int _beginCalled;
        // A buffer used for text manipulation in the DrawText method.
        private readonly StringBuilder _textBuffer = new StringBuilder(256);
        // The default font.
        private Lazy<GorgonFontFactory> _defaultFontFactory;
        // The default text sprite for rendering strings.
        private GorgonTextSprite _defaultTextSprite;
        // The renderable for primitives (lines, rectangles, etc...)
        private readonly BatchRenderable _primitiveRenderable = new BatchRenderable
                                                                {
                                                                    Vertices = new Gorgon2DVertex[4]
                                                                };
        // The currently active viewport.
        private DX.ViewportF _currentViewport;
        #endregion

        #region Properties.
        /// <summary>
        /// Property to return the log used to log debug messages.
        /// </summary>
        public IGorgonLog Log => _primaryTarget.Graphics.Log;

        /// <summary>
        /// Property to return the <see cref="GorgonGraphics"/> interface that owns this renderer.
        /// </summary>
        public GorgonGraphics Graphics => _primaryTarget.Graphics;
        #endregion

        #region Methods.
        /// <summary>
        /// Function to update the alpha test data.
        /// </summary>
        /// <param name="currentData">The data to write into the buffer.</param>
        private void UpdateAlphaTest(ref AlphaTestData currentData)
        {
            if (currentData.Equals(_alphaTestData))
            {
                return;
            }

            _alphaTest.Buffer.SetData(ref currentData);
            _alphaTestData = currentData;
        }

        /// <summary>
        /// Function to initialize the renderer.
        /// </summary>
        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (!GorgonShaderFactory.Includes.ContainsKey("Gorgon2DShaders"))
            {
                GorgonShaderFactory.Includes["Gorgon2DShaders"] = new GorgonShaderInclude("Gorgon2DShaders", Resources.BasicSprite);
            }

            _defaultVertexShader.Shader = GorgonShaderFactory.Compile<GorgonVertexShader>(Graphics, Resources.BasicSprite, "GorgonVertexShader", GorgonGraphics.IsDebugEnabled);
            _defaultPixelShader.Shader = GorgonShaderFactory.Compile<GorgonPixelShader>(Graphics, Resources.BasicSprite, "GorgonPixelShaderTextured", GorgonGraphics.IsDebugEnabled);

            _vertexLayout = GorgonInputLayout.CreateUsingType<Gorgon2DVertex>(Graphics, _defaultVertexShader.Shader);

            // We need to ensure that we have a default texture in case we decide not to send a texture in.
            GorgonTexture2D textureResource = Resources.White_2x2.ToTexture2D(Graphics,
                                                                              new GorgonTextureLoadOptions
                                                                              {
                                                                                  Name = "Default White 2x2 Texture",
                                                                                  Binding = TextureBinding.ShaderResource,
                                                                                  Usage = ResourceUsage.Immutable
                                                                              });
            _defaultTexture = textureResource.GetShaderResourceView();

            // Set up the sprite renderer buffers.
            DX.Matrix.OrthoOffCenterLH(0,
                                       _primaryTarget.Width,
                                       _primaryTarget.Height,
                                       0,
                                       0.0f,
                                       1.0f,
                                       out DX.Matrix projection);

            _viewProjection = GorgonConstantBufferView.CreateConstantBuffer(Graphics, ref projection, "View * Projection Matrix Buffer");

            _alphaTestData = new AlphaTestData(true, GorgonRangeF.Empty);
            _alphaTest = GorgonConstantBufferView.CreateConstantBuffer(Graphics, ref _alphaTestData, "Alpha Test Buffer");

            _spriteRenderer = new SpriteRenderer(Graphics);
            _drawCallFactory = new DrawCallFactory(Graphics, _defaultTexture, _vertexLayout)
                               {
                                   ProjectionViewBuffer = _viewProjection,
                                   AlphaTestBuffer = _alphaTest
                               };

            // Set up the initial state.
            _lastBatchState.PixelShader = _defaultPixelShader;
            _lastBatchState.VertexShader = _defaultVertexShader;
            _lastBatchState.BlendState = GorgonBlendState.Default;
            _lastBatchState.RasterState = GorgonRasterState.Default;
            _lastBatchState.DepthStencilState = GorgonDepthStencilState.Default;

            // Set the initial render target.
            if (Graphics.RenderTargets[0] == null)
            {
                Graphics.SetRenderTarget(_primaryTarget);
                _currentViewport = Graphics.Viewports[0];
            }

            _defaultTextSprite = new GorgonTextSprite(_defaultFontFactory.Value.DefaultFont);

            _initialized = true;
        }

        /// <summary>
        /// Function to begin rendering.
        /// </summary>
        /// <param name="batchState">[Optional] Defines common global state to use when rendering a batch of objects.</param>
        public void Begin(Gorgon2DBatchState batchState = null)
        {
            if (Interlocked.Exchange(ref _beginCalled, 1) == 1)
            {
                return;
            }

            // If we're not initialized, then do so now.
            // Note that this is not thread safe.
            if (!_initialized)
            {
                Initialize();
            }

            _lastRenderable = null;
            _lastBatchState.PixelShader = batchState?.PixelShader ?? _defaultPixelShader;
            _lastBatchState.VertexShader = batchState?.VertexShader ?? _defaultVertexShader;
            _lastBatchState.BlendState = batchState?.BlendState ?? GorgonBlendState.Default;
            _lastBatchState.RasterState = batchState?.RasterState ?? GorgonRasterState.Default;
            _lastBatchState.DepthStencilState = batchState?.DepthStencilState ?? GorgonDepthStencilState.Default;
            
            // If we didn't assign shaders, then use our defaults.
            if (_lastBatchState.PixelShader.Shader == null)
            {
                _lastBatchState.PixelShader.Shader = _defaultPixelShader.Shader;
            }

            if (_lastBatchState.PixelShader.RwConstantBuffers[0] == null)
            {
                _lastBatchState.PixelShader.RwConstantBuffers[0] = _alphaTest;
            }

            if (_lastBatchState.VertexShader.Shader == null)
            {
                _lastBatchState.VertexShader.Shader = _defaultVertexShader.Shader;
            }

            if (_lastBatchState.VertexShader.RwConstantBuffers[0] == null)
            {
                _lastBatchState.VertexShader.RwConstantBuffers[0] = _viewProjection;
            }
        }

        /// <summary>
        /// Function to check for changes in the batch state, and render the previous batch if necessary.
        /// </summary>
        /// <param name="renderable"></param>
        private void RenderBatchOnChange(BatchRenderable renderable)
        {
            // If we're sending the same guy in, there's no point in jumping through all of these hoops.
            if ((_lastRenderable != null) && (_spriteRenderer.RenderableStateComparer.Equals(_lastRenderable, renderable)))
            {
                return;
            }

            GorgonDrawIndexCall drawCall = _drawCallFactory.GetDrawIndexCall(renderable, _lastBatchState, _spriteRenderer);

            if ((_currentDrawCall != null) && (drawCall != _currentDrawCall))
            {
                if (_lastRenderable != null)
                {
                    UpdateAlphaTest(ref _lastRenderable.AlphaTestData);
                }

                _spriteRenderer.RenderBatches(_currentDrawCall);
            }

            _lastRenderable = renderable;
            // All states are reconciled, so reset the change flag.
            _lastRenderable.StateChanged = false;

            _currentDrawCall = drawCall;
        }

        /// <summary>
        /// Function called when a render target is changed on the main graphics interface.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event parameters.</param>
        private void RenderTarget_Changed(object sender, EventArgs e)
        {
            // If we've not been initialized yet, do so now.
            if (!_initialized)
            {
                Initialize();
            }

            DX.ViewportF viewPort = Graphics.Viewports[0];

            // Nothing's changed, so get out.
            if (viewPort.Equals(ref _currentViewport))
            {
                return;
            }

            // Set up the sprite renderer buffers.
            DX.Matrix.OrthoOffCenterLH(0,
                                       viewPort.Width,
                                       viewPort.Height,
                                       0,
                                       0.0f,
                                       1.0f,
                                       out DX.Matrix projection);

            _viewProjection.Buffer.SetData(ref projection);

            _currentViewport = viewPort;
        }

        /// <summary>
        /// Function to draw a sprite.
        /// </summary>
        /// <param name="sprite"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Begin"/> has not been called prior to calling this method.</exception>
        public void DrawSprite(GorgonSprite sprite)
        {
            sprite.ValidateObject(nameof(sprite));

            if (_beginCalled == 0)
            {
                throw new InvalidOperationException(Resources.GOR2D_ERR_BEGIN_NOT_CALLED);
            }

            BatchRenderable renderable = sprite.Renderable;

            RenderBatchOnChange(renderable);

            if (sprite.IsUpdated)
            {
                _spriteRenderer.SpriteTransformer.Transform(renderable);
            }

            _spriteRenderer.QueueSprite(renderable, 6);
        }

        /// <summary>
        /// Function to draw text.
        /// </summary>
        /// <param name="text">The text to render.</param>
        /// <param name="position">The position of the text.</param>
        /// <param name="font">[Optional] The font to use.</param>
        /// <param name="color">[Optional] The color of the text.</param>
        public void DrawString(string text, DX.Vector2 position, GorgonFont font = null, GorgonColor? color = null)
        {
            // We have nothing to render.
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _defaultTextSprite.Text = text;
            _defaultTextSprite.Color = color ?? GorgonColor.White;
            _defaultTextSprite.Position = position;
            _defaultTextSprite.Font = font ?? _defaultFontFactory.Value.DefaultFont;
            _defaultTextSprite.AllowColorCodes = (text.IndexOf("[c", StringComparison.CurrentCultureIgnoreCase) > -1)
                                                 && (text.IndexOf("[/c]", StringComparison.CurrentCultureIgnoreCase) > -1);

            DrawTextSprite(_defaultTextSprite);
        }

        /// <summary>
        /// Function to draw text.
        /// </summary>
        /// <param name="sprite">The text sprite to render.</param>
        public void DrawTextSprite(GorgonTextSprite sprite)
        {
            // The number of characters evaluated.
            int charCount = 0;
            // The index into the vertex array for the sprite.
            int vertexOffset = 0;
            // The position of the current glyph.
            DX.Vector2 position = DX.Vector2.Zero;
            // The number of indices to render.
            int indexCount = 0;

            if (_beginCalled == 0)
            {
                throw new InvalidOperationException(Resources.GOR2D_ERR_BEGIN_NOT_CALLED);
            }

            sprite.ValidateObject(nameof(sprite));

            _textBuffer.Length = 0;
            int textLength = sprite.Text.Length;
            
            // If there's no text, then there's nothing to render.
            if (textLength == 0)
            {
                return;
            }

            TextRenderable renderable = sprite.Renderable;

            // Flush the previous batch if we have one that's different from the upcoming batch.
            RenderBatchOnChange(renderable);

            _textBuffer.Append(sprite.Text);
            
            Alignment alignment = renderable.Alignment;
            GorgonFont font = renderable.Font;
            bool drawOutlines = ((renderable.DrawMode != TextDrawMode.GlyphsOnly) && (font.HasOutline));
            int drawCount = ((drawOutlines) && (renderable.DrawMode == TextDrawMode.OutlinedGlyphs))? 2 : 1;
            float fontHeight = font.FontHeight;
            bool hasKerning = (font.Info.UseKerningPairs) && (font.KerningPairs.Count > 0);
            IDictionary<GorgonKerningPair, int> kerningValues = font.KerningPairs;
            float lineSpaceMultiplier = renderable.LineSpaceMultiplier;
            List<ColorBlock> colorBlocks = renderable.ColorBlocks;
            bool allowColorCodes = sprite.AllowColorCodes;
            bool isUpdated = sprite.IsUpdated;
            
            renderable.ActualVertexCount = 0;

            for (int line = 0; line < renderable.Lines.Length; ++line)
            {
                string textLine = sprite.Lines[line];
                textLength = textLine.Length;

                for (int dc = 0; dc < drawCount; ++dc)
                {
                    bool isOutlinePass = (drawOutlines) && (dc == 0);

                    DX.Size2F lineMeasure = DX.Size2F.Empty;

                    if (alignment != Alignment.UpperLeft)
                    {
                        lineMeasure = font.MeasureLine(textLine, (drawOutlines) && (dc == 0), lineSpaceMultiplier);
                    }

                    position.X = 0;

                    for (int i = 0; i < textLength; ++i)
                    {
                        char character = textLine[i];
                        int kernAmount = 0;

                        // Find the color block for the text.
                        GorgonColor? blockColor = null;

                        if (!font.Glyphs.TryGetValue(character, out GorgonGlyph glyph))
                        {
                            if (!font.TryGetDefaultGlyph(out glyph))
                            {
                                // Only update when we're in non-outline.
                                if (!isOutlinePass)
                                {
                                    ++charCount;
                                }
                                continue;
                            }
                        }

                        // Handle whitespace by just advancing our position, we don't need geometry for this.
                        if ((char.IsWhiteSpace(character))
                            || (glyph.TextureView == null))
                        {
                            if (character == '\t')
                            {
                                position.X += glyph.Advance * renderable.TabSpaceCount;
                            }
                            // We don't use carriage returns.
                            else if (character != '\r')
                            {
                                position.X += glyph.Advance;
                            }

                            // Only update when we're in non-outline.
                            if (!isOutlinePass)
                            {
                                ++charCount;
                            }
                            continue;
                        }

                        // If we have a change of texture, then we need to let the renderer know that we need a flush.
                        if ((renderable.Texture != null) && (renderable.Texture != glyph.TextureView))
                        {
                            RenderBatchOnChange(renderable);
                            renderable.HasTextureChanges = true;
                        }

                        renderable.Texture = glyph.TextureView;

                        if (isUpdated)
                        {
                            if ((allowColorCodes) && (!isOutlinePass))
                            {
                                blockColor = _spriteRenderer.TextSpriteTransformer.GetColorForCharacter(charCount, colorBlocks);
                            }

                            if ((blockColor != null) && (!renderable.HasVertexColorChanges))
                            {
                                renderable.HasVertexColorChanges = true;
                            }

                            if ((hasKerning) && (i < textLength - 1))
                            {
                                var kernPair = new GorgonKerningPair(character, textLine[i + 1]);
                                kerningValues.TryGetValue(kernPair, out kernAmount);
                            }


                            _spriteRenderer.TextSpriteTransformer.Transform(renderable,
                                                                            glyph,
                                                                            blockColor,
                                                                            ref position,
                                                                            vertexOffset,
                                                                            isOutlinePass,
                                                                            lineMeasure.Width);

                            vertexOffset += 4;
                            position.X += glyph.Advance + kernAmount;
                        }

                        // Only update when we're in non-outline.
                        if (!isOutlinePass)
                        {
                            ++charCount;
                        }

                        indexCount += 6;
                        renderable.ActualVertexCount += 4;
                    }

                }

                // This is to account for the new line character.
                ++charCount;
                position.Y += fontHeight * lineSpaceMultiplier;
            }

            if (indexCount != 0)
            {
                _spriteRenderer.QueueSprite(renderable, indexCount);
            }
            
            renderable.VertexCountChanged = false;
            renderable.HasTransformChanges = false;
            renderable.HasVertexChanges = false;
            renderable.HasTextureChanges = false;
            renderable.HasVertexColorChanges = false;
        }

        /// <summary>
        /// Function to draw a filled rectangle.
        /// </summary>
        /// <param name="region">The region for the rectangle.</param>
        /// <param name="color">The color of the rectangle.</param>
        /// <param name="texture">[Optional] The texture for the rectangle.</param>
        /// <param name="textureRegion">[Optional] The texture coordinates to map to the rectangle.</param>
        /// <param name="textureArrayIndex">[Optional] The array index for a texture array to use.</param>
        /// <param name="textureSampler">[Optional] The texture sampler to apply to the texture.</param>
        /// <param name="depth">[Optional] The depth value for the rectangle.</param>
        public void DrawFilledRectangle(DX.RectangleF region, GorgonColor color, GorgonTexture2DView texture = null, DX.RectangleF? textureRegion = null, int textureArrayIndex = 0, GorgonSamplerState textureSampler = null, float depth = 0)
        {
            if (_beginCalled == 0)
            {
                throw new InvalidOperationException(Resources.GOR2D_ERR_BEGIN_NOT_CALLED);
            }

            // If there's no width/height, then there's nothing to draw.
            if (region.IsEmpty)
            {
                return;
            }

            ref Gorgon2DVertex v0 = ref _primitiveRenderable.Vertices[0];
            ref Gorgon2DVertex v1 = ref _primitiveRenderable.Vertices[1];
            ref Gorgon2DVertex v2 = ref _primitiveRenderable.Vertices[2];
            ref Gorgon2DVertex v3 = ref _primitiveRenderable.Vertices[3];

            if (textureSampler == null)
            {
                textureSampler = GorgonSamplerState.Wrapping;
            }

            if (texture != null)
            {
                textureArrayIndex = textureArrayIndex.Max(0);

                if (textureRegion == null)
                {
                    // Calculate the texture.
                    v0.UV = new DX.Vector3(region.Left / texture.Width, region.Top / texture.Height, textureArrayIndex);
                    v1.UV = new DX.Vector3(region.Right / texture.Width, region.Top / texture.Height, textureArrayIndex);
                    v2.UV = new DX.Vector3(region.Left / texture.Width, region.Bottom / texture.Height, textureArrayIndex);
                    v3.UV = new DX.Vector3(region.Right / texture.Width, region.Bottom / texture.Height, textureArrayIndex);
                }
                else
                {
                    v0.UV = new DX.Vector3(textureRegion.Value.TopLeft, textureArrayIndex);
                    v1.UV = new DX.Vector3(textureRegion.Value.TopRight, textureArrayIndex);
                    v2.UV = new DX.Vector3(textureRegion.Value.BottomLeft, textureArrayIndex);
                    v3.UV = new DX.Vector3(textureRegion.Value.BottomRight, textureArrayIndex);
                }
            }
            else
            {
                v0.UV = DX.Vector3.Zero;
                v1.UV = new DX.Vector3(1.0f, 0, 0);
                v2.UV = new DX.Vector3(0, 1.0f, 0);
                v3.UV = new DX.Vector3(1.0f, 1.0f, 0);
                
                texture = _defaultTexture;
            }

            v0.Color = color;
            v1.Color = color;
            v2.Color = color;
            v3.Color = color;

            v0.Position = new DX.Vector4(region.TopLeft, depth, 1.0f);
            v1.Position = new DX.Vector4(region.TopRight, depth, 1.0f);
            v2.Position = new DX.Vector4(region.BottomLeft, depth, 1.0f);
            v3.Position = new DX.Vector4(region.BottomRight, depth, 1.0f);

            var alphaTestData = new AlphaTestData(true, GorgonRangeF.Empty);

            _primitiveRenderable.StateChanged = (texture != _primitiveRenderable.Texture)
                                                || (textureSampler != _primitiveRenderable.TextureSampler)
                                                || (!AlphaTestData.Equals(in alphaTestData, in _primitiveRenderable.AlphaTestData));

            _primitiveRenderable.Bounds = region;
            _primitiveRenderable.ActualVertexCount = 4;
            _primitiveRenderable.AlphaTestData = new AlphaTestData(true, GorgonRangeF.Empty);
            _primitiveRenderable.Texture = texture;
            _primitiveRenderable.TextureSampler = textureSampler;

            RenderBatchOnChange(_primitiveRenderable);
            
            _spriteRenderer.QueueSprite(_primitiveRenderable, 6);
        }

        /// <summary>
        /// Function to draw a filled rectangle.
        /// </summary>
        /// <param name="region">The region for the rectangle.</param>
        /// <param name="color">The color of the rectangle.</param>
        /// <param name="thickness">[Optional] The line thickness.</param>
        /// <param name="texture">[Optional] The texture for the rectangle.</param>
        /// <param name="textureRegion">[Optional] The texture coordinates to map to the rectangle.</param>
        /// <param name="textureArrayIndex">[Optional] The array index for a texture array to use.</param>
        /// <param name="textureSampler">[Optional] The texture sampler to apply to the texture.</param>
        /// <param name="depth">[Optional] The depth value for the rectangle.</param>
        public void DrawRectangle(DX.RectangleF region,
                                  GorgonColor color,
                                  float thickness = 1.0f,
                                  GorgonTexture2DView texture = null,
                                  DX.RectangleF? textureRegion = null,
                                  int textureArrayIndex = 0,
                                  GorgonSamplerState textureSampler = null,
                                  float depth = 0)
        {
            if (_beginCalled == 0)
            {
                throw new InvalidOperationException(Resources.GOR2D_ERR_BEGIN_NOT_CALLED);
            }

            // If there's no width/height or thickness, then there's nothing to draw.
            if ((region.IsEmpty) || (thickness <= 0.0f))
            {
                return;
            }

            // Push borders to the outside.
            if (thickness > 1.0f)
            {
                region.Inflate(thickness / 2.0f, thickness / 2.0f);
            }

            // Top Across.
            DrawFilledRectangle(new DX.RectangleF(region.X, region.Y, region.Width, thickness),
                                color,
                                texture,
                                textureRegion,
                                textureArrayIndex,
                                textureSampler,
                                depth);

            // Right down.
            DrawFilledRectangle(new DX.RectangleF(region.Right - thickness, region.Y, thickness, region.Height),
                                color,
                                texture,
                                textureRegion,
                                textureArrayIndex,
                                textureSampler,
                                depth);

            // Bottom across.
            DrawFilledRectangle(new DX.RectangleF(region.X, region.Bottom - thickness, region.Width, thickness),
                                color,
                                texture,
                                textureRegion,
                                textureArrayIndex,
                                textureSampler,
                                depth);

            // Left down.
            DrawFilledRectangle(new DX.RectangleF(region.X, region.Y, thickness, region.Height),
                                color,
                                texture,
                                textureRegion,
                                textureArrayIndex,
                                textureSampler,
                                depth);
        }

        /// <summary>
        /// Function to draw a line.
        /// </summary>
        /// <param name="x1">The starting horizontal position.</param>
        /// <param name="y1">The starting vertical position.</param>
        /// <param name="x2">The ending horizontal position.</param>
        /// <param name="y2">The ending vertical position.</param>
        /// <param name="color">The color of the line.</param>
        /// <param name="thickness">[Optional] The line thickness.</param>
        /// <param name="texture">[Optional] The texture to render on the line.</param>
        /// <param name="textureRegion">[Optional] The texture coordinates to map to the rectangle.</param>
        /// <param name="textureArrayIndex">[Optional] The array index for a texture array to use.</param>
        /// <param name="textureSampler">[Optional] The texture sampler to apply to the texture.</param>
        /// <param name="startDepth">[Optional] The depth value for the starting point of the line.</param>
        /// <param name="endDepth">[Optional] The depth value for the ending point of the line.</param>
        public void DrawLine(float x1, float y1, float x2, float y2, GorgonColor color, float thickness = 1.0f, GorgonTexture2DView texture = null, DX.RectangleF? textureRegion = null, int textureArrayIndex = 0, GorgonSamplerState textureSampler = null, float startDepth = 0, float endDepth = 0)
        {
            if (_beginCalled == 0)
            {
                throw new InvalidOperationException(Resources.GOR2D_ERR_BEGIN_NOT_CALLED);
            }

            // There's nothing to render.
            if (((x2 == x1) && (y2 == y1)) || (thickness <= 0.0f))
            {
                return;
            }

            var topLeft = new DX.Vector2(x1, y1);
            var bottomRight = new DX.Vector2(x2, y2);
            
            ref Gorgon2DVertex v0 = ref _primitiveRenderable.Vertices[0];
            ref Gorgon2DVertex v1 = ref _primitiveRenderable.Vertices[1];
            ref Gorgon2DVertex v2 = ref _primitiveRenderable.Vertices[2];
            ref Gorgon2DVertex v3 = ref _primitiveRenderable.Vertices[3];

            if (textureSampler == null)
            {
                textureSampler = GorgonSamplerState.Wrapping;
            }

            DX.Vector2.Subtract(ref bottomRight, ref topLeft, out DX.Vector2 length);
            // Get cross products of start and end points.
            var cross = new DX.Vector2(length.Y, -length.X);
            cross.Normalize();

            DX.Vector2.Multiply(ref cross, thickness / 2.0f, out cross);
            
            var start1 = new DX.Vector2((x1 + cross.X).FastCeiling(), (y1 + cross.Y).FastCeiling());
            var end1 = new DX.Vector2((x2 + cross.X).FastCeiling(), (y2 + cross.Y).FastCeiling());
            var start2 = new DX.Vector2((x1 - cross.X).FastCeiling(), (y1 - cross.Y).FastCeiling());
            var end2 = new DX.Vector2((x2 - cross.X).FastCeiling(), (y2 - cross.Y).FastCeiling());
            
            if (texture != null)
            {
                textureArrayIndex = textureArrayIndex.Max(0);

                if (textureRegion == null)
                {
                    // Calculate the texture.
                    v0.UV = new DX.Vector3(start1.X / texture.Width, start1.Y / texture.Height, textureArrayIndex);
                    v1.UV = new DX.Vector3(end1.X / texture.Width, end1.Y / texture.Height, textureArrayIndex);
                    v2.UV = new DX.Vector3(start2.X / texture.Width, start2.Y / texture.Height, textureArrayIndex);
                    v3.UV = new DX.Vector3(end2.X / texture.Width, end2.Y / texture.Height, textureArrayIndex);
                }
                else
                {
                    v0.UV = new DX.Vector3(textureRegion.Value.TopLeft, textureArrayIndex);
                    v1.UV = new DX.Vector3(textureRegion.Value.TopRight, textureArrayIndex);
                    v2.UV = new DX.Vector3(textureRegion.Value.BottomLeft, textureArrayIndex);
                    v3.UV = new DX.Vector3(textureRegion.Value.BottomRight, textureArrayIndex);
                }
            }
            else
            {
                v0.UV = DX.Vector3.Zero;
                v1.UV = new DX.Vector3(1.0f, 0, 0);
                v2.UV = new DX.Vector3(0, 1.0f, 0);
                v3.UV = new DX.Vector3(1.0f, 1.0f, 0);
                
                texture = _defaultTexture;
            }
            

            v0.Color = color;
            v1.Color = color;
            v2.Color = color;
            v3.Color = color;

            v0.Position = new DX.Vector4(start1, startDepth, 1.0f);
            v1.Position = new DX.Vector4(end1, startDepth, 1.0f);
            v2.Position = new DX.Vector4(start2, startDepth, 1.0f);
            v3.Position = new DX.Vector4(end2, startDepth, 1.0f);

            var alphaTestData = new AlphaTestData(true, GorgonRangeF.Empty);

            _primitiveRenderable.StateChanged = (texture != _primitiveRenderable.Texture)
                                                || (textureSampler != _primitiveRenderable.TextureSampler)
                                                || (!AlphaTestData.Equals(in alphaTestData, in _primitiveRenderable.AlphaTestData));

            _primitiveRenderable.Bounds = new DX.RectangleF(x1, y1, length.X, length.Y);
            _primitiveRenderable.ActualVertexCount = 4;
            _primitiveRenderable.AlphaTestData = new AlphaTestData(true, GorgonRangeF.Empty);
            _primitiveRenderable.Texture = texture;
            _primitiveRenderable.TextureSampler = textureSampler;

            RenderBatchOnChange(_primitiveRenderable);
            
            _spriteRenderer.QueueSprite(_primitiveRenderable, 6);
        }

        /// <summary>
        /// Function to end rendering.
        /// </summary>
        public void End()
        {
            if (Interlocked.Exchange(ref _beginCalled, 0) == 0)
            {
                return;
            }

            if (_lastRenderable != null)
            {
                UpdateAlphaTest(ref _lastRenderable.AlphaTestData);
            }
            
            _spriteRenderer.RenderBatches(_currentDrawCall);
            _currentDrawCall = null;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Gorgon2DShader<GorgonVertexShader> vertexShader = Interlocked.Exchange(ref _defaultVertexShader, null);
            Gorgon2DShader<GorgonPixelShader> pixelShader = Interlocked.Exchange(ref _defaultPixelShader, null);
            GorgonInputLayout layout = Interlocked.Exchange(ref _vertexLayout, null);
            SpriteRenderer spriteRenderer = Interlocked.Exchange(ref _spriteRenderer, null);
            GorgonTexture2DView texture = Interlocked.Exchange(ref _defaultTexture, null);
            GorgonConstantBufferView viewProj = Interlocked.Exchange(ref _viewProjection, null);
            GorgonConstantBufferView alphaTest = Interlocked.Exchange(ref _alphaTest, null);
            Lazy<GorgonFontFactory> defaultFont = Interlocked.Exchange(ref _defaultFontFactory, null);

            WeakEventManager<GorgonGraphics, EventArgs>.RemoveHandler(Graphics, nameof(GorgonGraphics.RenderTargetChanged), RenderTarget_Changed);
            WeakEventManager<GorgonGraphics, EventArgs>.RemoveHandler(Graphics, nameof(GorgonGraphics.ViewPortChanged), RenderTarget_Changed);
            
            if (defaultFont?.IsValueCreated ?? false)
            {
                defaultFont.Value.Dispose();
            }

            spriteRenderer?.Dispose();
            alphaTest?.Buffer?.Dispose();
            viewProj?.Buffer?.Dispose();
            texture?.Texture?.Dispose();
            layout?.Dispose();
            vertexShader?.Shader?.Dispose();
            pixelShader?.Shader?.Dispose();
        }
        #endregion

        #region Constructor/Finalizer.
        /// <summary>
        /// Initializes a new instance of the <see cref="Gorgon2D"/> class.
        /// </summary>
        /// <param name="target">The render target that will receive the rendering data.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="target"/> parameter is <b>null</b>.</exception>
        public Gorgon2D(GorgonRenderTarget2DView target)
        {
            _primaryTarget = target ?? throw new ArgumentNullException(nameof(target));
            _defaultFontFactory = new Lazy<GorgonFontFactory>(() => new GorgonFontFactory(target.Graphics), true);

            WeakEventManager<GorgonGraphics, EventArgs>.AddHandler(target.Graphics, nameof(GorgonGraphics.RenderTargetChanged), RenderTarget_Changed);
            WeakEventManager<GorgonGraphics, EventArgs>.AddHandler(target.Graphics, nameof(GorgonGraphics.ViewPortChanged), RenderTarget_Changed);
        }
        #endregion
    }
}
