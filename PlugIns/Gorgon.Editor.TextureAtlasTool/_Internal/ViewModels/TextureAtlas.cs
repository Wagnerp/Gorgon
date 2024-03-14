﻿
// 
// Gorgon
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
// all copies or substantial portions of the Software
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE
// 
// Created: May 7, 2019 6:23:18 PM
// 


using System.ComponentModel;
using System.Diagnostics;
using Gorgon.Editor.Content;
using Gorgon.Editor.Services;
using Gorgon.Editor.TextureAtlasTool.Properties;
using Gorgon.Editor.UI;
using Gorgon.Graphics;
using Gorgon.Graphics.Core;
using Gorgon.IO;
using Gorgon.Renderers;
using Gorgon.Renderers.Services;
using DX = SharpDX;

namespace Gorgon.Editor.TextureAtlasTool;

/// <summary>
/// The view model for the main UI
/// </summary>
internal class TextureAtlas
        : EditorToolViewModelBase<TextureAtlasParameters>, ITextureAtlas
{

    // The settings for the texture atlas.
    private TextureAtlasSettings _settings;
    // The selected sprites.
    private IContentFile[] _spriteFiles = [];
    // The texture atlas service.
    private IGorgonTextureAtlasService _atlasService;
    // The file I/O service used to manage the atlas files.
    private FileIOService _fileIO;
    // The base name for textures generated by the atlas.
    private string _baseTextureName;
    // The texture atlas.
    private GorgonTextureAtlas _atlas;
    // The preview array index.
    private int _previewArrayIndex;
    // The preview texture index.
    private int _previewTextureIndex;
    // The sprites used to generate the atlas.
    private IReadOnlyDictionary<IContentFile, GorgonSprite> _sprites = new Dictionary<IContentFile, GorgonSprite>();



    /// <summary>
    /// Property to return the view model for the sprite file loader.
    /// </summary>
    public ISpriteFiles SpriteFiles
    {
        get;
        private set;
    }

    /// <summary>
    /// Property to return the number of sprites that were loaded.
    /// </summary>
    public int LoadedSpriteCount => _spriteFiles.Length;

    /// <summary>Property to return the path for the output files.</summary>
    public string OutputPath
    {
        get => _settings.LastOutputDir;
        private set
        {
            if (string.Equals(_settings.LastOutputDir, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            OnPropertyChanging();
            _settings.LastOutputDir = value?.FormatDirectory('/');
            OnPropertyChanged();
        }
    }

    /// <summary>Property to set or return the maximum size for the atlas texture.</summary>
    public DX.Size2 MaxTextureSize
    {
        get => _settings.MaxTextureSize;
        set
        {
            if (_settings.MaxTextureSize.Equals(value))
            {
                return;
            }

            OnPropertyChanging();
            _settings.MaxTextureSize = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Property to set or return the maximum number of array indices for the atlas texture.</summary>
    public int MaxArrayCount
    {
        get => _settings.MaxArrayCount;
        set
        {
            if (_settings.MaxArrayCount == value)
            {
                return;
            }

            OnPropertyChanging();
            _settings.MaxArrayCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Property to set or return the amount of padding, in pixels around each sprite on the texture.
    /// </summary>
    public int Padding
    {
        get => _settings.Padding;
        set
        {
            if (_settings.Padding == value)
            {
                return;
            }

            OnPropertyChanging();
            _settings.Padding = value;
            OnPropertyChanged();
        }
    }


    /// <summary>
    /// Property to return the base atlas texture name.
    /// </summary>
    public string BaseTextureName
    {
        get => _baseTextureName;
        set
        {
            if (string.Equals(_baseTextureName, value, StringComparison.CurrentCulture))
            {
                return;
            }

            OnPropertyChanging();
            _baseTextureName = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Property to return the atlas after it's been generated.
    /// </summary>
    public GorgonTextureAtlas Atlas
    {
        get => _atlas;
        private set
        {
            if (_atlas == value)
            {
                return;
            }

            OnPropertyChanging();

            if (_atlas?.Textures is not null)
            {
                foreach (GorgonTexture2DView texture in _atlas.Textures)
                {
                    texture.Dispose();
                }
            }

            _atlas = value;
            OnPropertyChanged();

            PreviewArrayIndex = 0;
            PreviewTextureIndex = 0;
        }
    }

    /// <summary>
    /// Property to return the preview array index.
    /// </summary>
    public int PreviewArrayIndex
    {
        get => _previewArrayIndex;
        private set
        {
            if (_previewArrayIndex == value)
            {
                return;
            }

            OnPropertyChanging();
            _previewArrayIndex = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Property to return the preview texture index.
    /// </summary>
    public int PreviewTextureIndex
    {
        get => _previewTextureIndex;
        private set
        {
            if (_previewTextureIndex == value)
            {
                return;
            }

            OnPropertyChanging();
            _previewTextureIndex = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Property to return the command used to calculate best fit sizes.
    /// </summary>
    public IEditorCommand<object> CalculateSizesCommand
    {
        get;
    }

    /// <summary>Property to return the folder selection command.</summary>
    public IEditorCommand<object> SelectFolderCommand
    {
        get;
    }

    /// <summary>
    /// Property to return the command used to generate the atlas.
    /// </summary>
    public IEditorCommand<object> GenerateCommand
    {
        get;
    }

    /// <summary>
    /// Property to return the command to move to the next preview item.
    /// </summary>
    public IEditorCommand<object> NextPreviewCommand
    {
        get;
    }

    /// <summary>
    /// Property to return the command to move to the previous preview item.
    /// </summary>
    public IEditorCommand<object> PrevPreviewCommand
    {
        get;
    }


    /// <summary>
    /// Property to return the command used to commit the atlas data back to the file system.
    /// </summary>
    public IEditorCommand<CancelEventArgs> CommitAtlasCommand
    {
        get;
    }



    /// <summary>
    /// Function to determine whether sprites can be loaded or not.
    /// </summary>
    /// <returns><b>true</b> if the sprites can be loaded, <b>false</b> if not.</returns>
    private bool CanLoadSprites() => SpriteFiles.SelectedFiles.Count > 1;

    /// <summary>
    /// Function to load the selected sprites.
    /// </summary>
    private void DoLoadSprites()
    {
        HostServices.BusyService.SetBusy();

        try
        {
            // Unload any current atlas.
            Atlas = null;

            _spriteFiles = SpriteFiles.SelectedFiles.Select(item => item.File).ToArray();

            NotifyPropertyChanged(nameof(LoadedSpriteCount));

            if (string.IsNullOrWhiteSpace(_settings.LastOutputDir))
            {
                OutputPath = Path.GetDirectoryName(SpriteFiles.SelectedFiles[0].File.Path).FormatDirectory('/');
            }

            if (string.IsNullOrWhiteSpace(BaseTextureName))
            {
                BaseTextureName = Path.GetFileNameWithoutExtension(_spriteFiles[0].Name);
            }
        }
        catch (Exception ex)
        {
            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_LOAD_SPRITES);
        }
        finally
        {
            HostServices.BusyService.SetIdle();
        }
    }

    /// <summary>
    /// Function to browse folders on the file system.
    /// </summary>
    private void DoBrowseFolders()
    {
        try
        {
            string outputDir = _settings.LastOutputDir.FormatDirectory('/');

            if (!ContentFileManager.DirectoryExists(outputDir))
            {
                outputDir = "/";
            }

            outputDir = HostServices.FolderBrowser.GetFolderPath(outputDir, Resources.GORTAG_CAPTION_FOLDER_SELECT, Resources.GORTAG_DESC_FOLDER_SELECT);

            if (string.IsNullOrWhiteSpace(outputDir))
            {
                return;
            }

            OutputPath = outputDir;
        }
        catch (Exception ex)
        {
            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_FOLDER_SELECT);
        }
    }

    /// <summary>
    /// Function to determine if the size of the atlas can be calculated.
    /// </summary>
    /// <returns><b>true</b> if calculation is allowed, <b>false</b> if not.</returns>
    private bool CanCalculateSize() => LoadedSpriteCount > 1;

    /// <summary>
    /// Function to calculate the best fit size of the texture and array count.
    /// </summary>
    private void DoCalculateSize()
    {
        IReadOnlyDictionary<IContentFile, GorgonSprite> sprites = null;
        HostServices.BusyService.SetBusy();

        try
        {
            sprites = _fileIO.LoadSprites(_spriteFiles);

            (DX.Size2 textureSize, int arrayCount) = _atlasService.GetBestFit(sprites.Values, new DX.Size2(256, 256), MaxArrayCount);

            if ((textureSize.Width == 0) || (textureSize.Height == 0) || (arrayCount == 0))
            {
                HostServices.MessageDisplay.ShowError(Resources.GORTAG_ERR_CALC_TOO_LARGE);
                return;
            }

            MaxTextureSize = textureSize;
            MaxArrayCount = arrayCount;
        }
        catch (Exception ex)
        {
            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_CALC);
        }
        finally
        {
            // We no longer need the textures.
            if (sprites is not null)
            {
                foreach (GorgonTexture2DView texture in sprites.Values.Select(item => item.Texture))
                {
                    texture?.Dispose();
                }
            }

            HostServices.BusyService.SetIdle();
        }
    }

    /// <summary>
    /// Function to determine if the atlas can be generated.
    /// </summary>
    /// <returns><b>true</b> if the atlas can be generated, <b>false</b> if not.</returns>
    private bool CanGenerate() => (LoadedSpriteCount > 1) && (!string.IsNullOrWhiteSpace(OutputPath)) && (!string.IsNullOrWhiteSpace(BaseTextureName));

    /// <summary>
    /// Function to perform the atlas generation.
    /// </summary>
    private GorgonTextureAtlas GenerateAtlas()
    {
        GorgonTextureAtlas atlas;

        _atlasService.Padding = _settings.Padding;
        _atlasService.ArrayCount = _settings.MaxArrayCount;
        _atlasService.TextureSize = _settings.MaxTextureSize;
        _atlasService.BaseTextureName = $"{_settings.LastOutputDir}{_baseTextureName.FormatFileName()}";

        IReadOnlyDictionary<GorgonSprite, (int textureIndex, DX.Rectangle region, int arrayIndex)> regions = _atlasService.GetSpriteRegions(_sprites.Values);

        if ((regions is null) || (regions.Count == 0))
        {
            HostServices.MessageDisplay.ShowError(Resources.GORTAG_ERR_NO_ROOM);
            return null;
        }

        atlas = _atlasService.GenerateAtlas(regions, BufferFormat.R8G8B8A8_UNorm);

        if ((atlas is null) || (atlas.Textures is null) || (atlas.Sprites is null) || (atlas.Textures.Count == 0))
        {
            HostServices.MessageDisplay.ShowError(Resources.GORTAG_ERR_GEN_ATLAS);
            return null;
        }

        return atlas;
    }

    /// <summary>
    /// Function to perform the atlas generation.
    /// </summary>
    private void DoGenerate()
    {
        GorgonTextureAtlas atlas = null;

        HostServices.BusyService.SetBusy();

        void UnloadAtlasTextures()
        {
            if (atlas is null)
            {
                return;
            }

            foreach (GorgonTexture2DView texture in atlas.Textures)
            {
                texture.Dispose();
            }
        }

        try
        {
            _sprites = _fileIO.LoadSprites(_spriteFiles);

            Debug.Assert(_sprites.Count != 0, "No sprites were returned.");

            // If any of the sprites returned are not linked to a texture, give the user a chance to fix the problem.
            if (_sprites.Values.All(item => item.Texture is null))
            {
                HostServices.MessageDisplay.ShowError(Resources.GORTAG_ERR_NO_TEXTURES);
                return;
            }

            if (_sprites.Values.Any(item => item.Texture is null))
            {
                HostServices.BusyService.SetIdle();
                if (HostServices.MessageDisplay.ShowConfirmation(Resources.GORTAG_CONFIRM_SOME_NO_TEXTURE) == MessageResponse.No)
                {
                    return;
                }
                HostServices.BusyService.SetBusy();
            }

            GorgonTexture2DView texture = _sprites.Values.First(item => item.Texture is not null).Texture;
            if (_sprites.Values.All(item => item.Texture == texture))
            {
                HostServices.MessageDisplay.ShowInfo(Resources.GORTAG_INF_ALREADY_ATLASED);
                return;
            }

            atlas = GenerateAtlas();
            // Keep this separate so that we can dispose the textures from the new atlas should something go wrong.
            Atlas = atlas;
        }
        catch (Exception ex)
        {
            if ((atlas?.Textures is not null) && (Atlas != atlas))
            {
                UnloadAtlasTextures();
            }

            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_GEN_ATLAS);
        }
        finally
        {
            // We no longer need the textures.
            if (_sprites is not null)
            {
                foreach (GorgonTexture2DView texture in _sprites.Values.Select(item => item.Texture))
                {
                    texture?.Dispose();
                }
            }
            HostServices.BusyService.SetIdle();
        }
    }


    /// <summary>
    /// Function to determine we can move to the next preview image.
    /// </summary>
    /// <returns><b>true</b> if possible, <b>false</b> if not.</returns>
    private bool CanNextPreview()
    {
        if ((Atlas?.Textures is null) || (Atlas.Textures.Count == 0))
        {
            return false;
        }

        GorgonTexture2D texture = Atlas.Textures[PreviewTextureIndex].Texture;

        return ((PreviewArrayIndex + 1 < texture.ArrayCount) || (PreviewTextureIndex + 1 < Atlas.Textures.Count));
    }

    /// <summary>
    /// Function to move to the next index in the preview.
    /// </summary>
    private void DoNextPreview()
    {
        try
        {
            GorgonTexture2D texture = Atlas.Textures[PreviewTextureIndex].Texture;

            if (PreviewArrayIndex + 1 >= texture.ArrayCount)
            {
                PreviewTextureIndex++;
                PreviewArrayIndex = 0;
            }
            else
            {
                PreviewArrayIndex++;
            }
        }
        catch (Exception ex)
        {
            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_PREVIEW_ARRAY);
        }
    }

    /// <summary>
    /// Function to determine we can move to the next preview image.
    /// </summary>
    /// <returns><b>true</b> if possible, <b>false</b> if not.</returns>
    private bool CanPrevPreview() => (Atlas?.Textures is not null) && (Atlas.Textures.Count != 0) && ((PreviewArrayIndex - 1 >= 0) || (PreviewTextureIndex - 1 >= 0));

    /// <summary>
    /// Function to move to the next index in the preview.
    /// </summary>
    private void DoPrevPreview()
    {
        try
        {
            if (PreviewArrayIndex - 1 < 0)
            {
                PreviewTextureIndex--;
                PreviewArrayIndex = 0;
            }
            else
            {
                PreviewArrayIndex--;
            }
        }
        catch (Exception ex)
        {
            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_PREVIEW_ARRAY);
        }
    }

    /// <summary>
    /// Function to determine if the atlas data can be committed back to the file system.
    /// </summary>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <returns><b>true</b> if the data can be committed, <b>false</b> if not.</returns>
    private bool CanCommitAtlas(CancelEventArgs args) => (Atlas?.Textures is not null) && (Atlas.Textures.Count > 0) && (_sprites.Count > 0);

    /// <summary>
    /// Function to commit the atlas data back to the file system.
    /// </summary>
    /// <param name="args">The arguments to pass to the command.</param>
    private void DoCommitAtlas(CancelEventArgs args)
    {
        GorgonTextureAtlas atlas = null;

        // Function to unload the textures from the atlas.
        void UnloadTextures()
        {
            if ((atlas is null) || (atlas == Atlas))
            {
                return;
            }

            foreach (GorgonTexture2DView texture in atlas.Textures)
            {
                texture?.Dispose();
            }
        }

        try
        {
            // If we've changed the base name, regenerate the texture.
            string nameCheck = $"{_settings.LastOutputDir}{_baseTextureName.FormatFileName()}";

            if (!string.Equals(nameCheck, _atlasService.BaseTextureName, StringComparison.OrdinalIgnoreCase))
            {
                _sprites = _fileIO.LoadSprites(_spriteFiles);
                atlas = GenerateAtlas();
            }
            else
            {
                atlas = Atlas;
            }

            if (_fileIO.HasExistingFiles(atlas))
            {
                if (HostServices.MessageDisplay.ShowConfirmation(Resources.GORTAG_CONFIRM_OVERWRITE) == MessageResponse.No)
                {
                    UnloadTextures();
                    args.Cancel = true;
                    return;
                }
            }

            HostServices.BusyService.SetBusy();
            _fileIO.SaveAtlas(_sprites, atlas);

            Atlas = atlas;
        }
        catch (Exception ex)
        {
            // Take out any allocated textures.
            UnloadTextures();

            HostServices.MessageDisplay.ShowError(ex, Resources.GORTAG_ERR_SAVE);
            args.Cancel = true;
        }
        finally
        {
            HostServices.BusyService.SetIdle();
        }
    }

    /// <summary>Function to inject dependencies for the view model.</summary>
    /// <param name="injectionParameters">The parameters to inject.</param>
    /// <remarks>
    /// Applications should call this when setting up the view model for complex operations and/or dependency injection. The constructor should only be used for simple set up and initialization of objects.
    /// </remarks>
    protected override void OnInitialize(TextureAtlasParameters injectionParameters)
    {
        base.OnInitialize(injectionParameters);

        _settings = injectionParameters.Settings;
        SpriteFiles = injectionParameters.SpriteFiles;
        _atlasService = injectionParameters.AtlasGenerator;
        _fileIO = injectionParameters.FileIO;
    }

    /// <summary>Function called when the associated view is loaded.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        if (SpriteFiles is not null)
        {
            SpriteFiles.ConfirmLoadCommand = new EditorCommand<object>(DoLoadSprites, CanLoadSprites);
        }
    }

    /// <summary>Function called when the associated view is unloaded.</summary>
    protected override void OnUnload()
    {
        if (SpriteFiles is not null)
        {
            SpriteFiles.ConfirmLoadCommand = null;
        }

        _sprites = new Dictionary<IContentFile, GorgonSprite>();
        _spriteFiles = [];
        Atlas = null;

        base.OnUnload();
    }



    /// <summary>Initializes a new instance of the <see cref="TextureAtlas"/> class.</summary>
    public TextureAtlas()
    {
        SelectFolderCommand = new EditorCommand<object>(DoBrowseFolders);
        CalculateSizesCommand = new EditorCommand<object>(DoCalculateSize, CanCalculateSize);
        GenerateCommand = new EditorCommand<object>(DoGenerate, CanGenerate);
        NextPreviewCommand = new EditorCommand<object>(DoNextPreview, CanNextPreview);
        PrevPreviewCommand = new EditorCommand<object>(DoPrevPreview, CanPrevPreview);
        CommitAtlasCommand = new EditorCommand<CancelEventArgs>(DoCommitAtlas, CanCommitAtlas);
    }

}
