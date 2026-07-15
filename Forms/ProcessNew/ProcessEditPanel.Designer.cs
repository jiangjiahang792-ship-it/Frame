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
            this.buttonLoop = new System.Windows.Forms.Button();
            this.buttonClean = new System.Windows.Forms.Button();
            this.uiLedBulb1 = new Sunny.UI.UILedBulb();
            this.uiSwitchEnable = new Sunny.UI.UISwitch();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.panelCanvasHost = new System.Windows.Forms.Panel();
            this.processFlowCanvas = new TDJS_Vision.Forms.ProcessNew.ProcessFlowCanvas();
            this.imagePreviewOverlay = new TDJS_Vision.Forms.ProcessNew.FlowImagePreviewOverlay();
            this.minimapOverlay = new TDJS_Vision.Forms.ProcessNew.FlowMinimapOverlay();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.是否启用流程ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设置流程优先级ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设置流程组别ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.流程重命名ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.是否输出日志ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.是否为触发流程ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripFlowCanvas = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.撤销ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.重做ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorCanvasHistory = new System.Windows.Forms.ToolStripSeparator();
            this.复制节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.粘贴节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorCanvasClipboard = new System.Windows.Forms.ToolStripSeparator();
            this.放大ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.缩小ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.还原缩放ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.吸附到网格ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorCanvasZoom = new System.Windows.Forms.ToolStripSeparator();
            this.打开节点参数ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设为起始节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.启用节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.禁用节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.节点是否输出日志ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.重命名节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.添加节点备注ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除节点ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除连线ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buttonRun = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelCanvasHost.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.contextMenuStripFlowCanvas.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this.tableLayoutPanel1.ColumnCount = 7;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel1.Controls.Add(this.buttonStop, 6, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonLoop, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.uiLedBulb1, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonRun, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.label2, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(616, 46);
            this.tableLayoutPanel1.TabIndex = 0;
            this.tableLayoutPanel1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            // 
            // buttonStop
            //
            this.buttonStop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(50)))), ((int)(((byte)(56)))));
            this.buttonStop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonStop.Enabled = false;
            this.buttonStop.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(84)))), ((int)(((byte)(92)))));
            this.buttonStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonStop.ForeColor = System.Drawing.Color.Transparent;
            this.buttonStop.Location = new System.Drawing.Point(566, 8);
            this.buttonStop.Margin = new System.Windows.Forms.Padding(4);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(34, 30);
            this.buttonStop.TabIndex = 5;
            this.buttonStop.UseVisualStyleBackColor = false;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            //
            // buttonLoop
            //
            this.buttonLoop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(50)))), ((int)(((byte)(56)))));
            this.buttonLoop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonLoop.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(84)))), ((int)(((byte)(92)))));
            this.buttonLoop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonLoop.ForeColor = System.Drawing.Color.Transparent;
            this.buttonLoop.Location = new System.Drawing.Point(520, 8);
            this.buttonLoop.Margin = new System.Windows.Forms.Padding(4);
            this.buttonLoop.Name = "buttonLoop";
            this.buttonLoop.Size = new System.Drawing.Size(38, 30);
            this.buttonLoop.TabIndex = 6;
            this.buttonLoop.UseVisualStyleBackColor = false;
            this.buttonLoop.Click += new System.EventHandler(this.buttonLoop_Click);
            // 
            // buttonClean
            // 
            this.buttonClean.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(50)))), ((int)(((byte)(56)))));
            this.buttonClean.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonClean.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(84)))), ((int)(((byte)(92)))));
            this.buttonClean.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonClean.ForeColor = System.Drawing.Color.Transparent;
            this.buttonClean.Location = new System.Drawing.Point(0, 0);
            this.buttonClean.Margin = new System.Windows.Forms.Padding(4);
            this.buttonClean.Name = "buttonClean";
            this.buttonClean.Size = new System.Drawing.Size(38, 30);
            this.buttonClean.TabIndex = 4;
            this.buttonClean.UseVisualStyleBackColor = false;
            this.buttonClean.Click += new System.EventHandler(this.buttonClean_Click);
            // 
            // uiLedBulb1
            // 
            this.uiLedBulb1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.uiLedBulb1.Color = System.Drawing.Color.DarkGray;
            this.uiLedBulb1.Location = new System.Drawing.Point(287, 10);
            this.uiLedBulb1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.uiLedBulb1.Name = "uiLedBulb1";
            this.uiLedBulb1.Size = new System.Drawing.Size(26, 26);
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
            this.uiSwitchEnable.Location = new System.Drawing.Point(0, 0);
            this.uiSwitchEnable.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.uiSwitchEnable.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSwitchEnable.Name = "uiSwitchEnable";
            this.uiSwitchEnable.Size = new System.Drawing.Size(88, 26);
            this.uiSwitchEnable.TabIndex = 0;
            this.uiSwitchEnable.Text = "uiSwitch1";
            this.uiSwitchEnable.ValueChanged += new Sunny.UI.UISwitch.OnValueChanged(this.uiSwitch1_ValueChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(231)))), ((int)(((byte)(239)))));
            this.label1.Location = new System.Drawing.Point(15, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(114, 18);
            this.label1.TabIndex = 2;
            this.label1.Text = "节点数:0";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(172)))), ((int)(((byte)(183)))), ((int)(((byte)(194)))));
            this.label2.Location = new System.Drawing.Point(135, 5);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(154, 36);
            this.label2.TabIndex = 2;
            this.label2.Text = "流程耗时:0ms";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label2.MouseClick += new System.Windows.Forms.MouseEventHandler(this.label1_MouseClick);
            //
            //
            // panelCanvasHost
            //
            this.panelCanvasHost.AllowDrop = true;
            this.panelCanvasHost.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(52)))), ((int)(((byte)(56)))));
            this.panelCanvasHost.Controls.Add(this.processFlowCanvas);
            this.panelCanvasHost.Controls.Add(this.imagePreviewOverlay);
            this.panelCanvasHost.Controls.Add(this.minimapOverlay);
            this.panelCanvasHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCanvasHost.Location = new System.Drawing.Point(0, 46);
            this.panelCanvasHost.Margin = new System.Windows.Forms.Padding(0);
            this.panelCanvasHost.Name = "panelCanvasHost";
            this.panelCanvasHost.Size = new System.Drawing.Size(616, 724);
            this.panelCanvasHost.TabIndex = 1;
            this.panelCanvasHost.DragDrop += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragDrop);
            this.panelCanvasHost.DragEnter += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragEnter);
            this.panelCanvasHost.DragOver += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragOver);
            //
            // processFlowCanvas
            //
            this.processFlowCanvas.AllowDrop = true;
            this.processFlowCanvas.AutoScroll = true;
            this.processFlowCanvas.ContextMenuStrip = this.contextMenuStripFlowCanvas;
            this.processFlowCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.processFlowCanvas.Location = new System.Drawing.Point(0, 0);
            this.processFlowCanvas.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.processFlowCanvas.Name = "processFlowCanvas";
            this.processFlowCanvas.Size = new System.Drawing.Size(616, 724);
            this.processFlowCanvas.TabIndex = 1;
            this.processFlowCanvas.DragDrop += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragDrop);
            this.processFlowCanvas.DragEnter += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragEnter);
            this.processFlowCanvas.DragOver += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragOver);
            //
            // imagePreviewOverlay
            //
            this.imagePreviewOverlay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.imagePreviewOverlay.Location = new System.Drawing.Point(162, 24);
            this.imagePreviewOverlay.Name = "imagePreviewOverlay";
            this.imagePreviewOverlay.Size = new System.Drawing.Size(430, 260);
            this.imagePreviewOverlay.TabIndex = 2;
            this.imagePreviewOverlay.Visible = false;
            //
            // minimapOverlay
            //
            this.minimapOverlay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.minimapOverlay.Location = new System.Drawing.Point(332, 530);
            this.minimapOverlay.Name = "minimapOverlay";
            this.minimapOverlay.Size = new System.Drawing.Size(260, 170);
            this.minimapOverlay.TabIndex = 3;
            this.minimapOverlay.Visible = false;
            //
            // contextMenuStrip1
            //
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.是否启用流程ToolStripMenuItem,
            this.设置流程优先级ToolStripMenuItem,
            this.设置流程组别ToolStripMenuItem,
            this.流程重命名ToolStripMenuItem,
            this.是否输出日志ToolStripMenuItem,
            this.是否为触发流程ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(225, 196);
            this.contextMenuStrip1.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip1_Opening);
            //
            // 是否启用流程ToolStripMenuItem
            //
            this.是否启用流程ToolStripMenuItem.Checked = true;
            this.是否启用流程ToolStripMenuItem.CheckOnClick = true;
            this.是否启用流程ToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.是否启用流程ToolStripMenuItem.Name = "是否启用流程ToolStripMenuItem";
            this.是否启用流程ToolStripMenuItem.Size = new System.Drawing.Size(224, 32);
            this.是否启用流程ToolStripMenuItem.Text = "是否启用流程";
            this.是否启用流程ToolStripMenuItem.Click += new System.EventHandler(this.是否启用流程ToolStripMenuItem_Click);
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
            // contextMenuStripFlowCanvas
            //
            this.contextMenuStripFlowCanvas.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStripFlowCanvas.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.撤销ToolStripMenuItem,
            this.重做ToolStripMenuItem,
            this.toolStripSeparatorCanvasHistory,
            this.复制节点ToolStripMenuItem,
            this.粘贴节点ToolStripMenuItem,
            this.toolStripSeparatorCanvasClipboard,
            this.放大ToolStripMenuItem,
            this.缩小ToolStripMenuItem,
            this.还原缩放ToolStripMenuItem,
            this.吸附到网格ToolStripMenuItem,
            this.toolStripSeparatorCanvasZoom,
            this.打开节点参数ToolStripMenuItem,
            this.设为起始节点ToolStripMenuItem,
            this.启用节点ToolStripMenuItem,
            this.禁用节点ToolStripMenuItem,
            this.节点是否输出日志ToolStripMenuItem,
            this.重命名节点ToolStripMenuItem,
            this.添加节点备注ToolStripMenuItem,
            this.删除节点ToolStripMenuItem,
            this.删除连线ToolStripMenuItem});
            this.contextMenuStripFlowCanvas.Name = "contextMenuStripFlowCanvas";
            this.contextMenuStripFlowCanvas.Size = new System.Drawing.Size(189, 570);
            this.contextMenuStripFlowCanvas.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripFlowCanvas_Opening);
            //
            // 撤销ToolStripMenuItem
            //
            this.撤销ToolStripMenuItem.Name = "撤销ToolStripMenuItem";
            this.撤销ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.撤销ToolStripMenuItem.Text = "撤销";
            this.撤销ToolStripMenuItem.Click += new System.EventHandler(this.撤销ToolStripMenuItem_Click);
            //
            // 重做ToolStripMenuItem
            //
            this.重做ToolStripMenuItem.Name = "重做ToolStripMenuItem";
            this.重做ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.重做ToolStripMenuItem.Text = "重做";
            this.重做ToolStripMenuItem.Click += new System.EventHandler(this.重做ToolStripMenuItem_Click);
            //
            // toolStripSeparatorCanvasHistory
            //
            this.toolStripSeparatorCanvasHistory.Name = "toolStripSeparatorCanvasHistory";
            this.toolStripSeparatorCanvasHistory.Size = new System.Drawing.Size(185, 6);
            //
            // 复制节点ToolStripMenuItem
            //
            this.复制节点ToolStripMenuItem.Name = "复制节点ToolStripMenuItem";
            this.复制节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.复制节点ToolStripMenuItem.Text = "复制节点";
            this.复制节点ToolStripMenuItem.Click += new System.EventHandler(this.复制节点ToolStripMenuItem_Click);
            //
            // 粘贴节点ToolStripMenuItem
            //
            this.粘贴节点ToolStripMenuItem.Name = "粘贴节点ToolStripMenuItem";
            this.粘贴节点ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.粘贴节点ToolStripMenuItem.Text = "粘贴节点";
            this.粘贴节点ToolStripMenuItem.Click += new System.EventHandler(this.粘贴节点ToolStripMenuItem_Click);
            //
            // toolStripSeparatorCanvasClipboard
            //
            this.toolStripSeparatorCanvasClipboard.Name = "toolStripSeparatorCanvasClipboard";
            this.toolStripSeparatorCanvasClipboard.Size = new System.Drawing.Size(185, 6);
            //
            // 放大ToolStripMenuItem
            //
            this.放大ToolStripMenuItem.Name = "放大ToolStripMenuItem";
            this.放大ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.放大ToolStripMenuItem.Text = "放大";
            this.放大ToolStripMenuItem.Click += new System.EventHandler(this.放大ToolStripMenuItem_Click);
            //
            // 缩小ToolStripMenuItem
            //
            this.缩小ToolStripMenuItem.Name = "缩小ToolStripMenuItem";
            this.缩小ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.缩小ToolStripMenuItem.Text = "缩小";
            this.缩小ToolStripMenuItem.Click += new System.EventHandler(this.缩小ToolStripMenuItem_Click);
            //
            // 还原缩放ToolStripMenuItem
            //
            this.还原缩放ToolStripMenuItem.Name = "还原缩放ToolStripMenuItem";
            this.还原缩放ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.还原缩放ToolStripMenuItem.Text = "还原缩放";
            this.还原缩放ToolStripMenuItem.Click += new System.EventHandler(this.还原缩放ToolStripMenuItem_Click);
            //
            // 吸附到网格ToolStripMenuItem
            //
            this.吸附到网格ToolStripMenuItem.CheckOnClick = true;
            this.吸附到网格ToolStripMenuItem.Name = "吸附到网格ToolStripMenuItem";
            this.吸附到网格ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.吸附到网格ToolStripMenuItem.Text = "吸附到网格";
            this.吸附到网格ToolStripMenuItem.Click += new System.EventHandler(this.吸附到网格ToolStripMenuItem_Click);
            //
            // toolStripSeparatorCanvasZoom
            //
            this.toolStripSeparatorCanvasZoom.Name = "toolStripSeparatorCanvasZoom";
            this.toolStripSeparatorCanvasZoom.Size = new System.Drawing.Size(185, 6);
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
            // 节点是否输出日志ToolStripMenuItem
            //
            this.节点是否输出日志ToolStripMenuItem.Checked = true;
            this.节点是否输出日志ToolStripMenuItem.CheckOnClick = true;
            this.节点是否输出日志ToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.节点是否输出日志ToolStripMenuItem.Name = "节点是否输出日志ToolStripMenuItem";
            this.节点是否输出日志ToolStripMenuItem.Size = new System.Drawing.Size(188, 32);
            this.节点是否输出日志ToolStripMenuItem.Text = "是否输出日志";
            this.节点是否输出日志ToolStripMenuItem.Click += new System.EventHandler(this.节点是否输出日志ToolStripMenuItem_Click);
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
            this.buttonRun.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(50)))), ((int)(((byte)(56)))));
            this.buttonRun.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRun.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(84)))), ((int)(((byte)(92)))));
            this.buttonRun.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonRun.ForeColor = System.Drawing.Color.Transparent;
            this.buttonRun.Location = new System.Drawing.Point(474, 8);
            this.buttonRun.Margin = new System.Windows.Forms.Padding(4);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(38, 30);
            this.buttonRun.TabIndex = 1;
            this.buttonRun.UseVisualStyleBackColor = false;
            this.buttonRun.Click += new System.EventHandler(this.button1_Click);
            // 
            // ProcessEditPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.Controls.Add(this.panelCanvasHost);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "ProcessEditPanel";
            this.Size = new System.Drawing.Size(616, 770);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragEnter);
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.NodeEditPanel_DragOver);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.panelCanvasHost.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.contextMenuStripFlowCanvas.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private Sunny.UI.UISwitch uiSwitchEnable;
        private System.Windows.Forms.Panel panelCanvasHost;
        private TDJS_Vision.Forms.ProcessNew.ProcessFlowCanvas processFlowCanvas;
        private TDJS_Vision.Forms.ProcessNew.FlowImagePreviewOverlay imagePreviewOverlay;
        private TDJS_Vision.Forms.ProcessNew.FlowMinimapOverlay minimapOverlay;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private Sunny.UI.UILedBulb uiLedBulb1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 是否启用流程ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 设置流程优先级ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 设置流程组别ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 流程重命名ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 是否输出日志ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 是否为触发流程ToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripFlowCanvas;
        private System.Windows.Forms.ToolStripMenuItem 撤销ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 重做ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCanvasHistory;
        private System.Windows.Forms.ToolStripMenuItem 复制节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 粘贴节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCanvasClipboard;
        private System.Windows.Forms.ToolStripMenuItem 放大ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 缩小ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 还原缩放ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 吸附到网格ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCanvasZoom;
        private System.Windows.Forms.ToolStripMenuItem 打开节点参数ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 设为起始节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 启用节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 禁用节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 节点是否输出日志ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 重命名节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 添加节点备注ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 删除节点ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 删除连线ToolStripMenuItem;
        private System.Windows.Forms.Button buttonClean;
        private System.Windows.Forms.Button buttonLoop;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.Button buttonRun;
    }
}
