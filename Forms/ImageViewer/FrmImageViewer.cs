using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using TDJS_Vision.Forms.Workspace;
using WeifenLuo.WinFormsUI.Docking;

namespace TDJS_Vision.Forms.ImageViewer
{
    public partial class FrmImageViewer : DockContent
    {
        /// <summary>
        /// 所有的图像窗口
        /// </summary>
        private static List<FrmSingleImage> _frmSingleImages = new List<FrmSingleImage>();

        /// <summary>
        /// 旧版 3D 图像窗口列表，保留用于反序列化兼容。
        /// </summary>
        private static readonly List<FrmSingle3DImage> _frmSingle3DImages = new List<FrmSingle3DImage>();

        /// <summary>
        /// 当前图像窗口管理器实例，保留给旧版独立窗口接口兼容使用。
        /// </summary>
        private static FrmImageViewer _activeViewer;

        /// <summary>布局仅在宿主挂接完成后恢复一次，避免静态构造期间浮窗触发跨屏 DPI 缩放。</summary>
        private bool layoutInitialized;

        public static List<FrmSingleImage> FrmSingleImages { get => _frmSingleImages; }

        /// <summary>
        /// 旧版 3D 图像窗口列表。
        /// </summary>
        public static List<FrmSingle3DImage> FrmSingle3DImages { get => _frmSingle3DImages; }

        /// <summary>
        /// 窗口布局配置
        /// </summary>
        private readonly string DockPanelConfig = Application.StartupPath + "\\DockPanelImageWindows.config";

        /// <summary>
        /// 反序列化DockContent代理
        /// </summary>
        private DeserializeDockContent DeserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);

        /// <summary>
        /// 当前显示的图像窗口数量
        /// </summary>
        public static int CurWindowsNum = 0;

        /// <summary>
        /// 当前布局中反序列化到的旧版 3D 图像窗口数量。
        /// </summary>
        public static int Cur3DWindowsNum = 0;

        /// <summary>
        /// 图像窗口隐藏事件
        /// </summary>
        public event EventHandler HideChangedEvent;

        public FrmImageViewer()
        {
            InitializeComponent();
            _activeViewer = this;
            // 仅图像浮窗使用生命周期保护，避免合并释放时的迟到激活消息触发库内空引用。
            GreenTheme imageTheme = new GreenTheme();
            imageTheme.Extender.FloatWindowFactory = new WorkspaceFloatWindowFactory();
            this.dockPanel1.Theme = imageTheme;
            new DockPanelPermissionBinding(dockPanel1);
            InitWindows();
            CanvasSet.WindowNumChangeEvent += WindowNumChangeEvent;
            CanvasSet.SaveDockPanelEvent += SaveDockPanelEventHandler;
        }

