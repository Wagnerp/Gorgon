﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2019 Michael Winsor
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
// Created: March 2, 2019 1:30:05 AM
// 
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gorgon.Core;
using Gorgon.Diagnostics;
using Gorgon.Editor.Content;
using Gorgon.Editor.Converters;
using Gorgon.Editor.Metadata;
using Gorgon.Editor.PlugIns;
using Gorgon.Editor.Services;
using Gorgon.Editor.SpriteEditor.Properties;
using Gorgon.Editor.UI;
using Gorgon.Graphics;
using Gorgon.Graphics.Core;
using Gorgon.Graphics.Imaging;
using Gorgon.Graphics.Imaging.Codecs;
using Gorgon.Graphics.Imaging.GdiPlus;
using Gorgon.IO;
using Gorgon.Math;
using Gorgon.Renderers;
using Gorgon.UI;
using Microsoft.IO;
using DX = SharpDX;

namespace Gorgon.Editor.SpriteEditor;

/// <summary>
/// Gorgon sprite editor content plug in interface.
/// </summary>
internal class SpriteEditorPlugIn
    : ContentPlugIn, IContentPlugInMetadata
{
    #region Constants.
    // The attribute key name for the sprite codec attribute.
    private const string CodecAttr = "SpriteCodec";
    #endregion

    #region Variables.
    // This is the only codec supported by the image plug in.  Images will be converted when imported.
    private GorgonV3SpriteBinaryCodec _defaultCodec;

    // The image codec to use.
    private IGorgonImageCodec _ddsCodec;

    // Pattern for sprite background.
    private GorgonTexture2DView _bgPattern;

    // The settings for the plug in.
    private SpriteEditorSettings _settings = new();

    // No thumbnail image.
    private IGorgonImage _noThumbnail;

    // No bound texture image.
    private IGorgonImage _noImage;

    // The synchronization lock for multiple threads.
    private readonly object _syncLock = new();

    /// <summary>
    /// The file name for the file that stores the settings.
    /// </summary>
    public readonly static string SettingsFilename = typeof(SpriteEditorPlugIn).FullName;
    #endregion

    #region Properties.
    /// <summary>
    /// Property to return the default file extension used by files generated by this content plug in.
    /// </summary>
    /// <remarks>
    /// Plug in developers can override this to default the file name extension for their content when creating new content with <see cref="GetDefaultContentAsync(string, HashSet{string})"/>.
    /// </remarks>
    protected override GorgonFileExtension DefaultFileExtension => _defaultCodec.FileExtensions.Count > 0 ? _defaultCodec.FileExtensions[0] : default;

    /// <summary>Property to return the name of the plug in.</summary>
    string IContentPlugInMetadata.PlugInName => Name;

    /// <summary>Property to return the description of the plugin.</summary>
    string IContentPlugInMetadata.Description => Description;

    /// <summary>Property to return whether or not the plugin is capable of creating content.</summary>
    public override bool CanCreateContent => true;

    /// <summary>Property to return the ID of the small icon for this plug in.</summary>
    public Guid SmallIconID
    {
        get;
    }

    /// <summary>Property to return the ID of the new icon for this plug in.</summary>
    public Guid NewIconID
    {
        get;
    }

    /// <summary>Property to return the ID for the type of content produced by this plug in.</summary>
    public override string ContentTypeID => CommonEditorContentTypes.SpriteType;

    /// <summary>Property to return the friendly (i.e shown on the UI) name for the type of content.</summary>
    public string ContentType => Resources.GORSPR_CONTENT_TYPE;
    #endregion

    #region Methods.
    /// <summary>
    /// Function to determine if an image content file is a 2D image or not.
    /// </summary>
    /// <param name="file">The content file for the image.</param>
    /// <returns><b>true</b> if the image is 2D, or <b>false</b> if not.</returns>
    private bool Is2DImage(IContentFile file)
    {
        using Stream stream = ContentFileManager.OpenStream(file.Path, FileMode.Open);
        IGorgonImageInfo metadata = _ddsCodec.GetMetaData(stream);
        return metadata.ImageType is not ImageType.Image3D and not ImageType.Image1D;
    }

    /// <summary>
    /// Function to update the dependencies for the sprite.
    /// </summary>
    /// <param name="fileStream">The stream for the file.</param>
    /// <param name="dependencyList">The list of dependency file paths.</param>
    private void UpdateDependencies(Stream fileStream, Dictionary<string, List<string>> dependencyList)
    {
        // If we have a texture bound in the dependencies already, and it actually exists in the project file system, then 
        // we don't need to update anything.
        if ((dependencyList.TryGetValue(CommonEditorContentTypes.ImageType, out List<string> textureNames))
            && (textureNames is not null)
            && (textureNames.Count > 0))
        {
            if (ContentFileManager.FileExists(textureNames[0]))
            {
                return;
            }
        }

        // We couldn't find the texture in the dependency list (either did not exist, or there were no dependencies).
        string textureName = _defaultCodec.GetAssociatedTextureName(fileStream);

        if (textureNames is null)
        {
            dependencyList[CommonEditorContentTypes.ImageType] = textureNames = new List<string>();                
        }

        // Remove any duplicate names.
        textureNames.RemoveAll(s => string.Equals(s, textureName, StringComparison.OrdinalIgnoreCase));

        if ((string.IsNullOrWhiteSpace(textureName)) || (!ContentFileManager.FileExists(textureName)))
        {
            // Unlink if we can't find anything.
            if (textureNames.Count == 0)
            {
                dependencyList.Remove(CommonEditorContentTypes.ImageType);
            }
            return;
        }

        textureNames.Add(textureName);
    }

    /// <summary>
    /// Function to find the image associated with the sprite file.
    /// </summary>
    /// <param name="spriteFile">The sprite file to evaluate.</param>
    /// <param name="fileManager">The file manager used to handle content files.</param>
    /// <returns>The file representing the image associated with the sprite.</returns>
    private IContentFile FindImage(IContentFile spriteFile)
    {
        if ((spriteFile.Metadata.DependsOn.Count == 0)
            || (!spriteFile.Metadata.DependsOn.TryGetValue(CommonEditorContentTypes.ImageType, out List<string> texturePaths))
            || (texturePaths is null)
            || (texturePaths.Count == 0))
        {
            return null;
        }

        IContentFile textureFile = ContentFileManager.GetFile(texturePaths[0]);

        if (textureFile is null)
        {
            HostContentServices.Log.Print($"ERROR: Sprite '{spriteFile.Path}' has texture '{texturePaths[0]}', but the file was not found on the file system.", LoggingLevel.Verbose);
            return null;
        }

        string textureFileContentType = textureFile.Metadata.ContentMetadata?.ContentTypeID;

        if (string.IsNullOrWhiteSpace(textureFileContentType))
        {
            HostContentServices.Log.Print($"ERROR: Sprite texture '{texturePaths[0]}' was found but has no content type ID.", LoggingLevel.Verbose);
            return null;
        }

        if ((!textureFile.Metadata.Attributes.TryGetValue(CommonEditorConstants.ContentTypeAttr, out string imageType))
            || (!string.Equals(imageType, textureFileContentType, StringComparison.OrdinalIgnoreCase)))
        {
            HostContentServices.Log.Print($"ERROR: Sprite '{spriteFile.Path}' has texture '{texturePaths[0]}', but the texture has a content type ID of '{textureFileContentType}', and the sprite requires a content type ID of '{imageType}'.", LoggingLevel.Verbose);
            return null;
        }

        return textureFile;
    }

    /// <summary>
    /// Function to render the sprite to a thumbnail image.
    /// </summary>
    /// <param name="image">The thumbnail image.</param>
    /// <param name="sprite">The sprite to render.</param>
    /// <param name="scale">The scale to apply.</param>
    /// <param name="bounds">The actual size of the sprite (AABB).</param>
    /// <returns>The image containing the rendered sprite.</returns>
    private void RenderThumbnail(ref IGorgonImage image, GorgonSprite sprite, float scale, DX.RectangleF bounds)
    {
        GorgonRenderTarget2DView rtv = null;
        GorgonRenderTargetView prevRtv = null;

        lock (_syncLock)
        {
            try
            {                    
                rtv = GorgonRenderTarget2DView.CreateRenderTarget(HostContentServices.GraphicsContext.Graphics, new GorgonTexture2DInfo((int)(bounds.Width * scale),
                                                                                                                                             (int)(bounds.Height * scale),
                                                                                                                                             BufferFormat.R8G8B8A8_UNorm)
                {
                    Name = $"SpriteEditor_Rtv_Preview_{Guid.NewGuid():N}",
                    Binding = TextureBinding.ShaderResource
                });
                rtv.Clear(GorgonColor.BlackTransparent);

                float bgSize;

                if (bounds.Width > bounds.Height)
                {
                    sprite.Scale = new Vector2(rtv.Width / (bounds.Width.Max(1)));
                    bgSize = rtv.Width;
                }
                else
                {
                    sprite.Scale = new Vector2(rtv.Height / (bounds.Height.Max(1)));
                    bgSize = rtv.Height;
                }

                // If our bounding box is not the same width/height as the sprite definition, then we've likely changed the offsets of the vertices.
                // To display this accurately, we need to find the anchor point for the center of the AABB.
                sprite.Anchor = new Vector2((bounds.Left + bounds.Width * 0.5f) / sprite.Size.Width, 
                                               (bounds.Top + bounds.Height * 0.5f) / sprite.Size.Height);
                sprite.Position = new Vector2(rtv.Width * 0.5f, rtv.Height * 0.5f);

                prevRtv = HostContentServices.GraphicsContext.Graphics.RenderTargets[0];
                HostContentServices.GraphicsContext.Graphics.SetRenderTarget(rtv);
                HostContentServices.GraphicsContext.Renderer2D.Begin();
                HostContentServices.GraphicsContext.Renderer2D.DrawFilledRectangle(new DX.RectangleF(0, 0, bgSize, bgSize), GorgonColor.White, _bgPattern, new DX.RectangleF(0, 0, 1.0f, 1.0f));
                HostContentServices.GraphicsContext.Renderer2D.DrawSprite(sprite);
                HostContentServices.GraphicsContext.Renderer2D.End();

                image?.Dispose();
                image = rtv.Texture.ToImage();
            }
            finally
            {
                rtv?.Dispose();
                if (prevRtv is not null)
                {
                    HostContentServices.GraphicsContext.Graphics.SetRenderTarget(prevRtv);
                }
            }
        }
    }

    /// <summary>
    /// Function to load the image to be used a thumbnail.
    /// </summary>
    /// <param name="thumbnailCodec">The codec for the thumbnail images.</param>
    /// <param name="thumbnailFile">The path to the thumbnail file.</param>
    /// <param name="content">The content being thumbnailed.</param>        
    /// <param name="cancelToken">The token used to cancel the operation.</param>
    /// <returns>The image, image content file and sprite, or just the thumbnail image if it was cached (sprite will be null).</returns>
    private (GorgonSprite sprite, IContentFile imageFile, IGorgonImage thumbNail) LoadThumbnailImage(IGorgonImageCodec thumbnailCodec, string thumbnailFile, IContentFile content, CancellationToken cancelToken)
    {
        IGorgonImage spriteImage;
        Stream inStream = null;
        Stream imgStream = null;

        try
        {
            IGorgonVirtualFile file = TemporaryFileSystem.FileSystem.GetFile(thumbnailFile);

            // If we've already got the file, then leave.
            if (file is not null)
            {
                inStream = file.OpenStream();

                spriteImage = thumbnailCodec.FromStream(inStream);

                if (cancelToken.IsCancellationRequested)
                {
                    spriteImage?.Dispose();
                    return (null, null, null);
                }

                return (null, null, spriteImage);
            }

            // Read in the sprite data.
            inStream = ContentFileManager.OpenStream(content.Path, FileMode.Open);

            if (!_defaultCodec.IsReadable(inStream))
            {
                return (null, null, _noThumbnail.Clone());
            }

            // Locate the texture for the sprite.
            IContentFile imageFile = FindImage(content);                

            // If we couldn't locate the texture based on the dependency information, try to locate it from the embedded information 
            // in the sprite data. We will not be updating the dependency metadata here, it will be updated when we open the sprite.
            if (imageFile is null)
            {
                string textureName = _defaultCodec.GetAssociatedTextureName(inStream);
                if ((string.IsNullOrWhiteSpace(textureName)) || (!ContentFileManager.FileExists(textureName)))
                {
                    return (null, null, _noImage.Clone());
                }

                imageFile = ContentFileManager.GetFile(textureName);

                if (imageFile is null)
                {
                    return (null, null, _noThumbnail.Clone());
                }
            }

            imgStream = ContentFileManager.OpenStream(imageFile.Path, FileMode.Open);
            if (!_ddsCodec.IsReadable(imgStream))
            {
                return (null, null, _noImage.Clone());
            }

            var texture = GorgonTexture2DView.FromStream(HostContentServices.GraphicsContext.Graphics, imgStream, _ddsCodec, options: new GorgonTexture2DLoadOptions
            {
                Binding = TextureBinding.ShaderResource,
                Usage = ResourceUsage.Immutable,
                Name = imageFile.Path,
                IsTextureCube = false       // We have force texture cube off so that our image will render correctly.
            });
            GorgonSprite sprite = _defaultCodec.FromStream(inStream, texture);

            // If we don't have a texture by this point, then update the preview to show that there's no image attached.
            return sprite.Texture is null ? (null, null, _noImage.Clone())
                                          : (sprite, imageFile, null);
        }
        catch (Exception ex)
        {
            HostContentServices.Log.Print($"ERROR: Cannot create thumbnail for '{content.Path}'", LoggingLevel.Intermediate);
            HostContentServices.Log.LogException(ex);
            return (null, null, null);
        }
        finally
        {
            imgStream?.Dispose();
            inStream?.Dispose();
        }
    }

    /// <summary>
    /// Function to update the metadata for a file that is missing metadata attributes.
    /// </summary>
    /// <param name="attributes">The attributes to update.</param>        
    /// <returns><b>true</b> if the metadata needs refreshing for the file, <b>false</b> if not.</returns>
    private bool UpdateFileMetadataAttributes(Dictionary<string, string> attributes)
    {
        bool needsRefresh = false;

        if ((attributes.TryGetValue(CodecAttr, out string currentCodecType))
            && (!string.IsNullOrWhiteSpace(currentCodecType)))
        {
            attributes.Remove(CodecAttr);
            needsRefresh = true;
        }

        if ((attributes.TryGetValue(CommonEditorConstants.ContentTypeAttr, out string currentContentType))
            && (string.Equals(currentContentType, CommonEditorContentTypes.SpriteType, StringComparison.OrdinalIgnoreCase)))
        {
            attributes.Remove(CommonEditorConstants.ContentTypeAttr);
            needsRefresh = true;
        }

        string codecType = _defaultCodec.GetType().FullName;
        if ((!attributes.TryGetValue(CodecAttr, out currentCodecType))
            || (!string.Equals(currentCodecType, codecType, StringComparison.OrdinalIgnoreCase)))
        {
            attributes[CodecAttr] = codecType;
            needsRefresh = true;
        }

        if ((!attributes.TryGetValue(CommonEditorConstants.ContentTypeAttr, out currentContentType))
            || (!string.Equals(currentContentType, CommonEditorContentTypes.SpriteType, StringComparison.OrdinalIgnoreCase)))
        {
            attributes[CommonEditorConstants.ContentTypeAttr] = CommonEditorContentTypes.SpriteType;
            needsRefresh = true;
        }

        return needsRefresh;
    }

    /// <summary>
    /// Function to determine if a file can be loaded in-place.
    /// </summary>
    /// <param name="file">The file to evaluate.</param>
    /// <param name="currentContent">The currently loaded content.</param>
    /// <returns><b>true</b> if it can be opened in-place, <b>false</b> if not.</returns>
    /// <remarks>
    /// <para>
    /// Developers can override this method to implement the correct checking for content information for their plug ins.
    /// </para>
    /// </remarks>
    protected override bool OnCanOpenInPlace(IContentFile file, IEditorContent currentContent)
    {
        if (currentContent is not SpriteContent current)
        {
            return false;
        }

        if ((!file.Metadata.Attributes.TryGetValue(CommonEditorConstants.ContentTypeAttr, out string contentType))
            || (!string.Equals(contentType, ContentTypeID, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if ((!file.Metadata.DependsOn.TryGetValue(CommonEditorContentTypes.ImageType, out List<string> imagePaths))
            || (imagePaths.Count == 0) || (current.Texture is null))
        {
            return false;
        }

        return string.Equals(current.Texture.Resource.Name, imagePaths[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Function to open a content object in place from this plugin.
    /// </summary>
    /// <param name="file">The file that contains the content.</param>
    /// <param name="current">The currently open content.</param>
    /// <param name="undoService">The undo service to use when correcting mistakes.</param>
    protected override void OnOpenInPlace(IContentFile file, IEditorContent current, IUndoService undoService)
    {
        var content = current as SpriteContent;

        Debug.Assert(content is not null, "The content is not a sprite.");

        GorgonSprite sprite;

        // Load the sprite now. 
        using Stream stream = ContentFileManager.OpenStream(file.Path, FileMode.Open);
        sprite = _defaultCodec.FromStream(stream, content.Texture);

        content.Initialize(sprite, file, undoService);
    }

    /// <summary>Function to register plug in specific search keywords with the system search.</summary>
    /// <typeparam name="T">The type of object being searched, must implement <see cref="IGorgonNamedObject"/>.</typeparam>
    /// <param name="searchService">The search service to use for registration.</param>
    protected override void OnRegisterSearchKeywords<T>(ISearchService<T> searchService)
    {
        // Not needed yet.
    }

    /// <summary>Function to open a content object from this plugin.</summary>
    /// <param name="file">The file that contains the content.</param>
    /// <param name = "fileManager" > The file manager used to access other content files.</param>
    /// <param name="scratchArea">The file system for the scratch area used to write transitory information.</param>
    /// <param name="undoService">The undo service for the plug in.</param>
    /// <returns>A new IEditorContent object.</returns>
    /// <remarks>
    /// The <paramref name="scratchArea" /> parameter is the file system where temporary files to store transitory information for the plug in is stored. This file system is destroyed when the
    /// application or plug in is shut down, and is not stored with the project.
    /// </remarks>
    protected async override Task<IEditorContent> OnOpenContentAsync(IContentFile file, IContentFileManager fileManager, IGorgonFileSystemWriter<Stream> scratchArea, IUndoService undoService)
    {
        var content = new SpriteContent();
        GorgonTexture2DView spriteImage = null;
        IContentFile imageFile;
        GorgonSprite sprite;
        SpriteTextureService textureService;
        Stream stream = null;            

        try
        {
            textureService = new SpriteTextureService(HostContentServices.GraphicsContext, fileManager, _defaultCodec, _ddsCodec, HostContentServices.Log);

            // Load the sprite image.
            (spriteImage, imageFile) = await textureService.LoadFromSpriteContentAsync(file);

            // Load the sprite now. 
            stream = ContentFileManager.OpenStream(file.Path, FileMode.Open);
            sprite = _defaultCodec.FromStream(stream, spriteImage);

            // We don't have a texture attached to this guy (probably due to a mismatch in the metadata), so we'll unlink it 
            // and remove any reference to any image that might have been loaded.
            if ((sprite.Texture is null) && (imageFile is not null))
            {
                file.UnlinkContent(imageFile);

                spriteImage?.Dispose();
                imageFile.IsOpen = false;
                imageFile = null;
            }

            var settings = new Settings();                
            settings.Initialize(new SettingsParameters(_settings, HostContentServices));

            var spritePickMaskEditor = new SpritePickMaskEditor();
            spritePickMaskEditor.Initialize(new SpritePickMaskEditorParameters(settings, HostContentServices));

            var colorEditor = new SpriteColorEdit();
            colorEditor.Initialize(new HostedPanelViewModelParameters(HostContentServices));

            var anchorEditor = new SpriteAnchorEdit();
            anchorEditor.Initialize(new SpriteAnchorEditParameters(new DX.Rectangle
            {
                Left = -HostContentServices.GraphicsContext.Graphics.VideoAdapter.MaxTextureWidth / 2,
                Top = -HostContentServices.GraphicsContext.Graphics.VideoAdapter.MaxTextureHeight / 2,
                Right = HostContentServices.GraphicsContext.Graphics.VideoAdapter.MaxTextureWidth / 2 - 1,
                Bottom = HostContentServices.GraphicsContext.Graphics.VideoAdapter.MaxTextureHeight / 2 - 1,
            }, HostContentServices));

            var builder = new GorgonSamplerStateBuilder(HostContentServices.GraphicsContext.Graphics);
            var wrapEditor = new SpriteTextureWrapEdit();
            wrapEditor.Initialize(new SpriteTextureWrapEditParameters(builder, HostContentServices));
            wrapEditor.CurrentSampler = sprite.TextureSampler;

            var spriteClipContext = new SpriteClipContext();
            var spritePickContext = new SpritePickContext();
            var spriteVertexEditContext = new SpriteVertexEditContext();

            var spriteContentServices = new SpriteContentServices(new NewSpriteService(fileManager, _ddsCodec),
                                                                  textureService, 
                                                                  undoService,
                                                                  builder);

            content.Initialize(new SpriteContentParameters(
                sprite,
                imageFile,
                settings,
                spriteClipContext,
                spritePickContext,
                spriteVertexEditContext,
                colorEditor,
                anchorEditor,
                wrapEditor,
                spriteContentServices,
                _defaultCodec,
                fileManager,
                file,
                HostContentServices));

            spriteClipContext.Initialize(new SpriteClipContextParameters(content, HostContentServices));
            spritePickContext.Initialize(new SpritePickContextParameters(content, spritePickMaskEditor, spriteContentServices.TextureService, HostContentServices));
            spriteVertexEditContext.Initialize(new SpriteVertexEditContextParameters(content, HostContentServices));

            if ((spritePickContext.GetImageDataCommand is not null) && (spritePickContext.GetImageDataCommand.CanExecute(null)))
            {
                await spritePickContext.GetImageDataCommand.ExecuteAsync(null);
            }

            return content;
        }
        catch
        {
            spriteImage?.Dispose();
            throw;
        }
        finally
        {
            stream?.Dispose();
        }            
    }

    /// <summary>Function to provide clean up for the plugin.</summary>
    protected override void OnShutdown()
    {
        try
        {
            if (_settings is not null)
            {
                // Persist any settings.
                HostContentServices.ContentPlugInService.WriteContentSettings(SettingsFilename, _settings, new JsonSharpDxRectConverter());
            }
        }
        catch (Exception ex)
        {
            // We don't care if it crashes. The worst thing that'll happen is your settings won't persist.
            HostContentServices.Log.LogException(ex);
        }

        _noImage?.Dispose();
        _noThumbnail?.Dispose();
        _bgPattern?.Dispose();

        ViewFactory.Unregister<ISpriteContent>();

        base.OnShutdown();
    }

    /// <summary>Function to provide initialization for the plugin.</summary>
    /// <remarks>This method is only called when the plugin is loaded at startup.</remarks>
    protected override void OnInitialize()
    {
        _noThumbnail = Resources.no_thumb_sprite_64x64.ToGorgonImage();
        _ddsCodec = new GorgonCodecDds();
        _defaultCodec = new GorgonV3SpriteBinaryCodec(HostContentServices.GraphicsContext.Renderer2D);
        _bgPattern = GorgonTexture2DView.CreateTexture(HostContentServices.GraphicsContext.Graphics, new GorgonTexture2DInfo(CommonEditorResources.CheckerBoardPatternImage.Width,
                                                                                                                                  CommonEditorResources.CheckerBoardPatternImage.Height,
                                                                                                                                  CommonEditorResources.CheckerBoardPatternImage.Format)
        {
            Name = $"Sprite_Editor_Bg_Preview_{Guid.NewGuid():N}"
        }, CommonEditorResources.CheckerBoardPatternImage);
        using MemoryStream noImageStream = CommonEditorResources.MemoryStreamManager.GetStream(Resources.NoImage_256x256);
        _noImage = _ddsCodec.FromStream(noImageStream);

        SpriteEditorSettings settings = HostContentServices.ContentPlugInService.ReadContentSettings<SpriteEditorSettings>(SettingsFilename, new JsonSharpDxRectConverter());
        if (settings is not null)
        {
            _settings = settings;
        }

        ViewFactory.Register<ISpriteContent>(() => new SpriteEditorView());
    }

    /// <summary>Function to retrieve the default content name, and data.</summary>
    /// <param name="generatedName">A default name generated by the application.</param>
    /// <param name="metadata">Custom metadata for the content.</param>
    /// <returns>The default content name along with the content data serialized as a byte array. If either the name or data are <b>null</b>, then the user cancelled..</returns>
    /// <remarks>
    ///   <para>
    /// Plug in authors may override this method so a custom UI can be presented when creating new content, or return a default set of data and a default name, or whatever they wish.
    /// </para>
    ///   <para>
    /// If an empty string (or whitespace) is returned for the name, then the <paramref name="generatedName" /> will be used.
    /// </para>
    /// </remarks>
    protected override Task<(string name, RecyclableMemoryStream data)> OnGetDefaultContentAsync(string generatedName, ProjectItemMetadata metadata)
    {
        // Creates a sprite object and converts it to a byte array.
        RecyclableMemoryStream CreateSprite(DX.Size2F size, IContentFile textureFile)
        {
            var sprite = new GorgonSprite
            {
                Anchor = new Vector2(0.5f, 0.5f),
                Size = size
            };

            if (textureFile is not null)
            {
                using (Stream textureStream = ContentFileManager.OpenStream(textureFile.Path, FileMode.Open))
                {
                    sprite.Texture = GorgonTexture2DView.FromStream(HostContentServices.GraphicsContext.Graphics,
                                                                    textureStream,
                                                                    _ddsCodec,
                                                                    options: new GorgonTexture2DLoadOptions
                                                                    {
                                                                        Name = textureFile.Path,
                                                                        Binding = TextureBinding.ShaderResource,
                                                                        Usage = ResourceUsage.Default
                                                                    });
                }

                sprite.TextureRegion = sprite.Texture.ToTexel(new DX.Rectangle(0, 0, (int)size.Width, (int)size.Height));
                metadata.DependsOn.Add(CommonEditorContentTypes.ImageType, new List<string> { textureFile.Path });
            }

            metadata.Attributes[CodecAttr] = _defaultCodec.GetType().FullName;

            var stream = CommonEditorResources.MemoryStreamManager.GetStream() as RecyclableMemoryStream;
            _defaultCodec.Save(sprite, stream);
            // We don't need this now.
            sprite.Texture?.Dispose();
            return stream;
        }

        // Find all available textures in our file system.
        IReadOnlyList<IContentFile> textures = ContentFileManager.EnumerateContentFiles("/", "*", true)
                                            .Where(item => (item.Metadata.Attributes.ContainsKey(CommonEditorConstants.ContentTypeAttr))
                                                    && (string.Equals(item.Metadata.Attributes[CommonEditorConstants.ContentTypeAttr], CommonEditorContentTypes.ImageType, StringComparison.OrdinalIgnoreCase))
                                                    && (Is2DImage(item)))
                                            .ToArray();

        using (var formName = new FormNewSprite
        {                
            ImageCodec = _ddsCodec,
            FileManager = ContentFileManager,
            Text = Resources.GORSPR_CAPTION_SPRITE_NAME,
            ObjectName = generatedName ?? string.Empty,
            CueText = Resources.GORSPR_TEXT_CUE_SPRITE_NAME
        })
        {
            formName.FillTextures(textures);
            if (formName.ShowDialog(GorgonApplication.MainForm) == DialogResult.OK)
            {                    
                return Task.FromResult((formName.ObjectName, CreateSprite(formName.SpriteSize, formName.TextureFile)));
            }
        }

        return Task.FromResult<(string, RecyclableMemoryStream)>((null, null));
    }

    /// <summary>
    /// Function to retrieve the small icon for the content plug in.
    /// </summary>
    /// <returns>An image for the small icon.</returns>
    public Image GetSmallIcon() => Resources.sprite_16x16;

    /// <summary>Function to retrieve a thumbnail for the content.</summary>
    /// <param name="contentFile">The content file used to retrieve the data to build the thumbnail with.</param>
    /// <param name="filePath">The path to the thumbnail file to write.</param>
    /// <param name="cancelToken">The token used to cancel the thumbnail generation.</param>
    /// <returns>A <see cref="IGorgonImage"/> containing the thumbnail image data.</returns>
    public async Task<IGorgonImage> GetThumbnailAsync(IContentFile contentFile, string filePath, CancellationToken cancelToken)
    {
        if (contentFile is null)
        {
            throw new ArgumentNullException(nameof(contentFile));
        }

        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentEmptyException(nameof(filePath));
        }

        // If the content is not a v3 sprite, then leave it.
        if ((!contentFile.Metadata.Attributes.TryGetValue(CodecAttr, out string codecName))
            || (string.IsNullOrWhiteSpace(codecName))
            || (!string.Equals(codecName, _defaultCodec.GetType().FullName, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string fileDirectoryPath = Path.GetDirectoryName(filePath).FormatDirectory('/');
        IGorgonVirtualDirectory directory = TemporaryFileSystem.FileSystem.GetDirectory(fileDirectoryPath);

        directory ??= TemporaryFileSystem.CreateDirectory(fileDirectoryPath);

        IGorgonImageCodec pngCodec = new GorgonCodecPng();

        (GorgonSprite sprite, IContentFile imageFile, IGorgonImage thumbnailImage) = await Task.Run(() => LoadThumbnailImage(pngCodec, filePath, contentFile, cancelToken), cancelToken);

        if ((sprite is null) || (cancelToken.IsCancellationRequested))
        {
            return thumbnailImage;
        }

        if (thumbnailImage is not null)
        {
            return thumbnailImage;
        }

        Cursor.Current = Cursors.WaitCursor;

        try
        {
            const float maxSize = 256;

            // Get rid of the anchor prior to retrieving the AABB, we'll be recalculating it anyway.
            sprite.Anchor = Vector2.Zero;
            DX.RectangleF bounds = HostContentServices.GraphicsContext.Renderer2D.MeasureSprite(sprite);
            float scale = (maxSize / bounds.Width).Min(maxSize / bounds.Height);
            RenderThumbnail(ref thumbnailImage, sprite, scale, bounds);

            if (cancelToken.IsCancellationRequested)
            {
                return null;
            }

            // We're done on the main thread, we can switch to another thread to write the image.
            Cursor.Current = Cursors.Default;

            await Task.Run(() => {
                using Stream stream = TemporaryFileSystem.OpenStream(filePath, FileMode.Create);
                pngCodec.Save(thumbnailImage, stream);
            }, cancelToken);

            if (cancelToken.IsCancellationRequested)
            {
                return null;
            }

            contentFile.Metadata.Thumbnail = Path.GetFileName(filePath);
            return thumbnailImage;
        }
        catch (Exception ex)
        {
            HostContentServices.Log.Print($"ERROR: Cannot create thumbnail for '{contentFile.Path}'", LoggingLevel.Intermediate);
            HostContentServices.Log.LogException(ex);
            return null;
        }
        finally
        {
            // We don't need the sprite texture any more.
            sprite?.Texture?.Dispose();
            Cursor.Current = Cursors.Default;
        }
    }

    /// <summary>Function to retrieve the icon used for new content creation.</summary>
    /// <returns>An image for the icon.</returns>
    /// <remarks>
    /// <para>
    /// This method is never called when <see cref="IContentPlugInMetadata.CanCreateContent"/> is <b>false</b>.
    /// </para>
    /// </remarks>
    public Image GetNewIcon() => Resources.sprite_24x24;

    /// <summary>Function to determine if the content plugin can open the specified file.</summary>
    /// <param name="filePath">The path to the file to evaluate.</param>
    /// <returns>
    ///   <b>true</b> if the plugin can open the file, or <b>false</b> if not.</returns>
    public bool CanOpenContent(string filePath)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentEmptyException(nameof(filePath));
        }

        IContentFile file = ContentFileManager.GetFile(filePath);

        Debug.Assert(file is not null, $"File '{filePath}' doesn't exist, but it should!");

        using Stream stream = ContentFileManager.OpenStream(filePath, FileMode.Open);
        if (!_defaultCodec.IsReadable(stream))
        {
            return false;
        }

        UpdateFileMetadataAttributes(file.Metadata.Attributes);
        UpdateDependencies(stream, file.Metadata.DependsOn);
        return true;
    }
    #endregion

    #region Constructor/Finalizer.
    /// <summary>Initializes a new instance of the SpriteEditorPlugIn class.</summary>
    public SpriteEditorPlugIn()
        : base(Resources.GORSPR_DESC)
    {
        SmallIconID = Guid.NewGuid();
        NewIconID = Guid.NewGuid();
    }
    #endregion
}
