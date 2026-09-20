using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._3_Detection.TDAI;
using Point = System.Drawing.Point;

namespace TDJS_Vision.Forms.DispShowImage
{
    /// <summary>
    /// ShowImageControl - 图像显示控件（新版渲染引擎 + 兼容性适配）
    /// </summary>
    public partial class ShowImageControl : UserControl
    {
        private Bitmap _image;
        private readonly object _imageLock = new object();
        private readonly object _roiLock = new object();
        private float _scale = 1f;
        private PointF _offset = PointF.Empty;
        private bool _isPanning = false;
        private Point _lastMouse;

        private bool _isInteracting = false;
        private readonly Timer _interactionTimer;

        /// <summary>
        /// 控件自有图像和交互资源是否已经释放，保证重复Dispose不会重复回收。
        /// </summary>
        private int _ownedResourcesReleased;

        private PointF _currentImgPt = new PointF(-1, -1);
        private Color _currentPixelColor = Color.Black;
        private int _currentGrayValue = 0;
        private const float PIXEL_GRID_THRESHOLD = 15f;

        private readonly List<IRoiShape> _dynamicRois = new List<IRoiShape>();
        private readonly List<IRoiShape> _staticRois = new List<IRoiShape>();
        private readonly List<ToolStripItem> _builtInMenuItems = new List<ToolStripItem>();
        private bool _rectangleRoiDrawingEnabled;
        private bool _isDrawingRectangleRoi;
        private PointF _drawingStartImagePoint;
        private RoiRotatedRect _drawingRectangleRoi;

        public ShowImageControl()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.TabStop = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.Selectable, true);
            this.BackColor = Color.FromArgb(20, 20, 20);

            this.MouseWheel += (s, e) => {
                _isInteracting = true; _interactionTimer.Stop(); _interactionTimer.Start();
                float delta = e.Delta > 0 ? 1.25f : 0.8f;
                PointF imgPt = ScreenToImage(e.Location);
                _scale = Math.Max(0.001f, Math.Min(1000f, _scale * delta));
                _offset.X = e.X - imgPt.X * _scale;
                _offset.Y = e.Y - imgPt.Y * _scale;
                Invalidate();
            };

            _interactionTimer = new Timer { Interval = 250 };
            _interactionTimer.Tick += (s, e) => { if (_isInteracting && !_isPanning) { _isInteracting = false; _interactionTimer.Stop(); Invalidate(); } };

