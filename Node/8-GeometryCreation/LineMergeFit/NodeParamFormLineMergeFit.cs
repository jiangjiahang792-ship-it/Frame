using Logger;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._8_GeometryCreation.LineMergeFit
{
    /// <summary>
    /// 线组合拟合参数窗体。
    /// </summary>
    public partial class NodeParamFormLineMergeFit : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前参数窗体所属节点。
        /// </summary>
        private NodeBase node;

        /// <summary>
        /// 创建线组合拟合参数窗体。
        /// </summary>
        public NodeParamFormLineMergeFit()
        {
            InitializeComponent();
            InitializeCombos();
            nodeSubscriptionLine1.HideText2();
            nodeSubscriptionLine2.HideText2();
        }

        /// <summary>
        /// 创建线组合拟合参数窗体。
        /// </summary>
        public NodeParamFormLineMergeFit(TDJS_Vision.Process process, NodeBase node) : this()
        {
            this.node = node;
        }

        /// <summary>
        /// 当前节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 初始化订阅控件所属节点。
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            this.node = node;
            nodeSubscriptionLine1.Init(node);
            nodeSubscriptionLine2.Init(node);
        }

        /// <summary>
        /// 将反序列化参数同步到界面。
        /// </summary>
        public void SetParam2Form()
        {
            var param = Params as NodeParamLineMergeFit;
            if (param == null)
                return;

            nodeSubscriptionLine1.SetText(param.Line1Text1, param.Line1Text2);
            nodeSubscriptionLine2.SetText(param.Line2Text1, param.Line2Text2);
            SelectComboValue(comboBoxFitMode, param.FitMode);
            SelectComboValue(comboBoxMeasureMode, param.MeasureMode);
            checkBoxPreferEdgePoints.Checked = param.PreferEdgePoints;
        }

        /// <summary>
        /// 执行线组合拟合。
        /// </summary>
        internal LineMergeFitMeasureResult ExecuteMeasure(NodeParamLineMergeFit param, CancellationToken token, out Mat output)
        {
            return ExecuteMeasure(param, token, true, out output);
        }

        /// <summary>
        /// 执行线组合拟合，并按调用场景决定是否准备预览底图。
        /// </summary>
        internal LineMergeFitMeasureResult ExecuteMeasure(NodeParamLineMergeFit param, CancellationToken token, bool needPreviewImage, out Mat output)
        {
            var stopwatch = Stopwatch.StartNew();
            token.ThrowIfCancellationRequested();

            output = needPreviewImage ? CloneFirstAvailableOutputImage() : null;
            stopwatch.Restart();
            MeasuredLine line1 = ReadSubscribedLine(nodeSubscriptionLine1, "直线1");
            MeasuredLine line2 = ReadSubscribedLine(nodeSubscriptionLine2, "直线2");
            List<PointF> line1Points = ReadSubscribedEdgePoints(nodeSubscriptionLine1);
            List<PointF> line2Points = ReadSubscribedEdgePoints(nodeSubscriptionLine2);

            LineMergeFitMeasureResult result = LineMergeFitAlgorithm.Execute(line1, line2, line1Points, line2Points, param);
            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// 初始化枚举下拉框。
        /// </summary>
        private void InitializeCombos()
        {
            AddComboItem(comboBoxFitMode, LineMergeFitMode.CollinearMerge, "共线合并拟合");
            AddComboItem(comboBoxFitMode, LineMergeFitMode.ParallelCenterLine, "平行中线拟合");
            SelectComboValue(comboBoxFitMode, LineMergeFitMode.CollinearMerge);

            AddComboItem(comboBoxMeasureMode, GeometryMeasureMode.SubPixel, "亚像素");
            AddComboItem(comboBoxMeasureMode, GeometryMeasureMode.Pixel, "像素");
            SelectComboValue(comboBoxMeasureMode, GeometryMeasureMode.SubPixel);
        }

        /// <summary>
        /// 保存界面参数。
        /// </summary>
        private bool SaveParams()
        {
            try
            {
                ValidateSubscriptions();
                Params = BuildParamFromForm();
                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从界面控件创建参数对象。
        /// </summary>
        private NodeParamLineMergeFit BuildParamFromForm()
        {
            return new NodeParamLineMergeFit
            {
                Line1Text1 = nodeSubscriptionLine1.GetText1(),
                Line1Text2 = nodeSubscriptionLine1.GetText2(),
                Line2Text1 = nodeSubscriptionLine2.GetText1(),
                Line2Text2 = nodeSubscriptionLine2.GetText2(),
                FitMode = GetComboValue(comboBoxFitMode, LineMergeFitMode.CollinearMerge),
                MeasureMode = GetComboValue(comboBoxMeasureMode, GeometryMeasureMode.SubPixel),
                PreferEdgePoints = checkBoxPreferEdgePoints.Checked
            };
        }

        /// <summary>
        /// 校验订阅输入。
        /// </summary>
        private void ValidateSubscriptions()
        {
            if (string.IsNullOrWhiteSpace(nodeSubscriptionLine1.GetText1()))
                throw new Exception("请选择第一条直线来源。");
            if (string.IsNullOrWhiteSpace(nodeSubscriptionLine2.GetText1()))
                throw new Exception("请选择第二条直线来源。");
            if (nodeSubscriptionLine1.GetText1() == nodeSubscriptionLine2.GetText1())
                throw new Exception("两条直线不能订阅同一个节点。");
        }

        /// <summary>
        /// 从订阅节点读取标准线段。
        /// </summary>
        private static MeasuredLine ReadSubscribedLine(NodeSubscription subscription, string label)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadLine(sourceNode.Result, out MeasuredLine line))
                throw new Exception($"{label}订阅节点没有可读取的线段结果。");

            return line;
        }

        /// <summary>
        /// 从订阅节点读取边缘点集合。
        /// </summary>
        private static List<PointF> ReadSubscribedEdgePoints(NodeSubscription subscription)
        {
            try
            {
                NodeBase sourceNode = subscription.GetSelectedNode();
                object result = sourceNode.Result;
                if (TryReadPointListProperty(result, "EdgePoints", out List<PointF> edgePoints))
                    return edgePoints;
                if (TryReadPointListProperty(result, "Points", out List<PointF> points))
                    return points;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"读取线组合拟合边缘点失败：{ex.Message}", true);
            }

            return new List<PointF>();
        }

        /// <summary>
        /// 克隆第一张可用的上游输出图像。
        /// </summary>
        private Mat CloneFirstAvailableOutputImage()
        {
            Mat image = TryCloneOutputImage(nodeSubscriptionLine1);
            if (image != null && !image.Empty())
                return image;

            image = TryCloneOutputImage(nodeSubscriptionLine2);
            if (image != null && !image.Empty())
                return image;

            image = TryCloneNearestUpstreamOutputImage(nodeSubscriptionLine1);
            if (image != null && !image.Empty())
                return image;

            image = TryCloneNearestUpstreamOutputImage(nodeSubscriptionLine2);
            if (image != null && !image.Empty())
                return image;

            image = TryCloneNearestCurrentUpstreamOutputImage();
            if (image != null && !image.Empty())
                return image;

            return new Mat();
        }

        /// <summary>
        /// 从订阅节点结果克隆输出图像。
        /// </summary>
        private static Mat TryCloneOutputImage(NodeSubscription subscription)
        {
            try
            {
                NodeBase sourceNode = subscription.GetSelectedNode();
                return TryCloneOutputImage(sourceNode);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从指定节点结果克隆第一张可预览图像。
        /// </summary>
        private static Mat TryCloneOutputImage(NodeBase sourceNode)
        {
            try
            {
                if (sourceNode == null)
                    return null;

                OutputImage outputImage = TryGetOutputImage(sourceNode.Result);
                if (outputImage == null)
                    return null;

                Mat preview = MeasurementNodeHelper.GetReadOnlyPreviewMat(outputImage);
                return preview == null || preview.Empty() ? null : preview.Clone();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从订阅线节点的最近上游节点中查找可预览图像。
        /// </summary>
        private static Mat TryCloneNearestUpstreamOutputImage(NodeSubscription subscription)
        {
            try
            {
                NodeBase sourceNode = subscription.GetSelectedNode();
                return TryCloneNearestUpstreamOutputImage(sourceNode);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从当前线组合拟合节点的最近上游节点中查找可预览图像。
        /// </summary>
        private Mat TryCloneNearestCurrentUpstreamOutputImage()
        {
            return TryCloneNearestUpstreamOutputImage(node);
        }

        /// <summary>
        /// 按流程显示顺序反向扫描上游节点，优先取得离目标节点更近的图像。
        /// </summary>
        private static Mat TryCloneNearestUpstreamOutputImage(NodeBase targetNode)
        {
            if (targetNode == null || targetNode.Process == null)
                return null;

            List<NodeBase> upstreamNodes = targetNode.Process.GetUpstreamNodes(targetNode);
            for (int index = upstreamNodes.Count - 1; index >= 0; index--)
            {
                Mat image = TryCloneOutputImage(upstreamNodes[index]);
                if (image != null && !image.Empty())
                    return image;
            }

            return null;
        }

        /// <summary>
        /// 从任意节点结果中读取输出图像属性。
        /// </summary>
        private static OutputImage TryGetOutputImage(object result)
        {
            if (result == null)
                return null;

            PropertyInfo property = result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(item => item.CanRead &&
                                        item.GetIndexParameters().Length == 0 &&
                                        typeof(OutputImage).IsAssignableFrom(item.PropertyType));
            return property == null ? null : property.GetValue(result, null) as OutputImage;
        }

        /// <summary>
        /// 读取指定名称的点集合属性。
        /// </summary>
        private static bool TryReadPointListProperty(object source, string propertyName, out List<PointF> points)
        {
            points = new List<PointF>();
            if (source == null)
                return false;

            PropertyInfo property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length > 0)
                return false;

            object value = property.GetValue(source, null);
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
                return false;

            foreach (object item in enumerable)
            {
                if (item is PointF)
                {
                    points.Add((PointF)item);
                }
                else if (item is System.Drawing.Point)
                {
                    System.Drawing.Point point = (System.Drawing.Point)item;
                    points.Add(new PointF(point.X, point.Y));
                }
            }

            return points.Count > 0;
        }

        /// <summary>
        /// 添加枚举下拉项。
        /// </summary>
        private static void AddComboItem<T>(ComboBox comboBox, T value, string text)
        {
            comboBox.Items.Add(new ComboItem<T>(value, text));
        }

        /// <summary>
        /// 选择指定枚举值。
        /// </summary>
        private static void SelectComboValue<T>(ComboBox comboBox, T value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                ComboItem<T> item = comboBox.Items[index] as ComboItem<T>;
                if (item != null && EqualityComparer<T>.Default.Equals(item.Value, value))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 获取下拉框枚举值。
        /// </summary>
        private static T GetComboValue<T>(ComboBox comboBox, T defaultValue)
        {
            ComboItem<T> item = comboBox.SelectedItem as ComboItem<T>;
            return item == null ? defaultValue : item.Value;
        }

        /// <summary>
        /// 更新运行状态文字。
        /// </summary>
        private void UpdateRuntimeStatus(LineMergeFitMeasureResult measureResult)
        {
            if (measureResult == null)
            {
                labelRuntimeStatus.Text = "暂无运行结果";
                return;
            }

            if (measureResult.Success && measureResult.OutputLine != null)
            {
                double length = Math.Sqrt(
                    Math.Pow(measureResult.OutputLine.End.X - measureResult.OutputLine.Start.X, 2) +
                    Math.Pow(measureResult.OutputLine.End.Y - measureResult.OutputLine.Start.Y, 2));
                labelRuntimeStatus.Text =
                    $"成功：长度 {length:F3}px，拟合误差 {measureResult.FitError:F3}px，角度差 {measureResult.AngleDifference:F3}°";
            }
            else
            {
                labelRuntimeStatus.Text = $"失败：{measureResult.Message}";
            }
        }

        /// <summary>
        /// 执行按钮事件。
        /// </summary>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                if (!SaveParams())
                    return;

                Mat output = null;
                try
                {
                    LineMergeFitMeasureResult measureResult = ExecuteMeasure((NodeParamLineMergeFit)Params, CancellationToken.None, out output);
                    UpdateRuntimeStatus(measureResult);
                    SetPreview(output, NodeLineMergeFit.BuildDisplayResult(measureResult));
                }
                finally
                {
                    output?.Dispose();
                }
            }
            catch (Exception ex)
            {
                labelRuntimeStatus.Text = $"运行异常：{ex.Message}";
                MessageBoxTD.Show($"运行异常：{ex.Message}");
                if (node != null)
                    LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})线组合拟合预览异常，原因：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 保存按钮事件。
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Close();
        }

        /// <summary>
        /// 将预览图像和拟合叠加层显示到窗体中。
        /// </summary>
        private void SetPreview(Mat image, TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult displayResult)
        {
            if (image == null || image.Empty())
            {
                showImageControlPreview.SetDisplayResult(null);
                labelRuntimeStatus.Text = "未找到可预览的上游输出图像，请确认上游卡尺找线已输出图像。";
                return;
            }

            showImageControlPreview.SetImage(MeasurementNodeHelper.ToPreviewBitmap(image), displayResult);
        }

        /// <summary>
        /// 下拉框显示项。
        /// </summary>
        private class ComboItem<T>
        {
            /// <summary>
            /// 业务值。
            /// </summary>
            public T Value { get; private set; }

            /// <summary>
            /// 显示文本。
            /// </summary>
            private string Text { get; set; }

            /// <summary>
            /// 创建下拉框显示项。
            /// </summary>
            public ComboItem(T value, string text)
            {
                Value = value;
                Text = text;
            }

            /// <summary>
            /// 返回显示文本。
            /// </summary>
            public override string ToString()
            {
                return Text;
            }
        }
    }
}