        /// <summary>等待图像管理器挂入主界面后，再创建和恢复浮动窗口。</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (layoutInitialized) return;
            layoutInitialized = true;
            InitDockPanel();
        }

        private void WindowNumChangeEvent(object sender, int e)
        {
            SetWindowNum(e);
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
                    LoadDockPanelLayout();
                    CurWindowsNum = CountVisibleImageWindows();
                }
                else
                {
                    LoadDefaultDockPanel();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("图像布局恢复失败：{0}", ex);
                // LoadFromXml 在创建窗格期间失败可能留下挂起的布局和部分内容。
                foreach (IDockContent content in dockPanel1.Contents.Cast<IDockContent>().ToArray())
                    content.DockHandler.DockPanel = null;
                dockPanel1.ResumeLayout(true, true);
                LoadDefaultDockPanel();
            }
        }
        /// <summary>
        /// 默认窗口布局
        /// </summary>
        private void LoadDefaultDockPanel()
        {
            for (int i = 0; i < 1; i++)
            {
                _frmSingleImages[i].Show(dockPanel1, DockState.Document);
            }

            CurWindowsNum = 1;
        }

        // 处理主窗口触发的保存布局事件
        private void SaveDockPanelEventHandler(object sender, EventArgs e)
        {
            if (!layoutInitialized || IsDisposed) return;
            this.dockPanel1.SaveAsXml(DockPanelConfig);
        }

        /// <summary>
        /// 修复旧清理逻辑留下的窗格编号缺口，再从内存加载；不删除原生双位置记录。
        /// </summary>
        private void LoadDockPanelLayout()
        {
            XDocument document = XDocument.Load(DockPanelConfig);
            XElement panes = document.Root.Element("Panes");
            if (panes != null)
            {
                HashSet<string> floatingContentIds = new HashSet<string>(panes.Elements("Pane")
                    .Where(pane => (string)pane.Attribute("DockState") == "Float")
                    .Elements("Contents").Elements("Content").Select(content => (string)content.Attribute("RefID")));
                // 旧清理代码删除了 FloatPane 却保留 IsFloat=True；恢复到尚存的停靠位置。
                foreach (XElement content in document.Root.Element("Contents").Elements("Content"))
                {
                    if (string.Equals((string)content.Attribute("IsFloat"), "True", StringComparison.OrdinalIgnoreCase) &&
                        !floatingContentIds.Contains((string)content.Attribute("ID")))
                    {
                        content.SetAttributeValue("IsFloat", "False");
                        string contentId = (string)content.Attribute("ID");
                        if (!string.Equals((string)content.Attribute("IsHidden"), "True", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (XElement pane in panes.Elements("Pane"))
                            {
                                if ((string)pane.Attribute("ActiveContent") == "-1" && pane.Elements("Contents")
                                    .Elements("Content").Any(item => (string)item.Attribute("RefID") == contentId))
                                    pane.SetAttributeValue("ActiveContent", contentId);
                            }
                        }
                    }
                }
                Dictionary<string, string> indices = panes.Elements("Pane")
                    .Select((pane, index) => new { Old = (string)pane.Attribute("ID"), New = index.ToString() })
                    .ToDictionary(item => item.Old, item => item.New);
                foreach (XElement pane in panes.Elements("Pane"))
                    pane.SetAttributeValue("ID", indices[(string)pane.Attribute("ID")]);
                // RefID/PrevPane 在嵌套窗格中引用全局窗格，内容 RefID 引用图像，不能混改。
                foreach (XElement nested in document.Descendants("NestedPanes").Elements("Pane"))
                {
                    RemapPaneIndex(nested.Attribute("RefID"), indices);
                    RemapPaneIndex(nested.Attribute("PrevPane"), indices);
                }
                RemapPaneIndex(document.Root.Attribute("ActivePane"), indices);
                RemapPaneIndex(document.Root.Attribute("ActiveDocumentPane"), indices);
                XElement floats = document.Root.Element("FloatWindows");
                if (floats != null)
                {
                    int index = 0;
                    foreach (XElement floating in floats.Elements("FloatWindow"))
                        floating.SetAttributeValue("ID", index++);
                }
            }
            using (MemoryStream stream = new MemoryStream())
            {
                document.Save(stream);
                stream.Position = 0;
                restoredImageIndex = 0;
                dockPanel1.LoadFromXml(stream, DeserializeDockContent);
            }
            Dictionary<FloatWindow, Rectangle> savedBounds = new Dictionary<FloatWindow, Rectangle>();
            foreach (XElement element in document.Root.Element("FloatWindows").Elements("FloatWindow"))
            {
                XElement pane = element.Element("NestedPanes")?.Element("Pane");
                if (pane == null) continue;
                int paneIndex = (int)pane.Attribute("RefID");
                FloatWindow floating = dockPanel1.Panes[paneIndex].FloatWindow;
                if (floating == null) continue;
                Rectangle bounds = (Rectangle)TypeDescriptor.GetConverter(typeof(Rectangle))
                    .ConvertFromInvariantString((string)element.Attribute("Bounds"));
                savedBounds[floating] = GetVisibleFloatBounds(bounds);
            }
            // 父窗体首次布局和 WM_DPICHANGED 完成后重用物理矩形，避免每次重启都按 96/144 再缩小。
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                foreach (KeyValuePair<FloatWindow, Rectangle> saved in savedBounds)
                    if (!saved.Key.IsDisposed) saved.Key.Bounds = saved.Value;
            }));
        }

        /// <summary>保留有效显示器上的原位置；屏幕拔除或标题越界时收回到最近的可用工作区。</summary>
        private static Rectangle GetVisibleFloatBounds(Rectangle bounds)
        {
            Rectangle title = new Rectangle(bounds.X, bounds.Y, Math.Min(100, bounds.Width), SystemInformation.CaptionHeight);
            if (Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(title))) return bounds;
            Rectangle area = Screen.FromRectangle(bounds).WorkingArea;
            int width = Math.Min(bounds.Width, area.Width);
            int height = Math.Min(bounds.Height, area.Height);
            return new Rectangle(Math.Max(area.Left, Math.Min(bounds.X, area.Right - width)),
                Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - height)), width, height);
        }

        /// <summary>将仍存在的窗格引用映射到连续编号，负一表示没有活动或前置窗格。</summary>
        private static void RemapPaneIndex(XAttribute attribute, Dictionary<string, string> indices)
        {
            if (attribute != null && attribute.Value != "-1")
                attribute.Value = indices.TryGetValue(attribute.Value, out string index) ? index : "-1";
        }

        /// <summary>
        /// 统计当前没有隐藏的图像窗口数量，供画布设置窗体显示当前数量。
        /// </summary>
        /// <returns>当前显示的图像窗口数量。</returns>
        private static int CountVisibleImageWindows()
        {
            int count = 0;
            foreach (FrmSingleImage window in _frmSingleImages)
            {
                if (!window.IsDisposed && window.DockPanel != null && !window.IsHidden)
                    count++;
            }

            return count;
        }

        /// <summary>旧版布局按内容顺序恢复时的索引，与当前可见窗口数独立。</summary>
        private static int restoredImageIndex;
        /// <summary>
        /// 配置委托函数
        /// </summary>
        /// <param name="persistString"></param>
        /// <returns></returns>
        private static IDockContent GetContentFromPersistString(string persistString)
        {
            if (persistString == typeof(FrmSingleImage).ToString())
            {
                return restoredImageIndex < _frmSingleImages.Count ? _frmSingleImages[restoredImageIndex++] : null;
            }
            if (persistString == typeof(FrmSingle3DImage).ToString())
            {
                int index = Math.Min(Cur3DWindowsNum++, _frmSingle3DImages.Count - 1);
                return index >= 0 ? _frmSingle3DImages[index] : null;
            }
            return null;
        }

        /// <summary>
        /// 初始化所有的窗口列表
        /// </summary>
        private void InitWindows()
        {
            for (int i = 0; i < 16; i++)
            {
                FrmSingleImage imageWin = new FrmSingleImage(FrmSingleImage.GetWindowKey(i + 1));
                _frmSingleImages.Add(imageWin);
            }

            // 3D 图像显示节点已改为复用普通图像窗口显示深度预览图，这里不再预创建独立 3D 窗口。
        }

        /// <summary>
        /// 按窗口名称显示旧版 3D 图像窗口，当前仅作为历史接口保留。
        /// </summary>
        /// <param name="windowName">3D 图像窗口名称。</param>
        public static void Show3DWindow(string windowName)
        {
            FrmSingle3DImage win = Find3DWindow(windowName);
            if (win == null)
                return;

            FrmImageViewer viewer = _activeViewer;
            if (viewer == null || viewer.IsDisposed || viewer.dockPanel1 == null)
            {
                if (!win.Visible)
                    win.Show();
                return;
            }

            Action showAction = () =>
            {
                if (win.IsDisposed)
                    return;

                if (win.DockPanel != viewer.dockPanel1 || win.IsHidden)
                    win.Show(viewer.dockPanel1, DockState.Document);
                else if (!win.Visible)
                    win.Show();

                win.Activate();
            };

            if (viewer.InvokeRequired)
                viewer.BeginInvoke(showAction);
            else
                showAction();
        }

        /// <summary>
        /// 根据窗口名称查找旧版 3D 图像窗口。
        /// </summary>
        /// <param name="windowName">3D 图像窗口名称。</param>
        /// <returns>3D 图像窗口。</returns>
        private static FrmSingle3DImage Find3DWindow(string windowName)
        {
            string key = FrmSingle3DImage.NormalizeWindowKey(windowName);
            foreach (FrmSingle3DImage win in _frmSingle3DImages)
            {
                if (win.FormName == key)
                    return win;
            }

            return null;
        }

        /// <summary>
        /// 设置对应数量的画布窗口
        /// </summary>
        /// <param name="num">窗口数</param>
        public void SetWindowNum(int num)
        {
            // 用户主动重排时以本次布局为准，之后首次显示不再覆盖为旧文件。
            layoutInitialized = true;
            foreach (var win in _frmSingleImages)
            {
                win.Hide();
            }

            for (int i = 0; i < num; i++)
            {
                _frmSingleImages[i].Show(dockPanel1, DockState.Document);
            }

            CurWindowsNum = num;

            if (num == 2)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[1].Show(dockPanel1, DockState.Document);
                _frmSingleImages[1].DockHandler.DockPanel.DockRightPortion = 0.5;
            }

            if (num == 3)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.Document);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;

                _frmSingleImages[1].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[1].DockHandler.DockPanel.DockRightPortion = 0.5;
                _frmSingleImages[1].DockHandler.DockPanel.DockTopPortion = 0.5;

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.5;
                _frmSingleImages[2].DockHandler.DockPanel.DockBottomPortion = 0.5;

            }

            if (num == 4)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[0].DockHandler.DockPanel.DockTopPortion = 0.5;


                _frmSingleImages[2].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[2].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[2].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[1].Show(dockPanel1, DockState.DockRight);

                _frmSingleImages[3].Show(dockPanel1, DockState.Document);

                _frmSingleImages[1].DockTo(_frmSingleImages[3].DockHandler.Pane, DockStyle.Top, 0);

            }

            if (num == 5)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.6666666;

                _frmSingleImages[3].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[2].DockTo(_frmSingleImages[3].DockHandler.Pane, DockStyle.Left, 0);
                _frmSingleImages[4].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
            }

            if (num == 6)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.333333;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.333333;

                _frmSingleImages[3].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[4].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[5].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
            }

            if (num == 7)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[3].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[3].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[3].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[4].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[4].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[4].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[5].Show(dockPanel1, DockState.Document);

                _frmSingleImages[6].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[6].DockHandler.DockPanel.DockRightPortion = 0.25;
                _frmSingleImages[6].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[1].DockTo(_frmSingleImages[5].DockHandler.Pane, DockStyle.Top, 0);
                _frmSingleImages[3].DockTo(_frmSingleImages[4].DockHandler.Pane, DockStyle.Left, 0);

            }

            if (num == 8)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.25;

                _frmSingleImages[1].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[1].DockHandler.DockPanel.DockLeftPortion = 0.25;

                _frmSingleImages[2].Show(dockPanel1, DockState.Document);

                _frmSingleImages[3].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[3].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[4].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[4].DockHandler.DockPanel.DockLeftPortion = 0.25;
                _frmSingleImages[4].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[5].Show(dockPanel1, DockState.Document);

                _frmSingleImages[6].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[6].DockHandler.DockPanel.DockRightPortion = 0.25;
                _frmSingleImages[6].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[7].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[7].DockHandler.DockPanel.DockRightPortion = 0.25;
                _frmSingleImages[7].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[1].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Left, 0);
                _frmSingleImages[5].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[6].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);

            }

            if (num == 9)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.333333;
                _frmSingleImages[0].DockHandler.DockPanel.DockBottomPortion = 0.333333;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.333333;
                _frmSingleImages[2].DockHandler.DockPanel.DockBottomPortion = 0.333333;

                _frmSingleImages[3].Show(_frmSingleImages[0].Pane, DockAlignment.Bottom, 0.6666);

                _frmSingleImages[4].Show(_frmSingleImages[1].Pane, DockAlignment.Bottom, 0.6666);

                _frmSingleImages[5].Show(_frmSingleImages[2].Pane, DockAlignment.Bottom, 0.6666);

                _frmSingleImages[6].Show(_frmSingleImages[3].Pane, DockAlignment.Bottom, 0.5);

                _frmSingleImages[7].Show(_frmSingleImages[4].Pane, DockAlignment.Bottom, 0.5);

                _frmSingleImages[8].Show(_frmSingleImages[5].Pane, DockAlignment.Bottom, 0.5);

            }

            if (num == 10)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.2;

                _frmSingleImages[5].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[5].DockHandler.DockPanel.DockLeftPortion = 0.2;

                _frmSingleImages[2].Show(dockPanel1, DockState.Document);

                _frmSingleImages[4].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Right, 0);
                _frmSingleImages[3].DockTo(_frmSingleImages[4].DockHandler.Pane, DockStyle.Left, 0);
                _frmSingleImages[2].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Right, 0);
                _frmSingleImages[6].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[7].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[8].DockTo(_frmSingleImages[3].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[9].DockTo(_frmSingleImages[4].DockHandler.Pane, DockStyle.Bottom, 0);
            }

            if (num == 11)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[0].DockHandler.DockPanel.DockBottomPortion = 0.25;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[4].Show(_frmSingleImages[0].Pane, DockAlignment.Bottom, 0.6666);
                _frmSingleImages[5].Show(_frmSingleImages[1].Pane, DockAlignment.Bottom, 0.6666);
                _frmSingleImages[6].Show(_frmSingleImages[2].Pane, DockAlignment.Bottom, 0.6666);
                _frmSingleImages[7].Show(_frmSingleImages[3].Pane, DockAlignment.Bottom, 0.6666);

                _frmSingleImages[8].Show(_frmSingleImages[4].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[9].Show(_frmSingleImages[5].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[10].Show(_frmSingleImages[6].Pane, DockAlignment.Bottom, 0.5);

                _frmSingleImages[3].DockTo(_frmSingleImages[4].DockHandler.Pane, DockStyle.Left, 0);
                _frmSingleImages[7].DockTo(_frmSingleImages[8].DockHandler.Pane, DockStyle.Left, 0);
            }

            if (num == 12)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.25;

                _frmSingleImages[1].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[1].DockHandler.DockPanel.DockLeftPortion = 0.25;

                _frmSingleImages[2].Show(dockPanel1, DockState.Document);

                _frmSingleImages[3].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[3].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[1].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Left, 0);

                _frmSingleImages[4].Show(_frmSingleImages[0].Pane, DockAlignment.Bottom, 0.6666);
                _frmSingleImages[5].Show(_frmSingleImages[1].Pane, DockAlignment.Bottom, 0.6666);
                _frmSingleImages[6].Show(_frmSingleImages[2].Pane, DockAlignment.Bottom, 0.6666);
                _frmSingleImages[7].Show(_frmSingleImages[3].Pane, DockAlignment.Bottom, 0.6666);

                _frmSingleImages[8].Show(_frmSingleImages[4].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[9].Show(_frmSingleImages[5].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[10].Show(_frmSingleImages[6].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[11].Show(_frmSingleImages[7].Pane, DockAlignment.Bottom, 0.5);

            }

            if (num == 13)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[0].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[3].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[5].DockTo(_frmSingleImages[3].DockHandler.Pane, DockStyle.Fill, 0);
                _frmSingleImages[6].DockTo(_frmSingleImages[5].DockHandler.Pane, DockStyle.Right, 0);
                _frmSingleImages[7].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[8].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[9].DockTo(_frmSingleImages[5].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[10].DockTo(_frmSingleImages[6].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[11].DockTo(_frmSingleImages[7].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[12].DockTo(_frmSingleImages[8].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[3].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[4].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
            }

            if (num == 14)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[0].DockHandler.DockPanel.DockBottomPortion = 0.5;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[6].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[7].DockTo(_frmSingleImages[6].DockHandler.Pane, DockStyle.Right, 0);
                _frmSingleImages[3].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[8].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[9].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[10].DockTo(_frmSingleImages[6].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[11].DockTo(_frmSingleImages[7].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[12].DockTo(_frmSingleImages[8].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[13].DockTo(_frmSingleImages[9].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[4].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[5].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
            }

            if (num == 15)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.5;
                _frmSingleImages[0].DockHandler.DockPanel.DockBottomPortion = 0.25;

                _frmSingleImages[1].Show(dockPanel1, DockState.Document);

                _frmSingleImages[2].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[2].DockHandler.DockPanel.DockRightPortion = 0.25;


                _frmSingleImages[3].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[4].DockTo(_frmSingleImages[3].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[7].DockTo(_frmSingleImages[4].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[3].DockTo(_frmSingleImages[0].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[4].DockTo(_frmSingleImages[3].DockHandler.Pane, DockStyle.Right, 0);
                _frmSingleImages[8].DockTo(_frmSingleImages[7].DockHandler.Pane, DockStyle.Right, 0);

                _frmSingleImages[5].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[6].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[9].DockTo(_frmSingleImages[5].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[10].DockTo(_frmSingleImages[6].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[5].DockTo(_frmSingleImages[1].DockHandler.Pane, DockStyle.Bottom, 0);
                _frmSingleImages[6].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Bottom, 0);

                _frmSingleImages[11].Show(_frmSingleImages[7].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[12].Show(_frmSingleImages[8].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[13].Show(_frmSingleImages[9].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[14].Show(_frmSingleImages[10].Pane, DockAlignment.Bottom, 0.5);
            }

            if (num == 16)
            {
                _frmSingleImages[0].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[0].DockHandler.DockPanel.DockLeftPortion = 0.25;

                _frmSingleImages[1].Show(dockPanel1, DockState.DockLeft);
                _frmSingleImages[1].DockHandler.DockPanel.DockLeftPortion = 0.25;

                _frmSingleImages[2].Show(dockPanel1, DockState.Document);

                _frmSingleImages[3].Show(dockPanel1, DockState.DockRight);
                _frmSingleImages[3].DockHandler.DockPanel.DockRightPortion = 0.25;

                _frmSingleImages[1].DockTo(_frmSingleImages[2].DockHandler.Pane, DockStyle.Left, 0);

                _frmSingleImages[4].Show(_frmSingleImages[0].Pane, DockAlignment.Bottom, 0.75);
                _frmSingleImages[5].Show(_frmSingleImages[1].Pane, DockAlignment.Bottom, 0.75);
                _frmSingleImages[6].Show(_frmSingleImages[2].Pane, DockAlignment.Bottom, 0.75);
                _frmSingleImages[7].Show(_frmSingleImages[3].Pane, DockAlignment.Bottom, 0.75);

                _frmSingleImages[8].Show(_frmSingleImages[4].Pane, DockAlignment.Bottom, 0.65);
                _frmSingleImages[9].Show(_frmSingleImages[5].Pane, DockAlignment.Bottom, 0.65);
                _frmSingleImages[10].Show(_frmSingleImages[6].Pane, DockAlignment.Bottom, 0.65);
                _frmSingleImages[11].Show(_frmSingleImages[7].Pane, DockAlignment.Bottom, 0.65);

                _frmSingleImages[12].Show(_frmSingleImages[8].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[13].Show(_frmSingleImages[9].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[14].Show(_frmSingleImages[10].Pane, DockAlignment.Bottom, 0.5);
                _frmSingleImages[15].Show(_frmSingleImages[11].Pane, DockAlignment.Bottom, 0.5);
            }
        }

        private void FrmImageViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !TDJS_Vision.Forms.Login.UserPermissionContext.CanEditWorkspaceLayout)
            {
                e.Cancel = true;
                return;
            }
            e.Cancel = true;  // 取消关闭事件，防止窗口关闭
            this.Hide(); // 隐藏窗口
            HideChangedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
