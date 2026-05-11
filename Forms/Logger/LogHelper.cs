using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Logger
{
    public partial class LogHelper : UserControl
    {
        static readonly string LogDictory = Environment.CurrentDirectory + @"\Logs\";
        static event EventHandler<LevelAndInfo> LogAddEvent;
        private static readonly BlockingCollection<Action> _logQueue = new BlockingCollection<Action>();

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

        public LogHelper()
        {
            InitializeComponent();
            LogAddEvent += LogHelper_LogAddEvent;
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

        private void LogHelper_LogAddEvent(object sender, LevelAndInfo levelAndInfo)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    // 根据日志级别分发到不同列表控件
                    switch (levelAndInfo.Level)
                    {
                        case MsgLevel.Debug:
                            ControlListBox(levelAndInfo.Info, listBoxDebug);
                            break;
                        case MsgLevel.Info:
                            ControlListBox(levelAndInfo.Info, listBoxInfo);
                            break;
                        case MsgLevel.Warn:
                            ControlListBox(levelAndInfo.Info, listBoxWarn);
                            break;
                        case MsgLevel.Exception:
                            ControlListBox(levelAndInfo.Info, listBoxExpection);
                            break;
                        case MsgLevel.Fatal:
                            ControlListBox(levelAndInfo.Info, listBoxFatal);
                            break;
                        default:
                            break;
                    }
                    // 同步在全局列表显示
                    listBoxAll.Items.Add(levelAndInfo.Info);
                    listBoxAll.SelectedIndex = listBoxAll.Items.Count - 1;
                    listBoxAll.SelectedIndex = -1;    // 取消选中效果
                    // 限制最大UI行数，防止内存溢出
                    if (listBoxAll.Items.Count > 1000)
                    {
                        listBoxAll.Items.Clear();
                    }
                });
            }
            else
            {
                // 根据日志级别分发到不同列表控件
                switch (levelAndInfo.Level)
                {
                    case MsgLevel.Debug:
                        ControlListBox(levelAndInfo.Info, listBoxDebug);
                        break;
                    case MsgLevel.Info:
                        ControlListBox(levelAndInfo.Info, listBoxInfo);
                        break;
                    case MsgLevel.Warn:
                        ControlListBox(levelAndInfo.Info, listBoxWarn);
                        break;
                    case MsgLevel.Exception:
                        ControlListBox(levelAndInfo.Info, listBoxExpection);
                        break;
                    case MsgLevel.Fatal:
                        ControlListBox(levelAndInfo.Info, listBoxFatal);
                        break;
                    default:
                        break;
                }
                // 同步在全局列表显示
                listBoxAll.Items.Add(levelAndInfo.Info);
                listBoxAll.SelectedIndex = listBoxAll.Items.Count - 1;
                listBoxAll.SelectedIndex = -1;    // 取消选中效果
                // 限制最大UI行数，防止内存溢出
                if (listBoxAll.Items.Count > 1000)
                {
                    listBoxAll.Items.Clear();
                }
            }
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
            if (OnlyLogException && (level == MsgLevel.Info || level == MsgLevel.Debug))
                return; // 过滤非异常日志
            SingleLog singleLog = new SingleLog();
            singleLog.MakeLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), $"{level}", logInfo, filePath, memberName, $"{lineNumber}");
            
            // 封装写文件和事件触发逻辑为匿名委托压入队列
            _logQueue.Add(() =>
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
            });
        }

        private void ControlListBox(string logToShow, ListBox myListBox)
        {
            myListBox.Items.Add(logToShow);
            myListBox.SelectedIndex = myListBox.Items.Count - 1;
            if (myListBox.Items.Count > 300)
            {
                myListBox.Items.Clear();
            }
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
