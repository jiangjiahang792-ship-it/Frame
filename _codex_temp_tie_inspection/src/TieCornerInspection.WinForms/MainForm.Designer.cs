#nullable enable

using TieCornerInspection.WinForms.Controls;

namespace TieCornerInspection.WinForms;

partial class MainForm
{
    /// <summary>
    /// 保存设计器创建的可释放组件。
    /// </summary>
    private System.ComponentModel.IContainer? components = null;

    /// <summary>
    /// 保存窗体的根布局面板。
    /// </summary>
    private TableLayoutPanel rootLayout = null!;

    /// <summary>
    /// 保存顶部参数滚动面板。
    /// </summary>
    private FlowLayoutPanel parameterPanel = null!;

    /// <summary>
    /// 保存打开图像按钮。
    /// </summary>
    private Button buttonOpen = null!;

    /// <summary>
    /// 保存加载随附样图按钮。
    /// </summary>
    private Button buttonSample = null!;

    /// <summary>
    /// 保存执行检测按钮。
    /// </summary>
    private Button buttonInspect = null!;

    /// <summary>
    /// 保存全部图像放大按钮。
    /// </summary>
    private Button buttonZoomIn = null!;

    /// <summary>
    /// 保存全部图像缩小按钮。
    /// </summary>
    private Button buttonZoomOut = null!;

    /// <summary>
    /// 保存全部图像适应窗口按钮。
    /// </summary>
    private Button buttonResetView = null!;

    /// <summary>
    /// 保存 ROI X 坐标标签。
    /// </summary>
    private Label labelRoiX = null!;

    /// <summary>
    /// 保存 ROI Y 坐标标签。
    /// </summary>
    private Label labelRoiY = null!;

    /// <summary>
    /// 保存 ROI 宽度标签。
    /// </summary>
    private Label labelRoiWidth = null!;

    /// <summary>
    /// 保存 ROI 高度标签。
    /// </summary>
    private Label labelRoiHeight = null!;

    /// <summary>
    /// 保存黑色阈值标签。
    /// </summary>
    private Label labelBlackThreshold = null!;

    /// <summary>
    /// 保存最小面积标签。
    /// </summary>
    private Label labelMinimumArea = null!;

    /// <summary>
    /// 保存最小宽度标签。
    /// </summary>
    private Label labelMinimumWidth = null!;

    /// <summary>
    /// 保存最小高度标签。
    /// </summary>
    private Label labelMinimumHeight = null!;

    /// <summary>
    /// 保存最大高度标签。
    /// </summary>
    private Label labelMaximumHeight = null!;

    /// <summary>
    /// 保存纹理灵敏度标签。
    /// </summary>
    private Label labelTextureSensitivity = null!;

    /// <summary>
    /// 保存闭运算核标签。
    /// </summary>
    private Label labelKernelSize = null!;

    /// <summary>
    /// 保存端头起点修正标签。
    /// </summary>
    private Label labelEndBoundaryOffset = null!;

    /// <summary>
    /// 保存 ROI X 坐标数值框。
    /// </summary>
    private NumericUpDown numericRoiX = null!;

    /// <summary>
    /// 保存 ROI Y 坐标数值框。
    /// </summary>
    private NumericUpDown numericRoiY = null!;

    /// <summary>
    /// 保存 ROI 宽度数值框。
    /// </summary>
    private NumericUpDown numericRoiWidth = null!;

    /// <summary>
    /// 保存 ROI 高度数值框。
    /// </summary>
    private NumericUpDown numericRoiHeight = null!;

    /// <summary>
    /// 保存黑色阈值数值框。
    /// </summary>
    private NumericUpDown numericBlackThreshold = null!;

    /// <summary>
    /// 保存连通域最小面积数值框。
    /// </summary>
    private NumericUpDown numericMinimumArea = null!;

    /// <summary>
    /// 保存连通域最小宽度数值框。
    /// </summary>
    private NumericUpDown numericMinimumWidth = null!;

