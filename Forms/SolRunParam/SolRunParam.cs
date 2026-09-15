using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using Newtonsoft.Json;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using static OpenCvSharp.ML.DTrees;

namespace TDJS_Vision.Forms.SolRunParam
{
    public partial class SolRunParamControl : UserControl
    {
        Process process;//显示哪个流程的参数
        ICamera _camera = null;
        TriggerSource triggerSrc;
        NodeTDAI _nodeTDAI;//AI节点
        NodeTDAI _targetAiNode;//外部指定的AI节点
        NodeImageSource _nodeSource;//图像源节点
        NodeImageCrop _nodeCrop;//图像裁剪节点
        /// <summary>
        /// 当前流程中可写入运行参数表的多条件节点。
        /// </summary>
        List<NodeMultiCondition> _nodeMultiConditions = new List<NodeMultiCondition>();
        /// <summary>
        /// 当前运行参数表的显示过滤范围，图像窗口手动调参时用于跟随ROI绘制来源。
        /// </summary>
        RunParamDisplayFilter _runParamDisplayFilter;
        string _windowsName; // 图像显示窗口名
        string _currentDetectItemName;//当前运行参数表显示的检测项名称
        /// <summary>
        /// 当前运行参数表显示的AI检测项上下限。
        /// </summary>
        List<DetectItemInfo> _currentDetectItems = new List<DetectItemInfo>();
        EventHandler<Bitmap> preEventHandler = null;//前一个活动的相机图像发布事件处理器
        public static event EventHandler<ImageShowPamra> ImageShowChanged; // 发布图像到窗口显示
        public static event EventHandler RefreshParamView; // 通知节点参数界面刷新事件
        public SolRunParamControl()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            BindLanguage();
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            Disposed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
            Disposed += SolRunParamControl_Disposed;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(label1, "SolRunParam.Exposure");
            LanguageManager.Bind(label2, "SolRunParam.Gain");
            LanguageManager.Bind(label3, "SolRunParam.ScoreThreshold");
            LanguageManager.Bind(label4, "SolRunParam.NMSScore");
            LanguageManager.Bind(label5, "SolRunParam.ContinuousGrab");
            LanguageManager.Bind(label6, "SolRunParam.OneClickLearning");
            LanguageManager.Bind(buttonOnce, "SolRunParam.GrabOnce");
            LanguageManager.Bind(buttonSingleImg, "SolRunParam.SingleImageTest");
            LanguageManager.Bind(buttonCatalogueImg, "SolRunParam.MultiImageTest");
            LanguageManager.Bind(buttonSaveAllParam, "SolRunParam.SaveSettings");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
            uiSwitchROI.ActiveText = LanguageManager.T("SolRunParam.ROIEnabled");
            uiSwitchROI.InActiveText = LanguageManager.T("SolRunParam.ROIDisabled");
        }

        /// <summary>
        /// 使用流程初始化
        /// </summary>
        /// <param name="process"></param>
        public void Init(Process process)
        {
            Init(process, null, null);
        }

        /// <summary>
        /// 使用流程和指定AI节点初始化。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="targetAiNode"></param>
        public void Init(Process process, NodeTDAI targetAiNode)
        {
            Init(process, targetAiNode, null);
        }

