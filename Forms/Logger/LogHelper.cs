using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Logger
{
    public partial class LogHelper : UserControl
    {
        /// <summary>
        /// 单次 UI 刷新最多处理的日志数量，限制每个消息循环节拍的工作量。
        /// </summary>
        private const int UiRefreshBatchSize = 100;

        /// <summary>
        /// “全部”日志列表允许保留的最大行数。
        /// </summary>
        private const int AllLogDisplayLimit = 1000;

        /// <summary>
        /// 单个日志等级列表允许保留的最大行数。
        /// </summary>
        private const int LevelLogDisplayLimit = 300;

        /// <summary>
        /// Debug性能诊断允许占用的最大待写日志数量，防止诊断洪峰无限推高内存。
        /// </summary>
        private const int DiagnosticPendingLogLimit = 4096;

        /// <summary>
        /// 保存等待显示日志的有界缓冲区。
        /// </summary>
        private readonly ILogUiBuffer _logUiBuffer;

        static readonly string LogDictory = Environment.CurrentDirectory + @"\Logs\";
        static event EventHandler<LevelAndInfo> LogAddEvent;
        private static readonly BlockingCollection<Action> _logQueue = new BlockingCollection<Action>();

        /// <summary>
        /// 因待写队列达到上限而放弃的Debug性能诊断数量。
        /// </summary>
        private static long _droppedDiagnosticLogCount;

        static LogHelper()
        {
            Task.Factory.StartNew(() =>
            {
                foreach (var action in _logQueue.GetConsumingEnumerable())
                {
                    try { action?.Invoke(); } catch { }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static bool OnlyLogException = false; // 仅记录异常日志

        // 高精度时间戳机制（规避 Windows DateTime.Now 的 ~15ms 精度限制）
        private static readonly DateTime _baseTime = DateTime.Now;
        private static readonly Stopwatch _timestampStopwatch = Stopwatch.StartNew();

        private static string GetHighResTimestamp()
        {
            // 通过 Stopwatch 的高精度 Elapsed 累加到初始时间上，提供微秒（.ffffff）级时间戳
            return _baseTime.Add(_timestampStopwatch.Elapsed).ToString("yyyy-MM-dd HH:mm:ss.ffffff");
        }
        #region 日志级别和信息结构体
        struct LevelAndInfo
        {
            public MsgLevel Level { get; set; }
            public string Info { get; set; }
        }
        #endregion

        #region 单条日志结构体
        public struct SingleLog
        {
            public string LogTime;
            public string LogLevel;
            public string LogInfo;
            public string LogFile;
            public string LogFunc;
            public string LogLine;
            public string Separator;
            public string Ex;
            public void MakeLog(string time, string level, string info, string file, string func, string line, string sep = "    ", string ex = "")
            {
                LogTime = time;
                LogLevel = level;
                LogInfo = info;
                LogFile = file;
                LogFunc = func;
                LogLine = line;
                Separator = sep;
                Ex = ex;
            }
            public string GetString()
            {
                if (Separator == string.Empty)
                    Separator = "    ";
                return LogTime + Separator + LogLevel + Separator + LogInfo + Separator + LogFile + Separator + LogFunc + Separator + LogLine + Separator + Ex;
            }
        }
        #endregion

        /// <summary>
        /// 创建使用默认 2000 条有界缓冲区的日志显示控件。
        /// </summary>
        public LogHelper() : this(new BoundedLogUiBuffer(2000))
        {
        }

        /// <summary>
        /// 创建使用指定界面缓冲区的日志显示控件。
        /// </summary>
        /// <param name="logUiBuffer">可替换的日志界面缓冲区。</param>
        internal LogHelper(ILogUiBuffer logUiBuffer)
        {
            _logUiBuffer = logUiBuffer ?? throw new ArgumentNullException(nameof(logUiBuffer));
            InitializeComponent();
            LogAddEvent += LogHelper_LogAddEvent;
            Disposed += LogHelper_Disposed;
        }

        /// <summary>
        /// 控件释放时解除静态日志事件并清空待显示记录，避免旧窗口实例被长期持有。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void LogHelper_Disposed(object sender, EventArgs e)
        {
            LogAddEvent -= LogHelper_LogAddEvent;
            Disposed -= LogHelper_Disposed;
            _logUiBuffer.Clear();
        }

        public void AdjustListBoxWidth(ListBox listBox)
        {
            if (listBox.Items.Count == 0) return;

            // 获取 Graphics 对象以测量字符串宽度
            using (Graphics g = listBox.CreateGraphics())
            {
                int maxWidth = 0;

                // 遍历寻找最长字符串
                foreach (var item in listBox.Items)
                {
                    string text = item.ToString();
                    // 测量字符串渲染后的尺寸
                    SizeF size = g.MeasureString(text, listBox.Font);
                    if (size.Width > maxWidth)
                    {
                        maxWidth = (int)size.Width;
                    }
                }

                // 设置水平滚动条宽度
                listBox.HorizontalExtent = maxWidth;
            }
        }

        /// <summary>
        /// 在 UI 线程中刷新一批待显示日志；非 UI 调用和隐藏窗口不会执行控件更新。
        /// </summary>
        public void FlushPendingLogs()
        {
            if (IsDisposed || Disposing || !IsHandleCreated || !Visible || InvokeRequired)
                return;

            FlushPendingLogsInternal();
        }

        /// <summary>
        /// 处理日志刷新定时器节拍，每个节拍最多消费一个固定批次。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void LogHelper_LogRefreshTimerTick(object sender, EventArgs e)
        {
            FlushPendingLogs();
        }

        /// <summary>
        /// 批量更新全部日志列表和各等级列表，并在一次刷新后统一裁剪与滚动。
        /// </summary>
        private void FlushPendingLogsInternal()
        {
            IReadOnlyList<LogUiEntry> batch = _logUiBuffer.DequeueBatch(UiRefreshBatchSize);
            if (batch.Count == 0)
                return;

            ListBox[] listBoxes = GetAllLogListBoxes();
            foreach (ListBox listBox in listBoxes)
                listBox.BeginUpdate();

            try
            {
                foreach (LogUiEntry entry in batch)
                {
                    listBoxAll.Items.Add(entry.Info);
                    ListBox levelListBox = GetLevelListBox(entry.Level);
                    if (levelListBox != null)
                        levelListBox.Items.Add(entry.Info);
                }

                TrimOldestItems(listBoxAll, AllLogDisplayLimit);
                foreach (ListBox listBox in listBoxes)
                {
                    if (!ReferenceEquals(listBox, listBoxAll))
                        TrimOldestItems(listBox, LevelLogDisplayLimit);
                }
            }
            finally
            {
                foreach (ListBox listBox in listBoxes)
                    listBox.EndUpdate();
            }

            ScrollSelectedLogListToEnd();
        }

        /// <summary>
        /// 将后台日志事件快速写入界面缓冲区，不直接投递 UI 消息。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="levelAndInfo">待显示日志。</param>
        private void LogHelper_LogAddEvent(object sender, LevelAndInfo levelAndInfo)
        {
            _logUiBuffer.Enqueue(new LogUiEntry(levelAndInfo.Level, levelAndInfo.Info));
        }

        /// <summary>
        /// 获取日志控件中的全部列表，供批量暂停和恢复绘制。
        /// </summary>
        /// <returns>全部日志列表控件。</returns>
        private ListBox[] GetAllLogListBoxes()
        {
            return new[] { listBoxAll, listBoxInfo, listBoxDebug, listBoxWarn, listBoxExpection, listBoxFatal };
        }

        /// <summary>
        /// 根据日志等级获取对应的分类列表。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <returns>对应分类列表；未知等级返回 null。</returns>
        private ListBox GetLevelListBox(MsgLevel level)
        {
            switch (level)
            {
                case MsgLevel.Debug:
                    return listBoxDebug;
                case MsgLevel.Info:
                    return listBoxInfo;
                case MsgLevel.Warn:
                    return listBoxWarn;
                case MsgLevel.Exception:
                    return listBoxExpection;
                case MsgLevel.Fatal:
                    return listBoxFatal;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 删除列表中超出显示上限的最旧日志。
        /// </summary>
        /// <param name="listBox">待裁剪列表。</param>
        /// <param name="displayLimit">允许保留的最大行数。</param>
        private static void TrimOldestItems(ListBox listBox, int displayLimit)
        {
            int removeCount = listBox.Items.Count - displayLimit;
            for (int index = 0; index < removeCount; index++)
                listBox.Items.RemoveAt(0);
        }

        /// <summary>
        /// 每批刷新结束后仅将当前可见日志列表滚动到底部一次。
        /// </summary>
        private void ScrollSelectedLogListToEnd()
        {
            if (tabControl1.SelectedTab == null || tabControl1.SelectedTab.Controls.Count == 0)
                return;

            ListBox listBox = tabControl1.SelectedTab.Controls[0] as ListBox;
            if (listBox == null || listBox.Items.Count == 0)
                return;

            int visibleItemCount = Math.Max(1, listBox.ClientSize.Height / Math.Max(1, listBox.ItemHeight));
            listBox.TopIndex = Math.Max(0, listBox.Items.Count - visibleItemCount);
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            else
            {
                ListBox seelctBox = sender as ListBox;
                string logText = seelctBox.Items[e.Index].ToString();
                e.DrawBackground();
                Brush mybsh = Brushes.Black;
                if (logText.Contains("Info"))
                {
                    mybsh = Brushes.DodgerBlue;
                }
                else if (logText.Contains("Debug"))
                {
                    mybsh = Brushes.Green;
                }
                else if (logText.Contains("Warn"))
                {
                    mybsh = Brushes.Orange;
                }
                else if (logText.Contains("Exception"))
                {
                    mybsh = Brushes.Red;
                }
                else if (logText.Contains("Fatal"))
                {
                    mybsh = Brushes.Red;
                }
                else
                    mybsh = Brushes.DarkGray;

                // 判断项是否被选中
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                // 配置不同状态下背景色与前景色
                if (isSelected)
                {
                    e.Graphics.FillRectangle(Brushes.MediumTurquoise, e.Bounds); // 选中时背景
                    mybsh = Brushes.White; // 选中时文字颜色
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.White, e.Bounds); // 默认背景
                }

                e.DrawFocusRectangle();
                e.Graphics.DrawString(seelctBox.Items[e.Index].ToString(), e.Font, mybsh, e.Bounds, StringFormat.GenericDefault);
            }
        }

        /// <summary>
        /// 判断指定日志等级当前是否会被记录，统一包含系统设置和仅记录异常菜单过滤。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <returns>当前等级会被记录返回 true，否则返回 false。</returns>
        public static bool CanRecord(MsgLevel level)
        {
            if (!LogLevelSettings.IsEnabled(level))
                return false;

            if (OnlyLogException && (level == MsgLevel.Info || level == MsgLevel.Debug))
                return false;

            return true;
        }

        /// <summary>
        /// 当前尚未写入文件的日志任务数量，供性能诊断观察日志积压。
        /// </summary>
        public static int PendingLogCount => _logQueue.Count;

        /// <summary>
        /// 获取因诊断队列保护而放弃的Debug性能诊断总数。
        /// </summary>
        public static long DroppedDiagnosticLogCount => Interlocked.Read(ref _droppedDiagnosticLogCount);

        /// <summary>
        /// 尝试写入一条有界Debug性能诊断；队列拥堵时只丢弃诊断，不阻塞检测流程。
        /// </summary>
        /// <param name="logInfo">诊断正文。</param>
        /// <param name="isDisplay">是否进入日志显示缓冲区。</param>
        /// <param name="filePath">调用者文件。</param>
        /// <param name="memberName">调用者方法。</param>
        /// <param name="lineNumber">调用者行号。</param>
        /// <returns>成功加入待写队列时返回true。</returns>
        public static bool TryAddDiagnosticLog(
            string logInfo,
            bool isDisplay = false,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!CanRecord(MsgLevel.Debug))
                return false;

            if (_logQueue.Count >= DiagnosticPendingLogLimit)
            {
                Interlocked.Increment(ref _droppedDiagnosticLogCount);
                return false;
            }

            return EnqueueLog(MsgLevel.Debug, logInfo, isDisplay, filePath, memberName, lineNumber, true);
        }

        /// <summary>
        /// 记录一条日志并触发异步落地
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="logInfo">日志正文内容</param>
        /// <param name="isDisplay">是否在界面上显示（true: 刷新UI并写文件, false: 仅写文件）</param>
        /// <param name="filePath">调用者所在文件</param>
        /// <param name="memberName">调用者方法名</param>
        /// <param name="lineNumber">调用者行号</param>
        public static void AddLog(
            MsgLevel level,
            string logInfo,
            bool isDisplay = false,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0
            )
        {
            EnqueueLog(level, logInfo, isDisplay, filePath, memberName, lineNumber, false);
        }

        /// <summary>
        /// 创建日志任务并加入后台文件队列。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="logInfo">日志正文。</param>
        /// <param name="isDisplay">是否进入日志显示缓冲区。</param>
        /// <param name="filePath">调用者文件。</param>
        /// <param name="memberName">调用者方法。</param>
        /// <param name="lineNumber">调用者行号。</param>
        /// <param name="useNonBlockingAdd">是否使用非阻塞入队。</param>
        /// <returns>成功加入待写队列时返回true。</returns>
        private static bool EnqueueLog(
            MsgLevel level,
            string logInfo,
            bool isDisplay,
            string filePath,
            string memberName,
            int lineNumber,
            bool useNonBlockingAdd)
        {
            if (!CanRecord(level))
                return false;

            SingleLog singleLog = new SingleLog();
            singleLog.MakeLog(GetHighResTimestamp(), $"{level}", logInfo, filePath, memberName, $"{lineNumber}");

            Action writeAction = () =>
            {
                WriteLog(singleLog);
                if (isDisplay)
                {
                    string logToShow = singleLog.LogTime + singleLog.Separator + singleLog.LogLevel + singleLog.Separator + singleLog.LogInfo;
                    LevelAndInfo levelAndInfo = new LevelAndInfo
                    {
                        Level = level,
                        Info = logToShow
                    };
                    LogAddEvent?.Invoke(null, levelAndInfo);
                }
            };

            if (useNonBlockingAdd)
            {
                bool added = _logQueue.TryAdd(writeAction);
                if (!added)
                    Interlocked.Increment(ref _droppedDiagnosticLogCount);
                return added;
            }

            _logQueue.Add(writeAction);
            return true;
        }

        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static bool _isChecking = false;

        private static void CheckAndClearLogFolder()
        {
            if ((DateTime.Now - _lastCheckTime).TotalHours < 1 || _isChecking) return;
            _isChecking = true;
            _lastCheckTime = DateTime.Now;

            Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(LogDictory)) return;

                    long size = 0;
                    DirectoryInfo dir = new DirectoryInfo(LogDictory);
                    FileInfo[] files = dir.GetFiles("*.log");
                    foreach (var file in files)
                    {
                        size += file.Length;
                    }
                    
                    if (size > 20L * 1024 * 1024 * 1024)
                    {
                        Array.Sort(files, (a, b) => a.LastWriteTime.CompareTo(b.LastWriteTime));
                        
                        foreach (var file in files)
                        {
                            try
                            {
                                file.Delete();
                                size -= file.Length;
                                if (size < 15L * 1024 * 1024 * 1024) break; 
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    _isChecking = false;
                }
            });
        }

        private static void WriteLog(SingleLog singleLog)
        {
            CheckAndClearLogFolder();

            string msg = singleLog.GetString();
            if (!Directory.Exists(LogDictory))
            {
                Directory.CreateDirectory(LogDictory);
            }
            string runningLogFileName = LogDictory + DateTime.Now.ToString("yyyyMMdd") + ".log";
            StreamWriter mySW = new StreamWriter(runningLogFileName, true);
            mySW.WriteLine(msg);
            mySW.Close();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                _logUiBuffer.Clear();
                switch (tabControl1.SelectedIndex)
                {
                    case 0:
                        listBoxAll.Items.Clear();
                        break;
                    case 1:
                        listBoxInfo.Items.Clear();
                        break;
                    case 2:
                        listBoxDebug.Items.Clear();
                        break;
                    case 3:
                        listBoxWarn.Items.Clear();
                        break;
                    case 4:
                        listBoxExpection.Items.Clear();
                        break;
                    case 5:
                        listBoxFatal.Items.Clear();
                        break;
                    default:
                        break;
                }

            }
            catch
            {
            }

        }

        private void 打开日志目录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 使用 Process.Start 打开日志目录
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = LogDictory,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        /// <summary>
        /// 菜单项：仅记录异常日志
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 仅记录异常日志ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            仅记录异常日志ToolStripMenuItem.Checked = !仅记录异常日志ToolStripMenuItem.Checked;
            OnlyLogException = 仅记录异常日志ToolStripMenuItem.Checked ? true : false;
        }
    }
}