    /// <summary>
    /// 保存连通域最小高度数值框。
    /// </summary>
    private NumericUpDown numericMinimumHeight = null!;

    /// <summary>
    /// 保存连通域最大高度数值框。
    /// </summary>
    private NumericUpDown numericMaximumHeight = null!;

    /// <summary>
    /// 保存纹理灵敏度数值框。
    /// </summary>
    private NumericUpDown numericTextureSensitivity = null!;

    /// <summary>
    /// 保存闭运算核边长数值框。
    /// </summary>
    private NumericUpDown numericKernelSize = null!;

    /// <summary>
    /// 保存端头起点修正像素数值框。
    /// </summary>
    private NumericUpDown numericEndBoundaryOffset = null!;

    /// <summary>
    /// 保存左右图像区域的分隔容器。
    /// </summary>
    private SplitContainer imageSplitContainer = null!;

    /// <summary>
    /// 保存原图分组框。
    /// </summary>
    private GroupBox sourceGroupBox = null!;

    /// <summary>
    /// 保存可拖动框选 ROI 的原图显示控件。
    /// </summary>
    private ZoomableImageBox pictureSource = null!;

    /// <summary>
    /// 保存右侧结果页签。
    /// </summary>
    private TabControl resultTabControl = null!;

    /// <summary>
    /// 保存检测标注页签。
    /// </summary>
    private TabPage annotatedTabPage = null!;

    /// <summary>
    /// 保存二值图页签。
    /// </summary>
    private TabPage binaryTabPage = null!;

    /// <summary>
    /// 保存端头提取预览页签。
    /// </summary>
    private TabPage endRegionTabPage = null!;

    /// <summary>
    /// 保存检测标注图显示控件。
    /// </summary>
    private ZoomableImageBox pictureAnnotated = null!;

    /// <summary>
    /// 保存二值图显示控件。
    /// </summary>
    private ZoomableImageBox pictureBinary = null!;

    /// <summary>
    /// 保存逐根彩色端头提取预览控件。
    /// </summary>
    private ZoomableImageBox pictureEndRegions = null!;

    /// <summary>
    /// 保存图像缩放和平移操作提示。
    /// </summary>
    private ToolTip imageToolTip = null!;

    /// <summary>
    /// 保存逐区域检测结果表格。
    /// </summary>
    private DataGridView resultGrid = null!;

    /// <summary>
    /// 保存状态栏。
    /// </summary>
    private StatusStrip statusStrip = null!;

    /// <summary>
    /// 保存状态栏文字。
    /// </summary>
    private ToolStripStatusLabel statusLabel = null!;

