using IWshRuntimeLibrary;
using Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Properties;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Forms.SystemSetting
{
    public partial class FrmSystemSetting : FormBase
    {
        /// <summary>
        /// 日志等级设置加载标记，避免初始化复选框时触发保存。
        /// </summary>
        private bool _isLoadingLogSettings;

        /// <summary>常规设置加载标记，避免打开窗体时重复保存或改写开机启动项。</summary>
        private bool _isLoadingGeneralSettings;

        /// <summary>性能设置加载和表格刷新标记，避免初始化触发预览与保存。</summary>
        private bool _isLoadingPerformanceSettings;

        /// <summary>性能设置表格是否存在尚未保存的修改。</summary>
        private bool _hasUnsavedPerformanceChanges;

        /// <summary>方案反序列化事件是否已经订阅。</summary>
        private bool _isRunIntervalEventSubscribed;

        /// <summary>本机性能设置持久化和快照服务。</summary>
        private readonly PerformanceResourceSettingsProvider _performanceSettingsProvider;

        /// <summary>系统设置界面允许调整的性能参数定义。</summary>
        private readonly List<PerformanceSettingDefinition> _performanceSettingDefinitions =
            CreatePerformanceSettingDefinitions();

        /// <summary>最近一次草稿预览结果。</summary>
        private ResourceProfileBuildResult _lastPerformancePreview;

        /// <summary>
        /// 初始化系统设置窗口并加载持久化配置。
        /// </summary>
        public FrmSystemSetting()
            : this(PerformanceResourceSettingsProvider.Default)
        {
        }

        /// <summary>
        /// 使用可替换机器设置提供器创建系统设置窗口，供测试和扩展模块隔离持久化位置。
        /// </summary>
        /// <param name="performanceSettingsProvider">机器性能设置提供器。</param>
        internal FrmSystemSetting(PerformanceResourceSettingsProvider performanceSettingsProvider)
        {
            _performanceSettingsProvider = performanceSettingsProvider ??
                throw new ArgumentNullException(nameof(performanceSettingsProvider));
            InitializeComponent();
            _isLoadingGeneralSettings = true;
            try
            {
                checkBox1.Checked = Settings.Default.IsPowerBoot;
                checkBox2.Checked = Settings.Default.IsAutoLoad;
                textBox1.Text = Settings.Default.SolutionAddress;
                textBox1.Enabled = checkBox2.Checked;
                button1.Enabled = checkBox2.Checked;
                checkBox3.Checked = Settings.Default.IsAutoRun;
                textBox2.Text = Solution.Instance.RunInterval.ToString();
            }
            finally
            {
                _isLoadingGeneralSettings = false;
            }

            LoadLogLevelSettings();
            LoadPerformanceResourceSettings();
        }

        /// <summary>窗体句柄创建后再订阅全局方案事件，保证跨线程刷新可以安全投递。</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_isRunIntervalEventSubscribed)
                return;

            ConfigHelper.DeserializationCompletionEvent += UpdateRunInterval;
            _isRunIntervalEventSubscribed = true;
        }

        /// <summary>方案加载完成后刷新流程运行间隔。</summary>
        private void UpdateRunInterval(object sender, bool e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => UpdateRunInterval(sender, e)));
                return;
            }

            textBox2.Text = Solution.Instance.RunInterval.ToString();
        }

        /// <summary>窗体关闭时解除全局事件订阅，避免已释放窗体被长期持有。</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ReleaseGlobalEventSubscriptions();
            base.OnFormClosed(e);
        }

        /// <summary>解除系统设置窗体持有的全部全局事件订阅，供关闭和直接释放共同调用。</summary>
        private void ReleaseGlobalEventSubscriptions()
        {
            if (!_isRunIntervalEventSubscribed)
                return;

            ConfigHelper.DeserializationCompletionEvent -= UpdateRunInterval;
            _isRunIntervalEventSubscribed = false;
        }

        #region 开机自启

        /// <summary>
        /// 快捷方式名称-任意自定义
        /// </summary>
        private const string QuickName = "机器视觉AI检测系统V1.0";

        /// <summary>
        /// 自动获取系统自动启动目录
        /// </summary>
        private string systemStartPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.Startup); } }

        /// <summary>
        /// 自动获取程序完整路径
        /// </summary>
        private string appAllPath { get { return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; } }

        /// <summary>
        /// 自动获取桌面目录
        /// </summary>
        private string desktopPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }

        /// <summary>
        /// 设置开机自动启动-只需要调用该方法就可以了参数里面的bool变量是控制开机启动的开关的，默认为开启自启启动
        /// </summary>
        /// <param name="onOff">自启开关</param>
        public void SetMeAutoStart(bool onOff = true)
        {
            if (onOff)//开机启动
            {
                //获取启动路径应用程序快捷方式的路径集合
                List<string> shortcutPaths = GetQuickFromFolder(systemStartPath, appAllPath);
                //存在2个以快捷方式则保留一个快捷方式-避免重复多于
                if (shortcutPaths.Count >= 2)
                {
                    for (int i = 1; i < shortcutPaths.Count; i++)
                    {
                        DeleteFile(shortcutPaths[i]);
                    }
                }
                else if (shortcutPaths.Count < 1)//不存在则创建快捷方式
                {
                    CreateShortcut(systemStartPath, QuickName, appAllPath);
                }
            }
            else//开机不启动
            {
                //获取启动路径应用程序快捷方式的路径集合
                List<string> shortcutPaths = GetQuickFromFolder(systemStartPath, appAllPath);
                //存在快捷方式则遍历全部删除
                if (shortcutPaths.Count > 0)
                {
                    for (int i = 0; i < shortcutPaths.Count; i++)
                    {
                        DeleteFile(shortcutPaths[i]);
                    }
                }
            }
        }

        /// <summary>
        ///  向目标路径创建指定文件的快捷方式
        /// </summary>
        /// <param name="directory">目标目录</param>
        /// <param name="shortcutName">快捷方式名字</param>
        /// <param name="targetPath">文件完全路径</param>
        /// <param name="description">描述</param>
        /// <param name="iconLocation">图标地址</param>
        /// <returns>成功或失败</returns>
        private bool CreateShortcut(string directory, string shortcutName, string targetPath, string description = null, string iconLocation = null)
        {
            try
            {
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);                         //目录不存在则创建
                //添加引用 Com 中搜索 Windows Script Host Object Model
                string shortcutPath = Path.Combine(directory, string.Format("{0}.lnk", shortcutName));          //合成路径
                WshShell shell = new IWshRuntimeLibrary.WshShell();
                IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);    //创建快捷方式对象
                shortcut.TargetPath = targetPath;                                                               //指定目标路径
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);                                  //设置起始位置
                shortcut.WindowStyle = 1;                                                                       //设置运行方式，默认为常规窗口
                shortcut.Description = description;                                                             //设置备注
                shortcut.IconLocation = string.IsNullOrWhiteSpace(iconLocation) ? targetPath : iconLocation;    //设置图标路径
                shortcut.Save();                                                                                //保存快捷方式
                return true;
            }
            catch (Exception ex)
            {
                string temp = ex.Message;
                temp = "";
            }
            return false;
        }

        /// <summary>
        /// 获取指定文件夹下指定应用程序的快捷方式路径集合
        /// </summary>
        /// <param name="directory">文件夹</param>
        /// <param name="targetPath">目标应用程序路径</param>
        /// <returns>目标应用程序的快捷方式</returns>
        private List<string> GetQuickFromFolder(string directory, string targetPath)
        {
            List<string> tempStrs = new List<string>();
            tempStrs.Clear();
            string tempStr = null;
            // 获取系统启动路径下的lnk文件
            string[] files = Directory.GetFiles(directory, "*.lnk");
            if (files == null || files.Length < 1)
            {
                return tempStrs;
            }
            for (int i = 0; i < files.Length; i++)
            {
                //files[i] = string.Format("{0}\\{1}", directory, files[i]);
                tempStr = GetAppPathFromQuick(files[i]);
                if (tempStr == targetPath)
                {
                    tempStrs.Add(files[i]);
                }
            }
            return tempStrs;
        }

        /// <summary>
        /// 获取快捷方式的目标文件路径-用于判断是否已经开启了自动启动
        /// </summary>
        /// <param name="shortcutPath"></param>
        /// <returns></returns>
        private string GetAppPathFromQuick(string shortcutPath)
        {
            //快捷方式文件的路径 = @"d:\Test.lnk";
            if (System.IO.File.Exists(shortcutPath))
            {
                WshShell shell = new WshShell();
                // 创建快捷方式对象
                IWshShortcut shortct = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                //快捷方式文件指向的路径.Text = 当前快捷方式文件IWshShortcut类.TargetPath;
                return shortct.TargetPath;
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// 根据路径删除文件-用于取消自启时从计算机自启目录删除程序的快捷方式
        /// </summary>
        /// <param name="path">路径</param>
        private void DeleteFile(string path)
        {
            FileAttributes attr = System.IO.File.GetAttributes(path);
            if (attr == FileAttributes.Directory)
            {
                Directory.Delete(path, true);
            }
            else
            {
                System.IO.File.Delete(path);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoadingGeneralSettings)
                return;

            if (checkBox1.Checked)
            {
                SetMeAutoStart();
            }
            else
            {
                SetMeAutoStart(false);
            }
            Settings.Default.IsPowerBoot = checkBox1.Checked;
            Settings.Default.Save();
        }

        #endregion


        #region 默认启动方案


        /// <summary>
        /// 选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoadingGeneralSettings)
                return;

            Properties.Settings.Default.IsAutoLoad = checkBox2.Checked;
            Properties.Settings.Default.Save();

            textBox1.Enabled = checkBox2.Checked;
            button1.Enabled = checkBox2.Checked;
        }

        /// <summary>
        /// 选择默认方案路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = openFileDialog1.FileName;
                //保存配置到程序设置中
                Properties.Settings.Default.SolutionAddress = textBox1.Text;
                Properties.Settings.Default.Save();
            }
        }

        #endregion


        #region 设置运行间隔时间

        /// <summary>
        /// 流程循环运行时设置间隔时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (_isLoadingGeneralSettings)
                return;

            try
            {
                Solution.Instance.RunInterval = int.Parse(textBox2.Text);
            }
            catch (Exception)
            {
                MessageBoxTD.Show("请设置合理的值！");
            }
        }

        #endregion

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoadingGeneralSettings)
                return;

            Settings.Default.IsAutoRun = checkBox3.Checked;
            Settings.Default.Save();
        }

        #region 日志记录等级

        /// <summary>
        /// 加载日志等级记录开关到界面复选框。
        /// </summary>
        private void LoadLogLevelSettings()
        {
            _isLoadingLogSettings = true;
            try
            {
                checkBox4.Checked = LogLevelSettings.IsEnabled(MsgLevel.Debug);
                checkBox5.Checked = LogLevelSettings.IsEnabled(MsgLevel.Info);
                checkBox6.Checked = LogLevelSettings.IsEnabled(MsgLevel.Warn);
                checkBox7.Checked = LogLevelSettings.IsEnabled(MsgLevel.Exception);
                checkBox8.Checked = LogLevelSettings.IsEnabled(MsgLevel.Fatal);
            }
            finally
            {
                _isLoadingLogSettings = false;
            }
        }

        /// <summary>
        /// 保存单个日志等级记录开关。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="enabled">是否允许记录。</param>
        private void SaveLogLevelSetting(MsgLevel level, bool enabled)
        {
            if (_isLoadingLogSettings)
            {
                return;
            }

            LogLevelSettings.SetEnabled(level, enabled);
        }

        /// <summary>
        /// 调试日志记录开关变更。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            SaveLogLevelSetting(MsgLevel.Debug, checkBox4.Checked);
        }

        /// <summary>
        /// 消息日志记录开关变更。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            SaveLogLevelSetting(MsgLevel.Info, checkBox5.Checked);
        }

        /// <summary>
        /// 警告日志记录开关变更。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            SaveLogLevelSetting(MsgLevel.Warn, checkBox6.Checked);
        }

        /// <summary>
        /// 异常日志记录开关变更。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            SaveLogLevelSetting(MsgLevel.Exception, checkBox7.Checked);
        }

        /// <summary>
        /// 致命日志记录开关变更。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            SaveLogLevelSetting(MsgLevel.Fatal, checkBox8.Checked);
        }

        #endregion

        #region 性能与资源设置

        /// <summary>建立系统设置界面允许手动覆盖的性能参数目录。</summary>
        private static List<PerformanceSettingDefinition> CreatePerformanceSettingDefinitions()
        {
            return new List<PerformanceSettingDefinition>
            {
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.CpuReserveCoreCount, "CPU与并发", "系统保留核心数", "1～物理核心数-1", 1, 255, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.CpuHeavyMaxConcurrency, "CPU与并发", "CPU重任务最大并发", "1～32，且不超过可用核心", 1, 32, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ImageConvertMaxConcurrency, "CPU与并发", "图像转换最大并发", "1～8，且不超过CPU总并发", 1, 8, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ImageSaveWorkerCount, "图片保存", "图片保存工作线程", "1～8", 1, 8, false, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.CpuHighWatermarkPercent, "CPU动态调节", "CPU高水位(%)", "50～100", 50, 100, true, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.CpuRecoveryWatermarkPercent, "CPU动态调节", "CPU恢复水位(%)", "20～高水位-5", 20, 95, true, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.CpuHighDurationSeconds, "CPU动态调节", "高水位持续(秒)", "1～60", 1, 60, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.CpuRecoveryDurationSeconds, "CPU动态调节", "恢复水位持续(秒)", "1～300", 1, 300, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ResourceAdjustmentIntervalMs, "CPU动态调节", "资源评估周期(ms)", "1000～60000", 1000, 60000, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ResourceAdjustmentCooldownSeconds, "CPU动态调节", "调整冷却时间(秒)", "5～600", 5, 600, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ConcurrencyAdjustmentStep, "CPU动态调节", "单次并发调整步长", "1～4", 1, 4, false, PerformanceSettingApplyKind.CpuAndRuntime),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.FlowImageMemoryBudgetPercent, "内存规划", "流程图像预算比例(%)", "5～30", 5, 30, true, PerformanceSettingApplyKind.PlanningOnly),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveQueueMemoryBudgetPercent, "图片保存", "保存队列内存比例(%)", "1～15", 1, 15, true, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.MemoryLowWatermarkPercent, "内存规划", "可用内存低水位(%)", "5～40", 5, 40, true, PerformanceSettingApplyKind.PlanningOnly),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.MemoryCriticalWatermarkPercent, "内存规划", "可用内存严重水位(%)", "2～低水位-2", 2, 38, true, PerformanceSettingApplyKind.PlanningOnly),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.MemoryLowWatermarkMinimumMb, "内存规划", "低水位最小值(MB)", "256～8192", 256, 8192, false, PerformanceSettingApplyKind.PlanningOnly),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.MemoryCriticalMinimumMb, "内存规划", "严重水位最小值(MB)", "128～4096，且低于低水位", 128, 4096, false, PerformanceSettingApplyKind.PlanningOnly),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ImageSizeSampleCount, "图像资源", "图像大小采样数量", "1～500", 1, 500, false, PerformanceSettingApplyKind.ImageSample),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.ShutdownResourceDrainTimeoutMs, "图像资源", "停止资源排空等待(ms)", "0～60000", 0, 60000, false, PerformanceSettingApplyKind.RuntimeAndImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.UiMaxRefreshFps, "界面显示", "每窗口最大刷新帧率", "1～60", 1, 60, false, PerformanceSettingApplyKind.UserInterface),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveQueueInitialCapacity, "图片保存", "保存队列初始容量", "1～50", 1, 50, false, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveQueueMaximumCapacity, "图片保存", "保存队列最大容量", "1～500", 1, 500, false, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveDiskLowSpaceMb, "图片保存", "磁盘低空间水位(MB)", "256～102400", 256, 102400, false, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveDiskCriticalSpaceMb, "图片保存", "磁盘严重水位(MB)", "64～低空间水位-1", 64, 102399, false, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveSlowThresholdMs, "图片保存", "单目标慢写阈值(ms)", "10～60000", 10, 60000, false, PerformanceSettingApplyKind.ImageSave),
                new PerformanceSettingDefinition(PerformanceResourceSettingKeys.SaveDiskProbeIntervalMs, "图片保存", "磁盘探测周期(ms)", "100～60000", 100, 60000, false, PerformanceSettingApplyKind.ImageSave)
            };
        }

        /// <summary>从机器配置加载性能参数，并生成第一份自动/最终值预览。</summary>
        private void LoadPerformanceResourceSettings()
        {
            _isLoadingPerformanceSettings = true;
            try
            {
                EnsurePerformanceSettingRows();
                MachinePerformanceResourceSettings settings = _performanceSettingsProvider.GetSnapshot();
                checkBoxAdaptiveCpu.Checked = settings.AdaptiveResourceManagementEnabled;
                foreach (DataGridViewRow row in dataGridViewPerformance.Rows)
                {
                    PerformanceSettingDefinition definition = row.Tag as PerformanceSettingDefinition;
                    if (definition == null)
                        continue;

                    string manualValue;
                    bool hasManualValue = settings.ManualOverrides.TryGetValue(definition.Key, out manualValue);
                    row.Cells[columnManualEnabled.Name].Value = hasManualValue;
                    row.Cells[columnManualValue.Name].Value = hasManualValue ? manualValue : string.Empty;
                    UpdateManualValueCellState(row, hasManualValue);
                }

                _hasUnsavedPerformanceChanges = false;
            }
            catch (Exception ex)
            {
                labelPerformanceStatus.Text = "性能设置加载失败：" + ex.Message;
            }
            finally
            {
                _isLoadingPerformanceSettings = false;
            }

            RefreshPerformancePreview(false);
            string warning = _performanceSettingsProvider.GetLastLoadWarning();
            if (!string.IsNullOrWhiteSpace(warning))
                labelPerformanceStatus.Text = warning;
        }

        /// <summary>按稳定目录创建表格行；自动值和最终值由后续预览填充。</summary>
        private void EnsurePerformanceSettingRows()
        {
            if (dataGridViewPerformance.Rows.Count == _performanceSettingDefinitions.Count)
                return;

            dataGridViewPerformance.Rows.Clear();
            foreach (PerformanceSettingDefinition definition in _performanceSettingDefinitions)
            {
                int rowIndex = dataGridViewPerformance.Rows.Add(
                    definition.Category,
                    definition.DisplayName,
                    string.Empty,
                    false,
                    string.Empty,
                    string.Empty,
                    definition.RangeText,
                    "预览值；点击保存并应用");
                dataGridViewPerformance.Rows[rowIndex].Tag = definition;
            }
        }

        /// <summary>从当前表格生成完整机器设置草稿，并拒绝非法数字。</summary>
        private bool TryCollectPerformanceSettings(
            out MachinePerformanceResourceSettings settings,
            out string error)
        {
            settings = new MachinePerformanceResourceSettings
            {
                AdaptiveResourceManagementEnabled = checkBoxAdaptiveCpu.Checked
            };
            foreach (DataGridViewRow row in dataGridViewPerformance.Rows)
            {
                PerformanceSettingDefinition definition = row.Tag as PerformanceSettingDefinition;
                bool useManual = Convert.ToBoolean(row.Cells[columnManualEnabled.Name].Value ?? false);
                if (definition == null || !useManual)
                    continue;

                string text = Convert.ToString(row.Cells[columnManualValue.Name].Value)?.Trim();
                decimal maximum = GetPerformanceSettingMaximum(definition);
                string normalized;
                if (!definition.TryNormalize(text, maximum, out normalized, out error))
                    return false;

                settings.ManualOverrides[definition.Key] = normalized;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>按本机物理核心数收紧系统保留核心参数的界面校验上限。</summary>
        private decimal GetPerformanceSettingMaximum(PerformanceSettingDefinition definition)
        {
            if (definition.Key != PerformanceResourceSettingKeys.CpuReserveCoreCount)
                return definition.Maximum;

            if (_lastPerformancePreview?.AutomaticProfile?.Hardware == null)
                return definition.Maximum;

            int physicalCoreCount = _lastPerformancePreview.AutomaticProfile.Hardware.PhysicalCoreCount;
            return Math.Max(1, physicalCoreCount - 1);
        }

        /// <summary>使用当前草稿刷新硬件摘要、自动值、最终值和行状态。</summary>
        private void RefreshPerformancePreview(bool resetHardwareCache)
        {
            if (_isLoadingPerformanceSettings)
                return;

            MachinePerformanceResourceSettings settings;
            string error;
            if (!TryCollectPerformanceSettings(out settings, out error))
            {
                labelPerformanceStatus.Text = error;
                return;
            }

            try
            {
                ResourceProfileBuildResult preview = Solution.Instance.BuildRuntimeResourceProfilePreview(
                    settings,
                    resetHardwareCache);
                if (preview?.AutomaticProfile == null || preview.EffectiveProfile == null)
                    throw new InvalidOperationException("资源档案预览为空。");

                _lastPerformancePreview = preview;
                HardwareResourceSnapshot hardware = preview.AutomaticProfile.Hardware;
                ResourceWorkloadSnapshot workload = preview.AutomaticProfile.Workload ?? new ResourceWorkloadSnapshot();
                labelHardwareSummary.Text = hardware?.ToLogText() ?? "未读取到硬件信息。";
                labelWorkloadSummary.Text = $"当前方案：启用流程{workload.EnabledProcessCount}个，相机{workload.CameraCount}台，平均图像资源{workload.AverageImageBytes}字节。";
                AutomaticResourceProfile effectiveProfile = preview.EffectiveProfile;
                labelProfileSummary.Text = $"本次预览：CPU并发{effectiveProfile.CpuHeavyMaxConcurrency}，转换{effectiveProfile.ImageConvertMaxConcurrency}，保存线程{effectiveProfile.ImageSaveWorkerCount}，保存队列{effectiveProfile.SaveQueueCalculatedCapacity}项/{effectiveProfile.SaveQueueMemoryBudgetMb}MB，UI {effectiveProfile.UiMaxRefreshFps} FPS。";

                _isLoadingPerformanceSettings = true;
                try
                {
                    foreach (DataGridViewRow row in dataGridViewPerformance.Rows)
                    {
                        PerformanceSettingDefinition definition = row.Tag as PerformanceSettingDefinition;
                        if (definition == null)
                            continue;

                        string automaticValue;
                        string effectiveValue;
                        preview.AutomaticParameterValues.TryGetValue(definition.Key, out automaticValue);
                        preview.EffectiveParameterValues.TryGetValue(definition.Key, out effectiveValue);
                        row.Cells[columnAutomaticValue.Name].Value = automaticValue ?? string.Empty;
                        row.Cells[columnEffectiveValue.Name].Value = effectiveValue ?? string.Empty;
                        row.Cells[columnRecommendedRange.Name].Value =
                            definition.Key == PerformanceResourceSettingKeys.CpuReserveCoreCount
                                ? $"1～{GetPerformanceSettingMaximum(definition):0}"
                                : definition.RangeText;
                        row.Cells[columnApplyState.Name].Value = GetPreviewApplyState(definition);
                    }
                }
                finally
                {
                    _isLoadingPerformanceSettings = false;
                }

                labelPerformanceStatus.Text = _hasUnsavedPerformanceChanges
                    ? "存在未保存修改；自动值和最终值仅为草稿预览。"
                    : "已加载机器设置；点击“保存并应用”后会显示各运行区域的实际生效状态。";
            }
            catch (Exception ex)
            {
                labelPerformanceStatus.Text = "性能设置预览失败：" + ex.Message;
            }
        }

        /// <summary>读取预览阶段的行状态文案。</summary>
        private string GetPreviewApplyState(PerformanceSettingDefinition definition)
        {
            if (definition.ApplyKind == PerformanceSettingApplyKind.PlanningOnly)
                return "规划值，尚未启用内存硬管控";
            return _hasUnsavedPerformanceChanges ? "未保存" : "已保存，待应用确认";
        }

        /// <summary>根据是否启用手动值更新单元格编辑状态和底色。</summary>
        private void UpdateManualValueCellState(DataGridViewRow row, bool enabled)
        {
            DataGridViewCell cell = row.Cells[columnManualValue.Name];
            cell.ReadOnly = !enabled;
            cell.Style.BackColor = enabled
                ? System.Drawing.Color.White
                : System.Drawing.SystemColors.Control;
            cell.Style.ForeColor = enabled
                ? System.Drawing.SystemColors.ControlText
                : System.Drawing.SystemColors.GrayText;
        }

        /// <summary>把保存和运行应用结果写回每个参数行。</summary>
        private void UpdatePerformanceApplyStates(RuntimeResourceProfileApplyResult result)
        {
            foreach (DataGridViewRow row in dataGridViewPerformance.Rows)
            {
                PerformanceSettingDefinition definition = row.Tag as PerformanceSettingDefinition;
                if (definition == null)
                    continue;

                string state;
                if (!result.Success)
                {
                    state = "已保存，运行应用失败";
                }
                else if (definition.ApplyKind == PerformanceSettingApplyKind.PlanningOnly)
                {
                    state = "已保存；规划值尚未启用硬管控";
                }
                else if (definition.ApplyKind == PerformanceSettingApplyKind.ImageSave)
                {
                    state = result.ImageSaveState == ImageSaveProfileApplyState.Pending
                        ? "已保存；待下一轮安全应用"
                        : result.ImageSaveState == ImageSaveProfileApplyState.UseLatestWhenCreated
                            ? "已保存；工作池创建时应用"
                            : "已应用";
                }
                else if (definition.ApplyKind == PerformanceSettingApplyKind.ImageSample)
                {
                    state = result.ImageSampleApplied ? "已应用" : "已保存；待当前流程结束";
                }
                else if (definition.ApplyKind == PerformanceSettingApplyKind.RuntimeAndImageSave)
                {
                    state = result.CpuAndDisplayApplied &&
                        result.ImageSaveState != ImageSaveProfileApplyState.Pending
                            ? "已应用"
                            : result.CpuAndDisplayApplied
                                ? "运行停止已应用；保存池待下一轮"
                                : "已保存；待下一轮安全应用";
                }
                else
                {
                    state = result.CpuAndDisplayApplied ? "已应用" : "已保存，待应用";
                }

                row.Cells[columnApplyState.Name].Value = state;
            }
        }

        /// <summary>CPU动态并发开关变更后刷新草稿预览。</summary>
        private void checkBoxAdaptiveCpu_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoadingPerformanceSettings)
                return;
            _hasUnsavedPerformanceChanges = true;
            RefreshPerformancePreview(false);
        }

        /// <summary>立即重新读取硬件信息并刷新自动推荐。</summary>
        private void buttonRefreshHardware_Click(object sender, EventArgs e)
        {
            RefreshPerformancePreview(true);
        }

        /// <summary>清除全部机器手动值并恢复CPU动态调节默认开启。</summary>
        private void buttonRestoreAutomatic_Click(object sender, EventArgs e)
        {
            _isLoadingPerformanceSettings = true;
            try
            {
                checkBoxAdaptiveCpu.Checked = true;
                foreach (DataGridViewRow row in dataGridViewPerformance.Rows)
                {
                    row.Cells[columnManualEnabled.Name].Value = false;
                    row.Cells[columnManualValue.Name].Value = string.Empty;
                    UpdateManualValueCellState(row, false);
                }
            }
            finally
            {
                _isLoadingPerformanceSettings = false;
            }

            _hasUnsavedPerformanceChanges = true;
            RefreshPerformancePreview(false);
        }

        /// <summary>原子保存完整机器设置并应用到当前允许安全更新的运行区域。</summary>
        private void buttonSavePerformanceSettings_Click(object sender, EventArgs e)
        {
            MachinePerformanceResourceSettings settings;
            string error;
            if (!TryCollectPerformanceSettings(out settings, out error))
            {
                labelPerformanceStatus.Text = error;
                MessageBoxTD.Show(error);
                return;
            }

            try
            {
                _performanceSettingsProvider.Save(settings);
                RuntimeResourceProfileApplyResult result =
                    Solution.Instance.ApplyMachinePerformanceResourceSettings(settings);
                _hasUnsavedPerformanceChanges = false;
                RefreshPerformancePreview(false);
                UpdatePerformanceApplyStates(result);
                labelPerformanceStatus.Text = result.Message;
                if (!result.Success)
                    MessageBoxTD.Show(result.Message);
            }
            catch (Exception ex)
            {
                labelPerformanceStatus.Text = "性能设置保存失败：" + ex.Message;
                MessageBoxTD.Show(labelPerformanceStatus.Text);
            }
        }

        /// <summary>手动值复选框或数值提交后更新编辑状态和草稿预览。</summary>
        private void dataGridViewPerformance_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_isLoadingPerformanceSettings || e.RowIndex < 0)
                return;

            DataGridViewRow row = dataGridViewPerformance.Rows[e.RowIndex];
            if (e.ColumnIndex == columnManualEnabled.Index)
            {
                bool enabled = Convert.ToBoolean(row.Cells[columnManualEnabled.Name].Value ?? false);
                if (enabled && string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnManualValue.Name].Value)))
                {
                    _isLoadingPerformanceSettings = true;
                    try
                    {
                        row.Cells[columnManualValue.Name].Value =
                            Convert.ToString(row.Cells[columnAutomaticValue.Name].Value);
                    }
                    finally
                    {
                        _isLoadingPerformanceSettings = false;
                    }
                }
                UpdateManualValueCellState(row, enabled);
            }

            if (e.ColumnIndex == columnManualEnabled.Index || e.ColumnIndex == columnManualValue.Index)
            {
                _hasUnsavedPerformanceChanges = true;
                RefreshPerformancePreview(false);
            }
        }

        /// <summary>复选框点击后立即提交单元格值，避免必须离开单元格才刷新。</summary>
        private void dataGridViewPerformance_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewPerformance.IsCurrentCellDirty &&
                dataGridViewPerformance.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridViewPerformance.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        /// <summary>表格格式错误只在状态栏提示，避免WinForms默认异常弹窗打断操作。</summary>
        private void dataGridViewPerformance_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            labelPerformanceStatus.Text = "性能参数格式无效，请检查当前手动值。";
        }

        #endregion
    }
}
