namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>线扫相机实时预览控件设计器。</summary>
    partial class LineScanCameraLiveDebugControl
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.PictureBox pictureBoxPreview;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelSettings;
        private System.Windows.Forms.Label labelCameraInfo;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.NumericUpDown numericWidth;
        private System.Windows.Forms.NumericUpDown numericHeight;
        private System.Windows.Forms.NumericUpDown numericExposure;
        private System.Windows.Forms.NumericUpDown numericGain;
        private System.Windows.Forms.ComboBox comboTriggerSelector;
        private System.Windows.Forms.ComboBox comboTriggerMode;
        private System.Windows.Forms.ComboBox comboTriggerSource;
        private System.Windows.Forms.ComboBox comboTriggerEdge;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.Button buttonPreview;

        /// <summary>初始化设计器控件。</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.pictureBoxPreview = new System.Windows.Forms.PictureBox();
            this.tableLayoutPanelSettings = new System.Windows.Forms.TableLayoutPanel();
            this.labelCameraInfo = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.numericWidth = new System.Windows.Forms.NumericUpDown(); this.numericHeight = new System.Windows.Forms.NumericUpDown();
            this.numericExposure = new System.Windows.Forms.NumericUpDown(); this.numericGain = new System.Windows.Forms.NumericUpDown();
            this.comboTriggerSelector = new System.Windows.Forms.ComboBox();
            this.comboTriggerMode = new System.Windows.Forms.ComboBox(); this.comboTriggerSource = new System.Windows.Forms.ComboBox(); this.comboTriggerEdge = new System.Windows.Forms.ComboBox();
            this.buttonApply = new System.Windows.Forms.Button(); this.buttonPreview = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit(); this.splitContainerMain.Panel1.SuspendLayout(); this.splitContainerMain.Panel2.SuspendLayout(); this.splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).BeginInit();
            this.SuspendLayout();
            // 必须先给SplitContainer一个能容纳两侧最小宽度的设计尺寸，再设置最小宽度和分隔位置。
            // 否则控件在EndInit阶段仍使用默认小尺寸，会抛出SplitterDistance越界异常。
            this.splitContainerMain.Size = new System.Drawing.Size(980, 600);
            this.splitContainerMain.SplitterWidth = 5;
            this.splitContainerMain.Panel1MinSize = 300;
            this.splitContainerMain.Panel2MinSize = 360;
            this.splitContainerMain.SplitterDistance = 615;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxPreview.BackColor = System.Drawing.Color.FromArgb(30, 30, 30); this.pictureBoxPreview.Dock = System.Windows.Forms.DockStyle.Fill; this.pictureBoxPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.splitContainerMain.Panel1.Controls.Add(this.pictureBoxPreview);
            this.tableLayoutPanelSettings.ColumnCount = 2; this.tableLayoutPanelSettings.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 125F)); this.tableLayoutPanelSettings.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F)); this.tableLayoutPanelSettings.Dock = System.Windows.Forms.DockStyle.Fill; this.tableLayoutPanelSettings.Padding = new System.Windows.Forms.Padding(10);
            string[] labels = { "图像宽度", "图像高度", "曝光时间(us)", "增益", "触发选择器", "触发模式", "触发源", "触发极性" };
            System.Windows.Forms.Control[] editors = { this.numericWidth, this.numericHeight, this.numericExposure, this.numericGain, this.comboTriggerSelector, this.comboTriggerMode, this.comboTriggerSource, this.comboTriggerEdge };
            for (int i = 0; i < labels.Length; i++) { var label = new System.Windows.Forms.Label { Text = labels[i], Dock = System.Windows.Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }; this.tableLayoutPanelSettings.Controls.Add(label, 0, i + 1); editors[i].Dock = System.Windows.Forms.DockStyle.Fill; this.tableLayoutPanelSettings.Controls.Add(editors[i], 1, i + 1); this.tableLayoutPanelSettings.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F)); }
            this.tableLayoutPanelSettings.SetColumnSpan(this.labelCameraInfo, 2); this.labelCameraInfo.Dock = System.Windows.Forms.DockStyle.Fill; this.labelCameraInfo.Text = "线扫相机"; this.tableLayoutPanelSettings.Controls.Add(this.labelCameraInfo, 0, 0);
            System.Windows.Forms.NumericUpDown[] nums = { this.numericWidth, this.numericHeight, this.numericExposure, this.numericGain }; decimal[] vals = { 4096, 1000, 100, 0 }; for (int i = 0; i < nums.Length; i++) { nums[i].Maximum = 1000000; nums[i].Value = vals[i]; nums[i].DecimalPlaces = i == 2 || i == 3 ? 2 : 0; }
            this.comboTriggerSelector.DropDownStyle = this.comboTriggerMode.DropDownStyle = this.comboTriggerSource.DropDownStyle = this.comboTriggerEdge.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList; this.comboTriggerMode.Items.AddRange(new object[] { "关闭", "开启" });
            this.comboTriggerSelector.SelectedIndexChanged += new System.EventHandler(this.comboTriggerSelector_SelectedIndexChanged);
            this.comboTriggerMode.SelectedIndexChanged += new System.EventHandler(this.comboTriggerMode_SelectedIndexChanged);
            this.comboTriggerSource.SelectedIndexChanged += new System.EventHandler(this.comboTriggerSource_SelectedIndexChanged);
            this.buttonApply.Text = "应用参数"; this.buttonApply.Dock = System.Windows.Forms.DockStyle.Fill; this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click); this.tableLayoutPanelSettings.Controls.Add(this.buttonApply, 0, 9);
            this.buttonPreview.Text = "开始预览"; this.buttonPreview.Dock = System.Windows.Forms.DockStyle.Fill; this.buttonPreview.Click += new System.EventHandler(this.buttonPreview_Click); this.tableLayoutPanelSettings.Controls.Add(this.buttonPreview, 1, 9);
            this.tableLayoutPanelSettings.SetColumnSpan(this.labelStatus, 2); this.labelStatus.Dock = System.Windows.Forms.DockStyle.Fill; this.labelStatus.Text = "未开始预览"; this.tableLayoutPanelSettings.Controls.Add(this.labelStatus, 0, 10);
            this.splitContainerMain.Panel2.Controls.Add(this.tableLayoutPanelSettings); this.Controls.Add(this.splitContainerMain); this.Name = "LineScanCameraLiveDebugControl"; this.Size = new System.Drawing.Size(980, 600);
            this.splitContainerMain.Panel1.ResumeLayout(false); this.splitContainerMain.Panel2.ResumeLayout(false); ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit(); this.splitContainerMain.ResumeLayout(false); ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).EndInit(); this.ResumeLayout(false);
        }
    }
}
