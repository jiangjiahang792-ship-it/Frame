using Logger;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;

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
                ResultOverlayDrawPerformanceDiagnostics performanceDiagnostics;
                ResultOverlayDrawBuilder.Build(this, param, out outputImage, out displayResult, out performanceDiagnostics);

                var nodeResult = (NodeResultResultOverlayDraw)Result;
                nodeResult.OutputImage = outputImage;
                nodeResult.Result = displayResult;

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if (showLog)
                {
                    LogHelper.AddLog(MsgLevel.Debug, $"【性能诊断-ROI结果绘制】节点({ID}.{NodeName}) {performanceDiagnostics.ToLogText()}；状态耗时={time}ms", true);
                    if (performanceDiagnostics.ShouldLogSlow())
                    {
                        LogHelper.AddLog(
                            MsgLevel.Debug,
                            $"【慢诊断-ROI结果绘制】节点({ID}.{NodeName}) {performanceDiagnostics.ToLogText()}；图像源={performanceDiagnostics.ImageSourceNodeText}；输入图像={performanceDiagnostics.InputImageText}；输出图像={performanceDiagnostics.OutputImageText}；显示结果={PerformanceSpikeDiagnostics.GetAlgorithmResultText(displayResult)}；状态耗时={time}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                            true);
                    }

                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                }

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
        /// <summary>
        /// 按来源结果类型缓存自动判定读取器，避免连续运行时每个绘制项重复反射。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Func<object, bool?>> AutomaticJudgeReaderCache =
            new ConcurrentDictionary<Type, Func<object, bool?>>();

        /// <summary>
        /// 按来源结果类型和订阅结果名称缓存多目标文本访问器，避免连续运行时重复扫描属性。
        /// </summary>
        private static readonly ConcurrentDictionary<Tuple<Type, string>, MultiTargetTextAccessor> MultiTargetTextAccessorCache =
            new ConcurrentDictionary<Tuple<Type, string>, MultiTargetTextAccessor>();

        public static void Build(
            NodeBase owner,
            NodeParamResultOverlayDraw param,
            out OutputImage outputImage,
            out AlgorithmResult displayResult)
        {
            ResultOverlayDrawPerformanceDiagnostics performanceDiagnostics;
            Build(owner, param, out outputImage, out displayResult, out performanceDiagnostics);
        }

        /// <summary>
        /// 根据参数构建输出图像和显示结果，并返回分阶段性能诊断。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="param">节点参数。</param>
        /// <param name="outputImage">输出图像。</param>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="performanceDiagnostics">构建过程性能诊断。</param>
        public static void Build(
            NodeBase owner,
            NodeParamResultOverlayDraw param,
            out OutputImage outputImage,
            out AlgorithmResult displayResult,
            out ResultOverlayDrawPerformanceDiagnostics performanceDiagnostics)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (param == null)
                throw new Exception("结果绘制参数为空！");

            Stopwatch stopwatch = Stopwatch.StartNew();
            performanceDiagnostics = new ResultOverlayDrawPerformanceDiagnostics();

            object imageValue = ReadSubscribedValue(owner, param.ImageText1, param.ImageText2, out NodeBase imageSourceNode);
            performanceDiagnostics.ImageSubscriptionMs = stopwatch.ElapsedMilliseconds;
            performanceDiagnostics.ImageSourceNodeText = imageSourceNode == null ? "空" : GetNodeText(imageSourceNode);
            OutputImage inputImage = imageValue as OutputImage;
            if (inputImage == null)
                throw new Exception("订阅的图像类型不是 OutputImage！");
            performanceDiagnostics.InputImageText = PerformanceSpikeDiagnostics.GetOutputImageText(inputImage);

            Mat cleanImage = MeasurementNodeHelper.GetFirstMat(inputImage);
            long afterGetCleanImage = stopwatch.ElapsedMilliseconds;
            performanceDiagnostics.GetCleanImageMs = afterGetCleanImage - performanceDiagnostics.ImageSubscriptionMs;

            displayResult = BuildDisplayResult(owner, param, performanceDiagnostics);
            long afterBuildDisplay = stopwatch.ElapsedMilliseconds;
            performanceDiagnostics.BuildDisplayResultMs = afterBuildDisplay - afterGetCleanImage;
            outputImage = new OutputImage
            {
                Bitmaps = new List<Mat> { cleanImage },
                Rectangles = inputImage.Rectangles == null ? new List<Rect>() : inputImage.Rectangles.ToList(),
                DisplayResult = displayResult
            };
            long afterBuildOutput = stopwatch.ElapsedMilliseconds;
            performanceDiagnostics.BuildOutputImageMs = afterBuildOutput - afterBuildDisplay;
            performanceDiagnostics.TotalMs = afterBuildOutput;
            performanceDiagnostics.CaptureDisplayResultCounts(displayResult);
            performanceDiagnostics.OutputImageText = PerformanceSpikeDiagnostics.GetOutputImageText(outputImage);
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

        /// <summary>
        /// 生成显示结果，并按绘制项类型统计耗时。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="param">节点参数。</param>
        /// <param name="performanceDiagnostics">可选的性能诊断对象。</param>
        /// <returns>显示结果。</returns>
        private static AlgorithmResult BuildDisplayResult(
            NodeBase owner,
            NodeParamResultOverlayDraw param,
            ResultOverlayDrawPerformanceDiagnostics performanceDiagnostics)
        {
            var displayResult = new AlgorithmResult();
            if (param.Items == null)
                return displayResult;

            List<ResultOverlayDrawItem> enabledItems = param.Items.Where(i => i != null && i.Enabled).ToList();
            if (performanceDiagnostics != null)
                performanceDiagnostics.EnabledItemCount = enabledItems.Count;

            foreach (ResultOverlayDrawItem item in enabledItems)
            {
                Stopwatch itemWatch = Stopwatch.StartNew();
                switch (item.ItemType)
                {
                    case ResultOverlayDrawItemType.Text:
                        AppendText(owner, displayResult, item, param);
                        if (performanceDiagnostics != null)
                        {
                            performanceDiagnostics.TextItemCount++;
                            performanceDiagnostics.TextItemsMs += itemWatch.ElapsedMilliseconds;
                        }
                        break;
                    case ResultOverlayDrawItemType.Line:
                        AppendLines(owner, displayResult, item);
                        if (performanceDiagnostics != null)
                        {
                            performanceDiagnostics.LineItemCount++;
                            performanceDiagnostics.LineItemsMs += itemWatch.ElapsedMilliseconds;
                        }
                        break;
                    case ResultOverlayDrawItemType.Rectangle:
                        AppendRectangles(owner, displayResult, item);
                        if (performanceDiagnostics != null)
                        {
                            performanceDiagnostics.RectangleItemCount++;
                            performanceDiagnostics.RectangleItemsMs += itemWatch.ElapsedMilliseconds;
                        }
                        break;
                    case ResultOverlayDrawItemType.Region:
                        AppendRegions(owner, displayResult, item);
                        if (performanceDiagnostics != null)
                        {
                            performanceDiagnostics.RegionItemCount++;
                            performanceDiagnostics.RegionItemsMs += itemWatch.ElapsedMilliseconds;
                        }
                        break;
                    case ResultOverlayDrawItemType.Roi:
                        AppendAutomaticRoi(owner, displayResult, item, param);
                        if (performanceDiagnostics != null)
                        {
                            performanceDiagnostics.RoiItemCount++;
                            performanceDiagnostics.RoiItemsMs += itemWatch.ElapsedMilliseconds;
                        }
                        break;
                }
            }

            return displayResult;
        }

        private static void AppendText(
            NodeBase owner,
            AlgorithmResult displayResult,
            ResultOverlayDrawItem item,
            NodeParamResultOverlayDraw param)
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
                {
                    AppendMissingResultText(displayResult, item, sourceNode);
                    return;
                }

                texts.AddRange(BuildTextValueLines(
                    sourceNode == null ? null : sourceNode.Result,
                    item.SourceText2,
                    sourceValue));
                if (texts.Count == 0 && sourceNode != null)
                    AddTextFromValue(texts, sourceNode.Result);

                if (texts.Count == 0)
                {
                    AppendMissingResultText(displayResult, item, sourceNode);
                    return;
                }

                usePrefix = !string.IsNullOrEmpty(item.TextPrefix);
            }

            bool isOk = ResolveAutomaticJudgeOk(sourceValue, sourceNode == null ? null : sourceNode.Result);
            displayResult.IsAllOk = displayResult.IsAllOk && isOk;
            Color drawColor = isOk ? param.OkColor : param.NgColor;
            foreach (string text in texts.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                string displayText = usePrefix ? item.TextPrefix + text : text;
                AddDisplayText(displayResult, new ColorText(displayText, drawColor)
                {
                    FontSize = Math.Max(1, item.FontSize),
                    Position = item.TextPosition,
                    CoordinateMode = item.TextCoordinateMode,
                    Margin = Math.Max(0, item.TextMargin),
                    Title = string.Empty
                }, IsMissingPromptText(displayText));
            }
        }

        /// <summary>
        /// 按订阅值实际类型自动追加全部ROI几何内容。
        /// </summary>
        /// <param name="owner">当前ROI结果绘制节点。</param>
        /// <param name="displayResult">目标显示结果。</param>
        /// <param name="item">自动ROI绘制项。</param>
        /// <param name="param">包含统一OK/NG颜色的节点参数。</param>
        private static void AppendAutomaticRoi(
            NodeBase owner,
            AlgorithmResult displayResult,
            ResultOverlayDrawItem item,
            NodeParamResultOverlayDraw param)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
            {
                AppendMissingResultText(displayResult, item, sourceNode);
                return;
            }

            SubscriptionDataCategory category = ResolveSubscriptionCategory(
                sourceNode,
                item.SourceText2,
                value);
            bool isOk = ResolveAutomaticJudgeOk(value, sourceNode == null ? null : sourceNode.Result);
            displayResult.IsAllOk = displayResult.IsAllOk && isOk;
            Color displayColor = isOk ? param.OkColor : param.NgColor;
            OverlayGeometryRenderContext context = new OverlayGeometryRenderContext
            {
                FallbackColor = displayColor,
                OverrideColor = displayColor,
                LineWidth = Math.Max(1, item.LineWidth)
            };

            bool recognized = OverlayGeometryAdapterRegistry.TryAppend(
                displayResult,
                value,
                sourceNode == null ? null : sourceNode.Result,
                category,
                context,
                out int addedCount);
            if (!recognized || addedCount == 0)
                AppendMissingResultText(displayResult, item, sourceNode);
        }

        /// <summary>
        /// 从统一订阅目录读取输出类别，找不到描述时按实际CLR类型回退。
        /// </summary>
        /// <param name="sourceNode">订阅来源节点。</param>
        /// <param name="resultText">结果显示名或属性路径。</param>
        /// <param name="value">已经读取一次的订阅值。</param>
        /// <returns>统一订阅数据类别。</returns>
        private static SubscriptionDataCategory ResolveSubscriptionCategory(
            NodeBase sourceNode,
            string resultText,
            object value)
        {
            if (sourceNode != null)
            {
                IReadOnlyList<SubscriptionOutputDescriptor> outputs = SubscriptionPortCatalog.GetOutputs(
                    sourceNode,
                    SubscriptionInputContract.AnyVisible(),
                    true,
                    resultText);
                SubscriptionOutputDescriptor descriptor = outputs.FirstOrDefault(output =>
                    string.Equals(output.DisplayName, resultText, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(output.PropertyPath, resultText, StringComparison.OrdinalIgnoreCase));
                if (descriptor != null && !descriptor.IsMissing)
                    return descriptor.Category;
            }

            return value == null
                ? SubscriptionDataCategory.Unknown
                : SubscriptionTypeCompatibility.ResolveCategory(value.GetType());
        }

        private static void AppendLines(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
            {
                AppendMissingResultText(displayResult, item, sourceNode);
                return;
            }

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

            AppendMissingResultText(displayResult, item, sourceNode);
            return;
        }

        private static void AppendRegions(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
            {
                AppendMissingResultText(displayResult, item, sourceNode);
                return;
            }

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

            AppendMissingResultText(displayResult, item, sourceNode);
            return;
        }

        private static void AppendRectangles(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDrawItem item)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
            {
                AppendMissingResultText(displayResult, item, sourceNode);
                return;
            }

            Color drawColor = ResolveItemColor(item, value, sourceNode);
            AlgorithmResult algorithmResult = TryGetAlgorithmResult(value) ?? TryGetAlgorithmResult(sourceNode == null ? null : sourceNode.Result);
            if (algorithmResult != null)
            {
                bool added = false;
                bool preserveElementColor = ShouldPreserveAlgorithmResultElementColor(item, algorithmResult);
                foreach (ColorRotatedRect rect in algorithmResult.Rects)
                {
                    displayResult.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                    {
                        Color = preserveElementColor ? rect.Color : drawColor,
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
                            Color = preserveElementColor ? rect.Color : drawColor,
                            LineWidth = Math.Max(1, item.LineWidth)
                        });
                        added = true;
                    }
                }

                if (added)
                    return;
            }

            AppendMissingResultText(displayResult, item, sourceNode);
            return;
        }

        /// <summary>
        /// 订阅结果不存在或没有可绘制几何时追加红色提示，避免失败项在客户画面上静默消失。
        /// </summary>
        private static void AppendMissingResultText(AlgorithmResult displayResult, ResultOverlayDrawItem item, NodeBase sourceNode)
        {
            if (displayResult == null || item == null)
                return;
            if (ShouldSuppressMissingPromptForSource(sourceNode))
                return;

            string text = BuildMissingResultText(item, sourceNode);
            displayResult.IsAllOk = false;
            AddDisplayText(displayResult, new ColorText(text, Color.Red)
            {
                FontSize = Math.Max(1, item.FontSize),
                Position = item.TextPosition,
                CoordinateMode = item.TextCoordinateMode,
                Margin = Math.Max(0, item.TextMargin),
                Title = string.Empty
            }, true);
        }

        /// <summary>
        /// 生成缺失结果提示文本，文本绘制项优先沿用配置前缀，图形绘制项优先显示订阅源名称。
        /// </summary>
        private static string BuildMissingResultText(ResultOverlayDrawItem item, NodeBase sourceNode)
        {
            if (item.ItemType == ResultOverlayDrawItemType.Text && !string.IsNullOrEmpty(item.TextPrefix))
                return item.TextPrefix + "未查到";

            string sourceText = sourceNode == null ? item.SourceText1 : GetNodeText(sourceNode);
            if (!string.IsNullOrWhiteSpace(sourceText))
                return sourceText + "：未查到";

            if (!string.IsNullOrWhiteSpace(item.Name))
                return item.Name + "：未查到";

            return "未查到";
        }

        /// <summary>
        /// 判断订阅来源成功运行但没有几何输出时，是否应视为正常空结果。
        /// </summary>
        /// <param name="sourceNode">订阅来源节点。</param>
        /// <returns>需要隐藏缺失提示时返回 true。</returns>
        private static bool ShouldSuppressMissingPromptForSource(NodeBase sourceNode)
        {
            if (sourceNode == null || sourceNode.RuntimeStatus != NodeStatus.Successful)
                return false;

            return IsSuccessfulEmptyGeometrySource(sourceNode);
        }

        /// <summary>
        /// 判断节点类型是否允许“成功运行但没有矩形/轮廓”作为有效输出。
        /// </summary>
        /// <param name="sourceNode">订阅来源节点。</param>
        /// <returns>允许成功空几何输出时返回 true。</returns>
        private static bool IsSuccessfulEmptyGeometrySource(NodeBase sourceNode)
        {
            return sourceNode is NodeTDAI ||
                sourceNode.NodeType == NodeType.UnsupervisedDetection ||
                sourceNode.NodeType == NodeType.LargeModelDetection;
        }

        /// <summary>
        /// 判断算法结果是否应该保留检测项内部颜色，避免AI多检测项被整体IsAllOk颜色覆盖。
        /// </summary>
        /// <param name="item">当前绘制项。</param>
        /// <param name="algorithmResult">源算法结果。</param>
        /// <returns>需要保留源元素颜色返回true。</returns>
        private static bool ShouldPreserveAlgorithmResultElementColor(ResultOverlayDrawItem item, AlgorithmResult algorithmResult)
        {
            return item != null &&
                item.UseJudgeColor &&
                algorithmResult != null &&
                algorithmResult.DetectResults != null &&
                algorithmResult.DetectResults.Count > 0;
        }

        /// <summary>
        /// 按AI检测项自身颜色追加文本结果，避免整节点NG时所有文本都变红。
        /// </summary>
        /// <param name="displayResult">目标显示结果。</param>
        /// <param name="algorithmResult">源算法结果。</param>
        /// <param name="item">当前绘制项。</param>
        private static void AppendAlgorithmResultTexts(AlgorithmResult displayResult, AlgorithmResult algorithmResult, ResultOverlayDrawItem item)
        {
            if (displayResult == null || algorithmResult == null || item == null)
                return;

            bool usePrefix = !string.IsNullOrEmpty(item.TextPrefix);
            int addedCount = 0;
            if (algorithmResult.Texts != null)
            {
                foreach (ColorText textInfo in algorithmResult.Texts)
                {
                    if (textInfo == null || string.IsNullOrWhiteSpace(textInfo.Text))
                        continue;

                    string[] lines = textInfo.Text.Replace("\r\n", "\n").Split('\n');
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string displayText = usePrefix ? item.TextPrefix + line : line;
                        AddDisplayText(displayResult, new ColorText(displayText, textInfo.Color)
                        {
                            FontSize = Math.Max(1, item.FontSize),
                            Position = item.TextPosition,
                            CoordinateMode = item.TextCoordinateMode,
                            Margin = Math.Max(0, item.TextMargin),
                            Title = string.Empty
                        }, IsMissingPromptText(displayText));
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0 || algorithmResult.DetectResults == null)
                return;

            foreach (var pair in algorithmResult.DetectResults)
            {
                if (pair.Value == null)
                    continue;

                foreach (SingleDetectResult result in pair.Value)
                {
                    if (result == null)
                        continue;

                    string text = $"{result.Name}:{result.Value} {(result.IsOk ? "OK" : "NG")}";
                    string displayText = usePrefix ? item.TextPrefix + text : text;
                    AddDisplayText(displayResult, new ColorText(displayText, result.IsOk ? item.OkColor : item.NgColor)
                    {
                        FontSize = Math.Max(1, item.FontSize),
                        Position = item.TextPosition,
                        CoordinateMode = item.TextCoordinateMode,
                        Margin = Math.Max(0, item.TextMargin),
                        Title = string.Empty
                    }, IsMissingPromptText(displayText));
                }
            }
        }

        /// <summary>
        /// 添加显示文本；失败提示相同且来自不同坐标系时只保留一份，优先保留控件坐标系。
        /// </summary>
        private static void AddDisplayText(AlgorithmResult displayResult, ColorText text, bool suppressDuplicate)
        {
            if (displayResult == null || text == null || string.IsNullOrWhiteSpace(text.Text))
                return;

            if (displayResult.Texts == null)
                displayResult.Texts = new List<ColorText>();

            if (suppressDuplicate)
            {
                ColorText existing = displayResult.Texts.FirstOrDefault(item => IsSameDisplayText(item == null ? null : item.Text, text.Text));
                if (existing != null)
                {
                    if (existing.CoordinateMode != DisplayTextCoordinateMode.Control &&
                        text.CoordinateMode == DisplayTextCoordinateMode.Control)
                    {
                        existing.Color = text.Color;
                        existing.FontSize = text.FontSize;
                        existing.Position = text.Position;
                        existing.CoordinateMode = text.CoordinateMode;
                        existing.Margin = text.Margin;
                        existing.Title = text.Title;
                    }

                    return;
                }
            }

            displayResult.Texts.Add(text);
        }

        /// <summary>
        /// 判断文本是否是失败提示，避免同一个未查到结果在图像坐标和控件坐标各显示一次。
        /// </summary>
        private static bool IsMissingPromptText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                (text.IndexOf("未查到", StringComparison.Ordinal) >= 0 ||
                 text.IndexOf("未找到", StringComparison.Ordinal) >= 0);
        }

        /// <summary>
        /// 比较显示文本内容，忽略首尾空白和换行差异。
        /// </summary>
        private static bool IsSameDisplayText(string left, string right)
        {
            return string.Equals(NormalizeDisplayText(left), NormalizeDisplayText(right), StringComparison.Ordinal);
        }

        /// <summary>
        /// 归一化显示文本，避免同一提示因为换行格式不同无法去重。
        /// </summary>
        private static string NormalizeDisplayText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Trim();
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
        /// 从当前绘制项的来源结果自动解析OK/NG状态；来源没有判定字段时按OK处理。
        /// 来源节点完整结果优先于单个订阅值，确保多条件回写的JudgeOk能够立即控制颜色。
        /// </summary>
        /// <param name="sourceValue">绘制项订阅读取到的实际值。</param>
        /// <param name="sourceResult">绘制项来源节点的完整结果。</param>
        /// <returns>自动判定状态；没有任何判定信息时返回true。</returns>
        internal static bool ResolveAutomaticJudgeOk(object sourceValue, INodeResult sourceResult)
        {
            if (TryReadAutomaticJudgeOk(sourceResult, out bool sourceResultOk))
                return sourceResultOk;
            if (TryReadAutomaticJudgeOk(sourceValue, out bool sourceValueOk))
                return sourceValueOk;
            return true;
        }

        /// <summary>
        /// 只读取明确的布尔判定字段，不把普通数字或文本误当成OK/NG状态。
        /// </summary>
        /// <param name="source">待检查的来源对象。</param>
        /// <param name="judgeOk">读取到的判定状态。</param>
        /// <returns>找到明确判定状态时返回true。</returns>
        private static bool TryReadAutomaticJudgeOk(object source, out bool judgeOk)
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

            if (source is bool)
            {
                judgeOk = (bool)source;
                return true;
            }

            Func<object, bool?> reader = AutomaticJudgeReaderCache.GetOrAdd(
                source.GetType(),
                CreateAutomaticJudgeReader);
            bool? result = reader(source);
            if (!result.HasValue)
                return false;

            judgeOk = result.Value;
            return true;
        }

        /// <summary>
        /// 为一种来源结果类型创建可复用的明确布尔判定读取器。
        /// </summary>
        /// <param name="sourceType">来源结果实际类型。</param>
        /// <returns>读取到判定时返回布尔值，没有判定字段时返回null。</returns>
        private static Func<object, bool?> CreateAutomaticJudgeReader(Type sourceType)
        {
            string[] propertyNames = { "JudgeOk", "JudgmentOk", "DisplayOk", "ConditionResult", "IsAllOk", "IsOk" };
            PropertyInfo property = sourceType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(item =>
                {
                    if (!item.CanRead || item.GetIndexParameters().Length > 0 || item.PropertyType != typeof(bool))
                        return false;

                    string displayName = GetDisplayName(item);
                    return propertyNames.Contains(item.Name) ||
                        string.Equals(displayName, "判定OK", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(displayName, "条件结果", StringComparison.OrdinalIgnoreCase);
                });
            if (property != null)
                return source => (bool)property.GetValue(source, null);

            return source =>
            {
                AlgorithmResult algorithmResult = OverlayGeometryAdapterRegistry.TryExtractAlgorithmResult(source);
                return algorithmResult == null ? (bool?)null : algorithmResult.IsAllOk;
            };
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

        /// <summary>
        /// 为文本绘制项生成显示行；多目标结果的第一目标摘要会自动合并为全部目标的同名值数组。
        /// </summary>
        /// <param name="sourceResult">订阅来源节点的完整结果。</param>
        /// <param name="selectedResultText">用户选择的结果显示名或属性名。</param>
        /// <param name="selectedValue">普通订阅读取到的摘要值。</param>
        /// <returns>多目标值返回单行方括号数组；非多目标结果返回普通文本行。</returns>
        internal static List<string> BuildTextValueLines(
            object sourceResult,
            string selectedResultText,
            object selectedValue)
        {
            var texts = new List<string>();
            if (TryAddMultiTargetTextLines(texts, sourceResult, selectedResultText))
                return texts;

            AddTextFromValue(texts, selectedValue);
            return texts;
        }

        /// <summary>
        /// 把多目标结果Items中与摘要属性同名的字段按顺序合并为“[值1,值2,值3]”。
        /// </summary>
        /// <param name="texts">目标文本集合。</param>
        /// <param name="sourceResult">来源节点完整结果。</param>
        /// <param name="selectedResultText">已选择的结果显示名或属性名。</param>
        /// <returns>成功展开至少一个目标时返回true。</returns>
        private static bool TryAddMultiTargetTextLines(
            ICollection<string> texts,
            object sourceResult,
            string selectedResultText)
        {
            if (texts == null || sourceResult == null || string.IsNullOrWhiteSpace(selectedResultText))
                return false;

            Tuple<Type, string> cacheKey = Tuple.Create(
                sourceResult.GetType(),
                selectedResultText.Trim().ToUpperInvariant());
            MultiTargetTextAccessor accessor = MultiTargetTextAccessorCache.GetOrAdd(
                cacheKey,
                key => CreateMultiTargetTextAccessor(key.Item1, selectedResultText));
            if (!accessor.IsAvailable)
                return false;

            IEnumerable targetItems = accessor.ItemsProperty.GetValue(sourceResult, null) as IEnumerable;
            if (targetItems == null)
                return false;

            var targetValues = new List<string>();
            foreach (object targetItem in targetItems)
            {
                if (targetItem == null)
                    continue;

                if (!accessor.TargetProperty.DeclaringType.IsAssignableFrom(targetItem.GetType()))
                    return false;

                object targetValue = accessor.TargetProperty.GetValue(targetItem, null);
                targetValues.Add(Convert.ToString(targetValue));
            }

            if (targetValues.Count == 0)
                return false;

            texts.Add("[" + string.Join(",", targetValues) + "]");
            return true;
        }

        /// <summary>
        /// 创建多目标结果中“摘要属性→Items同名属性”的快速访问器。
        /// </summary>
        /// <param name="sourceResultType">来源节点完整结果类型。</param>
        /// <param name="selectedResultText">订阅结果显示名或属性名。</param>
        /// <returns>可用访问器；结构不符合多目标约定时返回不可用访问器。</returns>
        private static MultiTargetTextAccessor CreateMultiTargetTextAccessor(
            Type sourceResultType,
            string selectedResultText)
        {
            PropertyInfo selectedProperty = sourceResultType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(property =>
                    property.CanRead &&
                    (string.Equals(GetDisplayName(property), selectedResultText, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(property.Name, selectedResultText, StringComparison.OrdinalIgnoreCase)));
            PropertyInfo itemsProperty = sourceResultType.GetProperty(
                "Items",
                BindingFlags.Instance | BindingFlags.Public);
            SubscriptionOutputAttribute itemsOutput = itemsProperty == null
                ? null
                : itemsProperty.GetCustomAttribute<SubscriptionOutputAttribute>(true);
            if (selectedProperty == null || itemsProperty == null || !itemsProperty.CanRead ||
                itemsOutput == null || itemsOutput.Multiplicity != SubscriptionValueMultiplicity.MultiTarget)
            {
                return MultiTargetTextAccessor.Unavailable;
            }

            Type itemType = itemsProperty.PropertyType.IsGenericType
                ? itemsProperty.PropertyType.GetGenericArguments().FirstOrDefault()
                : null;
            PropertyInfo targetProperty = itemType == null
                ? null
                : itemType.GetProperty(
                    selectedProperty.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            return targetProperty == null || !targetProperty.CanRead
                ? MultiTargetTextAccessor.Unavailable
                : new MultiTargetTextAccessor(itemsProperty, targetProperty);
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

        /// <summary>
        /// 保存多目标Items集合属性与目标同名结果属性的缓存访问器。
        /// </summary>
        private sealed class MultiTargetTextAccessor
        {
            /// <summary>表示当前结果结构无法进行多目标同名字段展开。</summary>
            internal static readonly MultiTargetTextAccessor Unavailable =
                new MultiTargetTextAccessor(null, null);

            /// <summary>
            /// 初始化多目标文本访问器。
            /// </summary>
            /// <param name="itemsProperty">来源结果的Items属性。</param>
            /// <param name="targetProperty">单目标结果的同名属性。</param>
            internal MultiTargetTextAccessor(PropertyInfo itemsProperty, PropertyInfo targetProperty)
            {
                ItemsProperty = itemsProperty;
                TargetProperty = targetProperty;
            }

            /// <summary>获取来源结果的Items集合属性。</summary>
            internal PropertyInfo ItemsProperty { get; private set; }

            /// <summary>获取单目标结果的同名值属性。</summary>
            internal PropertyInfo TargetProperty { get; private set; }

            /// <summary>获取当前访问器是否可以执行多目标展开。</summary>
            internal bool IsAvailable
            {
                get { return ItemsProperty != null && TargetProperty != null; }
            }
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

    /// <summary>
    /// ROI结果绘制节点构建显示层时的性能诊断数据。
    /// </summary>
    internal sealed class ResultOverlayDrawPerformanceDiagnostics
    {
        /// <summary>读取上游图像订阅耗时。</summary>
        public long ImageSubscriptionMs { get; set; }
        /// <summary>获取干净图像引用耗时。</summary>
        public long GetCleanImageMs { get; set; }
        /// <summary>构建显示结果耗时。</summary>
        public long BuildDisplayResultMs { get; set; }
        /// <summary>构建输出图像对象耗时。</summary>
        public long BuildOutputImageMs { get; set; }
        /// <summary>总耗时。</summary>
        public long TotalMs { get; set; }
        /// <summary>启用的绘制项数量。</summary>
        public int EnabledItemCount { get; set; }
        /// <summary>文本绘制项数量。</summary>
        public int TextItemCount { get; set; }
        /// <summary>线段绘制项数量。</summary>
        public int LineItemCount { get; set; }
        /// <summary>矩形绘制项数量。</summary>
        public int RectangleItemCount { get; set; }
        /// <summary>区域绘制项数量。</summary>
        public int RegionItemCount { get; set; }
        /// <summary>自动ROI绘制项数量。</summary>
        public int RoiItemCount { get; set; }
        /// <summary>文本绘制项构建耗时。</summary>
        public long TextItemsMs { get; set; }
        /// <summary>线段绘制项构建耗时。</summary>
        public long LineItemsMs { get; set; }
        /// <summary>矩形绘制项构建耗时。</summary>
        public long RectangleItemsMs { get; set; }
        /// <summary>区域绘制项构建耗时。</summary>
        public long RegionItemsMs { get; set; }
        /// <summary>自动ROI绘制项构建耗时。</summary>
        public long RoiItemsMs { get; set; }
        /// <summary>输出矩形数量。</summary>
        public int OutputRectCount { get; private set; }
        /// <summary>输出NG矩形数量。</summary>
        public int OutputNgRectCount { get; private set; }
        /// <summary>输出线段数量。</summary>
        public int OutputLineCount { get; private set; }
        /// <summary>输出轮廓数量。</summary>
        public int OutputContourCount { get; private set; }
        /// <summary>输出文本数量。</summary>
        public int OutputTextCount { get; private set; }
        /// <summary>图像订阅源节点文本。</summary>
        public string ImageSourceNodeText { get; set; }
        /// <summary>输入图像摘要。</summary>
        public string InputImageText { get; set; }
        /// <summary>输出图像摘要。</summary>
        public string OutputImageText { get; set; }

        /// <summary>
        /// 捕获最终显示结果中的各类元素数量。
        /// </summary>
        /// <param name="displayResult">显示结果。</param>
        public void CaptureDisplayResultCounts(AlgorithmResult displayResult)
        {
            if (displayResult == null)
                return;

            OutputRectCount = displayResult.Rects == null ? 0 : displayResult.Rects.Count;
            OutputNgRectCount = displayResult.RectsNgMap == null ? 0 : displayResult.RectsNgMap.Values.Where(v => v != null).Sum(v => v.Count);
            OutputLineCount = displayResult.Lines == null ? 0 : displayResult.Lines.Count;
            OutputContourCount = displayResult.Contours == null ? 0 : displayResult.Contours.Count;
            OutputTextCount = displayResult.Texts == null ? 0 : displayResult.Texts.Count;
        }

        /// <summary>
        /// 判断ROI结果绘制是否需要输出慢诊断日志。
        /// </summary>
        /// <returns>存在慢耗时阶段时返回 true。</returns>
        public bool ShouldLogSlow()
        {
            return PerformanceSpikeDiagnostics.ShouldLog(
                PerformanceSpikeDiagnostics.CommonSlowMs,
                ImageSubscriptionMs,
                GetCleanImageMs,
                BuildDisplayResultMs,
                BuildOutputImageMs,
                TotalMs,
                TextItemsMs,
                LineItemsMs,
                RectangleItemsMs,
                RegionItemsMs,
                RoiItemsMs);
        }

        /// <summary>
        /// 生成便于现场日志查看的诊断文本。
        /// </summary>
        public string ToLogText()
        {
            return $"订阅图像={ImageSubscriptionMs}ms；取图引用={GetCleanImageMs}ms；绘制项构建={BuildDisplayResultMs}ms（含自动判定）；输出对象={BuildOutputImageMs}ms；总构建={TotalMs}ms；绘制项=启用{EnabledItemCount}/文本{TextItemCount}({TextItemsMs}ms)/自动ROI{RoiItemCount}({RoiItemsMs}ms)/旧线{LineItemCount}({LineItemsMs}ms)/旧矩形{RectangleItemCount}({RectangleItemsMs}ms)/旧区域{RegionItemCount}({RegionItemsMs}ms)；输出=矩形{OutputRectCount}/NG矩形{OutputNgRectCount}/线{OutputLineCount}/轮廓{OutputContourCount}/文本{OutputTextCount}";
        }
    }
}
