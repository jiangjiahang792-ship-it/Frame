namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    partial class NodeParamFormPositionCorrection
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.labelAngle = new System.Windows.Forms.Label();
            this.nodeSubscriptionAngle = new TDJS_Vision.Node.NodeSubscription();
            this.labelY = new System.Windows.Forms.Label();
            this.nodeSubscriptionY = new TDJS_Vision.Node.NodeSubscription();
            this.labelX = new System.Windows.Forms.Label();
            this.nodeSubscriptionX = new TDJS_Vision.Node.NodeSubscription();
            this.buttonCreateBaseline = new System.Windows.Forms.Button();
            this.labelBaselineStatus = new System.Windows.Forms.Label();
            this.groupBoxResult = new System.Windows.Forms.GroupBox();
            this.labelRuntimeStatus = new System.Windows.Forms.Label();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.groupBoxInput.SuspendLayout();
            this.groupBoxResult.SuspendLayout();
            this.SuspendLayout();
            //
            // groupBoxInput
            //
            this.groupBoxInput.Controls.Add(this.labelAngle);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionAngle);
            this.groupBoxInput.Controls.Add(this.labelY);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionY);
            this.groupBoxInput.Controls.Add(this.labelX);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionX);
            this.groupBoxInput.Controls.Add(this.buttonCreateBaseline);
            this.groupBoxInput.Controls.Add(this.labelBaselineStatus);
            this.groupBoxInput.Location = new System.Drawing.Point(18, 44);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Size = new System.Drawing.Size(548, 222);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "位置补正";
            //
            // labelAngle
            //
            this.labelAngle.AutoSize = true;
            this.labelAngle.Location = new System.Drawing.Point(18, 118);
            this.labelAngle.Name = "labelAngle";
            this.labelAngle.Size = new System.Drawing.Size(37, 15);
            this.labelAngle.TabIndex = 4;
            this.labelAngle.Text = "角度";
            //
            // nodeSubscriptionAngle
            //
            this.nodeSubscriptionAngle.Location = new System.Drawing.Point(86, 108);
            this.nodeSubscriptionAngle.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionAngle.Name = "nodeSubscriptionAngle";
            this.nodeSubscriptionAngle.Size = new System.Drawing.Size(438, 32);
            this.nodeSubscriptionAngle.TabIndex = 5;
            //
            // labelY
            //
            this.labelY.AutoSize = true;
            this.labelY.Location = new System.Drawing.Point(18, 76);
            this.labelY.Name = "labelY";
            this.labelY.Size = new System.Drawing.Size(52, 15);
            this.labelY.TabIndex = 2;
            this.labelY.Text = "Y位置";
            //
            // nodeSubscriptionY
            //
            this.nodeSubscriptionY.Location = new System.Drawing.Point(86, 66);
            this.nodeSubscriptionY.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionY.Name = "nodeSubscriptionY";
            this.nodeSubscriptionY.Size = new System.Drawing.Size(438, 32);
            this.nodeSubscriptionY.TabIndex = 3;
            //
            // labelX
            //
            this.labelX.AutoSize = true;
            this.labelX.Location = new System.Drawing.Point(18, 34);
            this.labelX.Name = "labelX";
            this.labelX.Size = new System.Drawing.Size(52, 15);
            this.labelX.TabIndex = 0;
            this.labelX.Text = "X位置";
            //
            // nodeSubscriptionX
            //
            this.nodeSubscriptionX.Location = new System.Drawing.Point(86, 24);
            this.nodeSubscriptionX.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionX.Name = "nodeSubscriptionX";
            this.nodeSubscriptionX.Size = new System.Drawing.Size(438, 32);
            this.nodeSubscriptionX.TabIndex = 1;
            //
            // buttonCreateBaseline
            //
            this.buttonCreateBaseline.Location = new System.Drawing.Point(21, 161);
            this.buttonCreateBaseline.Name = "buttonCreateBaseline";
            this.buttonCreateBaseline.Size = new System.Drawing.Size(100, 32);
            this.buttonCreateBaseline.TabIndex = 6;
            this.buttonCreateBaseline.Text = "创建基准";
            this.buttonCreateBaseline.UseVisualStyleBackColor = true;
            this.buttonCreateBaseline.Click += new System.EventHandler(this.buttonCreateBaseline_Click);
            //
            // labelBaselineStatus
            //
            this.labelBaselineStatus.AutoSize = true;
            this.labelBaselineStatus.Location = new System.Drawing.Point(140, 169);
            this.labelBaselineStatus.Name = "labelBaselineStatus";
            this.labelBaselineStatus.Size = new System.Drawing.Size(97, 15);
            this.labelBaselineStatus.TabIndex = 7;
            this.labelBaselineStatus.Text = "基准：未创建";
            //
            // groupBoxResult
            //
            this.groupBoxResult.Controls.Add(this.labelRuntimeStatus);
            this.groupBoxResult.Location = new System.Drawing.Point(18, 282);
            this.groupBoxResult.Name = "groupBoxResult";
            this.groupBoxResult.Size = new System.Drawing.Size(548, 120);
            this.groupBoxResult.TabIndex = 1;
            this.groupBoxResult.TabStop = false;
            this.groupBoxResult.Text = "结果显示";
            //
            // labelRuntimeStatus
            //
            this.labelRuntimeStatus.AutoSize = true;
            this.labelRuntimeStatus.Location = new System.Drawing.Point(18, 34);
            this.labelRuntimeStatus.MaximumSize = new System.Drawing.Size(500, 0);
            this.labelRuntimeStatus.Name = "labelRuntimeStatus";
            this.labelRuntimeStatus.Size = new System.Drawing.Size(112, 15);
            this.labelRuntimeStatus.TabIndex = 0;
            this.labelRuntimeStatus.Text = "暂无运行结果";
            //
            // buttonRun
            //
            this.buttonRun.Location = new System.Drawing.Point(354, 424);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(96, 34);
            this.buttonRun.TabIndex = 2;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            //
            // buttonSave
            //
            this.buttonSave.Location = new System.Drawing.Point(470, 424);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(96, 34);
            this.buttonSave.TabIndex = 3;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            //
            // NodeParamFormPositionCorrection
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 478);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.groupBoxResult);
            this.Controls.Add(this.groupBoxInput);
            this.Name = "NodeParamFormPositionCorrection";
            this.Text = "位置修正";
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxInput.PerformLayout();
            this.groupBoxResult.ResumeLayout(false);
            this.groupBoxResult.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.GroupBox groupBoxInput;
        private System.Windows.Forms.Label labelAngle;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionAngle;
        private System.Windows.Forms.Label labelY;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionY;
        private System.Windows.Forms.Label labelX;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionX;
        private System.Windows.Forms.Button buttonCreateBaseline;
        private System.Windows.Forms.Label labelBaselineStatus;
        private System.Windows.Forms.GroupBox groupBoxResult;
        private System.Windows.Forms.Label labelRuntimeStatus;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonSave;
    }
}