        /// <summary>
        /// 使用流程、指定AI节点和运行参数显示范围初始化。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="targetAiNode">外部指定的AI检测节点。</param>
        /// <param name="runParamDisplayFilter">运行参数显示过滤范围。</param>
        public void Init(Process process, NodeTDAI targetAiNode, RunParamDisplayFilter runParamDisplayFilter)
        {
            try
            {
                Stopwatch initStopwatch = Stopwatch.StartNew();
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【手动调参诊断】SolRunParamControl.Init开始，流程={(process == null ? "空" : process.ProcessName)}，节点数={(process == null || process.Nodes == null ? 0 : process.Nodes.Count)}，目标AI={(targetAiNode == null ? "空" : targetAiNode.ID + "." + targetAiNode.NodeName)}，过滤范围={(runParamDisplayFilter == null ? "空" : runParamDisplayFilter.ToLogText())}。",
                    true);
                this.process = process;
                _targetAiNode = targetAiNode;
                _runParamDisplayFilter = runParamDisplayFilter;
                _camera = null;
                _nodeSource = null;
                _nodeCrop = null;
                SetCurrentAiNode(null);
                SetMultiConditionNodes(null);
                _currentDetectItemName = null;
                _currentDetectItems = new List<DetectItemInfo>();
                _windowsName = null;
                SetEnable(false);
                List<NodeMultiCondition> multiConditionNodes = new List<NodeMultiCondition>();
                foreach (var node in process.Nodes)
                {
                    if (node is NodeMultiCondition nodeMultiCondition)
                        multiConditionNodes.Add(nodeMultiCondition);

                    //获取相机触发模式和曝光
                    if (node is NodeImageSource nodeSource && node.ParamForm.Params
                        is NodeParamImageSoucre nodeParam)
                    {
                        _nodeSource = nodeSource;
                        if (nodeParam != null)
                        {
                            Stopwatch cameraStopwatch = Stopwatch.StartNew();
                            LogHelper.AddLog(
                                MsgLevel.Info,
                                $"【手动调参诊断】开始加载图像源方案参数，节点={nodeSource.ID}.{nodeSource.NodeName}。",
                                true);

                            // 运行调参页显示方案值，不能用相机当前值反向覆盖方案。
                            _camera = Solution.Instance.ResolveImageSourceCamera(nodeParam);
                            triggerSrc = nodeParam.TriggerSource == TriggerSource.Auto
                                ? TriggerSource.SOFT
                                : nodeParam.TriggerSource;
                            textBoxExposureTime.Text = nodeParam.ExposureTime.ToString();
                            textBoxGain.Text = nodeParam.Gain.ToString();
                            uiSwitchContinue.Active = false;
                            textBoxExposureTime.Enabled = true;
                            textBoxGain.Enabled = true;
                            uiSwitchContinue.Enabled = _camera != null && _camera.IsOpen;
                            buttonOnce.Enabled = _camera != null && _camera.IsOpen;

                            LogHelper.AddLog(
                                MsgLevel.Info,
                                $"【手动调参诊断】图像源方案参数加载完成，节点={nodeSource.ID}.{nodeSource.NodeName}，曝光={nodeParam.ExposureTime}us，增益={nodeParam.Gain}，耗时={cameraStopwatch.ElapsedMilliseconds}ms。",
                                true);
                        }
                    }
                    //获取图像裁剪节点
                    if (node is NodeImageCrop nodeCrop && node.ParamForm.Params
                        is NodeParamImageCrop nodeCropParam)
                    {
                        _nodeCrop = nodeCrop;//临时保存图像裁剪节点
                        if (nodeCropParam != null)
                        {
                            try
                            {
                                //设置参数
                                uiSwitchROI.Active = nodeCropParam.RoiEnable;
                                //设置控件可用性
                                uiSwitchROI.Enabled = true;
                            }
                            catch (Exception) { }
                        }
                    }
                    //获取AI参数
                    if (node is NodeTDAI nodeTDAI &&
                        node.ParamForm.Params is NodeParamTDAI paramTDAI)
                    {
                        if (!IsSourceNodeAllowedByRunParamFilter(nodeTDAI.ID))
                            continue;

                        if (_targetAiNode != null && nodeTDAI.ID != _targetAiNode.ID)
                            continue;

                        SetCurrentAiNode(nodeTDAI);//临时保存AI节点
                        if (paramTDAI != null)
                        {
                            //设置参数
                            textBoxScoreThreshold.Text = paramTDAI.AIInputInfo.ScoreThreshold.ToString();
                            textBoxScoreNMS.Text = paramTDAI.AIInputInfo.ScoreNMS.ToString();
                            uiSwitch_Learning.Active = paramTDAI.IsAutoStudy;
                            //设置控件可用性
                            textBoxScoreThreshold.Enabled = true;
                            textBoxScoreNMS.Enabled = true;
                            uiSwitch_Learning.Enabled = true;
                            LogHelper.AddLog(
                                MsgLevel.Info,
                                $"【手动调参诊断】开始加载AI检测项运行参数，AI节点={nodeTDAI.ID}.{nodeTDAI.NodeName}，检测项={paramTDAI.CurDetectItemName}。",
                                true);
                            LoadDetectItemsByName(paramTDAI.CurDetectItemName);
                        }
                    }
                    if (node is NodeImageShow nodeShow)
                    {
                        if (nodeShow.ParamForm.Params is NodeParamImageShow showParam)
                        {
                            _windowsName = FrmSingleImage.NormalizeWindowKey(showParam.WindowName);
                            showParam.WindowName = _windowsName;
                        }
                    }
                }

                SetMultiConditionNodes(multiConditionNodes);
                RefreshRunParamGrid();
                // 本地图像测试只依赖有效的图像源节点，无需相机连接成功。
                buttonSingleImg.Enabled = _nodeSource != null;
                buttonCatalogueImg.Enabled = _nodeSource != null;
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【手动调参诊断】SolRunParamControl.Init完成，流程={process.ProcessName}，多条件节点数={multiConditionNodes.Count}，耗时={initStopwatch.ElapsedMilliseconds}ms。",
                    true);
            }
            catch (Exception e)
            {
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"【手动调参诊断】SolRunParamControl.Init异常，流程={(process == null ? "空" : process.ProcessName)}，原因={e.Message}",
                    true);
                throw new Exception(e.Message);
            }
        }

        /// <summary>
        /// 加载检测内容
        /// </summary>
        /// <param name="info"></param>
        private void LoadDetectItems(List<DetectItemInfo> infos)
        {
            _currentDetectItems = CloneDetectItems(infos);
            RefreshRunParamGrid();
        }

        /// <summary>
        /// 根据检测项名称加载运行参数表中的公差上下限。
        /// </summary>
        /// <param name="detectItemName">检测项配置名称。</param>
        /// <returns>是否成功加载检测项配置。</returns>
        private bool LoadDetectItemsByName(string detectItemName)
        {
            if (string.IsNullOrWhiteSpace(detectItemName))
                return false;

            detectItemName = detectItemName.Trim();
            if (Solution.Instance.DetectItemDic == null ||
                !Solution.Instance.DetectItemDic.TryGetValue(detectItemName, out List<DetectItemInfo> detectItems))
            {
                return false;
            }

            _currentDetectItemName = detectItemName;
            LoadDetectItems(detectItems);
            return true;
        }

        /// <summary>
        /// 设置当前AI节点并维护检测项切换事件订阅。
        /// </summary>
        /// <param name="nodeTDAI">当前运行参数界面对应的AI节点。</param>
        private void SetCurrentAiNode(NodeTDAI nodeTDAI)
        {
            if (ReferenceEquals(_nodeTDAI, nodeTDAI))
                return;

            if (_nodeTDAI != null)
                _nodeTDAI.DetectItemConfigChanged -= NodeTDAI_DetectItemConfigChanged;

            _nodeTDAI = nodeTDAI;

            if (_nodeTDAI != null)
                _nodeTDAI.DetectItemConfigChanged += NodeTDAI_DetectItemConfigChanged;
        }

        /// <summary>
        /// 释放运行参数界面时解除AI节点事件订阅。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void SolRunParamControl_Disposed(object sender, EventArgs e)
        {
            SetCurrentAiNode(null);
            SetMultiConditionNodes(null);
        }

        /// <summary>
        /// 设置当前流程中的多条件节点并维护运行参数刷新事件订阅。
        /// </summary>
        /// <param name="nodes">当前流程中的多条件节点集合。</param>
        private void SetMultiConditionNodes(IEnumerable<NodeMultiCondition> nodes)
        {
            if (_nodeMultiConditions != null)
            {
                foreach (NodeMultiCondition node in _nodeMultiConditions)
                {
                    if (node != null)
                        node.RunParamChanged -= NodeMultiCondition_RunParamChanged;
                }
            }

            _nodeMultiConditions = nodes == null
                ? new List<NodeMultiCondition>()
                : nodes.Where(node => node != null).ToList();

            foreach (NodeMultiCondition node in _nodeMultiConditions)
                node.RunParamChanged += NodeMultiCondition_RunParamChanged;
        }

        /// <summary>
        /// 多条件节点运行后刷新运行参数表里的当前值。
        /// </summary>
        /// <param name="sender">触发事件的多条件节点。</param>
        /// <param name="e">多条件运行明细事件参数。</param>
        private void NodeMultiCondition_RunParamChanged(object sender, MultiConditionRunParamChangedEventArgs e)
        {
            NodeMultiCondition node = sender as NodeMultiCondition;
            if (node == null || _nodeMultiConditions == null || !_nodeMultiConditions.Contains(node))
                return;

            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;

                try
                {
                    BeginInvoke(new Action(RefreshRunParamGrid));
                }
                catch (ObjectDisposedException)
                {
                    // 窗体正在关闭时忽略最后一次刷新通知。
                }
                return;
            }

            RefreshRunParamGrid();
        }

        /// <summary>
        /// AI节点通信切换检测项或运行后更新上下限时，同步刷新运行参数表。
        /// </summary>
        /// <param name="sender">触发事件的AI节点。</param>
        /// <param name="e">检测项切换事件参数。</param>
        private void NodeTDAI_DetectItemConfigChanged(object sender, TDAIDetectItemChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, _nodeTDAI) || e == null)
                return;

            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;

                try
                {
                    BeginInvoke(new Action(() => RefreshDetectItemsFromNode(e.DetectItemName)));
                }
                catch (ObjectDisposedException)
                {
                    // 窗体正在关闭时忽略最后一次刷新通知。
                }
                return;
            }

            RefreshDetectItemsFromNode(e.DetectItemName);
        }

        /// <summary>
        /// 从AI节点通知中刷新当前通信检测项的上下限表格。
        /// </summary>
        /// <param name="detectItemName">当前检测项配置名称。</param>
        private void RefreshDetectItemsFromNode(string detectItemName)
        {
            if (!LoadDetectItemsByName(detectItemName))
                return;

            if (_nodeTDAI?.ParamForm.Params is NodeParamTDAI aiParams)
                aiParams.CurDetectItemName = detectItemName;
        }

        /// <summary>
        /// 重建运行参数表，AI检测项和多条件项共用同一套上下限列。
        /// </summary>
        private void RefreshRunParamGrid()
        {
            if (myDataGridViewForm1 == null || myDataGridViewForm1.IsDisposed)
                return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<RunParamGridItem> items = BuildRunParamGridItems();
            myDataGridViewForm1.LoadRunParamItems(items);
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【手动调参诊断】运行参数表刷新完成，总行数={items.Count}，多条件行数={items.Count(item => item.SourceType == RunParamGridSourceType.MultiCondition)}，耗时={stopwatch.ElapsedMilliseconds}ms。",
                true);
        }

        /// <summary>
        /// 构建运行参数表格行集合。
        /// </summary>
        /// <returns>运行参数表格行集合。</returns>
        private List<RunParamGridItem> BuildRunParamGridItems()
        {
            List<RunParamGridItem> items = new List<RunParamGridItem>();

            if (_currentDetectItems != null)
            {
                foreach (DetectItemInfo info in _currentDetectItems)
                    items.Add(RunParamGridItem.FromDetectItem(info));
            }

            if (_nodeMultiConditions != null)
            {
                foreach (NodeMultiCondition node in _nodeMultiConditions)
                    AddMultiConditionRunParamItems(items, node);
            }

            return items;
        }

        /// <summary>
        /// 判断指定来源节点是否允许进入当前运行参数表。
        /// </summary>
        /// <param name="sourceNodeId">来源节点ID。</param>
        /// <returns>允许显示返回true。</returns>
        private bool IsSourceNodeAllowedByRunParamFilter(int sourceNodeId)
        {
            if (_runParamDisplayFilter == null || !_runParamDisplayFilter.IsSourceNodeLimited)
                return true;

            return _runParamDisplayFilter.ContainsSourceNode(sourceNodeId);
        }

        /// <summary>
        /// 判断多条件项是否属于当前ROI绘制订阅的来源节点。
        /// </summary>
        /// <param name="node">多条件节点。</param>
        /// <param name="condition">多条件项。</param>
        /// <returns>允许显示返回true。</returns>
        private bool IsConditionAllowedByRunParamFilter(NodeMultiCondition node, MultiConditionItem condition)
        {
            if (_runParamDisplayFilter == null || !_runParamDisplayFilter.IsSourceNodeLimited)
                return true;
            if (condition == null)
                return false;
            if (node != null && _runParamDisplayFilter.ContainsSourceNode(node.ID))
                return true;

            int sourceNodeId = condition.SourceNodeId;
            if (sourceNodeId <= 0)
                RunParamDisplayFilter.TryGetNodeId(condition.SourceNodeText, out sourceNodeId);

            return sourceNodeId > 0 && _runParamDisplayFilter.ContainsSourceNode(sourceNodeId);
        }

        /// <summary>
        /// 将一个多条件节点中开启运行参数调整的条件加入表格行集合。
        /// </summary>
        /// <param name="items">运行参数表格行集合。</param>
        /// <param name="node">多条件节点。</param>
        private void AddMultiConditionRunParamItems(List<RunParamGridItem> items, NodeMultiCondition node)
        {
            if (items == null || node == null || node.ParamForm == null)
                return;

            NodeParamMultiCondition param = node.ParamForm.Params as NodeParamMultiCondition;
            if (param == null || param.Conditions == null || param.Conditions.Count == 0)
                return;

            NodeResultMultiCondition result = node.Result as NodeResultMultiCondition;
            List<NodeConditionEvaluation> details = result == null ? null : result.Details;

            for (int index = 0; index < param.Conditions.Count; index++)
            {
                MultiConditionItem condition = param.Conditions[index];
                if (condition == null || !condition.EnableRunParamAdjust)
                    continue;

                if (!IsConditionAllowedByRunParamFilter(node, condition))
                    continue;

                items.Add(new RunParamGridItem
                {
                    SourceType = RunParamGridSourceType.MultiCondition,
                    SourceNodeId = node.ID,
                    SourceIndex = index,
                    DisplayName = GetMultiConditionDisplayName(node, condition, index),
                    Info = new DetectItemInfo
                    {
                        Enable = condition.Enabled,
                        Name = BuildMultiConditionGridName(node, index),
                        MinValue = GetRunParamMinValue(condition),
                        CurValue = GetRunParamCurrentValue(details, index),
                        MaxValue = GetRunParamMaxValue(condition),
                        IsCountItem = false
                    }
                });
            }
        }

        /// <summary>
        /// 获取多条件表格显示名称，优先使用用户填写的注释。
        /// </summary>
        /// <param name="node">多条件节点。</param>
        /// <param name="condition">多条件项。</param>
        /// <param name="index">条件索引。</param>
        /// <returns>表格显示名称。</returns>
        private static string GetMultiConditionDisplayName(NodeMultiCondition node, MultiConditionItem condition, int index)
        {
            if (condition != null && !string.IsNullOrWhiteSpace(condition.Note))
                return condition.Note.Trim();

            if (condition != null && !string.IsNullOrWhiteSpace(condition.Name))
                return condition.Name.Trim();

            string nodeName = node == null ? "多条件" : node.NodeName;
            return nodeName + "-条件" + (index + 1);
        }

        /// <summary>
        /// 构建多条件隐藏行键，避免显示名称变化影响保存定位。
        /// </summary>
        /// <param name="node">多条件节点。</param>
        /// <param name="index">条件索引。</param>
        /// <returns>隐藏行键。</returns>
        private static string BuildMultiConditionGridName(NodeMultiCondition node, int index)
        {
            int nodeId = node == null ? 0 : node.ID;
            return "MultiCondition:" + nodeId + ":" + index;
        }

        /// <summary>
        /// 获取多条件表格当前值。
        /// </summary>
        /// <param name="details">多条件运行明细。</param>
        /// <param name="index">条件索引。</param>
        /// <returns>当前值文本。</returns>
        private static string GetRunParamCurrentValue(List<NodeConditionEvaluation> details, int index)
        {
            if (details == null || index < 0 || index >= details.Count || details[index] == null)
                return string.Empty;

            return details[index].ActualValue ?? string.Empty;
        }

        /// <summary>
        /// 根据操作符把多条件值映射到运行参数表下限列。
        /// </summary>
        /// <param name="condition">多条件项。</param>
        /// <returns>下限文本。</returns>
        private static string GetRunParamMinValue(MultiConditionItem condition)
        {
            if (condition == null)
                return string.Empty;

            switch (condition.Operator)
            {
                case MultiConditionOperator.LessThan:
                case MultiConditionOperator.LessThanOrEqual:
                case MultiConditionOperator.IsTrue:
                case MultiConditionOperator.IsFalse:
                case MultiConditionOperator.IsNull:
                case MultiConditionOperator.IsNotNull:
                    return string.Empty;
                default:
                    return condition.Value1 ?? string.Empty;
            }
        }

        /// <summary>
        /// 根据操作符把多条件值映射到运行参数表上限列。
        /// </summary>
        /// <param name="condition">多条件项。</param>
        /// <returns>上限文本。</returns>
        private static string GetRunParamMaxValue(MultiConditionItem condition)
        {
            if (condition == null)
                return string.Empty;

            switch (condition.Operator)
            {
                case MultiConditionOperator.GreaterThan:
                case MultiConditionOperator.GreaterThanOrEqual:
                case MultiConditionOperator.IsTrue:
                case MultiConditionOperator.IsFalse:
                case MultiConditionOperator.IsNull:
                case MultiConditionOperator.IsNotNull:
                    return string.Empty;
                case MultiConditionOperator.LessThan:
                case MultiConditionOperator.LessThanOrEqual:
                    return condition.Value1 ?? string.Empty;
                case MultiConditionOperator.Between:
                case MultiConditionOperator.NotBetween:
                    return condition.Value2 ?? string.Empty;
                default:
                    return condition.Value2 ?? string.Empty;
            }
        }

        /// <summary>
        /// 复制检测项集合，避免运行参数表刷新时直接持有方案字典对象。
        /// </summary>
        /// <param name="infos">检测项集合。</param>
        /// <returns>复制后的检测项集合。</returns>
        private static List<DetectItemInfo> CloneDetectItems(List<DetectItemInfo> infos)
        {
            List<DetectItemInfo> result = new List<DetectItemInfo>();
            if (infos == null)
                return result;

            foreach (DetectItemInfo info in infos)
            {
                if (info == null)
                    continue;

                result.Add(new DetectItemInfo
                {
                    Name = info.Name,
                    MinValue = info.MinValue,
                    MaxValue = info.MaxValue,
                    Enable = info.Enable,
                    IsCountItem = info.IsCountItem,
                    CurValue = info.CurValue
                });
            }

            return result;
        }

        /// <summary>
        /// 设置控件可用性
        /// </summary>
        /// <param name="enable"></param>
        private void SetEnable(bool enable)
        {
            uiSwitchContinue.Enabled = enable;
            uiSwitch_Learning.Enabled = enable;
            buttonOnce.Enabled = enable;
            textBoxExposureTime.Enabled = enable;
            textBoxGain.Enabled = enable;
            uiSwitchROI.Enabled = enable;
            textBoxScoreThreshold.Enabled = enable;
            textBoxScoreNMS.Enabled = enable;

            buttonOnce.Enabled = enable;
            buttonSingleImg.Enabled = enable;
            buttonCatalogueImg.Enabled = enable;
        }
        /// <summary>
        /// 单图点检
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSingleImg_Click(object sender, EventArgs e)
        {
            //实现方法;找到流程中的图像源节点，将图像路径参数设置为当前选择的图像路径
            //最后运行流程即可，并恢复原来的图像路径
            foreach (var node in process.Nodes)
            {
                if (node is NodeImageSource nodeImage)
                {
                    if (nodeImage.ParamForm.Params is NodeParamImageSoucre nodeParam)
                    {
                        var imgSrc = nodeParam.ImageSource;
                        nodeParam.ImageSource = "本地图像";
                        //打开文件对话框，选择图像文件
                        openFileDialog1.Filter = LanguageManager.T("SolRunParam.ImageFileFilter");
                        openFileDialog1.Title = LanguageManager.T("SolRunParam.SelectImageFile");
                        string modelOldPath = nodeParam.ImagePath;
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            //设置当前图像路径
                            nodeParam.ImagePath = openFileDialog1.FileName;
                            //运行流程
                            process.Run(false);
                            //恢复原来的图像路径
                            nodeParam.ImagePath = modelOldPath;
                        }
                        nodeParam.ImageSource = imgSrc;
                        nodeImage.ParamForm.Params = nodeParam;
                    }
                }
            }
        }
        /// <summary>
        /// 多图点检
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCatalogueImg_Click(object sender, EventArgs e)
        {
            //实现方法;找到流程中的图像源节点，将图像路径参数设置为当前选择的图像目录
            foreach (var node in process.Nodes)
            {
                if (node is NodeImageSource nodeImage)
                {
                    if (nodeImage.ParamForm.Params is NodeParamImageSoucre nodeParam)
                    {
                        folderBrowserDialog1.Description = LanguageManager.T("SolRunParam.SelectImageFolder");

                        if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                        {
                            if (string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
                            {
                                MessageBoxTD.Show(LanguageManager.T("SolRunParam.FolderPathRequired"));
                                return;
                            }
                            // 定义支持的图像扩展名（全部小写，便于比较）
                            string[] imageExtensions = { ".bmp", ".jpg", ".jpeg", ".png" };

                            string folderPath = folderBrowserDialog1.SelectedPath;

                            // 获取指定目录下所有文件，并筛选出符合条件的
                            var files = Directory.GetFiles(folderPath)
                                .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower())).ToArray();
                            // 临时保存原目录
                            var imgFiles = new List<string>(nodeParam.ImagePaths);
                            var imgPath = nodeParam.ImagePath;
                            var imageSource = nodeParam.ImageSource;
                            // 设置当前运行参数
                            nodeParam.ImagePath = null;
                            nodeParam.ImagePaths = new List<string>(files);
                            nodeParam.IsAutoLoop = true;
                            nodeImage.LastIndex = 0;
                            nodeParam.ImageSource = "本地图像";
                            //运行流程
                            for (int i = 0; i < files.Length; i++)
                            {
                                process.Run(false);
                                MessageBoxTD.Show(LanguageManager.Format("SolRunParam.ImageDetectionComplete", files[nodeImage.LastIndex]));
                            }
                            //恢复原来的图像路径
                            nodeParam.ImagePaths = imgFiles;
                            nodeParam.ImagePath = imgPath;
                            nodeImage.ParamForm.Params = nodeParam;
                            nodeParam.ImageSource = imageSource;
                            nodeImage.ParamForm.Params = nodeParam;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 保存所有参数到对应节点中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSaveAllParam_Click(object sender, EventArgs e)
        {
            try
            {
                SaveConfigs();
                new YTMessageBox.YTMessageBox(LanguageManager.T("SolRunParam.ParamSaveSuccess")).Show();
            }
            catch (Exception)
            {
                MessageBoxTD.Show(LanguageManager.T("SolRunParam.ParamSaveFailed"), LanguageManager.T("Common.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void SaveConfigs()
        {
            string saveStage = "读取运行参数表格";
            try
            {
                List<RunParamGridItem> runParamItems = myDataGridViewForm1.GetRunParamItems();
                if (runParamItems == null)
                    throw new Exception("运行参数表格读取失败！");

                if(_nodeSource != null)
                {
                    saveStage = "保存相机曝光和增益";
                    if (_nodeSource.ParamForm.Params is NodeParamImageSoucre imageSrcParams && textBoxExposureTime.Enabled)
                    {
                        imageSrcParams.ExposureTime = double.Parse(textBoxExposureTime.Text);
                        imageSrcParams.Gain = double.Parse(textBoxGain.Text);
                    }
                }
                if (_nodeCrop != null)
                {
                    saveStage = "保存ROI开关";
                    if (_nodeCrop.ParamForm.Params is NodeParamImageCrop imageCropParams && uiSwitchROI.Enabled)
                    {
                        imageCropParams.RoiEnable = uiSwitchROI.Active;
                    }
                }
                if (_nodeTDAI != null)
                {
                    saveStage = "保存AI阈值和检测项";
                    if (_nodeTDAI.ParamForm.Params is NodeParamTDAI aiParams && textBoxScoreThreshold.Enabled)
                    {
                        ApplyAiScoreThresholdsFromTextBoxes(aiParams);
                        aiParams.IsAutoStudy = uiSwitch_Learning.Active;
                        // 也要更新AI节点的检测项配置
                        string detectItemName = string.IsNullOrWhiteSpace(aiParams.CurDetectItemName) ? _currentDetectItemName : aiParams.CurDetectItemName.Trim();
                        if (!string.IsNullOrWhiteSpace(detectItemName))
                        {
                            if (Solution.Instance.DetectItemDic == null)
                                Solution.Instance.DetectItemDic = new Dictionary<string, List<DetectItemInfo>>();

                            List<DetectItemInfo> detectItems = runParamItems
                                .Where(item => item.SourceType == RunParamGridSourceType.DetectItem && item.Info != null)
                                .Select(item => item.Info)
                                .ToList();
                            Solution.Instance.DetectItemDic[detectItemName] = CloneDetectItems(detectItems);
                            _currentDetectItems = CloneDetectItems(detectItems);
                        }
                        var jsonStr = JsonConvert.SerializeObject(aiParams.AIInputInfo, Formatting.Indented);
                        jsonStr = StringCipher.Encrypt(jsonStr); // 加密AI配置内容
                        saveStage = "写入AI配置文件";
                        File.WriteAllText(aiParams.ConfigPath, jsonStr);
                    }
                }
                saveStage = "保存多条件上下限";
                ApplyMultiConditionRunParamItems(runParamItems);
                if (_nodeSource == null && _nodeTDAI == null && _nodeCrop == null &&
                    (_nodeMultiConditions == null || _nodeMultiConditions.Count == 0))
                {
                    return;
                }

                saveStage = "刷新运行参数表格";
                RefreshRunParamGrid();
                //2025年5月27日 节点参数有更新，应该触发节点参数界面刷新当前设置的值
                saveStage = "刷新节点参数界面";
                RefreshParamView?.Invoke(this, EventArgs.Empty);
                saveStage = "保存方案文件";
                Solution.Instance.Save(Solution.Instance.SolFileName);
                saveStage = "记录保存成功日志";
                LogHelper.AddLog(MsgLevel.Info, LanguageManager.Format("SolRunParam.ParamSavedToSolution", Solution.Instance.SolFileName), true);
            }
            catch (Exception ex)
            {
                LogManualTuningSaveFailure(ex, saveStage);
                throw;
            }
        }

        /// <summary>
        /// 安全记录手动调参保存失败的完整上下文，日志组件异常不能覆盖原始保存异常。
        /// </summary>
        /// <param name="exception">保存过程中捕获的原始异常。</param>
        /// <param name="saveStage">发生异常时正在执行的保存阶段。</param>
        private void LogManualTuningSaveFailure(Exception exception, string saveStage)
        {
            try
            {
                string processName = process == null || string.IsNullOrWhiteSpace(process.ProcessName)
                    ? "空"
                    : process.ProcessName;
                string aiNodeText = _nodeTDAI == null
                    ? "空"
                    : $"{_nodeTDAI.ID}.{_nodeTDAI.NodeName}";
                string aiConfigPath = "空";
                if (_nodeTDAI != null && _nodeTDAI.ParamForm != null &&
                    _nodeTDAI.ParamForm.Params is NodeParamTDAI aiParams &&
                    !string.IsNullOrWhiteSpace(aiParams.ConfigPath))
                {
                    aiConfigPath = aiParams.ConfigPath;
                }

                string solutionPath = string.IsNullOrWhiteSpace(Solution.Instance.SolFileName)
                    ? "空"
                    : Solution.Instance.SolFileName;
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"【手动调参保存异常】阶段={saveStage}，流程={processName}，AI节点={aiNodeText}，AI配置路径={aiConfigPath}，方案路径={solutionPath}。\r\n异常详情：{exception}",
                    true);
            }
            catch
            {
                // 诊断日志写入失败时保留原始保存异常，由上层继续显示原有错误提示。
            }
        }

        /// <summary>
        /// 从运行参数界面读取AI置信度和NMS阈值，并同步到配置对象与当前已加载模型句柄。
        /// </summary>
        /// <param name="aiParams">AI节点参数。</param>
        private void ApplyAiScoreThresholdsFromTextBoxes(NodeParamTDAI aiParams)
        {
            if (aiParams == null || aiParams.AIInputInfo == null)
                throw new Exception("AI节点配置为空，无法写入阈值！");

            if (!float.TryParse(textBoxScoreThreshold.Text, out float scoreThreshold))
                throw new Exception("AI置信度阈值格式不正确！");
            if (!float.TryParse(textBoxScoreNMS.Text, out float scoreNms))
                throw new Exception("AI NMS阈值格式不正确！");

            ApplyAiScoreThresholds(aiParams, scoreThreshold, scoreNms);
        }

        /// <summary>
        /// 同步AI阈值到持久化配置和当前Yolo模型对象，避免只保存配置但运行仍使用旧阈值。
        /// </summary>
        /// <param name="aiParams">AI节点参数。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="scoreNms">NMS阈值。</param>
        private static void ApplyAiScoreThresholds(NodeParamTDAI aiParams, float scoreThreshold, float scoreNms)
        {
            if (aiParams == null || aiParams.AIInputInfo == null)
                return;

            aiParams.AIInputInfo.ScoreThreshold = scoreThreshold;
            aiParams.AIInputInfo.ScoreNMS = scoreNms;

            if (aiParams.Yolo8 != null)
            {
                aiParams.Yolo8.ScoreThreshold = scoreThreshold;
                aiParams.Yolo8.NMSThreshold = scoreNms;
            }
        }

        /// <summary>
        /// 文本框离开焦点时即时回写当前AI模型阈值，使下一次运行立即使用新阈值。
        /// </summary>
        private void TryApplyAiScoreThresholdsFromTextBoxes()
        {
            if (_nodeTDAI == null || !textBoxScoreThreshold.Enabled)
                return;

            if (!float.TryParse(textBoxScoreThreshold.Text, out float scoreThreshold) ||
                !float.TryParse(textBoxScoreNMS.Text, out float scoreNms))
            {
                return;
            }

            if (_nodeTDAI.ParamForm.Params is NodeParamTDAI aiParams)
                ApplyAiScoreThresholds(aiParams, scoreThreshold, scoreNms);
        }

        /// <summary>
        /// 将运行参数表中的多条件上下限写回对应多条件节点。
        /// </summary>
        /// <param name="items">运行参数表格行集合。</param>
        private void ApplyMultiConditionRunParamItems(List<RunParamGridItem> items)
        {
            if (items == null || _nodeMultiConditions == null || _nodeMultiConditions.Count == 0)
                return;

            foreach (RunParamGridItem item in items)
            {
                if (item == null || item.SourceType != RunParamGridSourceType.MultiCondition || item.Info == null)
                    continue;

                NodeMultiCondition node = FindMultiConditionNode(item.SourceNodeId);
                if (node == null || node.ParamForm == null)
                    continue;

                NodeParamMultiCondition param = node.ParamForm.Params as NodeParamMultiCondition;
                if (param == null || param.Conditions == null ||
                    item.SourceIndex < 0 || item.SourceIndex >= param.Conditions.Count)
                {
                    continue;
                }

                MultiConditionItem condition = param.Conditions[item.SourceIndex];
                if (condition == null)
                    continue;

                condition.Enabled = item.Info.Enable;
                ApplyRunParamValueToCondition(condition, item.Info);
                node.ParamForm.Params = param;
            }
        }

        /// <summary>
        /// 查找指定ID的多条件节点。
        /// </summary>
        /// <param name="nodeId">多条件节点ID。</param>
        /// <returns>匹配的多条件节点。</returns>
        private NodeMultiCondition FindMultiConditionNode(int nodeId)
        {
            if (_nodeMultiConditions == null)
                return null;

            foreach (NodeMultiCondition node in _nodeMultiConditions)
            {
                if (node != null && node.ID == nodeId)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// 根据操作符把表格上下限写回多条件值1和值2。
        /// </summary>
        /// <param name="condition">多条件项。</param>
        /// <param name="info">运行参数表格值。</param>
        private static void ApplyRunParamValueToCondition(MultiConditionItem condition, DetectItemInfo info)
        {
            if (condition == null || info == null)
                return;

            switch (condition.Operator)
            {
                case MultiConditionOperator.GreaterThan:
                case MultiConditionOperator.GreaterThanOrEqual:
                    condition.Value1 = info.MinValue;
                    break;
                case MultiConditionOperator.LessThan:
                case MultiConditionOperator.LessThanOrEqual:
                    condition.Value1 = info.MaxValue;
                    break;
                case MultiConditionOperator.Between:
                case MultiConditionOperator.NotBetween:
                    condition.Value1 = info.MinValue;
                    condition.Value2 = info.MaxValue;
                    break;
                case MultiConditionOperator.IsTrue:
                case MultiConditionOperator.IsFalse:
                case MultiConditionOperator.IsNull:
                case MultiConditionOperator.IsNotNull:
                    break;
                default:
                    condition.Value1 = info.MinValue;
                    condition.Value2 = info.MaxValue;
                    break;
            }
        }

        public bool StartAutoLearning()
        {
            if (_nodeTDAI == null || !uiSwitch_Learning.Enabled)
                return false;

            uiSwitch_Learning.Active = true;
            SaveConfigs();
            return true;
        }
        CancellationTokenSource tokenSource;
        /// <summary>
        /// 连续采图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private async void uiSwitchContinue_ValueChanged(object sender, bool value)
        {
            return;
            if (_camera == null)
                return;
            if (value)
            {
                if (_camera.GetTriggerMode()==TriggerModel.Off)
                    _camera.SetTriggerMode(TriggerModel.Off);
                if (!_camera.GetGrabStatus())
                    _camera.StartGrabbing();

                tokenSource = new CancellationTokenSource();
                // 在后台线程中运行循环
                await Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            // 检查取消请求
                            tokenSource.Token.ThrowIfCancellationRequested();
                            //var img = (await _camera.GetOneFrameImage()).Bitmap;
                            var img = _camera.GetOneFrameImage();
                            var eventArgs = new ImageShowPamra(_windowsName, img);
                            try
                            {
                                ImageShowChanged?.Invoke(this, eventArgs);
                            }
                            finally
                            {
                                // 连续采图功能即使重新启用，也不能遗留无人接管的Bitmap。
                                eventArgs.DisposeUnclaimedBitmap();
                            }
                            // 可以加个短暂延时避免 CPU 占用过高（可选）
                            await Task.Delay(5, tokenSource.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { }
                }, tokenSource.Token);
            }
            else
            {
                // 停止采集
                tokenSource.Cancel();
                if (!_camera.GetGrabStatus())
                    _camera.StartGrabbing();
                if (!(_camera.GetTriggerMode() == TriggerModel.On))
                    _camera.SetTriggerMode(TriggerModel.On);
            }
        }

        /// <summary>
        /// 单次采图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void buttonOnce_Click(object sender, EventArgs e)
        {
            if (uiSwitchContinue.Active)
                uiSwitchContinue.Active = false;

            if (_nodeSource.ParamForm.Params is NodeParamImageSoucre imageSrcParams)
            {
                var imgSrc = imageSrcParams.ImageSource;
                imageSrcParams.ImageSource = "相机";
                imageSrcParams.ExposureTime = double.Parse(textBoxExposureTime.Text);

                //运行流程
                process.Run(false);
                imageSrcParams.ImageSource = imgSrc;
                _nodeSource.ParamForm.Params = imageSrcParams;
            }
        }

        /// <summary>
        /// 参数设置完成时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxExposureTime_Leave(object sender, EventArgs e)
        {
            if (sender is Control con)
            {
                switch (con.Name)
                {
                    case "textBoxExposureTime":
                        if (float.TryParse(textBoxExposureTime.Text, out float exposure))
                        {
                            _camera?.SetExposureTime(float.Parse(textBoxExposureTime.Text));
                            if (_nodeSource.ParamForm.Params is NodeParamImageSoucre imageSrcParams)
                            {
                                imageSrcParams.ExposureTime = double.Parse(textBoxExposureTime.Text);
                            }
                        }
                        break;
                    case "textBoxGain":
                        if (float.TryParse(textBoxGain.Text, out float gain))
                        {
                            _camera?.SetGain(float.Parse(textBoxGain.Text));
                            if (_nodeSource.ParamForm.Params is NodeParamImageSoucre imageSrcParams)
                            {
                                imageSrcParams.Gain = double.Parse(textBoxGain.Text);
                            }
                        }
                        break;
                    case "textBoxScoreThreshold":
                        if (double.TryParse(textBoxScoreThreshold.Text, out double scoreThreshold))
                        {
                            TryApplyAiScoreThresholdsFromTextBoxes();
                        }
                        break;
                    case "textBoxScoreNMS":
                        if (double.TryParse(textBoxScoreNMS.Text, out double scoreNMS))
                        {
                            TryApplyAiScoreThresholdsFromTextBoxes();
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
