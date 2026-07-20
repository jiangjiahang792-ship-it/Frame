using TDJS_Vision.Node;

namespace TDJS_Vision.Forms.ProcessNew
{
    partial class FormNewProcessWizard
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormNewProcessWizard));
            gCursorLib.TextShadower textShadower1 = new gCursorLib.TextShadower();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelToolbox = new System.Windows.Forms.Panel();
            this.panelCategoryRail = new System.Windows.Forms.Panel();
            this.flowLayoutPanelCategories = new System.Windows.Forms.FlowLayoutPanel();
            this.panelToolboxHeader = new System.Windows.Forms.Panel();
            this.buttonToggleToolbox = new System.Windows.Forms.Button();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.全部展开ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.全部折叠ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.panelEditorSurface = new System.Windows.Forms.Panel();
            this.panelModuleArea = new System.Windows.Forms.Panel();
            this.flowLayoutPanelModules = new System.Windows.Forms.FlowLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.panelProcessActions = new System.Windows.Forms.Panel();
            this.flowLayoutPanelProcessActions = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.gCursor1 = new gCursorLib.gCursor(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.panelToolbox.SuspendLayout();
            this.panelCategoryRail.SuspendLayout();
            this.panelToolboxHeader.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.panelEditorSurface.SuspendLayout();
            this.panelModuleArea.SuspendLayout();
            this.panelProcessActions.SuspendLayout();
            this.flowLayoutPanelProcessActions.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 74F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.panelToolbox, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelEditorSurface, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1310, 790);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // panelToolbox
            // 
            this.panelToolbox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.panelToolbox.Controls.Add(this.panelCategoryRail);
            this.panelToolbox.Controls.Add(this.treeView1);
            this.panelToolbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelToolbox.Location = new System.Drawing.Point(0, 0);
            this.panelToolbox.Margin = new System.Windows.Forms.Padding(0);
            this.panelToolbox.Name = "panelToolbox";
            this.panelToolbox.Size = new System.Drawing.Size(74, 790);
            this.panelToolbox.TabIndex = 5;
            // 
            // panelCategoryRail
            // 
            this.panelCategoryRail.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(38)))), ((int)(((byte)(43)))));
            this.panelCategoryRail.Controls.Add(this.flowLayoutPanelCategories);
            this.panelCategoryRail.Controls.Add(this.panelToolboxHeader);
            this.panelCategoryRail.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelCategoryRail.Location = new System.Drawing.Point(0, 0);
            this.panelCategoryRail.Name = "panelCategoryRail";
            this.panelCategoryRail.Size = new System.Drawing.Size(74, 790);
            this.panelCategoryRail.TabIndex = 7;
            // 
            // flowLayoutPanelCategories
            // 
            this.flowLayoutPanelCategories.AutoScroll = true;
            this.flowLayoutPanelCategories.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelCategories.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanelCategories.Location = new System.Drawing.Point(0, 54);
            this.flowLayoutPanelCategories.Name = "flowLayoutPanelCategories";
            this.flowLayoutPanelCategories.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.flowLayoutPanelCategories.Size = new System.Drawing.Size(74, 736);
            this.flowLayoutPanelCategories.TabIndex = 8;
            this.flowLayoutPanelCategories.WrapContents = false;
            //
            // panelToolboxHeader
            //
            this.panelToolboxHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(47)))), ((int)(((byte)(52)))), ((int)(((byte)(57)))));
            this.panelToolboxHeader.Controls.Add(this.buttonToggleToolbox);
            this.panelToolboxHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelToolboxHeader.Location = new System.Drawing.Point(0, 0);
            this.panelToolboxHeader.Name = "panelToolboxHeader";
            this.panelToolboxHeader.Size = new System.Drawing.Size(74, 54);
            this.panelToolboxHeader.TabIndex = 4;
            //
            // buttonToggleToolbox
            //
            this.buttonToggleToolbox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(62)))), ((int)(((byte)(68)))));
            this.buttonToggleToolbox.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonToggleToolbox.FlatAppearance.BorderSize = 0;
            this.buttonToggleToolbox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonToggleToolbox.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.buttonToggleToolbox.ForeColor = System.Drawing.Color.White;
            this.buttonToggleToolbox.Location = new System.Drawing.Point(0, 0);
            this.buttonToggleToolbox.Name = "buttonToggleToolbox";
            this.buttonToggleToolbox.Size = new System.Drawing.Size(74, 54);
            this.buttonToggleToolbox.TabIndex = 5;
            this.buttonToggleToolbox.Text = "<";
            this.buttonToggleToolbox.UseVisualStyleBackColor = false;
            this.buttonToggleToolbox.Click += new System.EventHandler(this.buttonToggleToolbox_Click);
            // 
            // treeView1
            // 
            this.treeView1.AllowDrop = true;
            this.treeView1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.treeView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeView1.ContextMenuStrip = this.contextMenuStrip1;
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.treeView1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(231)))), ((int)(((byte)(239)))));
            this.treeView1.HideSelection = false;
            this.treeView1.ImageIndex = 0;
            this.treeView1.ImageList = this.imageList1;
            this.treeView1.Indent = 18;
            this.treeView1.ItemHeight = 32;
            this.treeView1.LineColor = System.Drawing.Color.FromArgb(((int)(((byte)(73)))), ((int)(((byte)(82)))), ((int)(((byte)(91)))));
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.treeView1.Name = "treeView1";
            this.treeView1.SelectedImageIndex = 0;
            this.treeView1.ShowLines = false;
            this.treeView1.Size = new System.Drawing.Size(74, 790);
            this.treeView1.TabIndex = 3;
            this.treeView1.Visible = false;
            this.treeView1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.treeView1_MouseDown);
            this.treeView1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.treeView1_MouseMove);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.全部展开ToolStripMenuItem,
            this.全部折叠ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(153, 64);
            // 
            // 全部展开ToolStripMenuItem
            // 
            this.全部展开ToolStripMenuItem.Name = "全部展开ToolStripMenuItem";
            this.全部展开ToolStripMenuItem.Size = new System.Drawing.Size(152, 30);
            this.全部展开ToolStripMenuItem.Text = "全部展开";
            this.全部展开ToolStripMenuItem.Click += new System.EventHandler(this.全部展开ToolStripMenuItem_Click);
            // 
            // 全部折叠ToolStripMenuItem
            // 
            this.全部折叠ToolStripMenuItem.Name = "全部折叠ToolStripMenuItem";
            this.全部折叠ToolStripMenuItem.Size = new System.Drawing.Size(152, 30);
            this.全部折叠ToolStripMenuItem.Text = "全部折叠";
            this.全部折叠ToolStripMenuItem.Click += new System.EventHandler(this.全部折叠ToolStripMenuItem_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "图像采集");
            this.imageList1.Images.SetKeyName(1, "图像源");
            this.imageList1.Images.SetKeyName(2, "图像显示");
            this.imageList1.Images.SetKeyName(3, "图像处理");
            this.imageList1.Images.SetKeyName(4, "图像裁剪");
            this.imageList1.Images.SetKeyName(5, "图像旋转");
            this.imageList1.Images.SetKeyName(6, "图像分割");
            this.imageList1.Images.SetKeyName(7, "检测识别");
            this.imageList1.Images.SetKeyName(8, "AI检测");
            this.imageList1.Images.SetKeyName(9, "检测直线");
            this.imageList1.Images.SetKeyName(10, "检测圆");
            this.imageList1.Images.SetKeyName(11, "二维码识别");
            this.imageList1.Images.SetKeyName(12, "模版匹配");
            this.imageList1.Images.SetKeyName(13, "极耳检测");
            this.imageList1.Images.SetKeyName(14, "颜色识别");
            this.imageList1.Images.SetKeyName(15, "二值化分析");
            this.imageList1.Images.SetKeyName(16, "测量工具");
            this.imageList1.Images.SetKeyName(17, "通信工具");
            this.imageList1.Images.SetKeyName(18, "打开光源");
            this.imageList1.Images.SetKeyName(19, "相机IO");
            this.imageList1.Images.SetKeyName(20, "相机IO手动控制");
            this.imageList1.Images.SetKeyName(21, "串口发送");
            this.imageList1.Images.SetKeyName(22, "PLC读");
            this.imageList1.Images.SetKeyName(23, "PLC写");
            this.imageList1.Images.SetKeyName(24, "PLC软触发");
            this.imageList1.Images.SetKeyName(25, "客户端请求");
            this.imageList1.Images.SetKeyName(26, "服务器响应");
            this.imageList1.Images.SetKeyName(27, "Modbus读取");
            this.imageList1.Images.SetKeyName(28, "Modbus写入");
            this.imageList1.Images.SetKeyName(29, "Modbus软触发");
            this.imageList1.Images.SetKeyName(30, "科锐IO模块");
            this.imageList1.Images.SetKeyName(31, "监听Flag信号");
            this.imageList1.Images.SetKeyName(32, "逻辑工具");
            this.imageList1.Images.SetKeyName(33, "共享变量");
            this.imageList1.Images.SetKeyName(34, "条件运行");
            this.imageList1.Images.SetKeyName(35, "延迟执行");
            this.imageList1.Images.SetKeyName(36, "触发同组流程");
            this.imageList1.Images.SetKeyName(37, "等待流程完成");
            this.imageList1.Images.SetKeyName(38, "流程信号");
            this.imageList1.Images.SetKeyName(39, "If");
            this.imageList1.Images.SetKeyName(40, "Else");
            this.imageList1.Images.SetKeyName(41, "EndIf");
            this.imageList1.Images.SetKeyName(42, "C#脚本");
            this.imageList1.Images.SetKeyName(43, "弹窗");
            this.imageList1.Images.SetKeyName(44, "结果处理");
            this.imageList1.Images.SetKeyName(45, "发送AI结果");
            this.imageList1.Images.SetKeyName(46, "AI结果绘制");
            this.imageList1.Images.SetKeyName(47, "保存图片");
            this.imageList1.Images.SetKeyName(48, "检测结果显示");
            this.imageList1.Images.SetKeyName(49, "结果汇总");
            this.imageList1.Images.SetKeyName(50, "图片删除");
            this.imageList1.Images.SetKeyName(51, "导出检测表格");
            //
            // panelEditorSurface
            //
            this.panelEditorSurface.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this.panelEditorSurface.Controls.Add(this.panelModuleArea);
            this.panelEditorSurface.Controls.Add(this.tabControl1);
            this.panelEditorSurface.Controls.Add(this.panelProcessActions);
            this.panelEditorSurface.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelEditorSurface.Location = new System.Drawing.Point(74, 0);
            this.panelEditorSurface.Margin = new System.Windows.Forms.Padding(0);
            this.panelEditorSurface.Name = "panelEditorSurface";
            this.panelEditorSurface.Size = new System.Drawing.Size(1236, 790);
            this.panelEditorSurface.TabIndex = 6;
            //
            // panelModuleArea
            //
            this.panelModuleArea.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(27)))), ((int)(((byte)(31)))));
            this.panelModuleArea.Controls.Add(this.flowLayoutPanelModules);
            this.panelModuleArea.Controls.Add(this.label1);
            this.panelModuleArea.Location = new System.Drawing.Point(0, 40);
            this.panelModuleArea.Name = "panelModuleArea";
            this.panelModuleArea.Size = new System.Drawing.Size(286, 650);
            this.panelModuleArea.TabIndex = 8;
            this.panelModuleArea.Visible = false;
            //
            // flowLayoutPanelModules
            //
            this.flowLayoutPanelModules.AllowDrop = true;
            this.flowLayoutPanelModules.AutoScroll = true;
            this.flowLayoutPanelModules.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(27)))), ((int)(((byte)(31)))));
            this.flowLayoutPanelModules.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelModules.Location = new System.Drawing.Point(0, 54);
            this.flowLayoutPanelModules.Name = "flowLayoutPanelModules";
            this.flowLayoutPanelModules.Padding = new System.Windows.Forms.Padding(10, 8, 8, 8);
            this.flowLayoutPanelModules.Size = new System.Drawing.Size(286, 596);
            this.flowLayoutPanelModules.TabIndex = 9;
            //
            // label1
            //
            this.label1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(91)))), ((int)(((byte)(219)))), ((int)(((byte)(213)))));
            this.label1.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Padding = new System.Windows.Forms.Padding(14, 0, 10, 0);
            this.label1.Size = new System.Drawing.Size(286, 54);
            this.label1.TabIndex = 4;
            this.label1.Text = "工具箱";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // tabControl1
            //
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabControl1.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tabControl1.ItemSize = new System.Drawing.Size(128, 40);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1236, 790);
            this.tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl1.TabIndex = 1;
            this.tabControl1.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.tabControl1_DrawItem);
            //
            // panelProcessActions
            //
            this.panelProcessActions.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(47)))), ((int)(((byte)(50)))), ((int)(((byte)(53)))));
            this.panelProcessActions.Controls.Add(this.flowLayoutPanelProcessActions);
            this.panelProcessActions.Location = new System.Drawing.Point(128, 0);
            this.panelProcessActions.Name = "panelProcessActions";
            this.panelProcessActions.Size = new System.Drawing.Size(1108, 40);
            this.panelProcessActions.TabIndex = 7;
            //
            // flowLayoutPanelProcessActions
            //
            this.flowLayoutPanelProcessActions.AutoSize = true;
            this.flowLayoutPanelProcessActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanelProcessActions.Controls.Add(this.buttonAdd);
            this.flowLayoutPanelProcessActions.Controls.Add(this.buttonRemove);
            this.flowLayoutPanelProcessActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flowLayoutPanelProcessActions.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanelProcessActions.Name = "flowLayoutPanelProcessActions";
            this.flowLayoutPanelProcessActions.Size = new System.Drawing.Size(96, 40);
            this.flowLayoutPanelProcessActions.TabIndex = 8;
            this.flowLayoutPanelProcessActions.WrapContents = false;
            //
            // buttonAdd
            //
            this.buttonAdd.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonAdd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(54)))), ((int)(((byte)(58)))));
            this.buttonAdd.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this.buttonAdd.FlatAppearance.BorderSize = 1;
            this.buttonAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonAdd.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(156)))), ((int)(((byte)(162)))));
            this.buttonAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonAdd.Location = new System.Drawing.Point(0, 0);
            this.buttonAdd.Margin = new System.Windows.Forms.Padding(0);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(48, 40);
            this.buttonAdd.TabIndex = 3;
            this.buttonAdd.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.buttonAdd.Text = "+";
            this.buttonAdd.UseVisualStyleBackColor = false;
            this.buttonAdd.Click += new System.EventHandler(this.button2_Click);
            //
            // buttonRemove
            //
            this.buttonRemove.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonRemove.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(54)))), ((int)(((byte)(58)))));
            this.buttonRemove.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(34)))), ((int)(((byte)(38)))));
            this.buttonRemove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonRemove.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(156)))), ((int)(((byte)(162)))));
            this.buttonRemove.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonRemove.Location = new System.Drawing.Point(48, 0);
            this.buttonRemove.Margin = new System.Windows.Forms.Padding(0);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(48, 40);
            this.buttonRemove.TabIndex = 2;
            this.buttonRemove.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.buttonRemove.Text = "-";
            this.buttonRemove.UseVisualStyleBackColor = false;
            this.buttonRemove.Click += new System.EventHandler(this.button1_Click);
            // 
            // gCursor1
            // 
            this.gCursor1.gBlackBitBack = false;
            this.gCursor1.gBoxShadow = true;
            this.gCursor1.gCursorImage = ((System.Drawing.Bitmap)(resources.GetObject("gCursor1.gCursorImage")));
            this.gCursor1.gEffect = gCursorLib.gCursor.eEffect.No;
            this.gCursor1.gFont = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Bold);
            this.gCursor1.gHotSpot = System.Drawing.ContentAlignment.MiddleCenter;
            this.gCursor1.gIBTransp = 80;
            this.gCursor1.gImage = null;
            this.gCursor1.gImageBorderColor = System.Drawing.Color.Black;
            this.gCursor1.gImageBox = new System.Drawing.Size(75, 56);
            this.gCursor1.gImageBoxColor = System.Drawing.Color.White;
            this.gCursor1.gITransp = 0;
            this.gCursor1.gScrolling = gCursorLib.gCursor.eScrolling.No;
            this.gCursor1.gShowImageBox = false;
            this.gCursor1.gShowTextBox = false;
            this.gCursor1.gTBTransp = 80;
            this.gCursor1.gText = "";
            this.gCursor1.gTextAlignment = System.Drawing.ContentAlignment.TopCenter;
            this.gCursor1.gTextAutoFit = gCursorLib.gCursor.eTextAutoFit.None;
            this.gCursor1.gTextBorderColor = System.Drawing.Color.Red;
            this.gCursor1.gTextBox = new System.Drawing.Size(100, 10);
            this.gCursor1.gTextBoxColor = System.Drawing.Color.Blue;
            this.gCursor1.gTextColor = System.Drawing.Color.Blue;
            this.gCursor1.gTextFade = gCursorLib.gCursor.eTextFade.Solid;
            this.gCursor1.gTextMultiline = false;
            this.gCursor1.gTextShadow = false;
            this.gCursor1.gTextShadowColor = System.Drawing.Color.Black;
            textShadower1.Alignment = System.Drawing.ContentAlignment.MiddleCenter;
            textShadower1.Blur = 2F;
            textShadower1.Font = new System.Drawing.Font("Arial", 7F, System.Drawing.FontStyle.Bold);
            textShadower1.Offset = ((System.Drawing.PointF)(resources.GetObject("textShadower1.Offset")));
            textShadower1.Padding = new System.Windows.Forms.Padding(0);
            textShadower1.ShadowColor = System.Drawing.Color.Black;
            textShadower1.ShadowTransp = 128;
            textShadower1.Text = "Drop Shadow";
            textShadower1.TextColor = System.Drawing.Color.Blue;
            this.gCursor1.gTextShadower = textShadower1;
            this.gCursor1.gTTransp = 0;
            this.gCursor1.gType = gCursorLib.gCursor.eType.Text;
            // 
            // FormNewProcessWizard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1314, 830);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimumSize = new System.Drawing.Size(1024, 720);
            this.Name = "FormNewProcessWizard";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "流程编辑";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.GiveFeedback += new System.Windows.Forms.GiveFeedbackEventHandler(this.Form1_GiveFeedback);
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panelToolbox.ResumeLayout(false);
            this.panelCategoryRail.ResumeLayout(false);
            this.panelToolboxHeader.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.panelEditorSurface.ResumeLayout(false);
            this.panelModuleArea.ResumeLayout(false);
            this.panelProcessActions.ResumeLayout(false);
            this.flowLayoutPanelProcessActions.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Panel panelEditorSurface;
        private System.Windows.Forms.Panel panelProcessActions;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelProcessActions;
        private System.Windows.Forms.Panel panelToolbox;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.Panel panelModuleArea;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelModules;
        private System.Windows.Forms.Panel panelCategoryRail;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelCategories;
        private System.Windows.Forms.Panel panelToolboxHeader;
        private System.Windows.Forms.Button buttonToggleToolbox;
        private System.Windows.Forms.ImageList imageList1;
        private gCursorLib.gCursor gCursor1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 全部展开ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 全部折叠ToolStripMenuItem;
    }
}
