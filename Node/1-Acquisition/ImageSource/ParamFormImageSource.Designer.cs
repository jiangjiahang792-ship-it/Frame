namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    partial class ParamFormImageSource
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParamFormImageSource));
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelImgSource = new System.Windows.Forms.Label();
            this.comboBoxImgSource = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanelChoiceImage = new System.Windows.Forms.TableLayoutPanel();
            this.checkBoxAuto = new System.Windows.Forms.CheckBox();
            this.textBoxImgPath = new System.Windows.Forms.TextBox();
            this.buttonChoiceImageCatalog = new System.Windows.Forms.Button();
            this.buttonChoiceImg = new System.Windows.Forms.Button();
            this.tableLayoutPanelCamera = new System.Windows.Forms.TableLayoutPanel();
            this.labelChoiceCamera = new System.Windows.Forms.Label();
            this.comboBoxChoiceCamera = new System.Windows.Forms.ComboBox();
            this.labelStrobe = new System.Windows.Forms.Label();
            this.comboBoxStrobe = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.numericUpDownTimeOut = new System.Windows.Forms.NumericUpDown();
            this.labelGain = new System.Windows.Forms.Label();
            this.numericUpDownGain = new System.Windows.Forms.NumericUpDown();
            this.labelExposureTime = new System.Windows.Forms.Label();
            this.numericUpDownExposureTime = new System.Windows.Forms.NumericUpDown();
            this.numericUpDownTriggerDelay = new System.Windows.Forms.NumericUpDown();
            this.labelTriggerDelay = new System.Windows.Forms.Label();
            this.labelHardTrigger = new System.Windows.Forms.Label();
            this.comboBoxTriggerEdge = new System.Windows.Forms.ComboBox();
            this.labelTriggerModel = new System.Windows.Forms.Label();
            this.comboBoxTriggerModel = new System.Windows.Forms.ComboBox();
            this.labelTriggerMode = new System.Windows.Forms.Label();
            this.comboBoxTriggerMode = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanelSharedVariable = new System.Windows.Forms.TableLayoutPanel();
            this.labelSharedVariable = new System.Windows.Forms.Label();
            this.comboBoxSharedVariable = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanelSave = new System.Windows.Forms.TableLayoutPanel();
            this.button1 = new System.Windows.Forms.Button();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.flowLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanelChoiceImage.SuspendLayout();
            this.tableLayoutPanelCamera.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimeOut)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownGain)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownExposureTime)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTriggerDelay)).BeginInit();
            this.tableLayoutPanelSharedVariable.SuspendLayout();
            this.tableLayoutPanelSave.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoScroll = true;
            this.flowLayoutPanel1.Controls.Add(this.tableLayoutPanel1);
            this.flowLayoutPanel1.Controls.Add(this.tableLayoutPanelChoiceImage);
            this.flowLayoutPanel1.Controls.Add(this.tableLayoutPanelCamera);
            this.flowLayoutPanel1.Controls.Add(this.tableLayoutPanelSharedVariable);
            this.flowLayoutPanel1.Controls.Add(this.tableLayoutPanelSave);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(2, 38);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(678, 960);
            this.flowLayoutPanel1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 49.89107F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.10893F));
            this.tableLayoutPanel1.Controls.Add(this.labelImgSource, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxImgSource, 1, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 4);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(674, 61);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // labelImgSource
            // 
            this.labelImgSource.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelImgSource.AutoSize = true;
            this.labelImgSource.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelImgSource.Location = new System.Drawing.Point(131, 20);
            this.labelImgSource.Name = "labelImgSource";
            this.labelImgSource.Size = new System.Drawing.Size(73, 21);
            this.labelImgSource.TabIndex = 0;
            this.labelImgSource.Text = "图像源";
            // 
            // comboBoxImgSource
            // 
            this.comboBoxImgSource.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxImgSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxImgSource.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxImgSource.FormattingEnabled = true;
            this.comboBoxImgSource.Items.AddRange(new object[] {
            "本地图像",
            "相机",
            "共享变量"});
            this.comboBoxImgSource.Location = new System.Drawing.Point(399, 16);
            this.comboBoxImgSource.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxImgSource.Name = "comboBoxImgSource";
            this.comboBoxImgSource.Size = new System.Drawing.Size(211, 29);
            this.comboBoxImgSource.TabIndex = 1;
            this.comboBoxImgSource.SelectedIndexChanged += new System.EventHandler(this.comboBoxImgSource_SelectedIndexChanged);
            // 
            // tableLayoutPanelChoiceImage
            // 
            this.tableLayoutPanelChoiceImage.ColumnCount = 2;
            this.tableLayoutPanelChoiceImage.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelChoiceImage.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelChoiceImage.Controls.Add(this.checkBoxAuto, 1, 2);
            this.tableLayoutPanelChoiceImage.Controls.Add(this.textBoxImgPath, 0, 0);
            this.tableLayoutPanelChoiceImage.Controls.Add(this.buttonChoiceImageCatalog, 1, 1);
            this.tableLayoutPanelChoiceImage.Controls.Add(this.buttonChoiceImg, 0, 1);
            this.tableLayoutPanelChoiceImage.Location = new System.Drawing.Point(3, 73);
            this.tableLayoutPanelChoiceImage.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelChoiceImage.Name = "tableLayoutPanelChoiceImage";
            this.tableLayoutPanelChoiceImage.RowCount = 3;
            this.tableLayoutPanelChoiceImage.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelChoiceImage.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelChoiceImage.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelChoiceImage.Size = new System.Drawing.Size(674, 175);
            this.tableLayoutPanelChoiceImage.TabIndex = 1;
            // 
            // checkBoxAuto
            // 
            this.checkBoxAuto.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBoxAuto.AutoSize = true;
            this.checkBoxAuto.Enabled = false;
            this.checkBoxAuto.Font = new System.Drawing.Font("宋体", 10.5F);
            this.checkBoxAuto.Location = new System.Drawing.Point(414, 133);
            this.checkBoxAuto.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBoxAuto.Name = "checkBoxAuto";
            this.checkBoxAuto.Size = new System.Drawing.Size(183, 25);
            this.checkBoxAuto.TabIndex = 2;
            this.checkBoxAuto.Text = "自动切换下一张";
            this.checkBoxAuto.UseVisualStyleBackColor = true;
            // 
            // textBoxImgPath
            // 
            this.textBoxImgPath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanelChoiceImage.SetColumnSpan(this.textBoxImgPath, 2);
            this.textBoxImgPath.Font = new System.Drawing.Font("宋体", 10.5F);
            this.textBoxImgPath.Location = new System.Drawing.Point(80, 13);
            this.textBoxImgPath.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxImgPath.Name = "textBoxImgPath";
            this.textBoxImgPath.Size = new System.Drawing.Size(514, 31);
            this.textBoxImgPath.TabIndex = 0;
            // 
            // buttonChoiceImageCatalog
            // 
            this.buttonChoiceImageCatalog.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonChoiceImageCatalog.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonChoiceImageCatalog.Location = new System.Drawing.Point(445, 68);
            this.buttonChoiceImageCatalog.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonChoiceImageCatalog.Name = "buttonChoiceImageCatalog";
            this.buttonChoiceImageCatalog.Size = new System.Drawing.Size(120, 37);
            this.buttonChoiceImageCatalog.TabIndex = 1;
            this.buttonChoiceImageCatalog.Text = "选择目录";
            this.buttonChoiceImageCatalog.UseVisualStyleBackColor = true;
            this.buttonChoiceImageCatalog.Click += new System.EventHandler(this.buttonChoiceImageCatalog_Click);
            // 
            // buttonChoiceImg
            // 
            this.buttonChoiceImg.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonChoiceImg.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonChoiceImg.Location = new System.Drawing.Point(107, 67);
            this.buttonChoiceImg.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonChoiceImg.Name = "buttonChoiceImg";
            this.buttonChoiceImg.Size = new System.Drawing.Size(122, 40);
            this.buttonChoiceImg.TabIndex = 1;
            this.buttonChoiceImg.Text = "选择图片";
            this.buttonChoiceImg.UseVisualStyleBackColor = true;
            this.buttonChoiceImg.Click += new System.EventHandler(this.buttonChoiceImg_Click);
            // 
            // tableLayoutPanelCamera
            // 
            this.tableLayoutPanelCamera.ColumnCount = 2;
            this.tableLayoutPanelCamera.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 49.99999F));
            this.tableLayoutPanelCamera.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.00001F));
            this.tableLayoutPanelCamera.Controls.Add(this.labelChoiceCamera, 0, 0);
            this.tableLayoutPanelCamera.Controls.Add(this.comboBoxChoiceCamera, 1, 0);
            this.tableLayoutPanelCamera.Controls.Add(this.labelStrobe, 0, 8);
            this.tableLayoutPanelCamera.Controls.Add(this.comboBoxStrobe, 1, 8);
            this.tableLayoutPanelCamera.Controls.Add(this.label1, 0, 7);
            this.tableLayoutPanelCamera.Controls.Add(this.numericUpDownTimeOut, 1, 7);
            this.tableLayoutPanelCamera.Controls.Add(this.labelGain, 0, 6);
            this.tableLayoutPanelCamera.Controls.Add(this.numericUpDownGain, 1, 6);
            this.tableLayoutPanelCamera.Controls.Add(this.labelExposureTime, 0, 5);
            this.tableLayoutPanelCamera.Controls.Add(this.numericUpDownExposureTime, 1, 5);
            this.tableLayoutPanelCamera.Controls.Add(this.numericUpDownTriggerDelay, 1, 4);
            this.tableLayoutPanelCamera.Controls.Add(this.labelTriggerDelay, 0, 4);
            this.tableLayoutPanelCamera.Controls.Add(this.labelHardTrigger, 0, 3);
            this.tableLayoutPanelCamera.Controls.Add(this.comboBoxTriggerEdge, 1, 3);
            this.tableLayoutPanelCamera.Controls.Add(this.labelTriggerMode, 0, 2);
            this.tableLayoutPanelCamera.Controls.Add(this.comboBoxTriggerMode, 1, 2);
            this.tableLayoutPanelCamera.Controls.Add(this.labelTriggerModel, 0, 1);
            this.tableLayoutPanelCamera.Controls.Add(this.comboBoxTriggerModel, 1, 1);
            this.tableLayoutPanelCamera.Font = new System.Drawing.Font("宋体", 10.5F);
            this.tableLayoutPanelCamera.Location = new System.Drawing.Point(3, 256);
            this.tableLayoutPanelCamera.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelCamera.Name = "tableLayoutPanelCamera";
            this.tableLayoutPanelCamera.RowCount = 9;
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tableLayoutPanelCamera.Size = new System.Drawing.Size(674, 468);
            this.tableLayoutPanelCamera.TabIndex = 2;
            // 
            // labelTriggerModel
            //
            this.labelTriggerModel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelTriggerModel.AutoSize = true;
            this.labelTriggerModel.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelTriggerModel.Name = "labelTriggerModel";
            this.labelTriggerModel.Text = "触发模式";
            //
            // comboBoxTriggerModel
            //
            this.comboBoxTriggerModel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxTriggerModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTriggerModel.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxTriggerModel.Items.AddRange(new object[] { "开启", "关闭（连续采集）" });
            this.comboBoxTriggerModel.Name = "comboBoxTriggerModel";
            this.comboBoxTriggerModel.Size = new System.Drawing.Size(211, 29);
            this.comboBoxTriggerModel.TabIndex = 1;
            this.comboBoxTriggerModel.SelectedIndexChanged += new System.EventHandler(this.comboBoxTriggerModel_SelectedIndexChanged);
            //
            // labelChoiceCamera
            // 
            this.labelChoiceCamera.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelChoiceCamera.AutoSize = true;
            this.labelChoiceCamera.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelChoiceCamera.Location = new System.Drawing.Point(121, 15);
            this.labelChoiceCamera.Name = "labelChoiceCamera";
            this.labelChoiceCamera.Size = new System.Drawing.Size(94, 21);
            this.labelChoiceCamera.TabIndex = 0;
            this.labelChoiceCamera.Text = "选择相机";
            // 
            // comboBoxChoiceCamera
            // 
            this.comboBoxChoiceCamera.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxChoiceCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxChoiceCamera.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxChoiceCamera.FormattingEnabled = true;
            this.comboBoxChoiceCamera.Location = new System.Drawing.Point(399, 11);
            this.comboBoxChoiceCamera.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxChoiceCamera.Name = "comboBoxChoiceCamera";
            this.comboBoxChoiceCamera.Size = new System.Drawing.Size(211, 29);
            this.comboBoxChoiceCamera.TabIndex = 1;
            this.comboBoxChoiceCamera.SelectedIndexChanged += new System.EventHandler(this.comboBoxChoiceCamera_SelectedIndexChanged);
            // 
            // labelStrobe
            // 
            this.labelStrobe.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelStrobe.AutoSize = true;
            this.labelStrobe.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelStrobe.Location = new System.Drawing.Point(37, 431);
            this.labelStrobe.Name = "labelStrobe";
            this.labelStrobe.Size = new System.Drawing.Size(262, 21);
            this.labelStrobe.TabIndex = 0;
            this.labelStrobe.Text = "每次执行都要设置相机参数";
            // 
            // comboBoxStrobe
            // 
            this.comboBoxStrobe.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxStrobe.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStrobe.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxStrobe.FormattingEnabled = true;
            this.comboBoxStrobe.Items.AddRange(new object[] {
            "否",
            "是"});
            this.comboBoxStrobe.Location = new System.Drawing.Point(399, 427);
            this.comboBoxStrobe.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxStrobe.Name = "comboBoxStrobe";
            this.comboBoxStrobe.Size = new System.Drawing.Size(211, 29);
            this.comboBoxStrobe.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 10.5F);
            this.label1.Location = new System.Drawing.Point(99, 379);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(138, 21);
            this.label1.TabIndex = 0;
            this.label1.Text = "超时时间(ms)";
            // 
            // numericUpDownTimeOut
            // 
            this.numericUpDownTimeOut.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numericUpDownTimeOut.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownTimeOut.Location = new System.Drawing.Point(399, 374);
            this.numericUpDownTimeOut.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.numericUpDownTimeOut.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericUpDownTimeOut.Name = "numericUpDownTimeOut";
            this.numericUpDownTimeOut.Size = new System.Drawing.Size(212, 31);
            this.numericUpDownTimeOut.TabIndex = 2;
            this.numericUpDownTimeOut.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDownTimeOut.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // labelGain
            // 
            this.labelGain.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelGain.AutoSize = true;
            this.labelGain.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelGain.Location = new System.Drawing.Point(142, 327);
            this.labelGain.Name = "labelGain";
            this.labelGain.Size = new System.Drawing.Size(52, 21);
            this.labelGain.TabIndex = 0;
            this.labelGain.Text = "增益";
            // 
            // numericUpDownGain
            // 
            this.numericUpDownGain.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numericUpDownGain.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownGain.Location = new System.Drawing.Point(399, 322);
            this.numericUpDownGain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.numericUpDownGain.DecimalPlaces = 3;
            this.numericUpDownGain.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numericUpDownGain.Name = "numericUpDownGain";
            this.numericUpDownGain.Size = new System.Drawing.Size(212, 31);
            this.numericUpDownGain.TabIndex = 2;
            this.numericUpDownGain.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // labelExposureTime
            // 
            this.labelExposureTime.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelExposureTime.AutoSize = true;
            this.labelExposureTime.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelExposureTime.Location = new System.Drawing.Point(120, 275);
            this.labelExposureTime.Name = "labelExposureTime";
            this.labelExposureTime.Size = new System.Drawing.Size(96, 21);
            this.labelExposureTime.TabIndex = 0;
            this.labelExposureTime.Text = "曝光(us)";
            // 
            // numericUpDownExposureTime
            // 
            this.numericUpDownExposureTime.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numericUpDownExposureTime.DecimalPlaces = 3;
            this.numericUpDownExposureTime.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownExposureTime.Location = new System.Drawing.Point(399, 270);
            this.numericUpDownExposureTime.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.numericUpDownExposureTime.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.numericUpDownExposureTime.Name = "numericUpDownExposureTime";
            this.numericUpDownExposureTime.Size = new System.Drawing.Size(212, 31);
            this.numericUpDownExposureTime.TabIndex = 2;
            this.numericUpDownExposureTime.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // numericUpDownTriggerDelay
            // 
            this.numericUpDownTriggerDelay.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numericUpDownTriggerDelay.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownTriggerDelay.Location = new System.Drawing.Point(399, 218);
            this.numericUpDownTriggerDelay.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.numericUpDownTriggerDelay.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.numericUpDownTriggerDelay.Name = "numericUpDownTriggerDelay";
            this.numericUpDownTriggerDelay.Size = new System.Drawing.Size(212, 31);
            this.numericUpDownTriggerDelay.TabIndex = 2;
            this.numericUpDownTriggerDelay.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // labelTriggerDelay
            // 
            this.labelTriggerDelay.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelTriggerDelay.AutoSize = true;
            this.labelTriggerDelay.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelTriggerDelay.Location = new System.Drawing.Point(99, 223);
            this.labelTriggerDelay.Name = "labelTriggerDelay";
            this.labelTriggerDelay.Size = new System.Drawing.Size(138, 21);
            this.labelTriggerDelay.TabIndex = 0;
            this.labelTriggerDelay.Text = "触发延迟(us)";
            // 
            // labelHardTrigger
            // 
            this.labelHardTrigger.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelHardTrigger.AutoSize = true;
            this.labelHardTrigger.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelHardTrigger.Location = new System.Drawing.Point(121, 171);
            this.labelHardTrigger.Name = "labelHardTrigger";
            this.labelHardTrigger.Size = new System.Drawing.Size(94, 21);
            this.labelHardTrigger.TabIndex = 0;
            this.labelHardTrigger.Text = "硬触发沿";
            // 
            // comboBoxTriggerEdge
            // 
            this.comboBoxTriggerEdge.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxTriggerEdge.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTriggerEdge.Enabled = false;
            this.comboBoxTriggerEdge.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxTriggerEdge.FormattingEnabled = true;
            this.comboBoxTriggerEdge.Items.AddRange(new object[] {
            "上升沿",
            "下降沿",
            "高电平",
            "低电平"});
            this.comboBoxTriggerEdge.Location = new System.Drawing.Point(399, 167);
            this.comboBoxTriggerEdge.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxTriggerEdge.Name = "comboBoxTriggerEdge";
            this.comboBoxTriggerEdge.Size = new System.Drawing.Size(211, 29);
            this.comboBoxTriggerEdge.TabIndex = 1;
            // 
            // labelTriggerMode
            // 
            this.labelTriggerMode.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelTriggerMode.AutoSize = true;
            this.labelTriggerMode.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelTriggerMode.Location = new System.Drawing.Point(121, 119);
            this.labelTriggerMode.Name = "labelTriggerMode";
            this.labelTriggerMode.Size = new System.Drawing.Size(94, 21);
            this.labelTriggerMode.TabIndex = 0;
            this.labelTriggerMode.Text = "触发源";
            // 
            // comboBoxTriggerMode
            // 
            this.comboBoxTriggerMode.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxTriggerMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTriggerMode.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxTriggerMode.FormattingEnabled = true;
            this.comboBoxTriggerMode.Items.AddRange(new object[] {
            "软触发",
            "Line0",
            "Line1",
            "Line2",
            "Line3"});
            this.comboBoxTriggerMode.Location = new System.Drawing.Point(399, 115);
            this.comboBoxTriggerMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxTriggerMode.Name = "comboBoxTriggerMode";
            this.comboBoxTriggerMode.Size = new System.Drawing.Size(211, 29);
            this.comboBoxTriggerMode.TabIndex = 1;
            this.comboBoxTriggerMode.SelectedIndexChanged += new System.EventHandler(this.comboBoxTriggerMode_SelectedIndexChanged);
            // 
            // tableLayoutPanelSharedVariable
            // 
            this.tableLayoutPanelSharedVariable.ColumnCount = 2;
            this.tableLayoutPanelSharedVariable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 49.99999F));
            this.tableLayoutPanelSharedVariable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.00001F));
            this.tableLayoutPanelSharedVariable.Controls.Add(this.labelSharedVariable, 0, 0);
            this.tableLayoutPanelSharedVariable.Controls.Add(this.comboBoxSharedVariable, 1, 0);
            this.tableLayoutPanelSharedVariable.Font = new System.Drawing.Font("宋体", 10.5F);
            this.tableLayoutPanelSharedVariable.Location = new System.Drawing.Point(3, 680);
            this.tableLayoutPanelSharedVariable.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelSharedVariable.Name = "tableLayoutPanelSharedVariable";
            this.tableLayoutPanelSharedVariable.RowCount = 1;
            this.tableLayoutPanelSharedVariable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelSharedVariable.Size = new System.Drawing.Size(674, 82);
            this.tableLayoutPanelSharedVariable.TabIndex = 3;
            // 
            // labelSharedVariable
            // 
            this.labelSharedVariable.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelSharedVariable.AutoSize = true;
            this.labelSharedVariable.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelSharedVariable.Location = new System.Drawing.Point(100, 30);
            this.labelSharedVariable.Name = "labelSharedVariable";
            this.labelSharedVariable.Size = new System.Drawing.Size(136, 21);
            this.labelSharedVariable.TabIndex = 0;
            this.labelSharedVariable.Text = "选择共享变量";
            // 
            // comboBoxSharedVariable
            // 
            this.comboBoxSharedVariable.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxSharedVariable.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSharedVariable.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxSharedVariable.FormattingEnabled = true;
            this.comboBoxSharedVariable.Location = new System.Drawing.Point(399, 26);
            this.comboBoxSharedVariable.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxSharedVariable.Name = "comboBoxSharedVariable";
            this.comboBoxSharedVariable.Size = new System.Drawing.Size(211, 29);
            this.comboBoxSharedVariable.TabIndex = 1;
            // 
            // tableLayoutPanelSave
            // 
            this.tableLayoutPanelSave.ColumnCount = 2;
            this.tableLayoutPanelSave.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelSave.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelSave.Controls.Add(this.button1, 0, 0);
            this.tableLayoutPanelSave.Location = new System.Drawing.Point(3, 770);
            this.tableLayoutPanelSave.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelSave.Name = "tableLayoutPanelSave";
            this.tableLayoutPanelSave.RowCount = 1;
            this.tableLayoutPanelSave.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelSave.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tableLayoutPanelSave.Size = new System.Drawing.Size(674, 82);
            this.tableLayoutPanelSave.TabIndex = 4;
            // 
            // button1
            // 
            this.button1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanelSave.SetColumnSpan(this.button1, 2);
            this.button1.Font = new System.Drawing.Font("宋体", 10.5F);
            this.button1.Location = new System.Drawing.Point(275, 19);
            this.button1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(123, 44);
            this.button1.TabIndex = 2;
            this.button1.Text = "保存";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // ParamFormImageSource
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(682, 1000);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParamFormImageSource";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "图像源";
            this.Load += new System.EventHandler(this.ParamFormImageSource_Load);
            this.Controls.SetChildIndex(this.flowLayoutPanel1, 0);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.tableLayoutPanelChoiceImage.ResumeLayout(false);
            this.tableLayoutPanelChoiceImage.PerformLayout();
            this.tableLayoutPanelCamera.ResumeLayout(false);
            this.tableLayoutPanelCamera.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimeOut)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownGain)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownExposureTime)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTriggerDelay)).EndInit();
            this.tableLayoutPanelSharedVariable.ResumeLayout(false);
            this.tableLayoutPanelSharedVariable.PerformLayout();
            this.tableLayoutPanelSave.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelImgSource;
        private System.Windows.Forms.ComboBox comboBoxImgSource;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelChoiceImage;
        private System.Windows.Forms.TextBox textBoxImgPath;
        private System.Windows.Forms.Button buttonChoiceImg;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelCamera;
        private System.Windows.Forms.Label labelChoiceCamera;
        private System.Windows.Forms.ComboBox comboBoxChoiceCamera;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelSave;
        /// <summary>相机触发模式标签。</summary>
        private System.Windows.Forms.Label labelTriggerModel;
        /// <summary>开启触发或关闭触发并连续采集的选择控件。</summary>
        private System.Windows.Forms.ComboBox comboBoxTriggerModel;
        private System.Windows.Forms.Label labelTriggerMode;
        private System.Windows.Forms.ComboBox comboBoxTriggerMode;
        private System.Windows.Forms.Label labelHardTrigger;
        private System.Windows.Forms.ComboBox comboBoxTriggerEdge;
        private System.Windows.Forms.Label labelTriggerDelay;
        private System.Windows.Forms.NumericUpDown numericUpDownTriggerDelay;
        private System.Windows.Forms.Label labelExposureTime;
        private System.Windows.Forms.NumericUpDown numericUpDownExposureTime;
        private System.Windows.Forms.Label labelGain;
        private System.Windows.Forms.NumericUpDown numericUpDownGain;
        private System.Windows.Forms.Label labelStrobe;
        private System.Windows.Forms.ComboBox comboBoxStrobe;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button buttonChoiceImageCatalog;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.CheckBox checkBoxAuto;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numericUpDownTimeOut;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelSharedVariable;
        private System.Windows.Forms.Label labelSharedVariable;
        private System.Windows.Forms.ComboBox comboBoxSharedVariable;
    }
}
