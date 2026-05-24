using Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageSplit;
using TDJS_Vision.Node._3_Detection.BatteryEar;
using TDJS_Vision.Node._3_Detection.BinaryAnalysis;
using TDJS_Vision.Node._3_Detection.ColorDiscern;
using TDJS_Vision.Node._3_Detection.FindCircle;
using TDJS_Vision.Node._3_Detection.FindLine;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._3_Detection.QRScan;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.CaliperCircle;
using TDJS_Vision.Node._4_Measurement.CaliperEllipse;
using TDJS_Vision.Node._4_Measurement.CaliperLine;
using TDJS_Vision.Node._4_Measurement.PositionCorrection;
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
using TDJS_Vision.Node._7_ResultProcessing.ResultSummarize;
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
        private NodeCopySnapshot _copiedNodeSnapshot;
        private int _pasteCount;
        private bool _isLoopRunRequested;
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
        /// <param name="processName"></param>
        public ProcessEditPanel(string processName, bool showInfo = true, ProcessConfig processConfig = null)
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
                CreateProcess(processConfig, showInfo);
                uiSwitchEnable.Active = processConfig.Enable;
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

        private void ProcessFlowCanvas_OverlayInvalidated(object sender, EventArgs e)
        {
            imagePreviewOverlay.Invalidate();
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
        private void CreateProcess(ProcessConfig processConfig, bool showInfo)
        {
            _process.HasCanvasGraph = processConfig.HasCanvasGraph;
            int nodeIndex = 0;
            foreach (var nodeInfo in processConfig.NodeInfos)
            {
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
                    MessageBoxTD.Show(ex.Message, LanguageManager.T("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if(showInfo)
                        LogHelper.AddLog(MsgLevel.Exception, LanguageManager.Format("ProcessNew.NodeLoadFailed", nodeInfo.ID, nodeInfo.NodeName, ex.Message), true);
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
                case NodeType.PositionCorrection:
                    node = new NodePositionCorrection(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageCrop:
                    node = new NodeImageCrop(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageShow:
                    node = new NodeImageShow(nodeId, nodeName, _process, nodeType);
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
                case NodeType.ImageSource:
                    node = new NodeImageSource(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.ImageSplit:
                    node = new NodeImageSplit(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.QRScan:
                    node = new NodeQRScan(nodeId, nodeName, _process, nodeType);
                    break;
                case NodeType.MatchTemplate:
                    node = new NodeMatchTemplate(nodeId, nodeName, _process, nodeType);
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
                case NodeType.DrawAIResult:
                    node = new NodeImageDraw(nodeId, nodeName, _process, nodeType);
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
            try
            {
                if (_isLoopRunRequested)
                    return;

                // 重置运行取消令牌
                Solution.Instance.ResetTokenSource();
                _process.IsHandRun = true;
                await _process.Run(false);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 点击循环运行流程
        /// </summary>
        private async void buttonLoop_Click(object sender, EventArgs e)
        {
            if (_isLoopRunRequested)
                return;

            _isLoopRunRequested = true;
            buttonRun.Enabled = false;
            buttonLoop.Enabled = false;
            buttonStop.Enabled = true;
            Solution.Instance.ResetTokenSource();

            try
            {
                if (_process.Nodes.Count == 0 || !_process.Enable)
                    return;

                while (!Solution.Instance.CancellationToken.IsCancellationRequested)
                {
                    _process.IsHandRun = true;
                    await _process.Run(false);

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
                _isLoopRunRequested = false;
                buttonRun.Enabled = true;
                buttonLoop.Enabled = true;
                buttonStop.Enabled = false;
            }
        }

        /// <summary>
        /// 点击停止流程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStop_Click(object sender, EventArgs e)
        {
            Solution.Instance.CancelToken();
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
            _process.Enable = value;
            if (value)
                LogHelper.AddLog(MsgLevel.Info, LanguageManager.Format("ProcessNew.ProcessEnabled", _process.ProcessName), true);
            else
                LogHelper.AddLog(MsgLevel.Info, LanguageManager.Format("ProcessNew.ProcessDisabled", _process.ProcessName), true);
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
            bool canPaste = _copiedNodeSnapshot != null;
            bool hasActiveNode = false;
            bool hasInactiveNode = false;

            foreach (NodeBase node in processFlowCanvas.SelectedNodes)
            {
                if (node.Active)
                    hasActiveNode = true;
                else
                    hasInactiveNode = true;
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
            重命名节点ToolStripMenuItem.Enabled = hasSingleNode;
            添加节点备注ToolStripMenuItem.Enabled = hasSingleNode;
            删除节点ToolStripMenuItem.Enabled = hasNode;
            删除连线ToolStripMenuItem.Enabled = hasConnection;
            e.Cancel = false;
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

            _copiedNodeSnapshot = new NodeCopySnapshot
            {
                Nodes = new List<NodeCopyItem>(),
                Connections = new List<ConnectionCopySnapshot>()
            };

            Dictionary<int, NodeBase> selectedNodeMap = new Dictionary<int, NodeBase>();
            foreach (NodeBase node in selectedNodes)
            {
                selectedNodeMap[node.ID] = node;
                _copiedNodeSnapshot.Nodes.Add(new NodeCopyItem
                {
                    SourceNodeId = node.ID,
                    NodeType = node.NodeType,
                    NodeName = node.NodeName,
                    Active = node.Active,
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

                _copiedNodeSnapshot.Connections.Add(new ConnectionCopySnapshot
                {
                    FromNodeId = connection.FromNodeId,
                    ToNodeId = connection.ToNodeId,
                    FromAnchor = connection.FromAnchor,
                    ToAnchor = connection.ToAnchor,
                    Branch = connection.Branch
                });
            }

            _pasteCount = 0;
            return true;
        }

        private bool PasteCopiedNode()
        {
            if (_copiedNodeSnapshot == null ||
                _copiedNodeSnapshot.Nodes == null ||
                _copiedNodeSnapshot.Nodes.Count == 0)
                return false;

            List<NodeBase> pastedNodes = new List<NodeBase>();
            List<ProcessConnection> pastedConnections = new List<ProcessConnection>();
            try
            {
                Point pasteOffset = GetNextPasteOffset(_copiedNodeSnapshot);
                Dictionary<int, NodeBase> pastedNodeMap = new Dictionary<int, NodeBase>();
                Dictionary<string, string> nodeTextMap = new Dictionary<string, string>();
                Dictionary<int, int> nodeIdMap = new Dictionary<int, int>();

                foreach (NodeCopyItem item in _copiedNodeSnapshot.Nodes)
                {
                    Point pasteLocation = new Point(
                        item.CanvasLocation.X + pasteOffset.X,
                        item.CanvasLocation.Y + pasteOffset.Y);
                    string nodeName = GetUniquePastedNodeName(item.NodeName);
                    NodeBase pastedNode = CreateNode(item.NodeType, nodeName, -1, pasteLocation, false, false);
                    pastedNode.CanvasSize = item.CanvasSize;
                    pastedNode.Active = item.Active;
                    pastedNode.SetNotes(item.Notes);

                    if (item.NodeParam != null && pastedNode.ParamForm != null)
                        pastedNode.ParamForm.Params = CloneNodeParam(item.NodeParam);

                    processFlowCanvas.AddNode(pastedNode, pasteLocation);
                    pastedNodes.Add(pastedNode);
                    pastedNodeMap[item.SourceNodeId] = pastedNode;
                    nodeIdMap[item.SourceNodeId] = pastedNode.ID;
                    nodeTextMap[$"{item.SourceNodeId}.{item.NodeName}"] = $"{pastedNode.ID}.{pastedNode.NodeName}";
                }

                foreach (NodeBase pastedNode in pastedNodes)
                {
                    if (pastedNode.ParamForm == null || pastedNode.ParamForm.Params == null)
                        continue;

                    RemapInternalNodeReferences(pastedNode.ParamForm.Params, nodeTextMap, nodeIdMap);
                    pastedNode.ParamForm.SetParam2Form();
                }

                foreach (ConnectionCopySnapshot connection in _copiedNodeSnapshot.Connections)
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
                            pastedNode.DeleteFromProcess(false);
                    }

                    UpdateNode();
                    UpdateNodeCountText();
                }

                throw;
            }
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

        private string GetUniquePastedNodeName(string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName)
                ? "节点_复制"
                : sourceName + "_复制";
            string candidate = baseName;
            int index = 2;
            while (NodeNameExists(candidate))
            {
                candidate = baseName + index;
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
            int depth = 0)
        {
            if (target == null || nodeTextMap == null || nodeTextMap.Count == 0 || depth > 5)
                return;

            Type type = target.GetType();
            if (type == typeof(string) ||
                type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(decimal))
            {
                return;
            }

            System.Collections.IEnumerable enumerable = target as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                foreach (object item in enumerable)
                    RemapInternalNodeReferences(item, nodeTextMap, nodeIdMap, depth + 1);

                return;
            }

            foreach (System.Reflection.PropertyInfo property in type.GetProperties())
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
                        property.SetValue(target, newNodeId, null);

                    continue;
                }

                if (property.PropertyType == typeof(string))
                {
                    string oldValue = property.GetValue(target, null) as string;
                    string newValue;
                    if (!string.IsNullOrEmpty(oldValue) &&
                        nodeTextMap.TryGetValue(oldValue, out newValue))
                    {
                        property.SetValue(target, newValue, null);
                    }

                    continue;
                }

                if (property.PropertyType.IsValueType)
                    continue;

                object value = property.GetValue(target, null);
                RemapInternalNodeReferences(value, nodeTextMap, nodeIdMap, depth + 1);
            }
        }

        private sealed class NodeCopySnapshot
        {
            public List<NodeCopyItem> Nodes = new List<NodeCopyItem>();
            public List<ConnectionCopySnapshot> Connections = new List<ConnectionCopySnapshot>();
        }

        private sealed class NodeCopyItem
        {
            public int SourceNodeId;
            public NodeType NodeType;
            public string NodeName;
            public bool Active;
            public Point CanvasLocation;
            public Size CanvasSize;
            public string Notes;
            public INodeParam NodeParam;
        }

        private sealed class ConnectionCopySnapshot
        {
            public int FromNodeId;
            public int ToNodeId;
            public string FromAnchor;
            public string ToAnchor;
            public ProcessConnectionBranch Branch;
        }
    }
}
