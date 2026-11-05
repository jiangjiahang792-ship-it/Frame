namespace TDJS_Vision.Forms.AiTrainForm
{
    partial class UnsupervisedRoiSettingsForm
    {
        /// <summary>设计器组件容器。</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>释放窗体资源。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>初始化设计器控件。</summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelRoot = new System.Windows.Forms.TableLayoutPanel();
            this.panelOptions = new System.Windows.Forms.Panel();
            this.checkBoxEnableCrop = new System.Windows.Forms.CheckBox();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.buttonClearRoi = new System.Windows.Forms.Button();
            this.labelRoiShape = new System.Windows.Forms.Label();
            this.comboBoxRoiShape = new System.Windows.Forms.ComboBox();
            this.checkBoxEnableTemplateMatch = new System.Windows.Forms.CheckBox();
            this.buttonConfigureTemplate = new System.Windows.Forms.Button();
            this.labelTemplateDefaults = new System.Windows.Forms.Label();
            this.imageRoiEditor = new TDJS_Vision.Forms.ShapeDraw.ImageROIEditControl();
            this.panelFooter = new System.Windows.Forms.Panel();
            this.labelRoiStatus = new System.Windows.Forms.Label();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOk = new System.Windows.Forms.Button();
            this.tableLayoutPanelRoot.SuspendLayout();
            this.panelOptions.SuspendLayout();
            this.panelFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelRoot
            // 
            this.tableLayoutPanelRoot.ColumnCount = 1;
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Controls.Add(this.panelOptions, 0, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.imageRoiEditor, 0, 1);
            this.tableLayoutPanelRoot.Controls.Add(this.panelFooter, 0, 2);
            this.tableLayoutPanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoot.RowCount = 3;
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 96F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            // 
            // panelOptions
            // 
            this.panelOptions.Controls.Add(this.checkBoxEnableCrop);
            this.panelOptions.Controls.Add(this.buttonDrawRoi);
            this.panelOptions.Controls.Add(this.buttonClearRoi);
            this.panelOptions.Controls.Add(this.labelRoiShape);
            this.panelOptions.Controls.Add(this.comboBoxRoiShape);
            this.panelOptions.Controls.Add(this.checkBoxEnableTemplateMatch);
            this.panelOptions.Controls.Add(this.buttonConfigureTemplate);
            this.panelOptions.Controls.Add(this.labelTemplateDefaults);
            this.panelOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            // 
            // checkBoxEnableCrop
            // 
            this.checkBoxEnableCrop.AutoSize = true;
            this.checkBoxEnableCrop.Location = new System.Drawing.Point(14, 14);
            this.checkBoxEnableCrop.Text = "启用截取ROI";
            this.checkBoxEnableCrop.CheckedChanged += new System.EventHandler(this.checkBoxEnableCrop_CheckedChanged);
            // 
            // buttonDrawRoi
            // 
            this.buttonDrawRoi.Location = new System.Drawing.Point(158, 8);
            this.buttonDrawRoi.Size = new System.Drawing.Size(108, 34);
            this.buttonDrawRoi.Text = "绘制ROI";
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            // 
            // buttonClearRoi
            // 
            this.buttonClearRoi.Location = new System.Drawing.Point(274, 8);
            this.buttonClearRoi.Size = new System.Drawing.Size(108, 34);
            this.buttonClearRoi.Text = "清空ROI";
            this.buttonClearRoi.Click += new System.EventHandler(this.buttonClearRoi_Click);
            //
            // labelRoiShape
            //
            this.labelRoiShape.AutoSize = true;
            this.labelRoiShape.Location = new System.Drawing.Point(394, 14);
            this.labelRoiShape.Text = "ROI形状";
            //
            // comboBoxRoiShape
            //
            this.comboBoxRoiShape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRoiShape.Items.AddRange(new object[] { "矩形", "圆形" });
            this.comboBoxRoiShape.Location = new System.Drawing.Point(474, 9);
            this.comboBoxRoiShape.Size = new System.Drawing.Size(112, 31);
            this.comboBoxRoiShape.SelectedIndex = 0;
            // 
            // checkBoxEnableTemplateMatch
            // 
            this.checkBoxEnableTemplateMatch.AutoSize = true;
            this.checkBoxEnableTemplateMatch.Location = new System.Drawing.Point(14, 57);
            this.checkBoxEnableTemplateMatch.Text = "启用模板匹配";
            this.checkBoxEnableTemplateMatch.CheckedChanged += new System.EventHandler(this.checkBoxEnableTemplateMatch_CheckedChanged);
            // 
            // buttonConfigureTemplate
            // 
            this.buttonConfigureTemplate.Location = new System.Drawing.Point(158, 51);
            this.buttonConfigureTemplate.Size = new System.Drawing.Size(108, 34);
            this.buttonConfigureTemplate.Text = "创建模板";
            this.buttonConfigureTemplate.Click += new System.EventHandler(this.buttonConfigureTemplate_Click);
            // 
            // labelTemplateDefaults
            // 
            this.labelTemplateDefaults.AutoEllipsis = true;
            this.labelTemplateDefaults.Location = new System.Drawing.Point(282, 55);
            this.labelTemplateDefaults.Size = new System.Drawing.Size(520, 30);
            this.labelTemplateDefaults.Text = "默认参数：数量1，角度-360°～360°，分数0.5";
            // 
            // imageRoiEditor
            // 
            this.imageRoiEditor.BackColor = System.Drawing.Color.Black;
            this.imageRoiEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            // 
            // panelFooter
            // 
            this.panelFooter.Controls.Add(this.labelRoiStatus);
            this.panelFooter.Controls.Add(this.buttonCancel);
            this.panelFooter.Controls.Add(this.buttonOk);
            this.panelFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            // 
            // labelRoiStatus
            // 
            this.labelRoiStatus.AutoEllipsis = true;
            this.labelRoiStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiStatus.Padding = new System.Windows.Forms.Padding(14, 15, 0, 0);
            this.labelRoiStatus.Text = "ROI：未绘制";
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(8, 10, 8, 10);
            this.buttonCancel.Size = new System.Drawing.Size(112, 58);
            this.buttonCancel.Text = "取消";
            // 
            // buttonOk
            // 
            this.buttonOk.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonOk.Margin = new System.Windows.Forms.Padding(8, 10, 8, 10);
            this.buttonOk.Size = new System.Drawing.Size(112, 58);
            this.buttonOk.Text = "确定";
            this.buttonOk.Click += new System.EventHandler(this.buttonOk_Click);
            // 
            // UnsupervisedRoiSettingsForm
            // 
            this.AcceptButton = this.buttonOk;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(914, 680);
            this.Controls.Add(this.tableLayoutPanelRoot);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.MinimumSize = new System.Drawing.Size(760, 560);
            this.Name = "UnsupervisedRoiSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ROI设置";
            this.tableLayoutPanelRoot.ResumeLayout(false);
            this.panelOptions.ResumeLayout(false);
            this.panelOptions.PerformLayout();
            this.panelFooter.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoot;
        private System.Windows.Forms.Panel panelOptions;
        private System.Windows.Forms.CheckBox checkBoxEnableCrop;
        private System.Windows.Forms.Button buttonDrawRoi;
        private System.Windows.Forms.Button buttonClearRoi;
        private System.Windows.Forms.Label labelRoiShape;
        private System.Windows.Forms.ComboBox comboBoxRoiShape;
        private System.Windows.Forms.CheckBox checkBoxEnableTemplateMatch;
        private System.Windows.Forms.Button buttonConfigureTemplate;
        private System.Windows.Forms.Label labelTemplateDefaults;
        private TDJS_Vision.Forms.ShapeDraw.ImageROIEditControl imageRoiEditor;
        private System.Windows.Forms.Panel panelFooter;
        private System.Windows.Forms.Label labelRoiStatus;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOk;
    }
}