            InitContextMenu();
        }

        private void InitContextMenu()
        {
            var menu = new ContextMenuStrip();
            _builtInMenuItems.Add(menu.Items.Add("自适应显示 (Fit)", null, (s, e) => ShowFit()));
            _builtInMenuItems.Add(menu.Items.Add("清空显示", null, (s, e) => ClearDisplay()));
            _builtInMenuItems.Add(menu.Items.Add("-"));
            _builtInMenuItems.Add(menu.Items.Add("保存效果图", null, (s, e) => SaveResultImage()));
            _builtInMenuItems.Add(menu.Items.Add("保存原图", null, (s, e) => SaveOriginalImage()));
            this.ContextMenuStrip = menu;
        }

        /// <summary>开启或禁用内置的右键菜单项</summary>
        public void SetBuiltInMenuEnabled(bool enabled)
        {
            foreach (var item in _builtInMenuItems) item.Enabled = enabled;
        }

        /// <summary>显示或隐藏内置的右键菜单项</summary>
        public void SetBuiltInMenuVisible(bool visible)
        {
            foreach (var item in _builtInMenuItems) item.Visible = visible;
        }

        /// <summary>向右键菜单注册自定义菜单项</summary>
        public ToolStripItem RegisterContextMenuItem(string text, EventHandler onClick, Image icon = null)
        {
            if (this.ContextMenuStrip == null) return null;
            return this.ContextMenuStrip.Items.Add(text, icon, onClick);
        }

        /// <summary>移除指定的菜单项对象</summary>
        public void RemoveContextMenuItem(ToolStripItem item)
        {
            if (this.ContextMenuStrip == null || item == null) return;
            this.ContextMenuStrip.Items.Remove(item);
            item.Dispose();
        }

        /// <summary>根据文本移除菜单项</summary>
        public void RemoveContextMenuItem(string text)
        {
            if (this.ContextMenuStrip == null) return;
            for (int i = this.ContextMenuStrip.Items.Count - 1; i >= 0; i--)
            {
                if (this.ContextMenuStrip.Items[i].Text == text)
                {
                    this.ContextMenuStrip.Items[i].Dispose();
                    this.ContextMenuStrip.Items.RemoveAt(i);
                }
            }
        }

        /// <summary>注册菜单分隔线</summary>
        public void RegisterContextMenuSeparator()
        {
            if (this.ContextMenuStrip == null) return;
            this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        }

        [Category("Behavior")]
        [Description("是否在鼠标移动时采集并显示当前像素的颜色信息（建议循环检测时关闭）")]
        public bool ShowPixelInfo { get; set; } = false;

        #region Designer Compatibility Properties
        [Category("Appearance")]
        public Color BackgroundColorCustom { get; set; } = Color.Black;
        [Category("Appearance")]
        public Image BackgroundImageCustom { get; set; }
        public enum BackgroundImageLayoutMode { Fill, Center, Stretch }
        [Category("Appearance")]
        public BackgroundImageLayoutMode BackgroundLayoutMode { get; set; } = BackgroundImageLayoutMode.Fill;
        [Category("Appearance")]
        public float BackgroundTransparency { get; set; } = 1f;
        [Category("Appearance")]
        public Color RoiColor { get; set; } = Color.Lime;
        [Category("Appearance")]
        public bool ShowCheckerBackground { get; set; } = false;
        [Category("Appearance")]
        public Color StaticShapeColor { get; set; } = Color.Red;
        #endregion

        #region API - 核心
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Image
        {
            get { lock (_imageLock) return _image; }
            set
            {
                if (value == null)
                {
                    ImageBitmap = null;
                    return;
                }

                ImageBitmap = value as Bitmap ?? new Bitmap(value);
            }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Bitmap ImageBitmap
        {
            get { lock (_imageLock) return _image; }
            set 
            {
                Bitmap oldImage = null;
                lock (_imageLock)
                {
                    if (_image != value) 
                    {
                        oldImage = _image;
                        _image = value;
                    }
                }

                DisposeOldImageAsync(oldImage);
                SafeInvalidate();
            }
        }

        /// <summary>
        /// 后台释放上一帧大图资源，避免UI线程在高频刷新时同步释放35MB位图造成卡顿。
        /// </summary>
        /// <param name="oldImage">上一帧位图。</param>
        private static void DisposeOldImageAsync(Bitmap oldImage)
        {
            if (oldImage == null)
                return;

            Task.Run(() =>
            {
                string oldImageInfo = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug)
                    ? PerformanceSpikeDiagnostics.GetBitmapText(oldImage)
                    : string.Empty;
                Stopwatch disposeStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                try { oldImage?.Dispose(); } catch { }
                long disposeMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(disposeStopwatch);
                if (disposeStopwatch != null)
                {
                    PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                        MsgLevel.Debug,
                        PerformanceSpikeDiagnostics.CommonSlowMs,
                        () => $"【慢诊断-旧图释放】图像={oldImageInfo}；释放耗时={disposeMs}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                        true,
                        disposeMs);
                }
            });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            lock (_imageLock)
            {
                if (_image != null)
                    ShowFit();
            }
        }

        public void ShowFit()
        {
            SafeInvoke(() =>
            {
                lock (_imageLock)
                {
                    if (_image == null) return;
                    _scale = Math.Min((float)Width / _image.Width, (float)Height / _image.Height);
                    _offset = new PointF((Width - _image.Width * _scale) / 2f, (Height - _image.Height * _scale) / 2f);
                }
                Invalidate();
            });
        }

        public void ClearAll()
        {
            lock (_roiLock)
            {
                ClearRoiShapes(_dynamicRois);
                ClearRoiShapes(_staticRois);
                _textElements.Clear();
            }
            SafeInvalidate();
        }
        public void ClearDisplay() 
        { 
            SafeInvoke(() =>
            {
                lock (_imageLock)
                {
                    _image?.Dispose(); 
                    _image = null; 
                }
                ClearAll(); 
                _scale = 1f; 
                _offset = PointF.Empty; 
                Invalidate();
            });
        }

        public PointF ImageToScreen(PointF p) => new PointF(p.X * _scale + _offset.X, p.Y * _scale + _offset.Y);
        public PointF ScreenToImage(Point p) => new PointF((p.X - _offset.X) / _scale, (p.Y - _offset.Y) / _scale);
        
        public event Action<PointF> MouseImageClick;
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
        }
        #endregion

        #region API - 兼容性适配 (Compatibility Layer)

        /// <summary>兼容旧版名称</summary>
        public void ResetView() => ShowFit();
        
        /// <summary>兼容旧版名称</summary>
        public void ClearAllRoi() => ClearAll();
        
        /// <summary>兼容旧版名称</summary>
        public void ClearStaticShapes() { lock (_roiLock) ClearRoiShapes(_staticRois); SafeInvalidate(); }

        /// <summary>兼容旧版名称</summary>
        public void ClearRois() { lock (_roiLock) ClearRoiShapes(_dynamicRois); SafeInvalidate(); }

        public void ClearDynamicRoi() { lock (_roiLock) ClearRoiShapes(_dynamicRois); SafeInvalidate(); }

        public void ClearStaticRoi() { lock (_roiLock) ClearRoiShapes(_staticRois); SafeInvalidate(); }

        public int DynamicRoiCount { get { lock (_roiLock) return _dynamicRois.Count; } }

        /// <summary>
        /// 获取或设置是否允许在空白图像区域拖拽绘制矩形 ROI。
        /// </summary>
        [Category("Behavior")]
        [Description("是否允许在空白图像区域拖拽绘制矩形ROI")]
        public bool EnableRectangleRoiDrawing
        {
            get { return _rectangleRoiDrawingEnabled; }
            set { _rectangleRoiDrawingEnabled = value; }
        }

        public IRoiShape GetDynamicRoi(int index)
        {
            lock (_roiLock)
            {
                return index >= 0 && index < _dynamicRois.Count ? _dynamicRois[index] : null;
            }
        }

        /// <summary>
        /// 删除当前选中的动态 ROI。
        /// </summary>
        /// <returns>成功删除时返回 true。</returns>
        public bool DeleteSelectedDynamicRoi()
        {
            lock (_roiLock)
            {
                IRoiShape selected = _dynamicRois.LastOrDefault(roi => roi.IsSelected);
                if (selected == null)
                    return false;

                _dynamicRois.Remove(selected);
                DisposeRoiShape(selected);
                if (ReferenceEquals(_activeRoi, selected))
                    _activeRoi = null;
                if (ReferenceEquals(_drawingRectangleRoi, selected))
                    _drawingRectangleRoi = null;
            }

            SafeInvalidate();
            return true;
        }
         
        /// <summary>安全设置图像（线程安全）</summary>
        public void SetImage(Bitmap bmp) => SafeInvoke(() =>
        {
            bool needShowFit = ImageBitmap == null && bmp != null;
            ImageBitmap = bmp;
            if (needShowFit)
                ShowFit();
        });

        public void SetImage(Bitmap bmp, AlgorithmResult displayResult)
        {
            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            Stopwatch queueStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            SafeInvoke(() =>
            {
                long uiQueueWait = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(queueStopwatch);
                Stopwatch stopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                bool needShowFit = ImageBitmap == null && bmp != null;
                ImageBitmap = bmp;
                long afterSetBitmap = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                if (needShowFit)
                    ShowFit();
                long afterShowFit = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);

                StaticRoiBuildDiagnostics roiDiagnostics;
                int roiCount = ApplyDisplayResultCore(displayResult, out roiDiagnostics);
                long afterApplyDisplayResult = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                Invalidate();
                long afterInvalidate = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                if (diagnosticEnabled)
                {
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【性能诊断-图像显示】ShowImageControl.SetImage 图像={GetBitmapDiagnosticText(bmp)}；需要自适应={needShowFit}；UI排队等待={uiQueueWait}ms；静态ROI={roiCount}；设置Bitmap={afterSetBitmap}ms；ShowFit={afterShowFit - afterSetBitmap}ms；构建ROI={afterApplyDisplayResult - afterShowFit}ms；{roiDiagnostics.ToLogText()}；Invalidate={afterInvalidate - afterApplyDisplayResult}ms；总耗时={afterInvalidate}ms。",
                        true);
                    PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                        MsgLevel.Debug,
                        PerformanceSpikeDiagnostics.CommonSlowMs,
                        () => $"【慢诊断-显示控件SetImage】图像={PerformanceSpikeDiagnostics.GetBitmapText(bmp)}；显示结果={PerformanceSpikeDiagnostics.GetAlgorithmResultText(displayResult)}；需要自适应={needShowFit}；UI排队等待={uiQueueWait}ms；静态ROI={roiCount}；设置Bitmap={afterSetBitmap}ms；ShowFit={afterShowFit - afterSetBitmap}ms；构建ROI={afterApplyDisplayResult - afterShowFit}ms；{roiDiagnostics.ToLogText()}；Invalidate={afterInvalidate - afterApplyDisplayResult}ms；总耗时={afterInvalidate}ms；控件尺寸={Width}x{Height}；可见={Visible}；句柄已创建={IsHandleCreated}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                        true,
                        uiQueueWait,
                        afterInvalidate,
                        afterSetBitmap,
                        afterShowFit - afterSetBitmap,
                        afterApplyDisplayResult - afterShowFit,
                        afterInvalidate - afterApplyDisplayResult);
                }
            });
        }

        /// <summary>
        /// 仅在当前UI线程和有效句柄上接管图像，供有明确Bitmap所有权的调用方使用。
        /// </summary>
        /// <param name="bmp">准备移交给显示控件的Bitmap。</param>
        /// <param name="displayResult">与Bitmap同帧的叠加结果。</param>
        /// <returns>控件已经接管Bitmap返回 true；调用方仍需释放返回 false。</returns>
        public bool TrySetImage(Bitmap bmp, AlgorithmResult displayResult)
        {
            if (IsDisposed || !IsHandleCreated || InvokeRequired)
                return false;

            SetImage(bmp, displayResult);
            return true;
        }

        public void SetDisplayResult(AlgorithmResult displayResult)
        {
            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            Stopwatch queueStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            SafeInvoke(() =>
            {
                long uiQueueWait = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(queueStopwatch);
                Stopwatch stopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                StaticRoiBuildDiagnostics roiDiagnostics;
                int roiCount = ApplyDisplayResultCore(displayResult, out roiDiagnostics);
                long afterApplyDisplayResult = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                Invalidate();
                long afterInvalidate = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                if (diagnosticEnabled)
                {
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【性能诊断-图像显示】ShowImageControl.SetDisplayResult UI排队等待={uiQueueWait}ms；静态ROI={roiCount}；构建ROI={afterApplyDisplayResult}ms；{roiDiagnostics.ToLogText()}；Invalidate={afterInvalidate - afterApplyDisplayResult}ms；总耗时={afterInvalidate}ms。",
                        true);
                    PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                        MsgLevel.Debug,
                        PerformanceSpikeDiagnostics.CommonSlowMs,
                        () => $"【慢诊断-显示控件SetDisplayResult】显示结果={PerformanceSpikeDiagnostics.GetAlgorithmResultText(displayResult)}；UI排队等待={uiQueueWait}ms；静态ROI={roiCount}；构建ROI={afterApplyDisplayResult}ms；{roiDiagnostics.ToLogText()}；Invalidate={afterInvalidate - afterApplyDisplayResult}ms；总耗时={afterInvalidate}ms；控件尺寸={Width}x{Height}；可见={Visible}；句柄已创建={IsHandleCreated}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                        true,
                        uiQueueWait,
                        afterInvalidate,
                        afterApplyDisplayResult,
                        afterInvalidate - afterApplyDisplayResult);
                }
            });
        }

        /// <summary>
        /// 应用显示结果并返回静态 ROI 构建诊断信息。
        /// </summary>
        /// <param name="displayResult">待显示的算法结果。</param>
        /// <param name="diagnostics">静态 ROI 构建诊断信息。</param>
        /// <returns>构建出的静态 ROI 数量。</returns>
        private int ApplyDisplayResultCore(AlgorithmResult displayResult, out StaticRoiBuildDiagnostics diagnostics)
        {
            lock (_roiLock)
            {
                ClearRoiShapes(_staticRois);
                int imageWidth = _image == null ? 0 : _image.Width;
                int imageHeight = _image == null ? 0 : _image.Height;
                List<IRoiShape> rois = BuildStaticRois(displayResult, imageWidth, imageHeight, out diagnostics);
                _staticRois.AddRange(rois);
                return rois.Count;
            }
        }

        /// <summary>
        /// 生成图像显示控件性能诊断所需的Bitmap尺寸文本。
        /// </summary>
        /// <param name="bitmap">待显示位图。</param>
        /// <returns>位图诊断文本。</returns>
        private static string GetBitmapDiagnosticText(Bitmap bitmap)
        {
            if (bitmap == null)
                return "空";

            double megaBytes = bitmap.Width * bitmap.Height * Math.Max(1, Image.GetPixelFormatSize(bitmap.PixelFormat) / 8D) / 1024D / 1024D;
            return $"{bitmap.Width}x{bitmap.Height}，{bitmap.PixelFormat}，约{megaBytes:F2}MB";
        }

        /// <summary>
        /// 将显示结果直接绘制到位图上，供需要生成带标注结果图的运行节点复用现有 ROI 渲染逻辑。
        /// </summary>
        /// <param name="bitmap">需要写入标注的位图。</param>
        /// <param name="displayResult">待绘制的显示结果。</param>
        public static void DrawDisplayResultToBitmap(Bitmap bitmap, AlgorithmResult displayResult)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            Stopwatch stopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            StaticRoiBuildDiagnostics diagnostics;
            List<IRoiShape> rois = BuildStaticRois(displayResult, bitmap.Width, bitmap.Height, out diagnostics);
            long afterBuildRois = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            if (rois.Count == 0)
            {
                if (diagnosticEnabled)
                {
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【性能诊断-ROI绘制】DrawDisplayResultToBitmap 图像={GetBitmapDiagnosticText(bitmap)}；静态ROI=0；构建ROI={afterBuildRois}ms；{diagnostics.ToLogText()}；绘制Bitmap=0ms；总耗时={afterBuildRois}ms。",
                        true);
                }
                return;
            }

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (IRoiShape roi in rois)
                    roi.Draw(graphics, 1F, PointF.Empty);
            }
            long afterDraw = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            if (diagnosticEnabled)
            {
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【性能诊断-ROI绘制】DrawDisplayResultToBitmap 图像={GetBitmapDiagnosticText(bitmap)}；静态ROI={rois.Count}；构建ROI={afterBuildRois}ms；{diagnostics.ToLogText()}；绘制Bitmap={afterDraw - afterBuildRois}ms；总耗时={afterDraw}ms。",
                    true);
            }
        }

        /// <summary>
        /// 按 ShowImageControl 的静态 ROI 风格构建显示对象，界面叠加和结果图写入共用同一套绘制对象。
        /// </summary>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="imageWidth">图像宽度，用于文本角落定位。</param>
        /// <param name="imageHeight">图像高度，用于文本角落定位。</param>
        /// <returns>可直接绘制的静态 ROI 列表。</returns>
        private static List<IRoiShape> BuildStaticRois(AlgorithmResult displayResult, int imageWidth, int imageHeight)
        {
            StaticRoiBuildDiagnostics diagnostics;
            return BuildStaticRois(displayResult, imageWidth, imageHeight, out diagnostics);
        }

        /// <summary>
        /// 按 ShowImageControl 的静态 ROI 风格构建显示对象，并输出分阶段耗时。
        /// </summary>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="imageWidth">图像宽度，用于文本角落定位。</param>
        /// <param name="imageHeight">图像高度，用于文本角落定位。</param>
        /// <param name="diagnostics">构建过程诊断信息。</param>
        /// <returns>可直接绘制的静态 ROI 列表。</returns>
        private static List<IRoiShape> BuildStaticRois(AlgorithmResult displayResult, int imageWidth, int imageHeight, out StaticRoiBuildDiagnostics diagnostics)
        {
            Stopwatch stopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            diagnostics = new StaticRoiBuildDiagnostics();
            List<IRoiShape> rois = new List<IRoiShape>();
            if (displayResult == null)
            {
                diagnostics.TotalMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                return rois;
            }

            if (displayResult.Rects != null)
            {
                diagnostics.RectCount = displayResult.Rects.Count;
                foreach (var rect in displayResult.Rects)
                    AddRotatedRectRoi(rois, rect);
            }
            long afterRects = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            diagnostics.RectsMs = afterRects;

            if (displayResult.RectsNgMap != null)
            {
                foreach (var rectGroup in displayResult.RectsNgMap.Values)
                {
                    if (rectGroup == null)
                        continue;

                    diagnostics.NgRectCount += rectGroup.Count;
                    foreach (var rect in rectGroup)
                        AddRotatedRectRoi(rois, rect);
                }
            }
            long afterNgRects = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            diagnostics.NgRectsMs = afterNgRects - afterRects;

            if (displayResult.Lines != null)
            {
                diagnostics.LineCount = displayResult.Lines.Count;
                foreach (var line in displayResult.Lines)
                {
                    if (line == null)
                        continue;

                    rois.Add(new RoiLine(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y)
                    {
                        ShapeColor = line.Color,
                        StrokeWidth = line.LineWidth,
                        ShowCenterCross = line.ShowCenterCross,
                        IsStatic = true
                    });
                }
            }
            long afterLines = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            diagnostics.LinesMs = afterLines - afterNgRects;

            if (displayResult.Circles != null)
            {
                diagnostics.CircleCount = displayResult.Circles.Count;
                foreach (var circle in displayResult.Circles)
                {
                    if (circle == null)
                        continue;

                    rois.Add(new RoiCircle(circle.Center.X, circle.Center.Y, circle.Radius)
                    {
                        ShapeColor = circle.Color,
                        StrokeWidth = circle.LineWidth,
                        IsStatic = true
                    });
                }
            }

            if (displayResult.Arcs != null)
            {
                diagnostics.ArcCount = displayResult.Arcs.Count;
                foreach (var arc in displayResult.Arcs)
                {
                    if (arc == null)
                        continue;

                    rois.Add(new RoiArc(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.SweepAngle)
                    {
                        ShapeColor = arc.Color,
                        StrokeWidth = arc.LineWidth,
                        IsStatic = true
                    });
                }
            }

            if (displayResult.Ellipses != null)
            {
                diagnostics.EllipseCount = displayResult.Ellipses.Count;
                foreach (var ellipse in displayResult.Ellipses)
                {
                    if (ellipse == null)
                        continue;

                    rois.Add(new RoiEllipse(ellipse.Center.X, ellipse.Center.Y, ellipse.Width, ellipse.Height, ellipse.Angle)
                    {
                        ShapeColor = ellipse.Color,
                        StrokeWidth = ellipse.LineWidth,
                        IsStatic = true
                    });
                }
            }
            long afterMeasureShapes = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            diagnostics.MeasureShapesMs = afterMeasureShapes - afterLines;

            if (displayResult.Contours != null)
            {
                diagnostics.ContourCount = displayResult.Contours.Count;
                foreach (var contour in displayResult.Contours)
                {
                    if (contour == null)
                        continue;

                    rois.Add(new RoiContour(contour.Points, contour.Color)
                    {
                        StrokeWidth = contour.LineWidth
                    });
                }
            }
            long afterContours = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            diagnostics.ContoursMs = afterContours - afterMeasureShapes;

            if (displayResult.Texts != null && displayResult.Texts.Count > 0)
            {
                diagnostics.TextCount = displayResult.Texts.Count;
                foreach (ColorText text in displayResult.Texts.Where(t => t != null && t.UseImagePosition))
                {
                    rois.Add(new RoiTextBlock(
                        text.Title,
                        new List<(string text, Color color)> { (text.Text, text.Color) },
                        text.ImagePosition.X,
                        text.ImagePosition.Y,
                        Math.Max(1, text.FontSize)));
                }

                foreach (var group in displayResult.Texts.Where(t => t != null && !t.UseImagePosition).GroupBy(t => new { t.Position, t.FontSize, t.Margin, t.Title, t.CoordinateMode }))
                {
                    rois.Add(new RoiTextBlock(
                        group.Key.Title,
                        group.Select(t => (t.Text, t.Color)).ToList(),
                        10,
                        10,
                        Math.Max(1, group.Key.FontSize))
                    {
                        Position = group.Key.Position,
                        Margin = Math.Max(0, group.Key.Margin),
                        ImageWidth = imageWidth,
                        ImageHeight = imageHeight,
                        UseControlCoordinate = group.Key.CoordinateMode == DisplayTextCoordinateMode.Control
                    });
                }
            }

            long afterTexts = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            diagnostics.TextsMs = afterTexts - afterContours;
            diagnostics.RoiCount = rois.Count;
            diagnostics.TotalMs = afterTexts;
            return rois;
        }

        /// <summary>
        /// 静态 ROI 构建过程的数量和耗时诊断。
        /// </summary>
        private sealed class StaticRoiBuildDiagnostics
        {
            /// <summary>普通旋转矩形数量。</summary>
            public int RectCount { get; set; }
            /// <summary>NG分组旋转矩形数量。</summary>
            public int NgRectCount { get; set; }
            /// <summary>线段数量。</summary>
            public int LineCount { get; set; }
            /// <summary>圆数量。</summary>
            public int CircleCount { get; set; }
            /// <summary>圆弧数量。</summary>
            public int ArcCount { get; set; }
            /// <summary>椭圆数量。</summary>
            public int EllipseCount { get; set; }
            /// <summary>轮廓数量。</summary>
            public int ContourCount { get; set; }
            /// <summary>文本数量。</summary>
            public int TextCount { get; set; }
            /// <summary>最终生成的 ROI 数量。</summary>
            public int RoiCount { get; set; }
            /// <summary>普通矩形构建耗时。</summary>
            public long RectsMs { get; set; }
            /// <summary>NG矩形构建耗时。</summary>
            public long NgRectsMs { get; set; }
            /// <summary>线段构建耗时。</summary>
            public long LinesMs { get; set; }
            /// <summary>圆、圆弧、椭圆构建耗时。</summary>
            public long MeasureShapesMs { get; set; }
            /// <summary>轮廓构建耗时。</summary>
            public long ContoursMs { get; set; }
            /// <summary>文本构建耗时。</summary>
            public long TextsMs { get; set; }
            /// <summary>总构建耗时。</summary>
            public long TotalMs { get; set; }

            /// <summary>
            /// 生成便于日志输出的诊断文本。
            /// </summary>
            public string ToLogText()
            {
                return $"ROI明细=矩形{RectCount}/NG矩形{NgRectCount}/线{LineCount}/圆{CircleCount}/圆弧{ArcCount}/椭圆{EllipseCount}/轮廓{ContourCount}/文本{TextCount}/生成{RoiCount}；ROI分段=矩形{RectsMs}ms，NG矩形{NgRectsMs}ms，线{LinesMs}ms，圆弧椭圆{MeasureShapesMs}ms，轮廓{ContoursMs}ms，文本{TextsMs}ms，总构建{TotalMs}ms";
            }
        }

        /// <summary>
        /// 添加旋转矩形 ROI。
        /// </summary>
        /// <param name="rois">待追加的 ROI 列表。</param>
        /// <param name="rect">旋转矩形结果。</param>
        private static void AddRotatedRectRoi(List<IRoiShape> rois, ColorRotatedRect rect)
        {
            if (rect == null)
                return;

            var rotatedRect = rect.RotatedRect;
            rois.Add(new RoiRotatedRect(rotatedRect.center.x, rotatedRect.center.y, rotatedRect.size.width, rotatedRect.size.height, rotatedRect.angle)
            {
                ShapeColor = rect.Color,
                StrokeWidth = rect.LineWidth,
                IsStatic = true
            });
        }

        /// <summary>添加旋转矩形 ROI（参数：中心行, 中心列, 弧度, 半长1, 半长2, 线宽, 颜色）</summary>
        public IRoiShape AddRoiRotatedRect(float row, float column, float phiRad, float length1, float length2, float lineWidth = 1f, Color? color = null)
        {
            float phiDeg = (float)(phiRad * 180.0 / Math.PI);
            var r = new RoiRotatedRect(column, row, length1 * 2, length2 * 2, phiDeg) { ShapeColor = color ?? Color.Lime };
            lock (_roiLock) _dynamicRois.Add(r);
            SafeInvalidate();
            return r;
        }

        public IRoiShape AddRoiRotatedRect(float row, float column, float phiRad, float length1, float length2, float lineWidth, Color color, string label)
        {
            var roi = AddRoiRotatedRect(row, column, phiRad, length1, length2, lineWidth, color);
            roi.Label = label;
            return roi;
        }

        /// <summary>添加静态旋转矩形</summary>
        public void AddStaticRotatedRect(float row, float column, float phiRad, float length1, float length2, float lineWidth = 1f, Color? color = null)
        {
            float phiDeg = (float)(phiRad * 180.0 / Math.PI);
            lock (_roiLock) _staticRois.Add(new RoiRotatedRect(column, row, length1 * 2, length2 * 2, phiDeg) { ShapeColor = color ?? Color.Red, IsStatic = true });
            SafeInvalidate();
        }

        /// <summary>添加静态圆形</summary>
        public void AddStaticCircle(float row, float column, float radius, float lineWidth = 1f, Color? color = null)
        {
            lock (_roiLock) _staticRois.Add(new RoiCircle(column, row, radius) { ShapeColor = color ?? Color.Red, IsStatic = true });
            SafeInvalidate();
        }

        /// <summary>获取所有动态旋转矩形的信息（返回：Row, Column, Phi(弧度), Length1, Length2）</summary>
        public List<(float Row, float Column, float Phi, float Length1, float Length2)> GetAllRotatedRectInfos()
        {
            lock (_roiLock)
            {
                return _dynamicRois.OfType<RoiRotatedRect>().Select(r => 
                    (r.CY, r.CX, (float)(r.Phi * Math.PI / 180.0), r.W / 2, r.H / 2)).ToList();
            }
        }

        public List<RoiRotatedRect> GetAllDynamicRects() { lock (_roiLock) return _dynamicRois.OfType<RoiRotatedRect>().ToList(); }
        public List<RoiCaliper> GetAllDynamicCalipers() { lock (_roiLock) return _dynamicRois.OfType<RoiCaliper>().ToList(); }
        public List<RoiCircleCaliper> GetAllDynamicCircleCalipers() { lock (_roiLock) return _dynamicRois.OfType<RoiCircleCaliper>().ToList(); }
        public List<RoiCircle> GetAllDynamicCircles() { lock (_roiLock) return _dynamicRois.OfType<RoiCircle>().ToList(); }
        public List<RoiPolygon> GetAllDynamicPolygons() { lock (_roiLock) return _dynamicRois.OfType<RoiPolygon>().ToList(); }
        public List<RoiLine> GetAllDynamicLines() { lock (_roiLock) return _dynamicRois.OfType<RoiLine>().ToList(); }
        public List<RoiEllipseCaliper> GetAllDynamicEllipseCalipers() { lock (_roiLock) return _dynamicRois.OfType<RoiEllipseCaliper>().ToList(); }

        public string GetSelectedRoiInfo()
        {
            lock (_roiLock)
            {
                var r = _dynamicRois.FirstOrDefault(x => x.IsSelected);
                if (r == null) return "未选中";
                string info = $"Center:({r.GetCenter().X:F1},{r.GetCenter().Y:F1}) Type:{r.GetType().Name}";
                if (r is RoiCaliper cp) info += $" W:{cp.CaliperWidth:F1} H:{cp.CaliperHeight:F1} N:{cp.Count}";
                if (r is RoiCircleCaliper cc) info += $" R:{cc.Radius:F1} Angle:({cc.StartAngle:F0}~{cc.EndAngle:F0}) N:{cc.Count}";
                if (r is RoiEllipseCaliper ec) info += $" W:{ec.W:F1} H:{ec.H:F1} Phi:{ec.Phi:F1} N:{ec.Count}";
                return info;
            }
        }

        // OpenCV 风格便捷方法
        public void AddDynamicRect(float cx, float cy, float w, float h, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiRotatedRect(cx, cy, w, h, 0) { ShapeColor = c ?? Color.Lime, Label = l }); SafeInvalidate(); }
        public void AddStaticRect(float cx, float cy, float w, float h, float phi = 0, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiRotatedRect(cx, cy, w, h, phi) { ShapeColor = c ?? Color.Red, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddDynamicCaliper(float sx, float sy, float ex, float ey, float cw, float ch, int count, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiCaliper(sx, sy, ex, ey, cw, ch, count) { ShapeColor = c ?? Color.Lime, Label = l }); SafeInvalidate(); }
        public void AddStaticCaliper(float sx, float sy, float ex, float ey, float cw, float ch, int count, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiCaliper(sx, sy, ex, ey, cw, ch, count) { ShapeColor = c ?? Color.Red, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddDynamicCircleCaliper(float cx, float cy, float r, float sa, float ea, float cw, float ch, int count, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiCircleCaliper(cx, cy, r, sa, ea, cw, ch, count) { ShapeColor = c ?? Color.DodgerBlue, Label = l }); SafeInvalidate(); }
        public void AddStaticCircleCaliper(float cx, float cy, float r, float sa, float ea, float cw, float ch, int count, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiCircleCaliper(cx, cy, r, sa, ea, cw, ch, count) { ShapeColor = c ?? Color.Orange, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddDynamicCircle(float cx, float cy, float r, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiCircle(cx, cy, r) { ShapeColor = c ?? Color.Lime, Label = l }); SafeInvalidate(); }
        public void AddDynamicEllipseCaliper(float cx, float cy, float w, float h, float phi, float cw, float ch, int count, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiEllipseCaliper(cx, cy, w, h, phi, cw, ch, count) { ShapeColor = c ?? Color.DodgerBlue, Label = l }); SafeInvalidate(); }
        public void AddStaticEllipseCaliper(float cx, float cy, float w, float h, float phi, float cw, float ch, int count, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiEllipseCaliper(cx, cy, w, h, phi, cw, ch, count) { ShapeColor = c ?? Color.Red, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddStaticEllipse(float cx, float cy, float w, float h, float phi = 0, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiEllipse(cx, cy, w, h, phi) { ShapeColor = c ?? Color.Cyan, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddDynamicPolygon(List<PointF> pts, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiPolygon(pts) { ShapeColor = c ?? Color.Lime, Label = l }); SafeInvalidate(); }
        public void AddStaticPolygon(List<PointF> pts, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiPolygon(pts) { ShapeColor = c ?? Color.Red, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddContour(List<PointF> pts, Color? c = null) { lock (_roiLock) _staticRois.Add(new RoiContour(pts, c ?? Color.Magenta)); SafeInvalidate(); }
        public void AddDynamicLine(float x1, float y1, float x2, float y2, Color? c = null, string l = "") { lock (_roiLock) _dynamicRois.Add(new RoiLine(x1, y1, x2, y2) { ShapeColor = c ?? Color.Lime, Label = l }); SafeInvalidate(); }
        public void AddStaticLine(float x1, float y1, float x2, float y2, Color? c = null, string l = "") { lock (_roiLock) _staticRois.Add(new RoiLine(x1, y1, x2, y2) { ShapeColor = c ?? Color.Red, Label = l, IsStatic = true }); SafeInvalidate(); }
        public void AddTextBlock(string title, List<(string text, Color color)> lines, float imgX = 0, float imgY = 0, float fontSize = 11f) { lock (_roiLock) _staticRois.Add(new RoiTextBlock(title, lines, imgX, imgY, fontSize)); SafeInvalidate(); }
        public void AddResultBadge(string text, Color bgColor, float imgX, float imgY, float fontSize = 48f) { lock (_roiLock) _staticRois.Add(new RoiResultBadge(text, bgColor, imgX, imgY, fontSize)); SafeInvalidate(); }
        public void AddScreenResultBadge(string text, Color color) { lock (_roiLock) _staticRois.Add(new RoiScreenResultBadge(text, color)); SafeInvalidate(); }

        /// <summary>在图像上叠加半透明 mask 区域显示（mask 为灰度 Bitmap，>0 的像素以指定颜色半透明显示）</summary>
        /// <param name="mask">二值/灰度 mask 位图</param>
        /// <param name="offsetX">mask 在原图中的左上角 X（列）偏移</param>
        /// <param name="offsetY">mask 在原图中的左上角 Y（行）偏移</param>
        /// <param name="color">叠加显示颜色</param>
        /// <param name="alpha">透明度 0~255，默认100</param>
        public void AddMaskOverlay(Bitmap mask, int offsetX, int offsetY, Color color, int alpha = 100)
        {
            if (mask == null) return;
            lock (_roiLock) _staticRois.Add(new RoiMaskOverlay(mask, offsetX, offsetY, color, alpha));
            SafeInvalidate();
        }

        public enum TextPosition { TopLeft, TopRight, BottomLeft, BottomRight }

        private class TextElement { public string Text; public TextPosition Position; public Color Color; public int FontSize; public int Padding; public float LineSpacing; }
        private readonly List<TextElement> _textElements = new List<TextElement>();

        /// <summary>绘制文本（保留旧版风格）</summary>
        public void DrawText(string text, TextPosition position = TextPosition.TopLeft, Color color = default, int fontSize = 12, int padding = 10, float lineSpacing = 0.3f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (color.IsEmpty) color = Color.White;
            SafeInvoke(() => {
                string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
                lock (_roiLock)
                {
                    foreach (var line in lines)
                    {
                        _textElements.Add(new TextElement { Text = line, Position = position, Color = color, FontSize = fontSize, Padding = padding, LineSpacing = lineSpacing });
                    }
                }
                Invalidate();
            });
        }

        public void ClearAllText() => SafeInvoke(() => { lock (_roiLock) _textElements.Clear(); Invalidate(); });

        private void SafeInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) try { BeginInvoke(action); } catch { }
            else action?.Invoke();
        }

        /// <summary>线程安全的 Invalidate，确保在 UI 线程上执行</summary>
        private void SafeInvalidate()
        {
            SafeInvoke(() => Invalidate());
        }

        #endregion

        #region Rendering
        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.InterpolationMode = (_isInteracting || _scale > 5 || _scale < 0.5f) ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawCheckerBoard(g);
                lock (_imageLock)
                {
                    if (_image != null)
                    {
                        float l = Math.Max(0, -_offset.X / _scale), t = Math.Max(0, -_offset.Y / _scale);
                        float r = Math.Min(_image.Width, (Width - _offset.X) / _scale), b = Math.Min(_image.Height, (Height - _offset.Y) / _scale);
                        RectangleF src = new RectangleF(l, t, r - l, b - t);
                        PointF p1 = ImageToScreen(new PointF(l, t)), p2 = ImageToScreen(new PointF(r, b));
                        // 加入 try-catch 防止外部线程同时锁定或释放该位图资源时引起 "对象当前正在其他地方使用" 的异常
                        try
                        {
                            g.DrawImage(_image, new RectangleF(p1.X, p1.Y, p2.X - p1.X, p2.Y - p1.Y), src, GraphicsUnit.Pixel);
                        }
                        catch (InvalidOperationException)
                        {
                            // 忽略多线程争用时的偶尔绘制失败，避免程序直接抛异常产生红叉现象
                        }
                        
                        if (_scale >= PIXEL_GRID_THRESHOLD) DrawPixelGrid(g, src);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        lock (_roiLock)
                        {
                            foreach (var roi in _dynamicRois.Concat(_staticRois))
                            {
                                RoiTextBlock textBlock = roi as RoiTextBlock;
                                if (textBlock != null)
                                    textBlock.SetControlBounds(Width, Height);

                                roi.Draw(g, _scale, _offset);
                            }
                        }
                    }
                }
                DrawAllText(g);
                DrawStatusBar(g);
            }
            catch (Exception ex)
            {
                // 统一捕获，防止GDI+致命崩溃导致控件变为大红叉
                Console.WriteLine($"ShowImageControl OnPaint Error: {ex.Message}");
            }
        }

        private void DrawPixelGrid(Graphics g, RectangleF src)
        {
            int sX = (int)Math.Floor(src.X), sY = (int)Math.Floor(src.Y), eX = (int)Math.Ceiling(src.Right), eY = (int)Math.Ceiling(src.Bottom);
            if ((eX - sX) * (eY - sY) > 5000) return;
            
            // 注意：外层 OnPaint 已经持有 _imageLock 锁
            using (Pen p = new Pen(Color.FromArgb(50, 255, 255, 255), 1f))
            using (Font f = new Font("Consolas", 8f))
            {
                for (int y = sY; y < eY; y++)
                {
                    if (y < 0 || y >= _image.Height) continue;
                    for (int x = sX; x < eX; x++)
                    {
                        if (x < 0 || x >= _image.Width) continue;

                        var tl = ImageToScreen(new PointF(x, y));
                        g.DrawRectangle(p, tl.X, tl.Y, _scale, _scale);
                        if (_scale > 25)
                        {
                            Color v = _image.GetPixel(x, y); 
                            int gray = (int)(v.R * 0.3 + v.G * 0.59 + v.B * 0.11);
                            var sz = g.MeasureString(gray.ToString(), f);
                            g.DrawString(gray.ToString(), f, gray > 128 ? Brushes.Black : Brushes.White, tl.X + (_scale - sz.Width) / 2, tl.Y + (_scale - sz.Height) / 2);
                        }
                    }
                }
            }
        }

        private void DrawCheckerBoard(Graphics g)
        {
            int sz = 30;
            using (Brush darkBrush = new SolidBrush(Color.FromArgb(20, 20, 20)))
            {
                for (int y = 0; y < Height; y += sz)
                {
                    for (int x = 0; x < Width; x += sz)
                    {
                        Brush brush = ((x / sz) + (y / sz)) % 2 == 0 ? Brushes.Black : darkBrush;
                        g.FillRectangle(brush, x, y, sz, sz);
                    }
                }
            }
        }

        private void DrawStatusBar(Graphics g)
        {
            string txt;
            lock (_imageLock)
            {
                txt = _image == null ? "No Image" : $"{_image.Width}x{_image.Height} | R:{_currentPixelColor.R:D3} G:{_currentPixelColor.G:D3} B:{_currentPixelColor.B:D3} | Gray:{_currentGrayValue:D3} | X:{(int)_currentImgPt.X} Y:{(int)_currentImgPt.Y} | Zoom:{_scale * 100:F1}%";
            }
            using (Brush statusBrush = new SolidBrush(Color.FromArgb(180, 10, 10, 10)))
                g.FillRectangle(statusBrush, 0, Height - 22, Width, 22);
            g.DrawString(txt, SystemFonts.DefaultFont, Brushes.LightGray, 8, Height - 18);
        }

        private void DrawAllText(Graphics g)
        {
            List<IGrouping<TextPosition, TextElement>> groups;
            lock (_roiLock)
            {
                if (_textElements.Count == 0) return;
                groups = _textElements.GroupBy(e => e.Position).ToList();
            }
            foreach (var group in groups)
            {
                float usedH = 0;
                foreach (var elem in group)
                {
                    using (Font f = new Font("Microsoft YaHei", elem.FontSize, FontStyle.Bold))
                    using (Font outlineFont = new Font("Microsoft YaHei", elem.FontSize, FontStyle.Bold))
                    {
                        SizeF sz = g.MeasureString(elem.Text, f);
                        float x = elem.Padding, y = elem.Padding + usedH;
                        if (elem.Position == TextPosition.TopRight) x = Width - sz.Width - elem.Padding;
                        else if (elem.Position == TextPosition.BottomLeft) y = Height - 25 - elem.Padding - usedH - sz.Height; // 避开状态栏
                        else if (elem.Position == TextPosition.BottomRight) { x = Width - sz.Width - elem.Padding; y = Height - 25 - elem.Padding - usedH - sz.Height; }
                        
                        // 绘制描边
                        using (Brush bBlack = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                        {
                            g.DrawString(elem.Text, f, bBlack, x + 1, y + 1);
                        }
                        using (Brush b = new SolidBrush(elem.Color)) g.DrawString(elem.Text, f, b, x, y);
                        usedH += sz.Height + elem.FontSize * elem.LineSpacing;
                    }
                }
            }
        }
        #endregion

        #region Interaction
        private IRoiShape _activeRoi = null;
        private int _handleIdx = -1;
        private Point _mouseDownLocation;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            _lastMouse = e.Location; 
            _mouseDownLocation = e.Location;
            PointF imgPt = ScreenToImage(e.Location);
            
            if (e.Button == MouseButtons.Left)
            {
                _isPanning = false; // 初始不将其视为拖拽（除非移动超过阈值）
                lock (_roiLock)
                {
                    _activeRoi = null;
                    _handleIdx = -1;
                    for (int i = _dynamicRois.Count - 1; i >= 0; i--)
                    {
                        int h = _dynamicRois[i].HitTest(e.Location, _scale, _offset);
                        if (h != -1) { _activeRoi = _dynamicRois[i]; _handleIdx = h; break; }
                    }
                    if (_activeRoi != null) { foreach (var r in _dynamicRois) r.IsSelected = (r == _activeRoi); _activeRoi.BeginDrag(imgPt); }
                    else
                    {
                        foreach (var r in _dynamicRois) r.IsSelected = false;
                        if (_rectangleRoiDrawingEnabled && IsImagePointInside(imgPt))
                            BeginRectangleRoiDrawing(imgPt);
                    }
                }
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointF imgPt = ScreenToImage(e.Location); _currentImgPt = imgPt;
            
            if (ShowPixelInfo)
            {
                lock (_imageLock)
                {
                    if (_image != null && imgPt.X >= 0 && imgPt.X < _image.Width && imgPt.Y >= 0 && imgPt.Y < _image.Height)
                    {
                        try
                        {
                            _currentPixelColor = _image.GetPixel((int)imgPt.X, (int)imgPt.Y);
                            _currentGrayValue = (int)(_currentPixelColor.R * 0.3 + _currentPixelColor.G * 0.59 + _currentPixelColor.B * 0.11);
                        }
                        catch
                        {
                            _currentGrayValue = 0;
                        }
                    }
                }
            }

            if (_isDrawingRectangleRoi && e.Button == MouseButtons.Left)
            {
                UpdateDrawingRectangleRoi(imgPt);
                Invalidate();
                return;
            }

            // 判断是否转为拖拽（平移）图像模式
            if (e.Button == MouseButtons.Left && _activeRoi == null && !_isPanning)
            {
                if (Math.Abs(e.X - _mouseDownLocation.X) > 3 || Math.Abs(e.Y - _mouseDownLocation.Y) > 3)
                {
                    _isPanning = true;
                    Cursor = Cursors.SizeAll;
                }
            }

            if (_activeRoi != null && e.Button == MouseButtons.Left) { _activeRoi.DragTo(imgPt, _handleIdx); Invalidate(); }
            else if (_isPanning && e.Button == MouseButtons.Left) { _offset = new PointF(_offset.X + e.X - _lastMouse.X, _offset.Y + e.Y - _lastMouse.Y); _lastMouse = e.Location; Invalidate(); }
            else
            {
                Cursor c = Cursors.Default;
                lock (_roiLock)
                {
                    foreach (var r in _dynamicRois) if (r.HitTest(e.Location, _scale, _offset) != -1) { c = Cursors.Hand; break; }
                }
                if (!_isPanning) Cursor = c;
            }
            Invalidate(new Rectangle(0, Height - 25, Width, 25));
        }

        protected override void OnMouseUp(MouseEventArgs e) 
        { 
            base.OnMouseUp(e);

            // 只有左键释放才结束当前绘制，最终位置可能尚未收到MouseMove。
            if (e.Button != MouseButtons.Left)
                return;

            if (_isDrawingRectangleRoi)
            {
                UpdateDrawingRectangleRoi(ScreenToImage(e.Location));
                FinishRectangleRoiDrawing();
                if (Cursor == Cursors.SizeAll) Cursor = Cursors.Default;
                Invalidate();
                return;
            }
            
            if (e.Button == MouseButtons.Left && !_isPanning && _activeRoi == null && _handleIdx == -1)
            {
                MouseImageClick?.Invoke(ScreenToImage(e.Location));
            }
            
            _activeRoi = null; 
            _isPanning = false; 
            _handleIdx = -1; 
            if (Cursor == Cursors.SizeAll) Cursor = Cursors.Default;
            Invalidate(); 
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Delete && DeleteSelectedDynamicRoi())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// 开始拖拽创建矩形 ROI。
        /// </summary>
        /// <param name="imagePoint">鼠标按下时的图像坐标。</param>
        private void BeginRectangleRoiDrawing(PointF imagePoint)
        {
            _isDrawingRectangleRoi = true;
            _drawingStartImagePoint = ClampImagePoint(imagePoint);
            _drawingRectangleRoi = new RoiRotatedRect(_drawingStartImagePoint.X, _drawingStartImagePoint.Y, 1F, 1F, 0F)
            {
                IsSelected = true,
                ShapeColor = RoiColor,
                Label = "ROI" + (_dynamicRois.Count + 1)
            };
            _dynamicRois.Add(_drawingRectangleRoi);
        }

        /// <summary>
        /// 根据当前拖拽点更新正在创建的矩形 ROI。
        /// </summary>
        /// <param name="imagePoint">当前鼠标图像坐标。</param>
        private void UpdateDrawingRectangleRoi(PointF imagePoint)
        {
            if (_drawingRectangleRoi == null)
                return;

            PointF current = ClampImagePoint(imagePoint);
            float left = Math.Min(_drawingStartImagePoint.X, current.X);
            float top = Math.Min(_drawingStartImagePoint.Y, current.Y);
            float right = Math.Max(_drawingStartImagePoint.X, current.X);
            float bottom = Math.Max(_drawingStartImagePoint.Y, current.Y);
            _drawingRectangleRoi.CX = (left + right) / 2F;
            _drawingRectangleRoi.CY = (top + bottom) / 2F;
            _drawingRectangleRoi.W = Math.Max(1F, right - left);
            _drawingRectangleRoi.H = Math.Max(1F, bottom - top);
            _drawingRectangleRoi.Phi = 0F;
        }

        /// <summary>
        /// 完成矩形 ROI 创建，过小的误操作区域会被丢弃。
        /// </summary>
        private void FinishRectangleRoiDrawing()
        {
            RoiRotatedRect roi = _drawingRectangleRoi;
            _isDrawingRectangleRoi = false;
            _drawingRectangleRoi = null;
            _activeRoi = null;
            _handleIdx = -1;

            if (roi == null)
                return;

            if (roi.W < 5F || roi.H < 5F)
            {
                lock (_roiLock)
                {
                    _dynamicRois.Remove(roi);
                    DisposeRoiShape(roi);
                }
            }
        }

        /// <summary>
        /// 判断图像坐标点是否位于当前图像范围内。
        /// </summary>
        /// <param name="imagePoint">图像坐标点。</param>
        /// <returns>在图像内返回 true。</returns>
        private bool IsImagePointInside(PointF imagePoint)
        {
            lock (_imageLock)
            {
                return _image != null &&
                    imagePoint.X >= 0F &&
                    imagePoint.Y >= 0F &&
                    imagePoint.X < _image.Width &&
                    imagePoint.Y < _image.Height;
            }
        }

        /// <summary>
        /// 将图像坐标限制到当前图像范围内，避免拖拽到图像外生成非法 ROI。
        /// </summary>
        /// <param name="imagePoint">原始图像坐标。</param>
        /// <returns>限制后的图像坐标。</returns>
        private PointF ClampImagePoint(PointF imagePoint)
        {
            lock (_imageLock)
            {
                if (_image == null)
                    return imagePoint;

                float x = Math.Max(0F, Math.Min(_image.Width - 1F, imagePoint.X));
                float y = Math.Max(0F, Math.Min(_image.Height - 1F, imagePoint.Y));
                return new PointF(x, y);
            }
        }
        #endregion

        #region Helpers
        public Bitmap GetResultBitmap()
        {
            lock (_imageLock)
            {
                if (_image == null) return null;
                var bmp = (Bitmap)_image.Clone();
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    foreach (var roi in _staticRois) roi.Draw(g, 1f, PointF.Empty);
                }
                return bmp;
            }
        }

        private void SaveOriginalImage() 
        { 
            lock (_imageLock)
            {
                if (_image != null) using (var sfd = new SaveFileDialog { Filter = "JPG|*.jpg" }) if (sfd.ShowDialog() == DialogResult.OK) _image.Save(sfd.FileName); 
            }
        }
        private void SaveResultImage() { using (var b = new Bitmap(Width, Height)) { DrawToBitmap(b, new Rectangle(0, 0, Width, Height)); using (var sfd = new SaveFileDialog { Filter = "JPG|*.jpg" }) if (sfd.ShowDialog() == DialogResult.OK) b.Save(sfd.FileName); } }

        private static void DrawLabel(Graphics g, string label, PointF center)
        {
            if (string.IsNullOrEmpty(label)) return;
            using (Font f = new Font("SimSun", 10f, FontStyle.Bold))
            using (Brush backgroundBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                var sz = g.MeasureString(label, f);
                var r = new RectangleF(center.X - sz.Width / 2, center.Y - sz.Height / 2, sz.Width, sz.Height);
                g.FillRectangle(backgroundBrush, r);
                g.DrawString(label, f, Brushes.Yellow, r.X, r.Y);
            }
        }

        private static void DrawCross(Graphics g, PointF sc)
        {
            using (Pen gp = new Pen(Color.Lime, 1.5f)) { g.DrawLine(gp, sc.X - 10, sc.Y, sc.X + 10, sc.Y); g.DrawLine(gp, sc.X, sc.Y - 10, sc.X, sc.Y + 10); }
        }
        #endregion

        #region ROI Definition
        public interface IRoiShape { bool IsSelected { get; set; } bool IsStatic { get; set; } string Label { get; set; } Color ShapeColor { get; set; } void Draw(Graphics g, float s, PointF o); int HitTest(Point p, float s, PointF o); void BeginDrag(PointF p); void DragTo(PointF p, int h); PointF GetCenter(); }

        public class RoiRotatedRect : IRoiShape
        {
            public float CX, CY, W, H, Phi;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }
            public float StrokeWidth { get; set; } = 2F;
            private float _oCX, _oCY, _oW, _oH, _oPhi; private PointF _sPt;
            private float _startAngleOffset;

            public RoiRotatedRect(float cx, float cy, float w, float h, float p) { CX = cx; CY = cy; W = w; H = h; Phi = p; ShapeColor = Color.Lime; }
            public PointF GetCenter() => new PointF(CX, CY);
            public void Draw(Graphics g, float s, PointF o)
            {
                var st = g.Save(); PointF sc = new PointF(CX * s + o.X, CY * s + o.Y);
                g.TranslateTransform(sc.X, sc.Y); g.RotateTransform(Phi);
                float sw = W * s, sh = H * s;
                Color drawCol = (IsSelected && !IsStatic) ? Color.Cyan : ShapeColor;
                using (Pen p = new Pen(drawCol, Math.Max(1F, StrokeWidth)))
                {
                    g.DrawRectangle(p, -sw / 2, -sh / 2, sw, sh);
                    if (!IsStatic) using (Pen d = new Pen(Color.Black, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(d, -sw / 2 + 2, -sh / 2 + 2, sw - 4, sh - 4);
                }
                if (!IsStatic && IsSelected)
                {
                    using (Brush orb = new SolidBrush(Color.Orange))
                    using (Pen rotatePen = new Pen(Color.Orange, 2f))
                    {
                        g.FillRectangle(orb, -sw / 2 - 4, -sh / 2 - 4, 8, 8); g.FillRectangle(orb, sw / 2 - 4, -sh / 2 - 4, 8, 8);
                        g.FillRectangle(orb, sw / 2 - 4, sh / 2 - 4, 8, 8); g.FillRectangle(orb, -sw / 2 - 4, sh / 2 - 4, 8, 8);
                        g.DrawArc(rotatePen, sw / 2 + 5, -sh / 2 - 15, 15, 15, -90, 180);
                    }
                }
                g.Restore(st); DrawCross(g, sc); DrawLabel(g, Label, sc);
            }
            public int HitTest(Point p, float s, PointF o)
            {
                if (IsStatic) return -1;
                PointF lp = RotatePoint(p, new PointF(CX * s + o.X, CY * s + o.Y), (float)(-Phi * Math.PI / 180.0));
                float sw = W * s, sh = H * s; float tx = lp.X - (CX * s + o.X), ty = lp.Y - (CY * s + o.Y);
                if (Math.Abs(tx - sw / 2 - 10) < 15 && Math.Abs(ty + sh / 2 + 10) < 15) return 5;
                if (Math.Abs(tx + sw / 2) < 8 && Math.Abs(ty + sh / 2) < 8) return 1;
                if (Math.Abs(tx - sw / 2) < 8 && Math.Abs(ty + sh / 2) < 8) return 2;
                if (Math.Abs(tx - sw / 2) < 8 && Math.Abs(ty - sh / 2) < 8) return 3;
                if (Math.Abs(tx + sw / 2) < 8 && Math.Abs(ty - sh / 2) < 8) return 4;
                return (Math.Abs(tx) <= sw / 2 && Math.Abs(ty) <= sh / 2) ? 0 : -1;
            }
            public void BeginDrag(PointF p) { _sPt = p; _oCX = CX; _oCY = CY; _oW = W; _oH = H; _oPhi = Phi; _startAngleOffset = Phi - (float)(Math.Atan2(p.Y - CY, p.X - CX) * 180.0 / Math.PI); }
            public void DragTo(PointF p, int h)
            {
                if (h == 0) { CX = _oCX + (p.X - _sPt.X); CY = _oCY + (p.Y - _sPt.Y); }
                else if (h == 5) { double currentRad = Math.Atan2(p.Y - CY, p.X - CX); Phi = _startAngleOffset + (float)(currentRad * 180.0 / Math.PI); }
                else
                {
                    var lp = RotatePoint(p, new PointF(CX, CY), (float)(-Phi * Math.PI / 180.0));
                    var slp = RotatePoint(_sPt, new PointF(CX, CY), (float)(-Phi * Math.PI / 180.0));
                    float dx = (lp.X - slp.X) * 2, dy = (lp.Y - slp.Y) * 2;
                    if (h == 3) { W = Math.Max(5, _oW + dx); H = Math.Max(5, _oH + dy); }
                    else if (h == 1) { W = Math.Max(5, _oW - dx); H = Math.Max(5, _oH - dy); }
                    else if (h == 2) { W = Math.Max(5, _oW + dx); H = Math.Max(5, _oH - dy); }
                    else if (h == 4) { W = Math.Max(5, _oW - dx); H = Math.Max(5, _oH + dy); }
                }
            }
            private static PointF RotatePoint(PointF p, PointF c, float r) { float s = (float)Math.Sin(r), co = (float)Math.Cos(r); return new PointF(co * (p.X - c.X) - s * (p.Y - c.Y) + c.X, s * (p.X - c.X) + co * (p.Y - c.Y) + c.Y); }
        }

        public class RoiCircle : IRoiShape
        {
            public float CX, CY, R; public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            string IRoiShape.Label { get => Label; set => Label = value; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }
            public float StrokeWidth { get; set; } = 2F;
            private float _oCX, _oCY, _oR; private PointF _sPt;
            public RoiCircle(float x, float y, float r) { CX = x; CY = y; R = r; ShapeColor = Color.Lime; }
            public PointF GetCenter() => new PointF(CX, CY);
            public void Draw(Graphics g, float s, PointF o)
            {
                PointF sc = new PointF(CX * s + o.X, CY * s + o.Y); float sr = R * s;
                Color drawCol = (IsSelected && !IsStatic) ? Color.Cyan : ShapeColor;
                using (Pen p = new Pen(drawCol, Math.Max(1F, StrokeWidth))) { g.DrawEllipse(p, sc.X - sr, sc.Y - sr, sr * 2, sr * 2); if (!IsStatic) using (Pen d = new Pen(Color.Black, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(d, sc.X - sr + 2, sc.Y - sr + 2, (sr - 2) * 2, (sr - 2) * 2); }
                if (!IsStatic && IsSelected) g.FillRectangle(Brushes.Orange, sc.X + sr - 4, sc.Y - 4, 8, 8);
                DrawCross(g, sc); DrawLabel(g, Label, sc);
            }
            public int HitTest(Point p, float s, PointF o) { if (IsStatic) return -1; float dist = (float)Math.Sqrt(Math.Pow(p.X - (CX * s + o.X), 2) + Math.Pow(p.Y - (CY * s + o.Y), 2)); if (Math.Abs(dist - R * s) < 8) return 1; return dist < R * s ? 0 : -1; }
            public void BeginDrag(PointF p) { _sPt = p; _oCX = CX; _oCY = CY; _oR = R; }
            public void DragTo(PointF p, int h) { if (h == 0) { CX = _oCX + p.X - _sPt.X; CY = _oCY + p.Y - _sPt.Y; } else { R = Math.Max(5, (float)Math.Sqrt(Math.Pow(p.X - CX, 2) + Math.Pow(p.Y - CY, 2))); } }
        }

        /// <summary>
        /// 静态圆弧 ROI，用于夹角、方向等结果标注。
        /// </summary>
        public class RoiArc : IRoiShape
        {
            /// <summary>
            /// 圆心、半径、起始角和扫描角，均使用图像坐标系。
            /// </summary>
            public float CX, CY, Radius, StartAngle, SweepAngle;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; } = true;
            public string Label { get; set; }
            public Color ShapeColor { get; set; } = Color.Red;
            public float StrokeWidth { get; set; } = 2F;

            /// <summary>
            /// 创建静态圆弧 ROI。
            /// </summary>
            public RoiArc(float cx, float cy, float radius, float startAngle, float sweepAngle)
            {
                CX = cx;
                CY = cy;
                Radius = radius;
                StartAngle = startAngle;
                SweepAngle = sweepAngle;
            }

            public PointF GetCenter() => new PointF(CX, CY);

            /// <summary>
            /// 按当前图像缩放和偏移绘制圆弧。
            /// </summary>
            public void Draw(Graphics g, float s, PointF o)
            {
                float scaledRadius = Radius * s;
                if (scaledRadius <= 0.5F || Math.Abs(SweepAngle) <= 0.1F)
                    return;

                PointF screenCenter = new PointF(CX * s + o.X, CY * s + o.Y);
                using (Pen pen = new Pen(ShapeColor, Math.Max(1F, StrokeWidth)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(
                        pen,
                        screenCenter.X - scaledRadius,
                        screenCenter.Y - scaledRadius,
                        scaledRadius * 2F,
                        scaledRadius * 2F,
                        StartAngle,
                        SweepAngle);
                }
            }

            public int HitTest(Point p, float s, PointF o) => -1;
            public void BeginDrag(PointF p) { }
            public void DragTo(PointF p, int h) { }
        }

        public class RoiPolygon : IRoiShape
        {
            public List<PointF> Points; public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            string IRoiShape.Label { get => Label; set => Label = value; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }
            private List<PointF> _oPts; private PointF _sPt;
            public RoiPolygon(List<PointF> pts) { Points = pts; ShapeColor = Color.Lime; }
            public PointF GetCenter() => new PointF(Points.Average(p => p.X), Points.Average(p => p.Y));
            public void Draw(Graphics g, float s, PointF o)
            {
                if (Points.Count < 2) return;
                PointF[] pts = Points.Select(p => new PointF(p.X * s + o.X, p.Y * s + o.Y)).ToArray();
                Color drawCol = (IsSelected && !IsStatic) ? Color.Cyan : ShapeColor;
                using (Pen p = new Pen(drawCol, 2f)) { g.DrawPolygon(p, pts); if (!IsStatic) using (Pen d = new Pen(Color.Black, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawPolygon(d, pts); }
                if (!IsStatic && IsSelected) foreach (var pt in pts) g.FillRectangle(Brushes.Orange, pt.X - 4, pt.Y - 4, 8, 8);
                PointF c = new PointF(pts.Average(p => p.X), pts.Average(p => p.Y)); DrawCross(g, c); DrawLabel(g, Label, c);
            }
            public int HitTest(Point p, float s, PointF o) { if (IsStatic) return -1; for (int i = 0; i < Points.Count; i++) { var sp = new PointF(Points[i].X * s + o.X, Points[i].Y * s + o.Y); if (Math.Abs(p.X - sp.X) < 8 && Math.Abs(p.Y - sp.Y) < 8) return i + 1; } PointF[] pts = Points.Select(pt => new PointF(pt.X * s + o.X, pt.Y * s + o.Y)).ToArray(); using (GraphicsPath gp = new GraphicsPath()) { gp.AddPolygon(pts); if (gp.IsVisible(p)) return 0; } return -1; }
            public void BeginDrag(PointF p) { _sPt = p; _oPts = new List<PointF>(Points); }
            public void DragTo(PointF p, int h) { if (h == 0) { float dx = p.X - _sPt.X, dy = p.Y - _sPt.Y; for (int i = 0; i < Points.Count; i++) Points[i] = new PointF(_oPts[i].X + dx, _oPts[i].Y + dy); } else if (h > 0 && h <= Points.Count) Points[h - 1] = p; }
        }

        public class RoiContour : IRoiShape
        {
            public List<PointF> Points; public Color ShapeColor { get; set; }
            public float StrokeWidth { get; set; } = 2F;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            string IRoiShape.Label { get => Label; set => Label = value; }
            public string Label { get; set; }
            public RoiContour(List<PointF> pts, Color c) { Points = pts; ShapeColor = c; IsStatic = true; }
            public PointF GetCenter() => Points.Count == 0 ? PointF.Empty : new PointF(Points.Average(p => p.X), Points.Average(p => p.Y));
            public void Draw(Graphics g, float s, PointF o)
            {
                if (Points.Count == 0) return;
                using (Brush b = new SolidBrush(ShapeColor))
                {
                    float dotSize = Math.Max(2.0f, StrokeWidth * Math.Max(0.7F, s));
                    foreach (var p in Points) g.FillRectangle(b, p.X * s + o.X, p.Y * s + o.Y, dotSize, dotSize);
                }
            }
            public int HitTest(Point p, float s, PointF o) => -1;
            public void BeginDrag(PointF p) { }
            public void DragTo(PointF p, int h) { }
        }

        public class RoiCaliper : IRoiShape
        {
            public float SX, SY, EX, EY, CaliperHeight, CaliperWidth;
            public int Count;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }
            private float _oSX, _oSY, _oEX, _oEY, _oCH, _oCW; private PointF _sPt;
            public float Angle => (float)(Math.Atan2(EY - SY, EX - SX) * 180.0 / Math.PI);
            public int Direction { get; set; } = 0;
            public RoiCaliper(float sx, float sy, float ex, float ey, float cw, float ch, int count = 20) { SX = sx; SY = sy; EX = ex; EY = ey; CaliperHeight = ch; CaliperWidth = cw; Count = Math.Max(1, count); ShapeColor = Color.DodgerBlue; }
            public PointF GetCenter() => new PointF((SX + EX) / 2, (SY + EY) / 2);
            public void Draw(Graphics g, float s, PointF o)
            {
                float dx = EX - SX, dy = EY - SY;
                float segLen = (float)Math.Sqrt(dx * dx + dy * dy);
                float phi = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                PointF center = GetCenter();
                float halfSeg = segLen * s / 2;
                var st = g.Save(); PointF sc = new PointF(center.X * s + o.X, center.Y * s + o.Y);
                g.TranslateTransform(sc.X, sc.Y); g.RotateTransform(phi);
                Color mainCol = (IsSelected && !IsStatic) ? Color.DodgerBlue : ShapeColor;
                float sw = CaliperWidth * s, sh = CaliperHeight * s;
                using (Pen p = new Pen(mainCol, 2f))
                {
                    g.DrawLine(p, -halfSeg, 0, halfSeg, 0);
                    for (int i = 0; i < Count; i++)
                    {
                        float cx = (Count == 1) ? 0 : (-halfSeg + (segLen * s / (Count - 1)) * i);
                        float rectLeft = Math.Max(-halfSeg, cx - sw / 2);
                        float rectRight = Math.Min(halfSeg, cx + sw / 2);
                        if (rectRight > rectLeft) g.DrawRectangle(p, rectLeft, -sh / 2, rectRight - rectLeft, sh);
                    }
                }
                using (Brush b = new SolidBrush(Color.FromArgb(120, mainCol)))
                using (Pen ap = new Pen(mainCol, 2f))
                {
                    float arrowSize = Math.Min(12, sh / 4 + 4);
                    if (Direction == 0) { ap.DashStyle = DashStyle.Dash; g.DrawLine(ap, 0, -sh / 2, 0, sh / 2); ap.DashStyle = DashStyle.Solid; PointF[] arrowHead = { new PointF(-arrowSize * 0.8f, sh / 2 - arrowSize), new PointF(arrowSize * 0.8f, sh / 2 - arrowSize), new PointF(0, sh / 2) }; g.FillPolygon(b, arrowHead); }
                    else { ap.DashStyle = DashStyle.Dash; g.DrawLine(ap, -halfSeg, 0, halfSeg, 0); ap.DashStyle = DashStyle.Solid; PointF[] arrowHead = { new PointF(halfSeg - arrowSize, -arrowSize * 0.8f), new PointF(halfSeg - arrowSize, arrowSize * 0.8f), new PointF(halfSeg, 0) }; g.FillPolygon(b, arrowHead); }
                }
                if (!IsStatic && IsSelected) { using (Brush wb = new SolidBrush(Color.White)) using (Pen bp = new Pen(mainCol, 2f)) { g.FillEllipse(wb, -halfSeg - 7, -7, 14, 14); g.DrawEllipse(bp, -halfSeg - 7, -7, 14, 14); g.FillEllipse(wb, halfSeg - 7, -7, 14, 14); g.DrawEllipse(bp, halfSeg - 7, -7, 14, 14); g.FillEllipse(wb, -7, -7, 14, 14); g.DrawEllipse(bp, -7, -7, 14, 14); } using (Brush orb = new SolidBrush(Color.Orange)) { g.FillRectangle(orb, -6, -sh / 2 - 6, 12, 12); } }
                g.Restore(st); if (IsSelected) DrawLabel(g, Label, sc);
            }
            public int HitTest(Point p, float s, PointF o) { if (IsStatic) return -1; float dx = EX - SX, dy = EY - SY; float segLen = (float)Math.Sqrt(dx * dx + dy * dy); float phi = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI); PointF center = GetCenter(); float sw = CaliperWidth * s, sh = CaliperHeight * s; PointF lp = RotatePoint(p, new PointF(center.X * s + o.X, center.Y * s + o.Y), (float)(-phi * Math.PI / 180.0)); float tx = lp.X - (center.X * s + o.X), ty = lp.Y - (center.Y * s + o.Y); if (Math.Sqrt(Math.Pow(tx + segLen * s / 2, 2) + ty * ty) < 14) return 1; if (Math.Sqrt(Math.Pow(tx - segLen * s / 2, 2) + ty * ty) < 14) return 2; if (Math.Sqrt(tx * tx + ty * ty) < 14) return 0; if (Math.Abs(tx) < 12 && Math.Abs(ty + sh / 2 + 5) < 12) return 3; if (Math.Abs(tx) <= segLen * s / 2 && Math.Abs(ty) <= sh / 2) return 0; return -1; }
            public void BeginDrag(PointF p) { _sPt = p; _oSX = SX; _oSY = SY; _oEX = EX; _oEY = EY; _oCH = CaliperHeight; _oCW = CaliperWidth; }
            public void DragTo(PointF p, int h) { float dx = p.X - _sPt.X, dy = p.Y - _sPt.Y; if (h == 0) { SX = _oSX + dx; SY = _oSY + dy; EX = _oEX + dx; EY = _oEY + dy; } else if (h == 1) { SX = _oSX + dx; SY = _oSY + dy; } else if (h == 2) { EX = _oEX + dx; EY = _oEY + dy; } else if (h == 3) { PointF center = GetCenter(); dx = EX - SX; dy = EY - SY; float segLen = (float)Math.Sqrt(dx * dx + dy * dy); float phi = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI); PointF lp = RotatePoint(p, center, (float)(-phi * Math.PI / 180.0)); CaliperHeight = Math.Max(5, Math.Abs(lp.Y - center.Y) * 2); CaliperWidth = Math.Min(segLen, Math.Max(3, Math.Abs(lp.X - center.X) * 2)); } }
            private static PointF RotatePoint(PointF p, PointF c, float r) { float s = (float)Math.Sin(r), co = (float)Math.Cos(r); return new PointF(co * (p.X - c.X) - s * (p.Y - c.Y) + c.X, s * (p.X - c.X) + co * (p.Y - c.Y) + c.Y); }
        }

        public class RoiCircleCaliper : IRoiShape
        {
            public float CX, CY, Radius, CaliperHeight, CaliperWidth;
            public float StartAngle, EndAngle;
            public int Count;
            public int Direction { get; set; } = 0;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }

            private float _oCX, _oCY, _oR, _oCH, _oCW, _oSA, _oEA;
            private PointF _sPt;

            public RoiCircleCaliper(float cx, float cy, float r, float sa, float ea, float cw, float ch, int count = 20)
            {
                CX = cx;
                CY = cy;
                Radius = r;
                StartAngle = sa;
                EndAngle = ea;
                CaliperWidth = cw;
                CaliperHeight = ch;
                Count = Math.Max(1, count);
                ShapeColor = Color.DodgerBlue;
            }

            public PointF GetCenter() => new PointF(CX, CY);

            public void Draw(Graphics g, float s, PointF o)
            {
                var st = g.Save();
                PointF sc = new PointF(CX * s + o.X, CY * s + o.Y);
                g.TranslateTransform(sc.X, sc.Y);

                float sr = Radius * s;
                float sw = CaliperWidth * s;
                float sh = CaliperHeight * s;
                float reqSpan = EndAngle - StartAngle;
                bool isFullCircle = Math.Abs(Math.Abs(reqSpan) - 360) < 0.1f;
                Color mainCol = (IsSelected && !IsStatic) ? Color.DodgerBlue : ShapeColor;

                using (Pen p = new Pen(mainCol, 2f))
                {
                    if (isFullCircle)
                        g.DrawEllipse(p, -sr, -sr, sr * 2, sr * 2);
                    else
                        g.DrawArc(p, -sr, -sr, sr * 2, sr * 2, StartAngle, reqSpan);

                    for (int i = 0; i < Count; i++)
                    {
                        float ratio = Count == 1 ? 0.5f : (float)i / (Count - 1);
                        if (isFullCircle && Count > 1)
                            ratio = (float)i / Count;

                        float angleDeg = StartAngle + reqSpan * ratio;
                        var innerSt = g.Save();
                        g.RotateTransform(angleDeg);
                        g.DrawRectangle(p, sr - sh / 2, -sw / 2, sh, sw);
                        g.Restore(innerSt);
                    }

                    float midAngle = StartAngle + reqSpan / 2f;
                    var arrSt = g.Save();
                    g.RotateTransform(midAngle);
                    using (Pen ap = new Pen(mainCol, 2f))
                    {
                        ap.DashStyle = DashStyle.Dash;
                        g.DrawLine(ap, sr - sh / 2, 0, sr + sh / 2, 0);
                        ap.DashStyle = DashStyle.Solid;
                        float arrowSize = Math.Min(12, sh / 4 + 4);
                        PointF[] arrowHead = Direction == 0
                            ? new[] { new PointF(sr + sh / 2 - arrowSize, -arrowSize * 0.8f), new PointF(sr + sh / 2 - arrowSize, arrowSize * 0.8f), new PointF(sr + sh / 2, 0) }
                            : new[] { new PointF(sr - sh / 2 + arrowSize, -arrowSize * 0.8f), new PointF(sr - sh / 2 + arrowSize, arrowSize * 0.8f), new PointF(sr - sh / 2, 0) };
                        using (Brush b = new SolidBrush(mainCol))
                            g.FillPolygon(b, arrowHead);
                    }
                    g.Restore(arrSt);
                }

                if (!IsStatic && IsSelected)
                {
                    using (Brush wb = new SolidBrush(Color.White))
                    using (Pen bp = new Pen(mainCol, 2f))
                    {
                        g.FillEllipse(wb, -7, -7, 14, 14);
                        g.DrawEllipse(bp, -7, -7, 14, 14);

                        if (!isFullCircle)
                        {
                            float sx = sr * (float)Math.Cos(StartAngle * Math.PI / 180);
                            float sy = sr * (float)Math.Sin(StartAngle * Math.PI / 180);
                            float ex = sr * (float)Math.Cos(EndAngle * Math.PI / 180);
                            float ey = sr * (float)Math.Sin(EndAngle * Math.PI / 180);
                            g.FillEllipse(wb, sx - 6, sy - 6, 12, 12);
                            g.DrawEllipse(bp, sx - 6, sy - 6, 12, 12);
                            g.FillEllipse(wb, ex - 6, ey - 6, 12, 12);
                            g.DrawEllipse(bp, ex - 6, ey - 6, 12, 12);
                        }
                    }

                    using (Brush orb = new SolidBrush(Color.Orange))
                    {
                        float rHAngle = isFullCircle ? 0 : StartAngle + reqSpan / 2f;
                        float rx = sr * (float)Math.Cos(rHAngle * Math.PI / 180);
                        float ry = sr * (float)Math.Sin(rHAngle * Math.PI / 180);
                        g.FillRectangle(orb, rx - 6, ry - 6, 12, 12);

                        float whHAngle = isFullCircle ? -90 : StartAngle + reqSpan / 2f;
                        float whx = (sr + sh / 2) * (float)Math.Cos(whHAngle * Math.PI / 180);
                        float why = (sr + sh / 2) * (float)Math.Sin(whHAngle * Math.PI / 180);
                        g.FillRectangle(orb, whx - 6, why - 6, 12, 12);
                    }
                }

                g.Restore(st);
                if (IsSelected) DrawLabel(g, Label, sc);
            }

            public int HitTest(Point p, float s, PointF o)
            {
                if (IsStatic) return -1;
                PointF sc = new PointF(CX * s + o.X, CY * s + o.Y);
                float tx = p.X - sc.X;
                float ty = p.Y - sc.Y;
                float dist = (float)Math.Sqrt(tx * tx + ty * ty);
                float sr = Radius * s;
                float sh = CaliperHeight * s;
                float reqSpan = EndAngle - StartAngle;
                bool isFullCircle = Math.Abs(Math.Abs(reqSpan) - 360) < 0.1f;

                float whHAngle = isFullCircle ? -90 : StartAngle + reqSpan / 2f;
                float whx = (sr + sh / 2) * (float)Math.Cos(whHAngle * Math.PI / 180);
                float why = (sr + sh / 2) * (float)Math.Sin(whHAngle * Math.PI / 180);
                if (Math.Sqrt((tx - whx) * (tx - whx) + (ty - why) * (ty - why)) < 12) return 4;

                float rHAngle = isFullCircle ? 0 : StartAngle + reqSpan / 2f;
                float rx = sr * (float)Math.Cos(rHAngle * Math.PI / 180);
                float ry = sr * (float)Math.Sin(rHAngle * Math.PI / 180);
                if (Math.Sqrt((tx - rx) * (tx - rx) + (ty - ry) * (ty - ry)) < 12) return 3;

                if (dist < 14) return 0;

                if (!isFullCircle)
                {
                    float sx = sr * (float)Math.Cos(StartAngle * Math.PI / 180);
                    float sy = sr * (float)Math.Sin(StartAngle * Math.PI / 180);
                    if (Math.Sqrt((tx - sx) * (tx - sx) + (ty - sy) * (ty - sy)) < 12) return 1;

                    float ex = sr * (float)Math.Cos(EndAngle * Math.PI / 180);
                    float ey = sr * (float)Math.Sin(EndAngle * Math.PI / 180);
                    if (Math.Sqrt((tx - ex) * (tx - ex) + (ty - ey) * (ty - ey)) < 12) return 2;
                }

                if (dist >= sr - sh / 2 - 5 && dist <= sr + sh / 2 + 5)
                {
                    if (isFullCircle) return 0;

                    float a = (float)(Math.Atan2(ty, tx) * 180 / Math.PI);
                    a = a >= 0 ? a : a + 360;
                    float sa = StartAngle % 360; if (sa < 0) sa += 360;
                    float ea = EndAngle % 360; if (ea < 0) ea += 360;
                    bool inArc = sa <= ea ? (a >= sa && a <= ea) : (a >= sa || a <= ea);
                    if (inArc) return 0;
                }

                return -1;
            }

            public void BeginDrag(PointF p)
            {
                _sPt = p;
                _oCX = CX;
                _oCY = CY;
                _oR = Radius;
                _oSA = StartAngle;
                _oEA = EndAngle;
                _oCH = CaliperHeight;
                _oCW = CaliperWidth;
            }

            public void DragTo(PointF p, int h)
            {
                if (h == 0)
                {
                    CX = _oCX + p.X - _sPt.X;
                    CY = _oCY + p.Y - _sPt.Y;
                }
                else if (h == 1)
                {
                    StartAngle = (float)(Math.Atan2(p.Y - CY, p.X - CX) * 180 / Math.PI);
                }
                else if (h == 2)
                {
                    EndAngle = (float)(Math.Atan2(p.Y - CY, p.X - CX) * 180 / Math.PI);
                }
                else if (h == 3)
                {
                    Radius = Math.Max(5, (float)Math.Sqrt(Math.Pow(p.X - CX, 2) + Math.Pow(p.Y - CY, 2)));
                }
                else if (h == 4)
                {
                    float dist = (float)Math.Sqrt(Math.Pow(p.X - CX, 2) + Math.Pow(p.Y - CY, 2));
                    CaliperHeight = Math.Max(5, Math.Abs(dist - Radius) * 2);

                    float reqSpan = _oEA - _oSA;
                    bool isFullCircle = Math.Abs(Math.Abs(reqSpan) - 360) < 0.1f;
                    float whHAngle = isFullCircle ? -90 : _oSA + reqSpan / 2f;
                    PointF lp = RotatePoint(p, new PointF(CX, CY), (float)(-whHAngle * Math.PI / 180.0));
                    CaliperWidth = Math.Max(3, Math.Abs(lp.Y - CY) * 2);
                }
            }

            private static PointF RotatePoint(PointF p, PointF c, float r) { float s = (float)Math.Sin(r), co = (float)Math.Cos(r); return new PointF(co * (p.X - c.X) - s * (p.Y - c.Y) + c.X, s * (p.X - c.X) + co * (p.Y - c.Y) + c.Y); }
        }

        public class RoiEllipseCaliper : IRoiShape
        {
            public float CX, CY, W, H, Phi;
            public float CaliperWidth, CaliperHeight;
            public int Count;
            public int Direction { get; set; } = 0;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }

            private float _oCX, _oCY, _oW, _oH, _oPhi, _oCH, _oCW;
            private PointF _sPt;
            private float _startAngleOffset;

            public RoiEllipseCaliper(float cx, float cy, float w, float h, float phi, float cw, float ch, int count = 30)
            {
                CX = cx;
                CY = cy;
                W = w;
                H = h;
                Phi = phi;
                CaliperWidth = cw;
                CaliperHeight = ch;
                Count = Math.Max(1, count);
                ShapeColor = Color.DodgerBlue;
            }

            public PointF GetCenter() => new PointF(CX, CY);

            public void Draw(Graphics g, float s, PointF o)
            {
                var st = g.Save();
                PointF sc = new PointF(CX * s + o.X, CY * s + o.Y);
                g.TranslateTransform(sc.X, sc.Y);
                g.RotateTransform(Phi);

                float sw = W * s;
                float sh = H * s;
                float scw = CaliperWidth * s;
                float sch = CaliperHeight * s;
                float a = sw / 2f;
                float b = sh / 2f;
                Color mainCol = (IsSelected && !IsStatic) ? Color.DodgerBlue : ShapeColor;

                using (Pen p = new Pen(mainCol, 2f))
                {
                    g.DrawEllipse(p, -a, -b, sw, sh);
                    using (Pen d = new Pen(Color.Lime, 1f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(d, -a, -b, sw, sh);

                    for (int i = 0; i < Count; i++)
                    {
                        float t = (float)(2.0 * Math.PI * i / Count);
                        float cost = (float)Math.Cos(t);
                        float sint = (float)Math.Sin(t);
                        float lx = a * cost;
                        float ly = b * sint;
                        float nx = cost / Math.Max(1f, a);
                        float ny = sint / Math.Max(1f, b);
                        float len = (float)Math.Sqrt(nx * nx + ny * ny);
                        nx /= len;
                        ny /= len;

                        var innerSt = g.Save();
                        g.TranslateTransform(lx, ly);
                        g.RotateTransform((float)(Math.Atan2(ny, nx) * 180.0 / Math.PI));
                        g.DrawRectangle(p, -sch / 2, -scw / 2, sch, scw);
                        g.Restore(innerSt);
                    }

                    var arrSt = g.Save();
                    g.TranslateTransform(a, 0);
                    using (Pen ap = new Pen(mainCol, 2f))
                    {
                        ap.DashStyle = DashStyle.Dash;
                        g.DrawLine(ap, -sch / 2, 0, sch / 2, 0);
                        ap.DashStyle = DashStyle.Solid;
                        float arrowSize = Math.Min(12, sch / 4 + 4);
                        PointF[] arrowHead = Direction == 0
                            ? new[] { new PointF(sch / 2 - arrowSize, -arrowSize * 0.8f), new PointF(sch / 2 - arrowSize, arrowSize * 0.8f), new PointF(sch / 2, 0) }
                            : new[] { new PointF(-sch / 2 + arrowSize, -arrowSize * 0.8f), new PointF(-sch / 2 + arrowSize, arrowSize * 0.8f), new PointF(-sch / 2, 0) };
                        using (Brush brush = new SolidBrush(mainCol))
                            g.FillPolygon(brush, arrowHead);
                    }
                    g.Restore(arrSt);
                }

                if (!IsStatic && IsSelected)
                {
                    using (Brush orb = new SolidBrush(Color.Orange))
                    {
                        g.FillRectangle(orb, -a - 4, -b - 4, 8, 8);
                        g.FillRectangle(orb, a - 4, -b - 4, 8, 8);
                        g.FillRectangle(orb, a - 4, b - 4, 8, 8);
                        g.FillRectangle(orb, -a - 4, b - 4, 8, 8);
                        using (Pen pen = new Pen(Color.Orange, 2f))
                            g.DrawArc(pen, a + 5, -b - 15, 15, 15, -90, 180);
                        g.FillRectangle(orb, a + sch / 2 - 6, -6, 12, 12);
                    }
                }

                g.Restore(st);
                DrawCross(g, sc);
                DrawLabel(g, Label, sc);
            }

            public int HitTest(Point p, float s, PointF o)
            {
                if (IsStatic) return -1;
                PointF lp = RotatePoint(p, new PointF(CX * s + o.X, CY * s + o.Y), (float)(-Phi * Math.PI / 180.0));
                float sw = W * s;
                float sh = H * s;
                float tx = lp.X - (CX * s + o.X);
                float ty = lp.Y - (CY * s + o.Y);
                float sch = CaliperHeight * s;

                if (Math.Abs(tx - (sw / 2 + sch / 2)) < 12 && Math.Abs(ty) < 12) return 6;
                if (Math.Abs(tx - sw / 2 - 10) < 15 && Math.Abs(ty + sh / 2 + 10) < 15) return 5;
                if (Math.Abs(tx + sw / 2) < 8 && Math.Abs(ty + sh / 2) < 8) return 1;
                if (Math.Abs(tx - sw / 2) < 8 && Math.Abs(ty + sh / 2) < 8) return 2;
                if (Math.Abs(tx - sw / 2) < 8 && Math.Abs(ty - sh / 2) < 8) return 3;
                if (Math.Abs(tx + sw / 2) < 8 && Math.Abs(ty - sh / 2) < 8) return 4;
                return Math.Abs(tx) <= sw / 2 + sch / 2 && Math.Abs(ty) <= sh / 2 + sch / 2 ? 0 : -1;
            }

            public void BeginDrag(PointF p)
            {
                _sPt = p;
                _oCX = CX;
                _oCY = CY;
                _oW = W;
                _oH = H;
                _oPhi = Phi;
                _oCH = CaliperHeight;
                _oCW = CaliperWidth;
                _startAngleOffset = Phi - (float)(Math.Atan2(p.Y - CY, p.X - CX) * 180.0 / Math.PI);
            }

            public void DragTo(PointF p, int h)
            {
                if (h == 0)
                {
                    CX = _oCX + (p.X - _sPt.X);
                    CY = _oCY + (p.Y - _sPt.Y);
                }
                else if (h == 5)
                {
                    double currentRad = Math.Atan2(p.Y - CY, p.X - CX);
                    Phi = _startAngleOffset + (float)(currentRad * 180.0 / Math.PI);
                }
                else if (h == 6)
                {
                    var lp = RotatePoint(p, new PointF(CX, CY), (float)(-Phi * Math.PI / 180.0));
                    CaliperHeight = Math.Max(5, Math.Abs(lp.X - CX - W / 2) * 2);
                    CaliperWidth = Math.Max(3, Math.Abs(lp.Y - CY) * 2);
                }
                else
                {
                    var lp = RotatePoint(p, new PointF(CX, CY), (float)(-Phi * Math.PI / 180.0));
                    var slp = RotatePoint(_sPt, new PointF(CX, CY), (float)(-Phi * Math.PI / 180.0));
                    float dx = (lp.X - slp.X) * 2;
                    float dy = (lp.Y - slp.Y) * 2;
                    if (h == 3) { W = Math.Max(10, _oW + dx); H = Math.Max(10, _oH + dy); }
                    else if (h == 1) { W = Math.Max(10, _oW - dx); H = Math.Max(10, _oH - dy); }
                    else if (h == 2) { W = Math.Max(10, _oW + dx); H = Math.Max(10, _oH - dy); }
                    else if (h == 4) { W = Math.Max(10, _oW - dx); H = Math.Max(10, _oH + dy); }
                }
            }

            private static PointF RotatePoint(PointF p, PointF c, float r) { float s = (float)Math.Sin(r), co = (float)Math.Cos(r); return new PointF(co * (p.X - c.X) - s * (p.Y - c.Y) + c.X, s * (p.X - c.X) + co * (p.Y - c.Y) + c.Y); }
        }

        public class RoiEllipse : IRoiShape
        {
            public float CX, CY, W, H, Phi;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; }
            public string Label { get; set; }
            public Color ShapeColor { get; set; }
            public float StrokeWidth { get; set; } = 2F;

            public RoiEllipse(float cx, float cy, float w, float h, float phi)
            {
                CX = cx;
                CY = cy;
                W = w;
                H = h;
                Phi = phi;
                ShapeColor = Color.Lime;
            }

            public PointF GetCenter() => new PointF(CX, CY);

            public void Draw(Graphics g, float s, PointF o)
            {
                var st = g.Save();
                PointF sc = new PointF(CX * s + o.X, CY * s + o.Y);
                g.TranslateTransform(sc.X, sc.Y);
                g.RotateTransform(Phi);
                float sw = W * s;
                float sh = H * s;
                Color drawCol = (IsSelected && !IsStatic) ? Color.Cyan : ShapeColor;
                using (Pen p = new Pen(drawCol, Math.Max(1F, StrokeWidth)))
                {
                    g.DrawEllipse(p, -sw / 2, -sh / 2, sw, sh);
                    if (!IsStatic)
                        using (Pen d = new Pen(Color.Black, 1.5f) { DashStyle = DashStyle.Dash })
                            g.DrawEllipse(d, -sw / 2 + 2, -sh / 2 + 2, sw - 4, sh - 4);
                }
                g.Restore(st);
                DrawCross(g, sc);
                DrawLabel(g, Label, sc);
            }

            public int HitTest(Point p, float s, PointF o) => -1;
            public void BeginDrag(PointF p) { }
            public void DragTo(PointF p, int h) { }
        }

        public class RoiTextBlock : IRoiShape
        {
            private const float TextPadding = 6F;
            private const float StatusBarReservedHeight = 25F;

            public string Title;
            public List<(string text, Color color)> Lines;
            public float ImgX;
            public float ImgY;
            public float FontSize;

            /// <summary>
            /// 四角文本定位位置。
            /// </summary>
            public DisplayTextPosition Position { get; set; } = DisplayTextPosition.TopLeft;

            /// <summary>
            /// 四角文本距离边缘的边距。
            /// </summary>
            public int Margin { get; set; } = 10;

            /// <summary>
            /// 原始图像宽度，用于按图像显示区域定位。
            /// </summary>
            public int ImageWidth { get; set; }

            /// <summary>
            /// 原始图像高度，用于按图像显示区域定位。
            /// </summary>
            public int ImageHeight { get; set; }

            /// <summary>
            /// 是否按显示控件坐标定位；否则按图像显示区域或绝对图像坐标定位。
            /// </summary>
            public bool UseControlCoordinate { get; set; }

            /// <summary>
            /// 当前显示控件宽度，由控件绘制前写入。
            /// </summary>
            public int ControlWidth { get; private set; }

            /// <summary>
            /// 当前显示控件高度，由控件绘制前写入。
            /// </summary>
            public int ControlHeight { get; private set; }

            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; } = true;
            public string Label { get; set; }
            public Color ShapeColor { get; set; } = Color.White;

            public RoiTextBlock(string title, List<(string text, Color color)> lines, float imgX = 0, float imgY = 0, float fontSize = 11f)
            {
                Title = title;
                Lines = lines ?? new List<(string text, Color color)>();
                ImgX = imgX;
                ImgY = imgY;
                FontSize = fontSize;
            }

            /// <summary>
            /// 写入当前控件尺寸，让控件坐标文本不依赖局部重绘裁剪区域。
            /// </summary>
            public void SetControlBounds(int width, int height)
            {
                ControlWidth = Math.Max(0, width);
                ControlHeight = Math.Max(0, height);
            }

            public PointF GetCenter() => new PointF(ImgX, ImgY);

            public void Draw(Graphics g, float s, PointF o)
            {
                float titleSize = FontSize * 1.2f;
                float lineSize = FontSize;
                using (Font titleFont = new Font("Microsoft YaHei", titleSize, FontStyle.Bold))
                using (Font lineFont = new Font("Microsoft YaHei", lineSize))
                {
                    bool showTitle = !string.IsNullOrWhiteSpace(Title);
                    SizeF titleSizeInfo = showTitle ? g.MeasureString(Title, titleFont) : SizeF.Empty;
                    float maxWidth = showTitle ? titleSizeInfo.Width : 0F;
                    float totalHeight = showTitle ? titleSizeInfo.Height + 4F : 0F;

                    foreach (var line in Lines)
                    {
                        SizeF lineSizeInfo = g.MeasureString(line.text ?? string.Empty, lineFont);
                        if (lineSizeInfo.Width > maxWidth)
                            maxWidth = lineSizeInfo.Width;
                        totalHeight += lineSizeInfo.Height + 2F;
                    }

                    PointF topLeft = ResolveTopLeft(s, o, maxWidth, totalHeight);
                    float x = topLeft.X;
                    float y = topLeft.Y;

                    if (showTitle)
                    {
                        g.DrawString(Title, titleFont, Brushes.White, x, y);
                        y += titleSizeInfo.Height + 4F;
                    }

                    foreach (var line in Lines)
                    {
                        string text = line.text ?? string.Empty;
                        using (Brush brush = new SolidBrush(line.color))
                            g.DrawString(text, lineFont, brush, x, y);

                        y += g.MeasureString(text, lineFont).Height + 2F;
                    }
                }
            }

            /// <summary>
            /// 根据当前坐标系解析文本块左上角屏幕坐标。
            /// </summary>
            private PointF ResolveTopLeft(float scale, PointF offset, float blockWidth, float blockHeight)
            {
                if (UseControlCoordinate && ControlWidth > 0 && ControlHeight > 0)
                    return ResolveCornerTopLeft(0F, 0F, ControlWidth, Math.Max(0F, ControlHeight - StatusBarReservedHeight), blockWidth, blockHeight);

                if (ImageWidth > 0 && ImageHeight > 0)
                    return ResolveCornerTopLeft(offset.X, offset.Y, ImageWidth * scale, ImageHeight * scale, blockWidth, blockHeight);

                return new PointF(ImgX * scale + offset.X, ImgY * scale + offset.Y);
            }

            /// <summary>
            /// 按指定矩形的四角计算文本左上角，右下角保留文本测量误差余量。
            /// </summary>
            private PointF ResolveCornerTopLeft(float left, float top, float width, float height, float blockWidth, float blockHeight)
            {
                float margin = Math.Max(0, Margin);
                float x = left + margin;
                float y = top + margin;

                if (Position == DisplayTextPosition.TopRight || Position == DisplayTextPosition.BottomRight)
                    x = left + width - blockWidth - TextPadding * 2F - margin;

                if (Position == DisplayTextPosition.BottomLeft || Position == DisplayTextPosition.BottomRight)
                    y = top + height - blockHeight - TextPadding * 2F - margin;

                return new PointF(Math.Max(left, x), Math.Max(top, y));
            }

            public int HitTest(Point p, float s, PointF o) => -1;
            public void BeginDrag(PointF p) { }
            public void DragTo(PointF p, int h) { }
        }

        public class RoiResultBadge : IRoiShape
        {
            public string Text; public Color BgColor; public float ImgX, ImgY, FontSize; public bool IsSelected { get; set; } public bool IsStatic { get; set; } = true; public string Label { get; set; } public Color ShapeColor { get; set; }
            public RoiResultBadge(string text, Color bgColor, float imgX, float imgY, float fontSize = 48f) { Text = text; BgColor = bgColor; ImgX = imgX; ImgY = imgY; FontSize = fontSize; }
            public PointF GetCenter() => new PointF(ImgX, ImgY);
            public void Draw(Graphics g, float s, PointF o) { float scaledSize = FontSize; using (Font f = new Font("Microsoft YaHei", scaledSize, FontStyle.Bold)) { var sz = g.MeasureString(Text, f); float px = ImgX * s + o.X, py = ImgY * s + o.Y, pad = scaledSize * 0.3f; RectangleF rect = new RectangleF(px, py, sz.Width + pad * 2, sz.Height + pad * 2); using (Brush bg = new SolidBrush(BgColor)) g.FillRectangle(bg, rect); g.DrawString(Text, f, Brushes.White, px + pad, py + pad); } }
            public int HitTest(Point p, float s, PointF o) => -1; public void BeginDrag(PointF p) { } public void DragTo(PointF p, int h) { }
        }

        public class RoiScreenResultBadge : IRoiShape
        {
            public string Text; public Color Color; public bool IsSelected { get; set; } public bool IsStatic { get; set; } = true; public string Label { get; set; } public Color ShapeColor { get; set; }
            public RoiScreenResultBadge(string text, Color color) { Text = text; Color = color; }
            public PointF GetCenter() => PointF.Empty;
            public void Draw(Graphics g, float s, PointF o)
            {
                using (Font f = new Font("Microsoft YaHei", 72f, FontStyle.Bold))
                {
                    var sz = g.MeasureString(Text, f);
                    // 强制在控件右下角显示，避开状态栏
                    float x = g.VisibleClipBounds.Width - sz.Width - 20;
                    float y = g.VisibleClipBounds.Height - sz.Height - 40;
                    
                    // 绘制阴影/描边
                    g.DrawString(Text, f, Brushes.Black, x + 4, y + 4);
                    using (Brush b = new SolidBrush(Color)) g.DrawString(Text, f, b, x, y);
                }
            }
            public int HitTest(Point p, float s, PointF o) => -1; public void BeginDrag(PointF p) { } public void DragTo(PointF p, int h) { }
        }

        public class RoiLine : IRoiShape
        {
            /// <summary>是否显示中心十字，动态编辑和既有结果默认保留。</summary>
            public bool ShowCenterCross { get; set; } = true;
            public PointF P1, P2; public bool IsSelected { get; set; } public bool IsStatic { get; set; } public string Label { get; set; } public Color ShapeColor { get; set; } public float StrokeWidth { get; set; } = 2F; private PointF _oP1, _oP2, _sPt;
            public RoiLine(float x1, float y1, float x2, float y2) { P1 = new PointF(x1, y1); P2 = new PointF(x2, y2); ShapeColor = Color.Lime; }
            public PointF GetCenter() => new PointF((P1.X + P2.X) / 2, (P1.Y + P2.Y) / 2);
            public void Draw(Graphics g, float s, PointF o) { PointF sp1 = new PointF(P1.X * s + o.X, P1.Y * s + o.Y), sp2 = new PointF(P2.X * s + o.X, P2.Y * s + o.Y); Color drawCol = (IsSelected && !IsStatic) ? Color.Cyan : ShapeColor; using (Pen p = new Pen(drawCol, Math.Max(1F, StrokeWidth))) { g.DrawLine(p, sp1, sp2); if (!IsStatic) using (Pen d = new Pen(Color.Black, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawLine(d, sp1, sp2); } if (!IsStatic && IsSelected) { using (Brush orb = new SolidBrush(Color.Orange)) { g.FillRectangle(orb, sp1.X - 5, sp1.Y - 5, 10, 10); g.FillRectangle(orb, sp2.X - 5, sp2.Y - 5, 10, 10); } } PointF sc = new PointF((sp1.X + sp2.X) / 2, (sp1.Y + sp2.Y) / 2); if (ShowCenterCross) DrawCross(g, sc); DrawLabel(g, Label, sc); }
            public int HitTest(Point p, float s, PointF o) { if (IsStatic) return -1; PointF sp1 = new PointF(P1.X * s + o.X, P1.Y * s + o.Y), sp2 = new PointF(P2.X * s + o.X, P2.Y * s + o.Y); if (Dist(p, sp1) < 8) return 1; if (Dist(p, sp2) < 8) return 2; if (DistToSegment(p, sp1, sp2) < 8) return 0; return -1; }
            public void BeginDrag(PointF p) { _sPt = p; _oP1 = P1; _oP2 = P2; }
            public void DragTo(PointF p, int h) { float dx = p.X - _sPt.X, dy = p.Y - _sPt.Y; if (h == 0) { P1 = new PointF(_oP1.X + dx, _oP1.Y + dy); P2 = new PointF(_oP2.X + dx, _oP2.Y + dy); } else if (h == 1) { P1 = p; } else if (h == 2) { P2 = p; } }
            private static float Dist(PointF a, PointF b) => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
            private static float DistToSegment(PointF p, PointF v, PointF w) { float l2 = (float)(Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2)); if (l2 == 0) return Dist(p, v); float t = Math.Max(0, Math.Min(1, ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2)); return Dist(p, new PointF(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y))); }
        }

        /// <summary>
        /// Mask 区域半透明叠加层（用于可视化颜色匹配检测区域）
        /// </summary>
        public class RoiMaskOverlay : IRoiShape, IDisposable
        {
            private readonly Bitmap _overlayBitmap;
            private readonly int _offsetX, _offsetY;
            public bool IsSelected { get; set; }
            public bool IsStatic { get; set; } = true;
            public string Label { get; set; }
            public Color ShapeColor { get; set; }

            public RoiMaskOverlay(Bitmap mask, int offsetX, int offsetY, Color color, int alpha = 100)
            {
                _offsetX = offsetX;
                _offsetY = offsetY;
                ShapeColor = color;

                // 将灰度 mask 转为带透明度的彩色叠加位图
                _overlayBitmap = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
                var srcRect = new Rectangle(0, 0, mask.Width, mask.Height);
                var maskData = mask.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                var dstData = _overlayBitmap.LockBits(srcRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                for (int y = 0; y < mask.Height; y++)
                {
                    byte[] srcRow = new byte[Math.Abs(maskData.Stride)];
                    byte[] dstRow = new byte[Math.Abs(dstData.Stride)];
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(maskData.Scan0, y * maskData.Stride), srcRow, 0, srcRow.Length);

                    for (int x = 0; x < mask.Width; x++)
                    {
                        if (srcRow[x] > 0)
                        {
                            dstRow[x * 4 + 0] = color.B;
                            dstRow[x * 4 + 1] = color.G;
                            dstRow[x * 4 + 2] = color.R;
                            dstRow[x * 4 + 3] = (byte)alpha;
                        }
                    }

                    System.Runtime.InteropServices.Marshal.Copy(dstRow, 0, IntPtr.Add(dstData.Scan0, y * dstData.Stride), dstRow.Length);
                }
                mask.UnlockBits(maskData);
                _overlayBitmap.UnlockBits(dstData);
            }

            public PointF GetCenter() => new PointF(_offsetX + _overlayBitmap.Width / 2f, _offsetY + _overlayBitmap.Height / 2f);

            public void Draw(Graphics g, float s, PointF o)
            {
                if (_overlayBitmap == null) return;
                float dx = _offsetX * s + o.X;
                float dy = _offsetY * s + o.Y;
                float dw = _overlayBitmap.Width * s;
                float dh = _overlayBitmap.Height * s;
                g.DrawImage(_overlayBitmap, dx, dy, dw, dh);
            }

            public int HitTest(Point p, float s, PointF o) => -1;
            public void BeginDrag(PointF p) { }
            public void DragTo(PointF p, int h) { }

            /// <summary>
            /// 释放Mask可视化创建的内部GDI位图。
            /// </summary>
            public void Dispose()
            {
                _overlayBitmap?.Dispose();
            }
        }
        #endregion

        /// <summary>
        /// 清空ROI集合，并释放其中实现了IDisposable的图像型ROI。
        /// </summary>
        /// <param name="rois">需要清空的ROI集合。</param>
        private static void ClearRoiShapes(ICollection<IRoiShape> rois)
        {
            if (rois == null)
                return;

            foreach (IRoiShape roi in rois)
                DisposeRoiShape(roi);
            rois.Clear();
        }

        /// <summary>
        /// 释放单个可释放ROI，普通几何ROI不执行额外操作。
        /// </summary>
        /// <param name="roi">待释放的ROI。</param>
        private static void DisposeRoiShape(IRoiShape roi)
        {
            (roi as IDisposable)?.Dispose();
        }

        /// <summary>
        /// 控件销毁时释放当前显示图、ROI内部图像和交互定时器。
        /// </summary>
        private void ReleaseOwnedResources()
        {
            if (System.Threading.Interlocked.Exchange(ref _ownedResourcesReleased, 1) != 0)
                return;

            _interactionTimer?.Stop();
            _interactionTimer?.Dispose();

            Bitmap imageToDispose;
            lock (_imageLock)
            {
                imageToDispose = _image;
                _image = null;
            }
            imageToDispose?.Dispose();

            lock (_roiLock)
            {
                ClearRoiShapes(_dynamicRois);
                ClearRoiShapes(_staticRois);
                _textElements.Clear();
            }
        }
    }
}
