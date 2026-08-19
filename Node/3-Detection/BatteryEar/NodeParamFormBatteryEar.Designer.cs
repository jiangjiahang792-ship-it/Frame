namespace TDJS_Vision.Node._3_Detection.BatteryEar
{
    partial class NodeParamFormBatteryEar
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
            if (disposing)
            {
                ReleaseImageResources();
                components?.Dispose();
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormBatteryEar));
            this.button3 = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.imageROIEditControl1 = new TDJS_Vision.Forms.ShapeDraw.ImageROIEditControl();
            this.label3 = new System.Windows.Forms.Label();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxFixtureWidthMM = new System.Windows.Forms.TextBox();
            this.buttonGetPixNum = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxScale = new System.Windows.Forms.TextBox();
            this.labelPixNum = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxScore = new System.Windows.Forms.TextBox();
            this.buttonRun = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.textBoxDeltaMM2 = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.comboBoxDevice = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.textBoxDeltaMM1 = new System.Windows.Forms.TextBox();
            this.textBoxDeltaMM4 = new System.Windows.Forms.TextBox();
            this.textBoxDeltaMM3 = new System.Windows.Forms.TextBox();
            this.buttonNewTemplate = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.textBoxImgSavePath = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button3
            // 
            this.button3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button3.Location = new System.Drawing.Point(478, 6);
            this.button3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(101, 44);
            this.button3.TabIndex = 4;
            this.button3.Text = "刷新图像";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSave.BackColor = System.Drawing.Color.CornflowerBlue;
            this.buttonSave.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonSave.Location = new System.Drawing.Point(774, 228);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(117, 47);
            this.buttonSave.TabIndex = 1;
            this.buttonSave.Text = "保存参数";
            this.buttonSave.UseVisualStyleBackColor = false;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 6;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanel1.Controls.Add(this.imageROIEditControl1, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.nodeSubscription1, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.button3, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.textBoxFixtureWidthMM, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.buttonGetPixNum, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.label5, 4, 1);
            this.tableLayoutPanel1.Controls.Add(this.textBoxScale, 5, 1);
            this.tableLayoutPanel1.Controls.Add(this.labelPixNum, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.textBoxScore, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.buttonRun, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.textBoxDeltaMM2, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label9, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxDevice, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.label6, 2, 3);
            this.tableLayoutPanel1.Controls.Add(this.label7, 4, 3);
            this.tableLayoutPanel1.Controls.Add(this.label8, 4, 2);
            this.tableLayoutPanel1.Controls.Add(this.textBoxDeltaMM1, 5, 2);
            this.tableLayoutPanel1.Controls.Add(this.textBoxDeltaMM4, 5, 3);
            this.tableLayoutPanel1.Controls.Add(this.textBoxDeltaMM3, 3, 3);
            this.tableLayoutPanel1.Controls.Add(this.buttonSave, 5, 4);
            this.tableLayoutPanel1.Controls.Add(this.buttonNewTemplate, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.label10, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.textBoxImgSavePath, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.button1, 4, 4);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(910, 721);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // imageROIEditControl1
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.imageROIEditControl1, 6);
            this.imageROIEditControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageROIEditControl1.Location = new System.Drawing.Point(4, 282);
            this.imageROIEditControl1.Margin = new System.Windows.Forms.Padding(4, 2, 4, 2);
            this.imageROIEditControl1.Name = "imageROIEditControl1";
            this.imageROIEditControl1.Size = new System.Drawing.Size(902, 437);
            this.imageROIEditControl1.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(35, 19);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(80, 18);
            this.label3.TabIndex = 7;
            this.label3.Text = "订阅图像";
            // 
            // nodeSubscription1
            // 
            this.nodeSubscription1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel1.SetColumnSpan(this.nodeSubscription1, 2);
            this.nodeSubscription1.Location = new System.Drawing.Point(154, 3);
            this.nodeSubscription1.Margin = new System.Windows.Forms.Padding(2);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(231, 49);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(296, 49);
            this.nodeSubscription1.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(26, 75);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(98, 18);
            this.label2.TabIndex = 7;
            this.label2.Text = "夹具宽度mm";
            // 
            // textBoxFixtureWidthMM
            // 
            this.textBoxFixtureWidthMM.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxFixtureWidthMM.Location = new System.Drawing.Point(176, 70);
            this.textBoxFixtureWidthMM.Name = "textBoxFixtureWidthMM";
            this.textBoxFixtureWidthMM.Size = new System.Drawing.Size(100, 27);
            this.textBoxFixtureWidthMM.TabIndex = 8;
            this.textBoxFixtureWidthMM.Text = "34.82";
            this.textBoxFixtureWidthMM.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // buttonGetPixNum
            // 
            this.buttonGetPixNum.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonGetPixNum.Location = new System.Drawing.Point(305, 63);
            this.buttonGetPixNum.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonGetPixNum.Name = "buttonGetPixNum";
            this.buttonGetPixNum.Size = new System.Drawing.Size(144, 41);
            this.buttonGetPixNum.TabIndex = 1;
            this.buttonGetPixNum.Text = "获取像素宽度";
            this.buttonGetPixNum.UseVisualStyleBackColor = true;
            this.buttonGetPixNum.Click += new System.EventHandler(this.buttonGetPixNum_Click);
            // 
            // label5
            // 
            this.label5.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(612, 75);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(134, 18);
            this.label5.TabIndex = 7;
            this.label5.Text = "比例尺(mm/pix)";
            // 
            // textBoxScale
            // 
            this.textBoxScale.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxScale.Location = new System.Drawing.Point(782, 70);
            this.textBoxScale.Name = "textBoxScale";
            this.textBoxScale.Size = new System.Drawing.Size(100, 27);
            this.textBoxScale.TabIndex = 8;
            this.textBoxScale.Text = "0.005";
            this.textBoxScale.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // labelPixNum
            // 
            this.labelPixNum.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelPixNum.AutoSize = true;
            this.labelPixNum.Location = new System.Drawing.Point(506, 75);
            this.labelPixNum.Name = "labelPixNum";
            this.labelPixNum.Size = new System.Drawing.Size(44, 18);
            this.labelPixNum.TabIndex = 7;
            this.labelPixNum.Text = "1300";
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 131);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(125, 18);
            this.label1.TabIndex = 7;
            this.label1.Text = "分数阈值(0-1)";
            // 
            // textBoxScore
            // 
            this.textBoxScore.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxScore.Location = new System.Drawing.Point(176, 126);
            this.textBoxScore.Name = "textBoxScore";
            this.textBoxScore.Size = new System.Drawing.Size(100, 27);
            this.textBoxScore.TabIndex = 8;
            this.textBoxScore.Text = "0.5";
            this.textBoxScore.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // buttonRun
            // 
            this.buttonRun.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonRun.Location = new System.Drawing.Point(629, 7);
            this.buttonRun.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(101, 42);
            this.buttonRun.TabIndex = 1;
            this.buttonRun.Text = "执行一次";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(35, 187);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(80, 18);
            this.label4.TabIndex = 7;
            this.label4.Text = "D2补偿mm";
            // 
            // textBoxDeltaMM2
            // 
            this.textBoxDeltaMM2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxDeltaMM2.Location = new System.Drawing.Point(176, 182);
            this.textBoxDeltaMM2.Name = "textBoxDeltaMM2";
            this.textBoxDeltaMM2.Size = new System.Drawing.Size(100, 27);
            this.textBoxDeltaMM2.TabIndex = 8;
            this.textBoxDeltaMM2.Text = "0";
            this.textBoxDeltaMM2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label9
            // 
            this.label9.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(337, 131);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(80, 18);
            this.label9.TabIndex = 7;
            this.label9.Text = "通信设备";
            // 
            // comboBoxDevice
            // 
            this.comboBoxDevice.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDevice.FormattingEnabled = true;
            this.comboBoxDevice.Location = new System.Drawing.Point(456, 127);
            this.comboBoxDevice.Name = "comboBoxDevice";
            this.comboBoxDevice.Size = new System.Drawing.Size(145, 25);
            this.comboBoxDevice.TabIndex = 9;
            // 
            // label6
            // 
            this.label6.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(337, 187);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(80, 18);
            this.label6.TabIndex = 7;
            this.label6.Text = "D3补偿mm";
            // 
            // label7
            // 
            this.label7.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(639, 187);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(80, 18);
            this.label7.TabIndex = 7;
            this.label7.Text = "D4补偿mm";
            // 
            // label8
            // 
            this.label8.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(639, 131);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(80, 18);
            this.label8.TabIndex = 7;
            this.label8.Text = "D1补偿mm";
            // 
            // textBoxDeltaMM1
            // 
            this.textBoxDeltaMM1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxDeltaMM1.Location = new System.Drawing.Point(782, 126);
            this.textBoxDeltaMM1.Name = "textBoxDeltaMM1";
            this.textBoxDeltaMM1.Size = new System.Drawing.Size(100, 27);
            this.textBoxDeltaMM1.TabIndex = 8;
            this.textBoxDeltaMM1.Text = "0";
            this.textBoxDeltaMM1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // textBoxDeltaMM4
            // 
            this.textBoxDeltaMM4.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxDeltaMM4.Location = new System.Drawing.Point(782, 182);
            this.textBoxDeltaMM4.Name = "textBoxDeltaMM4";
            this.textBoxDeltaMM4.Size = new System.Drawing.Size(100, 27);
            this.textBoxDeltaMM4.TabIndex = 8;
            this.textBoxDeltaMM4.Text = "0";
            this.textBoxDeltaMM4.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // textBoxDeltaMM3
            // 
            this.textBoxDeltaMM3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxDeltaMM3.Location = new System.Drawing.Point(478, 182);
            this.textBoxDeltaMM3.Name = "textBoxDeltaMM3";
            this.textBoxDeltaMM3.Size = new System.Drawing.Size(100, 27);
            this.textBoxDeltaMM3.TabIndex = 8;
            this.textBoxDeltaMM3.Text = "0";
            this.textBoxDeltaMM3.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // buttonNewTemplate
            // 
            this.buttonNewTemplate.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonNewTemplate.Location = new System.Drawing.Point(770, 7);
            this.buttonNewTemplate.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonNewTemplate.Name = "buttonNewTemplate";
            this.buttonNewTemplate.Size = new System.Drawing.Size(125, 41);
            this.buttonNewTemplate.TabIndex = 1;
            this.buttonNewTemplate.Text = "模版管理";
            this.buttonNewTemplate.UseVisualStyleBackColor = true;
            this.buttonNewTemplate.Click += new System.EventHandler(this.buttonNewTemplate_Click);
            // 
            // label10
            // 
            this.label10.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(35, 243);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(80, 18);
            this.label10.TabIndex = 7;
            this.label10.Text = "存图路径";
            // 
            // textBoxImgSavePath
            // 
            this.textBoxImgSavePath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel1.SetColumnSpan(this.textBoxImgSavePath, 3);
            this.textBoxImgSavePath.Location = new System.Drawing.Point(154, 238);
            this.textBoxImgSavePath.Name = "textBoxImgSavePath";
            this.textBoxImgSavePath.Size = new System.Drawing.Size(447, 27);
            this.textBoxImgSavePath.TabIndex = 8;
            this.textBoxImgSavePath.Text = "D:\\Images";
            this.textBoxImgSavePath.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // button1
            // 
            this.button1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button1.Location = new System.Drawing.Point(623, 231);
            this.button1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(112, 41);
            this.button1.TabIndex = 1;
            this.button1.Text = "选择路径";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.SelectedPath = "D:\\Images";
            // 
            // NodeParamFormBatteryEar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(914, 755);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormBatteryEar";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "锂电池极耳检测";
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private Forms.ShapeDraw.ImageROIEditControl imageROIEditControl1;
        private NodeSubscription nodeSubscription1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.TextBox textBoxScore;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBoxDeltaMM2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxFixtureWidthMM;
        private System.Windows.Forms.Button buttonGetPixNum;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBoxScale;
        private System.Windows.Forms.Label labelPixNum;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboBoxDevice;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textBoxDeltaMM1;
        private System.Windows.Forms.TextBox textBoxDeltaMM4;
        private System.Windows.Forms.TextBox textBoxDeltaMM3;
        private System.Windows.Forms.Button buttonNewTemplate;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox textBoxImgSavePath;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
    }
}
