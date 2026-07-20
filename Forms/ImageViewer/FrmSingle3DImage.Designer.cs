namespace TDJS_Vision.Forms.ImageViewer
{
    partial class FrmSingle3DImage
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，则为 true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 初始化窗体控件。
        /// </summary>
        private void InitializeComponent()
        {
            this.pointCloudPreviewControl = new TDJS_Vision.Forms.ImageViewer.PointCloudPreviewControl();
            this.SuspendLayout();
            // 
            // pointCloudPreviewControl
            // 
            this.pointCloudPreviewControl.BackColor = System.Drawing.Color.Black;
            this.pointCloudPreviewControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pointCloudPreviewControl.ForeColor = System.Drawing.Color.White;
            this.pointCloudPreviewControl.Location = new System.Drawing.Point(0, 0);
            this.pointCloudPreviewControl.Name = "pointCloudPreviewControl";
            this.pointCloudPreviewControl.Size = new System.Drawing.Size(800, 600);
            this.pointCloudPreviewControl.TabIndex = 0;
            // 
            // FrmSingle3DImage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.pointCloudPreviewControl);
            this.Name = "FrmSingle3DImage";
            this.ShowIcon = false;
            this.Text = "3D图像窗口";
            this.ResumeLayout(false);
        }

        /// <summary>
        /// 点云预览控件。
        /// </summary>
        private TDJS_Vision.Forms.ImageViewer.PointCloudPreviewControl pointCloudPreviewControl;
    }
}
