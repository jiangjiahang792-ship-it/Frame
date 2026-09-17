using System.Drawing;
using System.Windows.Forms;
namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>仅包含创建参数、轮廓编辑和保存的独立模板设计器。</summary>
    partial class ContourTemplateEditor
    {
        /// <summary>模板编辑主布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>创建参数容器。</summary>
        private TableLayoutPanel parameterLayout;
        /// <summary>创建参数网格。</summary>
        private TableLayoutPanel modelParametersFlow;
        /// <summary>编辑操作工具栏。</summary>
        private FlowLayoutPanel subscriptionPanel;
        /// <summary>模板保存与取消操作。</summary>
        private FlowLayoutPanel footerActions;
        /// <summary>紧凑画笔设置行。</summary>
        private FlowLayoutPanel brushPanel;
        /// <summary>自动对比度布局。</summary>
        private FlowLayoutPanel contrastModePanel;
        /// <summary>创建参数分组。</summary>
        private GroupBox modelParametersGroup;
        /// <summary>加载模板图像按钮。</summary>
        private Button loadImageButton;
        /// <summary>创建模板按钮。</summary>
        private Button createModelButton;
        /// <summary>涂抹删除按钮。</summary>
        private Button eraseButton;
        /// <summary>保存模板按钮。</summary>
        private Button saveButton;
        /// <summary>取消编辑按钮。</summary>
        private Button closeButton;
        /// <summary>画笔直径标签。</summary>
        private Label brushSizeLabel;
        /// <summary>编辑状态提示。</summary>
        private Label statusLabel;
        /// <summary>建模耗时显示。</summary>
        private Label modelTimeLabel;
        /// <summary>金字塔层数标签。</summary>
        private Label modelLevelsLabel;
        /// <summary>创建起始角标签。</summary>
        private Label modelAngleStartLabel;
        /// <summary>创建结束角标签。</summary>
        private Label modelAngleEndLabel;
        /// <summary>角度步长标签。</summary>
        private Label angleStepLabel;
        /// <summary>边缘阈值标签。</summary>
        private Label contrastLabel;
        /// <summary>搜索最小对比度标签。</summary>
        private Label minimumContrastLabel;
        /// <summary>特征数标签。</summary>
        private Label featureCountLabel;
        /// <summary>极性标签。</summary>
        private Label metricLabel;
        /// <summary>画笔直径输入。</summary>
        private NumericUpDown brushSizeNumeric;
        /// <summary>创建金字塔层数输入。</summary>
        private NumericUpDown modelLevelsNumeric;
        /// <summary>创建起始角输入。</summary>
        private NumericUpDown modelAngleStartNumeric;
        /// <summary>创建结束角输入。</summary>
        private NumericUpDown modelAngleEndNumeric;
        /// <summary>创建角度步长输入。</summary>
        private NumericUpDown angleStepNumeric;
        /// <summary>边缘阈值输入。</summary>
        private NumericUpDown contrastNumeric;
        /// <summary>搜索最小对比度输入。</summary>
        private NumericUpDown minimumContrastNumeric;
        /// <summary>特征数输入。</summary>
        private NumericUpDown featureCountNumeric;
        /// <summary>自动对比度开关。</summary>
        private CheckBox autoContrastCheckBox;
        /// <summary>极性选择。</summary>
        private ComboBox metricComboBox;
        /// <summary>模板框选和涂抹画布。</summary>
        private ImageCanvas imageCanvas;
        /// <summary>右侧导航工具栏与单一画布的紧凑布局。</summary>
        private TableLayoutPanel imageLayout;
        /// <summary>缩放操作栏，始终可在涂抹期间使用。</summary>
        private FlowLayoutPanel zoomActions;
        /// <summary>放大当前图像。</summary>
        private Button zoomInButton;
        /// <summary>缩小当前图像。</summary>
        private Button zoomOutButton;
        /// <summary>恢复整图适应画布。</summary>
        private Button fitImageButton;
        /// <summary>实际图像显示比例。</summary>
        private Label zoomLabel;
        /// <summary>鼠标导航操作提示。</summary>
        private Label navigationHint;
        /// <summary>模板图像选择对话框。</summary>
        private OpenFileDialog imageOpenFileDialog;

        /// <summary>释放编辑窗口独占的图像及模型。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _matcher?.Dispose(); _image?.Dispose(); _modelImage?.Dispose(); imageOpenFileDialog?.Dispose(); }
            base.Dispose(disposing);
        }
        /// <summary>创建模板编辑界面，所有控件及布局均保留在设计器文件中。</summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            rootLayout = new TableLayoutPanel();
            parameterLayout = new TableLayoutPanel();
            modelParametersFlow = new TableLayoutPanel();
            subscriptionPanel = new FlowLayoutPanel();
            footerActions = new FlowLayoutPanel();
            brushPanel = new FlowLayoutPanel();
            contrastModePanel = new FlowLayoutPanel();
            modelParametersGroup = new GroupBox();
            loadImageButton = new Button();
            createModelButton = new Button();
            eraseButton = new Button();
            saveButton = new Button();
            closeButton = new Button();
            brushSizeLabel = new Label();
            statusLabel = new Label();
            modelTimeLabel = new Label();
            modelLevelsLabel = new Label();
            modelAngleStartLabel = new Label();
            modelAngleEndLabel = new Label();
            angleStepLabel = new Label();
            contrastLabel = new Label();
            minimumContrastLabel = new Label();
            featureCountLabel = new Label();
            metricLabel = new Label();
            brushSizeNumeric = new NumericUpDown();
            modelLevelsNumeric = new NumericUpDown();
            modelAngleStartNumeric = new NumericUpDown();
            modelAngleEndNumeric = new NumericUpDown();
            angleStepNumeric = new NumericUpDown();
            contrastNumeric = new NumericUpDown();
            minimumContrastNumeric = new NumericUpDown();
            featureCountNumeric = new NumericUpDown();
            autoContrastCheckBox = new CheckBox();
            metricComboBox = new ComboBox();
            imageCanvas = new ImageCanvas();
            imageLayout = new TableLayoutPanel(); zoomActions = new FlowLayoutPanel();
            zoomInButton = new Button(); zoomOutButton = new Button(); fitImageButton = new Button();
            zoomLabel = new Label(); navigationHint = new Label();
            imageOpenFileDialog = new OpenFileDialog();

            rootLayout.Dock = DockStyle.Fill; rootLayout.ColumnCount = 2; rootLayout.RowCount = 1; rootLayout.Padding = new Padding(8);
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footerActions.Dock = DockStyle.Fill; footerActions.Margin = Padding.Empty; footerActions.FlowDirection = FlowDirection.RightToLeft;
            brushPanel.Dock = DockStyle.Fill; brushPanel.Margin = Padding.Empty;
            subscriptionPanel.Dock = DockStyle.Fill; subscriptionPanel.Padding = Padding.Empty; subscriptionPanel.Margin = Padding.Empty;
            loadImageButton.Text = "加载模板图像"; loadImageButton.AutoSize = true; loadImageButton.Height = 30; loadImageButton.Click += LoadImageButton_Click; subscriptionPanel.Controls.Add(loadImageButton);
            createModelButton.Text = "创建模板"; createModelButton.AutoSize = true; createModelButton.Height = 30; createModelButton.Click += CreateModelButton_Click; subscriptionPanel.Controls.Add(createModelButton);
            eraseButton.Text = "涂抹"; eraseButton.AutoSize = true; eraseButton.Height = 30; eraseButton.Click += EraseButton_Click; subscriptionPanel.Controls.Add(eraseButton);
            saveButton.Text = "保存模板"; saveButton.AutoSize = true; saveButton.Height = 30; saveButton.Click += SaveButton_Click; footerActions.Controls.Add(saveButton);
            closeButton.Text = "取消"; closeButton.AutoSize = true; closeButton.Height = 30; closeButton.Click += CloseButton_Click; footerActions.Controls.Add(closeButton);

            brushSizeLabel.Text = "画笔直径"; brushSizeLabel.AutoSize = true; brushSizeLabel.Margin = new Padding(12, 5, 3, 3);
            brushSizeNumeric.Minimum = 1; brushSizeNumeric.Maximum = 200; brushSizeNumeric.Value = 16; brushSizeNumeric.Width = 68;
            brushSizeNumeric.Margin = new Padding(3, 3, 3, 3); brushSizeNumeric.ValueChanged += BrushSizeNumeric_ValueChanged;
            brushPanel.Controls.Add(brushSizeLabel); brushPanel.Controls.Add(brushSizeNumeric);
            parameterLayout.Dock = DockStyle.Fill; parameterLayout.ColumnCount = 1; parameterLayout.RowCount = 6; parameterLayout.Margin = Padding.Empty;
            parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            parameterLayout.Controls.Add(subscriptionPanel, 0, 0); parameterLayout.Controls.Add(modelParametersGroup, 0, 1);
            parameterLayout.Controls.Add(brushPanel, 0, 2); parameterLayout.Controls.Add(footerActions, 0, 3);
            parameterLayout.Controls.Add(statusLabel, 0, 4); parameterLayout.Controls.Add(modelTimeLabel, 0, 5);
        modelParametersGroup.Controls.Add(modelParametersFlow);
        modelParametersGroup.Dock = DockStyle.Fill;
        modelParametersGroup.Text = "模板参数";
        //
        // modelParametersFlow
        //
        modelParametersFlow.Controls.Add(modelLevelsLabel, 0, 0); modelParametersFlow.Controls.Add(modelLevelsNumeric, 1, 0);
        modelParametersFlow.Controls.Add(modelAngleStartLabel, 0, 1); modelParametersFlow.Controls.Add(modelAngleStartNumeric, 1, 1);
        modelParametersFlow.Controls.Add(modelAngleEndLabel, 0, 2); modelParametersFlow.Controls.Add(modelAngleEndNumeric, 1, 2);
        modelParametersFlow.Controls.Add(angleStepLabel, 0, 3); modelParametersFlow.Controls.Add(angleStepNumeric, 1, 3);
        modelParametersFlow.Controls.Add(contrastModePanel, 0, 4); modelParametersFlow.Controls.Add(contrastNumeric, 1, 4);
        modelParametersFlow.Controls.Add(minimumContrastLabel, 0, 5); modelParametersFlow.Controls.Add(minimumContrastNumeric, 1, 5);
        modelParametersFlow.Controls.Add(featureCountLabel, 0, 6); modelParametersFlow.Controls.Add(featureCountNumeric, 1, 6);
        modelParametersFlow.Controls.Add(metricLabel, 0, 7); modelParametersFlow.Controls.Add(metricComboBox, 1, 7);
        modelParametersFlow.ColumnCount = 2;
        modelParametersFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        modelParametersFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        modelParametersFlow.RowCount = 8;
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelLevelsLabel.Anchor = AnchorStyles.Left;
        modelLevelsNumeric.Dock = DockStyle.Fill;
        modelAngleStartLabel.Anchor = AnchorStyles.Left;
        modelAngleStartNumeric.Dock = DockStyle.Fill;
        modelAngleEndLabel.Anchor = AnchorStyles.Left;
        modelAngleEndNumeric.Dock = DockStyle.Fill;
        angleStepLabel.Anchor = AnchorStyles.Left;
        angleStepNumeric.Dock = DockStyle.Fill;
        contrastLabel.Anchor = AnchorStyles.Left;
        contrastNumeric.Dock = DockStyle.Fill;
        minimumContrastLabel.Anchor = AnchorStyles.Left;
        minimumContrastNumeric.Dock = DockStyle.Fill;
        featureCountLabel.Anchor = AnchorStyles.Left;
        featureCountNumeric.Dock = DockStyle.Fill;
        metricLabel.Anchor = AnchorStyles.Left;
        metricComboBox.Dock = DockStyle.Fill;
        modelParametersFlow.Dock = DockStyle.Fill;
        modelParametersFlow.Padding = new Padding(6);
        //
        // modelLevelsLabel
        //
        modelLevelsLabel.AutoSize = true;
        modelLevelsLabel.Margin = new Padding(3, 8, 3, 0);
        modelLevelsLabel.Text = "金字塔层数(0自动)";
        //
        // modelLevelsNumeric
        //
        modelLevelsNumeric.Maximum = 6;
        modelLevelsNumeric.Size = new Size(58, 23);
        //
        // modelAngleStartLabel
        //
        modelAngleStartLabel.AutoSize = true;
        modelAngleStartLabel.Margin = new Padding(3, 8, 3, 0);
        modelAngleStartLabel.Text = "起始角(°)";
        //
        // modelAngleStartNumeric
        //
        modelAngleStartNumeric.DecimalPlaces = 1;
        modelAngleStartNumeric.Maximum = 180;
        modelAngleStartNumeric.Minimum = -180;
        modelAngleStartNumeric.Size = new Size(70, 23);
        modelAngleStartNumeric.Value = -30;
        //
        // modelAngleEndLabel
        //
        modelAngleEndLabel.AutoSize = true;
        modelAngleEndLabel.Margin = new Padding(3, 8, 3, 0);
        modelAngleEndLabel.Text = "结束角(°)";
        //
        // modelAngleEndNumeric
        //
        modelAngleEndNumeric.DecimalPlaces = 1;
        modelAngleEndNumeric.Maximum = 180;
        modelAngleEndNumeric.Minimum = -180;
        modelAngleEndNumeric.Size = new Size(70, 23);
        modelAngleEndNumeric.Value = 30;
        //
        // angleStepLabel
        //
        angleStepLabel.AutoSize = true;
        angleStepLabel.Margin = new Padding(3, 8, 3, 0);
        angleStepLabel.Text = "角度步长(°)";
        //
        // angleStepNumeric
        //
        angleStepNumeric.DecimalPlaces = 1;
        angleStepNumeric.Increment = 0.5M;
        angleStepNumeric.Maximum = 180;
        angleStepNumeric.Minimum = 0.1M;
        angleStepNumeric.Size = new Size(70, 23);
        angleStepNumeric.Value = 1;
        //
        // contrastLabel
        //
        contrastLabel.AutoSize = true;
        contrastLabel.Margin = new Padding(0, 6, 0, 0);
        contrastLabel.Text = "阈值";
        contrastModePanel.Name = "contrastModePanel";
        contrastModePanel.Dock = DockStyle.Fill;
        contrastModePanel.Margin = new Padding(3, 0, 0, 0);
        contrastModePanel.WrapContents = false;
        contrastModePanel.Controls.Add(contrastLabel);
        contrastModePanel.Controls.Add(autoContrastCheckBox);
        autoContrastCheckBox.Name = "autoContrastCheckBox";
        autoContrastCheckBox.Text = "自动";
        autoContrastCheckBox.AutoSize = true;
        autoContrastCheckBox.Margin = new Padding(2, 3, 0, 0);
        autoContrastCheckBox.Checked = true;
        autoContrastCheckBox.CheckedChanged += AutoContrastCheckBox_CheckedChanged;
        //
        // contrastNumeric
        //
        contrastNumeric.DecimalPlaces = 1;
        contrastNumeric.Maximum = 1024;
        contrastNumeric.Minimum = 1;
        contrastNumeric.Size = new Size(74, 23);
        contrastNumeric.Value = 20;
        contrastNumeric.Enabled = false;
        //
        // minimumContrastLabel
        //
        minimumContrastLabel.AutoSize = true;
        minimumContrastLabel.Margin = new Padding(3, 8, 3, 0);
        minimumContrastLabel.Text = "搜索最小对比度";
        //
        // minimumContrastNumeric
        //
        minimumContrastNumeric.DecimalPlaces = 1;
        minimumContrastNumeric.Maximum = 1024;
        minimumContrastNumeric.Size = new Size(74, 23);
        minimumContrastNumeric.Value = 10;
        //
        // featureCountLabel
        //
        featureCountLabel.AutoSize = true;
        featureCountLabel.Margin = new Padding(3, 8, 3, 0);
        featureCountLabel.Text = "特征数(0自动)";
        //
        // featureCountNumeric
        //
        featureCountNumeric.Increment = 50;
        featureCountNumeric.Maximum = 10000;
        featureCountNumeric.Size = new Size(78, 23);
        featureCountNumeric.Value = 300;
        //
        // metricLabel
        //
        metricLabel.AutoSize = true;
        metricLabel.Margin = new Padding(3, 8, 3, 0);
        metricLabel.Text = "梯度极性";
        //
        // metricComboBox
        //
        metricComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        metricComboBox.Items.AddRange(new object[] { "使用极性", "忽略极性" });
        metricComboBox.SelectedIndex = 0;
        metricComboBox.Size = new Size(96, 23);

            modelParametersGroup.Text = "创建参数";
            imageCanvas.Dock = DockStyle.Fill; imageCanvas.Margin = new Padding(8);
            imageCanvas.ConfirmRequested += ImageCanvas_ConfirmRequested; imageCanvas.EditCancelled += ImageCanvas_EditCancelled; imageCanvas.EditChanged += ImageCanvas_EditChanged;
            statusLabel.Dock = DockStyle.Fill; statusLabel.Text = "加载模板图像，左键框选ROI，右键确认创建"; statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            modelTimeLabel.Dock = DockStyle.Fill; modelTimeLabel.Text = "建模耗时：--";
            imageLayout.Dock = DockStyle.Fill; imageLayout.Margin = new Padding(10, 0, 0, 0);
            imageLayout.ColumnCount = 1; imageLayout.RowCount = 2;
            imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            zoomActions.Dock = DockStyle.Fill; zoomActions.Margin = Padding.Empty; zoomActions.WrapContents = false;
            zoomInButton.Text = "放大"; zoomInButton.Size = new Size(48, 28); zoomInButton.Click += ZoomInButton_Click;
            zoomOutButton.Text = "缩小"; zoomOutButton.Size = new Size(48, 28); zoomOutButton.Click += ZoomOutButton_Click;
            fitImageButton.Text = "适应"; fitImageButton.Size = new Size(54, 28); fitImageButton.Click += FitImageButton_Click;
            zoomLabel.Text = "--"; zoomLabel.Size = new Size(66, 28); zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            zoomLabel.Margin = new Padding(3);
            navigationHint.Text = "滚轮缩放 · 中键拖动"; navigationHint.AutoSize = true;
            navigationHint.Margin = new Padding(3, 8, 0, 0); navigationHint.ForeColor = Color.DimGray;
            zoomActions.Controls.AddRange(new Control[] { zoomInButton, zoomOutButton, fitImageButton, zoomLabel, navigationHint });
            imageCanvas.ViewChanged += ImageCanvas_ViewChanged;
            imageCanvas.Margin = Padding.Empty;
            imageLayout.Controls.Add(zoomActions, 0, 0); imageLayout.Controls.Add(imageCanvas, 0, 1);
            rootLayout.Controls.Add(parameterLayout, 0, 0); rootLayout.Controls.Add(imageLayout, 1, 0);
            statusLabel.ForeColor = Color.DimGray; modelTimeLabel.ForeColor = Color.DimGray;
            imageOpenFileDialog.Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*";
            AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Microsoft YaHei UI",9F);
            ClientSize = new Size(860,560); MinimumSize = new Size(800,540); StartPosition = FormStartPosition.CenterParent;
            Text = "特征模板编辑"; Padding = new Padding(1); Controls.Add(rootLayout); rootLayout.BringToFront(); ResumeLayout(true);
        }
    }
}
