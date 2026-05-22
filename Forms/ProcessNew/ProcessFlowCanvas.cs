using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TDJS_Vision.Node;

namespace TDJS_Vision.Forms.ProcessNew
{
    /// <summary>
    /// Free flow-canvas surface for the ProcessNew editor.
    /// </summary>
    public class ProcessFlowCanvas : Panel
    {
        private const int GridSize = 24;
        private const int AnchorSize = 12;

        private Process _process;
        private NodeBase _dragNode;
        private Point _dragOffset;
        private NodeBase _connectionStartNode;
        private string _connectionStartAnchor;
        private Point _connectionEndPoint;
        private ProcessConnection _selectedConnection;

        public static readonly Size DefaultCanvasNodeSize = new Size(240, 64);

        public ProcessFlowCanvas()
        {
            DoubleBuffered = true;
            TabStop = true;
            AllowDrop = true;
            AutoScroll = true;
            AutoScrollMinSize = new Size(2400, 1600);
            BackColor = Color.FromArgb(246, 248, 252);
            NodeBase.NodeStatusChanged += NodeBase_NodeStatusChanged;

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
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                NodeBase.NodeStatusChanged -= NodeBase_NodeStatusChanged;

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
                    BeginInvoke((MethodInvoker)(() => InvalidateNode(node)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            InvalidateNode(node);
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

        public ProcessConnection SelectedConnection
        {
            get { return _selectedConnection; }
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
        }

        public void RefreshNodes()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            DrawGrid(graphics);

            if (_process == null)
                return;

            foreach (ProcessConnection connection in _process.Connections)
                DrawConnection(graphics, connection);

            if (_connectionStartNode != null)
                DrawTemporaryConnection(graphics);

            foreach (NodeBase node in _process.Nodes)
                DrawNode(graphics, node);
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
                SelectAtPoint(canvasPoint);
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
                SelectOnly(node);
                _dragNode = node;
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
                    ClearSelection();
            }

            Invalidate();
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
                _dragNode.CanvasLocation = ClampLocation(location, GetNodeSize(_dragNode));
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

            _dragNode = null;

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
                    _process.AddConnection(new ProcessConnection
                    {
                        FromNodeId = _connectionStartNode.ID,
                        ToNodeId = targetNode.ID,
                        FromAnchor = _connectionStartAnchor,
                        ToAnchor = targetAnchor,
                        Branch = branch
                    });
                }

                _connectionStartNode = null;
                _connectionStartAnchor = null;
                Invalidate();
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
            NodeBase node = SelectedNode;
            if (node == null || !node.DeleteFromProcess())
                return false;

            _selectedConnection = null;
            Invalidate();
            return true;
        }

        public bool DeleteSelectedConnection()
        {
            if (_process == null || _selectedConnection == null)
                return false;

            bool removed = _process.RemoveConnection(_selectedConnection);
            _selectedConnection = null;
            Invalidate();
            return removed;
        }

        public bool SetSelectedNodeAsStart()
        {
            NodeBase node = SelectedNode;
            if (_process == null || node == null)
                return false;

            foreach (NodeBase processNode in _process.Nodes)
                processNode.IsStartNode = processNode == node;

            Invalidate();
            return true;
        }

        public bool EnableSelectedNode()
        {
            NodeBase node = SelectedNode;
            if (node == null)
                return false;

            node.EnableNode();
            Invalidate();
            return true;
        }

        public bool DisableSelectedNode()
        {
            NodeBase node = SelectedNode;
            if (node == null)
                return false;

            node.DisableNode();
            Invalidate();
            return true;
        }

        public bool RenameSelectedNode()
        {
            NodeBase node = SelectedNode;
            if (node == null)
                return false;

            node.ShowRenameDialog();
            Invalidate();
            return true;
        }

        public bool EditSelectedNodeNotes()
        {
            NodeBase node = SelectedNode;
            if (node == null)
                return false;

            node.ShowNotesDialog();
            Invalidate();
            return true;
        }

