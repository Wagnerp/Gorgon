﻿namespace Gorgon.Editor.SpriteEditor
{
    partial class SpriteColor
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            if (disposing)
            {
                UnassignEvents();
                DataContext?.OnUnload();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.Picker = new Gorgon.Editor.UI.Controls.ColorPicker();
            this.PanelBody.SuspendLayout();
            this.SuspendLayout();
            // 
            // PanelBody
            // 
            this.PanelBody.Controls.Add(this.Picker);
            this.PanelBody.Size = new System.Drawing.Size(408, 411);
            // 
            // Picker
            // 
            this.Picker.AutoSize = true;
            this.Picker.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Picker.Location = new System.Drawing.Point(0, 0);
            this.Picker.MinimumSize = new System.Drawing.Size(414, 0);
            this.Picker.Name = "Picker";
            this.Picker.Size = new System.Drawing.Size(414, 411);
            this.Picker.TabIndex = 0;
            this.Picker.ColorChanged += new System.EventHandler<Gorgon.Editor.UI.Controls.ColorChangedEventArgs>(this.Picker_ColorChanged);
            // 
            // SpriteColor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ForeColor = System.Drawing.Color.White;
            this.Name = "SpriteColor";
            this.Size = new System.Drawing.Size(408, 468);
            this.Text = "Sprite Color";
            this.PanelBody.ResumeLayout(false);
            this.PanelBody.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private UI.Controls.ColorPicker Picker;
    }
}