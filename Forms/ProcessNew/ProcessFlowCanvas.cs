using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp.Extensions;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using Mat = OpenCvSharp.Mat;

namespace TDJS_Vision.Forms.ProcessNew
{
    /// <summary>
    /// 流程画布右键菜单命中的对象类型。
    /// </summary>
    public enum FlowCanvasContextMenuTarget
    {
        /// <summary>
        /// 空白画布区域。
        /// </summary>
        Canvas,

        /// <summary>
        /// 流程节点。
        /// </summary>
        Node,

        /// <summary>
        /// 节点之间的连线。
        /// </summary>
        Connection
    }

    /// <summary>
    /// 选中节点与其它节点之间的关系类型。
    /// </summary>
    internal enum FlowNodeRelationRole
    {
        /// <summary>
        /// 与当前选中节点无直接输入输出关系。
        /// </summary>
        None,

        /// <summary>
        /// 当前节点是选中节点的输入来源。
        /// </summary>
        Input,

        /// <summary>
        /// 当前节点是选中节点的输出目标。
        /// </summary>
        Output
    }

    /// <summary>
    /// Free flow-canvas surface for the ProcessNew editor.
    /// </summary>
    public class ProcessFlowCanvas : Panel
    {
        private const int GridSize = 24;
        private const int AnchorSize = 12;
        private static readonly Size LogicalCanvasSize = new Size(3200, 2200);
        private static readonly Color CanvasBackColor = Color.FromArgb(48, 52, 56);
        private static readonly Color GridLineColor = Color.FromArgb(61, 67, 72);
        private static readonly Color GridMajorLineColor = Color.FromArgb(72, 79, 85);
        private static readonly Color NodeFillColor = Color.FromArgb(248, 250, 252);
        private static readonly Color NodeDisabledFillColor = Color.FromArgb(221, 225, 231);
        private static readonly Color NodeTextColor = Color.FromArgb(29, 41, 57);
        private static readonly Color NodeMetaTextColor = Color.FromArgb(86, 99, 117);
        private static readonly Color CanvasAccentColor = Color.FromArgb(255, 132, 32);
        /// <summary>
        /// 选中节点输入连线和输入节点边框颜色。
        /// </summary>
        private static readonly Color RelationInputColor = Color.FromArgb(74, 163, 255);
        /// <summary>
        /// 选中节点输出连线和输出节点边框颜色。
        /// </summary>
        private static readonly Color RelationOutputColor = Color.FromArgb(255, 132, 32);
        /// <summary>
        /// 节点拖拽对齐辅助线颜色。
        /// </summary>
        private static readonly Color AlignmentGuideColor = Color.FromArgb(190, 92, 232, 132);
        private static readonly Color OverlayBackColor = Color.FromArgb(42, 44, 48);
        private static readonly Color OverlayBorderColor = Color.FromArgb(132, 136, 142);
        /// <summary>
        /// 节点拖拽时允许吸附到其它节点边线或中心线的最大距离。
        /// </summary>
        private const int AlignmentSnapDistance = 8;
        /// <summary>
        /// 对齐辅助线相对节点边界向外延伸的距离。
        /// </summary>
        private const int AlignmentGuidePadding = 36;
        /// <summary>
        /// 绘制可视区域时额外扩展的像素，避免阴影、锚点和连线端帽在边界处被裁掉。
        /// </summary>
        private const int ViewportDrawPadding = 96;
        /// <summary>
        /// 运行状态刷新合并间隔，避免流程运行时大量节点状态变化挤占拖动和缩放交互。
        /// </summary>
        private const int RuntimeInvalidationIntervalMs = 80;
        /// <summary>
        /// 用户正在拖动、滚动或缩放时延后运行状态重绘的时间，优先保证交互响应。
        /// </summary>
        private const int RuntimeInteractionDeferMs = 180;

        private Process _process;
        private NodeBase _dragNode;
        private Point _dragOffset;
        private NodeBase _connectionStartNode;
        private string _connectionStartAnchor;
        private Point _connectionEndPoint;
        private ProcessConnection _selectedConnection;
        /// <summary>
        /// 最近一次右键菜单命中的画布对象类型。
        /// </summary>
        private FlowCanvasContextMenuTarget _lastContextMenuTarget = FlowCanvasContextMenuTarget.Canvas;
        private Point _dragStartLocation;
        private List<NodeLocationSnapshot> _dragStartSnapshots = new List<NodeLocationSnapshot>();
        private bool _isSelectingNodes;
        private Point _selectionStartPoint;
        private Point _selectionEndPoint;
        private readonly Stack<ICanvasEdit> _undoStack = new Stack<ICanvasEdit>();
        private readonly Stack<ICanvasEdit> _redoStack = new Stack<ICanvasEdit>();
        private bool _isApplyingHistory;
        private float _zoomFactor = 1F;
        private bool _snapToGridEnabled;
        private Rectangle _imagePreviewBounds = Rectangle.Empty;
        private Rectangle _previewSelectorBounds = Rectangle.Empty;
        private Rectangle _previewImageDrawBounds = Rectangle.Empty;
        private Size _previewImagePixelSize = Size.Empty;
        private Point? _previewHoverImagePoint;
        private readonly ContextMenuStrip _previewImageMenu = new ContextMenuStrip();
        /// <summary>
        /// 当前拖拽命中的对齐辅助线集合。
        /// </summary>
        private readonly List<AlignmentGuide> _alignmentGuides = new List<AlignmentGuide>();
        /// <summary>
        /// 连线最近一次路由结果缓存，避免无关节点移动时牵动已有连线。
        /// </summary>
        private readonly Dictionary<string, ConnectionRouteCache> _connectionRouteCache = new Dictionary<string, ConnectionRouteCache>();
        /// <summary>
        /// 节点 ID 到节点实例的查找缓存，避免大量连线绘制时反复线性查找节点。
        /// </summary>
        private readonly Dictionary<int, NodeBase> _nodeLookup = new Dictionary<int, NodeBase>();
        /// <summary>
        /// 节点集合变化后标记节点查找缓存需要重建。
        /// </summary>
        private bool _nodeLookupDirty = true;
        /// <summary>
        /// 当前节点矩形障碍缓存，连线路由在同一帧内复用，减少重复分配和遍历。
        /// </summary>
        private readonly List<Rectangle> _connectionObstacleCache = new List<Rectangle>();
        /// <summary>
        /// 节点位置或节点集合变化后标记路由障碍缓存需要重建。
        /// </summary>
        private bool _connectionObstacleCacheDirty = true;
        /// <summary>
        /// 当前拖动批次中的节点 ID 集合，拖动期间复用以减少鼠标移动时的临时分配。
        /// </summary>
        private readonly HashSet<int> _draggingNodeIds = new HashSet<int>();
        /// <summary>
        /// 当前拖动批次会受影响的连线集合，用于局部刷新拖动节点相关的连线区域。
        /// </summary>
        private readonly List<ProcessConnection> _dragAffectedConnections = new List<ProcessConnection>();
        /// <summary>
        /// 当前拖动批次可用于对齐吸附的静态节点矩形，非拖动节点不会在拖动过程中变化。
        /// </summary>
        private readonly List<NodeAlignmentTarget> _alignmentTargets = new List<NodeAlignmentTarget>();
        /// <summary>
        /// 单选节点的直接输入节点编号集合，用于绘制输入关系高亮。
        /// </summary>
        private readonly HashSet<int> _selectedInputNodeIds = new HashSet<int>();
        /// <summary>
        /// 单选节点的直接输出节点编号集合，用于绘制输出关系高亮。
        /// </summary>
        private readonly HashSet<int> _selectedOutputNodeIds = new HashSet<int>();
        /// <summary>
        /// 指向单选节点的连线集合。
        /// </summary>
        private readonly HashSet<ProcessConnection> _selectedInputConnections = new HashSet<ProcessConnection>();
        /// <summary>
        /// 单选节点指向下游节点的连线集合。
        /// </summary>
        private readonly HashSet<ProcessConnection> _selectedOutputConnections = new HashSet<ProcessConnection>();
        private string _selectedPreviewImageKey;
        private string _cachedPreviewKey;
        private Mat _cachedPreviewMat;
        private Bitmap _cachedPreviewBitmap;
        private Rectangle _minimapBounds = Rectangle.Empty;
        private Rectangle _minimapCanvasBounds = Rectangle.Empty;
        /// <summary>
        /// 缩略图静态图层缓存，滚动和缩放时只重绘视口框，避免反复遍历全部节点和连线。
        /// </summary>
        private Bitmap _cachedMinimapBitmap;
        /// <summary>
        /// 缩略图缓存对应的控件尺寸。
        /// </summary>
        private Size _cachedMinimapBitmapSize = Size.Empty;
        /// <summary>
        /// 缩略图静态图层是否需要重新生成。
        /// </summary>
        private bool _minimapCacheDirty = true;
        /// <summary>
        /// 运行状态待刷新节点集合的同步锁，状态事件可能来自后台运行线程。
        /// </summary>
        private readonly object _runtimeInvalidationLock = new object();
        /// <summary>
        /// 等待合并刷新的运行状态节点 ID 集合。
        /// </summary>
        private readonly HashSet<int> _pendingRuntimeInvalidationNodeIds = new HashSet<int>();
        /// <summary>
        /// UI 线程上的运行状态刷新节流定时器。
        /// </summary>
        private readonly Timer _runtimeInvalidationTimer = new Timer();
        /// <summary>
        /// 标记是否已经投递过启动节流定时器的 UI 消息。
        /// </summary>
        private bool _runtimeInvalidationStartPosted;
        /// <summary>
        /// 运行状态刷新延后到此时间之后，避免运行过程中拖动和缩放被重绘抢占。
        /// </summary>
        private DateTime _runtimeInvalidationDeferredUntilUtc = DateTime.MinValue;

        private const float MinZoomFactor = 0.5F;
        private const float MaxZoomFactor = 2F;
        private const float ZoomStep = 0.1F;

        public static readonly Size DefaultCanvasNodeSize = new Size(240, 64);

        public event EventHandler HistoryChanged;
        public event EventHandler NodeCollectionChanged;
        public event EventHandler ZoomChanged;
        public event EventHandler<FlowCanvasOverlayInvalidatedEventArgs> OverlayInvalidated;

        public Point LastContextMenuCanvasPoint { get; private set; }

        /// <summary>
        /// 获取最近一次右键菜单命中的画布对象类型。
        /// </summary>
        public FlowCanvasContextMenuTarget LastContextMenuTarget
        {
            get { return _lastContextMenuTarget; }
        }

        public float ZoomFactor
        {
            get { return _zoomFactor; }
        }

        public bool SnapToGridEnabled
        {
            get { return _snapToGridEnabled; }
            set
            {
                if (_snapToGridEnabled == value)
                    return;

                _snapToGridEnabled = value;
                Invalidate();
            }
        }

        public ProcessFlowCanvas()
        {
            DoubleBuffered = true;
            TabStop = true;
            AllowDrop = true;
            AutoScroll = true;
            UpdateScaledCanvasSize();
            BackColor = CanvasBackColor;
            NodeBase.NodeStatusChanged += NodeBase_NodeStatusChanged;
            _runtimeInvalidationTimer.Interval = RuntimeInvalidationIntervalMs;
            _runtimeInvalidationTimer.Tick += RuntimeInvalidationTimer_Tick;
            _previewImageMenu.ShowImageMargin = false;
            _previewImageMenu.BackColor = Color.FromArgb(42, 44, 48);
            _previewImageMenu.ForeColor = Color.FromArgb(222, 229, 236);

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
        }

        public void BindProcess(Process process)
        {
            _process = process;
            _selectedConnection = null;
            _lastContextMenuTarget = FlowCanvasContextMenuTarget.Canvas;
            ClearSelectedRelationHighlights();
            _alignmentGuides.Clear();
            _connectionRouteCache.Clear();
            InvalidateCanvasTopologyCaches();
            ClearPendingRuntimeInvalidations();
            ClearDragInteractionCache();
            _selectedPreviewImageKey = null;
            DisposePreviewCache();
            ClearHistory();
            Invalidate();
            RequestOverlayInvalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NodeBase.NodeStatusChanged -= NodeBase_NodeStatusChanged;
                _runtimeInvalidationTimer.Stop();
                _runtimeInvalidationTimer.Tick -= RuntimeInvalidationTimer_Tick;
                _runtimeInvalidationTimer.Dispose();
                _previewImageMenu.Dispose();
                DisposePreviewCache();
                DisposeMinimapCache();
            }

            base.Dispose(disposing);
        }

        private void NodeBase_NodeStatusChanged(object sender, NodeBase node)
        {
            if (_process == null ||
                node == null ||
                !ReferenceEquals(node.Process, _process) ||
                IsDisposed ||
                Disposing)
                return;

            QueueRuntimeInvalidation(node.ID);
        }

        /// <summary>
        /// 将节点运行状态刷新加入待处理队列，由定时器批量刷新画布小区域。
        /// </summary>
        /// <param name="nodeId">运行状态变化的节点 ID。</param>
        private void QueueRuntimeInvalidation(int nodeId)
        {
            bool shouldPostStart = false;
            lock (_runtimeInvalidationLock)
            {
                _pendingRuntimeInvalidationNodeIds.Add(nodeId);
                if (!_runtimeInvalidationStartPosted)
                {
                    _runtimeInvalidationStartPosted = true;
                    shouldPostStart = true;
                }
            }

            if (!shouldPostStart)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((MethodInvoker)StartRuntimeInvalidationTimer);
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            StartRuntimeInvalidationTimer();
        }

        /// <summary>
        /// 在 UI 线程启动运行状态刷新节流定时器。
        /// </summary>
        private void StartRuntimeInvalidationTimer()
        {
            lock (_runtimeInvalidationLock)
                _runtimeInvalidationStartPosted = false;

            if (IsDisposed || Disposing)
                return;

            if (!_runtimeInvalidationTimer.Enabled)
                _runtimeInvalidationTimer.Start();
        }

        /// <summary>
        /// 定时批量刷新运行状态变化。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void RuntimeInvalidationTimer_Tick(object sender, EventArgs e)
        {
            FlushRuntimeInvalidations();
        }

        /// <summary>
        /// 合并本批运行状态变化，只重绘受影响的节点区域。
        /// </summary>
        private void FlushRuntimeInvalidations()
        {
            List<int> nodeIds;
            lock (_runtimeInvalidationLock)
            {
                if (_pendingRuntimeInvalidationNodeIds.Count == 0)
                {
                    _runtimeInvalidationTimer.Stop();
                    return;
                }
            }

            if (ShouldDeferRuntimeInvalidations())
                return;

            lock (_runtimeInvalidationLock)
            {
                if (_pendingRuntimeInvalidationNodeIds.Count == 0)
                {
                    _runtimeInvalidationTimer.Stop();
                    return;
                }

                nodeIds = _pendingRuntimeInvalidationNodeIds.ToList();
                _pendingRuntimeInvalidationNodeIds.Clear();
            }

            EnsureNodeLookup();
            Rectangle invalidationBounds = Rectangle.Empty;
            bool shouldRefreshPreview = false;
            bool previewOverlayVisible = !GetImagePreviewOverlayBounds(ClientSize).IsEmpty;
            NodeBase selectedNode = SelectedNode;
            foreach (int nodeId in nodeIds)
            {
                NodeBase node = FindNode(nodeId);
                if (node == null)
                    continue;

                IncludeCanvasBounds(ref invalidationBounds, GetNodeVisualBounds(node));
                if (!shouldRefreshPreview &&
                    ShouldRefreshPreviewForRuntimeNode(node, selectedNode, previewOverlayVisible))
                {
                    shouldRefreshPreview = true;
                }
            }

            InvalidateCanvasBounds(invalidationBounds);
            if (shouldRefreshPreview)
            {
                DisposePreviewCache();
                RequestOverlayInvalidate(true, false);
            }

            lock (_runtimeInvalidationLock)
            {
                if (_pendingRuntimeInvalidationNodeIds.Count == 0)
                    _runtimeInvalidationTimer.Stop();
            }
        }

        /// <summary>
        /// 清理尚未处理的运行状态刷新请求。
        /// </summary>
        private void ClearPendingRuntimeInvalidations()
        {
            lock (_runtimeInvalidationLock)
            {
                _pendingRuntimeInvalidationNodeIds.Clear();
                _runtimeInvalidationStartPosted = false;
            }

            _runtimeInvalidationTimer.Stop();
        }

        /// <summary>
        /// 当前正在编辑视口时延后运行状态重绘，减少运行线程和拖动画布之间的刷新竞争。
        /// </summary>
        private void DeferRuntimeInvalidationsForInteraction()
        {
            if (_process == null || !_process.IsRuning)
                return;

            _runtimeInvalidationDeferredUntilUtc = DateTime.UtcNow.AddMilliseconds(RuntimeInteractionDeferMs);
        }

        /// <summary>
        /// 判断本次运行状态刷新是否应等待交互空闲后再执行。
        /// </summary>
        /// <returns>需要延后刷新时返回 true。</returns>
        private bool ShouldDeferRuntimeInvalidations()
        {
            return _dragNode != null ||
                _isSelectingNodes ||
                _connectionStartNode != null ||
                DateTime.UtcNow < _runtimeInvalidationDeferredUntilUtc;
        }

        /// <summary>
        /// 判断指定节点的运行结果变化是否需要刷新图像预览层。
        /// </summary>
        /// <param name="node">运行状态变化的节点。</param>
        /// <param name="selectedNode">当前选中的节点。</param>
        /// <param name="previewOverlayVisible">图像预览层当前是否可见。</param>
        /// <returns>需要刷新图像预览层时返回 true。</returns>
        private bool ShouldRefreshPreviewForRuntimeNode(NodeBase node, NodeBase selectedNode, bool previewOverlayVisible)
        {
            if (node == null || !previewOverlayVisible)
                return false;

            string nodeKeyPrefix = node.ID + ":";
            if (!string.IsNullOrEmpty(_selectedPreviewImageKey) &&
                _selectedPreviewImageKey.StartsWith(nodeKeyPrefix, StringComparison.Ordinal))
            {
                return HasPreviewImage(node);
            }

            if (ReferenceEquals(selectedNode, node))
                return HasPreviewImage(node);

            if (selectedNode != null)
                return false;

            if (!string.IsNullOrEmpty(_cachedPreviewKey))
            {
                return _cachedPreviewKey.StartsWith(nodeKeyPrefix, StringComparison.Ordinal) &&
                    HasPreviewImage(node);
            }

            return HasPreviewImage(node);
        }

        public NodeBase SelectedNode
        {
            get
            {
                if (_process == null)
                    return null;

                return _process.Nodes.Find(node => node.Selected);
            }
        }

        public List<NodeBase> SelectedNodes
        {
            get
            {
                List<NodeBase> selectedNodes = new List<NodeBase>();
                if (_process == null)
                    return selectedNodes;

                foreach (NodeBase node in _process.Nodes)
                {
                    if (node.Selected)
                        selectedNodes.Add(node);
                }

                return selectedNodes;
            }
        }

        public int SelectedNodeCount
        {
            get { return SelectedNodes.Count; }
        }

        public ProcessConnection SelectedConnection
        {
            get { return _selectedConnection; }
        }

        public bool CanUndo
        {
            get { return _undoStack.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _redoStack.Count > 0; }
        }

        public Point GetDefaultNodeLocation(int index)
        {
            int column = index / 8;
            int row = index % 8;
            return new Point(48 + (column * 300), 48 + (row * 110));
        }

        public Point ScreenToCanvasPoint(Point screenPoint)
        {
            return ToCanvasPoint(PointToClient(screenPoint));
        }

        public Point GetInteractiveNodeLocation(Point requestedLocation, Size size)
        {
            Size nodeSize = size.Width <= 0 || size.Height <= 0
                ? DefaultCanvasNodeSize
                : size;
            Point location = _snapToGridEnabled
                ? SnapPointToGrid(requestedLocation)
                : requestedLocation;

            return ClampLocation(location, nodeSize);
        }

        public bool ZoomIn()
        {
            return SetZoom(_zoomFactor + ZoomStep, GetViewportCenterPoint(), new Point(ClientSize.Width / 2, ClientSize.Height / 2));
        }

        public bool ZoomOut()
        {
            return SetZoom(_zoomFactor - ZoomStep, GetViewportCenterPoint(), new Point(ClientSize.Width / 2, ClientSize.Height / 2));
        }

        public bool ResetZoom()
        {
            return SetZoom(1F, GetViewportCenterPoint(), new Point(ClientSize.Width / 2, ClientSize.Height / 2));
        }

        public static Rectangle GetImagePreviewOverlayBounds(Size viewportSize)
        {
            if (viewportSize.Width < 720 || viewportSize.Height < 420)
                return Rectangle.Empty;

            int width = Math.Min(430, Math.Max(320, viewportSize.Width / 5));
            int height = Math.Min(260, Math.Max(210, viewportSize.Height / 4));
            return new Rectangle(
                viewportSize.Width - width - 24,
                24,
                width,
                height);
        }

        public static Rectangle GetMinimapOverlayBounds(Size viewportSize)
        {
            if (viewportSize.Width < 640 || viewportSize.Height < 460)
                return Rectangle.Empty;

            int width = Math.Min(260, Math.Max(210, viewportSize.Width / 8));
            int height = Math.Min(170, Math.Max(130, viewportSize.Height / 6));
            return new Rectangle(
                viewportSize.Width - width - 24,
                viewportSize.Height - height - 24,
                width,
                height);
        }

        public void AddNode(NodeBase node, Point? requestedLocation = null)
        {
            if (node == null)
                return;

            if (node.CanvasSize.Width <= 0 || node.CanvasSize.Height <= 0)
                node.CanvasSize = DefaultCanvasNodeSize;

            if (requestedLocation.HasValue)
                node.CanvasLocation = ClampLocation(requestedLocation.Value, node.CanvasSize);
            else if (node.CanvasLocation == Point.Empty)
                node.CanvasLocation = GetDefaultNodeLocation(GetNodeIndex(node));

            InvalidateCanvasTopologyCaches();
            Invalidate();
            RequestOverlayInvalidate();
        }

