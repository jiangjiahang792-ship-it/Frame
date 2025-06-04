using Logger;
using System;
using System.IO;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using TDJS_Vision.Forms;
using TDJS_Vision.Forms.Helper;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.ResultView;
using Sunny.UI;
using TDJS_Vision.Properties;
using TDJS_Vision.Forms.SystemSetting;
using TDJS_Vision.Forms.LightAdd;
using TDJS_Vision.Forms.CameraAdd;
using TDJS_Vision.Forms.PLCAdd;
using TDJS_Vision.Forms.ModbusAdd;
using TDJS_Vision.Forms.TCPAdd;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.Login;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace TDJS_Vision
{
    internal partial class FormMain : Form
    {
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
        /// 流程创建窗口
        /// </summary>
        static FormNewProcessWizard FrmNewProcessWizard = new FormNewProcessWizard();
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

        #endregion
        private UserRole Role { get; set; } = UserRole.Unknown; // 登录的用户角色
        /// <summary>
        /// 窗口布局配置
        /// </summary>
        private readonly string DockPanelConfig = Application.StartupPath + "\\DockPanel.config";
        /// <summary>
        /// 反序列化DockContent代理
        /// </summary>
        private DeserializeDockContent DeserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);

        public FormMain()
        {
            InitializeComponent();
            this.dockPanel1.Theme = new VS2015LightTheme();
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

            // 设置主窗口标题
            Text = $"鹰眼智检系统 V{Solution.Instance.SolVersion}";

            // 初始化海康相机SDK
            CameraHik.InitSDK();

            // 工具菜单状态同步
            运行日志ToolStripMenuItem.Checked = FrmLoggerDlg.Visible;
            检测结果ToolStripMenuItem.Checked = FrmResultDlg.Visible;
            图像显示ToolStripMenuItem.Checked = FrmImgeDlg.Visible;
            默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible ? true : false;

            // 布局同步更新事件
            FrmImgeDlg.HideChangedEvent += HideChangedEvent;
            FrmResultDlg.HideChangedEvent += HideChangedEvent;
            FrmLoggerDlg.HideChangedEvent += HideChangedEvent;

            // 界面锁事件
            FormLogin.LoginEvent += OnLoginEvent;
            FormLogin.LogoutEvent += OnLogoutEvent;

            // 保存快捷键事件处理
            FrmNewProcessWizard.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmLightAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmCameraAdd.OnShotKeySavePressed += OnShotKeySavePressed;
            FrmPLCAdd.OnShotKeySavePressed += OnShotKeySavePressed;

            // 加载默认方案
            AutoLoadSolutionAsync();

            // 设置控件Enable为未登录状态
            文件ToolStripMenuItem.Enabled = false;
            设置ToolStripMenuItem.Enabled = false;
            toolStrip1.Enabled = false;

#if DEBUG
            //调试时登录工程师权限
            FormLogin.LoginTest(UserRole.Engineer); // 测试登录
#endif
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
                    SetRunStatus(Role, true); // 设置为正在加载方案状态
                    Solution.Instance.Load(Settings.Default.SolutionAddress, true);
                    toolStripLabel1.Visible = false;
                    SetRunStatus(Role, false);
                }
                catch (Exception)
                {
                    LogHelper.AddLog(MsgLevel.Exception, "方案加载失败！", true);
                }
                if (Settings.Default.IsAutoRun)
                {
                    SetRunStatus(Role, true);
                    await Solution.Instance.Run(true);
                    SetRunStatus(Role, false);
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
                case UserRole.Engineer:
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
                    tsbt_TCPManager.Enabled = true;
                    tsbt_RunParamSetting.Enabled = true;
                    break;
                case UserRole.Manager:
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
                    tsbt_RunParamSetting.Enabled = true;
                    break;
                case UserRole.Operator:
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
            if(MessageBox.Show("确认关闭程序？", "提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            // 检测任务正在运行提示
            if (Solution.Instance.IsRunning)
            {
                e.Cancel = true;
                MessageBox.Show("请先停止当前任务再关闭！");
                return;
            }

            // 释放方案资源
            Solution.Instance.SolReset();

            // 海康相机SDK反序列化
            CameraHik.Finalize();

            // 保存主窗口布局
            this.dockPanel1.SaveAsXml(DockPanelConfig);
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
                //LoadDefaultDockPanel();
            }
        }
        /// <summary>
        /// 默认窗口布局
        /// </summary>
        private void LoadDefaultDockPanel()
        {
            FrmImgeDlg.Show(dockPanel1, DockState.Document);
            FrmResultDlg.Show(dockPanel1, DockState.DockRight);
            FrmLoggerDlg.Show(dockPanel1, DockState.DockBottom);
            图像显示ToolStripMenuItem.Checked = true;
            检测结果ToolStripMenuItem.Checked = true;
            运行日志ToolStripMenuItem.Checked = true;
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

            else
                return null;
        }

        private void 文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                switch (item.Text)
                {
                    case "新建方案":
                        新建方案ToolStripMenuItem_Click(sender, e);
                        break;
                    case "打开方案":
                        打开方案ToolStripMenuItem_Click(sender, e);
                        break;
                    case "另存方案":
                        另存方案ToolStripMenuItem_Click(sender, e);
                        break;
                    case "保存方案":
                        保存方案ToolStripMenuItem_Click(sender, e);
                        break;
                    case "退出":
                        if (MessageBox.Show("确认退出？") == DialogResult.OK)
                            Close();
                        break;
                    default:
                        break;
                }
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
            switch (tsbt.Text)
            {
                case "新建方案":
                    新建方案ToolStripMenuItem_Click(null, null);
                    break;
                case "打开方案":
                    打开方案ToolStripMenuItem_Click(null, null);
                    break;
                case "另存方案":
                    另存方案ToolStripMenuItem_Click(null, null);
                    break;
                case "保存方案":
                    保存方案ToolStripMenuItem_Click(null, null);
                    break;
                case "流程管理":
                    流程管理ToolStripMenuItem_Click(null, null);
                    break;
                case "光源管理":
                    光源管理ToolStripMenuItem_Click(null, null);
                    break;
                case "相机管理":
                    相机管理ToolStripMenuItem_Click(null, null);
                    break;
                case "PLC管理":
                    PLC管理ToolStripMenuItem_Click(null, null);
                    break;
                case "Modbus设备":
                    Modbus设备ToolStripMenuItem_Click(null, null);
                    break;
                case "TCP设备":
                    TCP设备ToolStripMenuItem_Click(null, null);
                    break;
                case "运行参数":
                    运行参数ToolStripMenuItem_Click(null, null);
                    break;
                case "循环运行":
                    循环运行ToolStripMenuItem_Click(null, null);
                    break;
                case "单次运行":
                    单次运行ToolStripMenuItem_Click(null, null);
                    break;
                case "停止运行":
                    停止运行ToolStripMenuItem_Click(null, null);
                    break;
            }
        }

        private void 流程管理ToolStripMenuItem_Click(object value1, object value2)
        {
            FrmNewProcessWizard.ShowDialog();
        }

        private async void 停止运行ToolStripMenuItem_Click(object value1, object value2)
        {
            Solution.Instance.Stop();
            SetRunStatus(Role, false);
        }

        private async void 循环运行ToolStripMenuItem_Click(object value1, object value2)
        {
            SetRunStatus(Role, true);
            await Solution.Instance.Run(true);
            SetRunStatus(Role, false);
        }

        private async void 单次运行ToolStripMenuItem_Click(object value1, object value2)
        {
            SetRunStatus(Role, true);
            await Solution.Instance.Run(false);
            SetRunStatus(Role, false);
        }

        /// <summary>
        /// 设置运行状态，启用/禁用控件
        /// </summary>
        /// <param name="isRunning"></param>
        /// <param name="all">停止运行按钮是否和其他一致</param>
        private void SetRunStatus(UserRole role, bool isRun)
        {
            // 运行和停止按钮
            tsbt_SolRunOnce.Enabled = !isRun;
            tsbt_SolRunLoop.Enabled = !isRun;
            tsbt_SolRunStop.Enabled = isRun;
            登录ToolStripMenuItem.Enabled = !isRun;
            switch (role)
            {
                case UserRole.Engineer:
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
                    tsbt_RunParamSetting.Enabled = !isRun;
                    // 文件
                    文件ToolStripMenuItem.Enabled = !isRun;
                    视图ToolStripMenuItem.Enabled = !isRun;
                    设置ToolStripMenuItem.Enabled = !isRun;
                    帮助ToolStripMenuItem.Enabled = !isRun;
                    break;
                case UserRole.Manager:
                    tsbt_RunParamSetting.Enabled = !isRun;
                    break;
                case UserRole.Operator:
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
            saveFileDialog1.Title = "方案另存为";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Solution.Instance.Save(saveFileDialog1.FileName);
                    LogHelper.AddLog(MsgLevel.Info, $"方案另存成功！路径：{saveFileDialog1.FileName}", true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"方案保存失败！原因：{ex.Message}");
                }
            }
        }

        private void 保存方案ToolStripMenuItem_Click(object value1, object value2)
        {
            if (Solution.Instance.SolFileName.IsNullOrEmpty())
            {
                saveFileDialog1.Title = "请选择方案保存路径";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    Solution.Instance.SolFileName = saveFileDialog1.FileName;
                else
                    return;
            }
            try
            {
                Solution.Instance.Save(Solution.Instance.SolFileName);
                LogHelper.AddLog(MsgLevel.Info, $"方案保存成功！路径：{Solution.Instance.SolFileName}", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"方案保存失败！原因：{ex.Message}");
            }
        }

        private bool isAutoLoadSol = true;

        private void 打开方案ToolStripMenuItem_Click(object value1, object value2)
        {
            openFileDialog1.Title = "请选择要打开的方案";
            if (openFileDialog1.ShowDialog()  == DialogResult.OK)
            {
                toolStripLabel1.Visible = true;
                SetRunStatus(Role, true); // 设置为正在加载方案状态
                Solution.Instance.Load(openFileDialog1.FileName, true);
                SetRunStatus(Role, false);
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
            LogHelper.AddLog(MsgLevel.Info, $"新建方案成功！", true);
        }

        private void 联系我们ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //contactUsFormForm.ShowDialog();
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
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible ? true : false;
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
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible ? true : false;
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
                FrmLoggerDlg.Show(); // 如果窗口隐藏，显示它
                运行日志ToolStripMenuItem.Checked = true;
                默认视图ToolStripMenuItem.Checked = FrmLoggerDlg.Visible && FrmResultDlg.Visible && FrmImgeDlg.Visible ? true : false;
            }
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }

        private void HideChangedEvent(object sender, EventArgs e)
        {
            FrmImageViewer frmImageViewer = sender as FrmImageViewer;
            FrmResultView frmResultView = sender as FrmResultView;
            FrmLogger frmLogger = sender as FrmLogger;
            if (frmImageViewer != null)
                图像显示ToolStripMenuItem.Checked = frmImageViewer.Visible;
            else if (frmResultView != null)
                检测结果ToolStripMenuItem.Checked = frmResultView.Visible;
            else if (frmLogger != null)
                运行日志ToolStripMenuItem.Checked = frmLogger.Visible;
        }

        private void 登录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            用户登录ToolStripMenuItem_Click(null, null);
        }

        private void 系统设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmSystemSetting.ShowDialog();
        }
    }
}
