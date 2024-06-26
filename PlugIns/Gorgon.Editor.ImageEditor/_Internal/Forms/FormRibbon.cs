﻿#region MIT
// 
// Gorgon.
// Copyright (C) 2021 Michael Winsor
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
// Created: February 13, 2021 12:53:57 PM
// 
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using Gorgon.Editor.ImageEditor.Native;
using Gorgon.Editor.ImageEditor.Properties;
using Gorgon.Editor.ImageEditor.ViewModels;
using Gorgon.Editor.Rendering;
using Gorgon.Editor.UI;
using Gorgon.Graphics;
using Gorgon.Graphics.Imaging;
using Gorgon.Graphics.Imaging.Codecs;
using Krypton.Toolkit;

namespace Gorgon.Editor.ImageEditor;

/// <summary>
/// Provides a ribbon interface for the plug in view.
/// </summary>
/// <remarks>
/// We cannot provide a ribbon on the control directly. For some reason, the krypton components will only allow ribbons on forms.
/// </remarks>
internal partial class FormRibbon
    : KryptonForm, IDataContext<IImageContent>
{
    #region Variables.
    // The list of menu items associated with the zoom level.
    private readonly Dictionary<ZoomLevels, ToolStripMenuItem> _menuItems = new();
    // The current zoom level.
    private ZoomLevels _zoomLevel = ZoomLevels.ToWindow;
    // The renderer for the content.
    private IContentRenderer _contentRenderer;
    #endregion

    #region Properties.
    /// <summary>
    /// Property to set or return the data context for the ribbon on the form.
    /// </summary>
    public IImageContent DataContext
    {
        get;
        private set;
    }

    /// <summary>
    /// Property to set or return the currently active renderer.
    /// </summary>
    public IContentRenderer ContentRenderer
    {
        get => _contentRenderer;
        set
        {
            if (_contentRenderer == value)
            {
                return;
            }

            if (_contentRenderer is not null)
            {
                ContentRenderer.ZoomScaleChanged -= ContentRenderer_ZoomScale;
            }

            _contentRenderer = value;

            if (_contentRenderer is not null)
            {
                ContentRenderer.ZoomScaleChanged += ContentRenderer_ZoomScale;
                _zoomLevel = _contentRenderer.ZoomLevel;
            }

            UpdateZoomMenu();
        }
    }
    #endregion

    #region Methods.
    /// <summary>Handles the ZoomScale event of the ContentRenderer control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="ZoomScaleEventArgs"/> instance containing the event data.</param>
    private void ContentRenderer_ZoomScale(object sender, ZoomScaleEventArgs e)
    {
        _zoomLevel = _contentRenderer.ZoomLevel;
        UpdateZoomMenu();
    }

    /// <summary>
    /// Function to update the image type menu to reflect the current selection.
    /// </summary>
    /// <param name="dataContext">The current data context.</param>
    private void UpdateImageTypeMenu(IImageContent dataContext)
    {
        if (dataContext is null)
        {
            ButtonImageType.TextLine1 = ImageType.Unknown.ToString();
            return;
        }

        ToolStripMenuItem currentItem = dataContext.ImageType switch
        {
            ImageType.ImageCube => ItemCubeMap,
            ImageType.Image3D => Item3DImage,
            _ => Item2DImage,
        };
        foreach (ToolStripMenuItem item in MenuImageType.Items.OfType<ToolStripMenuItem>().Where(item => item != currentItem))
        {
            item.Checked = false;
        }

        currentItem.Checked = true;

        ButtonImageType.TextLine1 = currentItem.Text;
    }

    /// <summary>
    /// Function to update the zoom item menu to reflect the current selection.
    /// </summary>
    private void UpdateZoomMenu()
    {
        if (!_menuItems.TryGetValue(_zoomLevel, out ToolStripMenuItem currentItem))
        {
            return;
        }

        foreach (ToolStripMenuItem item in MenuZoom.Items.OfType<ToolStripMenuItem>().Where(item => item != currentItem))
        {
            item.Checked = false;
        }

        if (!currentItem.Checked)
        {
            currentItem.Checked = true;
        }

        ButtonZoom.TextLine1 = string.Format(Resources.GORIMG_TEXT_ZOOM_BUTTON, _zoomLevel.GetName());
    }

    /// <summary>Handles the PropertyChanged event of the FxContext control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
    private void FxContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IFxContext.EffectsUpdated):
                ValidateButtons();
                break;
        }
    }

    /// <summary>Handles the PropertyChanged event of the DataContext control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The [PropertyChangedEventArgs] instance containing the event data.</param>
    private void DataContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IImageContent.ImageType):
                UpdateImageTypeMenu(DataContext);
                break;
            case nameof(IImageContent.Width):
            case nameof(IImageContent.Height):
            case nameof(IImageContent.PixelFormats):
                RefreshPixelFormats(DataContext);
                break;
            case nameof(IImageContent.IsPremultiplied):
                CheckPremultipliedAlpha.Checked = DataContext.IsPremultiplied;
                break;
            case nameof(IImageContent.CurrentPixelFormat):
                ButtonImageFormat.TextLine1 = $"{Resources.GORIMG_TEXT_IMAGE_FORMAT}: {DataContext.CurrentPixelFormat}";
                UpdatePixelFormatMenuSelection(DataContext);
                break;
            case nameof(IImageContent.Settings):
                RefreshExternalEditButton(DataContext);
                break;
        }

        ValidateButtons();
    }

    /// <summary>Handles the PropertyChanging event of the DataContext control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The [PropertyChangingEventArgs] instance containing the event data.</param>
    private void DataContext_PropertyChanging(object sender, PropertyChangingEventArgs e)
    {
    }

    /// <summary>Handles the Click event of the ButtonGrayScale control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonGrayScale_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.GrayScaleCommand is null) || (!DataContext.FxContext.GrayScaleCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.GrayScaleCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonGaussBlur control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxGaussBlur_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ShowBlurCommand is null) || (!DataContext.FxContext.ShowBlurCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ShowBlurCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxSharpen control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxSharpen_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ShowSharpenCommand is null) || (!DataContext.FxContext.ShowSharpenCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ShowSharpenCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxEmboss control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxEmboss_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ShowEmbossCommand is null) || (!DataContext.FxContext.ShowEmbossCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ShowEmbossCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxEdgeDetect control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxEdgeDetect_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ShowEdgeDetectCommand is null) || (!DataContext.FxContext.ShowEdgeDetectCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ShowEdgeDetectCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxInvert control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxInvert_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.InvertCommand is null) || (!DataContext.FxContext.InvertCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.InvertCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxBurn control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxBurn_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.BurnCommand is null) || (!DataContext.FxContext.BurnCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.BurnCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxOneBit control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxOneBit_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ShowOneBitCommand is null) || (!DataContext.FxContext.ShowOneBitCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ShowOneBitCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxDodge control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxDodge_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.DodgeCommand is null) || (!DataContext.FxContext.DodgeCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.DodgeCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonFxPosterize control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxPosterize_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ShowPosterizeCommand is null) || (!DataContext.FxContext.ShowPosterizeCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ShowPosterizeCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonGenerateMipMaps control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonGenerateMipMaps_Click(object sender, EventArgs e)
    {
        if ((DataContext?.ShowMipGenerationCommand is null)
            || (!DataContext.ShowMipGenerationCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.ShowMipGenerationCommand.Execute(null);
    }

    /// <summary>Handles the Click event of the ButtonSetAlpha control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonSetAlpha_Click(object sender, EventArgs e)
    {
        if ((DataContext?.ShowSetAlphaCommand is null)
            || (!DataContext.ShowSetAlphaCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.ShowSetAlphaCommand.Execute(null);
    }

    /// <summary>Handles the Click event of the ButtonDimensions control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonDimensions_Click(object sender, EventArgs e)
    {
        if ((DataContext?.ShowImageDimensionsCommand is null)
            || (!DataContext.ShowImageDimensionsCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.ShowImageDimensionsCommand.Execute(null);
    }

    /// <summary>Handles the Click event of the ButtonFx control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFx_Click(object sender, EventArgs e)
    {
        if ((DataContext?.ShowFxCommand is null) || (!DataContext.ShowFxCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.ShowFxCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the CollectionChanged event of the Codecs control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The [NotifyCollectionChangedEventArgs] instance containing the event data.</param>
    private void Codecs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (IGorgonImageCodec codec in e.NewItems.OfType<IGorgonImageCodec>())
                {
                    if (MenuCodecs.Items.OfType<ToolStripMenuItem>().Any(item => item.Tag == codec))
                    {
                        continue;
                    }

                    AddCodecItem(codec);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (IGorgonImageCodec codec in e.OldItems.OfType<IGorgonImageCodec>())
                {
                    RemoveCodecItem(codec);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                ClearCodecs();
                break;
        }
        ValidateButtons();
    }


    /// <summary>Handles the Click event of the ButtonImport control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private async void ButtonImport_Click(object sender, EventArgs e)
    {
        if ((DataContext?.ImportFileCommand is null) || (!DataContext.ImportFileCommand.CanExecute(0)))
        {
            return;
        }

        float dpi;
        using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
        {
            dpi = g.DpiX / 96.0f;
        }

        await DataContext.ImportFileCommand.ExecuteAsync(dpi);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the Item control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The [EventArgs] instance containing the event data.</param>
    private void PixelFormatItem_Click(object sender, EventArgs e)
    {
        if (DataContext is null)
        {
            return;
        }

        var item = (ToolStripMenuItem)sender;
        var format = (BufferFormat)item.Tag;

        if ((DataContext.ConvertFormatCommand is not null) && (DataContext.ConvertFormatCommand.CanExecute(format)))
        {
            DataContext.ConvertFormatCommand.Execute(format);
        }

        // Ensure only this item is checked.
        UpdatePixelFormatMenuSelection(DataContext);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the Item control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The [EventArgs] instance containing the event data.</param>
    private void CodecItem_Click(object sender, EventArgs e)
    {
        var item = (ToolStripMenuItem)sender;
        var codec = item.Tag as IGorgonImageCodec;

        if ((DataContext?.ExportImageCommand is not null) && (DataContext.ExportImageCommand.CanExecute(codec)))
        {
            DataContext.ExportImageCommand.Execute(codec);
        }
    }

    /// <summary>Handles the MenuItemClick event of the ExternalEditor control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="T:System.EventArgs">EventArgs</see> instance containing the event data.</param>
    private void ExternalEditor_MenuItemClick(object sender, EventArgs e)
    {
        string exePath = GetExePath((ToolStripMenuItem)sender);

        if ((DataContext?.EditInAppCommand is null) || (!DataContext.EditInAppCommand.CanExecute(exePath)))
        {
            return;
        }

        DataContext.EditInAppCommand.Execute(exePath);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonEditInApp control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonEditInApp_Click(object sender, EventArgs e)
    {
        string exePath = GetExePath(null);

        if ((DataContext?.EditInAppCommand is null) || (!DataContext.EditInAppCommand.CanExecute(exePath)))
        {
            return;
        }

        DataContext.EditInAppCommand.Execute(exePath);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonSaveImage control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private async void ButtonSaveImage_Click(object sender, EventArgs e)
    {
        if ((DataContext?.SaveContentCommand is null) || (!DataContext.SaveContentCommand.CanExecute(SaveReason.UserSave)))
        {
            return;
        }

        await DataContext.SaveContentCommand.ExecuteAsync(SaveReason.UserSave);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonImageRedo control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonImageRedo_Click(object sender, EventArgs e)
    {
        if ((DataContext?.RedoCommand is null) || (!DataContext.RedoCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.RedoCommand.Execute(null);
        ValidateButtons();
    }

    /// <summary>Handles the Click event of the ButtonImageUndo control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonImageUndo_Click(object sender, EventArgs e)
    {
        if ((DataContext?.UndoCommand is null) || (!DataContext.UndoCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.UndoCommand.Execute(null);
        ValidateButtons();
    }


    /// <summary>Handles the Click event of the ButtonPremultipliedAlpha control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private async void CheckPremultipliedAlpha_Click(object sender, EventArgs e)
    {
        if ((DataContext?.PremultipliedAlphaCommand is null) || (!DataContext.PremultipliedAlphaCommand.CanExecute(true)))
        {
            return;
        }

        await DataContext.PremultipliedAlphaCommand.ExecuteAsync(CheckPremultipliedAlpha.Checked);
        ValidateButtons();
    }

    /// <summary>
    /// Function to retrieve the correct path to use for the external editor.
    /// </summary>
    /// <param name="item">The menu item that was used.</param>
    /// <returns>The path to the exe.</returns>
    private string GetExePath(ToolStripMenuItem item)
    {
        if (DataContext is null)
        {
            return string.Empty;
        }

        if (item is null)
        {
            return string.IsNullOrWhiteSpace(DataContext.UserEditorInfo.ExePath) ? DataContext.ExternalEditorInfo.ExePath : DataContext.UserEditorInfo.ExePath;
        }

        return DataContext.ExternalEditorInfo.ExePath;
    }

    /// <summary>
    /// Function to validate the state of the buttons.
    /// </summary>
    private void ValidateButtons()
    {
        if (DataContext is null)
        {
            return;
        }

        ButtonImport.Enabled = DataContext.ImportFileCommand?.CanExecute(0) ?? false;
        ButtonEditInApp.Enabled = DataContext.EditInAppCommand?.CanExecute(GetExePath(null)) ?? false;
        ButtonDimensions.Enabled = DataContext.ShowImageDimensionsCommand?.CanExecute(null) ?? false;
        ButtonGenerateMipMaps.Enabled = DataContext.ShowMipGenerationCommand?.CanExecute(null) ?? false;
        ButtonImageFormat.Enabled = DataContext.ConvertFormatCommand?.CanExecute(BufferFormat.Unknown) ?? false;
        ButtonImageType.Enabled = DataContext.ChangeImageTypeCommand?.CanExecute(ImageType.Unknown) ?? false;
        ButtonImageUndo.Enabled = DataContext.UndoCommand?.CanExecute(null) ?? false;
        ButtonImageRedo.Enabled = DataContext.RedoCommand?.CanExecute(null) ?? false;
        ButtonExport.Enabled = DataContext.ExportImageCommand?.CanExecute(null) ?? false;
        ButtonSaveImage.Enabled = DataContext.SaveContentCommand?.CanExecute(SaveReason.UserSave) ?? false;
        CheckPremultipliedAlpha.Enabled = DataContext.PremultipliedAlphaCommand?.CanExecute(true) ?? false;
        ButtonSetAlpha.Enabled = DataContext.ShowSetAlphaCommand?.CanExecute(null) ?? false;
        ButtonFx.Enabled = DataContext.ShowFxCommand?.CanExecute(null) ?? false;
        ButtonGaussBlur.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ShowBlurCommand?.CanExecute(null) ?? false);
        ButtonGrayScale.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.GrayScaleCommand?.CanExecute(null) ?? false);
        ButtonFxInvert.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.InvertCommand?.CanExecute(null) ?? false);
        ButtonFxSharpen.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ShowSharpenCommand?.CanExecute(null) ?? false);
        ButtonFxEmboss.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ShowEmbossCommand?.CanExecute(null) ?? false);
        ButtonFxEdgeDetect.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ShowEdgeDetectCommand?.CanExecute(null) ?? false);
        ButtonFxBurn.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.BurnCommand?.CanExecute(null) ?? false);
        ButtonFxDodge.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.DodgeCommand?.CanExecute(null) ?? false);
        ButtonFxPosterize.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ShowPosterizeCommand?.CanExecute(null) ?? false);
        ButtonFxOneBit.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ShowOneBitCommand?.CanExecute(null) ?? false);
        ButtonFxApply.Enabled = (!ButtonFx.Enabled) && (DataContext.FxContext?.ApplyCommand?.CanExecute(null) ?? false);            

        if (DataContext.ChangeImageTypeCommand is null)
        {
            ButtonImageType.Enabled = false;
            return;
        }

        Item2DImage.Enabled = DataContext.ChangeImageTypeCommand.CanExecute(ImageType.Image2D);
        Item3DImage.Enabled = DataContext.ChangeImageTypeCommand.CanExecute(ImageType.Image3D);
        ItemCubeMap.Enabled = DataContext.ChangeImageTypeCommand.CanExecute(ImageType.ImageCube);
    }

    /// <summary>
    /// Function to ensure the menu is single selection only.
    /// </summary>
    /// <param name="dataContext">The current data context.</param>
    private void UpdatePixelFormatMenuSelection(IImageContent dataContext)
    {
        if (dataContext is null)
        {
            return;
        }

        foreach (ToolStripMenuItem item in MenuImageFormats.Items.OfType<ToolStripMenuItem>())
        {
            item.Checked = ((BufferFormat)item.Tag) == dataContext.CurrentPixelFormat;
        }
    }

    /// <summary>
    /// Function to add an image pixel format to the list.
    /// </summary>
    /// <param name="dataContext">The current data context.</param>
    private void RefreshPixelFormats(IImageContent dataContext)
    {
        foreach (ToolStripMenuItem item in MenuImageFormats.Items.OfType<ToolStripMenuItem>())
        {
            item.Click -= PixelFormatItem_Click;
        }

        MenuImageFormats.Items.Clear();

        if (dataContext is null)
        {
            return;
        }

        foreach (BufferFormat format in dataContext.PixelFormats)
        {
            var info = new GorgonFormatInfo(format);
            
            var item = new ToolStripMenuItem(format.ToString())
            {
                Name = format.ToString(),
                Checked = dataContext.CurrentPixelFormat == format,
                CheckOnClick = true,
                Tag = format,
                Enabled = ((!info.IsCompressed) ||
                            (((dataContext.Width % 4) == 0) && ((dataContext.Height % 4) == 0)))
            };

            if (!item.Enabled)
            {
                item.ToolTipText = string.Format(Resources.GORIMG_TIP_DISABLED_FORMAT, format);
            }
            else
            {
                item.ToolTipText = string.Empty;
            }

            item.Click += PixelFormatItem_Click;

            MenuImageFormats.Items.Add(item);

            if (item.Checked)
            {
                ButtonImageFormat.TextLine1 = $"{Resources.GORIMG_TEXT_IMAGE_FORMAT}: {format}";
            }
        }
    }


    /// <summary>
    /// Function to clear the codec list.
    /// </summary>
    private void ClearCodecs()
    {
        foreach (ToolStripMenuItem item in MenuCodecs.Items.OfType<ToolStripMenuItem>())
        {
            item.Click -= CodecItem_Click;
        }
        MenuCodecs.Items.Clear();
    }

    /// <summary>
    /// Function to add an image codec to the list.
    /// </summary>
    /// <param name="codec">The codec to add.</param>
    private void AddCodecItem(IGorgonImageCodec codec)
    {
        var item = new ToolStripMenuItem($"{codec.CodecDescription} ({codec.Codec})")
        {
            Name = codec.Name,
            CheckOnClick = false,
            Tag = codec
        };

        item.Click += CodecItem_Click;

        MenuCodecs.Items.Add(item);
    }

    /// <summary>
    /// Function to remove a codec item from the list.
    /// </summary>
    /// <param name="codec">The codec to remove.</param>
    private void RemoveCodecItem(IGorgonImageCodec codec)
    {
        ToolStripMenuItem menuItem = MenuCodecs.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Tag == codec);

        if (menuItem is null)
        {
            return;
        }

        menuItem.Click -= CodecItem_Click;
        MenuCodecs.Items.Remove(menuItem);
    }

    /// <summary>
    /// Function to unassign the events for the data context.
    /// </summary>
    private void UnassignEvents()
    {
        if (DataContext is null)
        {
            return;
        }

        if (DataContext.Codecs is not null)
        {
            DataContext.Codecs.CollectionChanged -= Codecs_CollectionChanged;
        }

        DataContext.FxContext.PropertyChanged -= FxContext_PropertyChanged;
        DataContext.PropertyChanging -= DataContext_PropertyChanging;
        DataContext.PropertyChanged -= DataContext_PropertyChanged;
    }

    

    /// <summary>
    /// Function to reset the view when no data context is assigned.
    /// </summary>
    private void ResetDataContext()
    {
        CheckPremultipliedAlpha.Checked = false;
        RibbonImageContent.Enabled = false;
        ClearCodecs();
        UpdateZoomMenu();
        UpdateImageTypeMenu(null);
        ItemZoomToWindow.Checked = true;
    }


    /// <summary>Handles the Click event of the ButtonFxApply control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxApply_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.ApplyCommand is null) || (!DataContext.FxContext.ApplyCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.ApplyCommand.Execute(null);
    }

    /// <summary>Handles the Click event of the ButtonFxCancel control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ButtonFxCancel_Click(object sender, EventArgs e)
    {
        if ((DataContext?.FxContext?.CancelCommand is null) || (!DataContext.FxContext.CancelCommand.CanExecute(null)))
        {
            return;
        }

        DataContext.FxContext.CancelCommand.Execute(null);
    }

    /// <summary>Handles the Click event of the ItemZoomToWindow control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The [EventArgs] instance containing the event data.</param>
    private void ItemZoom_Click(object sender, EventArgs e)
    {
        var item = (ToolStripMenuItem)sender;

        if ((item.Tag is null) || (!Enum.TryParse(item.Tag.ToString(), out ZoomLevels zoom)))
        {
            item.Checked = false;
            return;
        }

        // Do not let us uncheck.
        if (_zoomLevel == zoom)
        {
            item.Checked = true;
            return;
        }

        _zoomLevel = zoom;
        UpdateZoomMenu();

        ContentRenderer?.MoveTo(new Vector2(ContentRenderer.ClientSize.Width * 0.5f, ContentRenderer.ClientSize.Height * 0.5f),
                                _zoomLevel.GetScale());
    }


    /// <summary>Handles the Click event of the Item2DImage control.</summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void ItemImageType_Click(object sender, EventArgs e)
    {
        var item = (ToolStripMenuItem)sender;

        if (item.Tag is null)
        {
            item.Checked = false;
            return;
        }

        var imageType = (ImageType)item.Tag;

        if ((DataContext?.ChangeImageTypeCommand is null)
            || (!DataContext.ChangeImageTypeCommand.CanExecute(imageType))
            || (DataContext.ImageType == imageType))
        {
            item.Checked = true;
            return;
        }

        DataContext.ChangeImageTypeCommand.Execute(imageType);
        ValidateButtons();
    }

    /// <summary>
    /// Function to refresh the external edit button.
    /// </summary>
    /// <param name="dataContext">The current data context.</param>
    private void RefreshExternalEditButton(IImageContent dataContext)
    {
        if (!string.IsNullOrWhiteSpace(dataContext.UserEditorInfo.ExePath))
        {
            ButtonEditInApp.TextLine2 = dataContext.UserEditorInfo.FriendlyName;
            if (dataContext.UserEditorInfo.IconLarge is not null)
            {
                ButtonEditInApp.ImageLarge = dataContext.UserEditorInfo.IconLarge;
            }

            if (dataContext.UserEditorInfo.IconSmall is not null)
            {
                ButtonEditInApp.ImageSmall = dataContext.UserEditorInfo.IconSmall;
            }

            ButtonEditInApp.ButtonType = Krypton.Ribbon.GroupButtonType.Split;

            foreach (ToolStripMenuItem item in MenuExternalEdit.Items)
            {
                item.Click -= ExternalEditor_MenuItemClick;
                item.Dispose();
            }

            MenuExternalEdit.Items.Clear();

            ToolStripMenuItem newItem = new(dataContext.ExternalEditorInfo.FriendlyName, dataContext.ExternalEditorInfo.IconSmall, ExternalEditor_MenuItemClick);
            MenuExternalEdit.Items.Add(newItem);

            return;
        }

        ButtonEditInApp.ButtonType = Krypton.Ribbon.GroupButtonType.Push;
        ButtonEditInApp.TextLine2 = dataContext.ExternalEditorInfo.FriendlyName;

        if (dataContext.ExternalEditorInfo.IconLarge is not null)
        {
            ButtonEditInApp.ImageLarge = dataContext.ExternalEditorInfo.IconLarge;
        }
        if (dataContext.ExternalEditorInfo.IconSmall is not null)
        {
            ButtonEditInApp.ImageSmall = dataContext.ExternalEditorInfo.IconSmall;
        }
    }        

    /// <summary>
    /// Function to initialize the view based on the data context.
    /// </summary>
    /// <param name="dataContext">The data context used to initialize.</param>
    private void InitializeFromDataContext(IImageContent dataContext)
    {
        if (dataContext is null)
        {
            ResetDataContext();
            return;
        }

        ClearCodecs();
        foreach (IGorgonImageCodec codec in dataContext.Codecs)
        {
            AddCodecItem(codec);
        }

        RefreshPixelFormats(dataContext);

        UpdatePixelFormatMenuSelection(dataContext);
        UpdateZoomMenu();
        UpdateImageTypeMenu(dataContext);

        RefreshExternalEditButton(dataContext);

        CheckPremultipliedAlpha.Checked = dataContext.IsPremultiplied;
    }

    /// <summary>Function to assign a data context to the view as a view model.</summary>
    /// <param name="dataContext">The data context to assign.</param>
    /// <remarks>Data contexts should be nullable, in that, they should reset the view back to its original state when the context is null.</remarks>
    public void SetDataContext(IImageContent dataContext)
    {
        UnassignEvents();

        InitializeFromDataContext(dataContext);

        DataContext = dataContext;
        ValidateButtons();

        if (DataContext is null)
        {
            return;
        }

        DataContext.PropertyChanged += DataContext_PropertyChanged;
        DataContext.PropertyChanging += DataContext_PropertyChanging;
        DataContext.FxContext.PropertyChanged += FxContext_PropertyChanged;

        if (DataContext.Codecs is not null)
        {
            DataContext.Codecs.CollectionChanged += Codecs_CollectionChanged;
        }
    }        

    /// <summary>
    /// Function to reset the zoom back to the default.
    /// </summary>
    public void ResetZoom()
    {
        _zoomLevel = ZoomLevels.ToWindow;
        UpdateZoomMenu();
    }
    #endregion

    #region Constructor.
    /// <summary>Initializes a new instance of the FormRibbon class.</summary>
    public FormRibbon()
    {
        InitializeComponent();

        Item2DImage.Tag = ImageType.Image2D;
        ItemCubeMap.Tag = ImageType.ImageCube;
        Item3DImage.Tag = ImageType.Image3D;

        foreach (ToolStripMenuItem menuItem in MenuZoom.Items.OfType<ToolStripMenuItem>())
        {
            if (!Enum.TryParse(menuItem.Tag.ToString(), out ZoomLevels level))
            {
                menuItem.Enabled = false;
                continue;
            }

            menuItem.Text = level.GetName();
            _menuItems[level] = menuItem;
        }
    }
    #endregion
}
