using Logger;
using Sunny.UI;
using System;
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
using TDJS_Vision.Forms.TCPAdd;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Properties;
using WeifenLuo.WinFormsUI.Docking;

namespace TDJS_Vision
{
    public partial class FormMain : FormBase
    {
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
        /// 系统设置窗口
        /// </summary>
        private FrmSystemSetting frmSystemSetting = new FrmSystemSetting();
        /// <summary>
        /// 全局信号设置窗口
        /// </summary>
        static FormGlobalSignal formGlobalSignal = new FormGlobalSignal();
        /// <summary>
        /// 检测项配置窗口
        /// </summary>
        static FormDetectItemManager formDetectItemManager = new FormDetectItemManager();

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

        public FormMain()
        {
            InitializeComponent();
            InitializeLanguageUI();
            // 自定义主题设置
            this.dockPanel1.Theme = new GreenTheme();
        }
        string args = string.Empty;
        public FormMain(string args)
        {
            InitializeComponent();
            InitializeLanguageUI();
            // 自定义主题设置
            this.dockPanel1.Theme = new GreenTheme();
            this.args = args ?? string.Empty;
        }
        /// <summary>
        /// 主窗口加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_Load(object sender, EventArgs e)
        {
            // 初始化主窗口布局
            InitDockPanel();

            ApplyLanguage();

            // 初始化海康相机SDK
            CameraHik.InitSDK();

            // 工具菜单状态同步
            运行日志ToolStripMenuItem.Checked = FrmLoggerDlg.Visible;
            检测结果ToolStripMenuItem.Checked = FrmResultDlg.Visible;
            图像显示ToolStripMenuItem.Checked = FrmImgeDlg.Visible;
            公司Logo视图ToolStripMenuItem.Checked = FrmLogoDlg.Visible;
            默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible ? true : false;

            // 布局同步更新事件
            FrmImgeDlg.HideChangedEvent += HideChangedEvent;
            FrmResultDlg.HideChangedEvent += HideChangedEvent;
            FrmLoggerDlg.HideChangedEvent += HideChangedEvent;
            FrmLogoDlg.HideChangedEvent += HideChangedEvent;

            // 界面锁事件
            FormLogin.LoginEvent += OnLoginEvent;
            FormLogin.LogoutEvent += OnLogoutEvent;

            // 保存快捷键事件处理
            FrmNewProcessWizard.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmLightAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmCameraAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmPLCAdd.OnShotKeySavePressed += OnShotKeySavePressed;


            // 设置控件Enable为未登录状态
            文件ToolStripMenuItem.Enabled = false;
            设置ToolStripMenuItem.Enabled = false;
            toolStrip1.Enabled = false;

            // 加载默认方案
            AutoLoadSolutionAsync();

#if DEBUG
            //调试时登录工程师权限
            FormLogin.LoginTest(UserRole.Hight); // 测试登录
#else
            FormLogin.LoginTest(UserRole.Low); // 测试登录
#endif
            // 预热脚本引擎
            Task.Run(() => { CSharpScriptEngine.Instance.WarmUp(); });


            // 双击关联的方案文件，加载指定方案
            if (!string.IsNullOrEmpty(this.args))
            {
                ConfigHelper.SolLoad(this.args, true); // 加载指定方案
                this.args = string.Empty; // 清空参数，避免重复加载
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
            文件ToolStripMenuItem.Enabled = false;
            设置ToolStripMenuItem.Enabled = false;
            toolStrip1.Enabled = false;
            Role = UserRole.Unknown;
        }

        /// <summary>
        /// 软件启动是否加载指定方案
        /// </summary>
        private async Task AutoLoadSolutionAsync()
        {
            if (Settings.Default.IsAutoLoad)
            {
                try
                {
                    toolStripLabel1.Visible = true;
                    SetRunStatus(true); // 设置为正在加载方案状态
                    Solution.Instance.Load(Settings.Default.SolutionAddress, true);
                    toolStripLabel1.Visible = false;
                    SetRunStatus(false);
                }
                catch (Exception)
                {
                    LogHelper.AddLog(MsgLevel.Exception, LanguageManager.T("Log.LoadSolutionFailed"), true);
                }
                if (Settings.Default.IsAutoRun)
                {
                    SetLockStatus(UserRole.Low);
                    SetRunStatus(true);
                    try
                    {
                        FormGlobalSignal.StartListenSignals();
                        await Solution.Instance.Run(true);
                    }
                    finally
                    {
                        FormGlobalSignal.StopListenSignals();
                        SetRunStatus(false);
                    }
                }
            }
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
                    tsbt_RunParamSetting.Enabled = true;
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
            // 主程序关闭提醒
            var res = MessageBoxTD.Show(LanguageManager.T("Dialog.ConfirmClose"), LanguageManager.T("Common.Tip"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Cancel || res == DialogResult.None)
            {
                e.Cancel = true;
                return;
            }

            // 检测任务正在运行提示
            if (Solution.Instance.IsRunning)
            {
                var res1 = MessageBoxTD.Show(LanguageManager.T("Dialog.StopTaskBeforeClose"));
                if (res1 == DialogResult.OK || res1 == DialogResult.None)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // 海康相机SDK反序列化
            CameraHik.Finalize();

            // 保存主窗口布局
            this.dockPanel1.SaveAsXml(DockPanelConfig);

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
                        MessageBoxTD.Show(string.Format(LanguageManager.T("Dialog.SaveSolutionFailed"), ex.Message));
                    }
                }

                // 释放方案资源
                Solution.Instance.SolReset();
            }
            catch (Exception)
            {
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
            FrmNewProcessWizard.ShowDialog();
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
            await FormGlobalSignal.SendGlobalSignals(this, true);
            FormGlobalSignal.StartListenSignals();
            SetRunStatus(true);
            try
            {
                await Solution.Instance.Run(false);
            }
            finally
            {
                FormGlobalSignal.StopListenSignals();
                SetRunStatus(false);
            }
            await FormGlobalSignal.SendGlobalSignals(this, false);
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
                    tsbt_RunParamSetting.Enabled = !isRun;
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
                Solution.Instance.Load(openFileDialog1.FileName, true);
                SetRunStatus(false);
                toolStripLabel1.Visible = false;
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
            Solution.Instance.Load(Application.StartupPath + "\\空方案.Sol", false);
            Solution.Instance.SolFileName = string.Empty;
            LogHelper.AddLog(MsgLevel.Info, LanguageManager.T("Log.NewSolutionSuccess"), true);
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
            frmSystemSetting.ShowDialog();
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
