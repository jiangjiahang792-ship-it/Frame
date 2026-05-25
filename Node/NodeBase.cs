using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node
{
    public partial class NodeBase : UserControl
    {
        #region 节点界面的操作

        private int _id = 0;
        private bool _active = true;
        private bool _selected = false;
        private string _nodeName;
        private string _notes = "无备注"; // 节点备注
        private NodeStatus _runtimeStatus = NodeStatus.Unexecuted;
        private string _runtimeTimeText = "*";
        private FrmNodeRename _frmNodeRename;
        /// <summary>
        /// 节点所属流程
        /// </summary>
        public Process Process; 

        /// <summary>
        /// 节点的类别
        /// </summary>
        public NodeType NodeType;

        /// <summary>
        /// 节点改名后要刷新节点订阅控件的下拉框节点名称
        /// </summary>
        public static EventHandler<RenameResult> RefreshNodeSubControl;

        /// <summary>
        /// 节点运行状态变化后通知自由画布刷新对应节点。
        /// </summary>
        public static event EventHandler<NodeBase> NodeStatusChanged;

        /// <summary>
        /// 因为是控件类，提供无参构造函数让设计器可以显示出来
        /// </summary>
        public NodeBase() 
        {
            InitializeComponent();
            启用ToolStripMenuItem.Enabled = false;
        }

        /// <summary>
        /// 实际只使用这个有参构造函数创建控件
        /// </summary>
        /// <param name="paramForm"></param>
        public NodeBase(int nodeId, string nodeName, Process process, NodeType nodeType)
        {
            InitializeComponent();
            启用ToolStripMenuItem.Enabled = false;
            _id = nodeId;
            _nodeName = nodeName;
            label1.Text = $"{ID}.{_nodeName}";
            Process = process;
            _frmNodeRename = new FrmNodeRename(this);
            FrmNodeRename.RenameChangeEvent += RenameChangeEvent;
            NodeType = nodeType;
            toolTip1.SetToolTip(this.label3, _notes);
        }

        private void RenameChangeEvent(object sender, RenameResult e)
        {
            // 只重命名指定ID的节点
            if(e.NodeId == _id)
            {
                _nodeName = e.NodeNameNew;
                label1.Text = ID + "." + e.NodeNameNew;
                RefreshNodeSubControl?.Invoke(this, e);

                // AI检测项也要重命名
                if (TDAI.DetectItemMap.ContainsKey(e.NodeNameOld))
                {
                    var value = TDAI.DetectItemMap[e.NodeNameOld];     // 1. 获取旧值
                    TDAI.DetectItemMap.Remove(e.NodeNameOld);          // 2. 删除旧键
                    TDAI.DetectItemMap[e.NodeNameNew] = value;         // 3. 添加新键（自动覆盖）
                }
            }
        }

        /// <summary>
        /// 节点参数设置界面
        /// </summary>
        public INodeParamForm ParamForm { get; set; }
        /// <summary>
        /// 节点运行结果
        /// </summary>
        public INodeResult Result { get; protected set; }

        /// <summary>
        /// 检查节点信号源Token是否取消,如果取消会抛出异常，停止运行流程
        /// </summary>
        public virtual Task CheckTokenCancel(CancellationToken token) 
        {
            try
            {
                // 检查取消请求
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException ex)
            {
                throw ex;
            }
            return Task.CompletedTask;
        }

        public virtual Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            return Task.FromResult(new NodeReturn());
        }

        /// <summary>
        /// 节点id
        /// </summary>
        public int ID { get { return _id; } private set { _id = value; } }

        /// <summary>
        /// 节点是否启用
        /// </summary>
        public bool Active { get { return _active; } set => SetActive(value); }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool Selected { get { return _selected; } set => SetSelected(value); }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get => _nodeName; set => _nodeName = value; }

        /// <summary>
        /// 节点在流程画布中的位置。
        /// </summary>
        public Point CanvasLocation { get; set; } = new Point(40, 40);

        /// <summary>
        /// 节点在流程画布中的显示尺寸。
        /// </summary>
        public Size CanvasSize { get; set; } = new Size(240, 64);

        /// <summary>
        /// 流程图执行模式下的起始节点标记。
        /// </summary>
        public bool IsStartNode { get; set; }

        /// <summary>
        /// 当前运行状态，供自由画布绘制节点状态使用。
        /// </summary>
        public NodeStatus RuntimeStatus { get { return _runtimeStatus; } }

        /// <summary>
        /// 当前运行耗时文本，供自由画布绘制节点状态使用。
        /// </summary>
        public string RuntimeTimeText { get { return _runtimeTimeText; } }

        /// <summary>
        /// 当前节点最近一次产生可订阅结果所属的流程运行批次。
        /// </summary>
        public int LastSuccessfulRunId { get; private set; }

        public void BeginProcessRun(int runId)
        {
            if (LastSuccessfulRunId == runId)
                LastSuccessfulRunId = 0;

            SetStatus(NodeStatus.Unexecuted, "*");
        }

        public bool HasSuccessfulResultForRun(int runId)
        {
            return runId > 0 &&
                LastSuccessfulRunId == runId;
        }

        public void ApplyResultOutcomeStatus()
        {
            if (_runtimeStatus != NodeStatus.Successful || Result == null)
                return;

            if (ResultIndicatesFailure(Result))
                SetStatus(NodeStatus.Failed, _runtimeTimeText);
        }

        private static bool ResultIndicatesFailure(INodeResult result)
        {
            if (result == null)
                return false;

            bool ok;
            if (TryReadPublicBoolMember(result, "IsOk", out ok) && !ok)
                return true;

            if (TryReadPublicBoolMember(result, "IsAllOk", out ok) && !ok)
                return true;

            Type resultType = result.GetType();
            PropertyInfo[] properties = resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0 ||
                    !IsRunOutcomeProperty(property) ||
                    !typeof(AlgorithmResult).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                AlgorithmResult algorithmResult = property.GetValue(result, null) as AlgorithmResult;
                if (AlgorithmResultIndicatesFailure(algorithmResult))
                    return true;
            }

            return false;
        }

        private static bool IsRunOutcomeProperty(PropertyInfo property)
        {
            return property != null &&
                (string.Equals(property.Name, "Result", StringComparison.Ordinal) ||
                 string.Equals(property.Name, "AlgorithmResult", StringComparison.Ordinal) ||
                 string.Equals(property.Name, "SummaryResult", StringComparison.Ordinal));
        }

        private static bool AlgorithmResultIndicatesFailure(AlgorithmResult result)
        {
            if (result == null)
                return false;

            bool isAllOk;
            if (TryReadPublicBoolMember(result, "IsAllOk", out isAllOk))
                return !isAllOk;

            return !result.IsAllOk;
        }

        private static bool TryReadPublicBoolMember(object source, string memberName, out bool value)
        {
            value = true;
            if (source == null || string.IsNullOrEmpty(memberName))
                return false;

            Type type = source.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(source);
                return true;
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                if (!string.Equals(property.Name, memberName, StringComparison.Ordinal) ||
                    property.PropertyType != typeof(bool) ||
                    !property.CanRead ||
                    property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                value = (bool)property.GetValue(source, null);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 删除节点事件
        /// </summary>
        public static event EventHandler<NodeBase> NodeDeletedEvent;

        /// <summary>
        /// 禁用节点事件
        /// </summary>
        public static event EventHandler<bool> NodeDisableEvent;

        /// <summary>
        /// 设置备注文本
        /// </summary>
        /// <param name="text"></param>
        public void SetNotes(string text)
        {
            _notes = text;
            toolTip1.SetToolTip(this.label3, _notes);
        }

        public string Notes
        {
            get { return _notes; }
        }

        /// <summary>
        /// 设置节点状态,主要颜色，中心颜色，是否闪烁
        /// </summary>
        /// <param name="color"></param>
        /// <param name=""></param>
        public void SetStatus(NodeStatus status, string time)
        {
            if (this.InvokeRequired == false)
            {
                UpdateNodeUI(status, time);
            }
            else
            {
                this.Invoke((MethodInvoker)delegate {
                    UpdateNodeUI(status, time);
                });
            }
        }

        private void UpdateNodeUI(NodeStatus status, string time)
        {
            string displayTime = string.IsNullOrEmpty(time) ? "*" : time;
            NodeStatus displayStatus = status;
            if (status == NodeStatus.Unexecuted &&
                displayTime == "*" &&
                Process != null &&
                Process.IsRuning)
            {
                displayStatus = NodeStatus.Running;
            }

            _runtimeStatus = displayStatus;
            _runtimeTimeText = displayTime;

            label2.Text = displayStatus == NodeStatus.Running ? "运行中" : $"{displayTime} ms";
            switch (displayStatus)
            {
                case NodeStatus.Unexecuted:
                    uiLight1.State = Sunny.UI.UILightState.Off;
                    label2.ForeColor = uiLight1.OffColor;
                    label2.Text = $"* ms";
                    break;
                case NodeStatus.Running:
                    uiLight1.OnColor = Color.Goldenrod;
                    uiLight1.OnCenterColor = Color.Gold;
                    uiLight1.State = Sunny.UI.UILightState.Blink;
                    label2.ForeColor = Color.Goldenrod;
                    break;
                case NodeStatus.Successful:
                    uiLight1.OnColor = Color.Green;
                    uiLight1.OnCenterColor = Color.Lime;
                    uiLight1.State = Sunny.UI.UILightState.On;
                    label2.ForeColor = uiLight1.OnCenterColor;
                    break;
                case NodeStatus.Failed:
                    uiLight1.OnColor = Color.DarkRed;
                    uiLight1.OnCenterColor = Color.Red;
                    uiLight1.State = Sunny.UI.UILightState.Blink;
                    label2.ForeColor = uiLight1.OnCenterColor;
                    break;
                default:
                    break;
            }

            NodeStatusChanged?.Invoke(this, this);
        }

        /// <summary>
        /// 节点运行结果基本状态设置
        /// </summary>
        /// <param name="stopwatch"></param>
        /// <param name="status"></param>
        public int SetRunResult(DateTime startTime, NodeStatus status)
        {
            int elapsedMi11iseconds = (int)(DateTime.Now - startTime).TotalMilliseconds;
            if (Process != null && Process.CurrentRunId > 0)
            {
                if (status == NodeStatus.Successful)
                    LastSuccessfulRunId = Process.CurrentRunId;
                else if (LastSuccessfulRunId == Process.CurrentRunId)
                    LastSuccessfulRunId = 0;
            }

            SetStatus(status, elapsedMi11iseconds.ToString());
            return elapsedMi11iseconds;
        }

        /// <summary>
        /// 删除时触发删除事件，参数为待删除的节点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 删除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            #region 测试节点删除前代码

            //string str0 = "";
            //foreach (var node in Solution.Instance.Nodes)
            //{
            //    str0 += node.NodeName + "\n";
            //}
            //MessageBoxTD.Show("删除前方案节点：" + str0);

            //string str1 = "";
            //foreach (var node in Process.Nodes)
            //{
            //    str1 += node.NodeName + "\n";
            //}
            //MessageBoxTD.Show("删除前流程节点：" + str1);

            #endregion

            DeleteFromProcess();

            #region 测试节点删除后代码

            //string str2 = "";
            //foreach (var node in Solution.Instance.Nodes)
            //{
            //    str2 += node.NodeName + "\n";
            //}
            //MessageBoxTD.Show("删除后方案节点：" + str2);

            //string str3 = "";
            //foreach (var node in Process.Nodes)
            //{
            //    str3 += node.NodeName + "\n";
            //}
            //MessageBoxTD.Show("删除后流程节点：" + str3);

            #endregion
        }

        /// <summary>
        /// 删除当前选中的节点，并通知流程编辑器清理关联状态。
        /// </summary>
        public bool DeleteFromProcess(bool releaseResources = true)
        {
            if (!Selected)
                return false;

            Solution.Instance.Nodes.Remove(this);
            Process.Nodes.Remove(this);
            NodeDeletedEvent?.Invoke(this, this);

            if (!releaseResources)
                return true;

            // 移除AI节点需要释放AI句柄
            if (NodeType == NodeType.AITD && ParamForm.Params is NodeParamTDAI param)
                ModelHandleManager.Destroy(param.Yolo8);

            // 移除的节点如果包含了加载的检测项也需要从静态全局检测项中移除
            TDAI.DetectItemMap.Remove($"{this.ID}.{this.NodeName}");
            return true;
        }

        /// <summary>
        /// 设置节点为启用状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 启用ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EnableNode();
        }

        /// <summary>
        /// 设置节点为禁用状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 禁用ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisableNode();
        }

        public void EnableNode()
        {
            SetActive(true);
        }

        public void DisableNode()
        {
            SetActive(false);
            NodeDisableEvent?.Invoke(this, false);
        }

        /// <summary>
        /// 设置启用/禁用的呈现样式
        /// </summary>
        /// <param name="active"></param>
        private void SetActive(bool active)
        {
            _active = active;
            if (_active)
            {
                label1.BackColor = SystemColors.ActiveCaption;
                tableLayoutPanel1.BackColor = SystemColors.ActiveCaption;
                禁用ToolStripMenuItem.Enabled = true;
                启用ToolStripMenuItem.Enabled = false;
            }
            else
            {
                label1.BackColor = SystemColors.AppWorkspace;
                tableLayoutPanel1.BackColor = SystemColors.AppWorkspace;
                禁用ToolStripMenuItem.Enabled = false;
                启用ToolStripMenuItem.Enabled = true;
            }
        }

        /// <summary>
        /// 设置选中状态以及样式
        /// </summary>
        /// <param name="selected"></param>
        private void SetSelected(bool selected)
        {
            // 设置当前实例为选中
            _selected = selected;
            if (_active)
            {
                if (_selected)
                {
                    label1.BackColor = Color.CornflowerBlue;
                    tableLayoutPanel1.BackColor = Color.CornflowerBlue;
                }
                else
                {
                    label1.BackColor = SystemColors.ActiveCaption;
                    tableLayoutPanel1.BackColor = SystemColors.ActiveCaption;
                }
            }
            else
            {
                if (_selected)
                {
                    label1.BackColor = SystemColors.ControlDarkDark;
                    tableLayoutPanel1.BackColor = SystemColors.ControlDarkDark;
                }
                else
                {
                    label1.BackColor = SystemColors.AppWorkspace;
                    tableLayoutPanel1.BackColor = SystemColors.AppWorkspace;
                }
            }
        }

        /// <summary>
        /// 设置节点选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label1_MouseClick(object sender, MouseEventArgs e)
        {
            //清空全部选中状态
            foreach (var node in Solution.Instance.Nodes)
            {
                node.Selected = false;
            }
            SetSelected(!_selected);
        }

        /// <summary>
        /// 鼠标双击，打开设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (Active && ParamForm is Form form)
                    form.ShowDialog();
            }
            catch (Exception ex)
            {
                
            }
        }

        #endregion 定义节点界面操作-结束

        private void 重命名ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRenameDialog();
        }

        private void 添加备注ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowNotesDialog();
        }

        public void ShowRenameDialog()
        {
            if (_frmNodeRename != null)
                _frmNodeRename.ShowDialog();
        }

        public void ShowNotesDialog()
        {
            FormAddNotes formAddNotes = new FormAddNotes(this);
            formAddNotes.ShowDialog();
        }
    }


}
