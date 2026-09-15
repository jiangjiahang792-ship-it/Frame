using Logger;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.CameraAdd;
using TDJS_Vision.Forms.COMAdd;
using TDJS_Vision.Forms.DetectItemManager;
using TDJS_Vision.Forms.GlobalSignalSettings;
using TDJS_Vision.Forms.Helper;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.LightAdd;
using TDJS_Vision.Forms.Login;
using TDJS_Vision.Forms.LogoView;
using TDJS_Vision.Forms.ModbusAdd;
using TDJS_Vision.Forms.MyCSharpScript;
using TDJS_Vision.Forms.PLCAdd;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Forms.ResultView;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.SystemSetting;
using TDJS_Vision.Forms.AiTrainForm;
using TDJS_Vision.Forms.TCPAdd;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Forms.Workspace;
using TDJS_Vision.Properties;
using TDJS_Vision.ResourceManagement;
using TDJS_Vision.Startup;
using WeifenLuo.WinFormsUI.Docking;

namespace TDJS_Vision
{
    public partial class FormMain : FormBase
    {
        /// <summary>禁止窗口取得前台激活状态的Windows扩展样式。</summary>
        private const int ExtendedWindowStyleNoActivate = 0x08000000;

        /// <summary>
        /// 后台性能验收时创建真实主界面但不激活，正式运行仍保持原有前台显示行为。
        /// </summary>
        protected override bool ShowWithoutActivation =>
            StartupDisplayMode.IsBackgroundAcceptance || base.ShowWithoutActivation;

        /// <summary>
        /// 后台性能验收时从窗口句柄层禁止主窗体激活，正常用户启动仍使用原窗口样式。
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                if (StartupDisplayMode.IsBackgroundAcceptance)
                    createParams.ExStyle |= ExtendedWindowStyleNoActivate;

