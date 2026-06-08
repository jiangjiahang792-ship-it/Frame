using Logger;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2
{
    /// <summary>
    /// ROI结果绘制2节点，保持原绘制逻辑并用多条布尔订阅规则统一决定绘制颜色。
    /// </summary>
    public class NodeResultOverlayDraw2 : NodeBase, INodeSubscriptionDependencyProvider
    {
        /// <summary>
        /// 初始化ROI结果绘制2节点。
        /// </summary>
        /// <param name="nodeId">节点ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeResultOverlayDraw2(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormResultOverlayDraw2();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultResultOverlayDraw2();
        }

        /// <summary>
        /// 获取绘制项和颜色规则隐式订阅的上游节点ID，供流程运行器等待依赖完成。
        /// </summary>
        /// <returns>订阅依赖节点ID集合。</returns>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds()
        {
            HashSet<int> nodeIds = new HashSet<int>();
            NodeParamResultOverlayDraw2 param = ParamForm == null ? null : ParamForm.Params as NodeParamResultOverlayDraw2;
            if (param == null)
                return nodeIds;

            AddNodeId(nodeIds, param.ImageText1);

            if (param.Items != null)
            {
                foreach (ResultOverlayDraw2Item item in param.Items)
                {
                    if (item == null || !item.Enabled)
                        continue;

                    bool needSource = item.ItemType != ResultOverlayDraw2ItemType.Text || !item.UseManualText;
                    if (needSource)
                        AddNodeId(nodeIds, item.SourceText1);
                }
            }

            if (param.ColorRules != null)
            {
                foreach (ResultOverlayDraw2ColorRule rule in param.ColorRules)
                {
                    if (rule != null && rule.Enabled)
                        AddNodeId(nodeIds, rule.SourceText1);
                }
            }

            nodeIds.Remove(ID);
            return nodeIds;
        }

        /// <summary>
        /// 执行节点运行，输出带显示层的图像和当前颜色判定结果。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <param name="showLog">是否写入成功日志。</param>
        /// <returns>节点运行返回值。</returns>
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

                NodeParamResultOverlayDraw2 param = (NodeParamResultOverlayDraw2)ParamForm.Params;
                OutputImage outputImage;
                AlgorithmResult displayResult;
                bool isOk;
                string diagnostics;
                ResultOverlayDraw2Builder.Build(this, param, out outputImage, out displayResult, out isOk, out diagnostics);

                NodeResultResultOverlayDraw2 nodeResult = (NodeResultResultOverlayDraw2)Result;
                nodeResult.OutputImage = outputImage;
                nodeResult.Result = displayResult;
                nodeResult.IsOk = isOk;
                nodeResult.RuleDiagnostics = diagnostics;

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

        /// <summary>
        /// 从订阅控件保存的“ID.节点名”文本中提取节点ID。
        /// </summary>
        /// <param name="nodeIds">需要填充的节点ID集合。</param>
        /// <param name="nodeText">订阅节点文本。</param>
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
    }

    /// <summary>
    /// ROI结果绘制2构建器，集中处理订阅读取、颜色判定和显示层生成。
    /// </summary>
    internal static class ResultOverlayDraw2Builder
    {
        /// <summary>
        /// 根据参数构建输出图像、显示结果和颜色判定。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="param">节点参数。</param>
        /// <param name="outputImage">输出图像。</param>
        /// <param name="displayResult">绘制显示结果。</param>
        /// <param name="isOk">颜色判定结果。</param>
        /// <param name="diagnostics">颜色规则诊断文本。</param>
        public static void Build(
            NodeBase owner,
            NodeParamResultOverlayDraw2 param,
            out OutputImage outputImage,
            out AlgorithmResult displayResult,
            out bool isOk,
            out string diagnostics)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (param == null)
                throw new Exception("ROI结果绘制2参数为空！");

            object imageValue = ReadSubscribedValue(owner, param.ImageText1, param.ImageText2, out NodeBase _);
            OutputImage inputImage = imageValue as OutputImage;
            if (inputImage == null)
                throw new Exception("订阅的图像类型不是 OutputImage！");

            isOk = ResolveColorState(owner, param, out diagnostics);
            Color drawColor = isOk ? param.OkColor : param.NgColor;
            Mat cleanImage = MeasurementNodeHelper.GetFirstMat(inputImage);
            displayResult = BuildDisplayResult(owner, param, drawColor);
            outputImage = new OutputImage
            {
                Bitmaps = new List<Mat> { cleanImage },
                Rectangles = inputImage.Rectangles == null ? new List<Rect>() : inputImage.Rectangles.ToList(),
                DisplayResult = displayResult
            };
        }

        /// <summary>
        /// 按订阅文本读取上游节点结果或结果属性。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="nodeText">订阅节点文本。</param>
        /// <param name="resultText">订阅结果显示名。</param>
        /// <param name="sourceNode">读取到的源节点。</param>
        /// <returns>读取到的订阅值。</returns>
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

        /// <summary>
        /// 解析多条颜色规则的最终OK/NG状态。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="param">节点参数。</param>
        /// <param name="diagnostics">诊断文本。</param>
        /// <returns>最终颜色判定结果。</returns>
        private static bool ResolveColorState(NodeBase owner, NodeParamResultOverlayDraw2 param, out string diagnostics)
        {
            List<ResultOverlayDraw2ColorRule> rules = param.ColorRules == null
                ? new List<ResultOverlayDraw2ColorRule>()
                : param.ColorRules.Where(rule => rule != null && rule.Enabled && !string.IsNullOrWhiteSpace(rule.SourceText1)).ToList();

            if (rules.Count == 0)
            {
                diagnostics = "未配置颜色判定规则，默认使用OK颜色。";
                return true;
            }

            int trueCount = 0;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < rules.Count; i++)
            {
                ResultOverlayDraw2ColorRule rule = rules[i];
                bool value;
                string ruleDiagnostic;
                if (!TryResolveRuleBoolean(owner, rule, out value, out ruleDiagnostic))
                    throw new Exception("颜色规则“" + rule.DisplayText + "”读取失败：" + ruleDiagnostic);

                if (value)
                    trueCount++;

                if (builder.Length > 0)
                    builder.AppendLine();
                builder.Append("规则").Append(i + 1).Append("：").Append(rule.DisplayText)
                    .Append(" = ").Append(value ? "OK" : "NG");
            }

            diagnostics = builder.ToString();
            if (param.RuleMode == ResultOverlayColorRuleMode.AnyTrue)
                return trueCount > 0;

            return trueCount == rules.Count;
        }

        /// <summary>
        /// 读取并转换单条颜色规则的布尔值。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="rule">颜色规则。</param>
        /// <param name="result">解析到的布尔值。</param>
        /// <param name="diagnostic">失败时的诊断文本。</param>
        /// <returns>成功解析返回 true。</returns>
        private static bool TryResolveRuleBoolean(NodeBase owner, ResultOverlayDraw2ColorRule rule, out bool result, out string diagnostic)
        {
            result = false;
            diagnostic = string.Empty;
            object value = ReadSubscribedValue(owner, rule.SourceText1, rule.SourceText2, out NodeBase sourceNode);

            bool wantsConditionItem = !IsOverallCondition(rule.ConditionName);
            if (TryResolveMultiConditionBoolean(sourceNode, value, rule.ConditionName, out result, out diagnostic))
                return true;

            if (wantsConditionItem)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "当前订阅不是多条件判定结果，不能选择单个条件项。"
                    : diagnostic;
                return false;
            }

            if (TryConvertToBoolean(value, out result))
                return true;

            if (TryReadBooleanMember(value, out result))
                return true;

            if (sourceNode != null && TryReadBooleanMember(sourceNode.Result, out result))
                return true;

            diagnostic = "订阅值无法转换为布尔类型。";
            return false;
        }

        /// <summary>
        /// 从多条件节点整体结果或明细条件中读取布尔值。
        /// </summary>
        /// <param name="sourceNode">源节点。</param>
        /// <param name="selectedValue">订阅属性读取到的值。</param>
        /// <param name="conditionName">条件项名称。</param>
        /// <param name="result">解析到的布尔值。</param>
        /// <param name="diagnostic">失败诊断文本。</param>
        /// <returns>成功解析返回 true。</returns>
        private static bool TryResolveMultiConditionBoolean(
            NodeBase sourceNode,
            object selectedValue,
            string conditionName,
            out bool result,
            out string diagnostic)
        {
            result = false;
            diagnostic = string.Empty;
            bool useOverall = IsOverallCondition(conditionName);

            NodeResultMultiCondition conditionResult = selectedValue as NodeResultMultiCondition;
            if (conditionResult == null && sourceNode != null)
                conditionResult = sourceNode.Result as NodeResultMultiCondition;

            if (conditionResult != null)
            {
                if (useOverall)
                {
                    result = conditionResult.ConditionResult;
                    return true;
                }

                return TryResolveConditionDetails(conditionResult.Details, conditionName, out result, out diagnostic);
            }

            NodeConditionEvaluation evaluation = selectedValue as NodeConditionEvaluation;
            if (evaluation != null)
            {
                result = evaluation.IsMatched;
                return true;
            }

            IEnumerable<NodeConditionEvaluation> details = selectedValue as IEnumerable<NodeConditionEvaluation>;
            if (details != null)
            {
                if (useOverall)
                {
                    List<NodeConditionEvaluation> list = details.ToList();
                    result = list.Count > 0 && list.All(item => item != null && item.IsMatched);
                    return true;
                }

                return TryResolveConditionDetails(details, conditionName, out result, out diagnostic);
            }

            diagnostic = "未找到多条件整体结果或条件明细。";
            return false;
        }

        /// <summary>
        /// 从多条件明细集合中按名称、序号或“条件N”读取指定条件结果。
        /// </summary>
        /// <param name="details">多条件明细集合。</param>
        /// <param name="conditionName">条件项名称。</param>
        /// <param name="result">解析到的布尔值。</param>
        /// <param name="diagnostic">失败诊断文本。</param>
        /// <returns>成功解析返回 true。</returns>
        private static bool TryResolveConditionDetails(
            IEnumerable<NodeConditionEvaluation> details,
            string conditionName,
            out bool result,
            out string diagnostic)
        {
            result = false;
            diagnostic = string.Empty;
            if (details == null)
            {
                diagnostic = "多条件明细为空。";
                return false;
            }

            string target = (conditionName ?? string.Empty).Trim();
            int index = 0;
            foreach (NodeConditionEvaluation detail in details)
            {
                string defaultName = "条件" + (index + 1);
                if (detail != null && IsConditionNameMatch(target, detail.Name, defaultName, index + 1))
                {
                    result = detail.IsMatched;
                    return true;
                }

                index++;
            }

            diagnostic = "多条件明细中找不到条件项“" + conditionName + "”。";
            return false;
        }

        /// <summary>
        /// 判断用户选择是否代表多条件整体结果。
        /// </summary>
        /// <param name="conditionName">条件名称。</param>
        /// <returns>整体结果返回 true。</returns>
        private static bool IsOverallCondition(string conditionName)
        {
            if (string.IsNullOrWhiteSpace(conditionName))
                return true;

            string text = conditionName.Trim();
            return string.Equals(text, "整体结果", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "全部", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "总结果", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断条件明细名称是否匹配用户选择。
        /// </summary>
        /// <param name="target">用户选择名称。</param>
        /// <param name="detailName">明细名称。</param>
        /// <param name="defaultName">默认条件名称。</param>
        /// <param name="oneBasedIndex">一基序号。</param>
        /// <returns>匹配返回 true。</returns>
        private static bool IsConditionNameMatch(string target, string detailName, string defaultName, int oneBasedIndex)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;

            return string.Equals(target, detailName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, defaultName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, oneBasedIndex.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将常见布尔、数值和OK/NG文本转换为布尔值。
        /// </summary>
        /// <param name="value">待转换值。</param>
        /// <param name="result">转换后的布尔值。</param>
        /// <returns>成功转换返回 true。</returns>
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
                return TryParseBooleanText(text, out result);

            Type valueType = value.GetType();
            if (valueType.IsPrimitive && value is IConvertible && !(value is char))
            {
                try
                {
                    double numeric = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    result = Math.Abs(numeric) > double.Epsilon;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// 将常见文本状态转换为布尔值。
        /// </summary>
        /// <param name="text">状态文本。</param>
        /// <param name="result">转换后的布尔值。</param>
        /// <returns>成功转换返回 true。</returns>
        private static bool TryParseBooleanText(string text, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.Trim();
            string lower = normalized.ToLowerInvariant();
            string[] trueTexts = { "true", "1", "ok", "pass", "yes", "y", "是", "真", "通过", "合格", "良品", "正常" };
            string[] falseTexts = { "false", "0", "ng", "fail", "no", "n", "否", "假", "不通过", "不合格", "不良", "异常" };

            if (trueTexts.Contains(lower) || trueTexts.Contains(normalized))
            {
                result = true;
                return true;
            }

            if (falseTexts.Contains(lower) || falseTexts.Contains(normalized))
            {
                result = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从对象常见布尔成员中读取状态。
        /// </summary>
        /// <param name="source">来源对象。</param>
        /// <param name="result">读取到的布尔值。</param>
        /// <returns>成功读取返回 true。</returns>
        private static bool TryReadBooleanMember(object source, out bool result)
        {
            result = false;
            if (source == null)
                return false;

            string[] memberNames = { "ConditionResult", "IsMatched", "IsAllOk", "IsOk", "OK", "Ok", "Result", "Value" };
            Type sourceType = source.GetType();
            foreach (PropertyInfo property in sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                string displayName = GetDisplayName(property);
                if (!memberNames.Contains(property.Name) &&
                    !string.Equals(displayName, "条件结果", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(displayName, "判定结果", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(displayName, "颜色判定", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object value = property.GetValue(source, null);
                if (TryConvertToBoolean(value, out result))
                    return true;
            }

            foreach (FieldInfo field in sourceType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!memberNames.Contains(field.Name))
                    continue;

                object value = field.GetValue(source);
                if (TryConvertToBoolean(value, out result))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试读取单个绘制项的订阅值。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="item">绘制项。</param>
        /// <param name="value">读取到的值。</param>
        /// <param name="sourceNode">源节点。</param>
        /// <returns>成功读取返回 true。</returns>
        private static bool TryReadDrawItemValue(NodeBase owner, ResultOverlayDraw2Item item, out object value, out NodeBase sourceNode)
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
        /// 生成所有绘制项的显示结果。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="param">节点参数。</param>
        /// <param name="drawColor">最终绘制颜色。</param>
        /// <returns>显示结果。</returns>
        private static AlgorithmResult BuildDisplayResult(NodeBase owner, NodeParamResultOverlayDraw2 param, Color drawColor)
        {
            AlgorithmResult displayResult = new AlgorithmResult();
            if (param.Items == null)
                return displayResult;

            foreach (ResultOverlayDraw2Item item in param.Items.Where(i => i != null && i.Enabled))
            {
                switch (item.ItemType)
                {
                    case ResultOverlayDraw2ItemType.Text:
                        AppendText(owner, displayResult, item, drawColor);
                        break;
                    case ResultOverlayDraw2ItemType.Line:
                        AppendLines(owner, displayResult, item, drawColor);
                        break;
                    case ResultOverlayDraw2ItemType.Rectangle:
                        AppendRectangles(owner, displayResult, item, drawColor);
                        break;
                    case ResultOverlayDraw2ItemType.Region:
                        AppendRegions(owner, displayResult, item, drawColor);
                        break;
                }
            }

            return displayResult;
        }

        /// <summary>
        /// 追加文本绘制结果。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="item">绘制项。</param>
        /// <param name="drawColor">绘制颜色。</param>
        private static void AppendText(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDraw2Item item, Color drawColor)
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

                AlgorithmResult sourceAlgorithmResult = TryGetAlgorithmResult(sourceValue) ?? TryGetAlgorithmResult(sourceNode == null ? null : sourceNode.Result);
                if (ShouldPreserveAlgorithmResultElementColor(sourceAlgorithmResult))
                {
                    AppendAlgorithmResultTexts(displayResult, sourceAlgorithmResult, item);
                    return;
                }

                AddTextFromValue(texts, sourceValue);
                if (texts.Count == 0 && sourceNode != null)
                    AddTextFromValue(texts, sourceNode.Result);

                usePrefix = !string.IsNullOrEmpty(item.TextPrefix);
            }

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

        /// <summary>
        /// 追加线段绘制结果。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="item">绘制项。</param>
        /// <param name="drawColor">绘制颜色。</param>
        private static void AppendLines(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDraw2Item item, Color drawColor)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
                return;

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
            }
        }

        /// <summary>
        /// 追加区域或轮廓绘制结果。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="item">绘制项。</param>
        /// <param name="drawColor">绘制颜色。</param>
        private static void AppendRegions(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDraw2Item item, Color drawColor)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
                return;

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
            }
        }

        /// <summary>
        /// 追加矩形绘制结果。
        /// </summary>
        /// <param name="owner">当前节点。</param>
        /// <param name="displayResult">显示结果。</param>
        /// <param name="item">绘制项。</param>
        /// <param name="drawColor">绘制颜色。</param>
        private static void AppendRectangles(NodeBase owner, AlgorithmResult displayResult, ResultOverlayDraw2Item item, Color drawColor)
        {
            if (!TryReadDrawItemValue(owner, item, out object value, out NodeBase sourceNode))
                return;

            AlgorithmResult algorithmResult = TryGetAlgorithmResult(value) ?? TryGetAlgorithmResult(sourceNode == null ? null : sourceNode.Result);
            if (algorithmResult == null)
                return;

            bool preserveElementColor = ShouldPreserveAlgorithmResultElementColor(algorithmResult);
            foreach (ColorRotatedRect rect in algorithmResult.Rects)
            {
                displayResult.Rects.Add(new ColorRotatedRect(rect.RotatedRect)
                {
                    Color = preserveElementColor ? rect.Color : drawColor,
                    LineWidth = Math.Max(1, item.LineWidth)
                });
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
                }
            }
        }

        /// <summary>
        /// 判断算法结果是否应该保留检测项内部颜色，避免AI多检测项被整体颜色规则覆盖。
        /// </summary>
        /// <param name="algorithmResult">源算法结果。</param>
        /// <returns>需要保留源元素颜色返回true。</returns>
        private static bool ShouldPreserveAlgorithmResultElementColor(AlgorithmResult algorithmResult)
        {
            return algorithmResult != null &&
                algorithmResult.DetectResults != null &&
                algorithmResult.DetectResults.Count > 0;
        }

        /// <summary>
        /// 按AI检测项自身颜色追加文本结果，避免整节点NG时所有文本都变红。
        /// </summary>
        /// <param name="displayResult">目标显示结果。</param>
        /// <param name="algorithmResult">源算法结果。</param>
        /// <param name="item">当前绘制项。</param>
        private static void AppendAlgorithmResultTexts(
            AlgorithmResult displayResult,
            AlgorithmResult algorithmResult,
            ResultOverlayDraw2Item item)
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

                        displayResult.Texts.Add(new ColorText(usePrefix ? item.TextPrefix + line : line, textInfo.Color)
                        {
                            FontSize = Math.Max(1, item.FontSize),
                            Position = item.TextPosition,
                            Margin = Math.Max(0, item.TextMargin),
                            Title = string.Empty
                        });
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
                    displayResult.Texts.Add(new ColorText(usePrefix ? item.TextPrefix + text : text, result.IsOk ? Color.Lime : Color.Red)
                    {
                        FontSize = Math.Max(1, item.FontSize),
                        Position = item.TextPosition,
                        Margin = Math.Max(0, item.TextMargin),
                        Title = string.Empty
                    });
                }
            }
        }

        /// <summary>
        /// 从任意值中提取可显示文本。
        /// </summary>
        /// <param name="texts">文本集合。</param>
        /// <param name="value">来源值。</param>
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
        /// 按换行拆分手动文本。
        /// </summary>
        /// <param name="texts">文本集合。</param>
        /// <param name="text">手动文本。</param>
        private static void AddManualTextLines(List<string> texts, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            foreach (string line in lines)
                if (!string.IsNullOrWhiteSpace(line))
                    texts.Add(line.Trim());
        }

        /// <summary>
        /// 尝试从值或结果对象中读取算法绘制结果。
        /// </summary>
        /// <param name="source">来源对象。</param>
        /// <returns>算法绘制结果，读取失败返回 null。</returns>
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
        /// 获取节点订阅文本。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <returns>订阅文本。</returns>
        private static string GetNodeText(NodeBase node)
        {
            return $"{node.ID}.{node.NodeName}";
        }

        /// <summary>
        /// 获取属性显示名称。
        /// </summary>
        /// <param name="property">属性信息。</param>
        /// <returns>显示名称。</returns>
        private static string GetDisplayName(PropertyInfo property)
        {
            DisplayNameAttribute attribute = property.GetCustomAttribute<DisplayNameAttribute>();
            return attribute == null ? property.Name : attribute.DisplayName;
        }
    }
}
