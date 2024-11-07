namespace eft_dma_radar.Source
{
    partial class Black
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
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            skglControl1 = new SkiaSharp.Views.Desktop.SKGLControl();
            //skglControl1.VSync = true;
            SuspendLayout();
            // 
            // skglControl1
            // 
            skglControl1.BackColor = Color.Black;
            skglControl1.Dock = DockStyle.Fill;
            skglControl1.Location = new Point(0, 0);
            skglControl1.Margin = new Padding(4, 4, 4, 4);
            skglControl1.Name = "skglControl1";
            skglControl1.Size = new Size(800, 450);
            skglControl1.TabIndex = 0;
            skglControl1.VSync = true;
            skglControl1.PaintSurface += skglControl1_PaintSurface;
            // 
            // Black
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            BackgroundImageLayout = ImageLayout.None;
            ClientSize = new Size(800, 450);
            Controls.Add(skglControl1);
            ForeColor = Color.Black;
            Name = "Black";
            ResumeLayout(false);
        }

        #endregion

        public static SkiaSharp.Views.Desktop.SKGLControl skglControl1;
    }
}