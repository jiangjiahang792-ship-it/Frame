namespace TDJS_Vision.Forms.AiTrainForm
{
    partial class UnsupervisedTrainForm
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
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageCheck = new System.Windows.Forms.TabPage();
            this.tableLayoutPanelCheckRoot = new System.Windows.Forms.TableLayoutPanel();
            this.panelCheckToolbar = new System.Windows.Forms.Panel();
            this.buttonReloadImages = new System.Windows.Forms.Button();
            this.buttonSelectImageFolder = new System.Windows.Forms.Button();
            this.textBoxImageFolder = new System.Windows.Forms.TextBox();
            this.labelImageFolder = new System.Windows.Forms.Label();
            this.tableLayoutPanelCheckBody = new System.Windows.Forms.TableLayoutPanel();
            this.panelCategory = new System.Windows.Forms.Panel();
            this.buttonFilterNg = new System.Windows.Forms.Button();
            this.buttonFilterOk = new System.Windows.Forms.Button();
            this.buttonFilterAll = new System.Windows.Forms.Button();
            this.labelCategoryTitle = new System.Windows.Forms.Label();
            this.flowLayoutPanelImages = new System.Windows.Forms.FlowLayoutPanel();
            this.tableLayoutPanelPreview = new System.Windows.Forms.TableLayoutPanel();
            this.panelRoiToolbar = new System.Windows.Forms.Panel();
            this.buttonClearRoi = new System.Windows.Forms.Button();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.labelPreviewTitle = new System.Windows.Forms.Label();
            this.imageROIEditControlPreview = new TDJS_Vision.Forms.ShapeDraw.ImageROIEditControl();
            this.panelCheckStatus = new System.Windows.Forms.Panel();
            this.labelRoiStatus = new System.Windows.Forms.Label();
            this.labelImageCount = new System.Windows.Forms.Label();
            this.tabPageTraining = new System.Windows.Forms.TabPage();
            this.tableLayoutPanelTrainingRoot = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxTrainingParams = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelTrainingParams = new System.Windows.Forms.TableLayoutPanel();
            this.labelModelType = new System.Windows.Forms.Label();
            this.comboBoxModelType = new System.Windows.Forms.ComboBox();
            this.labelThreshold = new System.Windows.Forms.Label();
            this.numericUpDownThreshold = new System.Windows.Forms.NumericUpDown();
            this.labelMiniArea = new System.Windows.Forms.Label();
            this.numericUpDownMiniArea = new System.Windows.Forms.NumericUpDown();
            this.labelInputWidth = new System.Windows.Forms.Label();
            this.numericUpDownInputWidth = new System.Windows.Forms.NumericUpDown();
            this.labelInputHeight = new System.Windows.Forms.Label();
            this.numericUpDownInputHeight = new System.Windows.Forms.NumericUpDown();
            this.labelEpochs = new System.Windows.Forms.Label();
            this.numericUpDownEpochs = new System.Windows.Forms.NumericUpDown();
            this.labelBatchSize = new System.Windows.Forms.Label();
            this.numericUpDownBatchSize = new System.Windows.Forms.NumericUpDown();
            this.labelDevice = new System.Windows.Forms.Label();
            this.comboBoxDevice = new System.Windows.Forms.ComboBox();
            this.groupBoxTrainingOutput = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelTrainingOutput = new System.Windows.Forms.TableLayoutPanel();
            this.labelTemplateName = new System.Windows.Forms.Label();
            this.textBoxTemplateName = new System.Windows.Forms.TextBox();
            this.labelModelOutputPath = new System.Windows.Forms.Label();
            this.textBoxModelOutputPath = new System.Windows.Forms.TextBox();
            this.buttonSelectModelOutput = new System.Windows.Forms.Button();
            this.labelRuntimeStatus = new System.Windows.Forms.Label();
            this.panelTrainingButtons = new System.Windows.Forms.Panel();
            this.buttonStopTraining = new System.Windows.Forms.Button();
            this.buttonStartTraining = new System.Windows.Forms.Button();
            this.progressBarTraining = new System.Windows.Forms.ProgressBar();
            this.textBoxTrainingLog = new System.Windows.Forms.TextBox();
            this.tabControlMain.SuspendLayout();
            this.tabPageCheck.SuspendLayout();
            this.tableLayoutPanelCheckRoot.SuspendLayout();
            this.panelCheckToolbar.SuspendLayout();
            this.tableLayoutPanelCheckBody.SuspendLayout();
            this.panelCategory.SuspendLayout();
            this.tableLayoutPanelPreview.SuspendLayout();
            this.panelRoiToolbar.SuspendLayout();
            this.panelCheckStatus.SuspendLayout();
            this.tabPageTraining.SuspendLayout();
            this.tableLayoutPanelTrainingRoot.SuspendLayout();
            this.groupBoxTrainingParams.SuspendLayout();
            this.tableLayoutPanelTrainingParams.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMiniArea)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownInputWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownInputHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownEpochs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownBatchSize)).BeginInit();
            this.groupBoxTrainingOutput.SuspendLayout();
            this.tableLayoutPanelTrainingOutput.SuspendLayout();
            this.panelTrainingButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tabControlMain
            //
            this.tabControlMain.Controls.Add(this.tabPageCheck);
            this.tabControlMain.Controls.Add(this.tabPageTraining);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(1184, 762);
            this.tabControlMain.TabIndex = 0;
            //
            // tabPageCheck
            //
            this.tabPageCheck.Controls.Add(this.tableLayoutPanelCheckRoot);
            this.tabPageCheck.Location = new System.Drawing.Point(4, 28);
            this.tabPageCheck.Name = "tabPageCheck";
            this.tabPageCheck.Padding = new System.Windows.Forms.Padding(8);
            this.tabPageCheck.Size = new System.Drawing.Size(1176, 730);
            this.tabPageCheck.TabIndex = 0;
            this.tabPageCheck.Text = "检查";
            this.tabPageCheck.UseVisualStyleBackColor = true;
            //
            // tableLayoutPanelCheckRoot
            //
            this.tableLayoutPanelCheckRoot.ColumnCount = 1;
            this.tableLayoutPanelCheckRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelCheckRoot.Controls.Add(this.panelCheckToolbar, 0, 0);
            this.tableLayoutPanelCheckRoot.Controls.Add(this.tableLayoutPanelCheckBody, 0, 1);
            this.tableLayoutPanelCheckRoot.Controls.Add(this.panelCheckStatus, 0, 2);
            this.tableLayoutPanelCheckRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelCheckRoot.Location = new System.Drawing.Point(8, 8);
            this.tableLayoutPanelCheckRoot.Name = "tableLayoutPanelCheckRoot";
            this.tableLayoutPanelCheckRoot.RowCount = 3;
            this.tableLayoutPanelCheckRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tableLayoutPanelCheckRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelCheckRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelCheckRoot.Size = new System.Drawing.Size(1160, 714);
            this.tableLayoutPanelCheckRoot.TabIndex = 0;
            //
            // panelCheckToolbar
            //
            this.panelCheckToolbar.Controls.Add(this.buttonReloadImages);
            this.panelCheckToolbar.Controls.Add(this.buttonSelectImageFolder);
            this.panelCheckToolbar.Controls.Add(this.textBoxImageFolder);
            this.panelCheckToolbar.Controls.Add(this.labelImageFolder);
            this.panelCheckToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCheckToolbar.Location = new System.Drawing.Point(3, 3);
            this.panelCheckToolbar.Name = "panelCheckToolbar";
            this.panelCheckToolbar.Size = new System.Drawing.Size(1154, 42);
            this.panelCheckToolbar.TabIndex = 0;
            //
            // buttonReloadImages
            //
            this.buttonReloadImages.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonReloadImages.Location = new System.Drawing.Point(1039, 5);
            this.buttonReloadImages.Name = "buttonReloadImages";
            this.buttonReloadImages.Size = new System.Drawing.Size(104, 32);
            this.buttonReloadImages.TabIndex = 3;
            this.buttonReloadImages.Text = "重新加载";
            this.buttonReloadImages.UseVisualStyleBackColor = true;
            this.buttonReloadImages.Click += new System.EventHandler(this.buttonReloadImages_Click);
            //
            // buttonSelectImageFolder
            //
            this.buttonSelectImageFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSelectImageFolder.Location = new System.Drawing.Point(929, 5);
            this.buttonSelectImageFolder.Name = "buttonSelectImageFolder";
            this.buttonSelectImageFolder.Size = new System.Drawing.Size(104, 32);
            this.buttonSelectImageFolder.TabIndex = 2;
            this.buttonSelectImageFolder.Text = "选择路径";
            this.buttonSelectImageFolder.UseVisualStyleBackColor = true;
            this.buttonSelectImageFolder.Click += new System.EventHandler(this.buttonSelectImageFolder_Click);
            //
            // textBoxImageFolder
            //
            this.textBoxImageFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxImageFolder.Location = new System.Drawing.Point(82, 8);
            this.textBoxImageFolder.Name = "textBoxImageFolder";
            this.textBoxImageFolder.Size = new System.Drawing.Size(841, 28);
            this.textBoxImageFolder.TabIndex = 1;
            //
            // labelImageFolder
            //
            this.labelImageFolder.AutoSize = true;
            this.labelImageFolder.Location = new System.Drawing.Point(8, 12);
            this.labelImageFolder.Name = "labelImageFolder";
            this.labelImageFolder.Size = new System.Drawing.Size(68, 18);
            this.labelImageFolder.TabIndex = 0;
            this.labelImageFolder.Text = "图像路径";
            //
            // tableLayoutPanelCheckBody
            //
            this.tableLayoutPanelCheckBody.ColumnCount = 3;
            this.tableLayoutPanelCheckBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 128F));
            this.tableLayoutPanelCheckBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 382F));
            this.tableLayoutPanelCheckBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelCheckBody.Controls.Add(this.panelCategory, 0, 0);
            this.tableLayoutPanelCheckBody.Controls.Add(this.flowLayoutPanelImages, 1, 0);
            this.tableLayoutPanelCheckBody.Controls.Add(this.tableLayoutPanelPreview, 2, 0);
            this.tableLayoutPanelCheckBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelCheckBody.Location = new System.Drawing.Point(3, 51);
            this.tableLayoutPanelCheckBody.Name = "tableLayoutPanelCheckBody";
            this.tableLayoutPanelCheckBody.RowCount = 1;
            this.tableLayoutPanelCheckBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelCheckBody.Size = new System.Drawing.Size(1154, 626);
            this.tableLayoutPanelCheckBody.TabIndex = 1;
            //
            // panelCategory
            //
            this.panelCategory.Controls.Add(this.buttonFilterNg);
            this.panelCategory.Controls.Add(this.buttonFilterOk);
            this.panelCategory.Controls.Add(this.buttonFilterAll);
            this.panelCategory.Controls.Add(this.labelCategoryTitle);
            this.panelCategory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCategory.Location = new System.Drawing.Point(3, 3);
            this.panelCategory.Name = "panelCategory";
            this.panelCategory.Size = new System.Drawing.Size(122, 620);
            this.panelCategory.TabIndex = 0;
            //
            // buttonFilterNg
            //
            this.buttonFilterNg.Location = new System.Drawing.Point(10, 120);
            this.buttonFilterNg.Name = "buttonFilterNg";
            this.buttonFilterNg.Size = new System.Drawing.Size(102, 34);
            this.buttonFilterNg.TabIndex = 3;
            this.buttonFilterNg.Text = "NG 0";
            this.buttonFilterNg.UseVisualStyleBackColor = true;
            this.buttonFilterNg.Click += new System.EventHandler(this.buttonFilterNg_Click);
            //
            // buttonFilterOk
            //
            this.buttonFilterOk.Location = new System.Drawing.Point(10, 80);
            this.buttonFilterOk.Name = "buttonFilterOk";
            this.buttonFilterOk.Size = new System.Drawing.Size(102, 34);
            this.buttonFilterOk.TabIndex = 2;
            this.buttonFilterOk.Text = "OK 0";
            this.buttonFilterOk.UseVisualStyleBackColor = true;
            this.buttonFilterOk.Click += new System.EventHandler(this.buttonFilterOk_Click);
            //
            // buttonFilterAll
            //
            this.buttonFilterAll.Location = new System.Drawing.Point(10, 40);
            this.buttonFilterAll.Name = "buttonFilterAll";
            this.buttonFilterAll.Size = new System.Drawing.Size(102, 34);
            this.buttonFilterAll.TabIndex = 1;
            this.buttonFilterAll.Text = "All 0";
            this.buttonFilterAll.UseVisualStyleBackColor = true;
            this.buttonFilterAll.Click += new System.EventHandler(this.buttonFilterAll_Click);
            //
            // labelCategoryTitle
            //
            this.labelCategoryTitle.AutoSize = true;
            this.labelCategoryTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelCategoryTitle.Location = new System.Drawing.Point(12, 12);
            this.labelCategoryTitle.Name = "labelCategoryTitle";
            this.labelCategoryTitle.Size = new System.Drawing.Size(69, 19);
            this.labelCategoryTitle.TabIndex = 0;
            this.labelCategoryTitle.Text = "图像分类";
            //
            // flowLayoutPanelImages
            //
            this.flowLayoutPanelImages.AutoScroll = true;
            this.flowLayoutPanelImages.BackColor = System.Drawing.Color.White;
            this.flowLayoutPanelImages.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.flowLayoutPanelImages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelImages.Location = new System.Drawing.Point(131, 3);
            this.flowLayoutPanelImages.Name = "flowLayoutPanelImages";
            this.flowLayoutPanelImages.Padding = new System.Windows.Forms.Padding(8);
            this.flowLayoutPanelImages.Size = new System.Drawing.Size(376, 620);
            this.flowLayoutPanelImages.TabIndex = 1;
            //
            // tableLayoutPanelPreview
            //
            this.tableLayoutPanelPreview.ColumnCount = 1;
            this.tableLayoutPanelPreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPreview.Controls.Add(this.panelRoiToolbar, 0, 0);
            this.tableLayoutPanelPreview.Controls.Add(this.imageROIEditControlPreview, 0, 1);
            this.tableLayoutPanelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelPreview.Location = new System.Drawing.Point(513, 3);
            this.tableLayoutPanelPreview.Name = "tableLayoutPanelPreview";
            this.tableLayoutPanelPreview.RowCount = 2;
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPreview.Size = new System.Drawing.Size(638, 620);
            this.tableLayoutPanelPreview.TabIndex = 2;
            //
            // panelRoiToolbar
            //
            this.panelRoiToolbar.Controls.Add(this.buttonClearRoi);
            this.panelRoiToolbar.Controls.Add(this.buttonDrawRoi);
            this.panelRoiToolbar.Controls.Add(this.labelPreviewTitle);
            this.panelRoiToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRoiToolbar.Location = new System.Drawing.Point(3, 3);
            this.panelRoiToolbar.Name = "panelRoiToolbar";
            this.panelRoiToolbar.Size = new System.Drawing.Size(632, 36);
            this.panelRoiToolbar.TabIndex = 0;
            //
            // buttonClearRoi
            //
            this.buttonClearRoi.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonClearRoi.Location = new System.Drawing.Point(526, 2);
            this.buttonClearRoi.Name = "buttonClearRoi";
            this.buttonClearRoi.Size = new System.Drawing.Size(100, 32);
            this.buttonClearRoi.TabIndex = 2;
            this.buttonClearRoi.Text = "清空ROI";
            this.buttonClearRoi.UseVisualStyleBackColor = true;
            this.buttonClearRoi.Click += new System.EventHandler(this.buttonClearRoi_Click);
            //
            // buttonDrawRoi
            //
            this.buttonDrawRoi.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDrawRoi.Location = new System.Drawing.Point(420, 2);
            this.buttonDrawRoi.Name = "buttonDrawRoi";
            this.buttonDrawRoi.Size = new System.Drawing.Size(100, 32);
            this.buttonDrawRoi.TabIndex = 1;
            this.buttonDrawRoi.Text = "绘制ROI";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            //
            // labelPreviewTitle
            //
            this.labelPreviewTitle.AutoSize = true;
            this.labelPreviewTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelPreviewTitle.Location = new System.Drawing.Point(3, 8);
            this.labelPreviewTitle.Name = "labelPreviewTitle";
            this.labelPreviewTitle.Size = new System.Drawing.Size(140, 19);
            this.labelPreviewTitle.TabIndex = 0;
            this.labelPreviewTitle.Text = "图像预览 / ROI编辑";
            //
            // imageROIEditControlPreview
            //
            this.imageROIEditControlPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageROIEditControlPreview.Location = new System.Drawing.Point(3, 45);
            this.imageROIEditControlPreview.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.imageROIEditControlPreview.Name = "imageROIEditControlPreview";
            this.imageROIEditControlPreview.Size = new System.Drawing.Size(632, 572);
            this.imageROIEditControlPreview.TabIndex = 1;
            //
            // panelCheckStatus
            //
            this.panelCheckStatus.Controls.Add(this.labelRoiStatus);
            this.panelCheckStatus.Controls.Add(this.labelImageCount);
            this.panelCheckStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCheckStatus.Location = new System.Drawing.Point(3, 683);
            this.panelCheckStatus.Name = "panelCheckStatus";
            this.panelCheckStatus.Size = new System.Drawing.Size(1154, 28);
            this.panelCheckStatus.TabIndex = 2;
            //
            // labelRoiStatus
            //
            this.labelRoiStatus.AutoSize = true;
            this.labelRoiStatus.Location = new System.Drawing.Point(310, 5);
            this.labelRoiStatus.Name = "labelRoiStatus";
            this.labelRoiStatus.Size = new System.Drawing.Size(101, 18);
            this.labelRoiStatus.TabIndex = 1;
            this.labelRoiStatus.Text = "ROI：未使用";
            //
            // labelImageCount
            //
            this.labelImageCount.AutoSize = true;
            this.labelImageCount.Location = new System.Drawing.Point(8, 5);
            this.labelImageCount.Name = "labelImageCount";
            this.labelImageCount.Size = new System.Drawing.Size(207, 18);
            this.labelImageCount.TabIndex = 0;
            this.labelImageCount.Text = "图像总数：0    OK：0    NG：0";
            //
            // tabPageTraining
            //
            this.tabPageTraining.Controls.Add(this.tableLayoutPanelTrainingRoot);
            this.tabPageTraining.Location = new System.Drawing.Point(4, 28);
            this.tabPageTraining.Name = "tabPageTraining";
            this.tabPageTraining.Padding = new System.Windows.Forms.Padding(10);
            this.tabPageTraining.Size = new System.Drawing.Size(1176, 730);
            this.tabPageTraining.TabIndex = 1;
            this.tabPageTraining.Text = "训练";
            this.tabPageTraining.UseVisualStyleBackColor = true;
            //
            // tableLayoutPanelTrainingRoot
            //
            this.tableLayoutPanelTrainingRoot.ColumnCount = 2;
            this.tableLayoutPanelTrainingRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 430F));
            this.tableLayoutPanelTrainingRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrainingRoot.Controls.Add(this.groupBoxTrainingParams, 0, 0);
            this.tableLayoutPanelTrainingRoot.Controls.Add(this.groupBoxTrainingOutput, 1, 0);
            this.tableLayoutPanelTrainingRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTrainingRoot.Location = new System.Drawing.Point(10, 10);
            this.tableLayoutPanelTrainingRoot.Name = "tableLayoutPanelTrainingRoot";
            this.tableLayoutPanelTrainingRoot.RowCount = 1;
            this.tableLayoutPanelTrainingRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrainingRoot.Size = new System.Drawing.Size(1156, 710);
            this.tableLayoutPanelTrainingRoot.TabIndex = 0;
            //
            // groupBoxTrainingParams
            //
            this.groupBoxTrainingParams.Controls.Add(this.tableLayoutPanelTrainingParams);
            this.groupBoxTrainingParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxTrainingParams.Location = new System.Drawing.Point(3, 3);
            this.groupBoxTrainingParams.Name = "groupBoxTrainingParams";
            this.groupBoxTrainingParams.Padding = new System.Windows.Forms.Padding(10);
            this.groupBoxTrainingParams.Size = new System.Drawing.Size(424, 704);
            this.groupBoxTrainingParams.TabIndex = 0;
            this.groupBoxTrainingParams.TabStop = false;
            this.groupBoxTrainingParams.Text = "训练参数";
            //
            // tableLayoutPanelTrainingParams
            //
            this.tableLayoutPanelTrainingParams.ColumnCount = 2;
            this.tableLayoutPanelTrainingParams.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelTrainingParams.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelModelType, 0, 0);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.comboBoxModelType, 1, 0);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelThreshold, 0, 1);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.numericUpDownThreshold, 1, 1);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelMiniArea, 0, 2);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.numericUpDownMiniArea, 1, 2);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelInputWidth, 0, 3);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.numericUpDownInputWidth, 1, 3);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelInputHeight, 0, 4);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.numericUpDownInputHeight, 1, 4);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelEpochs, 0, 5);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.numericUpDownEpochs, 1, 5);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelBatchSize, 0, 6);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.numericUpDownBatchSize, 1, 6);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.labelDevice, 0, 7);
            this.tableLayoutPanelTrainingParams.Controls.Add(this.comboBoxDevice, 1, 7);
            this.tableLayoutPanelTrainingParams.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelTrainingParams.Location = new System.Drawing.Point(10, 31);
            this.tableLayoutPanelTrainingParams.Name = "tableLayoutPanelTrainingParams";
            this.tableLayoutPanelTrainingParams.RowCount = 9;
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrainingParams.Size = new System.Drawing.Size(404, 360);
            this.tableLayoutPanelTrainingParams.TabIndex = 0;
            //
            // labelModelType
            //
            this.labelModelType.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelModelType.AutoSize = true;
            this.labelModelType.Location = new System.Drawing.Point(3, 12);
            this.labelModelType.Name = "labelModelType";
            this.labelModelType.Size = new System.Drawing.Size(68, 18);
            this.labelModelType.TabIndex = 0;
            this.labelModelType.Text = "模型类型";
            //
            // comboBoxModelType
            //
            this.comboBoxModelType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxModelType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxModelType.FormattingEnabled = true;
            this.comboBoxModelType.Items.AddRange(new object[] {
            "patchcore",
            "padim",
            "stfpm",
            "fastflow",
            "cflow",
            "efficient_ad"});
            this.comboBoxModelType.Location = new System.Drawing.Point(123, 8);
            this.comboBoxModelType.Name = "comboBoxModelType";
            this.comboBoxModelType.Size = new System.Drawing.Size(278, 26);
            this.comboBoxModelType.TabIndex = 1;
            //
            // labelThreshold
            //
            this.labelThreshold.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelThreshold.AutoSize = true;
            this.labelThreshold.Location = new System.Drawing.Point(3, 54);
            this.labelThreshold.Name = "labelThreshold";
            this.labelThreshold.Size = new System.Drawing.Size(38, 18);
            this.labelThreshold.TabIndex = 2;
            this.labelThreshold.Text = "阈值";
            //
            // numericUpDownThreshold
            //
            this.numericUpDownThreshold.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownThreshold.DecimalPlaces = 2;
            this.numericUpDownThreshold.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.numericUpDownThreshold.Location = new System.Drawing.Point(123, 49);
            this.numericUpDownThreshold.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownThreshold.Name = "numericUpDownThreshold";
            this.numericUpDownThreshold.Size = new System.Drawing.Size(160, 28);
            this.numericUpDownThreshold.TabIndex = 3;
            this.numericUpDownThreshold.Value = new decimal(new int[] {
            30,
            0,
            0,
            131072});
            //
            // labelMiniArea
            //
            this.labelMiniArea.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelMiniArea.AutoSize = true;
            this.labelMiniArea.Location = new System.Drawing.Point(3, 96);
            this.labelMiniArea.Name = "labelMiniArea";
            this.labelMiniArea.Size = new System.Drawing.Size(68, 18);
            this.labelMiniArea.TabIndex = 4;
            this.labelMiniArea.Text = "最小面积";
            //
            // numericUpDownMiniArea
            //
            this.numericUpDownMiniArea.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownMiniArea.Location = new System.Drawing.Point(123, 91);
            this.numericUpDownMiniArea.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.numericUpDownMiniArea.Name = "numericUpDownMiniArea";
            this.numericUpDownMiniArea.Size = new System.Drawing.Size(160, 28);
            this.numericUpDownMiniArea.TabIndex = 5;
            this.numericUpDownMiniArea.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            //
            // labelInputWidth
            //
            this.labelInputWidth.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelInputWidth.AutoSize = true;
            this.labelInputWidth.Location = new System.Drawing.Point(3, 138);
            this.labelInputWidth.Name = "labelInputWidth";
            this.labelInputWidth.Size = new System.Drawing.Size(68, 18);
            this.labelInputWidth.TabIndex = 6;
            this.labelInputWidth.Text = "输入宽度";
            //
            // numericUpDownInputWidth
            //
            this.numericUpDownInputWidth.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownInputWidth.Location = new System.Drawing.Point(123, 133);
            this.numericUpDownInputWidth.Maximum = new decimal(new int[] {
            8192,
            0,
            0,
            0});
            this.numericUpDownInputWidth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownInputWidth.Name = "numericUpDownInputWidth";
            this.numericUpDownInputWidth.Size = new System.Drawing.Size(160, 28);
            this.numericUpDownInputWidth.TabIndex = 7;
            this.numericUpDownInputWidth.Value = new decimal(new int[] {
            512,
            0,
            0,
            0});
            //
            // labelInputHeight
            //
            this.labelInputHeight.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelInputHeight.AutoSize = true;
            this.labelInputHeight.Location = new System.Drawing.Point(3, 180);
            this.labelInputHeight.Name = "labelInputHeight";
            this.labelInputHeight.Size = new System.Drawing.Size(68, 18);
            this.labelInputHeight.TabIndex = 8;
            this.labelInputHeight.Text = "输入高度";
            //
            // numericUpDownInputHeight
            //
            this.numericUpDownInputHeight.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownInputHeight.Location = new System.Drawing.Point(123, 175);
            this.numericUpDownInputHeight.Maximum = new decimal(new int[] {
            8192,
            0,
            0,
            0});
            this.numericUpDownInputHeight.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownInputHeight.Name = "numericUpDownInputHeight";
            this.numericUpDownInputHeight.Size = new System.Drawing.Size(160, 28);
            this.numericUpDownInputHeight.TabIndex = 9;
            this.numericUpDownInputHeight.Value = new decimal(new int[] {
            256,
            0,
            0,
            0});
            //
            // labelEpochs
            //
            this.labelEpochs.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelEpochs.AutoSize = true;
            this.labelEpochs.Location = new System.Drawing.Point(3, 222);
            this.labelEpochs.Name = "labelEpochs";
            this.labelEpochs.Size = new System.Drawing.Size(68, 18);
            this.labelEpochs.TabIndex = 10;
            this.labelEpochs.Text = "训练轮数";
            //
            // numericUpDownEpochs
            //
            this.numericUpDownEpochs.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownEpochs.Location = new System.Drawing.Point(123, 217);
            this.numericUpDownEpochs.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericUpDownEpochs.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownEpochs.Name = "numericUpDownEpochs";
            this.numericUpDownEpochs.Size = new System.Drawing.Size(160, 28);
            this.numericUpDownEpochs.TabIndex = 11;
            this.numericUpDownEpochs.Value = new decimal(new int[] {
            50,
            0,
            0,
            0});
            //
            // labelBatchSize
            //
            this.labelBatchSize.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelBatchSize.AutoSize = true;
            this.labelBatchSize.Location = new System.Drawing.Point(3, 264);
            this.labelBatchSize.Name = "labelBatchSize";
            this.labelBatchSize.Size = new System.Drawing.Size(71, 18);
            this.labelBatchSize.TabIndex = 12;
            this.labelBatchSize.Text = "BatchSize";
            //
            // numericUpDownBatchSize
            //
            this.numericUpDownBatchSize.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownBatchSize.Location = new System.Drawing.Point(123, 259);
            this.numericUpDownBatchSize.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
            this.numericUpDownBatchSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownBatchSize.Name = "numericUpDownBatchSize";
            this.numericUpDownBatchSize.Size = new System.Drawing.Size(160, 28);
            this.numericUpDownBatchSize.TabIndex = 13;
            this.numericUpDownBatchSize.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            //
            // labelDevice
            //
            this.labelDevice.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelDevice.AutoSize = true;
            this.labelDevice.Location = new System.Drawing.Point(3, 306);
            this.labelDevice.Name = "labelDevice";
            this.labelDevice.Size = new System.Drawing.Size(68, 18);
            this.labelDevice.TabIndex = 14;
            this.labelDevice.Text = "运行设备";
            //
            // comboBoxDevice
            //
            this.comboBoxDevice.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDevice.FormattingEnabled = true;
            this.comboBoxDevice.Items.AddRange(new object[] {
            "CPU",
            "GPU",
            "AUTO"});
            this.comboBoxDevice.Location = new System.Drawing.Point(123, 302);
            this.comboBoxDevice.Name = "comboBoxDevice";
            this.comboBoxDevice.Size = new System.Drawing.Size(278, 26);
            this.comboBoxDevice.TabIndex = 15;
            //
            // groupBoxTrainingOutput
            //
            this.groupBoxTrainingOutput.Controls.Add(this.tableLayoutPanelTrainingOutput);
            this.groupBoxTrainingOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxTrainingOutput.Location = new System.Drawing.Point(433, 3);
            this.groupBoxTrainingOutput.Name = "groupBoxTrainingOutput";
            this.groupBoxTrainingOutput.Padding = new System.Windows.Forms.Padding(10);
            this.groupBoxTrainingOutput.Size = new System.Drawing.Size(720, 704);
            this.groupBoxTrainingOutput.TabIndex = 1;
            this.groupBoxTrainingOutput.TabStop = false;
            this.groupBoxTrainingOutput.Text = "输出与进度";
            //
            // tableLayoutPanelTrainingOutput
            //
            this.tableLayoutPanelTrainingOutput.ColumnCount = 3;
            this.tableLayoutPanelTrainingOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 104F));
            this.tableLayoutPanelTrainingOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrainingOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.labelTemplateName, 0, 0);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.textBoxTemplateName, 1, 0);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.labelModelOutputPath, 0, 1);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.textBoxModelOutputPath, 1, 1);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.buttonSelectModelOutput, 2, 1);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.labelRuntimeStatus, 0, 2);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.panelTrainingButtons, 0, 3);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.progressBarTraining, 0, 4);
            this.tableLayoutPanelTrainingOutput.Controls.Add(this.textBoxTrainingLog, 0, 5);
            this.tableLayoutPanelTrainingOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTrainingOutput.Location = new System.Drawing.Point(10, 31);
            this.tableLayoutPanelTrainingOutput.Name = "tableLayoutPanelTrainingOutput";
            this.tableLayoutPanelTrainingOutput.RowCount = 6;
            this.tableLayoutPanelTrainingOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelTrainingOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tableLayoutPanelTrainingOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tableLayoutPanelTrainingOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableLayoutPanelTrainingOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrainingOutput.Size = new System.Drawing.Size(700, 663);
            this.tableLayoutPanelTrainingOutput.TabIndex = 0;
            //
            // labelTemplateName
            //
            this.labelTemplateName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelTemplateName.AutoSize = true;
            this.labelTemplateName.Location = new System.Drawing.Point(3, 12);
            this.labelTemplateName.Name = "labelTemplateName";
            this.labelTemplateName.Size = new System.Drawing.Size(68, 18);
            this.labelTemplateName.TabIndex = 0;
            this.labelTemplateName.Text = "模板名称";
            //
            // textBoxTemplateName
            //
            this.textBoxTemplateName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelTrainingOutput.SetColumnSpan(this.textBoxTemplateName, 2);
            this.textBoxTemplateName.Location = new System.Drawing.Point(107, 7);
            this.textBoxTemplateName.Name = "textBoxTemplateName";
            this.textBoxTemplateName.Size = new System.Drawing.Size(590, 28);
            this.textBoxTemplateName.TabIndex = 1;
            this.textBoxTemplateName.TextChanged += new System.EventHandler(this.textBoxTemplateName_TextChanged);
            //
            // labelModelOutputPath
            //
            this.labelModelOutputPath.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelModelOutputPath.AutoSize = true;
            this.labelModelOutputPath.Location = new System.Drawing.Point(3, 54);
            this.labelModelOutputPath.Name = "labelModelOutputPath";
            this.labelModelOutputPath.Size = new System.Drawing.Size(68, 18);
            this.labelModelOutputPath.TabIndex = 2;
            this.labelModelOutputPath.Text = "模型路径";
            //
            // textBoxModelOutputPath
            //
            this.textBoxModelOutputPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxModelOutputPath.Location = new System.Drawing.Point(107, 49);
            this.textBoxModelOutputPath.Name = "textBoxModelOutputPath";
            this.textBoxModelOutputPath.ReadOnly = true;
            this.textBoxModelOutputPath.Size = new System.Drawing.Size(474, 28);
            this.textBoxModelOutputPath.TabIndex = 3;
            //
            // buttonSelectModelOutput
            //
            this.buttonSelectModelOutput.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSelectModelOutput.Location = new System.Drawing.Point(590, 47);
            this.buttonSelectModelOutput.Name = "buttonSelectModelOutput";
            this.buttonSelectModelOutput.Size = new System.Drawing.Size(104, 32);
            this.buttonSelectModelOutput.TabIndex = 4;
            this.buttonSelectModelOutput.Text = "选择路径";
            this.buttonSelectModelOutput.UseVisualStyleBackColor = true;
            this.buttonSelectModelOutput.Click += new System.EventHandler(this.buttonSelectModelOutput_Click);
            //
            // labelRuntimeStatus
            //
            this.labelRuntimeStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelRuntimeStatus.AutoSize = true;
            this.tableLayoutPanelTrainingOutput.SetColumnSpan(this.labelRuntimeStatus, 3);
            this.labelRuntimeStatus.Location = new System.Drawing.Point(3, 97);
            this.labelRuntimeStatus.Name = "labelRuntimeStatus";
            this.labelRuntimeStatus.Size = new System.Drawing.Size(128, 18);
            this.labelRuntimeStatus.TabIndex = 5;
            this.labelRuntimeStatus.Text = "无监督环境未检测";
            //
            // panelTrainingButtons
            //
            this.tableLayoutPanelTrainingOutput.SetColumnSpan(this.panelTrainingButtons, 3);
            this.panelTrainingButtons.Controls.Add(this.buttonStopTraining);
            this.panelTrainingButtons.Controls.Add(this.buttonStartTraining);
            this.panelTrainingButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTrainingButtons.Location = new System.Drawing.Point(3, 131);
            this.panelTrainingButtons.Name = "panelTrainingButtons";
            this.panelTrainingButtons.Size = new System.Drawing.Size(694, 42);
            this.panelTrainingButtons.TabIndex = 6;
            //
            // buttonStopTraining
            //
            this.buttonStopTraining.Location = new System.Drawing.Point(128, 5);
            this.buttonStopTraining.Name = "buttonStopTraining";
            this.buttonStopTraining.Size = new System.Drawing.Size(110, 32);
            this.buttonStopTraining.TabIndex = 1;
            this.buttonStopTraining.Text = "停止训练";
            this.buttonStopTraining.UseVisualStyleBackColor = true;
            this.buttonStopTraining.Click += new System.EventHandler(this.buttonStopTraining_Click);
            //
            // buttonStartTraining
            //
            this.buttonStartTraining.Location = new System.Drawing.Point(8, 5);
            this.buttonStartTraining.Name = "buttonStartTraining";
            this.buttonStartTraining.Size = new System.Drawing.Size(110, 32);
            this.buttonStartTraining.TabIndex = 0;
            this.buttonStartTraining.Text = "开始训练";
            this.buttonStartTraining.UseVisualStyleBackColor = true;
            this.buttonStartTraining.Click += new System.EventHandler(this.buttonStartTraining_Click);
            //
            // progressBarTraining
            //
            this.tableLayoutPanelTrainingOutput.SetColumnSpan(this.progressBarTraining, 3);
            this.progressBarTraining.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressBarTraining.Location = new System.Drawing.Point(3, 179);
            this.progressBarTraining.Name = "progressBarTraining";
            this.progressBarTraining.Size = new System.Drawing.Size(694, 30);
            this.progressBarTraining.TabIndex = 7;
            //
            // textBoxTrainingLog
            //
            this.tableLayoutPanelTrainingOutput.SetColumnSpan(this.textBoxTrainingLog, 3);
            this.textBoxTrainingLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxTrainingLog.Location = new System.Drawing.Point(3, 215);
            this.textBoxTrainingLog.Multiline = true;
            this.textBoxTrainingLog.Name = "textBoxTrainingLog";
            this.textBoxTrainingLog.ReadOnly = true;
            this.textBoxTrainingLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxTrainingLog.Size = new System.Drawing.Size(694, 445);
            this.textBoxTrainingLog.TabIndex = 8;
            //
            // UnsupervisedTrainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 762);
            this.Controls.Add(this.tabControlMain);
            this.MinimumSize = new System.Drawing.Size(1120, 680);
            this.Name = "UnsupervisedTrainForm";
            this.Text = "无监督训练界面";
            this.tabControlMain.ResumeLayout(false);
            this.tabPageCheck.ResumeLayout(false);
            this.tableLayoutPanelCheckRoot.ResumeLayout(false);
            this.panelCheckToolbar.ResumeLayout(false);
            this.panelCheckToolbar.PerformLayout();
            this.tableLayoutPanelCheckBody.ResumeLayout(false);
            this.panelCategory.ResumeLayout(false);
            this.panelCategory.PerformLayout();
            this.tableLayoutPanelPreview.ResumeLayout(false);
            this.panelRoiToolbar.ResumeLayout(false);
            this.panelRoiToolbar.PerformLayout();
            this.panelCheckStatus.ResumeLayout(false);
            this.panelCheckStatus.PerformLayout();
            this.tabPageTraining.ResumeLayout(false);
            this.tableLayoutPanelTrainingRoot.ResumeLayout(false);
            this.groupBoxTrainingParams.ResumeLayout(false);
            this.tableLayoutPanelTrainingParams.ResumeLayout(false);
            this.tableLayoutPanelTrainingParams.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMiniArea)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownInputWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownInputHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownEpochs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownBatchSize)).EndInit();
            this.groupBoxTrainingOutput.ResumeLayout(false);
            this.tableLayoutPanelTrainingOutput.ResumeLayout(false);
            this.tableLayoutPanelTrainingOutput.PerformLayout();
            this.panelTrainingButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageCheck;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelCheckRoot;
        private System.Windows.Forms.Panel panelCheckToolbar;
        private System.Windows.Forms.Button buttonReloadImages;
        private System.Windows.Forms.Button buttonSelectImageFolder;
        private System.Windows.Forms.TextBox textBoxImageFolder;
        private System.Windows.Forms.Label labelImageFolder;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelCheckBody;
        private System.Windows.Forms.Panel panelCategory;
        private System.Windows.Forms.Button buttonFilterNg;
        private System.Windows.Forms.Button buttonFilterOk;
        private System.Windows.Forms.Button buttonFilterAll;
        private System.Windows.Forms.Label labelCategoryTitle;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelImages;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelPreview;
        private System.Windows.Forms.Panel panelRoiToolbar;
        private System.Windows.Forms.Button buttonClearRoi;
        private System.Windows.Forms.Button buttonDrawRoi;
        private System.Windows.Forms.Label labelPreviewTitle;
        private TDJS_Vision.Forms.ShapeDraw.ImageROIEditControl imageROIEditControlPreview;
        private System.Windows.Forms.Panel panelCheckStatus;
        private System.Windows.Forms.Label labelRoiStatus;
        private System.Windows.Forms.Label labelImageCount;
        private System.Windows.Forms.TabPage tabPageTraining;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTrainingRoot;
        private System.Windows.Forms.GroupBox groupBoxTrainingParams;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTrainingParams;
        private System.Windows.Forms.Label labelModelType;
        private System.Windows.Forms.ComboBox comboBoxModelType;
        private System.Windows.Forms.Label labelThreshold;
        private System.Windows.Forms.NumericUpDown numericUpDownThreshold;
        private System.Windows.Forms.Label labelMiniArea;
        private System.Windows.Forms.NumericUpDown numericUpDownMiniArea;
        private System.Windows.Forms.Label labelInputWidth;
        private System.Windows.Forms.NumericUpDown numericUpDownInputWidth;
        private System.Windows.Forms.Label labelInputHeight;
        private System.Windows.Forms.NumericUpDown numericUpDownInputHeight;
        private System.Windows.Forms.Label labelEpochs;
        private System.Windows.Forms.NumericUpDown numericUpDownEpochs;
        private System.Windows.Forms.Label labelBatchSize;
        private System.Windows.Forms.NumericUpDown numericUpDownBatchSize;
        private System.Windows.Forms.Label labelDevice;
        private System.Windows.Forms.ComboBox comboBoxDevice;
        private System.Windows.Forms.GroupBox groupBoxTrainingOutput;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTrainingOutput;
        private System.Windows.Forms.Label labelTemplateName;
        private System.Windows.Forms.TextBox textBoxTemplateName;
        private System.Windows.Forms.Label labelModelOutputPath;
        private System.Windows.Forms.TextBox textBoxModelOutputPath;
        private System.Windows.Forms.Button buttonSelectModelOutput;
        private System.Windows.Forms.Label labelRuntimeStatus;
        private System.Windows.Forms.Panel panelTrainingButtons;
        private System.Windows.Forms.Button buttonStopTraining;
        private System.Windows.Forms.Button buttonStartTraining;
        private System.Windows.Forms.ProgressBar progressBarTraining;
        private System.Windows.Forms.TextBox textBoxTrainingLog;
    }
}
