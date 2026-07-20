namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    partial class NodeParamFormPositionCorrection
    {
        /// <summary>
        /// 设计器组件容器。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 释放设计器创建的资源。
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
        /// 初始化位置修正窗口控件，所有可视控件均由设计器文件维护。
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.labelPoses = new System.Windows.Forms.Label();
            this.nodeSubscriptionPoses = new TDJS_Vision.Node.NodeSubscription();
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
            this.groupBoxInput.Controls.Add(this.labelPoses);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionPoses);
            this.groupBoxInput.Controls.Add(this.buttonCreateBaseline);
            this.groupBoxInput.Controls.Add(this.labelBaselineStatus);
            this.groupBoxInput.Location = new System.Drawing.Point(18, 44);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Size = new System.Drawing.Size(548, 176);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "位置修正";
            //
            // labelPoses
            //
            this.labelPoses.AutoSize = true;
            this.labelPoses.Location = new System.Drawing.Point(18, 39);
            this.labelPoses.Name = "labelPoses";
            this.labelPoses.Size = new System.Drawing.Size(67, 15);
            this.labelPoses.TabIndex = 0;
            this.labelPoses.Text = "目标位姿";
            //
            // nodeSubscriptionPoses
            //
            this.nodeSubscriptionPoses.Location = new System.Drawing.Point(91, 24);
            this.nodeSubscriptionPoses.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionPoses.MinimumSize = new System.Drawing.Size(160, 49);
            this.nodeSubscriptionPoses.Name = "nodeSubscriptionPoses";
            this.nodeSubscriptionPoses.Size = new System.Drawing.Size(433, 49);
            this.nodeSubscriptionPoses.TabIndex = 1;
            //
            // buttonCreateBaseline
            //
            this.buttonCreateBaseline.Location = new System.Drawing.Point(21, 91);
            this.buttonCreateBaseline.Name = "buttonCreateBaseline";
            this.buttonCreateBaseline.Size = new System.Drawing.Size(100, 32);
            this.buttonCreateBaseline.TabIndex = 2;
            this.buttonCreateBaseline.Text = "创建基准";
            this.buttonCreateBaseline.UseVisualStyleBackColor = true;
            this.buttonCreateBaseline.Click += new System.EventHandler(this.buttonCreateBaseline_Click);
            //
            // labelBaselineStatus
            //
            this.labelBaselineStatus.AutoSize = true;
            this.labelBaselineStatus.Location = new System.Drawing.Point(140, 99);
            this.labelBaselineStatus.MaximumSize = new System.Drawing.Size(390, 0);
            this.labelBaselineStatus.Name = "labelBaselineStatus";
            this.labelBaselineStatus.Size = new System.Drawing.Size(307, 15);
            this.labelBaselineStatus.TabIndex = 3;
            this.labelBaselineStatus.Text = "基准：未创建（默认取位姿列表第1目标）";
            //
            // groupBoxResult
            //
            this.groupBoxResult.Controls.Add(this.labelRuntimeStatus);
            this.groupBoxResult.Location = new System.Drawing.Point(18, 236);
            this.groupBoxResult.Name = "groupBoxResult";
            this.groupBoxResult.Size = new System.Drawing.Size(548, 104);
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
            this.buttonRun.Location = new System.Drawing.Point(354, 360);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(96, 34);
            this.buttonRun.TabIndex = 2;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            //
            // buttonSave
            //
            this.buttonSave.Location = new System.Drawing.Point(470, 360);
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
            this.ClientSize = new System.Drawing.Size(584, 414);
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

        /// <summary>位置修正输入区域。</summary>
        private System.Windows.Forms.GroupBox groupBoxInput;

        /// <summary>模板目标位姿列表标签。</summary>
        private System.Windows.Forms.Label labelPoses;

        /// <summary>模板目标位姿列表订阅控件。</summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPoses;

        /// <summary>创建第一目标基准按钮。</summary>
        private System.Windows.Forms.Button buttonCreateBaseline;

        /// <summary>第一目标基准状态标签。</summary>
        private System.Windows.Forms.Label labelBaselineStatus;

        /// <summary>运行结果显示区域。</summary>
        private System.Windows.Forms.GroupBox groupBoxResult;

        /// <summary>运行结果摘要标签。</summary>
        private System.Windows.Forms.Label labelRuntimeStatus;

        /// <summary>预览执行按钮。</summary>
        private System.Windows.Forms.Button buttonRun;

        /// <summary>保存参数按钮。</summary>
        private System.Windows.Forms.Button buttonSave;
    }
}
