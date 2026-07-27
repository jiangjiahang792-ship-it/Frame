using Logger;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    /// <summary>
    /// 配置模板位姿列表订阅、创建第一目标基准并预览全部目标修正结果。
    /// </summary>
    public partial class NodeParamFormPositionCorrection : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前参数窗口所属的位置修正节点。
        /// </summary>
        private NodeBase node;

        /// <summary>
        /// 初始化位置修正参数窗口。
        /// </summary>
        public NodeParamFormPositionCorrection()
        {
            InitializeComponent();
            UpdateBaselineStatus();
        }

        /// <summary>
        /// 获取或设置位置修正参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 绑定所属节点并初始化模板位姿列表订阅控件。
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            this.node = node;
            nodeSubscriptionPoses.SetExpectedValueType<List<TemplateMatchPose>>();
            nodeSubscriptionPoses.Init(node);
        }

        /// <summary>
        /// 把已保存参数恢复到窗口控件。
        /// </summary>
        public void SetParam2Form()
        {
            var param = Params as NodeParamPositionCorrection;
            if (param == null)
                return;

            nodeSubscriptionPoses.SetText(param.PoseText1, param.PoseText2);
            UpdateBaselineStatus();
        }

        /// <summary>
        /// 根据固定基准和当前模板位姿列表生成全部位置修正信息。
        /// </summary>
        internal List<PositionCorrectionInfo> BuildCorrectionItems(NodeParamPositionCorrection param)
        {
            if (param == null)
                throw new Exception("位置修正参数异常。");
            if (!param.HasBaseline || param.BasePose == null)
                throw new Exception("请先点击“创建基准”记录模板列表中的第一目标。");

            List<TemplateMatchPose> poses = ReadPoses();
            return NodePositionCorrection.BuildCorrectionItems(param.BasePose, poses);
        }

        /// <summary>
        /// 校验并保存当前窗口参数。
        /// </summary>
        private bool SaveParams()
        {
            try
            {
                ValidateSubscription();
                var oldParam = Params as NodeParamPositionCorrection;
                Params = BuildParamFromForm(oldParam);
                UpdateBaselineStatus();
                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从订阅控件构建参数；订阅源改变时自动废弃旧基准。
        /// </summary>
        private NodeParamPositionCorrection BuildParamFromForm(NodeParamPositionCorrection oldParam)
        {
            string poseText1 = nodeSubscriptionPoses.GetText1();
            string poseText2 = nodeSubscriptionPoses.GetText2();
            bool keepBaseline = oldParam != null &&
                oldParam.HasBaseline &&
                oldParam.BasePose != null &&
                string.Equals(oldParam.PoseText1, poseText1, StringComparison.Ordinal) &&
                string.Equals(oldParam.PoseText2, poseText2, StringComparison.Ordinal);

            return new NodeParamPositionCorrection
            {
                PoseText1 = poseText1,
                PoseText2 = poseText2,
                HasBaseline = keepBaseline,
                BasePose = keepBaseline ? oldParam.BasePose.Clone() : null
            };
        }

        /// <summary>
        /// 检查模板位姿列表订阅是否完整。
        /// </summary>
        private void ValidateSubscription()
        {
            if (string.IsNullOrWhiteSpace(nodeSubscriptionPoses.GetText1()) ||
                string.IsNullOrWhiteSpace(nodeSubscriptionPoses.GetText2()))
            {
                throw new Exception("请选择模板匹配位姿列表。");
            }
        }

        /// <summary>
        /// 读取模板匹配节点输出的全部目标位姿。
        /// </summary>
        private List<TemplateMatchPose> ReadPoses()
        {
            List<TemplateMatchPose> poses = nodeSubscriptionPoses.GetValue<List<TemplateMatchPose>>();
            if (poses == null || poses.Count == 0)
                throw new Exception("模板匹配位姿列表为空，请先运行模板匹配节点。");

            return poses;
        }

        /// <summary>
        /// 刷新第一目标基准状态文本。
        /// </summary>
        private void UpdateBaselineStatus()
        {
            var param = Params as NodeParamPositionCorrection;
            TemplateMatchPose pose = param == null ? null : param.BasePose;
            if (param != null && param.HasBaseline && pose != null)
            {
                labelBaselineStatus.Text =
                    $"基准：第1目标，X {pose.CenterX:F3}, Y {pose.CenterY:F3}, 角度 {pose.Angle:F3}°，缩放 {pose.ScaleX:F3}/{pose.ScaleY:F3}";
            }
            else
            {
                labelBaselineStatus.Text = "基准：未创建（默认取位姿列表第1目标）";
            }
        }

        /// <summary>
        /// 保存当前模板位姿列表中的第一目标为基准。
        /// </summary>
        private void buttonCreateBaseline_Click(object sender, EventArgs e)
        {
            try
            {
                ValidateSubscription();
                List<TemplateMatchPose> poses = ReadPoses();
                var oldParam = Params as NodeParamPositionCorrection;
                var param = BuildParamFromForm(oldParam);
                param.HasBaseline = true;
                param.BasePose = NodePositionCorrection.CreateBaseline(poses);
                Params = param;

                List<PositionCorrectionInfo> items = BuildCorrectionItems(param);
                PublishCorrectionItems(items);
                UpdateBaselineStatus();
                labelRuntimeStatus.Text = $"已创建基准，当前有效目标：{items.Count}个。";
                MessageBoxTD.Show("位置修正基准创建成功，已默认使用模板位姿列表中的第一目标。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"创建基准失败：{ex.Message}");
                if (node != null)
                    LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})创建位置修正基准异常，原因：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 预览全部目标的位置修正结果。
        /// </summary>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                List<PositionCorrectionInfo> items = BuildCorrectionItems((NodeParamPositionCorrection)Params);
                PublishCorrectionItems(items);
                PositionCorrectionInfo first = items[0];
                labelRuntimeStatus.Text =
                    $"当前有效目标：{items.Count}个；第1目标偏移：X {first.DeltaX:F3}, Y {first.DeltaY:F3}, 角度 {first.DeltaAngle:F3}°。";
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 保存位置修正参数并关闭窗口。
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        /// <summary>
        /// 把预览修正集合发布到所属节点。
        /// </summary>
        private void PublishCorrectionItems(IReadOnlyList<PositionCorrectionInfo> items)
        {
            var positionNode = node as NodePositionCorrection;
            if (positionNode != null)
                positionNode.PublishPreviewResult(items);
        }
    }
}