        public void RegisterNodeCreated(NodeBase node)
        {
            if (_process == null || node == null || _isApplyingHistory)
                return;

            PushHistory(new NodeAddedEdit(
                node,
                _process.Nodes.IndexOf(node),
                Solution.Instance.Nodes.IndexOf(node)));
            OnNodeCollectionChanged();
        }

        public void RegisterNodesCreated(List<NodeBase> nodes, List<ProcessConnection> connections)
        {
            if (_process == null || nodes == null || nodes.Count == 0 || _isApplyingHistory)
                return;

            PushHistory(new NodesAddedEdit(this, nodes, connections));
            OnNodeCollectionChanged();
        }

        public void SelectNodes(List<NodeBase> selectedNodes)
        {
            if (_process == null)
                return;

            _selectedConnection = null;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = selectedNodes != null && selectedNodes.Contains(node);

            RefreshSelectedRelationHighlights();
            InvalidateMinimapCache();
            Invalidate();
            RequestOverlayInvalidate();
        }

        public void RefreshNodes()
        {
            InvalidateCanvasTopologyCaches();
            Invalidate();
            RequestOverlayInvalidate();
        }

        /// <summary>
        /// 立即刷新流程运行状态显示，用于手动运行开始或结束后兜底刷新节点颜色和耗时。
        /// </summary>
        public void RefreshRuntimeStateNow()
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((MethodInvoker)RefreshRuntimeStateNow);
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            _runtimeInvalidationDeferredUntilUtc = DateTime.MinValue;
            FlushRuntimeInvalidations();
            Invalidate();
            RequestOverlayInvalidate();
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            ICanvasEdit edit = _undoStack.Pop();
            _isApplyingHistory = true;
            try
            {
                edit.Undo(this);
            }
            finally
            {
                _isApplyingHistory = false;
            }

            _redoStack.Push(edit);
            OnHistoryChanged();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            ICanvasEdit edit = _redoStack.Pop();
            _isApplyingHistory = true;
            try
            {
                edit.Redo(this);
            }
            finally
            {
                _isApplyingHistory = false;
            }

            _undoStack.Push(edit);
            OnHistoryChanged();
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            using (SolidBrush backgroundBrush = new SolidBrush(CanvasBackColor))
                graphics.FillRectangle(backgroundBrush, e.ClipRectangle);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            graphics.ScaleTransform(_zoomFactor, _zoomFactor);

            EnsureNodeLookup();
            Rectangle visibleCanvasBounds = GetCanvasBoundsFromControlRectangle(e.ClipRectangle);
            DrawGrid(graphics, visibleCanvasBounds);

            if (_process != null)
            {
                foreach (ProcessConnection connection in _process.Connections)
                {
                    NodeBase fromNode = FindNode(connection.FromNodeId);
                    NodeBase toNode = FindNode(connection.ToNodeId);
                    if (fromNode != null &&
                        toNode != null &&
                        ShouldDrawConnection(connection, fromNode, toNode, visibleCanvasBounds))
                    {
                        DrawConnection(graphics, connection, fromNode, toNode);
                    }
                }

                if (_connectionStartNode != null)
                {
                    Rectangle temporaryBounds = GetTemporaryConnectionVisualBounds();
                    if (temporaryBounds.IsEmpty || temporaryBounds.IntersectsWith(visibleCanvasBounds))
                        DrawTemporaryConnection(graphics);
                }

                foreach (NodeBase node in _process.Nodes)
                {
                    if (ShouldDrawNode(node, visibleCanvasBounds))
                        DrawNode(graphics, node);
                }

                if (_alignmentGuides.Count > 0)
                    DrawAlignmentGuides(graphics);

                if (_isSelectingNodes)
                    DrawSelectionRectangle(graphics);
            }

            graphics.ResetTransform();
            DrawZoomBadge(graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_process == null)
                return;

            Focus();
            Point canvasPoint = ToCanvasPoint(e.Location);

            if (e.Button == MouseButtons.Right)
            {
                ClearDragInteractionCache();
                LastContextMenuCanvasPoint = canvasPoint;
                _lastContextMenuTarget = SelectAtPoint(canvasPoint, true);
                _alignmentGuides.Clear();
                Invalidate();
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            Capture = true;
            NodeBase anchorNode;
            string anchorName;
            if (TryHitAnchor(canvasPoint, out anchorNode, out anchorName))
            {
                ClearDragInteractionCache();
                SelectOnly(anchorNode);
                _alignmentGuides.Clear();
                _connectionStartNode = anchorNode;
                _connectionStartAnchor = anchorName;
                _connectionEndPoint = canvasPoint;
                Invalidate();
                return;
            }

            NodeBase node = HitNode(canvasPoint);
            _alignmentGuides.Clear();
            if (node != null)
            {
                if (!node.Selected)
                    SelectOnly(node);

                _dragNode = node;
                _dragStartLocation = node.CanvasLocation;
                _dragStartSnapshots = CaptureSelectedNodeLocations();
                _dragOffset = new Point(
                    canvasPoint.X - node.CanvasLocation.X,
                    canvasPoint.Y - node.CanvasLocation.Y);
                PrepareDragInteractionCache();
                EnsureConnectionRoutesCached();
            }
            else
            {
                ClearDragInteractionCache();
                ProcessConnection connection = HitConnection(canvasPoint);
                if (connection != null)
                    SelectOnly(connection);
                else
                {
                    ClearSelection();
                    _isSelectingNodes = true;
                    _selectionStartPoint = canvasPoint;
                    _selectionEndPoint = canvasPoint;
                }
            }

            Invalidate();
            RequestOverlayInvalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_process == null)
                return;

            Point canvasPoint = ToCanvasPoint(e.Location);
            if (_dragNode != null && e.Button == MouseButtons.Left)
            {
                DeferRuntimeInvalidationsForInteraction();
                Point location = new Point(
                    canvasPoint.X - _dragOffset.X,
                    canvasPoint.Y - _dragOffset.Y);
                Rectangle oldBounds = GetDragInvalidationBounds();
                if (MoveDraggedNodes(location))
                {
                    Rectangle newBounds = GetDragInvalidationBounds();
                    InvalidateCanvasBounds(UnionCanvasBounds(oldBounds, newBounds));
                }
                return;
            }

            if (_isSelectingNodes && e.Button == MouseButtons.Left)
            {
                DeferRuntimeInvalidationsForInteraction();
                Rectangle oldBounds = GetSelectionInvalidationBounds();
                _selectionEndPoint = canvasPoint;
                Rectangle changedBounds = SelectNodesInRectangle(GetSelectionRectangle());
                Rectangle newBounds = GetSelectionInvalidationBounds();
                InvalidateCanvasBounds(UnionCanvasBounds(UnionCanvasBounds(oldBounds, newBounds), changedBounds));
                return;
            }

            if (_connectionStartNode != null)
            {
                DeferRuntimeInvalidationsForInteraction();
                Rectangle oldBounds = GetTemporaryConnectionVisualBounds();
                _connectionEndPoint = canvasPoint;
                Rectangle newBounds = GetTemporaryConnectionVisualBounds();
                InvalidateCanvasBounds(UnionCanvasBounds(oldBounds, newBounds));
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Left)
                Capture = false;

            if (_process == null || e.Button != MouseButtons.Left)
                return;

            if (_isSelectingNodes)
            {
                _selectionEndPoint = ToCanvasPoint(e.Location);
                SelectNodesInRectangle(GetSelectionRectangle());
                _isSelectingNodes = false;
                _alignmentGuides.Clear();
                ClearDragInteractionCache();
                Invalidate();
                RequestOverlayInvalidate();
                return;
            }

            NodeBase movedNode = _dragNode;
            List<NodeLocationSnapshot> oldLocations = _dragStartSnapshots;
            _dragNode = null;
            _dragStartSnapshots = new List<NodeLocationSnapshot>();
            ClearDragInteractionCache();
            _alignmentGuides.Clear();
            Invalidate();
            if (movedNode != null)
            {
                PushMoveHistory(oldLocations);
                RequestOverlayInvalidate(false, true);
            }

            if (_connectionStartNode != null)
            {
                Point canvasPoint = ToCanvasPoint(e.Location);
                NodeBase targetNode;
                string targetAnchor;
                ProcessConnectionBranch branch = GetConnectionBranch(_connectionStartNode, _connectionStartAnchor);
                if (TryHitAnchor(canvasPoint, out targetNode, out targetAnchor) &&
                    targetNode != _connectionStartNode &&
                    !HasConnection(_connectionStartNode.ID, targetNode.ID, branch))
                {
                    ProcessConnection connection = new ProcessConnection
                    {
                        FromNodeId = _connectionStartNode.ID,
                        ToNodeId = targetNode.ID,
                        FromAnchor = _connectionStartAnchor,
                        ToAnchor = targetAnchor,
                        Branch = branch
                    };
                    ExecuteEdit(new ConnectionAddedEdit(connection, _process.Connections.Count));
                }

                _connectionStartNode = null;
                _connectionStartAnchor = null;
                Invalidate();
                RequestOverlayInvalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (_process == null || e.Button != MouseButtons.Left)
                return;

            NodeBase node = HitNode(ToCanvasPoint(e.Location));
            if (node != null)
            {
                SelectOnly(node);
                OpenNodeParameters(node);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                Point canvasPoint = ToCanvasPoint(e.Location);
                if (e.Delta > 0)
                    SetZoom(_zoomFactor + ZoomStep, canvasPoint, e.Location);
                else if (e.Delta < 0)
                    SetZoom(_zoomFactor - ZoomStep, canvasPoint, e.Location);

                return;
            }

            base.OnMouseWheel(e);
            Invalidate();
            RequestOverlayInvalidate(false, true);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            DeferRuntimeInvalidationsForInteraction();
            base.OnScroll(se);
            Invalidate();
            RequestOverlayInvalidate(false, true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode != Keys.Delete)
                return;

            if (DeleteSelectedConnection() || DeleteSelectedNode())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        public bool OpenSelectedNodeParameters()
        {
            return OpenNodeParameters(SelectedNode);
        }

        public bool DeleteSelectedNode()
        {
            List<NodeBase> nodes = SelectedNodes;
            if (nodes.Count == 0)
                return false;

            return ExecuteEdit(new NodesDeletedEdit(this, nodes));
        }

        public bool DeleteSelectedConnection()
        {
            if (_process == null || _selectedConnection == null)
                return false;

            return ExecuteEdit(new ConnectionDeletedEdit(
                _selectedConnection,
                _process.Connections.IndexOf(_selectedConnection)));
        }

        public bool SetSelectedNodeAsStart()
        {
            NodeBase node = SelectedNode;
            if (_process == null || node == null)
                return false;

            return ExecuteEdit(new StartNodeChangedEdit(CaptureStartStates(), node));
        }

        public bool EnableSelectedNode()
        {
            List<NodeBase> nodes = SelectedNodes;
            if (nodes.Count == 0)
                return false;

            return ExecuteEdit(new NodeActiveChangedEdit(nodes, true));
        }

        public bool DisableSelectedNode()
        {
            List<NodeBase> nodes = SelectedNodes;
            if (nodes.Count == 0)
                return false;

            return ExecuteEdit(new NodeActiveChangedEdit(nodes, false));
        }

        public bool RenameSelectedNode()
        {
            NodeBase node = SelectedNode;
            if (node == null)
                return false;

            node.ShowRenameDialog();
            Invalidate();
            RequestOverlayInvalidate();
            return true;
        }

        public bool EditSelectedNodeNotes()
        {
            NodeBase node = SelectedNode;
            if (node == null)
                return false;

            node.ShowNotesDialog();
            Invalidate();
            RequestOverlayInvalidate();
            return true;
        }

        private bool ExecuteEdit(ICanvasEdit edit)
        {
            if (edit == null)
                return false;

            _isApplyingHistory = true;
            try
            {
                if (!edit.Redo(this))
                    return false;
            }
            finally
            {
                _isApplyingHistory = false;
            }

            PushHistory(edit);
            return true;
        }

        private void PushHistory(ICanvasEdit edit)
        {
            if (edit == null || _isApplyingHistory)
                return;

            _undoStack.Push(edit);
            _redoStack.Clear();
            OnHistoryChanged();
        }

        private void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnHistoryChanged();
        }

        private void OnHistoryChanged()
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnNodeCollectionChanged()
        {
            InvalidateCanvasTopologyCaches();
            NodeCollectionChanged?.Invoke(this, EventArgs.Empty);
            RequestOverlayInvalidate();
        }

        /// <summary>
        /// 标记节点集合相关缓存失效，节点增删后需要重新生成查找表、障碍表和连线路由。
        /// </summary>
        private void InvalidateCanvasTopologyCaches()
        {
            _nodeLookupDirty = true;
            _connectionObstacleCacheDirty = true;
            _connectionRouteCache.Clear();
            RefreshSelectedRelationHighlights();
            InvalidateMinimapCache();
        }

        /// <summary>
        /// 清空当前选中节点的输入输出关系高亮缓存。
        /// </summary>
        private void ClearSelectedRelationHighlights()
        {
            _selectedInputNodeIds.Clear();
            _selectedOutputNodeIds.Clear();
            _selectedInputConnections.Clear();
            _selectedOutputConnections.Clear();
        }

        /// <summary>
        /// 根据当前单选节点刷新直接输入、直接输出关系缓存。
        /// </summary>
        private void RefreshSelectedRelationHighlights()
        {
            ClearSelectedRelationHighlights();
            if (_process == null)
                return;

            NodeBase selectedNode = null;
            int selectedCount = 0;
            foreach (NodeBase node in _process.Nodes)
            {
                if (node == null || !node.Selected)
                    continue;

                selectedNode = node;
                selectedCount++;
                if (selectedCount > 1)
                    return;
            }

            if (selectedNode == null || selectedCount != 1)
                return;

            foreach (ProcessConnection connection in _process.Connections)
            {
                if (connection == null)
                    continue;

                if (connection.ToNodeId == selectedNode.ID)
                {
                    _selectedInputNodeIds.Add(connection.FromNodeId);
                    _selectedInputConnections.Add(connection);
                }

                if (connection.FromNodeId == selectedNode.ID)
                {
                    _selectedOutputNodeIds.Add(connection.ToNodeId);
                    _selectedOutputConnections.Add(connection);
                }
            }
        }

        /// <summary>
        /// 标记节点位置相关的路由障碍缓存失效。
        /// </summary>
        private void InvalidateConnectionObstacleCache()
        {
            _connectionObstacleCacheDirty = true;
            InvalidateMinimapCache();
        }

        private void OnZoomChanged()
        {
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 请求两个悬浮层都刷新。
        /// </summary>
        private void RequestOverlayInvalidate()
        {
            RequestOverlayInvalidate(true, true);
        }

        /// <summary>
        /// 按需请求刷新图像预览层和缩略图层，减少滚动缩放时的无关重绘。
        /// </summary>
        /// <param name="invalidateImagePreview">是否刷新右上角图像预览层。</param>
        /// <param name="invalidateMinimap">是否刷新右下角缩略图层。</param>
        private void RequestOverlayInvalidate(bool invalidateImagePreview, bool invalidateMinimap)
        {
            if (!invalidateImagePreview && !invalidateMinimap)
                return;

            OverlayInvalidated?.Invoke(
                this,
                new FlowCanvasOverlayInvalidatedEventArgs(invalidateImagePreview, invalidateMinimap));
        }

        /// <summary>
        /// 标记缩略图静态图层失效，下次绘制时重新生成。
        /// </summary>
        private void InvalidateMinimapCache()
        {
            _minimapCacheDirty = true;
        }

        /// <summary>
        /// 释放缩略图缓存位图。
        /// </summary>
        private void DisposeMinimapCache()
        {
            if (_cachedMinimapBitmap != null)
            {
                _cachedMinimapBitmap.Dispose();
                _cachedMinimapBitmap = null;
            }

            _cachedMinimapBitmapSize = Size.Empty;
            _minimapCacheDirty = true;
        }

        private bool SetZoom(float zoomFactor, Point anchorCanvasPoint, Point anchorControlPoint)
        {
            float nextZoom = Math.Max(MinZoomFactor, Math.Min(MaxZoomFactor, zoomFactor));
            if (Math.Abs(nextZoom - _zoomFactor) < 0.001F)
                return false;

            DeferRuntimeInvalidationsForInteraction();
            _zoomFactor = nextZoom;
            UpdateScaledCanvasSize();
            SetScrollPositionForAnchor(anchorCanvasPoint, anchorControlPoint);
            Invalidate();
            RequestOverlayInvalidate(false, true);
            OnZoomChanged();
            return true;
        }

        private void UpdateScaledCanvasSize()
        {
            AutoScrollMinSize = new Size(
                (int)Math.Ceiling(LogicalCanvasSize.Width * _zoomFactor),
                (int)Math.Ceiling(LogicalCanvasSize.Height * _zoomFactor));
        }

        private Point GetViewportCenterPoint()
        {
            return ToCanvasPoint(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
        }

        private void SetScrollPositionForAnchor(Point anchorCanvasPoint, Point anchorControlPoint)
        {
            int targetX = Math.Max(0, (int)Math.Round(anchorCanvasPoint.X * _zoomFactor) - anchorControlPoint.X);
            int targetY = Math.Max(0, (int)Math.Round(anchorCanvasPoint.Y * _zoomFactor) - anchorControlPoint.Y);
            AutoScrollPosition = new Point(targetX, targetY);
        }

        private List<NodeStartState> CaptureStartStates()
        {
            List<NodeStartState> states = new List<NodeStartState>();
            if (_process == null)
                return states;

            foreach (NodeBase node in _process.Nodes)
                states.Add(new NodeStartState(node, node.IsStartNode));

            return states;
        }

        private List<ConnectionSnapshot> CaptureConnectionsForNode(NodeBase node)
        {
            List<ConnectionSnapshot> snapshots = new List<ConnectionSnapshot>();
            if (_process == null || node == null)
                return snapshots;

            for (int index = 0; index < _process.Connections.Count; index++)
            {
                ProcessConnection connection = _process.Connections[index];
                if (connection.FromNodeId == node.ID || connection.ToNodeId == node.ID)
                    snapshots.Add(new ConnectionSnapshot(connection, index));
            }

            return snapshots;
        }

        private bool DetachNode(NodeBase node)
        {
            if (_process == null || node == null || !_process.Nodes.Contains(node))
                return false;

            SelectOnly(node);
            bool removed = node.DeleteFromProcess(false);
            _selectedConnection = null;
            OnNodeCollectionChanged();
            Invalidate();
            return removed;
        }

        private void RestoreNode(
            NodeBase node,
            int processIndex,
            int solutionIndex,
            List<ConnectionSnapshot> connectionSnapshots)
        {
            if (_process == null || node == null)
                return;

            if (!_process.Nodes.Contains(node))
            {
                int insertIndex = Math.Max(0, Math.Min(processIndex, _process.Nodes.Count));
                _process.Nodes.Insert(insertIndex, node);
            }

            if (!Solution.Instance.Nodes.Contains(node))
            {
                int insertIndex = Math.Max(0, Math.Min(solutionIndex, Solution.Instance.Nodes.Count));
                Solution.Instance.Nodes.Insert(insertIndex, node);
            }

            RestoreConnections(connectionSnapshots);
            SelectOnly(node);
            OnNodeCollectionChanged();
            Invalidate();
        }

        private bool AddConnection(ProcessConnection connection, int index)
        {
            if (_process == null ||
                connection == null ||
                _process.Connections.Contains(connection) ||
                FindNode(connection.FromNodeId) == null ||
                FindNode(connection.ToNodeId) == null)
            {
                return false;
            }

            int insertIndex = Math.Max(0, Math.Min(index, _process.Connections.Count));
            _process.Connections.Insert(insertIndex, connection);
            _process.NormalizeConnectionBranch(connection);
            _process.HasCanvasGraph = true;
            _process.NotifyConnectionsChanged();
            RemoveConnectionRouteCache(connection);
            InvalidateMinimapCache();
            SelectOnly(connection);
            Invalidate();
            RequestOverlayInvalidate();
            return true;
        }

        private bool RemoveConnection(ProcessConnection connection)
        {
            if (_process == null || connection == null)
                return false;

            bool removed = _process.Connections.Remove(connection);
            if (!removed)
                return false;

            _selectedConnection = null;
            RefreshSelectedRelationHighlights();
            RemoveConnectionRouteCache(connection);
            _process.NotifyConnectionsChanged();
            InvalidateMinimapCache();
            Invalidate();
            RequestOverlayInvalidate();
            return true;
        }

        private void RestoreConnections(List<ConnectionSnapshot> snapshots)
        {
            if (_process == null || snapshots == null)
                return;

            foreach (ConnectionSnapshot snapshot in snapshots)
            {
                if (snapshot.Connection == null ||
                    _process.Connections.Contains(snapshot.Connection) ||
                    FindNode(snapshot.Connection.FromNodeId) == null ||
                    FindNode(snapshot.Connection.ToNodeId) == null)
                {
                    continue;
                }

                int insertIndex = Math.Max(0, Math.Min(snapshot.Index, _process.Connections.Count));
                _process.Connections.Insert(insertIndex, snapshot.Connection);
                RemoveConnectionRouteCache(snapshot.Connection);
            }

            _process.NotifyConnectionsChanged();
            RefreshSelectedRelationHighlights();
            InvalidateMinimapCache();
            RequestOverlayInvalidate();
        }

        private void SetNodeLocation(NodeBase node, Point location)
        {
            if (node == null)
                return;

            node.CanvasLocation = ClampLocation(location, GetNodeSize(node));
            InvalidateConnectionObstacleCache();
            Invalidate();
            RequestOverlayInvalidate();
        }

        private void SetNodeActive(NodeBase node, bool active)
        {
            if (node == null)
                return;

            if (active)
                node.EnableNode();
            else
                node.DisableNode();

            InvalidateMinimapCache();
            InvalidateNode(node);
            RequestOverlayInvalidate();
        }

        private void ApplyStartStates(List<NodeStartState> states)
        {
            if (states == null)
                return;

            foreach (NodeStartState state in states)
            {
                if (state.Node != null)
                    state.Node.IsStartNode = state.IsStartNode;
            }

            Invalidate();
            RequestOverlayInvalidate();
        }

        private void SetOnlyStartNode(NodeBase node)
        {
            if (_process == null || node == null)
                return;

            foreach (NodeBase processNode in _process.Nodes)
                processNode.IsStartNode = processNode == node;

            Invalidate();
            RequestOverlayInvalidate();
        }

        private List<NodeLocationSnapshot> CaptureSelectedNodeLocations()
        {
            List<NodeLocationSnapshot> snapshots = new List<NodeLocationSnapshot>();
            if (_process == null)
                return snapshots;

            foreach (NodeBase node in _process.Nodes)
            {
                if (node.Selected)
                    snapshots.Add(new NodeLocationSnapshot(node, node.CanvasLocation));
            }

            return snapshots;
        }

        /// <summary>
        /// 获取当前拖拽批次中会跟随移动的节点 ID。
        /// </summary>
        /// <returns>节点 ID 集合。</returns>
        private HashSet<int> GetDraggingNodeIds()
        {
            HashSet<int> nodeIds = new HashSet<int>();
            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                if (snapshot.Node != null)
                    nodeIds.Add(snapshot.Node.ID);
            }

            return nodeIds;
        }

        /// <summary>
        /// 预生成拖动过程需要复用的节点 ID、相关连线和对齐目标缓存。
        /// </summary>
        private void PrepareDragInteractionCache()
        {
            ClearDragInteractionCache();
            if (_process == null)
                return;

            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                if (snapshot.Node != null)
                    _draggingNodeIds.Add(snapshot.Node.ID);
            }

            foreach (ProcessConnection connection in _process.Connections)
            {
                if (_draggingNodeIds.Contains(connection.FromNodeId) ||
                    _draggingNodeIds.Contains(connection.ToNodeId))
                {
                    _dragAffectedConnections.Add(connection);
                }
            }

            foreach (NodeBase node in _process.Nodes)
            {
                if (node != null && !_draggingNodeIds.Contains(node.ID))
                    _alignmentTargets.Add(new NodeAlignmentTarget(GetNodeBounds(node)));
            }
        }