    /// <summary>
    /// 释放设计器组件。
    /// </summary>
    /// <param name="disposing">是否释放托管资源。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 创建并排列全部 WinForms 控件。
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayout = new TableLayoutPanel();
        parameterPanel = new FlowLayoutPanel();
        buttonOpen = new Button();
        buttonSample = new Button();
        buttonInspect = new Button();
        buttonZoomIn = new Button();
        buttonZoomOut = new Button();
        buttonResetView = new Button();
        labelRoiX = new Label();
        labelRoiY = new Label();
        labelRoiWidth = new Label();
        labelRoiHeight = new Label();
        labelBlackThreshold = new Label();
        labelMinimumArea = new Label();
        labelMinimumWidth = new Label();
        labelMinimumHeight = new Label();
        labelMaximumHeight = new Label();
        labelTextureSensitivity = new Label();
        labelKernelSize = new Label();
        labelEndBoundaryOffset = new Label();
        numericRoiX = new NumericUpDown();
        numericRoiY = new NumericUpDown();
        numericRoiWidth = new NumericUpDown();
        numericRoiHeight = new NumericUpDown();
        numericBlackThreshold = new NumericUpDown();
        numericMinimumArea = new NumericUpDown();
        numericMinimumWidth = new NumericUpDown();
        numericMinimumHeight = new NumericUpDown();
        numericMaximumHeight = new NumericUpDown();
        numericTextureSensitivity = new NumericUpDown();
        numericKernelSize = new NumericUpDown();
        numericEndBoundaryOffset = new NumericUpDown();
        imageSplitContainer = new SplitContainer();
        sourceGroupBox = new GroupBox();
        pictureSource = new ZoomableImageBox();
        resultTabControl = new TabControl();
        annotatedTabPage = new TabPage();
        binaryTabPage = new TabPage();
        endRegionTabPage = new TabPage();
        pictureAnnotated = new ZoomableImageBox();
        pictureBinary = new ZoomableImageBox();
        pictureEndRegions = new ZoomableImageBox();
        resultGrid = new DataGridView();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        imageToolTip = new ToolTip(components);
        rootLayout.SuspendLayout();
        parameterPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numericRoiX).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericRoiY).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericRoiWidth).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericRoiHeight).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericBlackThreshold).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMinimumArea).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMinimumWidth).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMinimumHeight).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMaximumHeight).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericTextureSensitivity).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericKernelSize).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericEndBoundaryOffset).BeginInit();
        ((System.ComponentModel.ISupportInitialize)imageSplitContainer).BeginInit();
        imageSplitContainer.Panel1.SuspendLayout();
        imageSplitContainer.Panel2.SuspendLayout();
        imageSplitContainer.SuspendLayout();
        sourceGroupBox.SuspendLayout();
        resultTabControl.SuspendLayout();
        annotatedTabPage.SuspendLayout();
        binaryTabPage.SuspendLayout();
        endRegionTabPage.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)resultGrid).BeginInit();
        statusStrip.SuspendLayout();
        SuspendLayout();

        rootLayout.BackColor = Color.FromArgb(245, 247, 250);
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(parameterPanel, 0, 0);
        rootLayout.Controls.Add(imageSplitContainer, 0, 1);
        rootLayout.Controls.Add(resultGrid, 0, 2);
        rootLayout.Controls.Add(statusStrip, 0, 3);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.RowCount = 4;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 245F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

        parameterPanel.AutoScroll = true;
        parameterPanel.BackColor = Color.White;
        parameterPanel.Controls.Add(buttonOpen);
        parameterPanel.Controls.Add(buttonSample);
        parameterPanel.Controls.Add(buttonInspect);
        parameterPanel.Controls.Add(buttonZoomIn);
        parameterPanel.Controls.Add(buttonZoomOut);
        parameterPanel.Controls.Add(buttonResetView);
        parameterPanel.Controls.Add(labelRoiX);
        parameterPanel.Controls.Add(numericRoiX);
        parameterPanel.Controls.Add(labelRoiY);
        parameterPanel.Controls.Add(numericRoiY);
        parameterPanel.Controls.Add(labelRoiWidth);
        parameterPanel.Controls.Add(numericRoiWidth);
        parameterPanel.Controls.Add(labelRoiHeight);
        parameterPanel.Controls.Add(numericRoiHeight);
        parameterPanel.Controls.Add(labelBlackThreshold);
        parameterPanel.Controls.Add(numericBlackThreshold);
        parameterPanel.Controls.Add(labelMinimumArea);
        parameterPanel.Controls.Add(numericMinimumArea);
        parameterPanel.Controls.Add(labelMinimumWidth);
        parameterPanel.Controls.Add(numericMinimumWidth);
        parameterPanel.Controls.Add(labelMinimumHeight);
        parameterPanel.Controls.Add(numericMinimumHeight);
        parameterPanel.Controls.Add(labelMaximumHeight);
        parameterPanel.Controls.Add(numericMaximumHeight);
        parameterPanel.Controls.Add(labelTextureSensitivity);
        parameterPanel.Controls.Add(numericTextureSensitivity);
        parameterPanel.Controls.Add(labelKernelSize);
        parameterPanel.Controls.Add(numericKernelSize);
        parameterPanel.Controls.Add(labelEndBoundaryOffset);
        parameterPanel.Controls.Add(numericEndBoundaryOffset);
        parameterPanel.Dock = DockStyle.Fill;
        parameterPanel.Padding = new Padding(10, 10, 10, 6);
        parameterPanel.WrapContents = true;

        ConfigureActionButton(buttonOpen, "打开图片", Color.FromArgb(52, 120, 246));
        buttonOpen.Click += ButtonOpen_Click;
        ConfigureActionButton(buttonSample, "加载样图", Color.FromArgb(92, 105, 125));
        buttonSample.Click += ButtonSample_Click;
        ConfigureActionButton(buttonInspect, "执行检测", Color.FromArgb(23, 158, 104));
        buttonInspect.Click += ButtonInspect_Click;
        ConfigureActionButton(buttonZoomIn, "全部放大", Color.FromArgb(126, 87, 194));
        buttonZoomIn.Click += ButtonZoomIn_Click;
        ConfigureActionButton(buttonZoomOut, "全部缩小", Color.FromArgb(126, 87, 194));
        buttonZoomOut.Click += ButtonZoomOut_Click;
        ConfigureActionButton(buttonResetView, "全部适应", Color.FromArgb(92, 105, 125));
        buttonResetView.Click += ButtonResetView_Click;

        ConfigureParameterLabel(labelRoiX, "区域 X");
        ConfigureParameterLabel(labelRoiY, "区域 Y");
        ConfigureParameterLabel(labelRoiWidth, "区域宽");
        ConfigureParameterLabel(labelRoiHeight, "区域高");
        ConfigureParameterLabel(labelBlackThreshold, "黑色阈值");
        ConfigureParameterLabel(labelMinimumArea, "最小面积");
        ConfigureParameterLabel(labelMinimumWidth, "最小宽");
        ConfigureParameterLabel(labelMinimumHeight, "最小高");
        ConfigureParameterLabel(labelMaximumHeight, "最大高");
        ConfigureParameterLabel(labelTextureSensitivity, "纹理灵敏度");
        ConfigureParameterLabel(labelKernelSize, "闭运算核");
        ConfigureParameterLabel(labelEndBoundaryOffset, "起点修正(px)");

        ConfigureIntegerNumeric(numericRoiX, 0, 100000, 810, 1);
        ConfigureIntegerNumeric(numericRoiY, 0, 100000, 50, 1);
        ConfigureIntegerNumeric(numericRoiWidth, 1, 100000, 147, 1);
        ConfigureIntegerNumeric(numericRoiHeight, 1, 100000, 1125, 1);
        ConfigureIntegerNumeric(numericBlackThreshold, 0, 255, 180, 1);
        ConfigureIntegerNumeric(numericMinimumArea, 1, 1000000, 120, 10);
        ConfigureIntegerNumeric(numericMinimumWidth, 1, 10000, 30, 5);
        ConfigureIntegerNumeric(numericMinimumHeight, 1, 10000, 6, 1);
        ConfigureIntegerNumeric(numericMaximumHeight, 1, 10000, 60, 1);
        ConfigureIntegerNumeric(numericTextureSensitivity, 0, 100, 60, 5);
        ConfigureIntegerNumeric(numericKernelSize, 1, 9, 3, 2);
        ConfigureIntegerNumeric(numericEndBoundaryOffset, -100, 100, 0, 1);

        numericRoiX.ValueChanged += RegionValueChanged;
        numericRoiY.ValueChanged += RegionValueChanged;
        numericRoiWidth.ValueChanged += RegionValueChanged;
        numericRoiHeight.ValueChanged += RegionValueChanged;

        imageSplitContainer.Dock = DockStyle.Fill;
        imageSplitContainer.FixedPanel = FixedPanel.None;
        imageSplitContainer.IsSplitterFixed = false;
        imageSplitContainer.Panel1.Controls.Add(sourceGroupBox);
        imageSplitContainer.Panel2.Controls.Add(resultTabControl);
        imageSplitContainer.SplitterDistance = 760;
        imageSplitContainer.SplitterWidth = 6;

        sourceGroupBox.Controls.Add(pictureSource);
        sourceGroupBox.Dock = DockStyle.Fill;
        sourceGroupBox.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        sourceGroupBox.Padding = new Padding(7);
        sourceGroupBox.Text = "原图（左键框选检测区；滚轮缩放；右键拖动；双击适应窗口）";

        pictureSource.BackColor = Color.FromArgb(30, 33, 38);
        pictureSource.Cursor = Cursors.Cross;
        pictureSource.Dock = DockStyle.Fill;
        pictureSource.EmptyText = "请打开图像\n滚轮缩放，右键拖动，双击适应窗口";
        pictureSource.Paint += PictureSource_Paint;
        pictureSource.MouseDown += PictureSource_MouseDown;
        pictureSource.MouseMove += PictureSource_MouseMove;
        pictureSource.MouseUp += PictureSource_MouseUp;
        imageToolTip.SetToolTip(pictureSource, "鼠标滚轮放大缩小；右键拖动平移；双击恢复适应窗口；左键框选检测区域。 ");

        resultTabControl.Controls.Add(annotatedTabPage);
        resultTabControl.Controls.Add(binaryTabPage);
        resultTabControl.Controls.Add(endRegionTabPage);
        resultTabControl.Dock = DockStyle.Fill;
        resultTabControl.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);

        annotatedTabPage.BackColor = Color.FromArgb(30, 33, 38);
        annotatedTabPage.Controls.Add(pictureAnnotated);
        annotatedTabPage.Padding = new Padding(3);
        annotatedTabPage.Text = "检测标注";

        binaryTabPage.BackColor = Color.FromArgb(30, 33, 38);
        binaryTabPage.Controls.Add(pictureBinary);
        binaryTabPage.Padding = new Padding(3);
        binaryTabPage.Text = "黑色二值图";

        endRegionTabPage.BackColor = Color.FromArgb(30, 33, 38);
        endRegionTabPage.Controls.Add(pictureEndRegions);
        endRegionTabPage.Padding = new Padding(3);
        endRegionTabPage.Text = "端头提取图";

        pictureAnnotated.BackColor = Color.FromArgb(30, 33, 38);
        pictureAnnotated.Dock = DockStyle.Fill;
        pictureAnnotated.EmptyText = "执行检测后显示标注图\n滚轮缩放，右键拖动，双击适应窗口";
        imageToolTip.SetToolTip(pictureAnnotated, "鼠标滚轮放大缩小；右键拖动平移；双击恢复适应窗口。 ");

        pictureBinary.BackColor = Color.FromArgb(30, 33, 38);
        pictureBinary.Dock = DockStyle.Fill;
        pictureBinary.EmptyText = "执行检测后显示二值图\n滚轮缩放，右键拖动，双击适应窗口";
        imageToolTip.SetToolTip(pictureBinary, "鼠标滚轮放大缩小；右键拖动平移；双击恢复适应窗口。 ");

        pictureEndRegions.BackColor = Color.FromArgb(30, 33, 38);
        pictureEndRegions.Dock = DockStyle.Fill;
        pictureEndRegions.EmptyText = "执行检测后显示逐根端头区域\n滚轮缩放，右键拖动，双击适应窗口";
        imageToolTip.SetToolTip(pictureEndRegions, "鼠标滚轮放大缩小；右键拖动平移；双击恢复适应窗口。 ");

        resultGrid.AllowUserToAddRows = false;
        resultGrid.AllowUserToDeleteRows = false;
        resultGrid.AllowUserToResizeRows = false;
        resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        resultGrid.BackgroundColor = Color.White;
        resultGrid.BorderStyle = BorderStyle.Fixed3D;
        resultGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        resultGrid.ColumnHeadersHeight = 34;
        resultGrid.Dock = DockStyle.Fill;
        resultGrid.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        resultGrid.MultiSelect = false;
        resultGrid.ReadOnly = true;
        resultGrid.RowHeadersVisible = false;
        resultGrid.RowTemplate.Height = 30;
        resultGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        resultGrid.Columns.Add("序号", "序号");
        resultGrid.Columns.Add("X", "X");
        resultGrid.Columns.Add("Y", "Y");
        resultGrid.Columns.Add("宽度", "宽度");
        resultGrid.Columns.Add("高度", "高度");
        resultGrid.Columns.Add("区域面积", "区域面积");
        resultGrid.Columns.Add("端头起点X", "端头起点 X");
        resultGrid.Columns.Add("裁剪宽度", "端头裁剪宽度");
        resultGrid.Columns.Add("定位来源", "定位来源");

        statusStrip.BackColor = Color.White;
        statusStrip.Dock = DockStyle.Fill;
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
        statusLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        statusLabel.Spring = true;
        statusLabel.Text = "请打开图像，或点击“加载样图”；滚轮缩放，右键拖动，双击适应窗口。";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1580, 940);
        Controls.Add(rootLayout);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        MinimumSize = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "扎带端头截取 Demo";
        WindowState = FormWindowState.Maximized;

        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        parameterPanel.ResumeLayout(false);
        parameterPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numericRoiX).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericRoiY).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericRoiWidth).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericRoiHeight).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericBlackThreshold).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMinimumArea).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMinimumWidth).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMinimumHeight).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMaximumHeight).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericTextureSensitivity).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericKernelSize).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericEndBoundaryOffset).EndInit();
        imageSplitContainer.Panel1.ResumeLayout(false);
        imageSplitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)imageSplitContainer).EndInit();
        imageSplitContainer.ResumeLayout(false);
        sourceGroupBox.ResumeLayout(false);
        resultTabControl.ResumeLayout(false);
        annotatedTabPage.ResumeLayout(false);
        binaryTabPage.ResumeLayout(false);
        endRegionTabPage.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)resultGrid).EndInit();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
    }

    /// <summary>
    /// 设置顶部主要操作按钮的一致样式。
    /// </summary>
    /// <param name="button">待设置的按钮。</param>
    /// <param name="text">中文按钮文字。</param>
    /// <param name="backColor">按钮背景色。</param>
    private static void ConfigureActionButton(Button button, string text, Color backColor)
    {
        button.AutoSize = false;
        button.BackColor = backColor;
        button.FlatAppearance.BorderSize = 0;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        button.ForeColor = Color.White;
        button.Margin = new Padding(4, 2, 8, 8);
        button.Size = new Size(108, 38);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
    }

    /// <summary>
    /// 设置参数标签的一致样式。
    /// </summary>
    /// <param name="label">待设置的标签。</param>
    /// <param name="text">中文标签文字。</param>
    private static void ConfigureParameterLabel(Label label, string text)
    {
        label.AutoSize = true;
        label.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        label.Margin = new Padding(8, 9, 2, 8);
        label.Text = text;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    /// <summary>
    /// 设置整数参数数值框的一致样式。
    /// </summary>
    /// <param name="control">待设置的数值框。</param>
    /// <param name="minimum">最小值。</param>
    /// <param name="maximum">最大值。</param>
    /// <param name="value">默认值。</param>
    /// <param name="increment">步长。</param>
    private static void ConfigureIntegerNumeric(NumericUpDown control, decimal minimum, decimal maximum, decimal value, decimal increment)
    {
        control.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        control.Margin = new Padding(0, 5, 4, 8);
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.Value = value;
        control.Increment = increment;
        control.Size = new Size(74, 27);
        control.TextAlign = HorizontalAlignment.Right;
    }

}