        private void DrawGrid(Graphics graphics)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(225, 231, 240), 1F))
            {
                for (int x = 0; x <= AutoScrollMinSize.Width; x += GridSize)
                    graphics.DrawLine(gridPen, x, 0, x, AutoScrollMinSize.Height);

                for (int y = 0; y <= AutoScrollMinSize.Height; y += GridSize)
                    graphics.DrawLine(gridPen, 0, y, AutoScrollMinSize.Width, y);
            }
        }

        private void DrawNode(Graphics graphics, NodeBase node)
        {
            Rectangle bounds = GetNodeBounds(node);
            Color fillColor = GetNodeFillColor(node);
            Color statusColor = GetNodeStatusColor(node);
            Color borderColor = node.Selected ? Color.FromArgb(18, 132, 219) : statusColor;

            using (GraphicsPath body = CreateRoundRect(bounds, 6))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor, node.Selected ? 2.5F : 1.2F))
            {
                graphics.FillPath(fill, body);
                graphics.DrawPath(border, body);
            }

            Rectangle titleBand = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 10);
            NodeType nodeType = node.NodeType;
            using (SolidBrush titleBrush = new SolidBrush(GetNodeBandColor(nodeType)))
                graphics.FillRectangle(titleBrush, titleBand);

            using (Font titleFont = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold))
            using (Font metaFont = new Font("Microsoft YaHei", 8F, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(32, 47, 66)))
            using (SolidBrush metaBrush = new SolidBrush(Color.FromArgb(92, 107, 128)))
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
            DrawAnchor(graphics, node, "Right", nodeType == NodeType.If ? "T" : null);
            DrawAnchor(graphics, node, "Bottom", nodeType == NodeType.If ? "F" : null);
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
                ? Color.FromArgb(18, 132, 219)
                : GetConnectionColor(connection.Branch);
            DrawBezierConnection(graphics, start, end, connectionColor, false);
            DrawConnectionBranchLabel(graphics, connection, start, end, connectionColor);
        }

        private void DrawTemporaryConnection(Graphics graphics)
        {
            Point start = GetAnchorCenter(_connectionStartNode, _connectionStartAnchor);
            ProcessConnectionBranch branch = GetConnectionBranch(_connectionStartNode, _connectionStartAnchor);
            DrawBezierConnection(graphics, start, _connectionEndPoint, GetConnectionColor(branch), true);
        }

        private void DrawBezierConnection(Graphics graphics, Point start, Point end, Color color, bool dashed)
        {
            using (AdjustableArrowCap arrow = new AdjustableArrowCap(5, 6))
            using (Pen pen = new Pen(color, 2.2F))
            using (GraphicsPath path = CreateConnectionPath(start, end))
            {
                pen.CustomEndCap = arrow;
                if (dashed)
                    pen.DashStyle = DashStyle.Dash;
                graphics.DrawPath(pen, path);
            }
        }

        private void DrawAnchor(Graphics graphics, NodeBase node, string anchorName, string branchLabel = null)
        {
            Rectangle anchor = GetAnchorBounds(node, anchorName);
            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen border = new Pen(Color.FromArgb(18, 132, 219), 1.4F))
            {
                graphics.FillEllipse(fill, anchor);
                graphics.DrawEllipse(border, anchor);
            }

            if (!string.IsNullOrEmpty(branchLabel))
            {
                using (Font branchFont = new Font("Segoe UI", 7F, FontStyle.Bold))
                using (SolidBrush branchBrush = new SolidBrush(Color.FromArgb(74, 91, 113)))
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
            bounds.Offset(AutoScrollPosition.X, AutoScrollPosition.Y);
            Invalidate(bounds);
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
                    using (GraphicsPath path = CreateConnectionPath(start, end))
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
            foreach (NodeBase node in _process.Nodes)
                node.Selected = node == selectedNode;
        }

        private void SelectOnly(ProcessConnection selectedConnection)
        {
            _selectedConnection = selectedConnection;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;
        }

        private void ClearSelection()
        {
            if (_process == null)
                return;

            _selectedConnection = null;
            foreach (NodeBase node in _process.Nodes)
                node.Selected = false;
        }

        private void SelectAtPoint(Point canvasPoint)
        {
            NodeBase node = HitNode(canvasPoint);
            if (node != null)
            {
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
            int maxX = Math.Max(0, AutoScrollMinSize.Width - size.Width - 24);
            int maxY = Math.Max(0, AutoScrollMinSize.Height - size.Height - 24);
            int x = Math.Max(24, Math.Min(maxX, location.X));
            int y = Math.Max(24, Math.Min(maxY, location.Y));
            return new Point(x, y);
        }

        private Point ToCanvasPoint(Point controlPoint)
        {
            return new Point(
                controlPoint.X - AutoScrollPosition.X,
                controlPoint.Y - AutoScrollPosition.Y);
        }

        private bool OpenNodeParameters(NodeBase node)
        {
            Form form = node == null ? null : node.ParamForm as Form;
            if (node == null || !node.Active || form == null)
                return false;

            form.ShowDialog();
            return true;
        }

        private GraphicsPath CreateConnectionPath(Point start, Point end)
        {
            int bend = Math.Max(48, Math.Abs(end.X - start.X) / 2);
            Point control1 = new Point(start.X + bend, start.Y);
            Point control2 = new Point(end.X - bend, end.Y);
            GraphicsPath path = new GraphicsPath();
            path.AddBezier(start, control1, control2, end);
            return path;
        }

        private void DrawConnectionBranchLabel(
            Graphics graphics,
            ProcessConnection connection,
            Point start,
            Point end,
            Color color)
        {
            if (connection.Branch == ProcessConnectionBranch.Default)
                return;

            string label = connection.Branch == ProcessConnectionBranch.True ? "T" : "F";
            Point center = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            Rectangle badge = new Rectangle(center.X - 9, center.Y - 9, 18, 18);
            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen border = new Pen(color, 1.2F))
            using (Font font = new Font("Segoe UI", 7F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(color))
            {
                graphics.FillEllipse(fill, badge);
                graphics.DrawEllipse(border, badge);
                graphics.DrawString(label, font, textBrush, badge.X + 4, badge.Y + 2);
            }
        }

        private ProcessConnectionBranch GetConnectionBranch(NodeBase fromNode, string fromAnchor)
        {
            if (fromNode == null || fromNode.NodeType != NodeType.If)
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
                    return Color.FromArgb(47, 158, 68);
                case ProcessConnectionBranch.False:
                    return Color.FromArgb(217, 72, 15);
                default:
                    return Color.FromArgb(84, 101, 123);
            }
        }

        private Color GetNodeFillColor(NodeBase node)
        {
            if (!node.Active)
                return Color.FromArgb(228, 231, 236);

            switch (node.RuntimeStatus)
            {
                case NodeStatus.Running:
                    return Color.FromArgb(255, 249, 219);
                case NodeStatus.Successful:
                    return Color.FromArgb(235, 251, 238);
                case NodeStatus.Failed:
                    return Color.FromArgb(255, 245, 245);
                default:
                    return Color.White;
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
    }
}
