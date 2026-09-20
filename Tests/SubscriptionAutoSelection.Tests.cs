using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._3_Detection.ContourMatch;
using TDJS_Vision.Node._4_Measurement.CaliperLine;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._4_Measurement.PositionCorrection;

/// <summary>验证真实节点连线、空参数恢复及手选保护，不启动设备或运行现场流程。</summary>
internal static class SubscriptionAutoSelectionTests
{
    /// <summary>累计通过的行为断言数。</summary>
    private static int _checks;
    /// <summary>读取真实控件的实例字段标志。</summary>
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>检查预期行为，失败时立即终止。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++;
    }

    /// <summary>读取 Designer 中声明的实际订阅控件。</summary>
    private static T Field<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    }

    /// <summary>通过生产结果属性安装不含设备的上游输出。</summary>
    private static void SetResult(NodeBase node, INodeResult result)
    {
        typeof(NodeBase).GetProperty("Result").SetValue(node, result);
    }

    /// <summary>按真实画布添加连线的方式发出流程通知。</summary>
    private static ProcessConnection Connect(TDJS_Vision.Process process, NodeBase from, NodeBase to)
    {
        var edge = new ProcessConnection { FromNodeId = from.ID, ToNodeId = to.ID };
        process.Connections.Add(edge);
        process.NotifyConnectionsChanged();
        return edge;
    }

    /// <summary>验证实际订阅来源和结果都已就绪。</summary>
    private static void Selected(NodeSubscription input, NodeBase expected, string message)
    {
        Check(ReferenceEquals(input.GetSelectedNode(), expected) && !string.IsNullOrWhiteSpace(input.GetText2()), message);
    }

    /// <summary>运行 STA 窗体及配置测试，确保所有临时窗体和节点释放。</summary>
    [STAThread]
    private static int Main()
    {
        var process = new TDJS_Vision.Process("自动订阅回归");
        var nodes = new List<NodeBase>();
        try
        {
            Application.EnableVisualStyles();
            var source = new NodeBase(1, "图像源", process, NodeType.ImageSource);
            SetResult(source, new NodeResultImageCrop()); nodes.Add(source); process.Nodes.Add(source);
            var crop = new NodeImageCrop(90, "图像裁切", process, NodeType.ImageCrop);
            nodes.Add(crop); process.Nodes.Add(crop);
            var caliper = new NodeCaliperLine(3, "卡尺找线", process, NodeType.CaliperLine);
            nodes.Add(caliper); process.Nodes.Add(caliper);
            var contour = new NodeContourMatch(4, "轮廓模板匹配", process, NodeType.ContourMatch);
            nodes.Add(contour); process.Nodes.Add(contour);
            var image = Field<NodeSubscription>(caliper.ParamForm, "nodeSubscription1");
            var correction = Field<NodeSubscription>(caliper.ParamForm, "nodeSubscriptionPositionCorrection");
            var contourImage = Field<NodeSubscription>(contour.ParamForm, "imageSubscription");
            Check(string.IsNullOrEmpty(image.GetText1()) && string.IsNullOrEmpty(contourImage.GetText1()), "未连线时错误选择了非上游节点。");
            Connect(process, source, crop);
            var cropCaliper = Connect(process, crop, caliper);
            var cropContour = Connect(process, crop, contour);
            Selected(image, crop, "图像源→裁切→卡尺没有选择最近的裁切输出。");
            Selected(contourImage, crop, "未打开轮廓参数窗口时没有自动订阅裁切图像。");
            var saved = (NodeParamContourMatch)contour.ParamForm.Params;
            Check(saved.SourceNodeId == crop.ID && saved.Text2 == "输出图像", "轮廓自动订阅没有立即写入运行参数。");
            Check(ReferenceEquals(image.GetValue<OutputImage>(), ((NodeResultImageCrop)crop.Result).OutputImage), "卡尺实际取值不是裁切输出。");
            var resolve = contour.ParamForm.GetType().GetMethod("ResolveInput", Flags);
            Check(ReferenceEquals(resolve.Invoke(contour.ParamForm, new object[] { saved }), image.GetValue<OutputImage>()), "轮廓生产取值未使用自动配置。");
            contour.ParamForm.SetParam2Form();
            Selected(contourImage, crop, "恢复窗口参数清空了轮廓自动订阅。");
            contour.ParamForm.Params = new NodeParamContourMatch(); contour.ParamForm.SetParam2Form();
            Check(((NodeParamContourMatch)contour.ParamForm.Params).SourceNodeId == crop.ID, "空方案参数恢复没有回填自动图像来源。");
            caliper.ParamForm.Params = new NodeParamCaliperLine(); caliper.ParamForm.SetParam2Form();
            Selected(image, crop, "卡尺恢复空参数后丢失自动选择。");
            Check(string.IsNullOrEmpty(correction.GetText1()), "没有兼容位置修正输出时错误选择了图像节点。");

            // 插入不输出图像的节点，图像继续向前找，位置修正留在最近一层。
            var position = new NodeBase(50, "位置修正", process, NodeType.PositionCorrection);
            SetResult(position, new NodeResultPositionCorrection()); nodes.Add(position); process.Nodes.Add(position);
            process.Connections.Remove(cropCaliper); process.Connections.Remove(cropContour);
            Connect(process, crop, position); Connect(process, position, caliper); Connect(process, position, contour);
            Selected(image, crop, "中间节点没有图像时未继续向前查找。");
            Selected(correction, position, "位置修正没有自动按实际列表类型选择上游。");
            Check(correction.GetText2() == "位置修正信息列表", "位置修正错误选择了旧版单值输出。");
            Check(!((NodeParamCaliperLine)caliper.ParamForm.Params).UsePositionCorrection, "自动填充错误开启了可选位置修正功能。");

            using (var number = new NodeSubscription())
            using (var boolean = new NodeSubscription())
            using (var text = new NodeSubscription())
            using (var nodeOnly = new NodeSubscription())
            {
                number.SetExpectedValueType<double>(); number.Init(caliper);
                boolean.SetExpectedValueType<bool>(); boolean.Init(caliper);
                text.SetExpectedValueType<string>(); text.Init(caliper);
                nodeOnly.HideText2(); nodeOnly.Init(caliper);
                Selected(number, position, "数值输入没有复用公共选择策略。");
                Selected(boolean, position, "布尔输入没有复用公共选择策略。");
                Check(string.IsNullOrEmpty(text.GetText1()), "没有文本输出时错误建立了订阅。");
                Check(ReferenceEquals(nodeOnly.GetSelectedNode(), position), "仅节点选择没有采用最近上游。");

                // 模拟将来新增节点，只声明标准输出即可被已有订阅识别。
                var future = new NodeBase(8, "新增业务节点", process, NodeType.SleepTool);
                SetResult(future, new FutureResult()); nodes.Add(future); process.Nodes.Add(future);
                Connect(process, position, future); Connect(process, future, caliper);
                Selected(text, future, "新增节点声明输出后未自动接入文本订阅。");
                Check(text.GetValue<string>() == "测试文本", "新增节点的实际文本取值错误。");
                SetResult(future, new NodeResultImageCrop()); future.NotifyOutputDefinitionChanged();
                Check(string.IsNullOrEmpty(text.GetText1()), "输出类型变化后仍保留失效的自动订阅。");
                // 同为直接上游，编号较小的未来节点提供图像；不受流程列表顺序影响。
                Selected(image, future, "新增更近的图像输出没有替换自动选择。");
            }

            // 显式选择旧图像源后，再连线也不覆盖用户选择；原路断开后保留待恢复路径。
            var nodeCombo = Field<ComboBox>(image, "comboBox1");
            nodeCombo.SelectedItem = "1.图像源";
            process.NotifyConnectionsChanged(); Selected(image, source, "连线刷新覆盖了用户手动选择。");
            var originalEdges = process.Connections.ToList();
            process.Connections.Clear(); process.NotifyConnectionsChanged();
            Check(image.GetText1() == "1.图像源", "临时断线丢失了明确订阅。");
            Check(string.IsNullOrEmpty(contourImage.GetText1()), "自动来源断开后仍保留无效选择。");
            foreach (var edge in originalEdges) process.Connections.Add(edge);
            process.NotifyConnectionsChanged(); Selected(image, source, "手选来源重新连线后没有恢复。");
            Selected(contourImage, crop, "轮廓自动来源断线重连后没有恢复。");
            image.SetText("90.图像裁切", "已删除的输出"); process.NotifyConnectionsChanged();
            Check(image.GetText2() == "已删除的输出", "旧方案失效输出被静默替换。");
            image.ClearText(); process.NotifyConnectionsChanged(); image.SetText("", "");
            Check(string.IsNullOrEmpty(image.GetText1()), "用户主动清空后被立即重新填充。");
            var roundTrip = JsonConvert.DeserializeObject<NodeParamContourMatch>(JsonConvert.SerializeObject(contour.ParamForm.Params));
            contour.ParamForm.Params = roundTrip; contour.ParamForm.SetParam2Form();
            Check(((NodeParamContourMatch)contour.ParamForm.Params).SourceNodeId == crop.ID, "轮廓订阅保存加载不一致。");
            // 环路不应让节点把自身列为来源，也不能无限遍历。
            Connect(process, caliper, source);
            var ordered = NearestSubscriptionSourceSelector.Instance.GetUpstreamNodes(caliper);
            Check(!ordered.Contains(caliper) && ordered.Select(node => node.ID).Distinct().Count() == ordered.Count, "循环连线未正确去重。");
            Console.WriteLine("自动订阅真实节点验证通过，断言数：" + _checks);
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            foreach (var node in nodes) { (node.ParamForm as IDisposable)?.Dispose(); node.Dispose(); }
        }
    }

    /// <summary>模拟新节点独立声明文本输出，无须修改公共订阅代码。</summary>
    private sealed class FutureResult : INodeResult
    {
        /// <summary>节点耗时。</summary>
        public int RunTime { get; set; }
        /// <summary>供文本输入订阅的业务值。</summary>
        [SubscriptionOutput, DisplayName("业务文本")]
        public string Text { get { return "测试文本"; } }
    }
}
