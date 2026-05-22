using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        /// <summary>
        /// 流程编辑面板构造函数
        /// </summary>
        /// <param name="processName"></param>
        public ProcessEditPanel(string processName, bool showInfo = true, ProcessConfig processConfig = null)
        {
            InitializeComponent();
            BindLanguage();
            _process = new Process(processName);
            processFlowCanvas.BindProcess(_process);
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
            Disposed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
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
            buttonStop.Text = string.Empty;
            uiSwitchEnable.ActiveText = LanguageManager.T("Common.Enabled");
            uiSwitchEnable.InActiveText = LanguageManager.T("Common.Disabled");
            UpdateNodeCountText();
            UpdateRunTimeText();
        }

        private void UpdateNodeCountText()
        {
            label1.Text = LanguageManager.Format("ProcessNew.NodeCount", _stack.Count);
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
                        buttonRun.Enabled = !e.IsRunning;
                        uiSwitchEnable.Enabled = !e.IsRunning;
                        uiLedBulb1.Color = e.IsRunning ? Color.DarkGray : e.IsSuccess ? Color.LawnGreen : Color.Red;
                    }));
                }
                else
                {
                    UpdateRunTimeText();
                    buttonRun.Enabled = !e.IsRunning;
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
                    nodeBase = CreateNode(nodeInfo.NodeType, nodeInfo.NodeName, nodeInfo.ID, canvasLocation);
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
        private NodeBase CreateNode(NodeType nodeType, string nodeName, int id = -1, Point? canvasLocation = null)
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
            node.CanvasLocation = location;
            foreach (NodeBase processNode in _process.Nodes)
                processNode.Selected = false;
            node.Selected = true;
            NodeBase.NodeDeletedEvent += NewNode_NodeDeletedEvent;
            _stack.Push(node);
            Solution.Instance.Nodes.Add(node);
            _process.AddNode(node);
            processFlowCanvas.AddNode(node, location);
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
                    FromAnchor = "Right",
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
                // 重置运行取消令牌
                Solution.Instance.ResetTokenSource();
                _process.IsHandRun = true;
                await _process.Run(false);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 点击停止流程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStop_Click(object sender, EventArgs e)
        {
            Solution.Instance.CancelToken();
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
            bool hasNode = selectedNode != null;
            bool hasConnection = processFlowCanvas.SelectedConnection != null;

            打开节点参数ToolStripMenuItem.Enabled = hasNode && selectedNode.Active;
            设为起始节点ToolStripMenuItem.Enabled = hasNode && !selectedNode.IsStartNode;
            启用节点ToolStripMenuItem.Enabled = hasNode && !selectedNode.Active;
            禁用节点ToolStripMenuItem.Enabled = hasNode && selectedNode.Active;
            重命名节点ToolStripMenuItem.Enabled = hasNode;
            添加节点备注ToolStripMenuItem.Enabled = hasNode;
            删除节点ToolStripMenuItem.Enabled = hasNode;
            删除连线ToolStripMenuItem.Enabled = hasConnection;
            e.Cancel = !hasNode && !hasConnection;
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
    }
}
