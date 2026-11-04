namespace TDJS_Vision.Node._1_Acquisition.LineScanImageSource
{
    /// <summary>线扫图像源参数窗体设计器。</summary>
    partial class ParamFormLineScanImageSource
    {
        /// <summary>设计器组件容器。</summary>
        private System.ComponentModel.IContainer components = null;
        /// <summary>主布局。</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>相机选择框。</summary>
        private System.Windows.Forms.ComboBox comboBoxCamera;
        /// <summary>图像高度输入框。</summary>
        private System.Windows.Forms.NumericUpDown numericHeight;
        /// <summary>图像宽度输入框。</summary>
        private System.Windows.Forms.NumericUpDown numericWidth;
        /// <summary>曝光输入框。</summary>
        private System.Windows.Forms.NumericUpDown numericExposure;
        /// <summary>增益输入框。</summary>
        private System.Windows.Forms.NumericUpDown numericGain;
        /// <summary>触发选择器选择框。</summary>
        private System.Windows.Forms.ComboBox comboTriggerSelector;
        /// <summary>触发模式选择框。</summary>
        private System.Windows.Forms.ComboBox comboTriggerMode;
        /// <summary>触发源选择框。</summary>
        private System.Windows.Forms.ComboBox comboTriggerSource;
        /// <summary>触发极性选择框。</summary>
        private System.Windows.Forms.ComboBox comboTriggerEdge;
        /// <summary>触发能力状态提示。</summary>
        private System.Windows.Forms.Label labelTriggerHint;
        /// <summary>刷新按钮。</summary>
        private System.Windows.Forms.Button buttonRefresh;
        /// <summary>保存按钮。</summary>
        private System.Windows.Forms.Button buttonSave;

        /// <summary>释放设计器资源。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>初始化设计器控件。</summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.comboBoxCamera = new System.Windows.Forms.ComboBox();
            this.numericHeight = new System.Windows.Forms.NumericUpDown();
            this.numericWidth = new System.Windows.Forms.NumericUpDown();
            this.numericExposure = new System.Windows.Forms.NumericUpDown();
            this.numericGain = new System.Windows.Forms.NumericUpDown();
            this.comboTriggerSelector = new System.Windows.Forms.ComboBox();
            this.comboTriggerMode = new System.Windows.Forms.ComboBox();
            this.comboTriggerSource = new System.Windows.Forms.ComboBox();
            this.comboTriggerEdge = new System.Windows.Forms.ComboBox();
            this.labelTriggerHint = new System.Windows.Forms.Label();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            string[] labels = { "选择线扫相机", "图像宽度(像素)", "图像高度(行)", "曝光时间(us)", "增益", "触发选择器", "触发模式", "触发源", "触发极性" };
            this.tableLayoutPanelMain.ColumnCount = 3;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Padding = new System.Windows.Forms.Padding(12);
            for (int i = 0; i < labels.Length; i++)
            {
                System.Windows.Forms.Label label = new System.Windows.Forms.Label();
                label.Text = labels[i]; label.Dock = System.Windows.Forms.DockStyle.Fill; label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                this.tableLayoutPanelMain.Controls.Add(label, 0, i);
                this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            }
            this.comboBoxCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCamera.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBoxCamera.SelectedIndexChanged += new System.EventHandler(this.comboBoxCamera_SelectedIndexChanged);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxCamera, 1, 0);
            this.buttonRefresh.Text = "刷新"; this.buttonRefresh.Dock = System.Windows.Forms.DockStyle.Fill; this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            this.tableLayoutPanelMain.Controls.Add(this.buttonRefresh, 2, 0);
            System.Windows.Forms.NumericUpDown[] values = { this.numericWidth, this.numericHeight, this.numericExposure, this.numericGain };
            decimal[] maximums = { 1000000M, 1000000M, 1000000M, 1000M };
            decimal[] defaults = { 4096M, 1000M, 100M, 0M };
            for (int i = 0; i < values.Length; i++)
            {
                values[i].Maximum = maximums[i]; values[i].Value = defaults[i]; values[i].DecimalPlaces = i == 2 || i == 3 ? 2 : 0; values[i].Dock = System.Windows.Forms.DockStyle.Fill;
                this.tableLayoutPanelMain.Controls.Add(values[i], 1, i + 1);
            }
            System.Windows.Forms.ComboBox[] triggerBoxes = { this.comboTriggerSelector, this.comboTriggerMode, this.comboTriggerSource, this.comboTriggerEdge };
            string[][] triggerItems = { new string[0], new[] { "关闭", "开启" }, new string[0], new string[0] };
            for (int i = 0; i < triggerBoxes.Length; i++)
            {
                triggerBoxes[i].DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
                triggerBoxes[i].Dock = System.Windows.Forms.DockStyle.Fill;
                triggerBoxes[i].Items.AddRange(triggerItems[i]);
                // 触发源和触发极性由已连接相机的GenICam能力动态加载，初始化时列表为空。
                triggerBoxes[i].SelectedIndex = triggerBoxes[i].Items.Count > 0 ? 0 : -1;
                this.tableLayoutPanelMain.Controls.Add(triggerBoxes[i], 1, i + 5);
            }
            this.comboTriggerSelector.SelectedIndexChanged += new System.EventHandler(this.comboTriggerSelector_SelectedIndexChanged);
            this.comboTriggerMode.SelectedIndexChanged += new System.EventHandler(this.comboTriggerMode_SelectedIndexChanged);
            this.comboTriggerSource.SelectedIndexChanged += new System.EventHandler(this.comboTriggerSource_SelectedIndexChanged);
            this.labelTriggerHint.Text = "触发模式关闭时不读取触发源和触发极性"; this.labelTriggerHint.Dock = System.Windows.Forms.DockStyle.Fill; this.labelTriggerHint.ForeColor = System.Drawing.Color.DimGray; this.labelTriggerHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.tableLayoutPanelMain.SetColumnSpan(this.labelTriggerHint, 2); this.tableLayoutPanelMain.Controls.Add(this.labelTriggerHint, 1, 9);
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.buttonSave.Text = "保存参数"; this.buttonSave.Dock = System.Windows.Forms.DockStyle.Fill; this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            this.tableLayoutPanelMain.Controls.Add(this.buttonSave, 1, 10);
            this.ClientSize = new System.Drawing.Size(560, 478);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Name = "ParamFormLineScanImageSource";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "线扫图像源参数";
            // 将内容布局放入继承窗体的客户区，避免覆盖 FormBase 的自定义标题栏。
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
        }
    }
}
