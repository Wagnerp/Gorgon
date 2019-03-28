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
// Created: March 28, 2019 9:48:28 AM
// 
#endregion

using System;
using System.ComponentModel;
using Gorgon.Editor.UI.Controls;
using Gorgon.Graphics;
using Gorgon.Editor.UI;

namespace Gorgon.Editor.SpriteEditor
{
    /// <summary>
    /// A color selection control for the sprite.
    /// </summary>
    internal partial class SpriteColor
        : EditorSubPanelCommon, IDataContext<ISpriteColorEdit>
    {
        #region Properties.
        /// <summary>
        /// Property to return the data context for the view.
        /// </summary>
        public ISpriteColorEdit DataContext
        {
            get;
            private set;
        }
        #endregion

        #region Methods.
        /// <summary>Handles the ColorChanged event of the Picker control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="T:Gorgon.Editor.UI.Controls.ColorChangedEventArgs"/> instance containing the event data.</param>
        private void Picker_ColorChanged(object sender, ColorChangedEventArgs e)
        {
            if (DataContext == null)
            {
                return;
            }

            DataContext.SpriteColor = e.Color;
            ValidateOk();
        }

        /// <summary>
        /// Function to unassign the events from the data context.
        /// </summary>
        private void UnassignEvents()
        {
            if (DataContext == null)
            {
                return;
            }

            DataContext.PropertyChanged -= DataContext_PropertyChanged;
        }

        /// <summary>Handles the PropertyChanged event of the DataContext control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void DataContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ISpriteColorEdit.OriginalSpriteColor):
                    Picker.OriginalColor = DataContext.OriginalSpriteColor;
                    break;
                case nameof(ISpriteColorEdit.SpriteColor):
                    Picker.SelectedColor = DataContext.SpriteColor;
                    break;
            }
        }

        /// <summary>
        /// Function to reset the values on the control for a null data context.
        /// </summary>
        private void ResetDataContext()
        {
            UnassignEvents();
            Picker.OriginalColor = Picker.SelectedColor = GorgonColor.BlackTransparent;
        }

        /// <summary>
        /// Function to initialize the control with the data context.
        /// </summary>
        /// <param name="dataContext">The data context to assign.</param>
        private void InitializeFromDataContext(ISpriteColorEdit dataContext)
        {
            ResetDataContext();

            Picker.OriginalColor = dataContext.OriginalSpriteColor;
            Picker.SelectedColor = dataContext.SpriteColor;
        }

        /// <summary>Function to submit the change.</summary>
        protected override void OnSubmit()
        {
            base.OnSubmit();

            if ((DataContext?.OkCommand == null) || (!DataContext.OkCommand.CanExecute(null)))
            {
                return;
            }

            DataContext.OkCommand.Execute(null);
        }

        /// <summary>Function to cancel the change.</summary>
        protected override void OnCancel()
        {
            base.OnCancel();

            if ((DataContext?.CancelCommand == null) || (!DataContext.CancelCommand.CanExecute(null)))
            {
                return;
            }

            DataContext.CancelCommand.Execute(null);
        }

        /// <summary>
        /// Function to validate the state of the OK button.
        /// </summary>
        /// <returns><b>true</b> if the OK button is valid, <b>false</b> if not.</returns>
        protected override bool OnValidateOk() => (DataContext?.OkCommand != null) && (DataContext.OkCommand.CanExecute(null));

        /// <summary>Raises the <see cref="E:System.Windows.Forms.UserControl.Load"/> event.</summary>
        /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (IsDesignTime)
            {
                return;
            }

            DataContext?.OnLoad();
        }

        /// <summary>Function to assign a data context to the view as a view model.</summary>
        /// <param name="dataContext">The data context to assign.</param>
        /// <remarks>Data contexts should be nullable, in that, they should reset the view back to its original state when the context is null.</remarks>
        public void SetDataContext(ISpriteColorEdit dataContext) 
        {
            InitializeFromDataContext(dataContext);

            DataContext = dataContext;

            if (DataContext == null)
            {
                return;
            }

            DataContext.PropertyChanged += DataContext_PropertyChanged;
        }
        #endregion

        #region Constructor/Finalizer.
        /// <summary>Initializes a new instance of the <see cref="T:Gorgon.Editor.SpriteEditor.SpriteColor"/> class.</summary>
        public SpriteColor() => InitializeComponent();
        #endregion
    }
}