                return createParams;
            }
        }

        /// <summary>Debug固定节拍压力使用的单次运行窗口消息。</summary>
        private const int FixedTriggerStressMessage = 0x8451;

        /// <summary>Debug固定节拍压力用于清除预热计数的窗口消息。</summary>
        private const int FixedTriggerStressResetMessage = 0x8452;

        /// <summary>控制同一方案只允许一轮单次运行并记录忙碌触发。</summary>
        private readonly SolutionRunAdmissionController _singleRunAdmissionController =
            new SolutionRunAdmissionController();

        /// <summary>当前Debug进程是否显式开放固定节拍测试入口。</summary>
        private readonly bool _fixedTriggerStressEnabled = IsFixedTriggerStressEnabled();

        /// <summary>固定节拍测试预期接收的正式触发请求数量。</summary>
        private readonly int _fixedTriggerStressExpectedAttempts = GetFixedTriggerStressExpectedAttempts();

        /// <summary>固定节拍测试声明的请求间隔，单位ms。</summary>
        private readonly int _fixedTriggerStressIntervalMilliseconds = GetFixedTriggerStressIntervalMilliseconds();

        /// <summary>保证每轮固定节拍测试只输出一次最终汇总。</summary>
        private bool _fixedTriggerStressSummaryLogged;

        private static readonly bool LanguageInitialized = InitializeLanguageManager();

        private static bool InitializeLanguageManager()
        {
            LanguageManager.Initialize();
            return true;
        }

        #region 子窗口

        /// <summary>
        /// 光源添加窗口
        /// </summary>
        static FrmLightListView FrmLightAdd = new FrmLightListView();
        /// <summary>
        /// 相机添加窗口
        /// </summary>
        static FrmCameraListView FrmCameraAdd = new FrmCameraListView();
        /// <summary>
        /// PLC添加窗口
        /// </summary>
        static FrmPLCListView FrmPLCAdd = new FrmPLCListView();
        /// <summary>
        /// Modbus添加窗口
        /// </summary>
        static FrmModbusListView FrmModbusAdd = new FrmModbusListView();
        /// <summary>
        /// TCP添加窗口
        /// </summary>
        static FrmTCPListView FrmTcpAdd = new FrmTCPListView();
        /// <summary>
        /// 串口添加窗口
        /// </summary>
        static FrmCOMListView FrmComAdd = new FrmCOMListView();
        /// <summary>
        /// 方案运行参数设置窗口
        /// </summary>
        static FormSolRunParam FrmSolRunParam = new FormSolRunParam();
        /// <summary>
        /// 图像显示栏
        /// </summary>
        static FrmImageViewer FrmImgeDlg = new FrmImageViewer();
        /// <summary>
        /// 结果数据显示栏
        /// </summary>
        static FrmResultView FrmResultDlg = new FrmResultView();
        /// <summary>
        /// 日志栏
        /// </summary>
        static FrmLogger FrmLoggerDlg = new FrmLogger();
        /// <summary>
        /// 客户公司Logo显示窗口
        /// </summary>
        static FrmLogo FrmLogoDlg = new FrmLogo();
        /// <summary>
        /// 流程创建窗口
        /// </summary>
        static FormNewProcessWizard FrmNewProcessWizard = new FormNewProcessWizard();
        /// <summary>
        /// AI配置工具
        /// </summary>
        static FormAIConfigTool formAIConfigTool = new FormAIConfigTool();
        /// <summary>
        /// 图像数量设置窗口
        /// </summary>
        static CanvasSet canvasSet = new CanvasSet();
        /// <summary>
        /// 联系我们
        /// </summary>
        static ContactUsFormForm contactUsFormForm = new ContactUsFormForm();
        /// <summary>
        /// 关于YTViisionPro
        /// </summary>
        static FrmAbout frmAbout = new FrmAbout();
        /// <summary>
        /// 软件登录
        /// </summary>
        static FormLogin frmLogin = new FormLogin();
        /// <summary>
        /// 全局信号设置窗口
        /// </summary>
        static FormGlobalSignal formGlobalSignal = new FormGlobalSignal();
        /// <summary>
        /// 检测项配置窗口
        /// </summary>
        static FormDetectItemManager formDetectItemManager = new FormDetectItemManager();
        /// <summary>
        /// 无监督训练窗口，采用懒加载避免主窗体启动时触碰无监督训练依赖。
        /// </summary>
        private UnsupervisedTrainForm unsupervisedTrainForm;

        /// <summary>
        /// 大模型训练窗口，采用懒加载避免主窗体启动时触碰大模型 native 依赖。
        /// </summary>
        private LargeModelTrainForm largeModelTrainForm;

        #endregion

        private UserRole Role { get; set; } = UserRole.Low; // 登录的用户角色
        /// <summary>
        /// 窗口布局配置
        /// </summary>
        private readonly string DockPanelConfig = Application.StartupPath + "\\DockPanel.config";
        /// <summary>
        /// 反序列化DockContent代理
        /// </summary>
        private DeserializeDockContent DeserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);
        private ToolStripLabel languageToolStripLabel;
        private ToolStripComboBox languageToolStripComboBox;
        private bool isApplyingLanguage;

        /// <summary>
        /// 用于启动授权程序的外部程序启动器。
        /// </summary>
        private readonly IExternalProgramLauncher externalProgramLauncher;

        /// <summary>
        /// 统一显示流程编辑和 AI 训练等独占工作区窗口。
        /// </summary>
        private readonly IExclusiveWorkspacePresenter exclusiveWorkspacePresenter;

        /// <summary>
        /// 使用独立动画和真实进度执行手动方案加载。
        /// </summary>
        private readonly ISolutionLoadingCoordinator solutionLoadingCoordinator;

        /// <summary>
        /// 启动时需要打开的方案路径。
        /// </summary>
        private string args = string.Empty;

        /// <summary>
        /// 标记主窗体布局、SDK 和事件是否已经完成一次结构初始化。
        /// </summary>
        private bool _startupStructureInitialized;

        /// <summary>
        /// 标记启动期事件是否已经绑定，避免重试时重复订阅静态事件。
        /// </summary>
        private bool _startupEventsBound;

        /// <summary>
        /// 标记全部关键启动初始化是否已经成功完成。
        /// </summary>
        private bool _startupInitializationCompleted;

        /// <summary>
        /// 标记脚本预热和自动运行后台任务是否已经启动。
        /// </summary>
        private bool _nonBlockingWarmupsStarted;

        /// <summary>
        /// 标记关键方案加载成功后是否需要启动自动运行。
        /// </summary>
        private bool _shouldStartAutoRun;

        /// <summary>
        /// 初始化主窗体。
        /// </summary>
        public FormMain() : this(
            string.Empty,
            new ExternalProgramLauncher(),
            new ExclusiveWorkspacePresenter(),
            new SolutionLoadingCoordinator())
        {
        }

        /// <summary>
        /// 使用指定启动参数初始化主窗体。
        /// </summary>
        /// <param name="args">启动时需要打开的方案路径。</param>
        public FormMain(string args) : this(
            args,
            new ExternalProgramLauncher(),
            new ExclusiveWorkspacePresenter(),
            new SolutionLoadingCoordinator())
        {
        }

        /// <summary>
        /// 使用指定启动参数和外部程序启动器初始化主窗体。
        /// </summary>
        /// <param name="args">启动时需要打开的方案路径。</param>
        /// <param name="externalProgramLauncher">外部程序启动器。</param>
        internal FormMain(string args, IExternalProgramLauncher externalProgramLauncher)
            : this(
                args,
                externalProgramLauncher,
                new ExclusiveWorkspacePresenter(),
                new SolutionLoadingCoordinator())
        {
        }

        /// <summary>
        /// 使用可替换的外部程序、工作区和方案加载服务初始化主窗体。
        /// </summary>
        /// <param name="args">启动时需要打开的方案路径。</param>
        /// <param name="externalProgramLauncher">外部程序启动器。</param>
        /// <param name="exclusiveWorkspacePresenter">独占工作区显示接口。</param>
        /// <param name="solutionLoadingCoordinator">方案动画加载接口。</param>
        internal FormMain(
            string args,
            IExternalProgramLauncher externalProgramLauncher,
            IExclusiveWorkspacePresenter exclusiveWorkspacePresenter,
            ISolutionLoadingCoordinator solutionLoadingCoordinator)
        {
            this.externalProgramLauncher = externalProgramLauncher ?? throw new ArgumentNullException(nameof(externalProgramLauncher));
            this.exclusiveWorkspacePresenter = exclusiveWorkspacePresenter ?? throw new ArgumentNullException(nameof(exclusiveWorkspacePresenter));
            this.solutionLoadingCoordinator = solutionLoadingCoordinator ?? throw new ArgumentNullException(nameof(solutionLoadingCoordinator));
            InitializeComponent();
            InitializeLanguageUI();
            // 自定义主题设置
            dockPanel1.Theme = new GreenTheme();
            this.args = args ?? string.Empty;
        }

        /// <summary>
        /// 主窗口加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FormMain_Load(object sender, EventArgs e)
        {
            if (_startupInitializationCompleted)
                return;

            try
            {
                await InitializeForStartupAsync(new NullStartupProgressReporter());
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"主窗体兜底启动初始化失败：{ex}", true);
                if (StartupDisplayMode.IsBackgroundAcceptance)
                {
                    Close();
                    return;
                }

                MessageBoxTD.Show($"软件启动初始化失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 在主窗体显示前完成关键启动初始化，并将真实进度上报给启动页。
        /// </summary>
        /// <param name="reporter">启动进度接收接口。</param>
        /// <returns>关键初始化完成任务。</returns>
        internal Task InitializeForStartupAsync(IStartupProgressReporter reporter)
        {
            if (_startupInitializationCompleted)
                return Task.CompletedTask;

            if (reporter == null)
                throw new ArgumentNullException(nameof(reporter));

            using (StartupProgressContext.Begin(reporter))
            {
                reporter.Report(new StartupProgressInfo("正在初始化主界面", "加载窗口布局和界面资源", 0, 0, 14));
                InitializeStartupStructure();

                reporter.Report(new StartupProgressInfo("正在检查启动方案", "准备读取方案配置", 0, 0, 28));
                LoadStartupSolution();
                StartupProgressContext.ThrowIfFailures();

                reporter.Report(new StartupProgressInfo("正在启动后台服务", "准备脚本引擎和自动运行任务", 0, 0, 97));
                StartNonBlockingWarmups();
                _startupInitializationCompleted = true;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 初始化只需执行一次的主窗口布局、SDK、菜单状态和事件。
        /// </summary>
        private void InitializeStartupStructure()
        {
            if (_startupStructureInitialized)
                return;

            InitDockPanel();
            ApplyLanguage();
            CameraHik.InitSDK();

            运行日志ToolStripMenuItem.Checked = FrmLoggerDlg.Visible;
            检测结果ToolStripMenuItem.Checked = FrmResultDlg.Visible;
            图像显示ToolStripMenuItem.Checked = FrmImgeDlg.Visible;
            公司Logo视图ToolStripMenuItem.Checked = FrmLogoDlg.Visible;
            默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible;

            BindStartupEvents();

            文件ToolStripMenuItem.Enabled = false;
            设置ToolStripMenuItem.Enabled = false;
            toolStrip1.Enabled = false;

#if DEBUG
            FormLogin.LoginTest(UserRole.Hight);
#else
            FormLogin.LoginTest(UserRole.Low);
#endif

            _startupStructureInitialized = true;
        }

        /// <summary>
        /// 绑定启动期间需要的窗口、登录和快捷键事件，重试时不会重复订阅。
        /// </summary>
        private void BindStartupEvents()
        {
            if (_startupEventsBound)
                return;

            FrmImgeDlg.HideChangedEvent += HideChangedEvent;
            FrmResultDlg.HideChangedEvent += HideChangedEvent;
            FrmLoggerDlg.HideChangedEvent += HideChangedEvent;
            FrmLogoDlg.HideChangedEvent += HideChangedEvent;

            FormLogin.LoginEvent += OnLoginEvent;
            FormLogin.LogoutEvent += OnLogoutEvent;

            FrmNewProcessWizard.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmLightAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmCameraAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmPLCAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            _startupEventsBound = true;
        }

        /// <summary>
        /// 按命令行方案优先、自动加载方案其次的顺序完成关键方案加载。
        /// </summary>
        private void LoadStartupSolution()
        {
            string solutionName = "启动方案";
            try
            {
                string targetSolutionPath = !string.IsNullOrWhiteSpace(args)
                    ? args
                    : Settings.Default.IsAutoLoad ? Settings.Default.SolutionAddress : string.Empty;

                if (string.IsNullOrWhiteSpace(targetSolutionPath))
                {
                    StartupProgressContext.ReportStage("未配置自动加载方案", "将使用空白主界面", 95);
                    _shouldStartAutoRun = false;
                    return;
                }

                solutionName = Path.GetFileName(targetSolutionPath);
                if (!File.Exists(targetSolutionPath))
                    throw new FileNotFoundException($"方案文件不存在：{solutionName}", targetSolutionPath);

                StartupProgressContext.ReportStage("正在加载方案数据", solutionName, 30);
                ConfigHelper.SolLoad(targetSolutionPath, true);

                IReadOnlyList<StartupFailure> dataFailures = StartupProgressContext.DrainFailures();
                if (dataFailures.Count > 0)
                {
                    ContinueAfterStartupSolutionLoadFailure(
                        solutionName,
                        new StartupInitializationException(dataFailures));
                    return;
                }

                _shouldStartAutoRun = Settings.Default.IsAutoRun;
            }
            catch (Exception ex)
            {
                ContinueAfterStartupSolutionLoadFailure(solutionName, ex);

                IReadOnlyList<StartupFailure> pendingFailures = StartupProgressContext.DrainFailures();
                if (pendingFailures.Count > 0)
                {
                    WriteStartupSolutionLogSafely(
                        $"方案数据恢复附加异常，已忽略：{new StartupInitializationException(pendingFailures)}");
                }
            }
            finally
            {
                args = string.Empty;
            }
        }

        /// <summary>
        /// 将启动方案数据异常降级为日志，并禁止自动运行可能不完整的方案。
        /// </summary>
        /// <param name="solutionName">发生异常的方案文件名。</param>
        /// <param name="exception">原始加载或数据恢复异常。</param>
        private void ContinueAfterStartupSolutionLoadFailure(string solutionName, Exception exception)
        {
            _shouldStartAutoRun = false;
            WriteStartupSolutionLogSafely($"方案数据加载异常，已忽略：方案={solutionName}；{exception}");

            try
            {
                StartupProgressContext.ReportStage(
                    "方案数据加载异常",
                    "已记录日志，继续进入主页面",
                    95);
            }
            catch (Exception progressException)
            {
                WriteStartupSolutionLogSafely($"方案异常进度上报失败，已忽略：{progressException}");
            }
        }

        /// <summary>
        /// 安全记录启动方案加载异常，日志组件自身异常也不会阻止进入主页面。
        /// </summary>
        /// <param name="message">需要写入运行日志的完整消息。</param>
        private static void WriteStartupSolutionLogSafely(string message)
        {
            try
            {
                LogHelper.AddLog(MsgLevel.Exception, message, true);
            }
            catch
            {
                // 数据加载已经失败时不再传播日志异常，确保主页面仍能显示。
            }
        }

        /// <summary>
        /// 启动不影响主页面显示的脚本预热和自动运行后台任务。
        /// </summary>
        private void StartNonBlockingWarmups()
        {
            if (_nonBlockingWarmupsStarted)
                return;

            _nonBlockingWarmupsStarted = true;
            _ = WarmUpScriptEngineInBackgroundAsync();

            if (_shouldStartAutoRun)
                RunAutoSolutionInBackgroundAsync();
        }

        /// <summary>
        /// 在后台预热脚本引擎，并将非关键失败写入日志而不阻止主页面显示。
        /// </summary>
        /// <returns>脚本引擎后台预热任务。</returns>
        private async Task WarmUpScriptEngineInBackgroundAsync()
        {
            try
            {
                await Task.Run(() => { CSharpScriptEngine.Instance.WarmUp(); });
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"脚本引擎后台预热失败：{ex}", true);
            }
        }

        /// <summary>
        /// 在后台保持方案自动运行，不让长期循环任务阻塞启动页关闭。
        /// </summary>
        private async void RunAutoSolutionInBackgroundAsync()
        {
            SetLockStatus(UserRole.Low);
            SetRunStatus(true);
            try
            {
                FormGlobalSignal.StartListenSignals();
                await Solution.Instance.Run(true);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"方案自动运行失败：{ex}", true);
            }
            finally
            {
                FormGlobalSignal.StopListenSignals();
                SetRunStatus(false);
            }
        }

        private void InitializeLanguageUI()
        {
            LanguageManager.Initialize();
            BindLanguageKeys();

            languageToolStripLabel = new ToolStripLabel();
            LanguageManager.Bind(languageToolStripLabel, "Common.Language");

            languageToolStripComboBox = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };

            var loginIndex = menuStrip1.Items.IndexOf(登录ToolStripMenuItem);
            if (loginIndex < 0)
                loginIndex = menuStrip1.Items.Count;
            menuStrip1.Items.Insert(loginIndex, languageToolStripLabel);
            menuStrip1.Items.Insert(loginIndex + 1, languageToolStripComboBox);

            LoadLanguageOptions();
            languageToolStripComboBox.SelectedIndexChanged += LanguageToolStripComboBox_SelectedIndexChanged;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            ApplyLanguage();
        }

        private void BindLanguageKeys()
        {
            LanguageManager.Bind(文件ToolStripMenuItem, "Main.File");
            LanguageManager.Bind(新建方案ToolStripMenuItem, "Main.NewSolution");
            LanguageManager.Bind(打开方案ToolStripMenuItem, "Main.OpenSolution");
            LanguageManager.Bind(保存方案ToolStripMenuItem, "Main.SaveSolution");
            LanguageManager.Bind(另存方案ToolStripMenuItem, "Main.SaveSolutionAs");
            LanguageManager.Bind(视图ToolStripMenuItem, "Main.View");
            LanguageManager.Bind(默认视图ToolStripMenuItem, "Main.DefaultView");
            LanguageManager.Bind(图像显示ToolStripMenuItem, "Main.ImageView");
            LanguageManager.Bind(检测结果ToolStripMenuItem, "Main.ResultView");
            LanguageManager.Bind(运行日志ToolStripMenuItem, "Main.LogView");
            LanguageManager.Bind(公司Logo视图ToolStripMenuItem, "Main.LogoView");
            LanguageManager.Bind(设置ToolStripMenuItem, "Main.Settings");
            LanguageManager.Bind(aI配置工具ToolStripMenuItem, "Main.AIConfigTool");
            LanguageManager.Bind(检测项配置ToolStripMenuItem, "Main.DetectItemConfig");
            LanguageManager.Bind(画布设置ToolStripMenuItem, "Main.CanvasSetting");
            LanguageManager.Bind(系统设置ToolStripMenuItem, "Main.SystemSetting");
            LanguageManager.Bind(全局信号设置ToolStripMenuItem, "Main.GlobalSignalSetting");
            LanguageManager.Bind(帮助ToolStripMenuItem, "Main.Help");
            LanguageManager.Bind(使用教程ToolStripMenuItem, "Main.Tutorial");
            LanguageManager.Bind(联系我们ToolStripMenuItem1, "Main.ContactUs");
            LanguageManager.Bind(关于TDJS_VisionToolStripMenuItem1, "Main.About");
            LanguageManager.Bind(登录ToolStripMenuItem, "Main.Login");
            LanguageManager.Bind(tsbt_SolNew, "Main.NewSolution", "Main.NewSolution");
            LanguageManager.Bind(tsbt_SolOpen, "Main.OpenSolution", "Main.OpenSolution");
            LanguageManager.Bind(tsbt_SolSave, "Main.SaveSolution", "Main.SaveSolution");
            LanguageManager.Bind(tsbt_SolSaveAs, "Main.SaveSolutionAs", "Main.SaveSolutionAs");
            LanguageManager.Bind(tsbt_ProcessManager, "Main.ProcessManager", "Main.ProcessManager");
            LanguageManager.Bind(tsbt_LightManager, "Main.LightManager", "Main.LightManager");
            LanguageManager.Bind(tsbt_CameraManager, "Main.CameraManager", "Main.CameraManager");
            LanguageManager.Bind(tsbt_PlcManager, "Main.PLCManager", "Main.PLCManager");
            LanguageManager.Bind(tsbt_ModbusManager, "Main.ModbusManager", "Main.ModbusManager");
            LanguageManager.Bind(tsbt_TCPManager, "Main.TCPManager", "Main.TCPManager");
            LanguageManager.Bind(tsbt_ComManager, "Main.COMManager", "Main.COMManager");
            LanguageManager.Bind(tsbt_RunParamSetting, "Main.RunParam", "Main.RunParam");
            LanguageManager.Bind(tsbt_ClearCount, "Main.ClearCount", "Main.ClearCount");
            LanguageManager.Bind(tsbt_SolRunOnce, "Main.RunOnce", "Main.RunOnce");
            LanguageManager.Bind(tsbt_SolRunLoop, "Main.RunLoop", "Main.RunLoop");
            LanguageManager.Bind(tsbt_SolRunStop, "Main.StopRun", "Main.StopRun");
            LanguageManager.Bind(toolStripLabel1, "Main.SolutionLoading");
        }

        private void LoadLanguageOptions()
        {
            isApplyingLanguage = true;
            try
            {
                languageToolStripComboBox.Items.Clear();
                foreach (var language in LanguageManager.GetAvailableLanguages())
                    languageToolStripComboBox.Items.Add(language);

                for (int i = 0; i < languageToolStripComboBox.Items.Count; i++)
                {
                    if (languageToolStripComboBox.Items[i] is LanguageInfo info &&
                        info.Culture == LanguageManager.CurrentCulture)
                    {
                        languageToolStripComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                isApplyingLanguage = false;
            }
        }

        private void LanguageToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isApplyingLanguage)
                return;

            if (languageToolStripComboBox.SelectedItem is LanguageInfo info)
                LanguageManager.SetLanguage(info.Culture);
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            isApplyingLanguage = true;
            try
            {
                Text = string.Format(LanguageManager.T("Main.Title"), VersionInfo.VersionInfo.GetExeVer());
                LanguageManager.ApplyToolStrip(menuStrip1);
                LanguageManager.ApplyToolStrip(toolStrip1);

                for (int i = 0; i < languageToolStripComboBox.Items.Count; i++)
                {
                    if (languageToolStripComboBox.Items[i] is LanguageInfo info &&
                        info.Culture == LanguageManager.CurrentCulture)
                    {
                        languageToolStripComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                isApplyingLanguage = false;
            }
        }

        private void OnLoginEvent(object sender, UserRole e)
        {
            SetLockStatus(e);
        }
        private void OnLogoutEvent(object sender, EventArgs e)
        {
            UserPermissionContext.UpdateRole(UserRole.Unknown);
            文件ToolStripMenuItem.Enabled = false;
            设置ToolStripMenuItem.Enabled = false;
            toolStrip1.Enabled = false;
            Role = UserRole.Unknown;
        }

        /// <summary>
        /// 按下快捷键事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnShotKeySavePressed(object sender, EventArgs e)
        {
            保存方案ToolStripMenuItem_Click(null, null);
        }

        /// <summary>
        /// 界面锁住状态
        /// </summary>
        private void SetLockStatus(UserRole role)
        {
            UserPermissionContext.UpdateRole(role);
            Role = role;
            toolStrip1.Enabled = true;
            switch (role)
            {
                case UserRole.Hight:
                    文件ToolStripMenuItem.Enabled = true;
                    设置ToolStripMenuItem.Enabled = true;
                    tsbt_SolNew.Enabled = true;
                    tsbt_SolOpen.Enabled = true;
                    tsbt_SolSaveAs.Enabled = true;
                    tsbt_SolSave.Enabled = true;
                    tsbt_ProcessManager.Enabled = true;
                    tsbt_LightManager.Enabled = true;
                    tsbt_CameraManager.Enabled = true;
                    tsbt_PlcManager.Enabled = true;
                    tsbt_ModbusManager.Enabled = true;
                    tsbt_ComManager.Enabled = true;
                    tsbt_TCPManager.Enabled = true;
                    tsbt_RunParamSetting.Enabled = true;
                    break;
                case UserRole.Medium:
                    文件ToolStripMenuItem.Enabled = false;
                    设置ToolStripMenuItem.Enabled = false;
                    tsbt_SolNew.Enabled = false;
                    tsbt_SolOpen.Enabled = false;
                    tsbt_SolSaveAs.Enabled = false;
                    tsbt_SolSave.Enabled = false;
                    tsbt_ProcessManager.Enabled = false;
                    tsbt_LightManager.Enabled = false;
                    tsbt_CameraManager.Enabled = false;
                    tsbt_PlcManager.Enabled = false;
                    tsbt_ModbusManager.Enabled = false;
                    tsbt_TCPManager.Enabled = false;
                    tsbt_ComManager.Enabled = false;
                    tsbt_RunParamSetting.Enabled = false;
                    break;
                case UserRole.Low:
                    文件ToolStripMenuItem.Enabled = false;
                    设置ToolStripMenuItem.Enabled = false;
                    tsbt_SolNew.Enabled = false;
                    tsbt_SolOpen.Enabled = false;
                    tsbt_SolSaveAs.Enabled = false;
                    tsbt_SolSave.Enabled = false;
                    tsbt_ProcessManager.Enabled = false;
                    tsbt_LightManager.Enabled = false;
                    tsbt_CameraManager.Enabled = false;
                    tsbt_PlcManager.Enabled = false;
                    tsbt_ModbusManager.Enabled = false;
                    tsbt_TCPManager.Enabled = false;
                    tsbt_ComManager.Enabled = false;
                    tsbt_RunParamSetting.Enabled = false;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 主窗口关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 后台验收子进程由测试工具负责关闭，不显示可能抢占用户桌面的确认框。
            if (!StartupDisplayMode.IsBackgroundAcceptance)
            {
                var res = MessageBoxTD.Show(LanguageManager.T("Dialog.ConfirmClose"), LanguageManager.T("Common.Tip"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (res == DialogResult.Cancel || res == DialogResult.None)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // 检测任务正在运行提示
            if (Solution.Instance.IsRunning)
            {
                if (StartupDisplayMode.IsBackgroundAcceptance)
                {
                    Solution.Instance.Stop();
                    e.Cancel = true;
                    return;
                }

                var res1 = MessageBoxTD.Show(LanguageManager.T("Dialog.StopTaskBeforeClose"));
                if (res1 == DialogResult.OK || res1 == DialogResult.None)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // 保存主窗口布局
            this.dockPanel1.SaveAsXml(DockPanelConfig);

            bool solutionResourcesReleased = false;
            try
            {
                //保存OK总数量
                if (!Solution.Instance.SolFileName.IsNullOrEmpty())
                {
                    try
                    {
                        Solution.Instance.SaveNumber(Solution.Instance.SolFileName);
                    }
                    catch (Exception ex)
                    {
                        if (StartupDisplayMode.IsBackgroundAcceptance)
                        {
                            LogHelper.AddLog(MsgLevel.Exception, $"后台性能验收关闭时保存方案计数失败：{ex}", true);
                        }
                        else
                        {
                            MessageBoxTD.Show(string.Format(LanguageManager.T("Dialog.SaveSolutionFailed"), ex.Message));
                        }
                    }
                }

                // 释放方案资源
                solutionResourcesReleased = Solution.Instance.SolReset();
                if (!solutionResourcesReleased)
                {
                    LogHelper.AddLog(
                        MsgLevel.Exception,
                        "软件退出时仍有运行流程或图像保存任务未在期限内退出。",
                        true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"软件退出时释放方案资源异常：{ex}", true);
            }
            finally
            {
                if (solutionResourcesReleased)
                {
                    // 所有相机句柄和回调均已释放后，才能安全反初始化海康原生SDK。
                    CameraHik.FinalizeSDK();
                }
                else
                {
                    LogHelper.AddLog(MsgLevel.Warn, "方案资源尚未完全释放，跳过海康原生SDK反初始化并交由进程退出回收。", true);
                }
            }
        }

        /// <summary>
        /// 加载窗口布局
        /// </summary>
        private void InitDockPanel()
        {
            try
            {
                if (File.Exists(DockPanelConfig))
                {
                    // 如果存在，则从配置文件加载布局
                    this.dockPanel1.LoadFromXml(DockPanelConfig, DeserializeDockContent);
                }
                else
                {
                    LoadDefaultDockPanel();
                }
            }
            catch (Exception ex)
            {
                LoadDefaultDockPanel();
            }
        }
        /// <summary>
        /// 默认窗口布局
        /// </summary>
        private void LoadDefaultDockPanel()
        {
            FrmImgeDlg.Show(dockPanel1, DockState.Document);
            FrmResultDlg.Show(FrmImgeDlg.Pane, DockAlignment.Right, 0.25);
            
            // 运行状态下不默认打开日志窗口
            if (!Solution.Instance.IsRunning)
            {
                FrmLoggerDlg.Show(dockPanel1, DockState.Document);
                运行日志ToolStripMenuItem.Checked = true;
            }
            else
            {
                if (FrmLoggerDlg.Visible)
                {
                    FrmLoggerDlg.Hide();
                }
                运行日志ToolStripMenuItem.Checked = false;
            }
            
            FrmLogoDlg.Show(FrmResultDlg.Pane, DockAlignment.Top, 0.5);
            图像显示ToolStripMenuItem.Checked = true;
            检测结果ToolStripMenuItem.Checked = true;
            公司Logo视图ToolStripMenuItem.Checked = true;
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }
        /// <summary>
        /// 配置委托函数
        /// </summary>
        /// <param name="persistString"></param>
        /// <returns></returns>
        private static IDockContent GetContentFromPersistString(string persistString)
        {
            if (persistString == typeof(FrmImageViewer).ToString())
                return FrmImgeDlg;

            else if (persistString == typeof(FrmResultView).ToString())
                return FrmResultDlg;

            else if (persistString == typeof(FrmLogger).ToString())
                return FrmLoggerDlg;

            else if (persistString == typeof(FrmLogo).ToString())
                return FrmLogoDlg;
            else
                return null;
        }

        private void 文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                if (item == 新建方案ToolStripMenuItem)
                    新建方案ToolStripMenuItem_Click(sender, e);
                else if (item == 打开方案ToolStripMenuItem)
                    打开方案ToolStripMenuItem_Click(sender, e);
                else if (item == 另存方案ToolStripMenuItem)
                    另存方案ToolStripMenuItem_Click(sender, e);
                else if (item == 保存方案ToolStripMenuItem)
                    保存方案ToolStripMenuItem_Click(sender, e);
            }
        }

        /// <summary>
        /// 点击工具栏的工具
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToolStripButton_Click(object sender, EventArgs e)
        {
            ToolStripButton tsbt = sender as ToolStripButton;
            if (tsbt == tsbt_SolNew)
                新建方案ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_SolOpen)
                打开方案ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_SolSaveAs)
                另存方案ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_SolSave)
                保存方案ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_ProcessManager)
                流程管理ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_LightManager)
                光源管理ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_CameraManager)
                相机管理ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_PlcManager)
                PLC管理ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_ModbusManager)
                Modbus设备ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_TCPManager)
                TCP设备ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_ComManager)
                串口管理ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_RunParamSetting)
                运行参数ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_SolRunLoop)
                循环运行ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_SolRunOnce)
                单次运行ToolStripMenuItem_Click(null, null);
            else if (tsbt == tsbt_SolRunStop)
                停止运行ToolStripMenuItem_Click(null, null);
        }

        private void 流程管理ToolStripMenuItem_Click(object value1, object value2)
        {
            if (FrmNewProcessWizard == null || FrmNewProcessWizard.IsDisposed)
            {
                FrmNewProcessWizard = new FormNewProcessWizard();
            }

            exclusiveWorkspacePresenter.ShowDialog(this, FrmNewProcessWizard);
        }

        private async void 停止运行ToolStripMenuItem_Click(object value1, object value2)
        {
            Solution.Instance.Stop();
            FormGlobalSignal.StopListenSignals();
            SetRunStatus(false);
            await FormGlobalSignal.SendGlobalSignals(this, false);
        }

        private async void 循环运行ToolStripMenuItem_Click(object value1, object value2)
        {
            if (!ValidateCameraConfigurationBeforeRun())
                return;

            await FormGlobalSignal.SendGlobalSignals(this, true);
            FormGlobalSignal.StartListenSignals();
            SetRunStatus(true);
            try
            {
                await Solution.Instance.Run(true);
            }
            finally
            {
                FormGlobalSignal.StopListenSignals();
                SetRunStatus(false);
            }
        }

        private async void 单次运行ToolStripMenuItem_Click(object value1, object value2)
        {
            if (!ValidateCameraConfigurationBeforeRun())
                return;

            if (TryStartSingleSolutionRun(out Task runTask))
                await runTask;
        }

        /// <summary>
        /// 尝试启动一轮完整方案；忙碌时立即拒绝并保留诊断计数。
        /// </summary>
        /// <param name="runTask">准入成功时返回完整执行任务。</param>
        /// <returns>成功取得本轮运行权返回true。</returns>
        private bool TryStartSingleSolutionRun(out Task runTask)
        {
            if (!_singleRunAdmissionController.TryAcquire(out SolutionRunAdmissionLease lease))
            {
                SolutionRunAdmissionSnapshot rejectedSnapshot = _singleRunAdmissionController.GetSnapshot();
                if (rejectedSnapshot.BusyRejectedCount == 1 || rejectedSnapshot.BusyRejectedCount % 100 == 0)
                {
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        $"【固定触发准入】方案仍在执行，本次触发已拒绝；{rejectedSnapshot.ToLogText()}",
                        false);
                }

                runTask = Task.CompletedTask;
                TryLogFixedTriggerStressSummary();
                return false;
            }

            runTask = ExecuteSingleSolutionRunAsync(lease);
            return true;
        }

        /// <summary>
        /// 执行一轮与主界面按钮相同的全局信号、完整方案和结束信号链路。
        /// </summary>
        /// <param name="lease">本轮唯一方案运行权。</param>
        /// <returns>完整运行任务。</returns>
        private async Task ExecuteSingleSolutionRunAsync(SolutionRunAdmissionLease lease)
        {
            bool succeeded = false;
            try
            {
                await FormGlobalSignal.SendGlobalSignals(this, true);
                FormGlobalSignal.StartListenSignals();
                SetRunStatus(true);
                await Solution.Instance.Run(false);
                succeeded = true;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"单次方案运行失败：{ex}", true);
            }
            finally
            {
                FormGlobalSignal.StopListenSignals();
                SetRunStatus(false);
                try
                {
                    await FormGlobalSignal.SendGlobalSignals(this, false);
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    LogHelper.AddLog(MsgLevel.Exception, $"单次方案结束信号发送失败：{ex}", true);
                }

                lease.Complete(succeeded);
                TryLogFixedTriggerStressSummary();
            }
        }

        /// <summary>
        /// 处理Debug压力工具发出的固定节拍触发和预热计数重置消息。
        /// </summary>
        /// <param name="m">Windows窗口消息。</param>
        protected override void WndProc(ref Message m)
        {
#if DEBUG
            if (_fixedTriggerStressEnabled && m.Msg == FixedTriggerStressMessage)
            {
                m.Result = TryStartSingleSolutionRun(out _) ? new IntPtr(1) : IntPtr.Zero;
                return;
            }

            if (_fixedTriggerStressEnabled && m.Msg == FixedTriggerStressResetMessage)
            {
                bool reset = _singleRunAdmissionController.TryReset();
                if (reset)
                    _fixedTriggerStressSummaryLogged = false;
                m.Result = reset ? new IntPtr(1) : IntPtr.Zero;
                return;
            }
#endif
            base.WndProc(ref m);
        }

        /// <summary>
        /// 在全部预期请求到达且最后一轮退出后写一次固定节拍压力汇总。
        /// </summary>
        private void TryLogFixedTriggerStressSummary()
        {
            if (!_fixedTriggerStressEnabled ||
                _fixedTriggerStressSummaryLogged ||
                _fixedTriggerStressExpectedAttempts <= 0)
            {
                return;
            }

            SolutionRunAdmissionSnapshot snapshot = _singleRunAdmissionController.GetSnapshot();
            if (snapshot.RequestedCount < _fixedTriggerStressExpectedAttempts || snapshot.IsActive)
                return;

            _fixedTriggerStressSummaryLogged = true;
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【固定触发汇总】间隔={_fixedTriggerStressIntervalMilliseconds}ms；{snapshot.ToLogText()}",
                false);
        }

        /// <summary>
        /// 判断当前Debug进程是否由固定节拍压力工具显式启动。
        /// </summary>
        /// <returns>仅环境变量值为1时返回true。</returns>
        private static bool IsFixedTriggerStressEnabled()
        {
#if DEBUG
            return string.Equals(
                Environment.GetEnvironmentVariable("TDJS_VISION_FIXED_TRIGGER_STRESS"),
                "1",
                StringComparison.Ordinal);
#else
            return false;
#endif
        }

        /// <summary>
        /// 读取固定节拍压力预期请求数，非法或缺失时返回0。
        /// </summary>
        /// <returns>大于零的预期请求数，或0。</returns>
        private static int GetFixedTriggerStressExpectedAttempts()
        {
            string rawValue = Environment.GetEnvironmentVariable("TDJS_VISION_FIXED_TRIGGER_EXPECTED_ATTEMPTS");
            return int.TryParse(rawValue, out int value) && value > 0 ? value : 0;
        }

        /// <summary>
        /// 读取固定节拍压力声明的请求间隔，非法或缺失时使用50ms。
        /// </summary>
        /// <returns>大于零的触发间隔。</returns>
        private static int GetFixedTriggerStressIntervalMilliseconds()
        {
            string rawValue = Environment.GetEnvironmentVariable("TDJS_VISION_FIXED_TRIGGER_INTERVAL_MS");
            return int.TryParse(rawValue, out int value) && value > 0 ? value : 50;
        }

        /// <summary>
        /// 校验方案内所有流程的硬触发图像源配置，并在错误时显示汇总提示。
        /// </summary>
        /// <returns>方案允许启动时返回 true。</returns>
        private bool ValidateCameraConfigurationBeforeRun()
        {
            string message;
            if (Solution.Instance.TryValidateCameraConfiguration(Solution.Instance.AllProcesses, out message))
                return true;

            MessageBoxTD.Show(message, "相机触发配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        /// <summary>
        /// 设置运行状态，启用/禁用控件
        /// </summary>
        /// <param name="isRunning"></param>
        /// <param name="all">停止运行按钮是否和其他一致</param>
        public void SetRunStatus(bool isRun)
        {
            // 如果当前在 UI 线程上，直接执行逻辑
            if (this.InvokeRequired == false)
            {
                UpdateUI(isRun);
            }
            else
            {
                // 否则通过 Invoke 切换到 UI 线程执行
                this.Invoke(new Action<bool>(UpdateUI), isRun);
            }
        }

        // 实际更新 UI 的方法
        private void UpdateUI(bool isRun)
        {
            if (!isRun)
            {
                FrmLoggerDlg.FlushLogs();
            }

            // 运行和停止按钮
            tsbt_SolRunOnce.Enabled = !isRun;
            tsbt_SolRunLoop.Enabled = !isRun;
            tsbt_SolRunStop.Enabled = isRun;
            登录ToolStripMenuItem.Enabled = !isRun;

            switch (Role)
            {
                case UserRole.Hight:
                    // 其他设置禁用/启用
                    tsbt_SolNew.Enabled = !isRun;
                    tsbt_SolOpen.Enabled = !isRun;
                    tsbt_SolSaveAs.Enabled = !isRun;
                    tsbt_SolSave.Enabled = !isRun;
                    tsbt_ProcessManager.Enabled = !isRun;
                    tsbt_LightManager.Enabled = !isRun;
                    tsbt_CameraManager.Enabled = !isRun;
                    tsbt_PlcManager.Enabled = !isRun;
                    tsbt_ModbusManager.Enabled = !isRun;
                    tsbt_TCPManager.Enabled = !isRun;
                    tsbt_ComManager.Enabled = !isRun;
                    tsbt_RunParamSetting.Enabled = !isRun;

                    // 菜单栏
                    文件ToolStripMenuItem.Enabled = !isRun;
                    视图ToolStripMenuItem.Enabled = !isRun;
                    设置ToolStripMenuItem.Enabled = !isRun;
                    帮助ToolStripMenuItem.Enabled = !isRun;
                    tsbt_ClearCount.Enabled = !isRun;
                    break;

                case UserRole.Medium:
                    tsbt_RunParamSetting.Enabled = false;
                    break;

                case UserRole.Low:
                    break;

                default:
                    break;
            }
        }

        private void 用户登录ToolStripMenuItem_Click(object value1, object value2)
        {
            frmLogin.ShowDialog();
        }

        private void PLC管理ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmPLCAdd.ShowDialog();
        }

        private void Modbus设备ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmModbusAdd.ShowDialog();
        }
        
        private void TCP设备ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmTcpAdd.ShowDialog();
        }

        private void 串口管理ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmComAdd.ShowDialog();
        }

        private void 运行参数ToolStripMenuItem_Click(object value1, object value2)
        {
            if (!UserPermissionContext.CanUseManualTuning)
            {
                MessageBoxTD.Show("当前用户没有权限使用手动调参，请登录最高权限账号！");
                return;
            }

            FrmSolRunParam.ShowDialog();
        }

        private void 相机管理ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmCameraAdd.ShowDialog();
        }

        private void 光源管理ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmLightAdd.ShowDialog();
        }

        private void 另存方案ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Solution.Instance.SolFileName.IsNullOrEmpty())
            {
                MessageBoxTD.Show(LanguageManager.T("Dialog.SaveBeforeSaveAs"));
                return;
            }
            saveFileDialog1.Title = LanguageManager.T("Dialog.SaveSolutionAsTitle");
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Solution.Instance.Save(saveFileDialog1.FileName);
                    LogHelper.AddLog(MsgLevel.Info, string.Format(LanguageManager.T("Log.SaveSolutionAsSuccess"), saveFileDialog1.FileName), true);
                }
                catch (Exception ex)
                {
                    MessageBoxTD.Show(string.Format(LanguageManager.T("Dialog.SaveSolutionFailed"), ex.Message));
                }
            }
        }

        private void 保存方案ToolStripMenuItem_Click(object value1, object value2)
        {
            if (Solution.Instance.SolFileName.IsNullOrEmpty())
            {
                saveFileDialog1.Title = LanguageManager.T("Dialog.SelectSaveSolutionPath");
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    Solution.Instance.SolFileName = saveFileDialog1.FileName;
                else
                    return;
            }
            try
            {
                Solution.Instance.Save(Solution.Instance.SolFileName);
                LogHelper.AddLog(MsgLevel.Info, string.Format(LanguageManager.T("Log.SaveSolutionSuccess"), Solution.Instance.SolFileName), true);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(string.Format(LanguageManager.T("Dialog.SaveSolutionFailed"), ex.Message));
            }
        }

        private void 打开方案ToolStripMenuItem_Click(object value1, object value2)
        {
            openFileDialog1.Title = LanguageManager.T("Dialog.SelectOpenSolution");
            if (openFileDialog1.ShowDialog()  == DialogResult.OK)
            {
                toolStripLabel1.Visible = true;
                SetRunStatus(true); // 设置为正在加载方案状态
                try
                {
                    solutionLoadingCoordinator.Load(this, openFileDialog1.FileName, true);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"手动打开方案失败，已忽略：{ex}", true);
                }
                finally
                {
                    SetRunStatus(false);
                    toolStripLabel1.Visible = false;
                }
            }
        }

        /// <summary>
        /// 新建方案
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 新建方案ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 新建方案实际和调用加载空方案一样(传入false，表示不需要打印反序列化的信息因为新建方案实际上就是加载一个空的方案)
            bool loadSucceeded = Solution.Instance.Load(Application.StartupPath + "\\空方案.Sol", false);
            Solution.Instance.SolFileName = string.Empty;
            if (loadSucceeded)
                LogHelper.AddLog(MsgLevel.Info, LanguageManager.T("Log.NewSolutionSuccess"), true);
            else
                LogHelper.AddLog(MsgLevel.Warn, "空方案数据加载异常，已忽略并继续使用当前界面。", true);
        }

        private void 联系我们ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //contactUsFormForm.ShowDialog();
        }

        private void aI配置工具ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            formAIConfigTool.ShowDialog();
        }
        /// <summary>
        /// 图像画布设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 画布设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            canvasSet.ShowDialog();
        }

        private void 关于TDJS_VisionToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            frmAbout.ShowDialog();
        }

        /// <summary>
        /// 启动软件运行目录中的授权程序。
        /// </summary>
        /// <param name="sender">触发事件的菜单项。</param>
        /// <param name="e">事件参数。</param>
        private void 授权ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string activationProgramPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activate.exe");
            if (!File.Exists(activationProgramPath))
            {
                MessageBoxTD.Show($"未找到授权程序：{activationProgramPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                externalProgramLauncher.Start(activationProgramPath);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"启动授权程序失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void 默认视图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(默认视图ToolStripMenuItem.Checked)
                LoadDefaultDockPanel();
        }

        private void 检测图像视图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FrmImgeDlg.Visible)
            {
                FrmImgeDlg.Hide(); // 如果窗口可见，隐藏它
                图像显示ToolStripMenuItem.Checked = false;
                默认视图ToolStripMenuItem.Checked = false;
            }
            else
            {
                FrmImgeDlg.Show(); // 如果窗口隐藏，显示它
                图像显示ToolStripMenuItem.Checked = true;
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible && FrmLogoDlg.Visible ? true : false;
            }
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }

        private void 检测结果视图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FrmResultDlg.Visible)
            {
                FrmResultDlg.Hide(); // 如果窗口可见，隐藏它
                检测结果ToolStripMenuItem.Checked = false;
                默认视图ToolStripMenuItem.Checked = false;
            }
            else
            {
                FrmResultDlg.Show(); // 如果窗口隐藏，显示它
                检测结果ToolStripMenuItem.Checked = true;
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible && FrmLogoDlg.Visible ? true : false;
            }
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }

        private void 运行日志视图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FrmLoggerDlg.Visible)
            {
                FrmLoggerDlg.Hide(); // 如果窗口可见，隐藏它
                运行日志ToolStripMenuItem.Checked = false;
                默认视图ToolStripMenuItem.Checked = false;
            }
            else
            {
                if (Solution.Instance.IsRunning)
                {
                    MessageBoxTD.Show(LanguageManager.T("Dialog.StopTaskBeforeOpenLog"), LanguageManager.T("Common.Tip"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                FrmLoggerDlg.Show(); // 如果窗口隐藏，显示它
                运行日志ToolStripMenuItem.Checked = true;
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible && FrmLogoDlg.Visible ? true : false;
            }
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }

        private void 公司Logo视图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FrmLogoDlg.Visible)
            {
                FrmLogoDlg.Hide(); // 如果窗口可见，隐藏它
                公司Logo视图ToolStripMenuItem.Checked = false;
                默认视图ToolStripMenuItem.Checked = false;
            }
            else
            {
                FrmLogoDlg.Show(); // 如果窗口隐藏，显示它
                公司Logo视图ToolStripMenuItem.Checked = true;
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible && FrmLogoDlg.Visible? true : false;
            }
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }

        private void HideChangedEvent(object sender, EventArgs e)
        {
            FrmImageViewer frmImageViewer = sender as FrmImageViewer;
            FrmResultView frmResultView = sender as FrmResultView;
            FrmLogger frmLogger = sender as FrmLogger;
            FrmLogo frmLogo = sender as FrmLogo;
            if (frmImageViewer != null)
                图像显示ToolStripMenuItem.Checked = frmImageViewer.Visible;
            else if (frmResultView != null)
                检测结果ToolStripMenuItem.Checked = frmResultView.Visible;
            else if (frmLogger != null)
                运行日志ToolStripMenuItem.Checked = frmLogger.Visible;
            else if (frmLogo != null)
                公司Logo视图ToolStripMenuItem.Checked = frmLogo.Visible;
        }

        private void 登录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            用户登录ToolStripMenuItem_Click(null, null);
        }

        private void 系统设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FrmSystemSetting systemSettingForm = new FrmSystemSetting())
                systemSettingForm.ShowDialog(this);
        }

        private void 全局信号设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            formGlobalSignal.ShowDialog();
        }

        private void 检测项配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            formDetectItemManager.ShowDialog();
        }

        /// <summary>
        /// 打开无监督训练窗口。
        /// </summary>
        /// <param name="sender">触发菜单项。</param>
        /// <param name="e">事件参数。</param>
        private void UnsupervisedTrainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenUnsupervisedTrainForm();
        }

        /// <summary>
        /// 打开大模型训练窗口。
        /// </summary>
        /// <param name="sender">触发菜单项。</param>
        /// <param name="e">事件参数。</param>
        private void LargeModelTrainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenLargeModelTrainForm();
        }

        /// <summary>
        /// 懒加载并显示无监督训练窗口，保持无监督运行环境与主程序启动隔离。
        /// </summary>
        private void OpenUnsupervisedTrainForm()
        {
            try
            {
                using (var form = new UnsupervisedTrainForm())
                {
                    unsupervisedTrainForm = form;
                    exclusiveWorkspacePresenter.ShowDialog(this, form);
                }
            }
            finally
            {
                unsupervisedTrainForm = null;
            }
        }

        /// <summary>
        /// 懒加载并显示大模型训练窗口，保持 LargeModelDll 运行环境与主程序启动隔离。
        /// </summary>
        private void OpenLargeModelTrainForm()
        {
            try
            {
                using (var form = new LargeModelTrainForm())
                {
                    largeModelTrainForm = form;
                    exclusiveWorkspacePresenter.ShowDialog(this, form);
                }
            }
            finally
            {
                largeModelTrainForm = null;
            }
        }

        /// <summary>
        /// 清除计数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            foreach (var process in Solution.Instance.AllProcesses)
            {
                process.OKNumber = 0;
                process.NGNumber = 0;
            }
        }
    }
}
