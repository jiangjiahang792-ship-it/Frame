using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp.Extensions;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using Mat = OpenCvSharp.Mat;

namespace TDJS_Vision.Forms.ProcessNew
{
    /// <summary>
    /// Free flow-canvas surface for the ProcessNew editor.
    /// </summary>
    public class ProcessFlowCanvas : Panel
    {
        private const int GridSize = 24;
        private const int AnchorSize = 12;
        private static readonly Size LogicalCanvasSize = new Size(2400, 1600);
        private static readonly Color CanvasBackColor = Color.FromArgb(48, 52, 56);
        private static readonly Color GridLineColor = Color.FromArgb(61, 67, 72);
        private static readonly Color GridMajorLineColor = Color.FromArgb(72, 79, 85);
        private static readonly Color NodeFillColor = Color.FromArgb(248, 250, 252);
        private static readonly Color NodeDisabledFillColor = Color.FromArgb(221, 225, 231);
        private static readonly Color NodeTextColor = Color.FromArgb(29, 41, 57);
        private static readonly Color NodeMetaTextColor = Color.FromArgb(86, 99, 117);
        private static readonly Color CanvasAccentColor = Color.FromArgb(255, 132, 32);
        private static readonly Color OverlayBackColor = Color.FromArgb(42, 44, 48);
        private static readonly Color OverlayBorderColor = Color.FromArgb(132, 136, 142);

        private Process _process;
        private NodeBase _dragNode;
        private Point _dragOffset;
        private NodeBase _connectionStartNode;
        private string _connectionStartAnchor;
        private Point _connectionEndPoint;
        private ProcessConnection _selectedConnection;
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
        private string _selectedPreviewImageKey;
        private string _cachedPreviewKey;
        private Mat _cachedPreviewMat;
        private Bitmap _cachedPreviewBitmap;
        private Rectangle _minimapBounds = Rectangle.Empty;
        private Rectangle _minimapCanvasBounds = Rectangle.Empty;

        private const float MinZoomFactor = 0.5F;
        private const float MaxZoomFactor = 2F;
        private const float ZoomStep = 0.1F;

        public static readonly Size DefaultCanvasNodeSize = new Size(240, 64);

        public event EventHandler HistoryChanged;
        public event EventHandler NodeCollectionChanged;
        public event EventHandler ZoomChanged;
        public event EventHandler OverlayInvalidated;

        public Point LastContextMenuCanvasPoint { get; private set; }

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
                _previewImageMenu.Dispose();
                DisposePreviewCache();
            }

