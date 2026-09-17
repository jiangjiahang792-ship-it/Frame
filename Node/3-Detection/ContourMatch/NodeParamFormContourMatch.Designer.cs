using System.Drawing;
using System.Windows.Forms;
namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>三页参数与预览布局，所有控件均在设计器文件中声明和初始化。</summary>
    partial class NodeParamFormContourMatch
    {
        /// <summary>窗体主布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>参数与图像分区。</summary>
        private SplitContainer contentSplit;
        /// <summary>三页参数导航。</summary>
        private TabControl parameterTabs;
        /// <summary>基础参数页面。</summary>
        private TabPage basicPage;
        /// <summary>特征模板页面。</summary>
        private TabPage templatesPage;
        /// <summary>运行参数页面。</summary>
        private TabPage runtimePage;
        /// <summary>基础参数布局。</summary>
        private TableLayoutPanel basicLayout;
        /// <summary>输入图像标题。</summary>
        private Label inputLabel;
        /// <summary>输入图像订阅。</summary>
        private NodeSubscription imageSubscription;
        /// <summary>图像操作。</summary>
        private FlowLayoutPanel inputActions;
        /// <summary>刷新订阅图像。</summary>
        private Button refreshImageButton;
        /// <summary>加载本地图像。</summary>
        private Button loadImageButton;
        /// <summary>搜索区域标题。</summary>
        private Label regionTitle;
        /// <summary>全图搜索选择。</summary>
        private CheckBox allSearchCheckBox;
        /// <summary>绘制搜索区域。</summary>
        private Button drawRegionButton;
        /// <summary>已确认搜索区域说明。</summary>
        private Label regionLabel;
        /// <summary>位置修正启用。</summary>
        private CheckBox correctionCheckBox;
        /// <summary>位置修正数据订阅。</summary>
        private NodeSubscription correctionSubscription;
        /// <summary>位置修正说明。</summary>
        private Label correctionHint;
        /// <summary>模板管理布局。</summary>
        private TableLayoutPanel templatesLayout;
        /// <summary>模板管理工具栏。</summary>
        private FlowLayoutPanel templateActions;
        /// <summary>创建模板。</summary>
        private Button createTemplateButton;
        /// <summary>编辑模板。</summary>
        private Button editTemplateButton;
        /// <summary>删除选中。</summary>
        private Button deleteTemplateButton;
        /// <summary>删除全部。</summary>
        private Button clearTemplatesButton;
        /// <summary>导入模板。</summary>
        private Button importTemplatesButton;
        /// <summary>导出选中。</summary>
        private Button exportTemplatesButton;
        /// <summary>可启用和重命名的模板列表。</summary>
        private DataGridView templatesGrid;
        /// <summary>图像预览容器，分开保存搜索图与模板图。</summary>
        private Panel previewSurface;
        /// <summary>选中模板的图像与实际轮廓。</summary>
        private ImageCanvas templateCanvas;
        /// <summary>模板信息与编辑说明。</summary>
        private Label templateSummary;
        /// <summary>Demo搜索参数布局。</summary>
        private TableLayoutPanel runtimeLayout;
        /// <summary>搜索起始角（度）标签。</summary>
        private Label runtimeLabel0;
        /// <summary>搜索起始角（度）。</summary>
        private NumericUpDown findAngleStartNumeric;
        /// <summary>搜索结束角（度）标签。</summary>
        private Label runtimeLabel1;
        /// <summary>搜索结束角（度）。</summary>
        private NumericUpDown findAngleEndNumeric;
        /// <summary>最小匹配分数标签。</summary>
        private Label runtimeLabel2;
        /// <summary>最小匹配分数。</summary>
        private NumericUpDown minimumScoreNumeric;
        /// <summary>最大匹配个数标签。</summary>
        private Label runtimeLabel3;
        /// <summary>最大匹配个数。</summary>
        private NumericUpDown maximumMatchesNumeric;
        /// <summary>最大重叠率标签。</summary>
        private Label runtimeLabel4;
        /// <summary>最大重叠率。</summary>
        private NumericUpDown maximumOverlapNumeric;
        /// <summary>搜索层数（0沿用模型）标签。</summary>
        private Label runtimeLabel5;
        /// <summary>搜索层数（0沿用模型）。</summary>
        private NumericUpDown findLevelsNumeric;
        /// <summary>运行模式标签。</summary>
        private Label modeLabel;
        /// <summary>Demo三种运行模式。</summary>
        private ComboBox modeComboBox;
        /// <summary>亚像素精修。</summary>
        private CheckBox subPixelCheckBox;
        /// <summary>参数应用说明。</summary>
        private Label runtimeHint;
        /// <summary>搜索图像与结果布局。</summary>
        private TableLayoutPanel previewLayout;
        /// <summary>搜索图像与区域画布。</summary>
        private ImageCanvas imageCanvas;
        /// <summary>匹配结果及来源模板。</summary>
        private DataGridView resultsGrid;
        /// <summary>底部操作栏。</summary>
        private FlowLayoutPanel footerActions;
        /// <summary>确定并关闭窗口。</summary>
        private Button closeButton;
        /// <summary>执行。</summary>
        private Button executeButton;
        /// <summary>状态提示。</summary>
        private Label statusLabel;
        /// <summary>本地图像选择。</summary>
        private OpenFileDialog imageOpenDialog;
        /// <summary>模板导入选择。</summary>
        private OpenFileDialog templateOpenDialog;
        /// <summary>模板导出选择。</summary>
        private SaveFileDialog templateSaveDialog;
        /// <summary>释放编辑图像、预览缓存及生产模板会话。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _previewSession.Dispose(); _runtimeSession.Dispose(); _image?.Dispose(); _templateImage?.Dispose(); imageOpenDialog.Dispose(); templateOpenDialog.Dispose(); templateSaveDialog.Dispose(); }
            base.Dispose(disposing);
        }
        /// <summary>创建可在WinForms设计器中查看的控件和布局。</summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            rootLayout = new TableLayoutPanel();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            contentSplit = new SplitContainer();
            contentSplit.Dock = DockStyle.Fill;
            contentSplit.Size = new Size(980, 640);
            contentSplit.SplitterDistance = 450;
            contentSplit.Panel1MinSize = 425;
            contentSplit.Panel2MinSize = 350;
            parameterTabs = new TabControl();
            parameterTabs.Dock = DockStyle.Fill;
            parameterTabs.Font = new Font("Microsoft YaHei UI", 10F);
            parameterTabs.SizeMode = TabSizeMode.Fixed;
            parameterTabs.ItemSize = new Size(126, 36);
            basicPage = new TabPage();
            basicPage.Text = "基础参数";
            basicPage.Padding = new Padding(10);
            basicPage.BackColor = Color.WhiteSmoke;
            parameterTabs.TabPages.Add(basicPage);
            templatesPage = new TabPage();
            templatesPage.Text = "特征模板";
            templatesPage.Padding = new Padding(10);
            templatesPage.BackColor = Color.WhiteSmoke;
            parameterTabs.TabPages.Add(templatesPage);
            runtimePage = new TabPage();
            runtimePage.Text = "运行参数";
            runtimePage.Padding = new Padding(10);
            runtimePage.BackColor = Color.WhiteSmoke;
            parameterTabs.TabPages.Add(runtimePage);
            contentSplit.Panel1.Controls.Add(parameterTabs); rootLayout.Controls.Add(contentSplit, 0, 0);
            basicLayout = new TableLayoutPanel();
            basicLayout.Dock = DockStyle.Top;
            basicLayout.AutoSize = true;
            basicLayout.ColumnCount = 1;
            basicLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            basicPage.Controls.Add(basicLayout);
            inputLabel = new Label();
            inputLabel.Text = "输入图像";
            inputLabel.AutoSize = true;
            inputLabel.Margin = new Padding(3, 8, 3, 10);
            imageSubscription = new NodeSubscription();
            imageSubscription.Dock = DockStyle.Top;
            imageSubscription.Height = 58;
            inputActions = new FlowLayoutPanel();
            inputActions.Dock = DockStyle.Top;
            inputActions.AutoSize = true;
            refreshImageButton = new Button();
            refreshImageButton.Text = "刷新订阅图像";
            refreshImageButton.AutoSize = true;
            refreshImageButton.Height = 34;
            refreshImageButton.Click += RefreshImageButton_Click; inputActions.Controls.Add(refreshImageButton);
            loadImageButton = new Button();
            loadImageButton.Text = "加载本地图像";
            loadImageButton.AutoSize = true;
            loadImageButton.Height = 34;
            loadImageButton.Click += LoadImageButton_Click; inputActions.Controls.Add(loadImageButton);
            regionTitle = new Label();
            regionTitle.Text = "搜索区域";
            regionTitle.AutoSize = true;
            regionTitle.Margin = new Padding(3, 22, 3, 8);
            allSearchCheckBox = new CheckBox();
            allSearchCheckBox.Text = "全图搜索";
            allSearchCheckBox.Checked = true;
            allSearchCheckBox.AutoSize = true;
            allSearchCheckBox.CheckedChanged += SearchMode_Changed;
            drawRegionButton = new Button();
            drawRegionButton.Text = "绘制搜索区域";
            drawRegionButton.AutoSize = true;
            drawRegionButton.Height = 34;
            drawRegionButton.Click += DrawRegionButton_Click;
            regionLabel = new Label();
            regionLabel.Text = "当前搜索整幅图像";
            regionLabel.AutoSize = true;
            regionLabel.MaximumSize = new Size(375, 0);
            regionLabel.Margin = new Padding(3, 8, 3, 18);
            correctionCheckBox = new CheckBox();
            correctionCheckBox.Text = "启用位置修正";
            correctionCheckBox.AutoSize = true;
            correctionCheckBox.CheckedChanged += CorrectionCheckBox_Changed;
            correctionSubscription = new NodeSubscription();
            correctionSubscription.Dock = DockStyle.Top;
            correctionSubscription.Height = 58;
            correctionSubscription.Enabled = false;
            correctionHint = new Label();
            correctionHint.Text = "先在基准图上绘制搜索区域，再订阅位置修正信息。运行时区域跟随目标移动和旋转。";
            correctionHint.AutoSize = true;
            correctionHint.MaximumSize = new Size(375, 0);
            correctionHint.ForeColor = Color.DimGray;
            correctionHint.Margin = new Padding(3, 8, 3, 3);
            basicLayout.Controls.Add(inputLabel);
            basicLayout.Controls.Add(imageSubscription);
            basicLayout.Controls.Add(inputActions);
            basicLayout.Controls.Add(regionTitle);
            basicLayout.Controls.Add(allSearchCheckBox);
            basicLayout.Controls.Add(drawRegionButton);
            basicLayout.Controls.Add(regionLabel);
            basicLayout.Controls.Add(correctionCheckBox);
            basicLayout.Controls.Add(correctionSubscription);
            basicLayout.Controls.Add(correctionHint);
            templatesLayout = new TableLayoutPanel();
            templatesLayout.Dock = DockStyle.Fill;
            templatesLayout.ColumnCount = 1;
            templatesLayout.RowCount = 3;
            templatesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); templatesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); templatesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); templatesPage.Controls.Add(templatesLayout);
            templateActions = new FlowLayoutPanel();
            templateActions.Dock = DockStyle.Fill;
            createTemplateButton = new Button();
            createTemplateButton.Text = "创建模板";
            createTemplateButton.AutoSize = false; createTemplateButton.Width = 114;
            createTemplateButton.Height = 30;
            createTemplateButton.Click += CreateTemplateButton_Click; templateActions.Controls.Add(createTemplateButton);
            editTemplateButton = new Button();
            editTemplateButton.Text = "编辑模板";
            editTemplateButton.AutoSize = false; editTemplateButton.Width = 114;
            editTemplateButton.Height = 30;
            editTemplateButton.Click += EditTemplateButton_Click; templateActions.Controls.Add(editTemplateButton);
            deleteTemplateButton = new Button();
            deleteTemplateButton.Text = "删除选中";
            deleteTemplateButton.AutoSize = false; deleteTemplateButton.Width = 114;
            deleteTemplateButton.Height = 30;
            deleteTemplateButton.Click += DeleteTemplateButton_Click; templateActions.Controls.Add(deleteTemplateButton);
            clearTemplatesButton = new Button();
            clearTemplatesButton.Text = "删除全部";
            clearTemplatesButton.AutoSize = false; clearTemplatesButton.Width = 114;
            clearTemplatesButton.Height = 30;
            clearTemplatesButton.Click += ClearTemplatesButton_Click; templateActions.Controls.Add(clearTemplatesButton);
            importTemplatesButton = new Button();
            importTemplatesButton.Text = "导入模板";
            importTemplatesButton.AutoSize = false; importTemplatesButton.Width = 114;
            importTemplatesButton.Height = 30;
            importTemplatesButton.Click += ImportTemplatesButton_Click; templateActions.Controls.Add(importTemplatesButton);
            exportTemplatesButton = new Button();
            exportTemplatesButton.Text = "导出选中";
            exportTemplatesButton.AutoSize = false; exportTemplatesButton.Width = 114;
            exportTemplatesButton.Height = 30;
            exportTemplatesButton.Click += ExportTemplatesButton_Click; templateActions.Controls.Add(exportTemplatesButton);
            templatesGrid = new DataGridView();
            templatesGrid.Dock = DockStyle.Fill;
            templatesGrid.AllowUserToAddRows = false;
            templatesGrid.AllowUserToDeleteRows = false;
            templatesGrid.RowHeadersVisible = false;
            templatesGrid.MultiSelect = false;
            templatesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            templatesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            templatesGrid.BackgroundColor = Color.White;
            templatesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "enabledColumn", HeaderText = "启用", FillWeight = 25 });
            templatesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "nameColumn", HeaderText = "模板名称", FillWeight = 100 });
            templatesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "sizeColumn", HeaderText = "尺寸", ReadOnly = true, FillWeight = 50 });
            templatesGrid.CurrentCellDirtyStateChanged += TemplatesGrid_CurrentCellDirtyStateChanged; templatesGrid.CellValueChanged += TemplatesGrid_CellValueChanged; templatesGrid.SelectionChanged += TemplatesGrid_SelectionChanged;
            templateSummary = new Label(); templateSummary.Dock = DockStyle.Fill; templateSummary.Padding = new Padding(4, 6, 4, 0);
            templateSummary.ForeColor = Color.DimGray; templateSummary.Text = "勾选启用模板，选中后在右侧查看图像与轮廓。";
            templatesLayout.Controls.Add(templateActions, 0, 0); templatesLayout.Controls.Add(templatesGrid, 0, 1); templatesLayout.Controls.Add(templateSummary, 0, 2);
            runtimeLayout = new TableLayoutPanel();
            runtimeLayout.Dock = DockStyle.Top;
            runtimeLayout.AutoSize = true;
            runtimeLayout.ColumnCount = 2;
            runtimeLayout.Padding = new Padding(8);
            runtimeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); runtimeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); runtimePage.Controls.Add(runtimeLayout);
            runtimeLabel0 = new Label();
            runtimeLabel0.Text = "搜索起始角（度）";
            runtimeLabel0.AutoSize = true;
            runtimeLabel0.Margin = new Padding(3, 10, 3, 10);
            findAngleStartNumeric = new NumericUpDown();
            findAngleStartNumeric.Minimum = -180M;
            findAngleStartNumeric.Maximum = 180M;
            findAngleStartNumeric.Value = -30M;
            findAngleStartNumeric.DecimalPlaces = 1;
            findAngleStartNumeric.Increment = 1M;
            findAngleStartNumeric.Dock = DockStyle.Fill;
            findAngleStartNumeric.Margin = new Padding(3, 8, 3, 8);
            runtimeLayout.Controls.Add(runtimeLabel0, 0, 0); runtimeLayout.Controls.Add(findAngleStartNumeric, 1, 0);
            runtimeLabel1 = new Label();
            runtimeLabel1.Text = "搜索结束角（度）";
            runtimeLabel1.AutoSize = true;
            runtimeLabel1.Margin = new Padding(3, 10, 3, 10);
            findAngleEndNumeric = new NumericUpDown();
            findAngleEndNumeric.Minimum = -180M;
            findAngleEndNumeric.Maximum = 180M;
            findAngleEndNumeric.Value = 30M;
            findAngleEndNumeric.DecimalPlaces = 1;
            findAngleEndNumeric.Increment = 1M;
            findAngleEndNumeric.Dock = DockStyle.Fill;
            findAngleEndNumeric.Margin = new Padding(3, 8, 3, 8);
            runtimeLayout.Controls.Add(runtimeLabel1, 0, 1); runtimeLayout.Controls.Add(findAngleEndNumeric, 1, 1);
            runtimeLabel2 = new Label();
            runtimeLabel2.Text = "最小匹配分数";
            runtimeLabel2.AutoSize = true;
            runtimeLabel2.Margin = new Padding(3, 10, 3, 10);
            minimumScoreNumeric = new NumericUpDown();
            minimumScoreNumeric.Minimum = 0M;
            minimumScoreNumeric.Maximum = 1M;
            minimumScoreNumeric.Value = 0.65M;
            minimumScoreNumeric.DecimalPlaces = 2;
            minimumScoreNumeric.Increment = 0.01M;
            minimumScoreNumeric.Dock = DockStyle.Fill;
            minimumScoreNumeric.Margin = new Padding(3, 8, 3, 8);
            runtimeLayout.Controls.Add(runtimeLabel2, 0, 2); runtimeLayout.Controls.Add(minimumScoreNumeric, 1, 2);
            runtimeLabel3 = new Label();
            runtimeLabel3.Text = "最大匹配个数";
            runtimeLabel3.AutoSize = true;
            runtimeLabel3.Margin = new Padding(3, 10, 3, 10);
            maximumMatchesNumeric = new NumericUpDown();
            maximumMatchesNumeric.Minimum = 1M;
            maximumMatchesNumeric.Maximum = 100M;
            maximumMatchesNumeric.Value = 5M;
            maximumMatchesNumeric.DecimalPlaces = 0;
            maximumMatchesNumeric.Increment = 1M;
            maximumMatchesNumeric.Dock = DockStyle.Fill;
            maximumMatchesNumeric.Margin = new Padding(3, 8, 3, 8);
            runtimeLayout.Controls.Add(runtimeLabel3, 0, 3); runtimeLayout.Controls.Add(maximumMatchesNumeric, 1, 3);
            runtimeLabel4 = new Label();
            runtimeLabel4.Text = "最大重叠率";
            runtimeLabel4.AutoSize = true;
            runtimeLabel4.Margin = new Padding(3, 10, 3, 10);
            maximumOverlapNumeric = new NumericUpDown();
            maximumOverlapNumeric.Minimum = 0M;
            maximumOverlapNumeric.Maximum = 1M;
            maximumOverlapNumeric.Value = 0.3M;
            maximumOverlapNumeric.DecimalPlaces = 2;
            maximumOverlapNumeric.Increment = 0.01M;
            maximumOverlapNumeric.Dock = DockStyle.Fill;
            maximumOverlapNumeric.Margin = new Padding(3, 8, 3, 8);
            runtimeLayout.Controls.Add(runtimeLabel4, 0, 4); runtimeLayout.Controls.Add(maximumOverlapNumeric, 1, 4);
            runtimeLabel5 = new Label();
            runtimeLabel5.Text = "搜索层数（0沿用模型）";
            runtimeLabel5.AutoSize = true;
            runtimeLabel5.Margin = new Padding(3, 10, 3, 10);
            findLevelsNumeric = new NumericUpDown();
            findLevelsNumeric.Minimum = 0M;
            findLevelsNumeric.Maximum = 6M;
            findLevelsNumeric.Value = 0M;
            findLevelsNumeric.DecimalPlaces = 0;
            findLevelsNumeric.Increment = 1M;
            findLevelsNumeric.Dock = DockStyle.Fill;
            findLevelsNumeric.Margin = new Padding(3, 8, 3, 8);
            runtimeLayout.Controls.Add(runtimeLabel5, 0, 5); runtimeLayout.Controls.Add(findLevelsNumeric, 1, 5);
            modeLabel = new Label();
            modeLabel.Text = "运行模式";
            modeLabel.AutoSize = true;
            modeLabel.Margin = new Padding(3, 10, 3, 10);
            modeComboBox = new ComboBox();
            modeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeComboBox.Dock = DockStyle.Fill;
            modeComboBox.Margin = new Padding(3, 8, 3, 8);
            modeComboBox.Items.AddRange(new object[] { "快速", "平衡", "高精度" }); modeComboBox.SelectedIndex = 1; runtimeLayout.Controls.Add(modeLabel, 0, 6); runtimeLayout.Controls.Add(modeComboBox, 1, 6);
            subPixelCheckBox = new CheckBox();
            subPixelCheckBox.Text = "启用亚像素精修";
            subPixelCheckBox.Checked = true;
            subPixelCheckBox.AutoSize = true;
            subPixelCheckBox.Margin = new Padding(3, 10, 3, 10);
            runtimeLayout.Controls.Add(subPixelCheckBox, 0, 7); runtimeLayout.SetColumnSpan(subPixelCheckBox, 2);
            runtimeHint = new Label();
            runtimeHint.Text = "运行参数对所有启用模板生效。最大匹配个数为合并结果的总数上限，重复目标按得分保留。";
            runtimeHint.AutoSize = true;
            runtimeHint.MaximumSize = new Size(360, 0);
            runtimeHint.ForeColor = Color.DimGray;
            runtimeHint.Margin = new Padding(3, 18, 3, 3);
            runtimeLayout.Controls.Add(runtimeHint, 0, 8); runtimeLayout.SetColumnSpan(runtimeHint, 2);
            previewLayout = new TableLayoutPanel();
            previewLayout.Dock = DockStyle.Fill;
            previewLayout.ColumnCount = 1;
            previewLayout.RowCount = 2;
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155)); contentSplit.Panel2.Controls.Add(previewLayout);
            imageCanvas = new ImageCanvas();
            imageCanvas.Dock = DockStyle.Fill;
            imageCanvas.ConfirmRequested += SearchRegion_ConfirmRequested; imageCanvas.EditCancelled += SearchRegion_EditCancelled;
            resultsGrid = new DataGridView();
            resultsGrid.Dock = DockStyle.Fill;
            resultsGrid.ReadOnly = true;
            resultsGrid.AllowUserToAddRows = false;
            resultsGrid.AllowUserToDeleteRows = false;
            resultsGrid.RowHeadersVisible = false;
            resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            resultsGrid.BackgroundColor = Color.White;
            resultsGrid.Columns.Add("template", "模板");
            resultsGrid.Columns.Add("x", "中心X");
            resultsGrid.Columns.Add("y", "中心Y");
            resultsGrid.Columns.Add("angle", "角度");
            resultsGrid.Columns.Add("score", "分数");
            previewSurface = new Panel(); previewSurface.Dock = DockStyle.Fill; previewSurface.Margin = Padding.Empty;
            templateCanvas = new ImageCanvas(); templateCanvas.Dock = DockStyle.Fill; templateCanvas.Visible = false;
            previewSurface.Controls.Add(imageCanvas); previewSurface.Controls.Add(templateCanvas);
            previewLayout.Controls.Add(previewSurface, 0, 0); previewLayout.Controls.Add(resultsGrid, 0, 1);
            parameterTabs.SelectedIndexChanged += ParameterTabs_SelectedIndexChanged;
            footerActions = new FlowLayoutPanel();
            footerActions.Dock = DockStyle.Fill;
            footerActions.FlowDirection = FlowDirection.RightToLeft;
            closeButton = new Button();
            closeButton.Text = "确定";
            closeButton.Width = 105;
            closeButton.Height = 34;
            closeButton.Click += CloseButton_Click; footerActions.Controls.Add(closeButton);
            executeButton = new Button();
            executeButton.Text = "执行";
            executeButton.Width = 105;
            executeButton.Height = 34;
            executeButton.Click += ExecuteButton_Click; footerActions.Controls.Add(executeButton);
            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Text = "请选择输入图像，创建或导入特征模板";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            rootLayout.Controls.Add(footerActions, 0, 1); rootLayout.Controls.Add(statusLabel, 0, 2);
            imageOpenDialog = new OpenFileDialog();
            imageOpenDialog.Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*";
            templateOpenDialog = new OpenFileDialog();
            templateOpenDialog.Filter = "轮廓模板文件|*.contour.json|所有文件|*.*";
            templateSaveDialog = new SaveFileDialog();
            templateSaveDialog.Filter = "轮廓模板文件|*.contour.json";
            templateSaveDialog.DefaultExt = "contour.json";
            templateSaveDialog.AddExtension = true;
            findAngleStartNumeric.ValueChanged += ParameterValue_Changed;
            findAngleEndNumeric.ValueChanged += ParameterValue_Changed;
            minimumScoreNumeric.ValueChanged += ParameterValue_Changed;
            maximumMatchesNumeric.ValueChanged += ParameterValue_Changed;
            maximumOverlapNumeric.ValueChanged += ParameterValue_Changed;
            findLevelsNumeric.ValueChanged += ParameterValue_Changed;
            modeComboBox.SelectedIndexChanged += ParameterValue_Changed; subPixelCheckBox.CheckedChanged += ParameterValue_Changed;
            imageSubscription.SelectionChanged += ParameterValue_Changed; correctionSubscription.SelectionChanged += ParameterValue_Changed;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F); Text = "轮廓模板匹配";
            ClientSize = new Size(980, 680); MinimumSize = new Size(920, 640);
            StartPosition = FormStartPosition.CenterScreen; Padding = new Padding(1);
            Controls.Add(rootLayout); rootLayout.BringToFront(); ResumeLayout(true);
        }
    }
}
