﻿using System.ComponentModel;
using System.Windows.Forms;

namespace Gorgon.Examples
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

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
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.LabelPleaseWait = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LabelPleaseWait
            // 
            this.LabelPleaseWait.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LabelPleaseWait.Location = new System.Drawing.Point(0, 0);
            this.LabelPleaseWait.Name = "LabelPleaseWait";
            this.LabelPleaseWait.Size = new System.Drawing.Size(1264, 761);
            this.LabelPleaseWait.TabIndex = 0;
            this.LabelPleaseWait.Text = "Example is loading, please wait...";
            this.LabelPleaseWait.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1264, 761);
            this.Controls.Add(this.LabelPleaseWait);
            this.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Effects";
            this.ResumeLayout(false);

        }

        #endregion

        private Label LabelPleaseWait;
    }
}