        /// <summary>
        /// 清理拖动交互缓存，结束拖动或切换交互模式时调用。
        /// </summary>
        private void ClearDragInteractionCache()
        {
            _draggingNodeIds.Clear();
            _dragAffectedConnections.Clear();
            _alignmentTargets.Clear();
        }

        /// <summary>
        /// 在网格吸附基础上叠加节点对齐吸附，并记录需要绘制的辅助虚线。
        /// </summary>
        /// <param name="requestedLocation">鼠标拖拽换算出的节点目标位置。</param>
        /// <param name="draggingNodeIds">当前拖拽批次中的节点 ID。</param>
        /// <returns>应用吸附后的拖拽节点位置。</returns>
        private Point GetAlignedDragNodeLocation(Point requestedLocation, HashSet<int> draggingNodeIds)
        {
            _alignmentGuides.Clear();
            if (_dragNode == null)
                return requestedLocation;

            Size dragNodeSize = GetNodeSize(_dragNode);
            Point baseLocation = _snapToGridEnabled
                ? SnapPointToGrid(requestedLocation)
                : requestedLocation;
            baseLocation = ClampLocation(baseLocation, dragNodeSize);

            AlignmentCandidate horizontalCandidate = null;
            AlignmentCandidate verticalCandidate = null;
            Rectangle proposedBounds = new Rectangle(baseLocation, dragNodeSize);
            if (_alignmentTargets.Count > 0)
            {
                foreach (NodeAlignmentTarget target in _alignmentTargets)
                {
                    TryUpdateAlignmentCandidatesForBounds(
                        target.Bounds,
                        proposedBounds,
                        dragNodeSize,
                        ref horizontalCandidate,
                        ref verticalCandidate);
                }
            }
            else
            {
                foreach (NodeBase node in _process.Nodes)
                {
                    if (node == null || draggingNodeIds.Contains(node.ID))
                        continue;

                    TryUpdateAlignmentCandidatesForBounds(
                        GetNodeBounds(node),
                        proposedBounds,
                        dragNodeSize,
                        ref horizontalCandidate,
                        ref verticalCandidate);
                }
            }

            Point alignedLocation = baseLocation;
            if (horizontalCandidate != null)
                alignedLocation.Y = horizontalCandidate.NewLocationCoordinate;

            if (verticalCandidate != null)
                alignedLocation.X = verticalCandidate.NewLocationCoordinate;

            alignedLocation = ClampLocation(alignedLocation, dragNodeSize);
            Rectangle alignedBounds = new Rectangle(alignedLocation, dragNodeSize);
            AddAlignmentGuide(horizontalCandidate, alignedBounds, draggingNodeIds);
            AddAlignmentGuide(verticalCandidate, alignedBounds, draggingNodeIds);
            return alignedLocation;
        }

        /// <summary>
        /// 将单个参考节点矩形转换为水平和垂直方向的对齐候选。
        /// </summary>
        /// <param name="targetBounds">参考节点边界。</param>
        /// <param name="proposedBounds">拖动节点当前候选边界。</param>
        /// <param name="dragNodeSize">拖动节点尺寸。</param>
        /// <param name="horizontalCandidate">当前最佳水平候选。</param>
        /// <param name="verticalCandidate">当前最佳垂直候选。</param>
        private void TryUpdateAlignmentCandidatesForBounds(
            Rectangle targetBounds,
            Rectangle proposedBounds,
            Size dragNodeSize,
            ref AlignmentCandidate horizontalCandidate,
            ref AlignmentCandidate verticalCandidate)
        {
            TryUpdateAlignmentCandidate(
                ref horizontalCandidate,
                CanvasAlignmentAxis.Horizontal,
                targetBounds.Top,
                proposedBounds.Top,
                targetBounds.Top,
                targetBounds,
                1);
            TryUpdateAlignmentCandidate(
                ref horizontalCandidate,
                CanvasAlignmentAxis.Horizontal,
                GetRectangleCenterY(targetBounds),
                GetRectangleCenterY(proposedBounds),
                GetRectangleCenterY(targetBounds) - (dragNodeSize.Height / 2),
                targetBounds,
                2);
            TryUpdateAlignmentCandidate(
                ref horizontalCandidate,
                CanvasAlignmentAxis.Horizontal,
                targetBounds.Bottom,
                proposedBounds.Bottom,
                targetBounds.Bottom - dragNodeSize.Height,
                targetBounds,
                1);
            TryUpdateAlignmentCandidate(
                ref verticalCandidate,
                CanvasAlignmentAxis.Vertical,
                targetBounds.Left,
                proposedBounds.Left,
                targetBounds.Left,
                targetBounds,
                1);
            TryUpdateAlignmentCandidate(
                ref verticalCandidate,
                CanvasAlignmentAxis.Vertical,
                GetRectangleCenterX(targetBounds),
                GetRectangleCenterX(proposedBounds),
                GetRectangleCenterX(targetBounds) - (dragNodeSize.Width / 2),
                targetBounds,
                2);
            TryUpdateAlignmentCandidate(
                ref verticalCandidate,
                CanvasAlignmentAxis.Vertical,
                targetBounds.Right,
                proposedBounds.Right,
                targetBounds.Right - dragNodeSize.Width,
                targetBounds,
                1);
        }

        /// <summary>
        /// 按距离选择最贴近的对齐候选。
        /// </summary>
        /// <param name="bestCandidate">当前最佳候选。</param>
        /// <param name="axis">对齐方向。</param>
        /// <param name="guideCoordinate">辅助线所在坐标。</param>
        /// <param name="proposedCoordinate">拖拽节点当前用于比较的坐标。</param>
        /// <param name="newLocationCoordinate">吸附后节点左上角对应坐标。</param>
        /// <param name="targetBounds">命中的参照节点边界。</param>
        /// <param name="priority">候选优先级，中心线高于边线。</param>
        private void TryUpdateAlignmentCandidate(
            ref AlignmentCandidate bestCandidate,
            CanvasAlignmentAxis axis,
            int guideCoordinate,
            int proposedCoordinate,
            int newLocationCoordinate,
            Rectangle targetBounds,
            int priority)
        {
            int distance = Math.Abs(proposedCoordinate - guideCoordinate);
            if (distance > AlignmentSnapDistance)
                return;

            if (bestCandidate == null ||
                distance < bestCandidate.Distance ||
                (distance == bestCandidate.Distance && priority > bestCandidate.Priority))
            {
                bestCandidate = new AlignmentCandidate(
                    axis,
                    guideCoordinate,
                    newLocationCoordinate,
                    distance,
                    targetBounds,
                    priority);
            }
        }

        /// <summary>
        /// 根据吸附候选生成辅助虚线，并扩展到同一坐标上的其它节点。
        /// </summary>
        /// <param name="candidate">吸附候选。</param>
        /// <param name="dragBounds">吸附后的拖拽节点边界。</param>
        /// <param name="draggingNodeIds">当前拖拽批次中的节点 ID。</param>
        private void AddAlignmentGuide(
            AlignmentCandidate candidate,
            Rectangle dragBounds,
            HashSet<int> draggingNodeIds)
        {
            if (candidate == null)
                return;

            if (!IsRectangleOnGuide(candidate.Axis, dragBounds, candidate.GuideCoordinate))
                return;

            int start = int.MaxValue;
            int end = int.MinValue;
            IncludeRectangleInGuideSpan(candidate.Axis, dragBounds, ref start, ref end);
            IncludeRectangleInGuideSpan(candidate.Axis, candidate.TargetBounds, ref start, ref end);
            if (_alignmentTargets.Count > 0)
            {
                foreach (NodeAlignmentTarget target in _alignmentTargets)
                {
                    if (IsRectangleOnGuide(candidate.Axis, target.Bounds, candidate.GuideCoordinate))
                        IncludeRectangleInGuideSpan(candidate.Axis, target.Bounds, ref start, ref end);
                }
            }
            else
            {
                foreach (NodeBase node in _process.Nodes)
                {
                    if (node == null || draggingNodeIds.Contains(node.ID))
                        continue;

                    Rectangle nodeBounds = GetNodeBounds(node);
                    if (IsRectangleOnGuide(candidate.Axis, nodeBounds, candidate.GuideCoordinate))
                        IncludeRectangleInGuideSpan(candidate.Axis, nodeBounds, ref start, ref end);
                }
            }

            if (start == int.MaxValue || end == int.MinValue)
                return;

            int canvasLimit = candidate.Axis == CanvasAlignmentAxis.Horizontal
                ? LogicalCanvasSize.Width
                : LogicalCanvasSize.Height;
            start = Math.Max(0, start - AlignmentGuidePadding);
            end = Math.Min(canvasLimit, end + AlignmentGuidePadding);
            _alignmentGuides.Add(new AlignmentGuide(candidate.Axis, candidate.GuideCoordinate, start, end));
        }

        /// <summary>
        /// 将节点矩形纳入辅助线显示范围。
        /// </summary>
        /// <param name="axis">辅助线方向。</param>
        /// <param name="bounds">节点边界。</param>
        /// <param name="start">辅助线起点。</param>
        /// <param name="end">辅助线终点。</param>
        private static void IncludeRectangleInGuideSpan(
            CanvasAlignmentAxis axis,
            Rectangle bounds,
            ref int start,
            ref int end)
        {
            if (axis == CanvasAlignmentAxis.Horizontal)
            {
                start = Math.Min(start, bounds.Left);
                end = Math.Max(end, bounds.Right);
                return;
            }

            start = Math.Min(start, bounds.Top);
            end = Math.Max(end, bounds.Bottom);
        }

        /// <summary>
        /// 判断节点矩形是否落在同一条对齐辅助线上。
        /// </summary>
        /// <param name="axis">辅助线方向。</param>
        /// <param name="bounds">节点边界。</param>
        /// <param name="coordinate">辅助线坐标。</param>
        /// <returns>落在辅助线附近时返回 true。</returns>
        private static bool IsRectangleOnGuide(CanvasAlignmentAxis axis, Rectangle bounds, int coordinate)
        {
            if (axis == CanvasAlignmentAxis.Horizontal)
            {
                return Math.Abs(bounds.Top - coordinate) <= AlignmentSnapDistance ||
                    Math.Abs(GetRectangleCenterY(bounds) - coordinate) <= AlignmentSnapDistance ||
                    Math.Abs(bounds.Bottom - coordinate) <= AlignmentSnapDistance;
            }

            return Math.Abs(bounds.Left - coordinate) <= AlignmentSnapDistance ||
                Math.Abs(GetRectangleCenterX(bounds) - coordinate) <= AlignmentSnapDistance ||
                Math.Abs(bounds.Right - coordinate) <= AlignmentSnapDistance;
        }

        /// <summary>
        /// 获取矩形水平中心坐标。
        /// </summary>
        /// <param name="bounds">矩形边界。</param>
        /// <returns>水平中心坐标。</returns>
        private static int GetRectangleCenterX(Rectangle bounds)
        {
            return bounds.Left + (bounds.Width / 2);
        }

        /// <summary>
        /// 获取矩形垂直中心坐标。
        /// </summary>
        /// <param name="bounds">矩形边界。</param>
        /// <returns>垂直中心坐标。</returns>
        private static int GetRectangleCenterY(Rectangle bounds)
        {
            return bounds.Top + (bounds.Height / 2);
        }

        /// <summary>
        /// 移动当前拖动批次中的节点，并返回可视位置是否真实发生变化。
        /// </summary>
        /// <param name="requestedDragNodeLocation">鼠标换算出的主拖动节点目标位置。</param>
        /// <returns>任一节点位置变化时返回 true。</returns>
        private bool MoveDraggedNodes(Point requestedDragNodeLocation)
        {
            if (_dragNode == null)
                return false;

            Point dragNodeStartLocation = _dragStartLocation;
            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                if (snapshot.Node == _dragNode)
                {
                    dragNodeStartLocation = snapshot.Location;
                    break;
                }
            }

