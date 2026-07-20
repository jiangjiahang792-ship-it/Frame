using Logger;
using System;
using System.Globalization;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    public partial class NodeParamFormPositionCorrection : FormBase, INodeParamForm
    {
        private NodeBase node;

        public NodeParamFormPositionCorrection()
        {
            InitializeComponent();
            UpdateBaselineStatus();
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            this.node = node;
            nodeSubscriptionX.Init(node);
            nodeSubscriptionY.Init(node);
            nodeSubscriptionAngle.Init(node);
        }

        public void SetParam2Form()
        {
            var param = Params as NodeParamPositionCorrection;
            if (param == null)
                return;

            nodeSubscriptionX.SetText(param.TextX1, param.TextX2);
            nodeSubscriptionY.SetText(param.TextY1, param.TextY2);
            nodeSubscriptionAngle.SetText(param.TextAngle1, param.TextAngle2);
            UpdateBaselineStatus();
        }

        internal PositionCorrectionInfo BuildCorrectionInfo(NodeParamPositionCorrection param)
        {
            if (param == null)
                throw new Exception("位置修正参数异常。");
            if (!param.HasBaseline)
                throw new Exception("请先点击“创建基准”记录模板基准位置。");

            double currentX = ReadDouble(nodeSubscriptionX, "X");
            double currentY = ReadDouble(nodeSubscriptionY, "Y");
            double currentAngle = ReadDouble(nodeSubscriptionAngle, "角度");

            return new PositionCorrectionInfo
            {
                IsValid = true,
                BaseX = param.BaseX,
                BaseY = param.BaseY,
                BaseAngle = param.BaseAngle,
                CurrentX = currentX,
                CurrentY = currentY,
                CurrentAngle = currentAngle
            };
        }

        private bool SaveParams()
        {
            try
            {
                ValidateSubscriptions();
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

        private NodeParamPositionCorrection BuildParamFromForm(NodeParamPositionCorrection oldParam)
        {
            string textX1 = nodeSubscriptionX.GetText1();
            string textX2 = nodeSubscriptionX.GetText2();
            string textY1 = nodeSubscriptionY.GetText1();
            string textY2 = nodeSubscriptionY.GetText2();
            string textAngle1 = nodeSubscriptionAngle.GetText1();
            string textAngle2 = nodeSubscriptionAngle.GetText2();
            bool keepBaseline = oldParam != null &&
                oldParam.HasBaseline &&
                string.Equals(oldParam.TextX1, textX1, StringComparison.Ordinal) &&
                string.Equals(oldParam.TextX2, textX2, StringComparison.Ordinal) &&
                string.Equals(oldParam.TextY1, textY1, StringComparison.Ordinal) &&
                string.Equals(oldParam.TextY2, textY2, StringComparison.Ordinal) &&
                string.Equals(oldParam.TextAngle1, textAngle1, StringComparison.Ordinal) &&
                string.Equals(oldParam.TextAngle2, textAngle2, StringComparison.Ordinal);

            return new NodeParamPositionCorrection
            {
                TextX1 = textX1,
                TextX2 = textX2,
                TextY1 = textY1,
                TextY2 = textY2,
                TextAngle1 = textAngle1,
                TextAngle2 = textAngle2,
                HasBaseline = keepBaseline,
                BaseX = keepBaseline ? oldParam.BaseX : 0,
                BaseY = keepBaseline ? oldParam.BaseY : 0,
                BaseAngle = keepBaseline ? oldParam.BaseAngle : 0
            };
        }

        private void ValidateSubscriptions()
        {
            if (string.IsNullOrWhiteSpace(nodeSubscriptionX.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionX.GetText2()))
                throw new Exception("请选择X位置信息。");
            if (string.IsNullOrWhiteSpace(nodeSubscriptionY.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionY.GetText2()))
                throw new Exception("请选择Y位置信息。");
            if (string.IsNullOrWhiteSpace(nodeSubscriptionAngle.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionAngle.GetText2()))
                throw new Exception("请选择角度信息。");
        }

        private static double ReadDouble(NodeSubscription subscription, string name)
        {
            object value = subscription.GetValue<object>();
            if (value == null)
                throw new Exception($"{name}订阅值为空。");

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new Exception($"{name}订阅值不是有效数字：{ex.Message}");
            }
        }

        private void UpdateBaselineStatus()
        {
            var param = Params as NodeParamPositionCorrection;
            if (param != null && param.HasBaseline)
            {
                labelBaselineStatus.Text = $"基准：X {param.BaseX:F3}, Y {param.BaseY:F3}, 角度 {param.BaseAngle:F3}°";
            }
            else
            {
                labelBaselineStatus.Text = "基准：未创建";
            }
        }

        private void buttonCreateBaseline_Click(object sender, EventArgs e)
        {
            try
            {
                ValidateSubscriptions();
                double baseX = ReadDouble(nodeSubscriptionX, "X");
                double baseY = ReadDouble(nodeSubscriptionY, "Y");
                double baseAngle = ReadDouble(nodeSubscriptionAngle, "角度");
                var oldParam = Params as NodeParamPositionCorrection;
                var param = BuildParamFromForm(oldParam);
                param.HasBaseline = true;
                param.BaseX = baseX;
                param.BaseY = baseY;
                param.BaseAngle = baseAngle;
                Params = param;
                PublishCorrectionInfo(BuildCorrectionInfo(param));
                UpdateBaselineStatus();
                MessageBoxTD.Show("位置修正基准创建成功。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"创建基准失败：{ex.Message}");
                if (node != null)
                    LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})创建位置修正基准异常，原因：{ex.Message}", true);
            }
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                var info = BuildCorrectionInfo((NodeParamPositionCorrection)Params);
                PublishCorrectionInfo(info);
                labelRuntimeStatus.Text = $"当前：X {info.CurrentX:F3}, Y {info.CurrentY:F3}, 角度 {info.CurrentAngle:F3}°；偏移：X {info.DeltaX:F3}, Y {info.DeltaY:F3}, 角度 {info.DeltaAngle:F3}°";
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        private void PublishCorrectionInfo(PositionCorrectionInfo info)
        {
            if (node is NodePositionCorrection positionNode)
                positionNode.PublishPreviewResult(info);
        }
    }
}