            base.Dispose(disposing);
        }

        private void NodeBase_NodeStatusChanged(object sender, NodeBase node)
        {
            if (_process == null ||
                node == null ||
                !_process.Nodes.Contains(node) ||
                IsDisposed ||
                Disposing)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((MethodInvoker)(() => InvalidateRuntimeChange(node)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            InvalidateRuntimeChange(node);
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

            Invalidate();
            RequestOverlayInvalidate();
        }

        public void RefreshNodes()
        {
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
            graphics.Clear(CanvasBackColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            graphics.ScaleTransform(_zoomFactor, _zoomFactor);

            DrawGrid(graphics);

            if (_process != null)
            {
                foreach (ProcessConnection connection in _process.Connections)
                    DrawConnection(graphics, connection);

                if (_connectionStartNode != null)
                    DrawTemporaryConnection(graphics);

                foreach (NodeBase node in _process.Nodes)
                    DrawNode(graphics, node);

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
                LastContextMenuCanvasPoint = canvasPoint;
                SelectAtPoint(canvasPoint, true);
                Invalidate();
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            NodeBase anchorNode;
            string anchorName;
            if (TryHitAnchor(canvasPoint, out anchorNode, out anchorName))
            {
                SelectOnly(anchorNode);
                _connectionStartNode = anchorNode;
                _connectionStartAnchor = anchorName;
                _connectionEndPoint = canvasPoint;
                Invalidate();
                return;
            }

            NodeBase node = HitNode(canvasPoint);
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
            }
            else
            {
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
                Point location = new Point(
                    canvasPoint.X - _dragOffset.X,
                    canvasPoint.Y - _dragOffset.Y);
                MoveDraggedNodes(location);
                Invalidate();
                return;
            }

            if (_isSelectingNodes && e.Button == MouseButtons.Left)
            {
                _selectionEndPoint = canvasPoint;
                SelectNodesInRectangle(GetSelectionRectangle());
                Invalidate();
                return;
            }

            if (_connectionStartNode != null)
            {
                _connectionEndPoint = canvasPoint;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_process == null || e.Button != MouseButtons.Left)
                return;

            if (_isSelectingNodes)
            {
                _selectionEndPoint = ToCanvasPoint(e.Location);
                SelectNodesInRectangle(GetSelectionRectangle());
                _isSelectingNodes = false;
                Invalidate();
                RequestOverlayInvalidate();
                return;
            }

            NodeBase movedNode = _dragNode;
            List<NodeLocationSnapshot> oldLocations = _dragStartSnapshots;
            _dragNode = null;
            _dragStartSnapshots = new List<NodeLocationSnapshot>();
            if (movedNode != null)
            {
                PushMoveHistory(oldLocations);
                RequestOverlayInvalidate();
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
            RequestOverlayInvalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
            RequestOverlayInvalidate();
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
            NodeCollectionChanged?.Invoke(this, EventArgs.Empty);
            RequestOverlayInvalidate();
        }

        private void OnZoomChanged()
        {
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RequestOverlayInvalidate()
        {
            OverlayInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private bool SetZoom(float zoomFactor, Point anchorCanvasPoint, Point anchorControlPoint)
        {
            float nextZoom = Math.Max(MinZoomFactor, Math.Min(MaxZoomFactor, zoomFactor));
            if (Math.Abs(nextZoom - _zoomFactor) < 0.001F)
                return false;

            _zoomFactor = nextZoom;
            UpdateScaledCanvasSize();
            SetScrollPositionForAnchor(anchorCanvasPoint, anchorControlPoint);
            Invalidate();
            RequestOverlayInvalidate();
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
            _process.NotifyConnectionsChanged();
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
            }

            _process.NotifyConnectionsChanged();
            RequestOverlayInvalidate();
        }

        private void SetNodeLocation(NodeBase node, Point location)
        {
            if (node == null)
                return;

            node.CanvasLocation = ClampLocation(location, GetNodeSize(node));
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

        private void MoveDraggedNodes(Point requestedDragNodeLocation)
        {
            if (_dragNode == null)
                return;

            Point dragNodeStartLocation = _dragStartLocation;
            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                if (snapshot.Node == _dragNode)
                {
                    dragNodeStartLocation = snapshot.Location;
                    break;
                }
            }

            Point clampedDragNodeLocation = GetInteractiveNodeLocation(requestedDragNodeLocation, GetNodeSize(_dragNode));
            int offsetX = clampedDragNodeLocation.X - dragNodeStartLocation.X;
            int offsetY = clampedDragNodeLocation.Y - dragNodeStartLocation.Y;

            foreach (NodeLocationSnapshot snapshot in _dragStartSnapshots)
            {
                Point nextLocation = new Point(
                    snapshot.Location.X + offsetX,
                    snapshot.Location.Y + offsetY);
                snapshot.Node.CanvasLocation = ClampLocation(nextLocation, GetNodeSize(snapshot.Node));
            }
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

        private void SelectNodesInRectangle(Rectangle selection)
        {
            if (_process == null)
                return;

            _selectedConnection = null;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = selection.IntersectsWith(GetNodeBounds(node));
        }

        private Rectangle GetSelectionRectangle()
        {
            int x = Math.Min(_selectionStartPoint.X, _selectionEndPoint.X);
            int y = Math.Min(_selectionStartPoint.Y, _selectionEndPoint.Y);
            int width = Math.Abs(_selectionStartPoint.X - _selectionEndPoint.X);
            int height = Math.Abs(_selectionStartPoint.Y - _selectionEndPoint.Y);
            return new Rectangle(x, y, width, height);
        }

        private void DrawGrid(Graphics graphics)
        {
            using (Pen gridPen = new Pen(GridLineColor, 1F / _zoomFactor))
            using (Pen majorGridPen = new Pen(GridMajorLineColor, 1.15F / _zoomFactor))
            {
                for (int x = 0; x <= LogicalCanvasSize.Width; x += GridSize)
                    graphics.DrawLine(x % (GridSize * 4) == 0 ? majorGridPen : gridPen, x, 0, x, LogicalCanvasSize.Height);

                for (int y = 0; y <= LogicalCanvasSize.Height; y += GridSize)
                    graphics.DrawLine(y % (GridSize * 4) == 0 ? majorGridPen : gridPen, 0, y, LogicalCanvasSize.Width, y);
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
                DrawPreviewBitmap(graphics, previewBitmap, content);
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

        private void DrawPreviewBitmap(Graphics graphics, Bitmap previewBitmap, Rectangle content)
        {
            _previewImageDrawBounds = GetContainedImageBounds(previewBitmap.Size, content);
            _previewImagePixelSize = previewBitmap.Size;

            InterpolationMode oldInterpolationMode = graphics.InterpolationMode;
            PixelOffsetMode oldPixelOffsetMode = graphics.PixelOffsetMode;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(previewBitmap, _previewImageDrawBounds);
            graphics.InterpolationMode = oldInterpolationMode;
            graphics.PixelOffsetMode = oldPixelOffsetMode;

            using (Pen border = new Pen(Color.FromArgb(82, 88, 96), 1F))
                graphics.DrawRectangle(border, _previewImageDrawBounds);
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

            using (SolidBrush fill = new SolidBrush(Color.FromArgb(220, 42, 44, 48)))
            using (Pen border = new Pen(Color.FromArgb(110, 116, 124), 1.2F))
            {
                graphics.FillRectangle(fill, _minimapBounds);
                graphics.DrawRectangle(border, _minimapBounds);
            }

            if (_process != null)
            {
                foreach (ProcessConnection connection in _process.Connections)
                    DrawMinimapConnection(graphics, connection);

                foreach (NodeBase node in _process.Nodes)
                    DrawMinimapNode(graphics, node);
            }

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
            SetScrollPositionForAnchor(
                new Point((int)Math.Round(canvasX), (int)Math.Round(canvasY)),
                new Point(ClientSize.Width / 2, ClientSize.Height / 2));
            Invalidate();
            RequestOverlayInvalidate();
        }

        private void DrawNode(Graphics graphics, NodeBase node)
        {
            Rectangle bounds = GetNodeBounds(node);
            Color fillColor = GetNodeFillColor(node);
            Color statusColor = GetNodeStatusColor(node);
            Color borderColor = node.Selected ? CanvasAccentColor : Color.FromArgb(100, 122, 143);

            Rectangle shadowBounds = bounds;
            shadowBounds.Offset(3, 4);
            using (GraphicsPath shadow = CreateRoundRect(shadowBounds, 6))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0)))
            {
                graphics.FillPath(shadowBrush, shadow);
            }

            using (GraphicsPath body = CreateRoundRect(bounds, 6))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor, node.Selected ? 2.6F : 1.15F))
            {
                graphics.FillPath(fill, body);
                graphics.DrawPath(border, body);
            }

            Rectangle titleBand = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 9);
            NodeType nodeType = node.NodeType;
            using (SolidBrush titleBrush = new SolidBrush(GetNodeBandColor(nodeType)))
                graphics.FillRectangle(titleBrush, titleBand);

            using (Font titleFont = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold))
            using (Font metaFont = new Font("Microsoft YaHei", 8F, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(NodeTextColor))
            using (SolidBrush metaBrush = new SolidBrush(NodeMetaTextColor))
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

            if (node.IsStartNode)
            {
                Rectangle badge = new Rectangle(bounds.Right - 47, bounds.Y + 18, 34, 16);
                using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(47, 158, 68)))
                using (Font badgeFont = new Font("Segoe UI", 7F, FontStyle.Bold))
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

        private void DrawConnection(Graphics graphics, ProcessConnection connection)
        {
            NodeBase fromNode = FindNode(connection.FromNodeId);
            NodeBase toNode = FindNode(connection.ToNodeId);
            if (fromNode == null || toNode == null)
                return;

            Point start = GetAnchorCenter(fromNode, connection.FromAnchor);
            Point end = GetAnchorCenter(toNode, connection.ToAnchor);
            Color connectionColor = connection == _selectedConnection
                ? Color.FromArgb(90, 190, 255)
                : GetConnectionColor(connection.Branch);
            Point[] route = CreateRoutedConnectionPoints(
                start,
                connection.FromAnchor,
                end,
                connection.ToAnchor,
                fromNode,
                toNode);

            DrawArrowPath(graphics, route, connectionColor, false, connection == _selectedConnection);
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

        private List<Rectangle> GetConnectionObstacles()
        {
            List<Rectangle> obstacles = new List<Rectangle>();
            if (_process == null)
                return obstacles;

            foreach (NodeBase node in _process.Nodes)
                obstacles.Add(GetNodeBounds(node));

            return obstacles;
        }

        private void DrawArrowPath(Graphics graphics, Point[] points, Color color, bool dashed, bool selected)
        {
            if (points == null || points.Length < 2)
                return;

            using (AdjustableArrowCap arrow = new AdjustableArrowCap(5, 5))
            using (Pen pen = new Pen(color, selected ? 3.2F : 2.4F))
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
            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen border = new Pen(node.Selected ? CanvasAccentColor : Color.FromArgb(90, 190, 255), 1.4F))
            {
                graphics.FillEllipse(fill, anchor);
                graphics.DrawEllipse(border, anchor);
            }

            if (!string.IsNullOrEmpty(branchLabel))
            {
                using (Font branchFont = new Font("Segoe UI", 7F, FontStyle.Bold))
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
            if (node.CanvasSize.Width <= 0 || node.CanvasSize.Height <= 0)
                return DefaultCanvasNodeSize;

            return node.CanvasSize;
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

                    Point start = GetAnchorCenter(fromNode, connection.FromAnchor);
                    Point end = GetAnchorCenter(toNode, connection.ToAnchor);
                    Point[] route = CreateRoutedConnectionPoints(
                        start,
                        connection.FromAnchor,
                        end,
                        connection.ToAnchor,
                        fromNode,
                        toNode);

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

            return _process.Nodes.Find(node => node.ID == nodeId);
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
            RequestOverlayInvalidate();
        }

        private void SelectOnly(ProcessConnection selectedConnection)
        {
            _selectedConnection = selectedConnection;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;
            RequestOverlayInvalidate();
        }

        private void ClearSelection()
        {
            if (_process == null)
                return;

            _selectedConnection = null;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;
            RequestOverlayInvalidate();
        }

        private void SelectAtPoint(Point canvasPoint, bool preserveSelectedNodeGroup = false)
        {
            NodeBase node = HitNode(canvasPoint);
            if (node != null)
            {
                if (!preserveSelectedNodeGroup || !node.Selected)
                    SelectOnly(node);
                return;
            }

            ProcessConnection connection = HitConnection(canvasPoint);
            if (connection != null)
            {
                SelectOnly(connection);
                return;
            }

            ClearSelection();
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

        private bool OpenNodeParameters(NodeBase node)
        {
            Form form = node == null ? null : node.ParamForm as Form;
            if (node == null || !node.Active || form == null)
                return false;

            form.ShowDialog();
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
            using (Font font = new Font("Segoe UI", 7F, FontStyle.Bold))
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

        private Color GetNodeFillColor(NodeBase node)
        {
            if (!node.Active)
                return NodeDisabledFillColor;

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

        private string GetNodeMetaText(NodeBase node)
        {
            string statusText = node.Active ? GetNodeStatusText(node.RuntimeStatus) : "已禁用";
            string timeText = GetNodeTimeText(node);
            return $"{node.NodeType}  {statusText}  {timeText}";
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
                case NodeType.ImageShow:
                case NodeType.ImageCrop:
                case NodeType.ImageRotate:
                case NodeType.ImageSplit:
                    return Color.FromArgb(18, 132, 219);
                case NodeType.If:
                case NodeType.MultiCondition:
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
