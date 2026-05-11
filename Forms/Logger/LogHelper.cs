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
        static LogHelper() { Task.Factory.StartNew(() => { foreach (var action in _logQueue.GetConsumingEnumerable()) { try { action?.Invoke(); } catch { } } }, TaskCreationOptions.LongRunning); }
        private static bool OnlyLogException = false; // �Ƿ����¼�쳣��־

        #region ��־�ȼ�����־���ݽṹ��
        struct LevelAndInfo
        {
            public MsgLevel Level { get; set; }
            public string Info { get; set; }
        }
        #endregion

        #region ������־�ṹ��
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

            // ��ȡ Graphics �������ڲ����ַ������
            using (Graphics g = listBox.CreateGraphics())
            {
                int maxWidth = 0;

                // ����������ҵ����һ��
                foreach (var item in listBox.Items)
                {
                    string text = item.ToString();
                    // ʹ�� MeasureString �����ַ������
                    SizeF size = g.MeasureString(text, listBox.Font);
                    if (size.Width > maxWidth)
                    {
                        maxWidth = (int)size.Width;
                    }
                }

                // ���� HorizontalExtent �Ա���ʾˮƽ������
                listBox.HorizontalExtent = maxWidth;
            }
        }

        private void LogHelper_LogAddEvent(object sender, LevelAndInfo levelAndInfo)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    // ��ʾ���ض���־�ȼ���
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
                    // ͬʱ��ʾ��ȫ����־��
                    listBoxAll.Items.Add(levelAndInfo.Info);
                    listBoxAll.SelectedIndex = listBoxAll.Items.Count - 1;
                    listBoxAll.SelectedIndex = -1;    //�Ƿ�ȡ��ѡ����
                    // ��ʾ1000�����Զ����UI��־
                    if (listBoxAll.Items.Count > 1000)
                    {
                        listBoxAll.Items.Clear();
                    }
                });
            }
            else
            {
                // ��ʾ���ض���־�ȼ���
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
                // ͬʱ��ʾ��ȫ����־��
                listBoxAll.Items.Add(levelAndInfo.Info);
                listBoxAll.SelectedIndex = listBoxAll.Items.Count - 1;
                listBoxAll.SelectedIndex = -1;    //�Ƿ�ȡ��ѡ����
                // ��ʾ1000�����Զ����UI��־
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

                // �ж��Ƿ���ѡ����
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                // ���ñ�����ɫ
                if (isSelected)
                {
                    e.Graphics.FillRectangle(Brushes.MediumTurquoise, e.Bounds); // ѡ��ʱ������ɫ
                    mybsh = Brushes.White; // ѡ��ʱ������ɫ
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.White, e.Bounds); // Ĭ�ϱ���ɫ
                }

                e.DrawFocusRectangle();
                e.Graphics.DrawString(seelctBox.Items[e.Index].ToString(), e.Font, mybsh, e.Bounds, StringFormat.GenericDefault);
            }
        }

        /// <summary>
        /// ���һ����־
        /// </summary>
        /// <param name="level">��־�ȼ�</param>
        /// <param name="logInfo">��־����</param>
        /// <param name="isDisplay">��־д���ļ���ͬʱ�Ƿ���ʾ�ڴ��ڣ�true��Ҫ�������־�ؼ���false����д����־�ļ�</param>
        /// <param name="filePath">��ӡ����־�Ĵ����ļ���</param>
        /// <param name="memberName">��־���ĸ�������ӡ</param>
        /// <param name="lineNumber">��־�ڴ����ļ�����һ��</param>
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
                return; // �������¼�쳣��־���ҵ�ǰ��־�����쳣���򲻼�¼
            SingleLog singleLog = new SingleLog();
            singleLog.MakeLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), $"{level}", logInfo, filePath, memberName, $"{lineNumber}");
            // д�뱾���ļ�Log
            _logQueue.Add(() => {
                WriteLog(singleLog);
                if (isDisplay) {
                    string logToShow = singleLog.LogTime + singleLog.Separator + singleLog.LogLevel + singleLog.Separator + singleLog.LogInfo;
                    LevelAndInfo levelAndInfo = new LevelAndInfo { Level = level, Info = logToShow };
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

        private void ����־Ŀ¼ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // ʹ�� Process.Start �����ļ�����������־�ļ���
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = LogDictory,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        /// <summary>
        /// ����¼�쳣��־
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ����¼�쳣��־ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ����¼�쳣��־ToolStripMenuItem.Checked = !����¼�쳣��־ToolStripMenuItem.Checked;
            OnlyLogException = ����¼�쳣��־ToolStripMenuItem.Checked ? true : false;
        }
    }
}
