namespace TDJS_Vision.Node._8_GeometryCreation.LineMergeFit
{
    partial class NodeParamFormLineMergeFit
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 设计器支持所需的方法。
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.checkBoxPreferEdgePoints = new System.Windows.Forms.CheckBox();
            this.comboBoxMeasureMode = new System.Windows.Forms.ComboBox();
            this.labelMeasureMode = new System.Windows.Forms.Label();
            this.comboBoxFitMode = new System.Windows.Forms.ComboBox();
            this.labelFitMode = new System.Windows.Forms.Label();
            this.nodeSubscriptionLine2 = new TDJS_Vision.Node.NodeSubscription();
            this.labelLine2 = new System.Windows.Forms.Label();
            this.nodeSubscriptionLine1 = new TDJS_Vision.Node.NodeSubscription();
            this.labelLine1 = new System.Windows.Forms.Label();
            this.groupBoxResult = new System.Windows.Forms.GroupBox();
            this.labelRuntimeStatus = new System.Windows.Forms.Label();
            this.showImageControlPreview = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.groupBoxInput.SuspendLayout();
            this.groupBoxResult.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxInput
            // 
            this.groupBoxInput.Controls.Add(this.checkBoxPreferEdgePoints);
            this.groupBoxInput.Controls.Add(this.comboBoxMeasureMode);
            this.groupBoxInput.Controls.Add(this.labelMeasureMode);
            this.groupBoxInput.Controls.Add(this.comboBoxFitMode);
            this.groupBoxInput.Controls.Add(this.labelFitMode);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionLine2);
            this.groupBoxInput.Controls.Add(this.labelLine2);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionLine1);
            this.groupBoxInput.Controls.Add(this.labelLine1);
            this.groupBoxInput.Location = new System.Drawing.Point(18, 44);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Size = new System.Drawing.Size(420, 230);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "输入与拟合参数";
            // 
            // checkBoxPreferEdgePoints
            // 
            this.checkBoxPreferEdgePoints.AutoSize = true;
            this.checkBoxPreferEdgePoints.Checked = true;
            this.checkBoxPreferEdgePoints.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxPreferEdgePoints.Location = new System.Drawing.Point(104, 210);
            this.checkBoxPreferEdgePoints.Name = "checkBoxPreferEdgePoints";
            this.checkBoxPreferEdgePoints.Size = new System.Drawing.Size(209, 19);
            this.checkBoxPreferEdgePoints.TabIndex = 8;
            this.checkBoxPreferEdgePoints.Text = "优先使用卡尺边缘点拟合";
            this.checkBoxPreferEdgePoints.UseVisualStyleBackColor = true;
            // 
            // comboBoxMeasureMode
            // 
            this.comboBoxMeasureMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMeasureMode.FormattingEnabled = true;
            this.comboBoxMeasureMode.Location = new System.Drawing.Point(104, 184);
            this.comboBoxMeasureMode.Name = "comboBoxMeasureMode";
            this.comboBoxMeasureMode.Size = new System.Drawing.Size(184, 23);
            this.comboBoxMeasureMode.TabIndex = 7;
            // 
            // labelMeasureMode
            // 
            this.labelMeasureMode.AutoSize = true;
            this.labelMeasureMode.Location = new System.Drawing.Point(26, 188);
            this.labelMeasureMode.Name = "labelMeasureMode";
            this.labelMeasureMode.Size = new System.Drawing.Size(67, 15);
            this.labelMeasureMode.TabIndex = 6;
            this.labelMeasureMode.Text = "精度模式";
            // 
            // comboBoxFitMode
            // 
            this.comboBoxFitMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFitMode.FormattingEnabled = true;
            this.comboBoxFitMode.Location = new System.Drawing.Point(104, 148);
            this.comboBoxFitMode.Name = "comboBoxFitMode";
            this.comboBoxFitMode.Size = new System.Drawing.Size(184, 23);
            this.comboBoxFitMode.TabIndex = 5;
            // 
            // labelFitMode
            // 
            this.labelFitMode.AutoSize = true;
            this.labelFitMode.Location = new System.Drawing.Point(26, 152);
            this.labelFitMode.Name = "labelFitMode";
            this.labelFitMode.Size = new System.Drawing.Size(67, 15);
            this.labelFitMode.TabIndex = 4;
            this.labelFitMode.Text = "拟合模式";
            // 
            // nodeSubscriptionLine2
            // 
            this.nodeSubscriptionLine2.Location = new System.Drawing.Point(104, 86);
            this.nodeSubscriptionLine2.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionLine2.Name = "nodeSubscriptionLine2";
            this.nodeSubscriptionLine2.Size = new System.Drawing.Size(292, 40);
            this.nodeSubscriptionLine2.TabIndex = 3;
            // 
            // labelLine2
            // 
            this.labelLine2.AutoSize = true;
            this.labelLine2.Location = new System.Drawing.Point(26, 98);
            this.labelLine2.Name = "labelLine2";
            this.labelLine2.Size = new System.Drawing.Size(52, 15);
            this.labelLine2.TabIndex = 2;
            this.labelLine2.Text = "直线2";
            // 
            // nodeSubscriptionLine1
            // 
            this.nodeSubscriptionLine1.Location = new System.Drawing.Point(104, 32);
            this.nodeSubscriptionLine1.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionLine1.Name = "nodeSubscriptionLine1";
            this.nodeSubscriptionLine1.Size = new System.Drawing.Size(292, 40);
            this.nodeSubscriptionLine1.TabIndex = 1;
            // 
            // labelLine1
            // 
            this.labelLine1.AutoSize = true;
            this.labelLine1.Location = new System.Drawing.Point(26, 44);
            this.labelLine1.Name = "labelLine1";
            this.labelLine1.Size = new System.Drawing.Size(52, 15);
            this.labelLine1.TabIndex = 0;
            this.labelLine1.Text = "直线1";
            // 
            // groupBoxResult
            // 
            this.groupBoxResult.Controls.Add(this.labelRuntimeStatus);
            this.groupBoxResult.Location = new System.Drawing.Point(18, 292);
            this.groupBoxResult.Name = "groupBoxResult";
            this.groupBoxResult.Size = new System.Drawing.Size(420, 118);
            this.groupBoxResult.TabIndex = 1;
            this.groupBoxResult.TabStop = false;
            this.groupBoxResult.Text = "运行状态";
            // 
            // labelRuntimeStatus
            // 
            this.labelRuntimeStatus.AutoSize = true;
            this.labelRuntimeStatus.Location = new System.Drawing.Point(24, 34);
            this.labelRuntimeStatus.MaximumSize = new System.Drawing.Size(570, 0);
            this.labelRuntimeStatus.Name = "labelRuntimeStatus";
            this.labelRuntimeStatus.Size = new System.Drawing.Size(112, 15);
            this.labelRuntimeStatus.TabIndex = 0;
            this.labelRuntimeStatus.Text = "暂无运行结果";
            // 
            // showImageControlPreview
            // 
            this.showImageControlPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(24)))), ((int)(((byte)(28)))));
            this.showImageControlPreview.Location = new System.Drawing.Point(460, 44);
            this.showImageControlPreview.Name = "showImageControlPreview";
            this.showImageControlPreview.Size = new System.Drawing.Size(760, 520);
            this.showImageControlPreview.TabIndex = 4;
            // 
            // buttonRun
            // 
            this.buttonRun.Location = new System.Drawing.Point(222, 430);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(96, 34);
            this.buttonRun.TabIndex = 2;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(342, 430);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(96, 34);
            this.buttonSave.TabIndex = 3;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormLineMergeFit
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1240, 586);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.showImageControlPreview);
            this.Controls.Add(this.groupBoxResult);
            this.Controls.Add(this.groupBoxInput);
            this.Name = "NodeParamFormLineMergeFit";
            this.Text = "线组合拟合";
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxInput.PerformLayout();
            this.groupBoxResult.ResumeLayout(false);
            this.groupBoxResult.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.GroupBox groupBoxInput;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionLine1;
        private System.Windows.Forms.Label labelLine1;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionLine2;
        private System.Windows.Forms.Label labelLine2;
        private System.Windows.Forms.ComboBox comboBoxFitMode;
        private System.Windows.Forms.Label labelFitMode;
        private System.Windows.Forms.ComboBox comboBoxMeasureMode;
        private System.Windows.Forms.Label labelMeasureMode;
        private System.Windows.Forms.CheckBox checkBoxPreferEdgePoints;
        private System.Windows.Forms.GroupBox groupBoxResult;
        private System.Windows.Forms.Label labelRuntimeStatus;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControlPreview;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonSave;
    }
}