            HashSet<int> draggingNodeIds = _draggingNodeIds.Count == 0
                ? GetDraggingNodeIds()
                : _draggingNodeIds;
            Point clampedDragNodeLocation = GetAlignedDragNodeLocation(requestedDragNodeLocation, draggingNodeIds);
            int offsetX = clampedDragNodeLocation.X - dragNodeStartLocation.X;
            int offsetY = clampedDragNodeLocation.Y - dragNodeStartLocation.Y;
            bool moved = false;

            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                Point nextLocation = new Point(
                    snapshot.Location.X + offsetX,
                    snapshot.Location.Y + offsetY);
                Point clampedLocation = ClampLocation(nextLocation, GetNodeSize(snapshot.Node));
                if (snapshot.Node.CanvasLocation != clampedLocation)
                {
                    snapshot.Node.CanvasLocation = clampedLocation;
                    moved = true;
                }
            }

            if (moved)
                InvalidateConnectionObstacleCache();

            return moved;
        }

        private void PushMoveHistory(List<NodeLocationSnapshot> oldLocations)
        {
            if (oldLocations == null || oldLocations.Count == 0)
                return;

            List<NodeMoveState> moves = new List<NodeMoveState>();
            foreach (NodeLocationSnapshot oldLocation in oldLocations)
            {
                if (oldLocation.Node != null &&
                    oldLocation.Node.CanvasLocation != oldLocation.Location)
                {
                    moves.Add(new NodeMoveState(
                        oldLocation.Node,
                        oldLocation.Location,
                        oldLocation.Node.CanvasLocation));
                }
            }

            if (moves.Count > 0)
                PushHistory(new NodesMoveEdit(moves));
        }

        private void ApplyNodeLocations(List<NodeLocationSnapshot> locations)
        {
            if (locations == null)
                return;

            foreach (NodeLocationSnapshot location in locations)
            {
                if (location.Node != null)
                    location.Node.CanvasLocation = ClampLocation(location.Location, GetNodeSize(location.Node));
            }

            InvalidateConnectionObstacleCache();
            Invalidate();
            RequestOverlayInvalidate();
        }

        private List<NodeRestoreSnapshot> CaptureNodeRestoreSnapshots(List<NodeBase> nodes)
        {
            List<NodeRestoreSnapshot> snapshots = new List<NodeRestoreSnapshot>();
            if (_process == null || nodes == null)
                return snapshots;

            foreach (NodeBase node in nodes)
            {
                if (node == null)
                    continue;

                snapshots.Add(new NodeRestoreSnapshot(
                    node,
                    _process.Nodes.IndexOf(node),
                    Solution.Instance.Nodes.IndexOf(node),
                    node.IsStartNode));
            }

            return snapshots;
        }

        private List<ConnectionSnapshot> CaptureConnectionsForNodes(List<NodeBase> nodes)
        {
            List<ConnectionSnapshot> snapshots = new List<ConnectionSnapshot>();
            if (_process == null || nodes == null || nodes.Count == 0)
                return snapshots;

            HashSet<int> nodeIds = new HashSet<int>();
            foreach (NodeBase node in nodes)
            {
                if (node != null)
                    nodeIds.Add(node.ID);
            }

            for (int index = 0; index < _process.Connections.Count; index++)
            {
                ProcessConnection connection = _process.Connections[index];
                if (nodeIds.Contains(connection.FromNodeId) ||
                    nodeIds.Contains(connection.ToNodeId))
                {
                    snapshots.Add(new ConnectionSnapshot(connection, index));
                }
            }

            return snapshots;
        }

        private bool DetachNodes(List<NodeRestoreSnapshot> nodeSnapshots)
        {
            if (_process == null || nodeSnapshots == null || nodeSnapshots.Count == 0)
                return false;

            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;

            foreach (NodeRestoreSnapshot snapshot in nodeSnapshots)
            {
                if (snapshot.Node != null && _process.Nodes.Contains(snapshot.Node))
                    snapshot.Node.Selected = true;
            }

            bool removedAny = false;
            foreach (NodeRestoreSnapshot snapshot in nodeSnapshots)
            {
                if (snapshot.Node != null &&
                    _process.Nodes.Contains(snapshot.Node) &&
                    snapshot.Node.DeleteFromProcess(false))
                {
                    removedAny = true;
                }
            }

            _selectedConnection = null;
            OnNodeCollectionChanged();
            Invalidate();
            return removedAny;
        }

        private void RestoreNodes(
            List<NodeRestoreSnapshot> nodeSnapshots,
            List<ConnectionSnapshot> connectionSnapshots)
        {
            if (_process == null || nodeSnapshots == null)
                return;

            nodeSnapshots.Sort((left, right) => left.ProcessIndex.CompareTo(right.ProcessIndex));
            foreach (NodeRestoreSnapshot snapshot in nodeSnapshots)
            {
                if (snapshot.Node == null)
                    continue;

                snapshot.Node.IsStartNode = snapshot.WasStartNode;
                if (!_process.Nodes.Contains(snapshot.Node))
                {
                    int insertIndex = Math.Max(0, Math.Min(snapshot.ProcessIndex, _process.Nodes.Count));
                    _process.Nodes.Insert(insertIndex, snapshot.Node);
                }

                if (!Solution.Instance.Nodes.Contains(snapshot.Node))
                {
                    int insertIndex = Math.Max(0, Math.Min(snapshot.SolutionIndex, Solution.Instance.Nodes.Count));
                    Solution.Instance.Nodes.Insert(insertIndex, snapshot.Node);
                }
            }

            RestoreConnections(connectionSnapshots);

            List<NodeBase> restoredNodes = new List<NodeBase>();
            foreach (NodeRestoreSnapshot snapshot in nodeSnapshots)
            {
                if (snapshot.Node != null)
                    restoredNodes.Add(snapshot.Node);
            }

            SelectNodes(restoredNodes);
            OnNodeCollectionChanged();
            Invalidate();
        }

        /// <summary>
        /// 按框选区域更新节点选中状态，并返回状态发生变化的节点绘制区域。
        /// </summary>
        /// <param name="selection">画布坐标下的框选区域。</param>
        /// <returns>需要重绘的节点区域。</returns>
        private Rectangle SelectNodesInRectangle(Rectangle selection)
        {
            if (_process == null)
                return Rectangle.Empty;

            _selectedConnection = null;
            Rectangle changedBounds = Rectangle.Empty;
            foreach (NodeBase node in _process.Nodes)
            {
                bool selected = selection.IntersectsWith(GetNodeBounds(node));
                if (node.Selected != selected)
                {
                    IncludeCanvasBounds(ref changedBounds, GetNodeVisualBounds(node));
                    node.Selected = selected;
                }
            }

            if (!changedBounds.IsEmpty)
            {
                RefreshSelectedRelationHighlights();
                InvalidateMinimapCache();
            }

            return changedBounds;
        }

        private Rectangle GetSelectionRectangle()
        {
            int x = Math.Min(_selectionStartPoint.X, _selectionEndPoint.X);
            int y = Math.Min(_selectionStartPoint.Y, _selectionEndPoint.Y);
            int width = Math.Abs(_selectionStartPoint.X - _selectionEndPoint.X);
            int height = Math.Abs(_selectionStartPoint.Y - _selectionEndPoint.Y);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 只绘制当前剪裁区域内的网格，避免大画布在局部刷新时仍然全量画线。
        /// </summary>
        /// <param name="graphics">画布绘图对象。</param>
        /// <param name="visibleCanvasBounds">当前需要绘制的画布坐标区域。</param>
        private void DrawGrid(Graphics graphics, Rectangle visibleCanvasBounds)
        {
            Rectangle gridBounds = Rectangle.Intersect(new Rectangle(Point.Empty, LogicalCanvasSize), visibleCanvasBounds);
            if (gridBounds.IsEmpty)
                return;

            int startX = Math.Max(0, (gridBounds.Left / GridSize) * GridSize);
            int startY = Math.Max(0, (gridBounds.Top / GridSize) * GridSize);
            int endX = Math.Min(LogicalCanvasSize.Width, gridBounds.Right + GridSize);
            int endY = Math.Min(LogicalCanvasSize.Height, gridBounds.Bottom + GridSize);

            using (Pen gridPen = new Pen(GridLineColor, 1F / _zoomFactor))
            using (Pen majorGridPen = new Pen(GridMajorLineColor, 1.15F / _zoomFactor))
            {
                for (int x = startX; x <= endX; x += GridSize)
                    graphics.DrawLine(x % (GridSize * 4) == 0 ? majorGridPen : gridPen, x, gridBounds.Top, x, gridBounds.Bottom);

                for (int y = startY; y <= endY; y += GridSize)
                    graphics.DrawLine(y % (GridSize * 4) == 0 ? majorGridPen : gridPen, gridBounds.Left, y, gridBounds.Right, y);
            }
        }

        private void DrawSelectionRectangle(Graphics graphics)
        {
            Rectangle selection = GetSelectionRectangle();
            if (selection.Width <= 0 || selection.Height <= 0)
                return;

            using (SolidBrush fill = new SolidBrush(Color.FromArgb(45, 66, 153, 225)))
            using (Pen border = new Pen(Color.FromArgb(90, 190, 255), 1.2F / _zoomFactor))
            {
                border.DashStyle = DashStyle.Dash;
                graphics.FillRectangle(fill, selection);
                graphics.DrawRectangle(border, selection);
            }
        }

        /// <summary>
        /// 绘制拖拽吸附时的水平和垂直辅助虚线。
        /// </summary>
        /// <param name="graphics">画布绘图对象。</param>
        private void DrawAlignmentGuides(Graphics graphics)
        {
            using (Pen guidePen = new Pen(AlignmentGuideColor, 1.35F / _zoomFactor))
            {
                guidePen.DashStyle = DashStyle.Dash;
                guidePen.StartCap = LineCap.Flat;
                guidePen.EndCap = LineCap.Flat;
                foreach (AlignmentGuide guide in _alignmentGuides)
                {
                    if (guide.Axis == CanvasAlignmentAxis.Horizontal)
                    {
                        graphics.DrawLine(
                            guidePen,
                            guide.Start,
                            guide.Coordinate,
                            guide.End,
                            guide.Coordinate);
                        continue;
                    }

                    graphics.DrawLine(
                        guidePen,
                        guide.Coordinate,
                        guide.Start,
                        guide.Coordinate,
                        guide.End);
                }
            }
        }

        private void DrawZoomBadge(Graphics graphics)
        {
            string text = ((int)Math.Round(_zoomFactor * 100F)) + "%";
            Rectangle badge = new Rectangle(12, 12, 58, 24);
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(235, 38, 42, 46)))
            using (Pen border = new Pen(Color.FromArgb(90, 98, 108), 1F))
            using (Font font = new Font("Segoe UI", 8F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(230, 236, 243)))
            {
                graphics.FillRectangle(fill, badge);
                graphics.DrawRectangle(border, badge);
                graphics.DrawString(text, font, textBrush, badge.X + 8, badge.Y + 3);
            }
        }

        internal void DrawImagePreviewOverlay(Graphics graphics, Size overlaySize)
        {
            PrepareImagePreviewOverlayBounds(overlaySize);
            if (_imagePreviewBounds.IsEmpty)
            {
                return;
            }

            using (SolidBrush fill = new SolidBrush(OverlayBackColor))
            using (Pen border = new Pen(OverlayBorderColor, 1.4F))
            {
                graphics.FillRectangle(fill, _imagePreviewBounds);
                graphics.DrawRectangle(border, _imagePreviewBounds);
            }

            PreviewImageInfo previewInfo = GetPreviewImageInfo();
            Bitmap previewBitmap = GetPreviewBitmap(previewInfo);
            string title = previewInfo == null ? string.Empty : previewInfo.Title;
            string rgbText = GetPreviewRgbText(previewBitmap);

            Rectangle header = new Rectangle(_imagePreviewBounds.X + 1, _imagePreviewBounds.Y + 1, _imagePreviewBounds.Width - 2, 32);
            Rectangle content = new Rectangle(
                _imagePreviewBounds.X + 8,
                header.Bottom + 8,
                _imagePreviewBounds.Width - 16,
                _imagePreviewBounds.Height - header.Height - 17);

            DrawPreviewHeader(graphics, header, title, rgbText);

            using (SolidBrush contentBrush = new SolidBrush(Color.FromArgb(37, 40, 44)))
                graphics.FillRectangle(contentBrush, content);

            if (previewBitmap != null)
                DrawPreviewBitmap(graphics, previewBitmap, GetPreviewDisplayResult(previewInfo), content);
            else
                DrawPreviewPlaceholder(graphics, content, string.Empty);
        }

        internal void HandleImagePreviewMouseDown(Control menuOwner, Point overlayPoint, MouseButtons button, Size overlaySize)
        {
            PrepareImagePreviewOverlayBounds(overlaySize);
            if (_imagePreviewBounds.IsEmpty)
                return;

            Focus();
            if (button == MouseButtons.Left && HitPreviewSelector(overlayPoint))
            {
                ShowPreviewImageMenu(menuOwner);
                return;
            }

            UpdatePreviewHover(overlayPoint);
        }

        internal void HandleImagePreviewMouseMove(Point overlayPoint, Size overlaySize)
        {
            PrepareImagePreviewOverlayBounds(overlaySize);
            UpdatePreviewHover(overlayPoint);
        }

        internal void HandleImagePreviewMouseLeave()
        {
            if (!_previewHoverImagePoint.HasValue)
                return;

            _previewHoverImagePoint = null;
            RequestOverlayInvalidate();
        }

        private void PrepareImagePreviewOverlayBounds(Size overlaySize)
        {
            if (overlaySize.Width <= 0 || overlaySize.Height <= 0)
            {
                _imagePreviewBounds = Rectangle.Empty;
                _previewSelectorBounds = Rectangle.Empty;
                _previewImageDrawBounds = Rectangle.Empty;
                _previewImagePixelSize = Size.Empty;
                return;
            }

            _imagePreviewBounds = new Rectangle(
                0,
                0,
                Math.Max(0, overlaySize.Width - 1),
                Math.Max(0, overlaySize.Height - 1));
        }

        private void DrawPreviewHeader(Graphics graphics, Rectangle header, string title, string rgbText)
        {
            Rectangle selector = new Rectangle(header.X, header.Y, Math.Max(120, header.Width - 155), header.Height);
            _previewSelectorBounds = selector;
            Rectangle arrow = new Rectangle(selector.Right - 20, selector.Y, 20, selector.Height);
            Rectangle rgb = new Rectangle(selector.Right, header.Y, header.Right - selector.Right, header.Height);
            if (string.IsNullOrEmpty(title))
                title = "选择图像";

            using (SolidBrush headerBrush = new SolidBrush(Color.FromArgb(36, 38, 42)))
            using (SolidBrush selectorBrush = new SolidBrush(Color.FromArgb(43, 46, 51)))
            using (SolidBrush arrowBrush = new SolidBrush(Color.FromArgb(235, 238, 242)))
            using (Pen border = new Pen(Color.FromArgb(178, 182, 188), 1F))
            using (Font font = new Font("Segoe UI", 8.5F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(222, 229, 236)))
            using (SolidBrush arrowTextBrush = new SolidBrush(Color.FromArgb(35, 38, 42)))
            using (StringFormat textFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                graphics.FillRectangle(headerBrush, header);
                graphics.FillRectangle(selectorBrush, selector);
                graphics.FillRectangle(arrowBrush, arrow);
                graphics.DrawRectangle(border, selector);
                graphics.DrawRectangle(border, rgb);
                Rectangle titleBounds = new Rectangle(selector.X + 8, selector.Y + 8, selector.Width - 32, selector.Height - 12);
                graphics.DrawString(title, font, textBrush, titleBounds, textFormat);
                graphics.DrawString(rgbText, font, textBrush, rgb.X + 8, rgb.Y + 8);
                graphics.DrawString("v", font, arrowTextBrush, arrow.X + 6, arrow.Y + 8);
            }
        }

        private void DrawPreviewBitmap(Graphics graphics, Bitmap previewBitmap, AlgorithmResult displayResult, Rectangle content)
        {
            _previewImageDrawBounds = GetContainedImageBounds(previewBitmap.Size, content);
            _previewImagePixelSize = previewBitmap.Size;

            InterpolationMode oldInterpolationMode = graphics.InterpolationMode;
            PixelOffsetMode oldPixelOffsetMode = graphics.PixelOffsetMode;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(previewBitmap, _previewImageDrawBounds);
            DrawPreviewDisplayResult(graphics, displayResult, previewBitmap.Size, _previewImageDrawBounds);
            graphics.InterpolationMode = oldInterpolationMode;
            graphics.PixelOffsetMode = oldPixelOffsetMode;

            using (Pen border = new Pen(Color.FromArgb(82, 88, 96), 1F))
                graphics.DrawRectangle(border, _previewImageDrawBounds);
        }

        private void DrawPreviewDisplayResult(Graphics graphics, AlgorithmResult displayResult, Size imageSize, Rectangle imageBounds)
        {
            if (displayResult == null ||
                imageSize.Width <= 0 ||
                imageSize.Height <= 0 ||
                imageBounds.Width <= 0 ||
                imageBounds.Height <= 0)
            {
                return;
            }

            float scaleX = imageBounds.Width / (float)imageSize.Width;
            float scaleY = imageBounds.Height / (float)imageSize.Height;
            float scale = Math.Min(scaleX, scaleY);
            SmoothingMode oldSmoothingMode = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (ColorRotatedRect rect in displayResult.Rects)
                DrawPreviewRotatedRect(graphics, rect, imageBounds, scaleX, scaleY);

            foreach (List<ColorRotatedRect> rects in displayResult.RectsNgMap.Values)
            {
                foreach (ColorRotatedRect rect in rects)
                    DrawPreviewRotatedRect(graphics, rect, imageBounds, scaleX, scaleY);
            }

            foreach (ColorLine line in displayResult.Lines)
            {
                using (Pen pen = CreatePreviewPen(line.Color, scale, line.LineWidth))
                    graphics.DrawLine(pen, MapPreviewPoint(line.P1, imageBounds, scaleX, scaleY), MapPreviewPoint(line.P2, imageBounds, scaleX, scaleY));
            }

            foreach (ColorCircle circle in displayResult.Circles)
            {
                PointF center = MapPreviewPoint(circle.Center, imageBounds, scaleX, scaleY);
                float radius = Math.Max(1.5F, circle.Radius * scale);
                using (Pen pen = CreatePreviewPen(circle.Color, scale, circle.LineWidth))
                    graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }

            foreach (ColorEllipse ellipse in displayResult.Ellipses)
            {
                PointF center = MapPreviewPoint(ellipse.Center, imageBounds, scaleX, scaleY);
                GraphicsState state = graphics.Save();
                graphics.TranslateTransform(center.X, center.Y);
                graphics.RotateTransform(ellipse.Angle);
                using (Pen pen = CreatePreviewPen(ellipse.Color, scale, ellipse.LineWidth))
                    graphics.DrawEllipse(pen, -ellipse.Width * scaleX / 2F, -ellipse.Height * scaleY / 2F, ellipse.Width * scaleX, ellipse.Height * scaleY);
                graphics.Restore(state);
            }

            foreach (ColorContour contour in displayResult.Contours)
                DrawPreviewContour(graphics, contour, imageBounds, scaleX, scaleY, scale);

            DrawPreviewResultText(graphics, displayResult, imageBounds);
            graphics.SmoothingMode = oldSmoothingMode;
        }

        private static void DrawPreviewRotatedRect(Graphics graphics, ColorRotatedRect rect, Rectangle imageBounds, float scaleX, float scaleY)
        {
            if (rect == null)
                return;

            float centerX = imageBounds.X + rect.RotatedRect.center.x * scaleX;
            float centerY = imageBounds.Y + rect.RotatedRect.center.y * scaleY;
            float width = rect.RotatedRect.size.width * scaleX;
            float height = rect.RotatedRect.size.height * scaleY;
            if (width <= 0F || height <= 0F)
                return;

            using (GraphicsPath path = CreateRotatedRectPath(centerX, centerY, width, height, rect.RotatedRect.angle))
            using (Pen pen = CreatePreviewPen(rect.Color, Math.Min(scaleX, scaleY), rect.LineWidth))
            {
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRotatedRectPath(float centerX, float centerY, float width, float height, float angle)
        {
            float halfWidth = width / 2F;
            float halfHeight = height / 2F;
            PointF[] points =
            {
                new PointF(-halfWidth, -halfHeight),
                new PointF(halfWidth, -halfHeight),
                new PointF(halfWidth, halfHeight),
                new PointF(-halfWidth, halfHeight)
            };

            using (Matrix matrix = new Matrix())
            {
                matrix.Rotate(angle);
                matrix.Translate(centerX, centerY, MatrixOrder.Append);
                matrix.TransformPoints(points);
            }

            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(points);
            return path;
        }

        private static void DrawPreviewContour(Graphics graphics, ColorContour contour, Rectangle imageBounds, float scaleX, float scaleY, float scale)
        {
            if (contour == null || contour.Points == null || contour.Points.Count == 0)
                return;

            int step = Math.Max(1, contour.Points.Count / 1200);
            float dotSize = Math.Max(1F, contour.LineWidth * Math.Max(0.7F, scale));
            using (SolidBrush brush = new SolidBrush(contour.Color))
            {
                for (int i = 0; i < contour.Points.Count; i += step)
                {
                    PointF point = MapPreviewPoint(contour.Points[i], imageBounds, scaleX, scaleY);
                    graphics.FillRectangle(brush, point.X - dotSize / 2F, point.Y - dotSize / 2F, dotSize, dotSize);
                }
            }
        }

        private static void DrawPreviewResultText(Graphics graphics, AlgorithmResult displayResult, Rectangle imageBounds)
        {
            if (displayResult.Texts.Count == 0)
                return;

            foreach (var group in displayResult.Texts.GroupBy(t => new { t.Position, t.FontSize, t.Margin, t.Title }))
            {
                DrawPreviewTextGroup(graphics, imageBounds, group.Key.Position, group.Key.FontSize, group.Key.Margin, group.Key.Title, group.ToList());
            }
        }

        private static void DrawPreviewTextGroup(
            Graphics graphics,
            Rectangle imageBounds,
            DisplayTextPosition position,
            int fontSize,
            int margin,
            string title,
            List<ColorText> texts)
        {
            if (texts == null || texts.Count == 0)
                return;

            int maxLines = Math.Min(3, texts.Count);
            float previewFontSize = Math.Max(7F, Math.Min(13F, fontSize * 0.45F));
            bool showTitle = !string.IsNullOrWhiteSpace(title);
            using (Font titleFont = new Font("Microsoft YaHei", previewFontSize + 1F, FontStyle.Bold))
            using (Font font = new Font("Microsoft YaHei", previewFontSize, FontStyle.Regular))
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                float maxWidth = 0F;
                float totalHeight = 0F;
                if (showTitle)
                {
                    SizeF titleSize = graphics.MeasureString(title, titleFont);
                    maxWidth = Math.Max(maxWidth, titleSize.Width);
                    totalHeight += titleSize.Height + 2F;
                }

                for (int i = 0; i < maxLines; i++)
                {
                    SizeF lineSize = graphics.MeasureString(texts[i].Text ?? string.Empty, font);
                    maxWidth = Math.Max(maxWidth, lineSize.Width);
                    totalHeight += lineSize.Height + 1F;
                }

                float pad = 4F;
                float marginPixels = Math.Max(4, Math.Min(16, margin));
                float x = (position == DisplayTextPosition.TopLeft || position == DisplayTextPosition.BottomLeft)
                    ? imageBounds.X + marginPixels
                    : imageBounds.Right - maxWidth - pad * 2 - marginPixels;
                float y = (position == DisplayTextPosition.TopLeft || position == DisplayTextPosition.TopRight)
                    ? imageBounds.Y + marginPixels
                    : imageBounds.Bottom - totalHeight - pad * 2 - marginPixels;

                RectangleF textBounds = new RectangleF(x - pad, y - pad, maxWidth + pad * 2, totalHeight + pad * 2);
                using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                    graphics.FillRectangle(backBrush, textBounds);

                if (showTitle)
                {
                    graphics.DrawString(title, titleFont, Brushes.White, new RectangleF(x, y, maxWidth, titleFont.Height + 2), format);
                    y += graphics.MeasureString(title, titleFont).Height + 2F;
                }

                for (int i = 0; i < maxLines; i++)
                {
                    ColorText text = texts[i];
                    using (SolidBrush textBrush = new SolidBrush(text.Color))
                    {
                        RectangleF lineBounds = new RectangleF(x, y, maxWidth, font.Height + 2);
                        graphics.DrawString(text.Text ?? string.Empty, font, textBrush, lineBounds, format);
                        y += graphics.MeasureString(text.Text ?? string.Empty, font).Height + 1F;
                    }
                }
            }
        }

        private static PointF MapPreviewPoint(PointF point, Rectangle imageBounds, float scaleX, float scaleY)
        {
            return new PointF(imageBounds.X + point.X * scaleX, imageBounds.Y + point.Y * scaleY);
        }

        private static Pen CreatePreviewPen(Color color, float scale, float lineWidth = 2F)
        {
            return new Pen(color, Math.Max(1.2F, lineWidth * Math.Max(0.7F, scale)));
        }

        private void DrawPreviewPlaceholder(Graphics graphics, Rectangle content, string text)
        {
            _previewImageDrawBounds = Rectangle.Empty;
            _previewImagePixelSize = Size.Empty;

            if (string.IsNullOrEmpty(text))
                return;

            using (Font font = new Font("Microsoft YaHei", 9F, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(136, 146, 158)))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(text, font, textBrush, content, format);
            }
        }

        private PreviewImageInfo GetPreviewImageInfo()
        {
            List<PreviewImageInfo> items = CollectPreviewImageItems();
            if (items.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(_selectedPreviewImageKey))
            {
                PreviewImageInfo selected = items.FirstOrDefault(item => item.Key == _selectedPreviewImageKey);
                if (selected != null)
                    return selected;

                _selectedPreviewImageKey = null;
            }

            NodeBase selectedNode = SelectedNode;
            if (selectedNode != null)
            {
                PreviewImageInfo nodeImage = items.FirstOrDefault(item => item.Node == selectedNode);
                if (nodeImage != null)
                    return nodeImage;
            }

            return items[items.Count - 1];
        }

        private List<PreviewImageInfo> CollectPreviewImageItems()
        {
            List<PreviewImageInfo> items = new List<PreviewImageInfo>();
            if (_process == null)
                return items;

            foreach (NodeBase node in _process.Nodes)
            {
                OutputImage output = GetOutputImage(node);
                if (output == null)
                    continue;

                string prefix = node.ID + "." + node.NodeName;
                if (IsUsableMat(output.SrcImg))
                    items.Add(new PreviewImageInfo(node, output.SrcImg, node.ID + ":src", prefix + " - 原图"));

                if (output.Bitmaps == null)
                    continue;

                int validIndex = 0;
                for (int index = 0; index < output.Bitmaps.Count; index++)
                {
                    Mat bitmap = output.Bitmaps[index];
                    if (!IsUsableMat(bitmap))
                        continue;

                    validIndex++;
                    string title = output.Bitmaps.Count == 1
                        ? prefix + " - 输出图像"
                        : prefix + " - 输出图像" + validIndex;
                    items.Add(new PreviewImageInfo(node, bitmap, node.ID + ":out:" + index, title));
                }
            }

            return items;
        }

        private bool TryGetPreviewMat(NodeBase node, out Mat image)
        {
            image = null;
            OutputImage output = GetOutputImage(node);
            if (output == null)
                return false;

            if (output.Bitmaps != null)
            {
                foreach (Mat bitmap in output.Bitmaps)
                {
                    if (IsUsableMat(bitmap))
                    {
                        image = bitmap;
                        return true;
                    }
                }
            }

            if (IsUsableMat(output.SrcImg))
            {
                image = output.SrcImg;
                return true;
            }

            return false;
        }

        private Bitmap GetPreviewBitmap(PreviewImageInfo previewInfo)
        {
            if (previewInfo == null || !IsUsableMat(previewInfo.Image))
            {
                DisposePreviewCache();
                return null;
            }

            if (_cachedPreviewBitmap != null &&
                ReferenceEquals(_cachedPreviewMat, previewInfo.Image) &&
                string.Equals(_cachedPreviewKey, previewInfo.Key, StringComparison.Ordinal))
            {
                return _cachedPreviewBitmap;
            }

            DisposePreviewCache();
            _cachedPreviewKey = previewInfo.Key;
            _cachedPreviewMat = previewInfo.Image;
            _cachedPreviewBitmap = CreatePreviewBitmap(previewInfo.Image);
            if (_cachedPreviewBitmap == null)
            {
                _cachedPreviewKey = null;
                _cachedPreviewMat = null;
            }

            return _cachedPreviewBitmap;
        }

        private AlgorithmResult GetPreviewDisplayResult(PreviewImageInfo previewInfo)
        {
            if (previewInfo == null)
                return null;

            OutputImage output = GetOutputImage(previewInfo.Node);
            return output == null ? null : output.DisplayResult;
        }

        private void DisposePreviewCache()
        {
            if (_cachedPreviewBitmap != null)
            {
                _cachedPreviewBitmap.Dispose();
                _cachedPreviewBitmap = null;
            }

            _cachedPreviewKey = null;
            _cachedPreviewMat = null;
        }

        private OutputImage GetOutputImage(NodeBase node)
        {
            if (node == null || node.Result == null)
                return null;

            try
            {
                System.Reflection.PropertyInfo property = node.Result.GetType().GetProperty("OutputImage");
                return property == null ? null : property.GetValue(node.Result, null) as OutputImage;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUsableMat(Mat image)
        {
            try
            {
                return image != null &&
                    !image.IsDisposed &&
                    !image.Empty() &&
                    image.Width > 0 &&
                    image.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static Bitmap CreatePreviewBitmap(Mat image)
        {
            if (!IsUsableMat(image))
                return null;

            try
            {
                return BitmapConverter.ToBitmap(image);
            }
            catch
            {
                return null;
            }
        }

        private string GetPreviewRgbText(Bitmap previewBitmap)
        {
            if (previewBitmap == null || !_previewHoverImagePoint.HasValue)
                return "RGB: --";

            Point point = _previewHoverImagePoint.Value;
            if (point.X < 0 ||
                point.Y < 0 ||
                point.X >= previewBitmap.Width ||
                point.Y >= previewBitmap.Height)
            {
                return "RGB: --";
            }

            try
            {
                Color color = previewBitmap.GetPixel(point.X, point.Y);
                return "RGB: " + color.R + "," + color.G + "," + color.B;
            }
            catch
            {
                return "RGB: --";
            }
        }

        private static Rectangle GetContainedImageBounds(Size imageSize, Rectangle content)
        {
            if (imageSize.Width <= 0 ||
                imageSize.Height <= 0 ||
                content.Width <= 0 ||
                content.Height <= 0)
            {
                return Rectangle.Empty;
            }

            float scale = Math.Min(
                content.Width / (float)imageSize.Width,
                content.Height / (float)imageSize.Height);
            int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
            int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
            int x = content.X + ((content.Width - width) / 2);
            int y = content.Y + ((content.Height - height) / 2);
            return new Rectangle(x, y, width, height);
        }

        private bool HitPreviewSelector(Point controlPoint)
        {
            return !_previewSelectorBounds.IsEmpty && _previewSelectorBounds.Contains(controlPoint);
        }

        private void ShowPreviewImageMenu(Control menuOwner)
        {
            List<PreviewImageInfo> items = CollectPreviewImageItems();
            PreviewImageInfo current = GetPreviewImageInfo();
            _previewImageMenu.Items.Clear();

            if (items.Count == 0)
            {
                ToolStripMenuItem emptyItem = new ToolStripMenuItem("暂无可查看图像");
                emptyItem.Enabled = false;
                _previewImageMenu.Items.Add(emptyItem);
            }
            else
            {
                foreach (PreviewImageInfo item in items)
                {
                    ToolStripMenuItem menuItem = new ToolStripMenuItem(item.Title);
                    menuItem.Checked = current != null && item.Key == current.Key;
                    menuItem.Tag = item.Key;
                    menuItem.Click += PreviewImageMenuItem_Click;
                    _previewImageMenu.Items.Add(menuItem);
                }
            }

            Point menuLocation = _previewSelectorBounds.IsEmpty
                ? new Point(_imagePreviewBounds.Left, _imagePreviewBounds.Top)
                : new Point(_previewSelectorBounds.Left, _previewSelectorBounds.Bottom);
            _previewImageMenu.Show(menuOwner ?? this, menuLocation);
        }

        private void PreviewImageMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            string key = menuItem == null ? null : menuItem.Tag as string;
            if (string.IsNullOrEmpty(key))
                return;

            _selectedPreviewImageKey = key;
            DisposePreviewCache();
            RequestOverlayInvalidate();
        }

        private bool UpdatePreviewHover(Point controlPoint)
        {
            Point? previous = _previewHoverImagePoint;
            Point? next = null;

            if (!_previewImageDrawBounds.IsEmpty &&
                !_previewImagePixelSize.IsEmpty &&
                _previewImageDrawBounds.Contains(controlPoint))
            {
                int x = (int)Math.Floor((controlPoint.X - _previewImageDrawBounds.X) *
                    (_previewImagePixelSize.Width / (double)_previewImageDrawBounds.Width));
                int y = (int)Math.Floor((controlPoint.Y - _previewImageDrawBounds.Y) *
                    (_previewImagePixelSize.Height / (double)_previewImageDrawBounds.Height));
                x = Math.Max(0, Math.Min(_previewImagePixelSize.Width - 1, x));
                y = Math.Max(0, Math.Min(_previewImagePixelSize.Height - 1, y));
                next = new Point(x, y);
            }

            bool changed = previous.HasValue != next.HasValue ||
                (previous.HasValue && next.HasValue && previous.Value != next.Value);
            if (!changed)
                return false;

            _previewHoverImagePoint = next;
            RequestOverlayInvalidate();
            return true;
        }

        private void InvalidateRuntimeChange(NodeBase node)
        {
            if (node == null)
                return;

            bool hasPreviewImage = HasPreviewImage(node);
            if (hasPreviewImage)
            {
                DisposePreviewCache();
                RequestOverlayInvalidate();
            }

            if (node.Selected)
            {
                Invalidate();
                RequestOverlayInvalidate();
                return;
            }

            if (SelectedNode == null && hasPreviewImage)
            {
                Invalidate();
                RequestOverlayInvalidate();
            }
            else
            {
                InvalidateNode(node);
            }
        }

        private bool HasPreviewImage(NodeBase node)
        {
            Mat image;
            return TryGetPreviewMat(node, out image);
        }

        internal void DrawMinimapOverlay(Graphics graphics, Size overlaySize)
        {
            PrepareMinimapOverlayBounds(overlaySize);
            if (_minimapBounds.IsEmpty)
            {
                return;
            }

            EnsureMinimapMapCache(overlaySize);
            if (_cachedMinimapBitmap != null)
                graphics.DrawImageUnscaled(_cachedMinimapBitmap, Point.Empty);

            Rectangle viewport = GetMinimapViewportBounds();
            if (!viewport.IsEmpty)
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(36, 255, 255, 255)))
                using (Pen border = new Pen(Color.FromArgb(210, 230, 236, 244), 1.4F))
                {
                    graphics.FillRectangle(fill, viewport);
                    graphics.DrawRectangle(border, viewport);
                }
            }
        }

        /// <summary>
        /// 确保缩略图静态图层缓存可用。
        /// </summary>
        /// <param name="overlaySize">缩略图控件当前尺寸。</param>
        private void EnsureMinimapMapCache(Size overlaySize)
        {
            if (overlaySize.Width <= 0 || overlaySize.Height <= 0)
                return;

            if (_cachedMinimapBitmap != null &&
                !_minimapCacheDirty &&
                _cachedMinimapBitmapSize == overlaySize)
            {
                return;
            }

            DisposeMinimapCache();
            _cachedMinimapBitmap = new Bitmap(Math.Max(1, overlaySize.Width), Math.Max(1, overlaySize.Height));
            _cachedMinimapBitmapSize = overlaySize;

            using (Graphics cacheGraphics = Graphics.FromImage(_cachedMinimapBitmap))
            {
                cacheGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                cacheGraphics.Clear(Color.FromArgb(42, 44, 48));

                using (SolidBrush fill = new SolidBrush(Color.FromArgb(220, 42, 44, 48)))
                using (Pen border = new Pen(Color.FromArgb(110, 116, 124), 1.2F))
                {
                    cacheGraphics.FillRectangle(fill, _minimapBounds);
                    cacheGraphics.DrawRectangle(border, _minimapBounds);
                }

                if (_process != null)
                {
                    foreach (ProcessConnection connection in _process.Connections)
                        DrawMinimapConnection(cacheGraphics, connection);

                    foreach (NodeBase node in _process.Nodes)
                        DrawMinimapNode(cacheGraphics, node);
                }
            }

            _minimapCacheDirty = false;
        }

        internal void CenterViewportFromMinimap(Point controlPoint, Size overlaySize)
        {
            PrepareMinimapOverlayBounds(overlaySize);
            CenterViewportFromMinimap(controlPoint);
        }

        private void PrepareMinimapOverlayBounds(Size overlaySize)
        {
            if (overlaySize.Width <= 0 || overlaySize.Height <= 0)
            {
                _minimapBounds = Rectangle.Empty;
                _minimapCanvasBounds = Rectangle.Empty;
                return;
            }

            _minimapBounds = new Rectangle(
                0,
                0,
                Math.Max(0, overlaySize.Width - 1),
                Math.Max(0, overlaySize.Height - 1));
            _minimapCanvasBounds = new Rectangle(
                _minimapBounds.X + 1,
                _minimapBounds.Y + 1,
                Math.Max(0, _minimapBounds.Width - 2),
                Math.Max(0, _minimapBounds.Height - 2));
        }

        private void DrawMinimapNode(Graphics graphics, NodeBase node)
        {
            Rectangle bounds = GetNodeBounds(node);
            RectangleF miniBounds = MapCanvasRectangleToMinimap(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            if (miniBounds.Width < 2F)
                miniBounds.Width = 2F;
            if (miniBounds.Height < 2F)
                miniBounds.Height = 2F;

            Color fillColor = node.Selected
                ? CanvasAccentColor
                : node.Active
                    ? Color.FromArgb(126, 141, 157)
                    : Color.FromArgb(82, 89, 98);
            using (SolidBrush brush = new SolidBrush(fillColor))
                graphics.FillRectangle(brush, miniBounds);
        }

        private void DrawMinimapConnection(Graphics graphics, ProcessConnection connection)
        {
            NodeBase fromNode = FindNode(connection.FromNodeId);
            NodeBase toNode = FindNode(connection.ToNodeId);
            if (fromNode == null || toNode == null)
                return;

            PointF start = MapCanvasPointToMinimap(GetAnchorCenter(fromNode, connection.FromAnchor));
            PointF end = MapCanvasPointToMinimap(GetAnchorCenter(toNode, connection.ToAnchor));
            using (Pen pen = new Pen(Color.FromArgb(95, 118, 132, 148), 1F))
                graphics.DrawLine(pen, start, end);
        }

        private Rectangle GetMinimapViewportBounds()
        {
            if (_minimapCanvasBounds.IsEmpty)
                return Rectangle.Empty;

            float viewportX = -AutoScrollPosition.X / _zoomFactor;
            float viewportY = -AutoScrollPosition.Y / _zoomFactor;
            float viewportWidth = ClientSize.Width / _zoomFactor;
            float viewportHeight = ClientSize.Height / _zoomFactor;
            RectangleF viewport = MapCanvasRectangleToMinimap(new RectangleF(viewportX, viewportY, viewportWidth, viewportHeight));
            Rectangle result = Rectangle.Round(viewport);
            result.Intersect(_minimapCanvasBounds);
            return result;
        }

        private RectangleF MapCanvasRectangleToMinimap(RectangleF canvasRectangle)
        {
            PointF location = MapCanvasPointToMinimap(new PointF(canvasRectangle.X, canvasRectangle.Y));
            float width = canvasRectangle.Width / LogicalCanvasSize.Width * _minimapCanvasBounds.Width;
            float height = canvasRectangle.Height / LogicalCanvasSize.Height * _minimapCanvasBounds.Height;
            return new RectangleF(location.X, location.Y, width, height);
        }

        private PointF MapCanvasPointToMinimap(PointF canvasPoint)
        {
            if (_minimapCanvasBounds.IsEmpty)
                return PointF.Empty;

            float x = _minimapCanvasBounds.X + (canvasPoint.X / LogicalCanvasSize.Width * _minimapCanvasBounds.Width);
            float y = _minimapCanvasBounds.Y + (canvasPoint.Y / LogicalCanvasSize.Height * _minimapCanvasBounds.Height);
            return new PointF(x, y);
        }

        private void CenterViewportFromMinimap(Point controlPoint)
        {
            if (_minimapCanvasBounds.IsEmpty)
                return;

            int x = Math.Max(_minimapCanvasBounds.Left, Math.Min(_minimapCanvasBounds.Right, controlPoint.X));
            int y = Math.Max(_minimapCanvasBounds.Top, Math.Min(_minimapCanvasBounds.Bottom, controlPoint.Y));
            float canvasX = (float)(x - _minimapCanvasBounds.X) / _minimapCanvasBounds.Width * LogicalCanvasSize.Width;
            float canvasY = (float)(y - _minimapCanvasBounds.Y) / _minimapCanvasBounds.Height * LogicalCanvasSize.Height;
            DeferRuntimeInvalidationsForInteraction();
            SetScrollPositionForAnchor(
                new Point((int)Math.Round(canvasX), (int)Math.Round(canvasY)),
                new Point(ClientSize.Width / 2, ClientSize.Height / 2));
            Invalidate();
            RequestOverlayInvalidate(false, true);
        }

        private void DrawNode(Graphics graphics, NodeBase node)
        {
            Rectangle bounds = GetNodeBounds(node);
            Color fillColor = GetNodeFillColor(node);
            Color statusColor = GetNodeStatusColor(node);
            Color borderColor = GetNodeBorderColor(node);
            float borderWidth = GetNodeBorderWidth(node);

            Rectangle shadowBounds = bounds;
            shadowBounds.Offset(3, 4);
            using (GraphicsPath shadow = CreateRoundRect(shadowBounds, 6))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0)))
            {
                graphics.FillPath(shadowBrush, shadow);
            }

            using (GraphicsPath body = CreateRoundRect(bounds, 6))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor, borderWidth))
            {
                graphics.FillPath(fill, body);
                graphics.DrawPath(border, body);
            }

            Rectangle titleBand = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 9);
            using (SolidBrush titleBrush = new SolidBrush(GetNodeTitleBandColor(node)))
                graphics.FillRectangle(titleBrush, titleBand);

            using (Font titleFont = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold))
            using (Font metaFont = new Font("Microsoft YaHei", 7.2F, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(NodeTextColor))
            using (SolidBrush metaBrush = new SolidBrush(IsNodeAlertStatus(node) ? statusColor : NodeMetaTextColor))
            using (SolidBrush statusBrush = new SolidBrush(statusColor))
            using (StringFormat textFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                int titleRightPadding = node.IsStartNode ? 58 : 28;
                RectangleF titleRect = new RectangleF(bounds.X + 14, bounds.Y + 19, bounds.Width - 14 - titleRightPadding, 20);
                RectangleF metaRect = new RectangleF(bounds.X + 30, bounds.Y + 40, bounds.Width - 44, 15);
                Rectangle statusDot = new Rectangle(bounds.X + 14, bounds.Y + 44, 9, 9);
                string metaText = GetNodeMetaText(node);

                graphics.DrawString(node.ID + "." + node.NodeName, titleFont, titleBrush, titleRect, textFormat);
                graphics.FillEllipse(statusBrush, statusDot);
                graphics.DrawString(metaText, metaFont, metaBrush, metaRect, textFormat);
            }

            DrawNodeParameterSummary(graphics, node, bounds);

            if (node.IsStartNode)
            {
                Rectangle badge = new Rectangle(bounds.Right - 47, bounds.Y + 18, 34, 16);
                using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(47, 158, 68)))
                using (Font badgeFont = new Font("Segoe UI", 6.5F, FontStyle.Bold))
                {
                    graphics.FillRectangle(badgeBrush, badge);
                    graphics.DrawString("Start", badgeFont, Brushes.White, badge.X + 3, badge.Y + 1);
                }
            }

            DrawAnchor(graphics, node, "Top");
            bool conditionalBranchNode = IsConditionalBranchNode(node);
            DrawAnchor(graphics, node, "Right", conditionalBranchNode ? "T" : null);
            DrawAnchor(graphics, node, "Bottom", conditionalBranchNode ? "F" : null);
            DrawAnchor(graphics, node, "Left");
        }

        /// <summary>
        /// 绘制节点运行参数摘要。
        /// </summary>
        /// <param name="graphics">绘图对象。</param>
        /// <param name="node">节点。</param>
        /// <param name="bounds">节点边界。</param>
        private void DrawNodeParameterSummary(Graphics graphics, NodeBase node, Rectangle bounds)
        {
            string summary = node == null ? null : node.GetCanvasParameterSummary();
            if (string.IsNullOrWhiteSpace(summary))
                return;

            using (Font summaryFont = new Font("Microsoft YaHei", 6.8F, FontStyle.Regular))
            using (SolidBrush summaryBrush = new SolidBrush(NodeMetaTextColor))
            using (StringFormat textFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                RectangleF summaryRect = new RectangleF(bounds.X + 14, bounds.Y + 55, bounds.Width - 28, 16);
                graphics.DrawString(summary, summaryFont, summaryBrush, summaryRect, textFormat);
            }
        }

        private void DrawConnection(Graphics graphics, ProcessConnection connection)
        {
            NodeBase fromNode = FindNode(connection.FromNodeId);
            NodeBase toNode = FindNode(connection.ToNodeId);
            if (fromNode == null || toNode == null)
                return;

            DrawConnection(graphics, connection, fromNode, toNode);
        }

        /// <summary>
        /// 使用已查找到的端点节点绘制连线，避免绘制循环中重复查找节点。
        /// </summary>
        /// <param name="graphics">画布绘图对象。</param>
        /// <param name="connection">流程连线。</param>
        /// <param name="fromNode">起点节点。</param>
        /// <param name="toNode">终点节点。</param>
        private void DrawConnection(Graphics graphics, ProcessConnection connection, NodeBase fromNode, NodeBase toNode)
        {
            FlowNodeRelationRole relationRole = GetConnectionRelationRole(connection);
            bool selectedConnection = connection == _selectedConnection;
            bool relationConnection = relationRole != FlowNodeRelationRole.None;
            Color connectionColor = selectedConnection
                ? Color.FromArgb(90, 190, 255)
                : GetConnectionHighlightColor(connection.Branch, relationRole);
            Point[] route = CreateRoutedConnectionPoints(connection, fromNode, toNode);

            DrawArrowPath(graphics, route, connectionColor, false, selectedConnection, relationConnection);
            DrawConnectionBranchLabel(graphics, connection, route, connectionColor);
        }

        private void DrawTemporaryConnection(Graphics graphics)
        {
            Point start = GetAnchorCenter(_connectionStartNode, _connectionStartAnchor);
            ProcessConnectionBranch branch = GetConnectionBranch(_connectionStartNode, _connectionStartAnchor);
            Point[] route = CreateRoutedConnectionPoints(
                start,
                _connectionStartAnchor,
                _connectionEndPoint,
                null,
                _connectionStartNode,
                null);

            DrawArrowPath(graphics, route, GetConnectionColor(branch), true, false);
        }

        private Point[] CreateRoutedConnectionPoints(
            Point start,
            string startAnchor,
            Point end,
            string endAnchor,
            NodeBase sourceNode,
            NodeBase targetNode)
        {
            return CanvasPathRouter.Route(
                start,
                startAnchor,
                end,
                endAnchor,
                GetConnectionObstacles(),
                sourceNode == null ? (Rectangle?)null : GetNodeBounds(sourceNode),
                targetNode == null ? (Rectangle?)null : GetNodeBounds(targetNode));
        }

        /// <summary>
        /// 获取持久连线的路由点。只有连线自身两端节点变化时才重新寻路。
        /// </summary>
        /// <param name="connection">流程连接。</param>
        /// <param name="sourceNode">源节点。</param>
        /// <param name="targetNode">目标节点。</param>
        /// <returns>连线折线路由点。</returns>
        private Point[] CreateRoutedConnectionPoints(
            ProcessConnection connection,
            NodeBase sourceNode,
            NodeBase targetNode)
        {
            if (connection == null || sourceNode == null || targetNode == null)
                return new Point[0];

            if (ShouldUseInteractiveConnectionRoute(connection))
            {
                return CreateInteractiveConnectionPoints(
                    GetAnchorCenter(sourceNode, connection.FromAnchor),
                    connection.FromAnchor,
                    GetAnchorCenter(targetNode, connection.ToAnchor),
                    connection.ToAnchor);
            }

            ConnectionRouteCache cache;
            if (TryGetReusableConnectionRouteCache(connection, sourceNode, targetNode, out cache))
                return cache.Points;

            Point start = GetAnchorCenter(sourceNode, connection.FromAnchor);
            Point end = GetAnchorCenter(targetNode, connection.ToAnchor);
            Rectangle sourceBounds = GetNodeBounds(sourceNode);
            Rectangle targetBounds = GetNodeBounds(targetNode);
            string cacheKey = GetConnectionRouteCacheKey(connection);

            Point[] route = CreateRoutedConnectionPoints(
                start,
                connection.FromAnchor,
                end,
                connection.ToAnchor,
                sourceNode,
                targetNode);

            if (!string.IsNullOrEmpty(cacheKey))
            {
                _connectionRouteCache[cacheKey] = new ConnectionRouteCache(
                    start,
                    connection.FromAnchor,
                    end,
                    connection.ToAnchor,
                    sourceBounds,
                    targetBounds,
                    route);
            }

            return route;
        }

        /// <summary>
        /// 在节点拖拽开始前预热当前连线路由，确保无关连线拖拽期间保持稳定。
        /// </summary>
        private void EnsureConnectionRoutesCached()
        {
            if (_process == null)
                return;

            Rectangle visibleCanvasBounds = GetVisibleCanvasBounds();
            foreach (ProcessConnection connection in _process.Connections)
            {
                NodeBase fromNode = FindNode(connection.FromNodeId);
                NodeBase toNode = FindNode(connection.ToNodeId);
                if (fromNode == null || toNode == null)
                    continue;

                if (!ShouldDrawConnection(connection, fromNode, toNode, visibleCanvasBounds))
                    continue;

                CreateRoutedConnectionPoints(connection, fromNode, toNode);
            }
        }

        /// <summary>
        /// 尝试获取端点仍然匹配的持久连线路由缓存。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <param name="sourceNode">起点节点。</param>
        /// <param name="targetNode">终点节点。</param>
        /// <param name="cache">命中的路由缓存。</param>
        /// <returns>缓存可复用时返回 true。</returns>
        /// <summary>
        /// 判断连线是否属于当前拖拽节点，拖拽时这类连线使用轻量预览路径。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <returns>连线端点包含正在拖拽的节点时返回 true。</returns>
        private bool ShouldUseInteractiveConnectionRoute(ProcessConnection connection)
        {
            return _dragNode != null &&
                connection != null &&
                (_draggingNodeIds.Contains(connection.FromNodeId) ||
                 _draggingNodeIds.Contains(connection.ToNodeId));
        }

        /// <summary>
        /// 创建拖拽过程中的轻量折线路径，不做全节点避障以保证鼠标移动流畅。
        /// </summary>
        /// <param name="start">连线起点。</param>
        /// <param name="startAnchor">起点锚点。</param>
        /// <param name="end">连线终点。</param>
        /// <param name="endAnchor">终点锚点。</param>
        /// <returns>用于绘制的折线路径点。</returns>
        private static Point[] CreateInteractiveConnectionPoints(
            Point start,
            string startAnchor,
            Point end,
            string endAnchor)
        {
            const int previewStep = 18;
            Point sourceStep = StepPointFromAnchor(start, startAnchor, previewStep);
            Point targetStep = StepPointFromAnchor(end, endAnchor, previewStep);
            List<Point> points = new List<Point> { start, sourceStep };

            if (sourceStep.X == targetStep.X || sourceStep.Y == targetStep.Y)
            {
                points.Add(targetStep);
            }
            else if (IsHorizontalAnchor(startAnchor))
            {
                int middleX = sourceStep.X + ((targetStep.X - sourceStep.X) / 2);
                points.Add(new Point(middleX, sourceStep.Y));
                points.Add(new Point(middleX, targetStep.Y));
                points.Add(targetStep);
            }
            else
            {
                int middleY = sourceStep.Y + ((targetStep.Y - sourceStep.Y) / 2);
                points.Add(new Point(sourceStep.X, middleY));
                points.Add(new Point(targetStep.X, middleY));
                points.Add(targetStep);
            }

            points.Add(end);
            return SimplifyInteractiveConnectionPoints(points);
        }

        /// <summary>
        /// 根据锚点方向向外推出一个折线路径点。
        /// </summary>
        /// <param name="point">锚点中心。</param>
        /// <param name="anchor">锚点名称。</param>
        /// <param name="distance">推出距离。</param>
        /// <returns>推出后的点。</returns>
        private static Point StepPointFromAnchor(Point point, string anchor, int distance)
        {
            switch (anchor)
            {
                case "Top":
                    return new Point(point.X, point.Y - distance);
                case "Bottom":
                    return new Point(point.X, point.Y + distance);
                case "Left":
                    return new Point(point.X - distance, point.Y);
                case "Right":
                    return new Point(point.X + distance, point.Y);
                default:
                    return point;
            }
        }

        /// <summary>
        /// 判断锚点方向是否为水平出线。
        /// </summary>
        /// <param name="anchor">锚点名称。</param>
        /// <returns>左/右锚点返回 true。</returns>
        private static bool IsHorizontalAnchor(string anchor)
        {
            return anchor == "Left" || anchor == "Right";
        }

        /// <summary>
        /// 移除拖拽预览折线中的重复点和共线中间点。
        /// </summary>
        /// <param name="points">原始路径点。</param>
        /// <returns>简化后的路径点。</returns>
        private static Point[] SimplifyInteractiveConnectionPoints(List<Point> points)
        {
            if (points == null || points.Count <= 2)
                return points == null ? new Point[0] : points.ToArray();

            List<Point> result = new List<Point> { points[0] };
            for (int index = 1; index < points.Count - 1; index++)
            {
                Point previous = result[result.Count - 1];
                Point current = points[index];
                Point next = points[index + 1];
                if (current == previous)
                    continue;

                bool sameX = previous.X == current.X && current.X == next.X;
                bool sameY = previous.Y == current.Y && current.Y == next.Y;
                if (!sameX && !sameY)
                    result.Add(current);
            }

            if (result[result.Count - 1] != points[points.Count - 1])
                result.Add(points[points.Count - 1]);

            return result.ToArray();
        }

        /// <summary>
        /// 尝试获取端点仍然匹配的持久连线路由缓存。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <param name="sourceNode">起点节点。</param>
        /// <param name="targetNode">终点节点。</param>
        /// <param name="cache">命中的路由缓存。</param>
        /// <returns>缓存可复用时返回 true。</returns>
        private bool TryGetReusableConnectionRouteCache(
            ProcessConnection connection,
            NodeBase sourceNode,
            NodeBase targetNode,
            out ConnectionRouteCache cache)
        {
            cache = null;
            if (connection == null || sourceNode == null || targetNode == null)
                return false;

            string cacheKey = GetConnectionRouteCacheKey(connection);
            if (string.IsNullOrEmpty(cacheKey) ||
                !_connectionRouteCache.TryGetValue(cacheKey, out cache))
            {
                cache = null;
                return false;
            }

            Point start = GetAnchorCenter(sourceNode, connection.FromAnchor);
            Point end = GetAnchorCenter(targetNode, connection.ToAnchor);
            Rectangle sourceBounds = GetNodeBounds(sourceNode);
            Rectangle targetBounds = GetNodeBounds(targetNode);
            if (cache.Matches(
                start,
                connection.FromAnchor,
                end,
                connection.ToAnchor,
                sourceBounds,
                targetBounds))
            {
                return true;
            }

            cache = null;
            return false;
        }

        /// <summary>
        /// 删除指定连线的路由缓存。
        /// </summary>
        /// <param name="connection">流程连接。</param>
        private void RemoveConnectionRouteCache(ProcessConnection connection)
        {
            string cacheKey = GetConnectionRouteCacheKey(connection);
            if (!string.IsNullOrEmpty(cacheKey))
                _connectionRouteCache.Remove(cacheKey);
        }

        /// <summary>
        /// 生成连线路由缓存键。
        /// </summary>
        /// <param name="connection">流程连接。</param>
        /// <returns>缓存键。</returns>
        private static string GetConnectionRouteCacheKey(ProcessConnection connection)
        {
            if (connection == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(connection.ID))
                return connection.ID;

            return connection.FromNodeId + ":" +
                connection.FromAnchor + ">" +
                connection.ToNodeId + ":" +
                connection.ToAnchor + ":" +
                connection.Branch;
        }

        private List<Rectangle> GetConnectionObstacles()
        {
            if (_process == null)
                return _connectionObstacleCache;

            if (!_connectionObstacleCacheDirty)
                return _connectionObstacleCache;

            _connectionObstacleCache.Clear();
            foreach (NodeBase node in _process.Nodes)
                _connectionObstacleCache.Add(GetNodeBounds(node));

            _connectionObstacleCacheDirty = false;
            return _connectionObstacleCache;
        }

        private void DrawArrowPath(Graphics graphics, Point[] points, Color color, bool dashed, bool selected, bool highlighted = false)
        {
            if (points == null || points.Length < 2)
                return;

            using (AdjustableArrowCap arrow = new AdjustableArrowCap(5, 5))
            using (Pen pen = new Pen(color, selected ? 3.2F : highlighted ? 3.0F : 2.4F))
            using (GraphicsPath path = CreateConnectionGraphicsPath(points))
            {
                pen.CustomEndCap = arrow;
                pen.StartCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (dashed)
                    pen.DashStyle = DashStyle.Dash;

                graphics.DrawPath(pen, path);
            }
        }

        private GraphicsPath CreateConnectionGraphicsPath(Point[] points)
        {
            GraphicsPath path = new GraphicsPath();
            if (points == null || points.Length < 2)
                return path;

            for (int index = 0; index < points.Length - 1; index++)
            {
                if (points[index] != points[index + 1])
                    path.AddLine(points[index], points[index + 1]);
            }

            return path;
        }

        private void DrawAnchor(Graphics graphics, NodeBase node, string anchorName, string branchLabel = null)
        {
            Rectangle anchor = GetAnchorBounds(node, anchorName);
            bool alertStatus = IsNodeAlertStatus(node);
            Color borderColor = alertStatus
                ? GetNodeStatusColor(node)
                : node.Selected ? CanvasAccentColor : Color.FromArgb(90, 190, 255);
            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen border = new Pen(borderColor, alertStatus ? 2.1F : 1.4F))
            {
                graphics.FillEllipse(fill, anchor);
                graphics.DrawEllipse(border, anchor);
            }

            if (!string.IsNullOrEmpty(branchLabel))
            {
                using (Font branchFont = new Font("Segoe UI", 6.5F, FontStyle.Bold))
                using (SolidBrush branchBrush = new SolidBrush(Color.FromArgb(220, 229, 239)))
                {
                    Point labelLocation = anchorName == "Bottom"
                        ? new Point(anchor.X + AnchorSize + 2, anchor.Y)
                        : new Point(anchor.X + 1, anchor.Y - 13);
                    graphics.DrawString(branchLabel, branchFont, branchBrush, labelLocation);
                }
            }
        }

        private Rectangle GetNodeBounds(NodeBase node)
        {
            return new Rectangle(node.CanvasLocation, GetNodeSize(node));
        }

        private void InvalidateNode(NodeBase node)
        {
            if (node == null)
                return;

            Rectangle bounds = GetNodeBounds(node);
            bounds.Inflate(72, 36);
            Rectangle scaledBounds = ToControlRectangle(bounds);
            scaledBounds.Inflate(2, 2);
            Invalidate(scaledBounds);
        }

        private Size GetNodeSize(NodeBase node)
        {
            if (node == null)
                return DefaultCanvasNodeSize;

            if (node.CanvasSize.Width <= 0 || node.CanvasSize.Height <= 0)
                return DefaultCanvasNodeSize;

            Size size = node.CanvasSize;
            if (!string.IsNullOrWhiteSpace(node.GetCanvasParameterSummary()))
                size.Height = Math.Max(size.Height, 84);

            return size;
        }

        private Rectangle GetAnchorBounds(NodeBase node, string anchorName)
        {
            Rectangle nodeBounds = GetNodeBounds(node);
            int half = AnchorSize / 2;
            switch (anchorName)
            {
                case "Top":
                    return new Rectangle(nodeBounds.X + (nodeBounds.Width / 2) - half, nodeBounds.Y - half, AnchorSize, AnchorSize);
                case "Bottom":
                    return new Rectangle(nodeBounds.X + (nodeBounds.Width / 2) - half, nodeBounds.Bottom - half, AnchorSize, AnchorSize);
                case "Left":
                    return new Rectangle(nodeBounds.X - half, nodeBounds.Y + (nodeBounds.Height / 2) - half, AnchorSize, AnchorSize);
                default:
                    return new Rectangle(nodeBounds.Right - half, nodeBounds.Y + (nodeBounds.Height / 2) - half, AnchorSize, AnchorSize);
            }
        }

        private Point GetAnchorCenter(NodeBase node, string anchorName)
        {
            Rectangle bounds = GetAnchorBounds(node, string.IsNullOrEmpty(anchorName) ? "Right" : anchorName);
            return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        }

        private bool TryHitAnchor(Point canvasPoint, out NodeBase hitNode, out string hitAnchor)
        {
            hitNode = null;
            hitAnchor = null;

            if (_process == null)
                return false;

            for (int i = _process.Nodes.Count - 1; i >= 0; i--)
            {
                NodeBase node = _process.Nodes[i];
                foreach (string anchor in new[] { "Top", "Right", "Bottom", "Left" })
                {
                    Rectangle hitBounds = GetAnchorBounds(node, anchor);
                    hitBounds.Inflate(4, 4);
                    if (hitBounds.Contains(canvasPoint))
                    {
                        hitNode = node;
                        hitAnchor = anchor;
                        return true;
                    }
                }
            }

            return false;
        }

        private NodeBase HitNode(Point canvasPoint)
        {
            if (_process == null)
                return null;

            for (int i = _process.Nodes.Count - 1; i >= 0; i--)
            {
                NodeBase node = _process.Nodes[i];
                if (GetNodeBounds(node).Contains(canvasPoint))
                    return node;
            }

            return null;
        }

        private ProcessConnection HitConnection(Point canvasPoint)
        {
            if (_process == null)
                return null;

            using (Pen hitPen = new Pen(Color.Black, 10F))
            {
                for (int i = _process.Connections.Count - 1; i >= 0; i--)
                {
                    ProcessConnection connection = _process.Connections[i];
                    NodeBase fromNode = FindNode(connection.FromNodeId);
                    NodeBase toNode = FindNode(connection.ToNodeId);
                    if (fromNode == null || toNode == null)
                        continue;

                    Rectangle hitBounds = GetConnectionCullBounds(connection, fromNode, toNode);
                    hitBounds.Inflate(12, 12);
                    if (!hitBounds.Contains(canvasPoint))
                        continue;

                    Point[] route = CreateRoutedConnectionPoints(connection, fromNode, toNode);

                    using (GraphicsPath path = CreateConnectionGraphicsPath(route))
                    {
                        if (path.IsOutlineVisible(canvasPoint, hitPen))
                            return connection;
                    }
                }
            }

            return null;
        }

        private NodeBase FindNode(int nodeId)
        {
            if (_process == null)
                return null;

            EnsureNodeLookup();
            NodeBase node;
            return _nodeLookup.TryGetValue(nodeId, out node) ? node : null;
        }

        /// <summary>
        /// 确保节点 ID 查找表已经按当前流程节点集合生成。
        /// </summary>
        private void EnsureNodeLookup()
        {
            if (!_nodeLookupDirty)
                return;

            _nodeLookup.Clear();
            if (_process != null)
            {
                foreach (NodeBase node in _process.Nodes)
                {
                    if (node != null && !_nodeLookup.ContainsKey(node.ID))
                        _nodeLookup.Add(node.ID, node);
                }
            }

            _nodeLookupDirty = false;
        }

        private int GetNodeIndex(NodeBase node)
        {
            if (_process == null)
                return 0;

            int index = _process.Nodes.IndexOf(node);
            return index < 0 ? _process.Nodes.Count : index;
        }

        private bool HasConnection(int fromNodeId, int toNodeId, ProcessConnectionBranch branch)
        {
            foreach (ProcessConnection connection in _process.Connections)
            {
                if (connection.FromNodeId == fromNodeId &&
                    connection.ToNodeId == toNodeId &&
                    connection.Branch == branch)
                    return true;
            }

            return false;
        }

        private void SelectOnly(NodeBase selectedNode)
        {
            _selectedConnection = null;
            _selectedPreviewImageKey = null;
            DisposePreviewCache();
            foreach (NodeBase node in _process.Nodes)
                node.Selected = node == selectedNode;
            RefreshSelectedRelationHighlights();
            InvalidateMinimapCache();
            RequestOverlayInvalidate();
        }

        private void SelectOnly(ProcessConnection selectedConnection)
        {
            _selectedConnection = selectedConnection;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;
            ClearSelectedRelationHighlights();
            InvalidateMinimapCache();
            RequestOverlayInvalidate();
        }

        private void ClearSelection()
        {
            if (_process == null)
                return;

            _selectedConnection = null;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;
            ClearSelectedRelationHighlights();
            InvalidateMinimapCache();
            RequestOverlayInvalidate();
        }

        private FlowCanvasContextMenuTarget SelectAtPoint(Point canvasPoint, bool preserveSelectedNodeGroup = false)
        {
            NodeBase node = HitNode(canvasPoint);
            if (node != null)
            {
                if (!preserveSelectedNodeGroup || !node.Selected)
                    SelectOnly(node);
                else
                    RefreshSelectedRelationHighlights();

                return FlowCanvasContextMenuTarget.Node;
            }

            ProcessConnection connection = HitConnection(canvasPoint);
            if (connection != null)
            {
                SelectOnly(connection);
                return FlowCanvasContextMenuTarget.Connection;
            }

            ClearSelection();
            return FlowCanvasContextMenuTarget.Canvas;
        }

        private Point ClampLocation(Point location, Size size)
        {
            int maxX = Math.Max(0, LogicalCanvasSize.Width - size.Width - 24);
            int maxY = Math.Max(0, LogicalCanvasSize.Height - size.Height - 24);
            int x = Math.Max(24, Math.Min(maxX, location.X));
            int y = Math.Max(24, Math.Min(maxY, location.Y));
            return new Point(x, y);
        }

        private static Point SnapPointToGrid(Point location)
        {
            return new Point(
                SnapCoordinateToGrid(location.X),
                SnapCoordinateToGrid(location.Y));
        }

        private static int SnapCoordinateToGrid(int value)
        {
            return (int)Math.Round(value / (double)GridSize, MidpointRounding.AwayFromZero) * GridSize;
        }

        private Point ToCanvasPoint(Point controlPoint)
        {
            return new Point(
                (int)Math.Round((controlPoint.X - AutoScrollPosition.X) / _zoomFactor),
                (int)Math.Round((controlPoint.Y - AutoScrollPosition.Y) / _zoomFactor));
        }

        private Rectangle ToControlRectangle(Rectangle canvasBounds)
        {
            int x = AutoScrollPosition.X + (int)Math.Floor(canvasBounds.X * _zoomFactor);
            int y = AutoScrollPosition.Y + (int)Math.Floor(canvasBounds.Y * _zoomFactor);
            int width = (int)Math.Ceiling(canvasBounds.Width * _zoomFactor);
            int height = (int)Math.Ceiling(canvasBounds.Height * _zoomFactor);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 将控件剪裁区域换算为画布坐标区域，并扩展绘制余量。
        /// </summary>
        /// <param name="controlBounds">控件坐标下的剪裁区域。</param>
        /// <returns>画布坐标下需要绘制的区域。</returns>
        private Rectangle GetCanvasBoundsFromControlRectangle(Rectangle controlBounds)
        {
            if (controlBounds.Width <= 0 || controlBounds.Height <= 0)
                return Rectangle.Empty;

            float zoom = Math.Max(0.01F, _zoomFactor);
            int left = (int)Math.Floor((controlBounds.Left - AutoScrollPosition.X) / zoom) - ViewportDrawPadding;
            int top = (int)Math.Floor((controlBounds.Top - AutoScrollPosition.Y) / zoom) - ViewportDrawPadding;
            int right = (int)Math.Ceiling((controlBounds.Right - AutoScrollPosition.X) / zoom) + ViewportDrawPadding;
            int bottom = (int)Math.Ceiling((controlBounds.Bottom - AutoScrollPosition.Y) / zoom) + ViewportDrawPadding;
            Rectangle canvasBounds = Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
            return Rectangle.Intersect(new Rectangle(Point.Empty, LogicalCanvasSize), canvasBounds);
        }

        /// <summary>
        /// 获取当前客户区对应的画布可视区域。
        /// </summary>
        /// <returns>画布坐标下的可视区域。</returns>
        private Rectangle GetVisibleCanvasBounds()
        {
            return GetCanvasBoundsFromControlRectangle(ClientRectangle);
        }

        /// <summary>
        /// 判断节点绘制范围是否和当前剪裁区域相交。
        /// </summary>
        /// <param name="node">待绘制节点。</param>
        /// <param name="visibleCanvasBounds">当前剪裁区域。</param>
        /// <returns>节点需要绘制时返回 true。</returns>
        private bool ShouldDrawNode(NodeBase node, Rectangle visibleCanvasBounds)
        {
            if (node == null || visibleCanvasBounds.IsEmpty)
                return false;

            return GetNodeVisualBounds(node).IntersectsWith(visibleCanvasBounds);
        }

        /// <summary>
        /// 判断连线绘制范围是否和当前剪裁区域相交。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <param name="fromNode">起点节点。</param>
        /// <param name="toNode">终点节点。</param>
        /// <param name="visibleCanvasBounds">当前剪裁区域。</param>
        /// <returns>连线需要绘制时返回 true。</returns>
        private bool ShouldDrawConnection(
            ProcessConnection connection,
            NodeBase fromNode,
            NodeBase toNode,
            Rectangle visibleCanvasBounds)
        {
            if (connection == null || fromNode == null || toNode == null || visibleCanvasBounds.IsEmpty)
                return false;

            Rectangle bounds = GetConnectionCullBounds(connection, fromNode, toNode);
            return !bounds.IsEmpty && bounds.IntersectsWith(visibleCanvasBounds);
        }

        /// <summary>
        /// 获取用于连线可视区域裁剪的近似边界，优先复用已有路由缓存。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <param name="fromNode">起点节点。</param>
        /// <param name="toNode">终点节点。</param>
        /// <returns>连线在画布坐标下的绘制边界。</returns>
        private Rectangle GetConnectionCullBounds(ProcessConnection connection, NodeBase fromNode, NodeBase toNode)
        {
            ConnectionRouteCache cache;
            if (TryGetReusableConnectionRouteCache(connection, fromNode, toNode, out cache))
                return InflateCanvasBounds(cache.Bounds, 56, 56);

            Point start = GetAnchorCenter(fromNode, connection.FromAnchor);
            Point end = GetAnchorCenter(toNode, connection.ToAnchor);
            Rectangle bounds = CreatePointBounds(new[] { start, end });
            return InflateCanvasBounds(bounds, 160, 160);
        }

        /// <summary>
        /// 将画布区域转换为控件区域后触发局部刷新。
        /// </summary>
        /// <param name="canvasBounds">需要刷新的画布坐标区域。</param>
        private void InvalidateCanvasBounds(Rectangle canvasBounds)
        {
            if (canvasBounds.IsEmpty)
                return;

            Rectangle controlBounds = ToControlRectangle(canvasBounds);
            controlBounds.Inflate(6, 6);
            controlBounds.Intersect(ClientRectangle);
            if (!controlBounds.IsEmpty)
                Invalidate(controlBounds);
        }

        /// <summary>
        /// 合并两个画布区域，自动跳过空区域。
        /// </summary>
        /// <param name="first">第一个区域。</param>
        /// <param name="second">第二个区域。</param>
        /// <returns>合并后的区域。</returns>
        private static Rectangle UnionCanvasBounds(Rectangle first, Rectangle second)
        {
            if (first.IsEmpty)
                return second;

            if (second.IsEmpty)
                return first;

            return Rectangle.Union(first, second);
        }

        /// <summary>
        /// 将区域按指定余量扩展，空区域保持为空。
        /// </summary>
        /// <param name="bounds">待扩展区域。</param>
        /// <param name="horizontalPadding">水平扩展量。</param>
        /// <param name="verticalPadding">垂直扩展量。</param>
        /// <returns>扩展后的区域。</returns>
        private static Rectangle InflateCanvasBounds(Rectangle bounds, int horizontalPadding, int verticalPadding)
        {
            if (bounds.IsEmpty)
                return bounds;

            bounds.Inflate(horizontalPadding, verticalPadding);
            return bounds;
        }

        /// <summary>
        /// 将一段区域合入目标区域，目标为空时直接赋值。
        /// </summary>
        /// <param name="target">目标区域。</param>
        /// <param name="source">待合并区域。</param>
        private static void IncludeCanvasBounds(ref Rectangle target, Rectangle source)
        {
            if (source.IsEmpty)
                return;

            target = target.IsEmpty ? source : Rectangle.Union(target, source);
        }

        /// <summary>
        /// 计算点集的最小外接矩形。
        /// </summary>
        /// <param name="points">画布坐标点集。</param>
        /// <returns>点集边界。</returns>
        private static Rectangle CreatePointBounds(Point[] points)
        {
            if (points == null || points.Length == 0)
                return Rectangle.Empty;

            int minX = points[0].X;
            int minY = points[0].Y;
            int maxX = points[0].X;
            int maxY = points[0].Y;
            for (int index = 1; index < points.Length; index++)
            {
                minX = Math.Min(minX, points[index].X);
                minY = Math.Min(minY, points[index].Y);
                maxX = Math.Max(maxX, points[index].X);
                maxY = Math.Max(maxY, points[index].Y);
            }

            return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        /// <summary>
        /// 获取节点含阴影和锚点的绘制区域。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <returns>节点绘制区域。</returns>
        private Rectangle GetNodeVisualBounds(NodeBase node)
        {
            Rectangle bounds = GetNodeBounds(node);
            bounds.Inflate(80, 48);
            return bounds;
        }

        /// <summary>
        /// 获取当前拖动批次需要重绘的画布区域。
        /// </summary>
        /// <returns>拖动节点、相关连线和辅助线的合并区域。</returns>
        private Rectangle GetDragInvalidationBounds()
        {
            Rectangle bounds = Rectangle.Empty;
            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                if (snapshot.Node != null)
                    IncludeCanvasBounds(ref bounds, GetNodeVisualBounds(snapshot.Node));
            }

            foreach (ProcessConnection connection in _dragAffectedConnections)
                IncludeCanvasBounds(ref bounds, GetConnectionVisualBounds(connection));

            IncludeCanvasBounds(ref bounds, GetAlignmentGuidesVisualBounds());
            return bounds;
        }

        /// <summary>
        /// 获取当前框选区域需要重绘的画布区域。
        /// </summary>
        /// <returns>框选矩形扩展后的区域。</returns>
        private Rectangle GetSelectionInvalidationBounds()
        {
            Rectangle bounds = GetSelectionRectangle();
            return InflateCanvasBounds(bounds, 96, 96);
        }

        /// <summary>
        /// 获取临时连线当前需要重绘的画布区域。
        /// </summary>
        /// <returns>临时连线路由区域。</returns>
        private Rectangle GetTemporaryConnectionVisualBounds()
        {
            if (_connectionStartNode == null)
                return Rectangle.Empty;

            Point start = GetAnchorCenter(_connectionStartNode, _connectionStartAnchor);
            ProcessConnectionBranch branch = GetConnectionBranch(_connectionStartNode, _connectionStartAnchor);
            Point[] route = CreateRoutedConnectionPoints(
                start,
                _connectionStartAnchor,
                _connectionEndPoint,
                null,
                _connectionStartNode,
                null);
            Rectangle bounds = CreatePointBounds(route);
            return InflateCanvasBounds(bounds, branch == ProcessConnectionBranch.Default ? 48 : 64, 48);
        }

        /// <summary>
        /// 获取指定连线含端帽和标签的绘制区域。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <returns>连线绘制区域。</returns>
        private Rectangle GetConnectionVisualBounds(ProcessConnection connection)
        {
            NodeBase fromNode = FindNode(connection.FromNodeId);
            NodeBase toNode = FindNode(connection.ToNodeId);
            if (fromNode == null || toNode == null)
                return Rectangle.Empty;

            Point[] route = CreateRoutedConnectionPoints(connection, fromNode, toNode);
            Rectangle bounds = CreatePointBounds(route);
            return InflateCanvasBounds(bounds, connection.Branch == ProcessConnectionBranch.Default ? 56 : 76, 56);
        }

        /// <summary>
        /// 获取当前所有对齐辅助线的绘制区域。
        /// </summary>
        /// <returns>辅助线合并区域。</returns>
        private Rectangle GetAlignmentGuidesVisualBounds()
        {
            Rectangle bounds = Rectangle.Empty;
            foreach (AlignmentGuide guide in _alignmentGuides)
            {
                Rectangle guideBounds = guide.Axis == CanvasAlignmentAxis.Horizontal
                    ? Rectangle.FromLTRB(guide.Start, guide.Coordinate - 3, guide.End + 1, guide.Coordinate + 4)
                    : Rectangle.FromLTRB(guide.Coordinate - 3, guide.Start, guide.Coordinate + 4, guide.End + 1);
                IncludeCanvasBounds(ref bounds, guideBounds);
            }

            return InflateCanvasBounds(bounds, 16, 16);
        }

        private bool OpenNodeParameters(NodeBase node)
        {
            Form form = node == null ? null : node.ParamForm as Form;
            if (node == null || !node.Active || form == null)
                return false;

            form.ShowDialog();
            // 参数保存或取消后统一重读当前定义；刷新是轻量操作且不运行算法，
            // 可覆盖所有节点参数窗口而无需各窗体重复维护通知代码。
            node.NotifyOutputDefinitionChanged();
            InvalidateNode(node);
            RequestOverlayInvalidate(false, true);
            return true;
        }

        private void DrawConnectionBranchLabel(
            Graphics graphics,
            ProcessConnection connection,
            Point[] route,
            Color color)
        {
            if (connection.Branch == ProcessConnectionBranch.Default)
                return;

            string label = connection.Branch == ProcessConnectionBranch.True ? "T" : "F";
            Point center = GetRouteMidPoint(route);
            Rectangle badge = new Rectangle(center.X - 9, center.Y - 9, 18, 18);
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(245, 248, 252)))
            using (Pen border = new Pen(color, 1.2F))
            using (Font font = new Font("Segoe UI", 6.5F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(color))
            {
                graphics.FillEllipse(fill, badge);
                graphics.DrawEllipse(border, badge);
                graphics.DrawString(label, font, textBrush, badge.X + 4, badge.Y + 2);
            }
        }

        private Point GetRouteMidPoint(Point[] route)
        {
            if (route == null || route.Length == 0)
                return Point.Empty;

            if (route.Length == 1)
                return route[0];

            int totalLength = 0;
            for (int index = 0; index < route.Length - 1; index++)
            {
                totalLength += Math.Abs(route[index + 1].X - route[index].X) +
                    Math.Abs(route[index + 1].Y - route[index].Y);
            }

            if (totalLength <= 0)
                return route[route.Length / 2];

            int targetLength = totalLength / 2;
            int walked = 0;
            for (int index = 0; index < route.Length - 1; index++)
            {
                Point start = route[index];
                Point end = route[index + 1];
                int segmentLength = Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y);
                if (segmentLength <= 0)
                    continue;

                if (walked + segmentLength >= targetLength)
                {
                    int remaining = targetLength - walked;
                    int directionX = Math.Sign(end.X - start.X);
                    int directionY = Math.Sign(end.Y - start.Y);
                    return new Point(
                        start.X + (directionX * remaining),
                        start.Y + (directionY * remaining));
                }

                walked += segmentLength;
            }

            return route[route.Length - 1];
        }

        private ProcessConnectionBranch GetConnectionBranch(NodeBase fromNode, string fromAnchor)
        {
            if (!IsConditionalBranchNode(fromNode))
                return ProcessConnectionBranch.Default;

            if (fromAnchor == "Right")
                return ProcessConnectionBranch.True;

            if (fromAnchor == "Bottom")
                return ProcessConnectionBranch.False;

            return ProcessConnectionBranch.Default;
        }

        private Color GetConnectionColor(ProcessConnectionBranch branch)
        {
            switch (branch)
            {
                case ProcessConnectionBranch.True:
                    return Color.FromArgb(80, 210, 120);
                case ProcessConnectionBranch.False:
                    return Color.FromArgb(255, 144, 84);
                default:
                    return Color.FromArgb(196, 205, 216);
            }
        }

        /// <summary>
        /// 获取连线在当前选中节点视角下的输入输出关系。
        /// </summary>
        /// <param name="connection">流程连线。</param>
        /// <returns>连线关系类型。</returns>
        private FlowNodeRelationRole GetConnectionRelationRole(ProcessConnection connection)
        {
            if (connection == null)
                return FlowNodeRelationRole.None;

            if (_selectedInputConnections.Contains(connection))
                return FlowNodeRelationRole.Input;

            if (_selectedOutputConnections.Contains(connection))
                return FlowNodeRelationRole.Output;

            return FlowNodeRelationRole.None;
        }

        /// <summary>
        /// 获取连线颜色，优先显示当前选中节点的输入输出关系。
        /// </summary>
        /// <param name="branch">连线分支类型。</param>
        /// <param name="relationRole">连线关系类型。</param>
        /// <returns>用于绘制连线的颜色。</returns>
        private Color GetConnectionHighlightColor(ProcessConnectionBranch branch, FlowNodeRelationRole relationRole)
        {
            if (relationRole == FlowNodeRelationRole.Input)
                return RelationInputColor;

            if (relationRole == FlowNodeRelationRole.Output)
                return RelationOutputColor;

            return GetConnectionColor(branch);
        }

        /// <summary>
        /// 获取节点在当前单选节点视角下的输入输出关系。
        /// </summary>
        /// <param name="node">需要判断的节点。</param>
        /// <returns>节点关系类型。</returns>
        private FlowNodeRelationRole GetNodeRelationRole(NodeBase node)
        {
            if (node == null || node.Selected)
                return FlowNodeRelationRole.None;

            if (_selectedInputNodeIds.Contains(node.ID))
                return FlowNodeRelationRole.Input;

            if (_selectedOutputNodeIds.Contains(node.ID))
                return FlowNodeRelationRole.Output;

            return FlowNodeRelationRole.None;
        }

        private Color GetNodeFillColor(NodeBase node)
        {
            if (!node.Active)
                return NodeDisabledFillColor;

            if (IsNodeResultNg(node))
                return Color.FromArgb(255, 247, 232);

            switch (node.RuntimeStatus)
            {
                case NodeStatus.Running:
                    return Color.FromArgb(255, 246, 221);
                case NodeStatus.Successful:
                    return Color.FromArgb(232, 250, 238);
                case NodeStatus.Failed:
                    return Color.FromArgb(255, 236, 236);
                default:
                    return NodeFillColor;
            }
        }

        private Color GetNodeStatusColor(NodeBase node)
        {
            if (!node.Active)
                return Color.FromArgb(134, 151, 171);

            if (IsNodeResultNg(node))
                return Color.FromArgb(245, 124, 0);

            switch (node.RuntimeStatus)
            {
                case NodeStatus.Running:
                    return Color.FromArgb(245, 159, 0);
                case NodeStatus.Successful:
                    return Color.FromArgb(47, 158, 68);
                case NodeStatus.Failed:
                    return Color.FromArgb(224, 49, 49);
                default:
                    return Color.FromArgb(134, 151, 171);
            }
        }

        private Color GetNodeBorderColor(NodeBase node)
        {
            FlowNodeRelationRole relationRole = GetNodeRelationRole(node);
            if (relationRole == FlowNodeRelationRole.Input)
                return RelationInputColor;

            if (relationRole == FlowNodeRelationRole.Output)
                return RelationOutputColor;

            if (node.RuntimeStatus == NodeStatus.Failed)
                return Color.FromArgb(255, 68, 68);

            if (IsNodeResultNg(node))
                return Color.FromArgb(245, 124, 0);

            return node.Selected ? CanvasAccentColor : Color.FromArgb(100, 122, 143);
        }

        private float GetNodeBorderWidth(NodeBase node)
        {
            FlowNodeRelationRole relationRole = GetNodeRelationRole(node);
            if (relationRole != FlowNodeRelationRole.None)
                return 2.4F;

            if (node.RuntimeStatus == NodeStatus.Failed)
                return node.Selected ? 3.4F : 3.0F;

            if (IsNodeResultNg(node))
                return node.Selected ? 3.0F : 2.4F;

            return node.Selected ? 2.6F : 1.15F;
        }

        private Color GetNodeTitleBandColor(NodeBase node)
        {
            if (node.RuntimeStatus == NodeStatus.Failed)
                return Color.FromArgb(224, 49, 49);

            if (IsNodeResultNg(node))
                return Color.FromArgb(245, 124, 0);

            return GetNodeBandColor(node.NodeType);
        }

        private string GetNodeMetaText(NodeBase node)
        {
            string statusText = node.Active ? GetNodeStatusText(node) : "已禁用";
            string timeText = GetNodeTimeText(node);
            return $"{node.NodeType}  {statusText}  {timeText}";
        }

        private static bool IsNodeResultNg(NodeBase node)
        {
            return node != null &&
                node.RuntimeStatus == NodeStatus.Successful &&
                node.RuntimeResultNg;
        }

        private static bool IsNodeAlertStatus(NodeBase node)
        {
            return node != null &&
                (node.RuntimeStatus == NodeStatus.Failed || IsNodeResultNg(node));
        }

        private string GetNodeStatusText(NodeBase node)
        {
            if (IsNodeResultNg(node))
                return "NG";

            return GetNodeStatusText(node == null ? NodeStatus.Unexecuted : node.RuntimeStatus);
        }

        private string GetNodeStatusText(NodeStatus status)
        {
            switch (status)
            {
                case NodeStatus.Running:
                    return "运行中";
                case NodeStatus.Successful:
                    return "成功";
                case NodeStatus.Failed:
                    return "失败";
                default:
                    return "未运行";
            }
        }

        private string GetNodeTimeText(NodeBase node)
        {
            if (node.RuntimeStatus == NodeStatus.Running)
                return "计时中";

            string timeText = string.IsNullOrEmpty(node.RuntimeTimeText) ? "*" : node.RuntimeTimeText;
            if (timeText == "*")
                return "* ms";

            return timeText + " ms";
        }

        private GraphicsPath CreateRoundRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Color GetNodeBandColor(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.ImageSource:
                case NodeType.CameraExposureGain:
                case NodeType.ImageSource3D:
                case NodeType.ImageShow:
                case NodeType.ImageShow3D:
                case NodeType.ImageCrop:
                case NodeType.ImageRotate:
                case NodeType.ImageSplit:
                case NodeType.ImagePreprocess:
                case NodeType.BlobAnalysis:
                    return Color.FromArgb(18, 132, 219);
                case NodeType.If:
                case NodeType.MultiCondition:
                case NodeType.ArithmeticOperation:
                case NodeType.CompositeModule:
                case NodeType.CompositeInput:
                case NodeType.CompositeOutput:
                case NodeType.Else:
                case NodeType.EndIf:
                case NodeType.ConditionRun:
                case NodeType.SleepTool:
                    return Color.FromArgb(125, 84, 188);
                case NodeType.LightSourceControl:
                case NodeType.PLCRead:
                case NodeType.PLCWrite:
                case NodeType.ModbusRead:
                case NodeType.ModbusWrite:
                    return Color.FromArgb(226, 140, 32);
                case NodeType.LineMergeFit:
                    return Color.FromArgb(64, 170, 112);
                case NodeType.ContourMatch:
                case NodeType.NccMatchTemplate:
                    return Color.FromArgb(42, 157, 143);
                default:
                    return Color.FromArgb(42, 157, 143);
            }
        }

        private static bool IsConditionalBranchNode(NodeBase node)
        {
            return node != null &&
                (node.NodeType == NodeType.If || node.NodeType == NodeType.MultiCondition);
        }

        private static class CanvasPathRouter
        {
            private const int AnchorStep = 18;
            private const int Margin = 22;

            public static Point[] Route(
                Point start,
                string startAnchor,
                Point end,
                string endAnchor,
                List<Rectangle> obstacles,
                Rectangle? sourceBounds,
                Rectangle? targetBounds)
            {
                if (start == end)
                    return new[] { start };

                Point sourceStep = Step(start, startAnchor, AnchorStep);
                Point targetStep = Step(end, endAnchor, AnchorStep);
                List<Rectangle> routeObstacles = BuildRouteObstacles(obstacles, sourceBounds, targetBounds);
                Point[] straightRoute = TryCreateStraightRoute(start, sourceStep, targetStep, end, routeObstacles);
                if (straightRoute != null)
                    return straightRoute;

                SortedSet<int> xSet = new SortedSet<int> { sourceStep.X, targetStep.X };
                SortedSet<int> ySet = new SortedSet<int> { sourceStep.Y, targetStep.Y };
                foreach (Rectangle obstacle in routeObstacles)
                {
                    xSet.Add(obstacle.Left - Margin);
                    xSet.Add(obstacle.Right + Margin);
                    ySet.Add(obstacle.Top - Margin);
                    ySet.Add(obstacle.Bottom + Margin);
                }

                List<int> xs = xSet.ToList();
                List<int> ys = ySet.ToList();
                int startXIndex = xs.IndexOf(sourceStep.X);
                int startYIndex = ys.IndexOf(sourceStep.Y);
                int endXIndex = xs.IndexOf(targetStep.X);
                int endYIndex = ys.IndexOf(targetStep.Y);

                RouteNode winner = FindRoute(
                    xs,
                    ys,
                    startXIndex,
                    startYIndex,
                    endXIndex,
                    endYIndex,
                    routeObstacles);

                List<Point> middlePoints = winner == null
                    ? BuildFallbackRoute(sourceStep, startAnchor, targetStep)
                    : RebuildRoute(winner, xs, ys);

                List<Point> fullPath = new List<Point> { start };
                fullPath.AddRange(middlePoints);
                fullPath.Add(end);
                return Simplify(fullPath);
            }

            private static List<Rectangle> BuildRouteObstacles(
                List<Rectangle> obstacles,
                Rectangle? sourceBounds,
                Rectangle? targetBounds)
            {
                List<Rectangle> routeObstacles = new List<Rectangle>();
                if (obstacles == null)
                    return routeObstacles;

                foreach (Rectangle obstacle in obstacles)
                {
                    bool isSource = sourceBounds.HasValue && obstacle == sourceBounds.Value;
                    bool isTarget = targetBounds.HasValue && obstacle == targetBounds.Value;
                    Rectangle routedObstacle = obstacle;
                    if (isSource || isTarget)
                        routedObstacle.Inflate(-8, -8);

                    routeObstacles.Add(routedObstacle);
                }

                return routeObstacles;
            }

            /// <summary>
            /// 当两个锚点已经横向或纵向正对齐时，优先使用直线路由。
            /// </summary>
            /// <param name="start">真实起点。</param>
            /// <param name="sourceStep">离开源节点后的起点。</param>
            /// <param name="targetStep">进入目标节点前的终点。</param>
            /// <param name="end">真实终点。</param>
            /// <param name="obstacles">避障节点矩形。</param>
            /// <returns>可直连时返回路由点，否则返回 null。</returns>
            private static Point[] TryCreateStraightRoute(
                Point start,
                Point sourceStep,
                Point targetStep,
                Point end,
                List<Rectangle> obstacles)
            {
                if (sourceStep.X != targetStep.X && sourceStep.Y != targetStep.Y)
                    return null;

                if (SegmentBlocked(sourceStep, targetStep, obstacles))
                    return null;

                return Simplify(new List<Point> { start, sourceStep, targetStep, end });
            }

            private static RouteNode FindRoute(
                List<int> xs,
                List<int> ys,
                int startXIndex,
                int startYIndex,
                int endXIndex,
                int endYIndex,
                List<Rectangle> obstacles)
            {
                int columnCount = xs.Count;
                int rowCount = ys.Count;
                bool[,] closed = new bool[columnCount, rowCount];
                List<RouteNode> open = new List<RouteNode>
                {
                    new RouteNode
                    {
                        XIndex = startXIndex,
                        YIndex = startYIndex,
                        Cost = 0,
                        Heuristic = Distance(xs[startXIndex], ys[startYIndex], xs[endXIndex], ys[endYIndex]),
                        Direction = -1
                    }
                };

                for (int iteration = 0; iteration < 5000 && open.Count > 0; iteration++)
                {
                    int bestIndex = 0;
                    for (int index = 1; index < open.Count; index++)
                    {
                        if (open[index].TotalCost < open[bestIndex].TotalCost)
                            bestIndex = index;
                    }

                    RouteNode current = open[bestIndex];
                    open.RemoveAt(bestIndex);

                    if (current.XIndex == endXIndex && current.YIndex == endYIndex)
                        return current;

                    if (closed[current.XIndex, current.YIndex])
                        continue;

                    closed[current.XIndex, current.YIndex] = true;
                    int[] dx = { -1, 0, 1, 0 };
                    int[] dy = { 0, 1, 0, -1 };
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int nextXIndex = current.XIndex + dx[direction];
                        int nextYIndex = current.YIndex + dy[direction];
                        if (nextXIndex < 0 ||
                            nextXIndex >= columnCount ||
                            nextYIndex < 0 ||
                            nextYIndex >= rowCount ||
                            closed[nextXIndex, nextYIndex])
                        {
                            continue;
                        }

                        Point from = new Point(xs[current.XIndex], ys[current.YIndex]);
                        Point to = new Point(xs[nextXIndex], ys[nextYIndex]);
                        if (SegmentBlocked(from, to, obstacles))
                            continue;

                        int moveCost = Distance(from.X, from.Y, to.X, to.Y);
                        int turnCost = current.Direction >= 0 && current.Direction != direction ? 60 : 0;
                        open.Add(new RouteNode
                        {
                            XIndex = nextXIndex,
                            YIndex = nextYIndex,
                            Cost = current.Cost + moveCost + turnCost,
                            Heuristic = Distance(to.X, to.Y, xs[endXIndex], ys[endYIndex]),
                            Direction = direction,
                            Parent = current
                        });
                    }
                }

                return null;
            }

            private static List<Point> RebuildRoute(RouteNode winner, List<int> xs, List<int> ys)
            {
                List<Point> points = new List<Point>();
                RouteNode current = winner;
                while (current != null)
                {
                    points.Add(new Point(xs[current.XIndex], ys[current.YIndex]));
                    current = current.Parent;
                }

                points.Reverse();
                return points;
            }

            private static List<Point> BuildFallbackRoute(Point start, string startAnchor, Point end)
            {
                List<Point> points = new List<Point>();
                if (startAnchor == "Top" || startAnchor == "Bottom")
                {
                    points.Add(start);
                    points.Add(new Point(start.X, end.Y));
                    points.Add(end);
                }
                else
                {
                    points.Add(start);
                    points.Add(new Point(end.X, start.Y));
                    points.Add(end);
                }

                return points;
            }

            private static Point Step(Point point, string anchor, int distance)
            {
                switch (anchor)
                {
                    case "Top":
                        return new Point(point.X, point.Y - distance);
                    case "Bottom":
                        return new Point(point.X, point.Y + distance);
                    case "Left":
                        return new Point(point.X - distance, point.Y);
                    case "Right":
                        return new Point(point.X + distance, point.Y);
                    default:
                        return point;
                }
            }

            private static int Distance(int x1, int y1, int x2, int y2)
            {
                return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
            }

            private static bool SegmentBlocked(Point from, Point to, List<Rectangle> obstacles)
            {
                int left = Math.Min(from.X, to.X);
                int top = Math.Min(from.Y, to.Y);
                int right = Math.Max(from.X, to.X);
                int bottom = Math.Max(from.Y, to.Y);
                Rectangle segment = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
                if (from.X == to.X)
                    segment.Inflate(1, 0);
                else if (from.Y == to.Y)
                    segment.Inflate(0, 1);

                foreach (Rectangle obstacle in obstacles)
                {
                    if (segment.IntersectsWith(obstacle))
                        return true;
                }

                return false;
            }

            private static Point[] Simplify(List<Point> points)
            {
                if (points == null || points.Count <= 2)
                    return points == null ? new Point[0] : points.ToArray();

                List<Point> result = new List<Point> { points[0] };
                for (int index = 1; index < points.Count - 1; index++)
                {
                    Point previous = result[result.Count - 1];
                    Point current = points[index];
                    Point next = points[index + 1];
                    if (current == previous)
                        continue;

                    bool sameX = previous.X == current.X && current.X == next.X;
                    bool sameY = previous.Y == current.Y && current.Y == next.Y;
                    if (!sameX && !sameY)
                        result.Add(current);
                }

                if (result[result.Count - 1] != points[points.Count - 1])
                    result.Add(points[points.Count - 1]);

                return result.ToArray();
            }

            private sealed class RouteNode
            {
                public int XIndex;
                public int YIndex;
                public int Cost;
                public int Heuristic;
                public int Direction;
                public RouteNode Parent;

                public int TotalCost
                {
                    get { return Cost + Heuristic; }
                }
            }
        }

        private interface ICanvasEdit
        {
            bool Redo(ProcessFlowCanvas canvas);
            void Undo(ProcessFlowCanvas canvas);
        }

        /// <summary>
        /// 节点拖拽辅助线方向。
        /// </summary>
        private enum CanvasAlignmentAxis
        {
            /// <summary>
            /// 水平辅助线，用于对齐节点的上边、中线或下边。
            /// </summary>
            Horizontal,
            /// <summary>
            /// 垂直辅助线，用于对齐节点的左边、中线或右边。
            /// </summary>
            Vertical
        }

        /// <summary>
        /// 节点拖拽时命中的一个吸附候选。
        /// </summary>
        private sealed class AlignmentCandidate
        {
            /// <summary>
            /// 对齐方向。
            /// </summary>
            public readonly CanvasAlignmentAxis Axis;
            /// <summary>
            /// 辅助线所在的画布坐标。
            /// </summary>
            public readonly int GuideCoordinate;
            /// <summary>
            /// 吸附后拖拽节点左上角在对应方向上的坐标。
            /// </summary>
            public readonly int NewLocationCoordinate;
            /// <summary>
            /// 吸附前拖拽节点与辅助线之间的距离。
            /// </summary>
            public readonly int Distance;
            /// <summary>
            /// 命中的参照节点边界。
            /// </summary>
            public readonly Rectangle TargetBounds;
            /// <summary>
            /// 候选优先级，中心线优先级高于边线。
            /// </summary>
            public readonly int Priority;

            /// <summary>
            /// 创建拖拽吸附候选。
            /// </summary>
            /// <param name="axis">对齐方向。</param>
            /// <param name="guideCoordinate">辅助线所在的画布坐标。</param>
            /// <param name="newLocationCoordinate">吸附后节点左上角坐标。</param>
            /// <param name="distance">吸附距离。</param>
            /// <param name="targetBounds">参照节点边界。</param>
            /// <param name="priority">候选优先级。</param>
            public AlignmentCandidate(
                CanvasAlignmentAxis axis,
                int guideCoordinate,
                int newLocationCoordinate,
                int distance,
                Rectangle targetBounds,
                int priority)
            {
                Axis = axis;
                GuideCoordinate = guideCoordinate;
                NewLocationCoordinate = newLocationCoordinate;
                Distance = distance;
                TargetBounds = targetBounds;
                Priority = priority;
            }
        }

        /// <summary>
        /// 当前画布需要绘制的一条对齐辅助线。
        /// </summary>
        private sealed class AlignmentGuide
        {
            /// <summary>
            /// 辅助线方向。
            /// </summary>
            public readonly CanvasAlignmentAxis Axis;
            /// <summary>
            /// 辅助线固定坐标，水平线为 Y，垂直线为 X。
            /// </summary>
            public readonly int Coordinate;
            /// <summary>
            /// 辅助线起点坐标，水平线为 X，垂直线为 Y。
            /// </summary>
            public readonly int Start;
            /// <summary>
            /// 辅助线终点坐标，水平线为 X，垂直线为 Y。
            /// </summary>
            public readonly int End;

            /// <summary>
            /// 创建一条辅助线。
            /// </summary>
            /// <param name="axis">辅助线方向。</param>
            /// <param name="coordinate">辅助线固定坐标。</param>
            /// <param name="start">辅助线起点坐标。</param>
            /// <param name="end">辅助线终点坐标。</param>
            public AlignmentGuide(CanvasAlignmentAxis axis, int coordinate, int start, int end)
            {
                Axis = axis;
                Coordinate = coordinate;
                Start = start;
                End = end;
            }
        }

        /// <summary>
        /// 拖动过程中用于对齐吸附的静态节点边界快照。
        /// </summary>
        private sealed class NodeAlignmentTarget
        {
            /// <summary>
            /// 参考节点在拖动开始时的画布边界。
            /// </summary>
            public readonly Rectangle Bounds;

            /// <summary>
            /// 创建一个对齐参考节点快照。
            /// </summary>
            /// <param name="bounds">参考节点画布边界。</param>
            public NodeAlignmentTarget(Rectangle bounds)
            {
                Bounds = bounds;
            }
        }

        /// <summary>
        /// 持久连线最近一次生成的路由缓存。
        /// </summary>
        private sealed class ConnectionRouteCache
        {
            /// <summary>
            /// 缓存生成时的起点。
            /// </summary>
            private readonly Point _start;
            /// <summary>
            /// 缓存生成时的起点锚点。
            /// </summary>
            private readonly string _startAnchor;
            /// <summary>
            /// 缓存生成时的终点。
            /// </summary>
            private readonly Point _end;
            /// <summary>
            /// 缓存生成时的终点锚点。
            /// </summary>
            private readonly string _endAnchor;
            /// <summary>
            /// 缓存生成时的源节点边界。
            /// </summary>
            private readonly Rectangle _sourceBounds;
            /// <summary>
            /// 缓存生成时的目标节点边界。
            /// </summary>
            private readonly Rectangle _targetBounds;
            /// <summary>
            /// 缓存的路由点。
            /// </summary>
            public readonly Point[] Points;
            /// <summary>
            /// 路由点的最小外接矩形，用于可视区域裁剪和局部刷新。
            /// </summary>
            public readonly Rectangle Bounds;

            /// <summary>
            /// 创建连线路由缓存。
            /// </summary>
            /// <param name="start">连线起点。</param>
            /// <param name="startAnchor">起点锚点。</param>
            /// <param name="end">连线终点。</param>
            /// <param name="endAnchor">终点锚点。</param>
            /// <param name="sourceBounds">源节点边界。</param>
            /// <param name="targetBounds">目标节点边界。</param>
            /// <param name="points">路由点。</param>
            public ConnectionRouteCache(
                Point start,
                string startAnchor,
                Point end,
                string endAnchor,
                Rectangle sourceBounds,
                Rectangle targetBounds,
                Point[] points)
            {
                _start = start;
                _startAnchor = startAnchor;
                _end = end;
                _endAnchor = endAnchor;
                _sourceBounds = sourceBounds;
                _targetBounds = targetBounds;
                Points = points == null ? new Point[0] : points.ToArray();
                Bounds = CreatePointBounds(Points);
            }

            /// <summary>
            /// 判断当前连线两端是否仍可复用缓存。
            /// </summary>
            /// <param name="start">当前起点。</param>
            /// <param name="startAnchor">当前起点锚点。</param>
            /// <param name="end">当前终点。</param>
            /// <param name="endAnchor">当前终点锚点。</param>
            /// <param name="sourceBounds">当前源节点边界。</param>
            /// <param name="targetBounds">当前目标节点边界。</param>
            /// <returns>两端节点未变化时返回 true。</returns>
            public bool Matches(
                Point start,
                string startAnchor,
                Point end,
                string endAnchor,
                Rectangle sourceBounds,
                Rectangle targetBounds)
            {
                return _start == start &&
                    _startAnchor == startAnchor &&
                    _end == end &&
                    _endAnchor == endAnchor &&
                    _sourceBounds == sourceBounds &&
                    _targetBounds == targetBounds;
            }
        }

        private sealed class PreviewImageInfo
        {
            public readonly NodeBase Node;
            public readonly Mat Image;
            public readonly string Key;
            public readonly string Title;

            public PreviewImageInfo(NodeBase node, Mat image, string key, string title)
            {
                Node = node;
                Image = image;
                Key = key;
                Title = title;
            }
        }

        private sealed class NodeMoveEdit : ICanvasEdit
        {
            private readonly NodeBase _node;
            private readonly Point _oldLocation;
            private readonly Point _newLocation;

            public NodeMoveEdit(NodeBase node, Point oldLocation, Point newLocation)
            {
                _node = node;
                _oldLocation = oldLocation;
                _newLocation = newLocation;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                canvas.SetNodeLocation(_node, _newLocation);
                return true;
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.SetNodeLocation(_node, _oldLocation);
            }
        }

        private sealed class NodesMoveEdit : ICanvasEdit
        {
            private readonly List<NodeMoveState> _moves;

            public NodesMoveEdit(List<NodeMoveState> moves)
            {
                _moves = moves ?? new List<NodeMoveState>();
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                canvas.ApplyNodeLocations(ToLocations(false));
                return true;
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.ApplyNodeLocations(ToLocations(true));
            }

            private List<NodeLocationSnapshot> ToLocations(bool useOldLocation)
            {
                List<NodeLocationSnapshot> locations = new List<NodeLocationSnapshot>();
                foreach (NodeMoveState move in _moves)
                {
                    locations.Add(new NodeLocationSnapshot(
                        move.Node,
                        useOldLocation ? move.OldLocation : move.NewLocation));
                }

                return locations;
            }
        }

        private sealed class NodeAddedEdit : ICanvasEdit
        {
            private readonly NodeBase _node;
            private readonly int _processIndex;
            private readonly int _solutionIndex;

            public NodeAddedEdit(NodeBase node, int processIndex, int solutionIndex)
            {
                _node = node;
                _processIndex = processIndex;
                _solutionIndex = solutionIndex;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                canvas.RestoreNode(_node, _processIndex, _solutionIndex, null);
                return true;
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.DetachNode(_node);
            }
        }

        private sealed class NodesAddedEdit : ICanvasEdit
        {
            private readonly List<NodeRestoreSnapshot> _nodeSnapshots;
            private readonly List<ConnectionSnapshot> _connectionSnapshots;

            public NodesAddedEdit(
                ProcessFlowCanvas canvas,
                List<NodeBase> nodes,
                List<ProcessConnection> connections)
            {
                _nodeSnapshots = canvas.CaptureNodeRestoreSnapshots(nodes);
                _connectionSnapshots = new List<ConnectionSnapshot>();
                if (canvas._process == null || connections == null)
                    return;

                foreach (ProcessConnection connection in connections)
                {
                    int index = canvas._process.Connections.IndexOf(connection);
                    if (index >= 0)
                        _connectionSnapshots.Add(new ConnectionSnapshot(connection, index));
                }
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                canvas.RestoreNodes(_nodeSnapshots, _connectionSnapshots);
                return true;
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.DetachNodes(_nodeSnapshots);
            }
        }

        private sealed class NodeDeletedEdit : ICanvasEdit
        {
            private readonly NodeBase _node;
            private readonly int _processIndex;
            private readonly int _solutionIndex;
            private readonly List<ConnectionSnapshot> _connectionSnapshots;
            private readonly bool _wasStartNode;

            public NodeDeletedEdit(ProcessFlowCanvas canvas, NodeBase node)
            {
                _node = node;
                _processIndex = canvas._process == null ? -1 : canvas._process.Nodes.IndexOf(node);
                _solutionIndex = Solution.Instance.Nodes.IndexOf(node);
                _connectionSnapshots = canvas.CaptureConnectionsForNode(node);
                _wasStartNode = node != null && node.IsStartNode;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                return canvas.DetachNode(_node);
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                if (_node != null)
                    _node.IsStartNode = _wasStartNode;

                canvas.RestoreNode(_node, _processIndex, _solutionIndex, _connectionSnapshots);
            }
        }

        private sealed class NodesDeletedEdit : ICanvasEdit
        {
            private readonly List<NodeRestoreSnapshot> _nodeSnapshots;
            private readonly List<ConnectionSnapshot> _connectionSnapshots;

            public NodesDeletedEdit(ProcessFlowCanvas canvas, List<NodeBase> nodes)
            {
                _nodeSnapshots = canvas.CaptureNodeRestoreSnapshots(nodes);
                _connectionSnapshots = canvas.CaptureConnectionsForNodes(nodes);
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                return canvas.DetachNodes(_nodeSnapshots);
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.RestoreNodes(_nodeSnapshots, _connectionSnapshots);
            }
        }

        private sealed class ConnectionAddedEdit : ICanvasEdit
        {
            private readonly ProcessConnection _connection;
            private readonly int _index;

            public ConnectionAddedEdit(ProcessConnection connection, int index)
            {
                _connection = connection;
                _index = index;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                return canvas.AddConnection(_connection, _index);
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.RemoveConnection(_connection);
            }
        }

        private sealed class ConnectionDeletedEdit : ICanvasEdit
        {
            private readonly ProcessConnection _connection;
            private readonly int _index;

            public ConnectionDeletedEdit(ProcessConnection connection, int index)
            {
                _connection = connection;
                _index = index;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                return canvas.RemoveConnection(_connection);
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.AddConnection(_connection, _index);
            }
        }

        private sealed class NodeActiveChangedEdit : ICanvasEdit
        {
            private readonly List<NodeActiveState> _oldStates;
            private readonly bool _newActive;

            public NodeActiveChangedEdit(List<NodeBase> nodes, bool newActive)
            {
                _oldStates = new List<NodeActiveState>();
                if (nodes != null)
                {
                    foreach (NodeBase node in nodes)
                    {
                        if (node != null)
                            _oldStates.Add(new NodeActiveState(node, node.Active));
                    }
                }

                _newActive = newActive;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                foreach (NodeActiveState state in _oldStates)
                    canvas.SetNodeActive(state.Node, _newActive);

                return true;
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                foreach (NodeActiveState state in _oldStates)
                    canvas.SetNodeActive(state.Node, state.WasActive);
            }
        }

        private sealed class StartNodeChangedEdit : ICanvasEdit
        {
            private readonly List<NodeStartState> _oldStates;
            private readonly NodeBase _newStartNode;

            public StartNodeChangedEdit(List<NodeStartState> oldStates, NodeBase newStartNode)
            {
                _oldStates = oldStates;
                _newStartNode = newStartNode;
            }

            public bool Redo(ProcessFlowCanvas canvas)
            {
                canvas.SetOnlyStartNode(_newStartNode);
                return true;
            }

            public void Undo(ProcessFlowCanvas canvas)
            {
                canvas.ApplyStartStates(_oldStates);
            }
        }

        private sealed class NodeLocationSnapshot
        {
            public readonly NodeBase Node;
            public readonly Point Location;

            public NodeLocationSnapshot(NodeBase node, Point location)
            {
                Node = node;
                Location = location;
            }
        }

        private sealed class NodeMoveState
        {
            public readonly NodeBase Node;
            public readonly Point OldLocation;
            public readonly Point NewLocation;

            public NodeMoveState(NodeBase node, Point oldLocation, Point newLocation)
            {
                Node = node;
                OldLocation = oldLocation;
                NewLocation = newLocation;
            }
        }

        private sealed class NodeRestoreSnapshot
        {
            public readonly NodeBase Node;
            public readonly int ProcessIndex;
            public readonly int SolutionIndex;
            public readonly bool WasStartNode;

            public NodeRestoreSnapshot(
                NodeBase node,
                int processIndex,
                int solutionIndex,
                bool wasStartNode)
            {
                Node = node;
                ProcessIndex = processIndex;
                SolutionIndex = solutionIndex;
                WasStartNode = wasStartNode;
            }
        }

        private sealed class NodeActiveState
        {
            public readonly NodeBase Node;
            public readonly bool WasActive;

            public NodeActiveState(NodeBase node, bool wasActive)
            {
                Node = node;
                WasActive = wasActive;
            }
        }

        private sealed class ConnectionSnapshot
        {
            public readonly ProcessConnection Connection;
            public readonly int Index;

            public ConnectionSnapshot(ProcessConnection connection, int index)
            {
                Connection = connection;
                Index = index;
            }
        }

        private sealed class NodeStartState
        {
            public readonly NodeBase Node;
            public readonly bool IsStartNode;

            public NodeStartState(NodeBase node, bool isStartNode)
            {
                Node = node;
                IsStartNode = isStartNode;
            }
        }
    }

    /// <summary>
    /// 流程画布悬浮层刷新请求参数。
    /// </summary>
    public sealed class FlowCanvasOverlayInvalidatedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否需要刷新图像预览层。
        /// </summary>
        public bool InvalidateImagePreview { get; private set; }

        /// <summary>
        /// 是否需要刷新缩略图层。
        /// </summary>
        public bool InvalidateMinimap { get; private set; }

        /// <summary>
        /// 创建悬浮层刷新请求参数。
        /// </summary>
        /// <param name="invalidateImagePreview">是否刷新图像预览层。</param>
        /// <param name="invalidateMinimap">是否刷新缩略图层。</param>
        public FlowCanvasOverlayInvalidatedEventArgs(bool invalidateImagePreview, bool invalidateMinimap)
        {
            InvalidateImagePreview = invalidateImagePreview;
            InvalidateMinimap = invalidateMinimap;
        }
    }

    public class FlowImagePreviewOverlay : Control
    {
        private ProcessFlowCanvas _canvas;

        public FlowImagePreviewOverlay()
        {
            TabStop = false;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Opaque,
                true);
        }

        public void BindCanvas(ProcessFlowCanvas canvas)
        {
            _canvas = canvas;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_canvas == null)
            {
                e.Graphics.Clear(Color.FromArgb(42, 44, 48));
                return;
            }

            _canvas.DrawImagePreviewOverlay(e.Graphics, ClientSize);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_canvas == null)
                return;

            _canvas.HandleImagePreviewMouseDown(this, e.Location, e.Button, ClientSize);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_canvas == null)
                return;

            _canvas.HandleImagePreviewMouseMove(e.Location, ClientSize);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_canvas == null)
                return;

            _canvas.HandleImagePreviewMouseLeave();
        }
    }

    public class FlowMinimapOverlay : Control
    {
        private ProcessFlowCanvas _canvas;
        private bool _isDragging;

        public FlowMinimapOverlay()
        {
            TabStop = false;
            Cursor = Cursors.SizeAll;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Opaque,
                true);
        }

        public void BindCanvas(ProcessFlowCanvas canvas)
        {
            _canvas = canvas;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_canvas == null)
            {
                e.Graphics.Clear(Color.FromArgb(42, 44, 48));
                return;
            }

            _canvas.DrawMinimapOverlay(e.Graphics, ClientSize);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_canvas == null || e.Button != MouseButtons.Left)
                return;

            _isDragging = true;
            Capture = true;
            _canvas.CenterViewportFromMinimap(e.Location, ClientSize);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_canvas == null || !_isDragging || e.Button != MouseButtons.Left)
                return;

            _canvas.CenterViewportFromMinimap(e.Location, ClientSize);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            _isDragging = false;
            Capture = false;
        }
    }
}
