using TDJS_Vision.Node._3_Detection.ContourMatch;
using Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.CameraExposureGain;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._1_Acquisition.ImageSource3D;
using TDJS_Vision.Node._1_Acquisition.ImageShow3D;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageSplit;
using TDJS_Vision.Node._3_Detection.BatteryEar;
using TDJS_Vision.Node._3_Detection.BinaryAnalysis;
using TDJS_Vision.Node._3_Detection.ColorDiscern;
using TDJS_Vision.Node._3_Detection.FindCircle;
using TDJS_Vision.Node._3_Detection.FindLine;
using TDJS_Vision.Node._3_Detection.LargeModel;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._3_Detection.QRScan;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.Unsupervised;
using TDJS_Vision.Node._4_Measurement.BlobAnalysis;
using TDJS_Vision.Node._4_Measurement.CaliperCircle;
using TDJS_Vision.Node._4_Measurement.CaliperEllipse;
using TDJS_Vision.Node._4_Measurement.CaliperLine;
using TDJS_Vision.Node._4_Measurement.FindPoint;
using TDJS_Vision.Node._4_Measurement.LineLineAngle;
using TDJS_Vision.Node._4_Measurement.PositionCorrection;
using TDJS_Vision.Node._4_Measurement.PointLineDistance;
using TDJS_Vision.Node._4_Measurement.PointPointDistance;
using TDJS_Vision.Node._4_Measurement.PointRegionDistance;
using TDJS_Vision.Node._5_EquipmentCommunication.AIResultSend;
using TDJS_Vision.Node._5_EquipmentCommunication.CameraIO;
using TDJS_Vision.Node._5_EquipmentCommunication.CamerIOImprovement;
using TDJS_Vision.Node._5_EquipmentCommunication.ComSend;
using TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO;
using TDJS_Vision.Node._5_EquipmentCommunication.LightOpen;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusSoftTrigger;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte;
using TDJS_Vision.Node._5_EquipmentCommunication.TcpClient;
using TDJS_Vision.Node._5_EquipmentCommunication.TcpServer;
using TDJS_Vision.Node._6_LogicTool.ConditionRun;
using TDJS_Vision.Node._6_LogicTool.ArithmeticOperation;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._6_LogicTool.CSharpScript;
using TDJS_Vision.Node._6_LogicTool.Else;
using TDJS_Vision.Node._6_LogicTool.EndIf;
using TDJS_Vision.Node._6_LogicTool.If;
using TDJS_Vision.Node._6_LogicTool.MessageBox;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._6_LogicTool.ProcessSignal;
using TDJS_Vision.Node._6_LogicTool.ProcessTrigger;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision.Node._6_LogicTool.SleepTool;
using TDJS_Vision.Node._6_LogicTool.WaitProcessComplete;
using TDJS_Vision.Node._7_ResultProcessing.DataShow;
using TDJS_Vision.Node._7_ResultProcessing.GenerateExcelSpreadsheet;
using TDJS_Vision.Node._7_ResultProcessing.ImageDelete;
using TDJS_Vision.Node._7_ResultProcessing.ImageDraw;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;
using TDJS_Vision.Node._7_ResultProcessing.ResultSummarize;
using TDJS_Vision.Node._8_GeometryCreation.LineMergeFit;
using TDJS_Vision.Forms.GlobalSignalSettings;
using TDJS_Vision.Startup;
using static OpenCvSharp.ML.DTrees;
using static TDJS_Vision.Forms.ProcessNew.FormNewProcessWizard;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class ProcessEditPanel : UserControl
    {
        /// <summary>
        /// 绑定的流程
        /// </summary>
        private Process _process { get; set; }

        /// <summary>
        /// 流程优先级设置窗口
        /// </summary>
        private FormSetProcessLv _processLvSet { get; set; }

        private FormProcessGroupSetting formProcessGroupSetting { get; set; }

        /// <summary>
        /// 所有的节点控件
        /// </summary>
        private Stack<NodeBase> _stack = new Stack<NodeBase>();
        /// <summary>
        /// 共享节点复制快照，用于让一个流程页复制的节点可以粘贴到另一个流程页。
        /// </summary>
        private static NodeCopySnapshot _sharedCopiedNodeSnapshot;
        /// <summary>
        /// 复制快照递增编号，用于目标流程识别当前粘贴次数是否需要重置。
        /// </summary>
        private static int _copySnapshotSeed;
        /// <summary>
        /// 当前流程页对当前复制快照已经执行的粘贴次数。
        /// </summary>
        private int _pasteCount;
        /// <summary>
        /// 当前流程页最近一次使用的复制快照编号。
        /// </summary>
        private int _lastPasteCopySequence;
        private bool _isLoopRunRequested;
        private bool _updatingProcessEnableState;
        private Button _hoveredCommandButton;
        private enum CommandGlyph
        {
            Run,
            Loop,
            Stop
        }
        /// <summary>
        /// 流程编辑面板构造函数
        /// </summary>
        /// <param name="processName">流程名称。</param>
        /// <param name="showInfo">是否记录详细加载日志。</param>
        /// <param name="processConfig">需要恢复的流程配置。</param>
        /// <param name="startupNodeOffset">当前流程之前已经恢复的节点数量。</param>
        /// <param name="startupNodeTotal">本次方案需要恢复的节点总数。</param>
        public ProcessEditPanel(
            string processName,
            bool showInfo = true,
            ProcessConfig processConfig = null,
            int startupNodeOffset = 0,
            int startupNodeTotal = 0)
        {
            InitializeComponent();
            ConfigureCommandButtons();
            BindLanguage();
            _process = new Process(processName);
            processFlowCanvas.BindProcess(_process);
            processFlowCanvas.NodeCollectionChanged += ProcessFlowCanvas_NodeCollectionChanged;
            InitializeFlowCanvasOverlays();
            _processLvSet = new FormSetProcessLv(_process);
            formProcessGroupSetting = new FormProcessGroupSetting(_process);
            Process.UpdateRunStatus += RunStatusChange;
            Solution.Instance.UpdateRunStatus += RunStatusChange;
            Solution.Instance.AddProcess(_process);

            // 反序列化需要执行以下逻辑
            if(processConfig != null)
            {
                _process.ID = processConfig.ID;
                _process.ShowLog = processConfig.ShowLog;
                _process.IsPassiveTriggered = processConfig.IsPassiveTriggered;
                _process.IsCameraCallbackTriggered = processConfig.IsCameraCallbackTriggered;
                _process.OKNumber = processConfig.OKNumber;
                _process.NGNumber = processConfig.NGNumber;
                
                foreach (ToolStripMenuItem item in contextMenuStrip1.Items)
                {
                    if ("是否输出日志ToolStripMenuItem" == item.Name)
                    {
                        item.Checked = processConfig.ShowLog;
                    }
                    if ("是否为触发流程ToolStripMenuItem" == item.Name)
                    {
                        item.Checked = processConfig.IsPassiveTriggered;
                    }
                }
                _process.RunLv = processConfig.Level;
                _process.Group = processConfig.Group;
                _stack.Clear();
                UpdateNodeCountText();
                if(showInfo)
                    LogHelper.AddLog(MsgLevel.Debug, LanguageManager.Format("ProcessNew.LoadingProcess", _process.ProcessName), true);
                // 阻塞UI去创建流程
                CreateProcess(processConfig, showInfo, startupNodeOffset, startupNodeTotal);
                SetProcessEnable(processConfig.Enable, true, false);
                if(showInfo)
                    LogHelper.AddLog(MsgLevel.Debug, LanguageManager.Format("ProcessNew.ProcessLoaded", _process.ProcessName), true);
            }
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            Disposed += (s, e) =>
            {
                LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
                processFlowCanvas.NodeCollectionChanged -= ProcessFlowCanvas_NodeCollectionChanged;
                processFlowCanvas.OverlayInvalidated -= ProcessFlowCanvas_OverlayInvalidated;
                processFlowCanvas.SizeChanged -= ProcessFlowCanvas_SizeChanged;
                panelCanvasHost.Resize -= PanelCanvasHost_Resize;
            };
        }

        private void InitializeFlowCanvasOverlays()
        {
            imagePreviewOverlay.BindCanvas(processFlowCanvas);
            minimapOverlay.BindCanvas(processFlowCanvas);
            processFlowCanvas.OverlayInvalidated += ProcessFlowCanvas_OverlayInvalidated;
            processFlowCanvas.SizeChanged += ProcessFlowCanvas_SizeChanged;
            panelCanvasHost.Resize += PanelCanvasHost_Resize;
            UpdateFlowCanvasOverlayBounds();
        }

        private void ProcessFlowCanvas_OverlayInvalidated(object sender, FlowCanvasOverlayInvalidatedEventArgs e)
        {
            if (e == null || e.InvalidateImagePreview)
                imagePreviewOverlay.Invalidate();

            if (e == null || e.InvalidateMinimap)
                minimapOverlay.Invalidate();
        }

        private void ProcessFlowCanvas_SizeChanged(object sender, EventArgs e)
        {
            UpdateFlowCanvasOverlayBounds();
        }

        private void PanelCanvasHost_Resize(object sender, EventArgs e)
        {
            UpdateFlowCanvasOverlayBounds();
        }

        private void UpdateFlowCanvasOverlayBounds()
        {
            ApplyFlowCanvasOverlayBounds(
                imagePreviewOverlay,
                ProcessFlowCanvas.GetImagePreviewOverlayBounds(processFlowCanvas.ClientSize));
            ApplyFlowCanvasOverlayBounds(
                minimapOverlay,
                ProcessFlowCanvas.GetMinimapOverlayBounds(processFlowCanvas.ClientSize));

            imagePreviewOverlay.BringToFront();
            minimapOverlay.BringToFront();
        }

        private static void ApplyFlowCanvasOverlayBounds(Control overlay, Rectangle bounds)
        {
            bool visible = !bounds.IsEmpty;
            if (overlay.Visible != visible)
                overlay.Visible = visible;

            if (visible && overlay.Bounds != bounds)
                overlay.Bounds = bounds;

            if (visible)
                overlay.Invalidate();
        }

        private void ProcessFlowCanvas_NodeCollectionChanged(object sender, EventArgs e)
        {
            UpdateNodeCountText();
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(是否启用流程ToolStripMenuItem, "ProcessNew.EnableProcess");
            LanguageManager.Bind(设置流程优先级ToolStripMenuItem, "ProcessNew.SetPriority");
            LanguageManager.Bind(设置流程组别ToolStripMenuItem, "ProcessNew.SetGroup");
            LanguageManager.Bind(流程重命名ToolStripMenuItem, "ProcessNew.RenameProcess");
            LanguageManager.Bind(是否输出日志ToolStripMenuItem, "ProcessNew.OutputLog");
            LanguageManager.Bind(是否为触发流程ToolStripMenuItem, "ProcessNew.PassiveTriggeredProcess");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
            LanguageManager.ApplyToolStripItem(是否启用流程ToolStripMenuItem);
            LanguageManager.ApplyToolStripItem(设置流程优先级ToolStripMenuItem);
            LanguageManager.ApplyToolStripItem(设置流程组别ToolStripMenuItem);
            LanguageManager.ApplyToolStripItem(流程重命名ToolStripMenuItem);
            LanguageManager.ApplyToolStripItem(是否输出日志ToolStripMenuItem);
            LanguageManager.ApplyToolStripItem(是否为触发流程ToolStripMenuItem);
            buttonClean.Text = string.Empty;
            buttonRun.Text = string.Empty;
            buttonLoop.Text = string.Empty;
            buttonStop.Text = string.Empty;
            uiSwitchEnable.ActiveText = LanguageManager.T("Common.Enabled");
            uiSwitchEnable.InActiveText = LanguageManager.T("Common.Disabled");
            buttonRun.Enabled = true;
            buttonLoop.Enabled = true;
            buttonStop.Enabled = false;
            UpdateNodeCountText();
            UpdateRunTimeText();
            processFlowCanvas?.Invalidate();
        }

        private void ConfigureCommandButtons()
        {
            ConfigureCommandButton(buttonRun, CommandGlyph.Run);
            ConfigureCommandButton(buttonLoop, CommandGlyph.Loop);
            ConfigureCommandButton(buttonStop, CommandGlyph.Stop);
        }

        private void ConfigureCommandButton(Button button, CommandGlyph glyph)
        {
            if (button == null)
                return;

            button.Tag = glyph;
            button.Image = null;
            button.Text = string.Empty;
            button.Cursor = Cursors.Hand;
            button.BackColor = Color.FromArgb(44, 48, 52);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Paint += CommandButton_Paint;
            button.MouseEnter += CommandButton_MouseEnter;
            button.MouseLeave += CommandButton_MouseLeave;
            button.EnabledChanged += (s, e) => button.Invalidate();
        }

        private void CommandButton_MouseEnter(object sender, EventArgs e)
        {
            _hoveredCommandButton = sender as Button;
            _hoveredCommandButton?.Invalidate();
        }

        private void CommandButton_MouseLeave(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (_hoveredCommandButton == button)
                _hoveredCommandButton = null;

            button?.Invalidate();
        }

        private void CommandButton_Paint(object sender, PaintEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !(button.Tag is CommandGlyph glyph))
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(button.Enabled
                ? (_hoveredCommandButton == button ? Color.FromArgb(55, 60, 65) : Color.FromArgb(44, 48, 52))
                : Color.FromArgb(38, 42, 46));

            Rectangle bounds = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            using (Pen border = new Pen(Color.FromArgb(28, 31, 34)))
                e.Graphics.DrawRectangle(border, bounds);

            Color glyphColor = button.Enabled
                ? Color.FromArgb(118, 124, 130)
                : Color.FromArgb(74, 79, 84);

            switch (glyph)
            {
                case CommandGlyph.Run:
                    DrawRunGlyph(e.Graphics, button.ClientRectangle, glyphColor);
                    break;
                case CommandGlyph.Loop:
                    DrawLoopGlyph(e.Graphics, button.ClientRectangle, glyphColor);
                    break;
                case CommandGlyph.Stop:
                    DrawStopGlyph(e.Graphics, button.ClientRectangle, glyphColor);
                    break;
            }
        }

        private static void DrawRunGlyph(Graphics graphics, Rectangle bounds, Color color)
        {
            Point center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            Point[] points =
            {
                new Point(center.X - 6, center.Y - 9),
                new Point(center.X - 6, center.Y + 9),
                new Point(center.X + 9, center.Y)
            };

            using (SolidBrush brush = new SolidBrush(color))
                graphics.FillPolygon(brush, points);
        }

        private static void DrawStopGlyph(Graphics graphics, Rectangle bounds, Color color)
        {
            int size = 16;
            Rectangle rect = new Rectangle(
                bounds.Left + ((bounds.Width - size) / 2),
                bounds.Top + ((bounds.Height - size) / 2),
                size,
                size);

            using (SolidBrush brush = new SolidBrush(color))
                graphics.FillRectangle(brush, rect);
        }

        private static void DrawLoopGlyph(Graphics graphics, Rectangle bounds, Color color)
        {
            Rectangle rect = new Rectangle(
                bounds.Left + ((bounds.Width - 22) / 2),
                bounds.Top + ((bounds.Height - 22) / 2),
                22,
                22);

            using (Pen pen = new Pen(color, 2.6F))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawArc(pen, rect, 35, 250);
                graphics.DrawArc(pen, rect, 215, 250);

                PointF arrowA = PointOnEllipse(rect, 35);
                graphics.FillPolygon(brush, new[]
                {
                    new PointF(arrowA.X + 1, arrowA.Y - 7),
                    new PointF(arrowA.X + 7, arrowA.Y - 1),
                    new PointF(arrowA.X - 2, arrowA.Y + 1)
                });

                PointF arrowB = PointOnEllipse(rect, 215);
                graphics.FillPolygon(brush, new[]
                {
                    new PointF(arrowB.X - 1, arrowB.Y + 7),
                    new PointF(arrowB.X - 7, arrowB.Y + 1),
                    new PointF(arrowB.X + 2, arrowB.Y - 1)
                });
            }
        }

        private static PointF PointOnEllipse(Rectangle rect, double degrees)
        {
            double radians = degrees * Math.PI / 180D;
            return new PointF(
                rect.Left + rect.Width / 2F + (float)Math.Cos(radians) * rect.Width / 2F,
                rect.Top + rect.Height / 2F + (float)Math.Sin(radians) * rect.Height / 2F);
        }

        private void UpdateNodeCountText()
        {
            int nodeCount = _process == null ? _stack.Count : _process.Nodes.Count;
            label1.Text = LanguageManager.Format("ProcessNew.NodeCount", nodeCount);
        }

        private void UpdateRunTimeText()
        {
            label2.Text = LanguageManager.Format("ProcessNew.Elapsed", _process?.RunTime ?? 0);
        }

        private void RunStatusChange(object sender, ProcessRunResult e)
        {
            // 线程安全判断 + 操作UI控件
            if (_process.ProcessName == e.ProcessName)
            {
                // 跨线程操作UI，必须用 Invoke
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        UpdateRunTimeText();
                        buttonRun.Enabled = !e.IsRunning && !_isLoopRunRequested;
                        buttonLoop.Enabled = !e.IsRunning && !_isLoopRunRequested;
                        buttonStop.Enabled = e.IsRunning || _isLoopRunRequested;
                        uiSwitchEnable.Enabled = !e.IsRunning;
                        uiLedBulb1.Color = e.IsRunning ? Color.DarkGray : e.IsSuccess ? Color.LawnGreen : Color.Red;
                        RefreshRuntimeCanvasState();
                    }));
                }
                else
                {
                    UpdateRunTimeText();
                    buttonRun.Enabled = !e.IsRunning && !_isLoopRunRequested;
                    buttonLoop.Enabled = !e.IsRunning && !_isLoopRunRequested;
                    buttonStop.Enabled = e.IsRunning || _isLoopRunRequested;
                    uiSwitchEnable.Enabled = !e.IsRunning;
                    uiLedBulb1.Color = e.IsRunning ? Color.DarkGray : e.IsSuccess ? Color.LawnGreen : Color.Red;
                    RefreshRuntimeCanvasState();
                }
            }
        }

        /// <summary>
        /// 拖拽进入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NodeEditPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DragDataFormat))
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 拖拽经过流程画布时持续声明可接收状态，避免部分控件切换焦点后丢失 Drop 效果。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">拖拽事件参数。</param>
        private void NodeEditPanel_DragOver(object sender, DragEventArgs e)
        {
            NodeEditPanel_DragEnter(sender, e);
        }

        /// <summary>
        /// 拖拽放下
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NodeEditPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DragDataFormat))
            {
                DragData data = (DragData)e.Data.GetData(DragDataFormat);
                try
                {
                    Point? canvasLocation = null;
                    if (sender == processFlowCanvas)
                        canvasLocation = processFlowCanvas.ScreenToCanvasPoint(new Point(e.X, e.Y));

                    CreateNode(data.NodeType, data.Text, -1, canvasLocation);
                }
                catch (Exception ex)
                {
                    MessageBoxTD.Show(ex.Message, LanguageManager.T("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
                }
            }
        }
        /// <summary>
        /// 根据配置恢复流程节点、参数和画布连接。
        /// </summary>
        /// <param name="processConfig">流程配置。</param>
        /// <param name="showInfo">是否记录详细加载日志。</param>
        /// <param name="startupNodeOffset">当前流程之前已经恢复的节点数量。</param>
        /// <param name="startupNodeTotal">本次方案需要恢复的节点总数。</param>
        private void CreateProcess(
            ProcessConfig processConfig,
            bool showInfo,
            int startupNodeOffset,
            int startupNodeTotal)
        {
            _process.HasCanvasGraph = processConfig.HasCanvasGraph;
            int nodeIndex = 0;
            int progressNodeIndex = 0;
            foreach (var nodeInfo in processConfig.NodeInfos)
            {
                progressNodeIndex++;
                StartupProgressContext.ReportItem(
                    "正在恢复流程节点",
                    $"{_process.ProcessName} / {nodeInfo.NodeName}",
                    startupNodeOffset + progressNodeIndex,
                    startupNodeTotal,
                    86,
                    95);

                NodeBase nodeBase = null;
                try
                {
                    Point canvasLocation = nodeInfo.HasCanvasLayout
                        ? new Point(nodeInfo.CanvasX, nodeInfo.CanvasY)
                        : processFlowCanvas.GetDefaultNodeLocation(nodeIndex);

                    // 1.还原节点
                    nodeBase = CreateNode(nodeInfo.NodeType, nodeInfo.NodeName, nodeInfo.ID, canvasLocation, false, false);
                    nodeBase.Selected = nodeInfo.Selected;
                    nodeBase.Active = nodeInfo.Active;
                    nodeBase.OutputLog = nodeInfo.OutputLog ?? true;
                    nodeBase.IsStartNode = nodeInfo.IsStartNode ||
                        (!processConfig.HasCanvasGraph && nodeIndex == 0);
                    if (nodeInfo.CanvasWidth > 0 && nodeInfo.CanvasHeight > 0)
                        nodeBase.CanvasSize = new Size(nodeInfo.CanvasWidth, nodeInfo.CanvasHeight);

                    if (nodeInfo.NodeParam != null)
                    {
                        // 2.还原节点的参数
                        nodeBase.ParamForm.Params = nodeInfo.NodeParam;
                        // 3.节点参数到参数设置界面
                        nodeBase.ParamForm.SetParam2Form();
                    }
                    if (showInfo)
                        LogHelper.AddLog(MsgLevel.Info, LanguageManager.Format("ProcessNew.NodeLoaded", nodeInfo.ID, nodeInfo.NodeName), true);
                }
                catch (Exception ex)
                {
                    var nodeLoadException = new InvalidOperationException(
                        $"流程“{_process.ProcessName}”的节点“{nodeInfo.NodeName}”恢复失败。",
                        ex);
                    if (StartupProgressContext.IsActive)
                        StartupProgressContext.ReportFailure("恢复流程节点", nodeLoadException);

                    LogHelper.AddLog(
                        MsgLevel.Exception,
                        LanguageManager.Format("ProcessNew.NodeLoadFailed", nodeInfo.ID, nodeInfo.NodeName, ex.Message),
                        true);
                    continue;
                }

                nodeIndex++;
            }

            RestoreProcessConnections(processConfig);
            processFlowCanvas.RefreshNodes();
        }

        /// <summary>
        /// 创建一个对应派生类型的节点，id参数只有反序列化需要传入
        /// </summary>
        /// <param name="nodeType"></param>
        /// <param name="nodeName"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private NodeBase CreateNode(NodeType nodeType, string nodeName, int id = -1, Point? canvasLocation = null, bool trackUndo = true, bool applyCanvasSnap = true)
        {

            NodeBase node = null;

            // 反序列化节点时使用原来的id
            // 而正常创建节点使用新id
            int nodeId = id;
            if (id == -1) //表示不是反序列化，正常创建对象
                nodeId = ++Solution.Instance.NodeCount;

            #region 根据text创建对应类型的节点

            switch (nodeType)
            {
                case NodeType.LightSourceControl:
                    node = new NodeLight(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.PLCRead:
                    node = new NodePlcRead(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.PLCWrite:
                    node = new NodePlcWrite(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.AITD:
                    node = new NodeTDAI(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.UnsupervisedDetection:
                    node = new NodeUnsupervisedDetection(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.LargeModelDetection:
                    node = new NodeLargeModelDetection(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageSave:
                    node = new NodeImageSave(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.SleepTool:
                    node = new NodeSleepTool(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.WaitSoftTrigger:
                    node = new NodeWaitSoftTrigger(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.DetectResultShow:
                    node = new NodeDataShow(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.Summarize:
                    node = new NodeSummarize(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.LineFind:
                    node = new NodeFIndLine(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CircleFind:
                    node = new NodeFIndCircle(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CaliperLine:
                    node = new NodeCaliperLine(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CaliperCircle:
                    node = new NodeCaliperCircle(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CaliperEllipse:
                    node = new NodeCaliperEllipse(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.FindPoint:
                    node = new NodeFindPoint(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.PositionCorrection:
                    node = new NodePositionCorrection(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.TerminalAngle:
                    node = new TDJS_Vision.Node._4_Measurement.TerminalAngle.NodeTerminalAngle(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.LineLineAngle:
                    node = new NodeLineLineAngle(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.PointPointDistance:
                    node = new NodePointPointDistance(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.PointLineDistance:
                    node = new NodePointLineDistance(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.PointRegionDistance:
                    node = new NodePointRegionDistance(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.LineMergeFit:
                    node = new NodeLineMergeFit(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageCrop:
                    node = new NodeImageCrop(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageShow:
                    node = new NodeImageShow(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageShow3D:
                    node = new NodeImageShow3D(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ModbusRead:
                    node = new NodeModbusRead(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ModbusWrite:
                    node = new NodeModbusWrite(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.TCPClientRequest:
                    node = new NodeTCPClient(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.TCPServerResponse:
                    node = new NodeTCPServer(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageRotate:
                    node = new NodeImageRotate(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ModbusSoftTrigger:
                    node = new NodeModbusSoftTrigger(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.AIResultSend:
                    node = new NodeSignalSend(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CameraIO:
                    node = new NodeCameraIO(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CameraExposureGain:
                    node = new NodeCameraExposureGain(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageSource:
                    node = new NodeImageSource(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageSource3D:
                    node = new NodeImageSource3D(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageSplit:
                    node = new NodeImageSplit(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImagePreprocess:
                    node = new NodeImagePreprocess(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.QRScan:
                    node = new NodeQRScan(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.MatchTemplate:
                    node = new NodeMatchTemplate(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ContourMatch:
                    node = new NodeContourMatch(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.NccMatchTemplate:
                    node = new NodeNccMatchTemplate(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.BlobAnalysis:
                    node = new NodeBlobAnalysis(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageFileDelete:
                    node = new NodeImageDelete(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.SharedVariable:
                    node = new NodeSharedVariable(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.GenerateExcel:
                    node = new NodeGenerateExcel(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ResultSend:
                    node = new TDJS_Vision.Node._7_ResultProcessing.ResultSend.NodeResultSend(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.DrawAIResult:
                    node = new NodeImageDraw(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ResultOverlayDraw:
                    node = new NodeResultOverlayDraw(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ResultOverlayDraw2:
                    node = new NodeResultOverlayDraw2(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ConditionRun:
                    node = new NodeConditionRun(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ProcessTrigger:
                    node = new NodeProcessTrigger(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ProcessSignal:
                    node = new NodeProcessSignal(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.If:
                    node = new NodeIf(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.MultiCondition:
                    node = new NodeMultiCondition(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ArithmeticOperation:
                    node = new NodeArithmeticOperation(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CompositeModule:
                    node = new NodeCompositeModule(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CompositeInput:
                    node = new NodeCompositeInput(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CompositeOutput:
                    node = new NodeCompositeOutput(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.Else:
                    node = new NodeElse(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.EndIf:
                    node = new NodeEndIf(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.BatteryEar:
                    node = new NodeBatteryEar(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CSharpScript:
                    node = new NodeCSharpScript(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ComSend:
                    node = new NodeComSend(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.WaitProcessComplete:
                    node = new NodeWaitProcessComplete(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.MessageBox:
                    node = new NodeMessageBox(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ERUIIO:
                    node = new NodeERUIIO(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.RGBDiscern:
                    node = new NodeColorDiscern(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.BinarizationAnalysis:
                    node = new NodeBinaryAnalysis(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.CameraIOManual:
                    node = new NodeCameraIOManual(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ReadFlag:
                    node = new NodeFlagRead(nodeId, nodeName, _process, nodeType);
                    break;
                default:
                    break;
                    //工具栏图标颜色：蓝 #1296db 紫 #56227a
            }
            if (node == null)
            {
                --Solution.Instance.NodeCount;
                throw new Exception(LanguageManager.T("ProcessNew.NodeCreateFailed"));
            }
            node.CanvasSize = ProcessFlowCanvas.DefaultCanvasNodeSize;
            if (_process.Nodes.Count == 0)
                node.IsStartNode = true;

            Point location = canvasLocation ?? processFlowCanvas.GetDefaultNodeLocation(_process.Nodes.Count);
            if (applyCanvasSnap)
                location = processFlowCanvas.GetInteractiveNodeLocation(location, node.CanvasSize);

            node.CanvasLocation = location;
            foreach (NodeBase processNode in _process.Nodes)
                processNode.Selected = false;
            node.Selected = true;
            NodeBase.NodeDeletedEvent += NewNode_NodeDeletedEvent;
            _stack.Push(node);
            Solution.Instance.Nodes.Add(node);
            _process.AddNode(node);
            processFlowCanvas.AddNode(node, location);
            if (trackUndo)
                processFlowCanvas.RegisterNodeCreated(node);
            UpdateNode();

            #endregion

            //更新节点数量到界面
            UpdateNodeCountText();

            return node;
        }

        /// <summary>
        /// 节点删除事件处理器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewNode_NodeDeletedEvent(object sender, NodeBase e)
        {
            //使用Stack<Node> 来临时存储控件，因为不能在迭代Stack时修改它
            Stack<NodeBase> tmp = new Stack<NodeBase>(_stack);
            // 清空原栈
            _stack.Clear();
            foreach (NodeBase node in tmp)
            {
                // 如果控件的Name与目标控件不同，再压入栈中
                if (node.ID != e.ID)
                {
                    _stack.Push(node);
                }
            }

            _process.RemoveNodeConnections(e.ID);
            UpdateNode();

            //更新节点数量到界面
            UpdateNodeCountText();
        }

        /// <summary>
        /// 刷新控件
        /// </summary>
        private void UpdateNode()
        {
            processFlowCanvas.RefreshNodes();
        }

        private void RestoreProcessConnections(ProcessConfig processConfig)
        {
            _process.Connections.Clear();

            if (processConfig.ConnectionInfos != null && processConfig.ConnectionInfos.Count > 0)
            {
                foreach (var connectionInfo in processConfig.ConnectionInfos)
                {
                    if (!ProcessContainsNode(connectionInfo.FromNodeId) ||
                        !ProcessContainsNode(connectionInfo.ToNodeId))
                        continue;

                    ProcessConnection connection = new ProcessConnection
                    {
                        FromNodeId = connectionInfo.FromNodeId,
                        ToNodeId = connectionInfo.ToNodeId,
                        FromAnchor = string.IsNullOrEmpty(connectionInfo.FromAnchor) ? "Right" : connectionInfo.FromAnchor,
                        ToAnchor = string.IsNullOrEmpty(connectionInfo.ToAnchor) ? "Left" : connectionInfo.ToAnchor,
                        Branch = connectionInfo.Branch
                    };
                    _process.NormalizeConnectionBranch(connection);

                    if (!string.IsNullOrEmpty(connectionInfo.ID))
                        connection.ID = connectionInfo.ID;

                    _process.Connections.Add(connection);
                }

                _process.NotifyConnectionsChanged();
                return;
            }

            if (!processConfig.HasCanvasGraph)
                CreateLegacyOrderedConnections();

            _process.NotifyConnectionsChanged();
        }

        private void CreateLegacyOrderedConnections()
        {
            for (int index = 0; index < _process.Nodes.Count - 1; index++)
            {
                _process.Connections.Add(new ProcessConnection
                {
                    FromNodeId = _process.Nodes[index].ID,
                    ToNodeId = _process.Nodes[index + 1].ID,
                    FromAnchor = "Legacy",
                    ToAnchor = "Left"
                });
            }
        }

        private bool ProcessContainsNode(int nodeId)
        {
            foreach (var node in _process.Nodes)
            {
                if (node.ID == nodeId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 点击清理状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonClean_Click(object sender, EventArgs e)
        {
            foreach (var node in _process.Nodes)
            {
                node.SetStatus(NodeStatus.Unexecuted, "*");
            }
        }

        /// <summary>
        /// 点击运行流程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button1_Click(object sender, EventArgs e)
        {
            if (_isLoopRunRequested)
                return;

            if (!ValidateCameraConfigurationBeforeRun())
                return;

            if (!Solution.Instance.TryBeginExternalRunSession())
                return;

            bool listenStarted = false;
            List<ICamera> startedCallbackCameras = null;
            try
            {
                startedCallbackCameras = Solution.Instance.StartCameraCallbackGrabbingForProcess(_process);
                FormGlobalSignal.StartListenSignals();
                listenStarted = true;
                _process.IsHandRun = true;
                await RunCurrentProcessInBackgroundAsync(false);
            }
            catch (Exception) { }
            finally
            {
                try
                {
                    Solution.Instance.StopStartedCameraCallbackGrabbing(startedCallbackCameras);
                    _process.IsHandRun = false;
                    if (listenStarted)
                        FormGlobalSignal.StopListenSignals();

                    RefreshRuntimeCanvasState();
                }
                finally
                {
                    Solution.Instance.EndExternalRunSession();
                }
            }
        }

        /// <summary>
        /// 点击循环运行流程
        /// </summary>
        private async void buttonLoop_Click(object sender, EventArgs e)
        {
            if (_isLoopRunRequested)
                return;

            if (!ValidateCameraConfigurationBeforeRun())
                return;

            if (!Solution.Instance.TryBeginExternalRunSession())
                return;

            _isLoopRunRequested = true;
            buttonRun.Enabled = false;
            buttonLoop.Enabled = false;
            buttonStop.Enabled = true;
            bool listenStarted = false;
            List<ICamera> startedCallbackCameras = null;
            try
            {
                if (_process.Nodes.Count == 0 || !_process.Enable)
                    return;

                FormGlobalSignal.StartListenSignals();
                listenStarted = true;
                startedCallbackCameras = Solution.Instance.StartCameraCallbackGrabbingForProcess(_process);

                while (!Solution.Instance.CancellationToken.IsCancellationRequested)
                {
                    _process.IsHandRun = true;
                    await RunCurrentProcessInBackgroundAsync(true);

                    int interval = Math.Max(1, Solution.Instance.RunInterval);
                    await System.Threading.Tasks.Task.Delay(interval, Solution.Instance.CancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
            finally
            {
                try
                {
                    Solution.Instance.StopStartedCameraCallbackGrabbing(startedCallbackCameras);
                    _process.IsHandRun = false;
                    if (listenStarted)
                        FormGlobalSignal.StopListenSignals();

                    _isLoopRunRequested = false;
                    buttonRun.Enabled = true;
                    buttonLoop.Enabled = true;
                    buttonStop.Enabled = false;
                    RefreshRuntimeCanvasState();
                }
                finally
                {
                    Solution.Instance.EndExternalRunSession();
                }
            }
        }

        /// <summary>
        /// 校验当前流程的硬触发图像源是否错误地配置了上游节点。
        /// </summary>
        /// <returns>当前流程允许启动时返回 true。</returns>
        private bool ValidateCameraConfigurationBeforeRun()
        {
            string message;
            if (Solution.Instance.TryValidateCameraConfiguration(new[] { _process }, out message))
                return true;

            MessageBoxTD.Show(message, "相机触发配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        /// <summary>
        /// 刷新流程画布运行状态，避免手动运行后台线程结束后节点状态等到鼠标交互才重绘。
        /// </summary>
        private void RefreshRuntimeCanvasState()
        {
            if (processFlowCanvas == null || processFlowCanvas.IsDisposed)
                return;

            processFlowCanvas.RefreshRuntimeStateNow();
        }

        /// <summary>
        /// 在后台线程执行当前流程，避免手动运行时同步节点计算阻塞流程图交互。
        /// </summary>
        /// <param name="isCyclical">是否循环运行。</param>
        /// <returns>流程运行任务。</returns>
        private System.Threading.Tasks.Task RunCurrentProcessInBackgroundAsync(bool isCyclical)
        {
            System.Threading.Tasks.TaskCompletionSource<object> completionSource = new System.Threading.Tasks.TaskCompletionSource<object>();
            System.Threading.Thread runThread = new System.Threading.Thread(() =>
            {
                try
                {
                    _process.Run(isCyclical).GetAwaiter().GetResult();
                    completionSource.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
            });

            runThread.IsBackground = true;
            runThread.Name = "流程手动运行-" + _process.ProcessName;
            runThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            runThread.Start();
            return completionSource.Task;
        }

        /// <summary>
        /// 点击停止流程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStop_Click(object sender, EventArgs e)
        {
            Solution.Instance.CancelToken();
            FormGlobalSignal.StopListenSignals();
            _isLoopRunRequested = false;
            buttonRun.Enabled = true;
            buttonLoop.Enabled = true;
            buttonStop.Enabled = false;
        }

        /// <summary>
        /// 设置流程状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void uiSwitch1_ValueChanged(object sender, bool value)
        {
            if (_updatingProcessEnableState)
                return;

            SetProcessEnable(value, false, true);
        }

        private void SetProcessEnable(bool enabled, bool updateSwitch, bool writeLog)
        {
            if (_process == null)
                return;

            bool changed = _process.Enable != enabled;
            _process.Enable = enabled;

            if (是否启用流程ToolStripMenuItem.Checked != enabled)
                是否启用流程ToolStripMenuItem.Checked = enabled;

            if (updateSwitch && uiSwitchEnable.Active != enabled)
            {
                _updatingProcessEnableState = true;
                try
                {
                    uiSwitchEnable.Active = enabled;
                }
                finally
                {
                    _updatingProcessEnableState = false;
                }
            }

            if (!writeLog || !changed)
                return;

            if (enabled)
                LogHelper.AddLog(MsgLevel.Info, LanguageManager.Format("ProcessNew.ProcessEnabled", _process.ProcessName), true);
            else
                LogHelper.AddLog(MsgLevel.Info, LanguageManager.Format("ProcessNew.ProcessDisabled", _process.ProcessName), true);
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            是否启用流程ToolStripMenuItem.Checked = _process == null || _process.Enable;
            是否启用流程ToolStripMenuItem.Enabled = uiSwitchEnable.Enabled;
            是否输出日志ToolStripMenuItem.Checked = _process == null || _process.ShowLog;
            是否为触发流程ToolStripMenuItem.Checked = _process != null && _process.IsPassiveTriggered;
        }

        private void 是否启用流程ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessEnable(是否启用流程ToolStripMenuItem.Checked, true, true);
        }

        private void 设置流程优先级ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _processLvSet.ShowDialog();
        }

        private void 设置流程组别ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            formProcessGroupSetting.ShowDialog();
        }

        private void 流程重命名ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormProcessRename formProcessRename = new FormProcessRename();
            formProcessRename.SetProcess(_process);
            formProcessRename.ShowDialog();
        }
        /// <summary>
        /// 是否输出日志菜单点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 是否输出日志ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            是否输出日志ToolStripMenuItem.Checked = !是否输出日志ToolStripMenuItem.Checked;
            _process.ShowLog = 是否输出日志ToolStripMenuItem.Checked;
        }

        private void 是否为触发流程ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            是否为触发流程ToolStripMenuItem.Checked = !是否为触发流程ToolStripMenuItem.Checked;
            _process.IsPassiveTriggered = 是否为触发流程ToolStripMenuItem.Checked;
        }

        /// <summary>
        /// 显示上下文菜单
        /// </summary>
        private void label1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                contextMenuStrip1.Show(Cursor.Position);
        }

        private void contextMenuStripFlowCanvas_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            NodeBase selectedNode = processFlowCanvas.SelectedNode;
            int selectedNodeCount = processFlowCanvas.SelectedNodeCount;
            bool hasNode = selectedNodeCount > 0;
            bool hasSingleNode = selectedNodeCount == 1;
            bool hasConnection = processFlowCanvas.SelectedConnection != null;
            bool canPaste = HasCopiedNodeSnapshot();
            FlowCanvasContextMenuTarget menuTarget = GetValidFlowCanvasMenuTarget(hasNode, hasConnection);
            bool isCanvasMenu = menuTarget == FlowCanvasContextMenuTarget.Canvas;
            bool isNodeMenu = menuTarget == FlowCanvasContextMenuTarget.Node;
            bool isConnectionMenu = menuTarget == FlowCanvasContextMenuTarget.Connection;
            bool hasActiveNode = false;
            bool hasInactiveNode = false;
            bool allSelectedNodesOutputLog = hasNode;

            foreach (NodeBase node in processFlowCanvas.SelectedNodes)
            {
                if (node == null)
                {
                    allSelectedNodesOutputLog = false;
                    continue;
                }

                if (node.Active)
                    hasActiveNode = true;
                else
                    hasInactiveNode = true;

                if (!node.OutputLog)
                    allSelectedNodesOutputLog = false;
            }

            撤销ToolStripMenuItem.Enabled = processFlowCanvas.CanUndo;
            重做ToolStripMenuItem.Enabled = processFlowCanvas.CanRedo;
            复制节点ToolStripMenuItem.Enabled = hasNode;
            粘贴节点ToolStripMenuItem.Enabled = canPaste;
            吸附到网格ToolStripMenuItem.Checked = processFlowCanvas.SnapToGridEnabled;
            打开节点参数ToolStripMenuItem.Enabled = hasSingleNode && selectedNode.Active;
            设为起始节点ToolStripMenuItem.Enabled = hasSingleNode && !selectedNode.IsStartNode;
            启用节点ToolStripMenuItem.Enabled = hasNode && hasInactiveNode;
            禁用节点ToolStripMenuItem.Enabled = hasNode && hasActiveNode;
            节点是否输出日志ToolStripMenuItem.Enabled = hasNode;
            节点是否输出日志ToolStripMenuItem.Checked = allSelectedNodesOutputLog;
            重命名节点ToolStripMenuItem.Enabled = hasSingleNode;
            添加节点备注ToolStripMenuItem.Enabled = hasSingleNode;
            删除节点ToolStripMenuItem.Enabled = hasNode;
            删除连线ToolStripMenuItem.Enabled = hasConnection;
            撤销ToolStripMenuItem.Visible = isCanvasMenu;
            重做ToolStripMenuItem.Visible = isCanvasMenu;
            toolStripSeparatorCanvasHistory.Visible = isCanvasMenu;
            复制节点ToolStripMenuItem.Visible = isNodeMenu;
            粘贴节点ToolStripMenuItem.Visible = isCanvasMenu;
            toolStripSeparatorCanvasClipboard.Visible = isCanvasMenu || isNodeMenu;
            放大ToolStripMenuItem.Visible = isCanvasMenu;
            缩小ToolStripMenuItem.Visible = isCanvasMenu;
            还原缩放ToolStripMenuItem.Visible = isCanvasMenu;
            吸附到网格ToolStripMenuItem.Visible = isCanvasMenu;
            toolStripSeparatorCanvasZoom.Visible = false;
            打开节点参数ToolStripMenuItem.Visible = isNodeMenu;
            设为起始节点ToolStripMenuItem.Visible = isNodeMenu;
            启用节点ToolStripMenuItem.Visible = isNodeMenu;
            禁用节点ToolStripMenuItem.Visible = isNodeMenu;
            节点是否输出日志ToolStripMenuItem.Visible = isNodeMenu;
            重命名节点ToolStripMenuItem.Visible = isNodeMenu;
            添加节点备注ToolStripMenuItem.Visible = isNodeMenu;
            删除节点ToolStripMenuItem.Visible = isNodeMenu;
            删除连线ToolStripMenuItem.Visible = isConnectionMenu;
            e.Cancel = false;
        }

        /// <summary>
        /// 获取可用的流程画布右键菜单目标，避免选择状态被外部改变后显示空菜单。
        /// </summary>
        /// <param name="hasNode">当前是否选中了节点。</param>
        /// <param name="hasConnection">当前是否选中了连线。</param>
        /// <returns>最终用于显示菜单项的目标类型。</returns>
        private FlowCanvasContextMenuTarget GetValidFlowCanvasMenuTarget(bool hasNode, bool hasConnection)
        {
            FlowCanvasContextMenuTarget target = processFlowCanvas.LastContextMenuTarget;
            if (target == FlowCanvasContextMenuTarget.Node && !hasNode)
                return FlowCanvasContextMenuTarget.Canvas;

            if (target == FlowCanvasContextMenuTarget.Connection && !hasConnection)
                return FlowCanvasContextMenuTarget.Canvas;

            return target;
        }

        private void 撤销ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.Undo();
        }

        private void 重做ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.Redo();
        }

        private void 复制节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                CopySelectedNode();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message, LanguageManager.T("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void 粘贴节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                PasteCopiedNode();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message, LanguageManager.T("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
        }

        private void 放大ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.ZoomIn();
        }

        private void 缩小ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.ZoomOut();
        }

        private void 还原缩放ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.ResetZoom();
        }

        private void 吸附到网格ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.SnapToGridEnabled = 吸附到网格ToolStripMenuItem.Checked;
        }

        private void 打开节点参数ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.OpenSelectedNodeParameters();
        }

        private void 删除节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.DeleteSelectedNode();
        }

        private void 设为起始节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.SetSelectedNodeAsStart();
        }

        private void 启用节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.EnableSelectedNode();
        }

        private void 禁用节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.DisableSelectedNode();
        }

        /// <summary>
        /// 批量切换选中节点是否允许输出运行日志。
        /// </summary>
        private void 节点是否输出日志ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool outputLog = 节点是否输出日志ToolStripMenuItem.Checked;
            foreach (NodeBase node in processFlowCanvas.SelectedNodes)
            {
                if (node != null)
                    node.OutputLog = outputLog;
            }
            processFlowCanvas.RefreshNodes();
        }

        private void 重命名节点ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.RenameSelectedNode();
        }

        private void 添加节点备注ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.EditSelectedNodeNotes();
        }

        private void 删除连线ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            processFlowCanvas.DeleteSelectedConnection();
        }

        private bool CopySelectedNode()
        {
            List<NodeBase> selectedNodes = processFlowCanvas.SelectedNodes;
            if (selectedNodes.Count == 0)
                return false;

            NodeCopySnapshot snapshot = new NodeCopySnapshot
            {
                CopySequence = ++_copySnapshotSeed,
                SourceProcessId = _process == null ? -1 : _process.ID,
                SourceProcessName = _process == null ? string.Empty : _process.ProcessName,
                SourceNodeTexts = new Dictionary<int, string>(),
                Nodes = new List<NodeCopyItem>(),
                Connections = new List<ConnectionCopySnapshot>()
            };

            if (_process != null)
            {
                foreach (NodeBase processNode in _process.Nodes)
                    snapshot.SourceNodeTexts[processNode.ID] = GetNodeText(processNode.ID, processNode.NodeName);
            }

            Dictionary<int, NodeBase> selectedNodeMap = new Dictionary<int, NodeBase>();
            foreach (NodeBase node in selectedNodes)
            {
                selectedNodeMap[node.ID] = node;
                snapshot.Nodes.Add(new NodeCopyItem
                {
                    SourceNodeId = node.ID,
                    NodeType = node.NodeType,
                    NodeName = node.NodeName,
                    Active = node.Active,
                    OutputLog = node.OutputLog,
                    CanvasLocation = node.CanvasLocation,
                    CanvasSize = node.CanvasSize,
                    Notes = node.Notes,
                    NodeParam = CloneNodeParam(node.ParamForm == null ? null : node.ParamForm.Params)
                });
            }

            foreach (ProcessConnection connection in _process.Connections)
            {
                if (!selectedNodeMap.ContainsKey(connection.FromNodeId) ||
                    !selectedNodeMap.ContainsKey(connection.ToNodeId))
                {
                    continue;
                }

                snapshot.Connections.Add(new ConnectionCopySnapshot
                {
                    FromNodeId = connection.FromNodeId,
                    ToNodeId = connection.ToNodeId,
                    FromAnchor = connection.FromAnchor,
                    ToAnchor = connection.ToAnchor,
                    Branch = connection.Branch
                });
            }

            _sharedCopiedNodeSnapshot = snapshot;
            _pasteCount = 0;
            _lastPasteCopySequence = snapshot.CopySequence;
            return true;
        }

        private bool PasteCopiedNode()
        {
            NodeCopySnapshot snapshot = _sharedCopiedNodeSnapshot;
            if (snapshot == null ||
                snapshot.Nodes == null ||
                snapshot.Nodes.Count == 0)
                return false;

            if (_lastPasteCopySequence != snapshot.CopySequence)
            {
                _pasteCount = 0;
                _lastPasteCopySequence = snapshot.CopySequence;
            }

            List<NodeBase> pastedNodes = new List<NodeBase>();
            List<ProcessConnection> pastedConnections = new List<ProcessConnection>();
            List<string> unresolvedReferences = new List<string>();
            try
            {
                bool isCrossProcessPaste = !snapshot.IsSourceProcess(_process);
                Point pasteOffset = GetNextPasteOffset(snapshot);
                Dictionary<int, NodeBase> pastedNodeMap = new Dictionary<int, NodeBase>();
                Dictionary<string, string> nodeTextMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<int, int> nodeIdMap = new Dictionary<int, int>();

                foreach (NodeCopyItem item in snapshot.Nodes)
                {
                    Point pasteLocation = new Point(
                        item.CanvasLocation.X + pasteOffset.X,
                        item.CanvasLocation.Y + pasteOffset.Y);
                    string nodeName = GetUniquePastedNodeName(item.NodeName);
                    NodeBase pastedNode = CreateNode(item.NodeType, nodeName, -1, pasteLocation, false, false);
                    pastedNode.CanvasSize = item.CanvasSize;
                    pastedNode.Active = item.Active;
                    pastedNode.OutputLog = item.OutputLog;
                    pastedNode.SetNotes(item.Notes);

                    if (item.NodeParam != null && pastedNode.ParamForm != null)
                        pastedNode.ParamForm.Params = CloneNodeParam(item.NodeParam);

                    processFlowCanvas.AddNode(pastedNode, pasteLocation);
                    pastedNodes.Add(pastedNode);
                    pastedNodeMap[item.SourceNodeId] = pastedNode;
                    nodeIdMap[item.SourceNodeId] = pastedNode.ID;
                    nodeTextMap[GetNodeText(item.SourceNodeId, item.NodeName)] = GetNodeText(pastedNode.ID, pastedNode.NodeName);
                }

                foreach (NodeBase pastedNode in pastedNodes)
                {
                    if (pastedNode.ParamForm == null || pastedNode.ParamForm.Params == null)
                        continue;

                    RemapInternalNodeReferences(
                        pastedNode.ParamForm.Params,
                        nodeTextMap,
                        nodeIdMap,
                        isCrossProcessPaste ? snapshot.SourceNodeTexts : null,
                        unresolvedReferences);
                    pastedNode.ParamForm.SetParam2Form();
                }

                foreach (ConnectionCopySnapshot connection in snapshot.Connections)
                {
                    if (!pastedNodeMap.ContainsKey(connection.FromNodeId) ||
                        !pastedNodeMap.ContainsKey(connection.ToNodeId))
                    {
                        continue;
                    }

                    ProcessConnection pastedConnection = new ProcessConnection
                    {
                        FromNodeId = pastedNodeMap[connection.FromNodeId].ID,
                        ToNodeId = pastedNodeMap[connection.ToNodeId].ID,
                        FromAnchor = string.IsNullOrEmpty(connection.FromAnchor) ? "Right" : connection.FromAnchor,
                        ToAnchor = string.IsNullOrEmpty(connection.ToAnchor) ? "Left" : connection.ToAnchor,
                        Branch = connection.Branch
                    };
                    _process.NormalizeConnectionBranch(pastedConnection);
                    _process.HasCanvasGraph = true;
                    _process.Connections.Add(pastedConnection);
                    pastedConnections.Add(pastedConnection);
                }

                if (pastedConnections.Count > 0)
                    _process.NotifyConnectionsChanged();

                processFlowCanvas.SelectNodes(pastedNodes);
                processFlowCanvas.RegisterNodesCreated(pastedNodes, pastedConnections);
                _pasteCount++;
                UpdateNode();
                UpdateNodeCountText();
                ShowUnresolvedReferenceWarning(unresolvedReferences);
                return true;
            }
            catch
            {
                foreach (ProcessConnection connection in pastedConnections)
                    _process.Connections.Remove(connection);
                if (pastedConnections.Count > 0)
                    _process.NotifyConnectionsChanged();

                if (pastedNodes.Count > 0)
                {
                    foreach (NodeBase node in _process.Nodes)
                        node.Selected = false;

                    foreach (NodeBase pastedNode in pastedNodes)
                    {
                        if (pastedNode != null)
                            pastedNode.Selected = true;
                    }

                    foreach (NodeBase pastedNode in new List<NodeBase>(pastedNodes))
                    {
                        if (pastedNode != null && _process.Nodes.Contains(pastedNode))
                            pastedNode.DeleteFromProcess(true);
                    }

                    UpdateNode();
                    UpdateNodeCountText();
                }

                throw;
            }
        }

        /// <summary>
        /// 判断当前是否存在可粘贴的共享节点复制快照。
        /// </summary>
        /// <returns>存在有效复制快照时返回 true。</returns>
        private static bool HasCopiedNodeSnapshot()
        {
            return _sharedCopiedNodeSnapshot != null &&
                _sharedCopiedNodeSnapshot.Nodes != null &&
                _sharedCopiedNodeSnapshot.Nodes.Count > 0;
        }

        /// <summary>
        /// 显示跨流程粘贴时被清空的源流程外部引用提示。
        /// </summary>
        /// <param name="unresolvedReferences">被检测到的源流程外部引用。</param>
        private static void ShowUnresolvedReferenceWarning(List<string> unresolvedReferences)
        {
            List<string> uniqueReferences = GetUniqueReferences(unresolvedReferences);
            if (uniqueReferences.Count == 0)
                return;

            int displayCount = Math.Min(5, uniqueReferences.Count);
            string detail = string.Join("、", uniqueReferences.GetRange(0, displayCount).ToArray());
            if (uniqueReferences.Count > displayCount)
                detail += " 等";

            string message = $"跨流程粘贴时发现 {uniqueReferences.Count} 个源流程外部引用未一起复制，已清空：{detail}。请在目标流程中重新选择订阅节点。";
            LogHelper.AddLog(MsgLevel.Warn, message, true);
            MessageBoxTD.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// 获取去重后的引用名称列表。
        /// </summary>
        /// <param name="references">原始引用名称列表。</param>
        /// <returns>保持首次出现顺序的去重列表。</returns>
        private static List<string> GetUniqueReferences(List<string> references)
        {
            List<string> uniqueReferences = new List<string>();
            if (references == null)
                return uniqueReferences;

            HashSet<string> visitedReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string reference in references)
            {
                if (string.IsNullOrWhiteSpace(reference) || !visitedReferences.Add(reference))
                    continue;

                uniqueReferences.Add(reference);
            }

            return uniqueReferences;
        }

        private Point GetNextPasteOffset(NodeCopySnapshot snapshot)
        {
            Rectangle copiedBounds = GetCopiedNodeBounds(snapshot);
            Point targetLocation = processFlowCanvas.LastContextMenuCanvasPoint;
            int repeatedPasteOffset = 32 * (_pasteCount + 1);
            if (targetLocation == Point.Empty)
                targetLocation = copiedBounds.Location;

            Point requestedTopLeft = new Point(
                targetLocation.X + repeatedPasteOffset,
                targetLocation.Y + repeatedPasteOffset);
            if (processFlowCanvas.SnapToGridEnabled)
                requestedTopLeft = processFlowCanvas.GetInteractiveNodeLocation(requestedTopLeft, copiedBounds.Size);

            return new Point(
                requestedTopLeft.X - copiedBounds.X,
                requestedTopLeft.Y - copiedBounds.Y);
        }

        private static Rectangle GetCopiedNodeBounds(NodeCopySnapshot snapshot)
        {
            Rectangle bounds = new Rectangle(snapshot.Nodes[0].CanvasLocation, snapshot.Nodes[0].CanvasSize);
            for (int index = 1; index < snapshot.Nodes.Count; index++)
            {
                NodeCopyItem item = snapshot.Nodes[index];
                bounds = Rectangle.Union(bounds, new Rectangle(item.CanvasLocation, item.CanvasSize));
            }

            return bounds;
        }

        /// <summary>
        /// 生成粘贴节点名称，优先保留原始显示文本，重名时仅追加数字编号。
        /// </summary>
        /// <param name="sourceName">复制来源节点名称。</param>
        /// <returns>可用于目标流程的节点名称。</returns>
        private string GetUniquePastedNodeName(string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName)
                ? "节点"
                : sourceName.Trim();
            string candidate = baseName;
            int index = 2;
            while (NodeNameExists(candidate))
            {
                candidate = baseName + "_" + index;
                index++;
            }

            return candidate;
        }

        private bool NodeNameExists(string nodeName)
        {
            if (_process == null)
                return false;

            foreach (NodeBase node in _process.Nodes)
            {
                if (string.Equals(node.NodeName, nodeName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 生成参数订阅中持久化使用的节点文本。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <returns>节点编号和名称组合后的文本。</returns>
        private static string GetNodeText(int nodeId, string nodeName)
        {
            return $"{nodeId}.{nodeName}";
        }

        private static INodeParam CloneNodeParam(INodeParam sourceParam)
        {
            if (sourceParam == null)
                return null;

            string json = JsonConvert.SerializeObject(sourceParam);
            INodeParam clonedParam = JsonConvert.DeserializeObject(json, sourceParam.GetType()) as INodeParam;
            if (clonedParam == null)
                throw new InvalidOperationException("节点参数复制失败！");

            return clonedParam;
        }

        private static void RemapInternalNodeReferences(
            object target,
            Dictionary<string, string> nodeTextMap,
            Dictionary<int, int> nodeIdMap = null,
            Dictionary<int, string> sourceNodeTextMap = null,
            List<string> unresolvedReferences = null,
            int depth = 0)
        {
            if (target == null || depth > 8)
                return;

            Type type = target.GetType();
            if (type == typeof(string) ||
                type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(decimal))
            {
                return;
            }

            System.Collections.IList list = target as System.Collections.IList;
            if (list != null && !list.IsReadOnly)
            {
                for (int index = 0; index < list.Count; index++)
                {
                    string oldItemText = list[index] as string;
                    if (oldItemText != null)
                    {
                        list[index] = RemapNodeReferenceText(
                            oldItemText,
                            nodeTextMap,
                            sourceNodeTextMap,
                            unresolvedReferences);
                        continue;
                    }

                    RemapInternalNodeReferences(list[index], nodeTextMap, nodeIdMap, sourceNodeTextMap, unresolvedReferences, depth + 1);
                }

                return;
            }

            System.Collections.IEnumerable enumerable = target as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                foreach (object item in enumerable)
                    RemapInternalNodeReferences(item, nodeTextMap, nodeIdMap, sourceNodeTextMap, unresolvedReferences, depth + 1);

                return;
            }

            foreach (System.Reflection.PropertyInfo property in type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (!property.CanRead ||
                    !property.CanWrite ||
                    property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (property.PropertyType == typeof(int) &&
                    nodeIdMap != null &&
                    string.Equals(property.Name, "SourceNodeId", StringComparison.OrdinalIgnoreCase))
                {
                    int oldNodeId = (int)property.GetValue(target, null);
                    int newNodeId;
                    if (nodeIdMap.TryGetValue(oldNodeId, out newNodeId))
                    {
                        property.SetValue(target, newNodeId, null);
                    }
                    else if (sourceNodeTextMap != null && sourceNodeTextMap.ContainsKey(oldNodeId))
                    {
                        property.SetValue(target, 0, null);
                        AddUnresolvedReference(unresolvedReferences, sourceNodeTextMap[oldNodeId]);
                    }

                    continue;
                }

                if (property.PropertyType == typeof(string))
                {
                    string oldValue = property.GetValue(target, null) as string;
                    property.SetValue(
                        target,
                        RemapNodeReferenceText(oldValue, nodeTextMap, sourceNodeTextMap, unresolvedReferences),
                        null);
                    continue;
                }

                if (property.PropertyType.IsValueType)
                    continue;

                object value = property.GetValue(target, null);
                RemapInternalNodeReferences(value, nodeTextMap, nodeIdMap, sourceNodeTextMap, unresolvedReferences, depth + 1);
            }

            foreach (System.Reflection.FieldInfo field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (field.IsInitOnly)
                    continue;

                if (field.FieldType == typeof(int) &&
                    nodeIdMap != null &&
                    string.Equals(field.Name, "SourceNodeId", StringComparison.OrdinalIgnoreCase))
                {
                    int oldNodeId = (int)field.GetValue(target);
                    int newNodeId;
                    if (nodeIdMap.TryGetValue(oldNodeId, out newNodeId))
                    {
                        field.SetValue(target, newNodeId);
                    }
                    else if (sourceNodeTextMap != null && sourceNodeTextMap.ContainsKey(oldNodeId))
                    {
                        field.SetValue(target, 0);
                        AddUnresolvedReference(unresolvedReferences, sourceNodeTextMap[oldNodeId]);
                    }

                    continue;
                }

                if (field.FieldType == typeof(string))
                {
                    string oldValue = field.GetValue(target) as string;
                    field.SetValue(
                        target,
                        RemapNodeReferenceText(oldValue, nodeTextMap, sourceNodeTextMap, unresolvedReferences));
                    continue;
                }

                if (field.FieldType.IsValueType)
                    continue;

                object value = field.GetValue(target);
                RemapInternalNodeReferences(value, nodeTextMap, nodeIdMap, sourceNodeTextMap, unresolvedReferences, depth + 1);
            }
        }

        /// <summary>
        /// 重映射参数对象中的节点文本引用；跨流程外部引用会被清空并记录。
        /// </summary>
        /// <param name="oldValue">原始节点文本。</param>
        /// <param name="nodeTextMap">复制批次内的节点文本重映射表。</param>
        /// <param name="sourceNodeTextMap">源流程节点文本表。</param>
        /// <param name="unresolvedReferences">跨流程外部引用记录列表。</param>
        /// <returns>重映射后的节点文本。</returns>
        private static string RemapNodeReferenceText(
            string oldValue,
            Dictionary<string, string> nodeTextMap,
            Dictionary<int, string> sourceNodeTextMap,
            List<string> unresolvedReferences)
        {
            if (string.IsNullOrEmpty(oldValue))
                return oldValue;

            string newValue;
            if (nodeTextMap != null && nodeTextMap.TryGetValue(oldValue, out newValue))
                return newValue;

            if (IsSourceOnlyNodeText(oldValue, sourceNodeTextMap))
            {
                AddUnresolvedReference(unresolvedReferences, oldValue);
                return string.Empty;
            }

            return oldValue;
        }

        /// <summary>
        /// 判断字符串是否是源流程中的节点文本，并且未被复制批次重映射。
        /// </summary>
        /// <param name="value">待判断的字符串。</param>
        /// <param name="sourceNodeTextMap">源流程节点文本表。</param>
        /// <returns>命中源流程节点文本时返回 true。</returns>
        private static bool IsSourceOnlyNodeText(string value, Dictionary<int, string> sourceNodeTextMap)
        {
            if (string.IsNullOrWhiteSpace(value) || sourceNodeTextMap == null || sourceNodeTextMap.Count == 0)
                return false;

            foreach (string sourceNodeText in sourceNodeTextMap.Values)
            {
                if (string.Equals(value, sourceNodeText, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 记录跨流程粘贴时无法自动迁移的源流程节点引用。
        /// </summary>
        /// <param name="unresolvedReferences">待追加的引用列表。</param>
        /// <param name="reference">无法自动迁移的引用文本。</param>
        private static void AddUnresolvedReference(List<string> unresolvedReferences, string reference)
        {
            if (unresolvedReferences == null || string.IsNullOrWhiteSpace(reference))
                return;

            unresolvedReferences.Add(reference);
        }

        private sealed class NodeCopySnapshot
        {
            /// <summary>
            /// 复制快照编号，用于区分不同批次的复制内容。
            /// </summary>
            public int CopySequence;
            /// <summary>
            /// 源流程编号。
            /// </summary>
            public int SourceProcessId;
            /// <summary>
            /// 源流程名称。
            /// </summary>
            public string SourceProcessName;
            /// <summary>
            /// 源流程中所有节点的订阅文本，用于跨流程粘贴时识别外部引用。
            /// </summary>
            public Dictionary<int, string> SourceNodeTexts = new Dictionary<int, string>();
            /// <summary>
            /// 被复制的节点快照。
            /// </summary>
            public List<NodeCopyItem> Nodes = new List<NodeCopyItem>();
            /// <summary>
            /// 被复制节点之间的内部连线快照。
            /// </summary>
            public List<ConnectionCopySnapshot> Connections = new List<ConnectionCopySnapshot>();

            /// <summary>
            /// 判断目标流程是否就是复制来源流程。
            /// </summary>
            /// <param name="process">当前准备粘贴的目标流程。</param>
            /// <returns>目标流程与来源流程一致时返回 true。</returns>
            public bool IsSourceProcess(Process process)
            {
                return process != null && process.ID == SourceProcessId;
            }
        }

        private sealed class NodeCopyItem
        {
            /// <summary>
            /// 源节点编号。
            /// </summary>
            public int SourceNodeId;
            /// <summary>
            /// 源节点类型。
            /// </summary>
            public NodeType NodeType;
            /// <summary>
            /// 源节点名称。
            /// </summary>
            public string NodeName;
            /// <summary>
            /// 源节点启用状态。
            /// </summary>
            public bool Active;
            /// <summary>
            /// 源节点是否允许输出运行日志。
            /// </summary>
            public bool OutputLog = true;
            /// <summary>
            /// 源节点画布位置。
            /// </summary>
            public Point CanvasLocation;
            /// <summary>
            /// 源节点画布尺寸。
            /// </summary>
            public Size CanvasSize;
            /// <summary>
            /// 源节点备注。
            /// </summary>
            public string Notes;
            /// <summary>
            /// 源节点参数深拷贝。
            /// </summary>
            public INodeParam NodeParam;
        }

        private sealed class ConnectionCopySnapshot
        {
            /// <summary>
            /// 源连线起点节点编号。
            /// </summary>
            public int FromNodeId;
            /// <summary>
            /// 源连线终点节点编号。
            /// </summary>
            public int ToNodeId;
            /// <summary>
            /// 源连线起点锚点。
            /// </summary>
            public string FromAnchor;
            /// <summary>
            /// 源连线终点锚点。
            /// </summary>
            public string ToAnchor;
            /// <summary>
            /// 源连线分支语义。
            /// </summary>
            public ProcessConnectionBranch Branch;
        }
    }
}
