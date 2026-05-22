namespace TDJS_Vision.Forms.ProcessNew
{
    partial class ProcessEditPanel
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonStop = new System.Windows.Forms.Button();
            this.buttonClean = new System.Windows.Forms.Button();
            this.uiLedBulb1 = new Sunny.UI.UILedBulb();
            this.uiSwitchEnable = new Sunny.UI.UISwitch();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.processFlowCanvas = new TDJS_Vision.Forms.ProcessNew.ProcessFlowCanvas();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.设置流程优先级ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设置流程组别ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.流程重命名ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.是否输出日志ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.是否为触发流程ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripFlowCanvas = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.打开节点参数ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设为起始节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.启用节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.禁用节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.重命名节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.添加节点备注ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除连线ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buttonRun = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.contextMenuStripFlowCanvas.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.tableLayoutPanel1.ColumnCount = 7;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28571F));
            this.tableLayoutPanel1.Controls.Add(this.buttonStop, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonClean, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.uiLedBulb1, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.uiSwitchEnable, 6, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonRun, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(616, 61);
            this.tableLayoutPanel1.TabIndex = 0;
            this.tableLayoutPanel1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            // 
            // buttonStop
            // 
            this.buttonStop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonStop.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.buttonStop.Image = global::TDJS_Vision.Properties.Resources.停止执行;
            this.buttonStop.Location = new System.Drawing.Point(443, 4);
            this.buttonStop.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(82, 53);
            this.buttonStop.TabIndex = 5;
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // buttonClean
            // 
            this.buttonClean.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonClean.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonClean.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.buttonClean.Image = global::TDJS_Vision.Properties.Resources.打扫;
            this.buttonClean.Location = new System.Drawing.Point(267, 4);
            this.buttonClean.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonClean.Name = "buttonClean";
            this.buttonClean.Size = new System.Drawing.Size(82, 53);
            this.buttonClean.TabIndex = 4;
            this.buttonClean.UseVisualStyleBackColor = true;
            this.buttonClean.Click += new System.EventHandler(this.buttonClean_Click);
            // 
            // uiLedBulb1
            // 
            this.uiLedBulb1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.uiLedBulb1.Color = System.Drawing.Color.DarkGray;
            this.uiLedBulb1.Location = new System.Drawing.Point(204, 14);
            this.uiLedBulb1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.uiLedBulb1.Name = "uiLedBulb1";
            this.uiLedBulb1.Size = new System.Drawing.Size(32, 32);
            this.uiLedBulb1.TabIndex = 0;
            this.uiLedBulb1.Text = "uiLedBulb1";
            this.uiLedBulb1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            // 
            // uiSwitchEnable
            // 
            this.uiSwitchEnable.Active = true;
            this.uiSwitchEnable.ActiveText = "启用";
            this.uiSwitchEnable.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.uiSwitchEnable.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSwitchEnable.InActiveText = "禁用";
            this.uiSwitchEnable.Location = new System.Drawing.Point(531, 16);
            this.uiSwitchEnable.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.uiSwitchEnable.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSwitchEnable.Name = "uiSwitchEnable";
            this.uiSwitchEnable.Size = new System.Drawing.Size(82, 29);
            this.uiSwitchEnable.TabIndex = 0;
            this.uiSwitchEnable.Text = "uiSwitch1";
            this.uiSwitchEnable.ValueChanged += new Sunny.UI.UISwitch.OnValueChanged(this.uiSwitch1_ValueChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 18);
            this.label1.TabIndex = 2;
            this.label1.Text = "节点数:0";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(91, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(82, 36);
            this.label2.TabIndex = 2;
            this.label2.Text = "流程耗时:0ms";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label2.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            //
            // processFlowCanvas
            //
            this.processFlowCanvas.AllowDrop = true;
            this.processFlowCanvas.AutoScroll = true;
            this.processFlowCanvas.ContextMenuStrip = this.contextMenuStripFlowCanvas;
            this.processFlowCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.processFlowCanvas.Location = new System.Drawing.Point(0, 61);
            this.processFlowCanvas.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.processFlowCanvas.Name = "processFlowCanvas";
            this.processFlowCanvas.Size = new System.Drawing.Size(616, 709);
            this.processFlowCanvas.TabIndex = 1;
            this.processFlowCanvas.DragDrop += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragDrop);
            this.processFlowCanvas.DragEnter += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragEnter);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.设置流程优先级ToolStripMenuItem,
            this.设置流程组别ToolStripMenuItem,
            this.流程重命名ToolStripMenuItem,
            this.是否输出日志ToolStripMenuItem,
            this.是否为触发流程ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(225, 164);
            // 
            // 设置流程优先级ToolStripMenuItem
            // 
            this.设置流程优先级ToolStripMenuItem.Name = "设置流程优先级ToolStripMenuItem";
            this.设置流程优先级ToolStripMenuItem.Size = new System.Drawing.Size(224, 32);
            this.设置流程优先级ToolStripMenuItem.Text = "设置流程优先级";
            this.设置流程优先级ToolStripMenuItem.Click += new System.EventHandler(this.设置流程优先级ToolStripMenuItem_Click);
            // 
            // 设置流程组别ToolStripMenuItem
            // 
            this.设置流程组别ToolStripMenuItem.Name = "设置流程组别ToolStripMenuItem";
            this.设置流程组别ToolStripMenuItem.Size = new System.Drawing.Size(224, 32);
            this.设置流程组别ToolStripMenuItem.Text = "设置流程组别";
            this.设置流程组别ToolStripMenuItem.Click += new System.EventHandler(this.设置流程组别ToolStripMenuItem_Click);
            // 
            // 流程重命名ToolStripMenuItem
            // 
            this.流程重命名ToolStripMenuItem.Name = "流程重命名ToolStripMenuItem";
            this.流程重命名ToolStripMenuItem.Size = new System.Drawing.Size(224, 32);
            this.流程重命名ToolStripMenuItem.Text = "流程重命名";
            this.流程重命名ToolStripMenuItem.Click += new System.EventHandler(this.流程重命名ToolStripMenuItem_Click);
            // 
            // 是否输出日志ToolStripMenuItem
            // 
            this.是否输出日志ToolStripMenuItem.Checked = true;
            this.是否输出日志ToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.是否输出日志ToolStripMenuItem.Name = "是否输出日志ToolStripMenuItem";
            this.是否输出日志ToolStripMenuItem.Size = new System.Drawing.Size(224, 32);
            this.是否输出日志ToolStripMenuItem.Text = "是否输出日志";
            this.是否输出日志ToolStripMenuItem.Click += new System.EventHandler(this.是否输出日志ToolStripMenuItem_Click);
            // 
            // 是否为触发流程ToolStripMenuItem
            // 
            this.是否为触发流程ToolStripMenuItem.Name = "是否为触发流程ToolStripMenuItem";
            this.是否为触发流程ToolStripMenuItem.Size = new System.Drawing.Size(224, 32);
            this.是否为触发流程ToolStripMenuItem.Text = "是否为被触发流程";
            this.是否为触发流程ToolStripMenuItem.Click += new System.EventHandler(this.是否为触发流程ToolStripMenuItem_Click);
            //
            // contextMenuStripFlowCanvas
            //
            this.contextMenuStripFlowCanvas.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStripFlowCanvas.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.打开节点参数ToolStripMenuItem,
            this.设为起始节点ToolStripMenuItem,
            this.启用节点ToolStripMenuItem,
            this.禁用节点ToolStripMenuItem,
            this.重命名节点ToolStripMenuItem,
            this.添加节点备注ToolStripMenuItem,
            this.删除节点ToolStripMenuItem,
            this.删除连线ToolStripMenuItem});
            this.contextMenuStripFlowCanvas.Name = "contextMenuStripFlowCanvas";
            this.contextMenuStripFlowCanvas.Size = new System.Drawing.Size(189, 260);
            this.contextMenuStripFlowCanvas.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripFlowCanvas_Opening);
            //
            // 打开节点参数ToolStripMenuItem
            //
            this.打开节点参数ToolStripMenuItem.Name = "打开节点参数ToolStripMenuItem";
            this.打开节点参数ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.打开节点参数ToolStripMenuItem.Text = "打开节点参数";
            this.打开节点参数ToolStripMenuItem.Click += new System.EventHandler(this.打开节点参数ToolStripMenuItem_Click);
            //
            // 设为起始节点ToolStripMenuItem
            //
            this.设为起始节点ToolStripMenuItem.Name = "设为起始节点ToolStripMenuItem";
            this.设为起始节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.设为起始节点ToolStripMenuItem.Text = "设为起始节点";
            this.设为起始节点ToolStripMenuItem.Click += new System.EventHandler(this.设为起始节点ToolStripMenuItem_Click);
            //
            // 启用节点ToolStripMenuItem
            //
            this.启用节点ToolStripMenuItem.Name = "启用节点ToolStripMenuItem";
            this.启用节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.启用节点ToolStripMenuItem.Text = "启用节点";
            this.启用节点ToolStripMenuItem.Click += new System.EventHandler(this.启用节点ToolStripMenuItem_Click);
            //
            // 禁用节点ToolStripMenuItem
            //
            this.禁用节点ToolStripMenuItem.Name = "禁用节点ToolStripMenuItem";
            this.禁用节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.禁用节点ToolStripMenuItem.Text = "禁用节点";
            this.禁用节点ToolStripMenuItem.Click += new System.EventHandler(this.禁用节点ToolStripMenuItem_Click);
            //
            // 重命名节点ToolStripMenuItem
            //
            this.重命名节点ToolStripMenuItem.Name = "重命名节点ToolStripMenuItem";
            this.重命名节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.重命名节点ToolStripMenuItem.Text = "重命名节点";
            this.重命名节点ToolStripMenuItem.Click += new System.EventHandler(this.重命名节点ToolStripMenuItem_Click);
            //
            // 添加节点备注ToolStripMenuItem
            //
            this.添加节点备注ToolStripMenuItem.Name = "添加节点备注ToolStripMenuItem";
            this.添加节点备注ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.添加节点备注ToolStripMenuItem.Text = "添加节点备注";
            this.添加节点备注ToolStripMenuItem.Click += new System.EventHandler(this.添加节点备注ToolStripMenuItem_Click);
            //
            // 删除节点ToolStripMenuItem
            //
            this.删除节点ToolStripMenuItem.Name = "删除节点ToolStripMenuItem";
            this.删除节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.删除节点ToolStripMenuItem.Text = "删除节点";
            this.删除节点ToolStripMenuItem.Click += new System.EventHandler(this.删除节点ToolStripMenuItem_Click);
            //
            // 删除连线ToolStripMenuItem
            //
            this.删除连线ToolStripMenuItem.Name = "删除连线ToolStripMenuItem";
            this.删除连线ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.删除连线ToolStripMenuItem.Text = "删除连线";
            this.删除连线ToolStripMenuItem.Click += new System.EventHandler(this.删除连线ToolStripMenuItem_Click);
            //
            // buttonRun
            //
            this.buttonRun.BackColor = System.Drawing.SystemColors.ControlLight;
            this.buttonRun.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRun.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonRun.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.buttonRun.Image = global::TDJS_Vision.Properties.Resources.单次执行;
            this.buttonRun.Location = new System.Drawing.Point(355, 2);
            this.buttonRun.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(82, 57);
            this.buttonRun.TabIndex = 1;
            this.buttonRun.UseVisualStyleBackColor = false;
            this.buttonRun.Click += new System.EventHandler(this.button1_Click);
            // 
            // ProcessEditPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.Controls.Add(this.processFlowCanvas);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "ProcessEditPanel";
            this.Size = new System.Drawing.Size(616, 770);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragEnter);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.contextMenuStrip1.ResumeLayout(false);
            this.contextMenuStripFlowCanvas.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private Sunny.UI.UISwitch uiSwitchEnable;
        private TDJS_Vision.Forms.ProcessNew.ProcessFlowCanvas processFlowCanvas;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private Sunny.UI.UILedBulb uiLedBulb1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 设置流程优先级ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 设置流程组别ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 流程重命名ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 是否输出日志ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 是否为触发流程ToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripFlowCanvas;
        private System.Windows.Forms.ToolStripMenuItem 打开节点参数ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 设为起始节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 启用节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 禁用节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 重命名节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 添加节点备注ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 删除节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 删除连线ToolStripMenuItem;
        private System.Windows.Forms.Button buttonClean;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.Button buttonRun;
    }
}
