using Logger;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    /// <summary>
    /// 结果叠加绘制节点，负责把订阅到的测量/检测结果转换为统一的显示结果。
    /// </summary>
    public class NodeResultOverlayDraw : NodeBase, INodeSubscriptionDependencyProvider
    {
        public NodeResultOverlayDraw(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormResultOverlayDraw();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultResultOverlayDraw();
        }

        /// <summary>
        /// 获取绘制参数中订阅的源节点 ID，供图运行器等待这些隐藏输入先完成。
        /// </summary>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds()
        {
            HashSet<int> nodeIds = new HashSet<int>();
            NodeParamResultOverlayDraw param = ParamForm == null ? null : ParamForm.Params as NodeParamResultOverlayDraw;
            if (param == null)
                return nodeIds;

            AddNodeId(nodeIds, param.ImageText1);
            if (param.Items == null)
                return nodeIds;

            foreach (ResultOverlayDrawItem item in param.Items)
            {
                if (item == null || !item.Enabled)
                    continue;

                bool needSource = item.ItemType != ResultOverlayDrawItemType.Text || !item.UseManualText;
                if (needSource)
                    AddNodeId(nodeIds, item.SourceText1);
            }

            nodeIds.Remove(ID);
            return nodeIds;
        }

        /// <summary>
        /// 从订阅控件保存的“ID.节点名”文本中提取节点 ID。
        /// </summary>
        private static void AddNodeId(HashSet<int> nodeIds, string nodeText)
        {
            if (nodeIds == null || string.IsNullOrWhiteSpace(nodeText))
                return;

            int separatorIndex = nodeText.IndexOf('.');
            string idText = separatorIndex > 0 ? nodeText.Substring(0, separatorIndex) : nodeText;
            int nodeId;
            if (int.TryParse(idText, out nodeId) && nodeId > 0)
                nodeIds.Add(nodeId);
        }

        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                CheckTokenCancel(token);

                var param = (NodeParamResultOverlayDraw)ParamForm.Params;
                OutputImage outputImage;
                AlgorithmResult displayResult;
                ResultOverlayDrawBuilder.Build(this, param, out outputImage, out displayResult);

                var nodeResult = (NodeResultResultOverlayDraw)Result;
                nodeResult.OutputImage = outputImage;
                nodeResult.Result = displayResult;

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }
    }

    internal static class ResultOverlayDrawBuilder
    {
        public static void Build(
            NodeBase owner,
            NodeParamResultOverlayDraw param,
            out OutputImage outputImage,
            out AlgorithmResult displayResult)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (param == null)
                throw new Exception("结果绘制参数为空！");

            object imageValue = ReadSubscribedValue(owner, param.ImageText1, param.ImageText2, out NodeBase _);
            OutputImage inputImage = imageValue as OutputImage;
            if (inputImage == null)
                throw new Exception("订阅的图像类型不是 OutputImage！");

            Mat cleanImage = MeasurementNodeHelper.GetFirstMat(inputImage);
            displayResult = BuildDisplayResult(owner, param);
            outputImage = new OutputImage
            {
                Bitmaps = new List<Mat> { cleanImage },
                Rectangles = inputImage.Rectangles == null ? new List<Rect>() : inputImage.Rectangles.ToList(),
                DisplayResult = displayResult
            };
        }

        public static object ReadSubscribedValue(NodeBase owner, string nodeText, string resultText, out NodeBase sourceNode)
        {
            sourceNode = null;
            if (owner == null || owner.Process == null)
                throw new Exception("当前节点没有所属流程，无法读取订阅！");
            if (string.IsNullOrWhiteSpace(nodeText))
                throw new Exception("订阅节点为空！");

            foreach (NodeBase node in owner.Process.GetUpstreamNodes(owner))
            {
                if (GetNodeText(node) != nodeText)
                    continue;

                sourceNode = node;
                if (owner.Process.IsRuning && !node.HasSuccessfulResultForRun(owner.Process.CurrentRunId))
                    throw new Exception($"节点({node.ID}.{node.NodeName})本次流程未成功运行，不能使用上次运行结果!");

                if (string.IsNullOrWhiteSpace(resultText))
                    return node.Result;

                // 动态输出变量不是真实属性，需要先按变量路径读取。
                object dynamicValue;
                if (DynamicResultVariableResolver.TryGetValue(node.Result, resultText, out dynamicValue))
                    return dynamicValue;

                PropertyInfo property = node.Result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(item => GetDisplayName(item) == resultText || item.Name == resultText);
                if (property == null || !property.CanRead)
                    throw new Exception($"节点({node.ID}.{node.NodeName})获取订阅的{resultText}值失败!");

                return property.GetValue(node.Result, null);
            }

            throw new Exception($"找不到订阅节点：{nodeText}");
        }

        private static bool TryReadDrawItemValue(NodeBase owner, ResultOverlayDrawItem item, out object value, out NodeBase sourceNode)
        {
            value = null;
            sourceNode = null;
            if (owner == null || owner.Process == null || item == null || string.IsNullOrWhiteSpace(item.SourceText1))
                return false;

            foreach (NodeBase node in owner.Process.GetUpstreamNodes(owner))
            {
                if (GetNodeText(node) != item.SourceText1)
                    continue;

                sourceNode = node;
                if (owner.Process.IsRuning && !node.HasSuccessfulResultForRun(owner.Process.CurrentRunId))
                    return false;

                if (node.Result == null)
                    return false;

                if (string.IsNullOrWhiteSpace(item.SourceText2))
                {
                    value = node.Result;
                    return true;
                }

                // 支持四则运算这类节点发布的命名输出变量，例如“变量.宽度差”。
                object dynamicValue;
                if (DynamicResultVariableResolver.TryGetValue(node.Result, item.SourceText2, out dynamicValue))
                {
                    value = dynamicValue;
                    return true;
                }

                PropertyInfo property = node.Result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(propertyInfo => GetDisplayName(propertyInfo) == item.SourceText2 || propertyInfo.Name == item.SourceText2);
                if (property == null || !property.CanRead)
                    return false;

                try
                {
                    value = property.GetValue(node.Result, null);
                }
                catch
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static AlgorithmResult BuildDisplayResult(NodeBase owner, NodeParamResultOverlayDraw param)
        {
            var displayResult = new AlgorithmResult();
            if (param.Items == null)
                return displayResult;

            foreach (ResultOverlayDrawItem item in param.Items.Where(i => i != null && i.Enabled))
            {
                switch (item.ItemType)
                {
                    case ResultOverlayDrawItemType.Text:
                        AppendText(owner, displayResult, item);
                        break;
                    case ResultOverlayDrawItemType.Line:
                        AppendLines(owner, displayResult, item);
                        break;
                    case ResultOverlayDrawItemType.Rectangle:
                        AppendRectangles(owner, displayResult, item);
                        break;
                    case ResultOverlayDrawItemType.Region:
                        AppendRegions(owner, displayResult, item);
                        break;
                }
            }

            return displayResult;
        }

        private static void AppendText(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            List<string> texts = new List<string>();
            bool usePrefix = false;
            object sourceValue = null;
            NodeBase sourceNode = null;
            if (item.UseManualText || string.IsNullOrWhiteSpace(item.SourceText1))
            {
                AddManualTextLines(texts, item.ManualText);
            }
            else
            {
                if (!TryReadDrawItemValue(owner, item, out sourceValue, out sourceNode))
                    return;

                AddTextFromValue(texts, sourceValue);
                if (texts.Count == 0 && sourceNode != null)
                    AddTextFromValue(texts, sourceNode.Result);

                usePrefix = !string.IsNullOrEmpty(item.TextPrefix);
            }

            Color drawColor = ResolveItemColor(item, sourceValue, sourceNode);
            foreach (string text in texts.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                string displayText = usePrefix ? item.TextPrefix + text : text;
                displayResult.Texts.Add(new ColorText(displayText, drawColor)
                {
                    FontSize = Math.Max(1, item.FontSize),
                    Position = item.TextPosition,
                    Margin = Math.Max(0, item.TextMargin),
                    Title = string.Empty
                });
            }
        }

        private static void AppendLines(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
                return;

            Color drawColor = ResolveItemColor(item, value, sourceNode);
            AlgorithmResult algorithmResult = TryGetAlgorithmResult(value) ?? TryGetAlgorithmResult(sourceNode == null ? null : sourceNode.Result);
            if (algorithmResult != null && algorithmResult.Lines.Count > 0)
            {
                foreach (ColorLine line in algorithmResult.Lines)
                {
                    displayResult.Lines.Add(new ColorLine(line.P1, line.P2, drawColor)
                    {
                        LineWidth = Math.Max(1, item.LineWidth)
                    });
                }
                return;
            }

            INodeResult nodeResult = value as INodeResult;
            if ((nodeResult != null && MeasurementResultReader.TryReadLine(nodeResult, out MeasuredLine measuredLine)) ||
                (sourceNode != null && MeasurementResultReader.TryReadLine(sourceNode.Result, out measuredLine)))
            {
                displayResult.Lines.Add(new ColorLine(measuredLine.Start, measuredLine.End, drawColor)
                {
                    LineWidth = Math.Max(1, item.LineWidth)
                });
                return;
            }

            return;
        }

        private static void AppendRegions(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
                return;

            Color drawColor = ResolveItemColor(item, value, sourceNode);
            AlgorithmResult algorithmResult = TryGetAlgorithmResult(value) ?? TryGetAlgorithmResult(sourceNode == null ? null : sourceNode.Result);
            if (algorithmResult != null && algorithmResult.Contours.Count > 0)
            {
                foreach (ColorContour contour in algorithmResult.Contours)
                {
                    displayResult.Contours.Add(new ColorContour(contour.Points == null ? new List<PointF>() : contour.Points.ToList(), drawColor)
                    {
                        LineWidth = Math.Max(1, item.LineWidth)
                    });
                }
                return;
            }

            INodeResult nodeResult = value as INodeResult;
            List<PointF> points;
            if ((nodeResult != null && MeasurementResultReader.TryReadRegion(nodeResult, out points)) ||
                (sourceNode != null && MeasurementResultReader.TryReadRegion(sourceNode.Result, out points)))
            {
                displayResult.Contours.Add(new ColorContour(points, drawColor)
                {
                    LineWidth = Math.Max(1, item.LineWidth)
                });
                return;
            }

            return;
        }

        private static void AppendRectangles(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
                return;

            Color drawColor = ResolveItemColor(item, value, sourceNode);
            AlgorithmResult algorithmResult = TryGetAlgorithmResult(value) ?? TryGetAlgorithmResult(sourceNode == null ? null : sourceNode.Result);
            if (algorithmResult != null)
            {
                bool added = false;
                foreach (ColorRotatedRect rect in algorithmResult.Rects)
                {
                    displayResult.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                    {
                        Color = drawColor,
                        LineWidth = Math.Max(1, item.LineWidth)
                    });
                    added = true;
                }

                foreach (List<ColorRotatedRect> rects in algorithmResult.RectsNgMap.Values)
                {
                    foreach (ColorRotatedRect rect in rects)
                    {
                        displayResult.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                        {
                            Color = drawColor,
                            LineWidth = Math.Max(1, item.LineWidth)
                        });
                        added = true;
                    }
                }

                if (added)
                    return;
            }

            return;
        }

        /// <summary>
        /// 根据绘制项颜色模式和源结果判定状态解析最终绘制颜色。
        /// </summary>
        private static Color ResolveItemColor(ResultOverlayDrawItem item, object value, NodeBase sourceNode)
        {
            if (item == null || !item.UseJudgeColor)
                return item == null ? Color.Lime : item.Color;

            bool judgeOk;
            if ((sourceNode != null && TryReadJudgeOk(sourceNode.Result, out judgeOk)) ||
                TryReadJudgeOk(value, out judgeOk))
            {
                return judgeOk ? item.OkColor : item.NgColor;
            }

            return item.Color;
        }

        /// <summary>
        /// 从结果对象或常见布尔属性中读取判定OK状态。
        /// </summary>
        private static bool TryReadJudgeOk(object source, out bool judgeOk)
        {
            judgeOk = true;
            if (source == null)
                return false;

            IJudgmentResult judgmentResult = source as IJudgmentResult;
            if (judgmentResult != null)
            {
                judgeOk = judgmentResult.JudgeOk;
                return true;
            }

            if (TryConvertToBoolean(source, out judgeOk))
                return true;

            string[] propertyNames = { "JudgeOk", "JudgmentOk", "DisplayOk", "ConditionResult", "IsAllOk", "IsOk" };
            PropertyInfo[] properties = source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                string displayName = GetDisplayName(property);
                bool isJudgeProperty = propertyNames.Contains(property.Name) ||
                    string.Equals(displayName, "判定OK", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(displayName, "条件结果", StringComparison.OrdinalIgnoreCase);
                if (!isJudgeProperty)
                    continue;

                object value = property.GetValue(source, null);
                if (TryConvertToBoolean(value, out judgeOk))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 将布尔、数字和常见OK/NG文本转换为布尔值。
        /// </summary>
        private static bool TryConvertToBoolean(object value, out bool result)
        {
            result = false;
            if (value == null)
                return false;

            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            string text = value as string;
            if (text != null)
            {
                string normalized = text.Trim();
                if (string.Equals(normalized, "OK", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "True", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "是", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }

                if (string.Equals(normalized, "NG", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "False", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, "否", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }

                return false;
            }

            if (value is IConvertible && !(value is char))
            {
                try
                {
                    result = Convert.ToDouble(value) != 0D;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static void AddTextFromValue(List<string> texts, object value)
        {
            if (value == null)
                return;

            AlgorithmResult algorithmResult = TryGetAlgorithmResult(value);
            if (algorithmResult != null)
            {
                foreach (ColorText text in algorithmResult.Texts)
                    AddManualTextLines(texts, text.Text);

                if (texts.Count == 0)
                {
                    foreach (var pair in algorithmResult.DetectResults)
                    {
                        foreach (var result in pair.Value)
                            texts.Add($"{result.Name}:{result.Value} {(result.IsOk ? "OK" : "NG")}");
                    }
                }
                return;
            }

            if (value is string)
            {
                AddManualTextLines(texts, (string)value);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is byte[]))
            {
                foreach (object item in enumerable)
                    if (item != null)
                        texts.Add(Convert.ToString(item));
                return;
            }

            texts.Add(Convert.ToString(value));
        }

        private static void AddManualTextLines(List<string> texts, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            foreach (string line in lines)
                if (!string.IsNullOrWhiteSpace(line))
                    texts.Add(line.Trim());
        }

        private static AlgorithmResult TryGetAlgorithmResult(object source)
        {
            if (source == null)
                return null;

            AlgorithmResult direct = source as AlgorithmResult;
            if (direct != null)
                return direct;

            OutputImage outputImage = source as OutputImage;
            if (outputImage != null && outputImage.DisplayResult != null)
                return outputImage.DisplayResult;

            PropertyInfo[] properties = source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0 ||
                    !typeof(AlgorithmResult).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                AlgorithmResult value = property.GetValue(source, null) as AlgorithmResult;
                if (value != null)
                    return value;
            }

            return null;
        }

        private static string GetNodeText(NodeBase node)
        {
            return $"{node.ID}.{node.NodeName}";
        }

        private static string GetDisplayName(PropertyInfo property)
        {
            DisplayNameAttribute attribute = property.GetCustomAttribute<DisplayNameAttribute>();
            return attribute == null ? property.Name : attribute.DisplayName;
        }
    }
}
