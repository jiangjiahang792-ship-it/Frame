using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>超声波三类解析与实际参数窗体的离线验证，不加载真实模型或连接设备。</summary>
internal static class UltrasoundSideTests
{
    /// <summary>失败时提供对应的业务场景。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    /// <summary>读取实际控件，验证设计器与参数保存。</summary>
    private static T Field<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }

    /// <summary>加载随程序发布的中文检测项模板。</summary>
    private static NodeParamTDAI Parameter(string name)
    {
        Solution.Instance.DetectItemDic[name] = JsonConvert.DeserializeObject<List<DetectItemInfo>>(
            File.ReadAllText("Template/TDAI/超声波焊接侧面三类检测项.json"));
        return new NodeParamTDAI { CurDetectItemName = name, DetectItemName1 = name, IsFixed = true,
            NodeName = name, ModelName = ModelName.超声波焊接侧面三类模型, StudyNum = 2, StudyPercentage = 10, Scale = 1 };
    }

    /// <summary>生成顺序打乱的检测框，验证左右位置不依赖模型输出顺序。</summary>
    private static List<DetResult> Detections()
    {
        return new List<DetResult>
        {
            new DetResult { ClassId = 0, Box = new Rect(200, 20, 40, 12) },
            new DetResult { ClassId = 1, Box = new Rect(100, 20, 60, 20) },
            new DetResult { ClassId = 0, Box = new Rect(10, 20, 30, 10) },
            new DetResult { ClassId = 2, Box = new Rect(80, 60, 6, 4) },
            new DetResult { ClassId = 2, Box = new Rect(90, 60, 6, 4) }
        };
    }

    /// <summary>直接调用项目真实解析器。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI parameter, List<DetResult> detections)
    {
        var result = new NodeResultTDAI();
        Ultrasound_Side_3Class.Parse(detections, 0, parameter, ref result);
        return result;
    }

    /// <summary>读取兼容旧内部键的单项数值。</summary>
    private static float Value(NodeResultTDAI result, string name)
    {
        return float.Parse(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName(name)][0].Value);
    }

    /// <summary>验证尺寸、数量、缺失更新、禁用项、混合颜色和多目标明细。</summary>
    private static void VerifyParsing()
    {
        var parameter = Parameter("解析");
        var detections = Detections();
        var result = Parse(parameter, detections);
        Check(result.DetectItemCount == 7, "七个检测项未完整输出。");
        Check(Value(result, "左线芯长度") == 30 && Value(result, "左线芯宽度") == 10, "左侧尺寸错误。");
        Check(Value(result, "右线芯长度") == 40 && Value(result, "右线芯宽度") == 12, "右侧尺寸错误。");
        Check(Value(result, "焊接区域长度") == 60 && Value(result, "焊接区域宽度") == 20, "焊接尺寸错误。");
        Check(Value(result, "飞丝") == 2 && !result.IsOk, "飞丝计数或整体NG错误。");
        Check(result.AlgorithmResult.Rects.Count == 5 && result.AlgorithmResult.Rects.Count(r => r.Color == Color.Red) == 2,
            "矩形未去重或飞丝NG污染正常框。");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.StartsWith("飞丝:")), "飞丝未按中文显示。");
        parameter.NeedConvert = true; parameter.Scale = 0.1f;
        result = Parse(parameter, detections);
        Check(Value(result, "左线芯长度") == 3 && Value(result, "飞丝") == 2, "尺寸换算影响了飞丝数量。");
        result = Parse(parameter, null);
        Check(result.DetectItemCount == 7 && Value(result, "右线芯长度") == 0 && !result.IsOk, "空结果错误。");
        Check(Solution.Instance.DetectItemDic["解析"].All(i => i.CurValue == "0"), "未检出保留旧值。");
        parameter.NeedConvert = false;
        var items = Solution.Instance.DetectItemDic["解析"];
        items.First(i => i.Name == "右线芯长度").MinValue = "0";
        result = Parse(parameter, new List<DetResult> { detections[0] });
        Check(Value(result, "左线芯长度") == 40 && result.AlgorithmResult.DetectResults["右线芯长度"][0].IsOk,
            "单线芯约定或缺失0的上下限判定错误。");
        items.First(i => i.Name == "右线芯长度").Enable = false;
        detections.Add(new DetResult { ClassId = 1, Box = new Rect(300, 20, 70, 22) });
        result = Parse(parameter, detections);
        Check(result.DetectItemCount == 6 && result.AlgorithmResult.DetectResults["焊接区域长度"].Count == 2, "禁用项或多焊接区域错误。");
        items.First(i => i.Name == "左线芯宽度").MaxValue = "5";
        result = Parse(parameter, detections);
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 30).Color == Color.Red,
            "同框宽度NG没有合并到矩形颜色。");
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 40).Color == Color.Green,
            "左侧NG污染了右侧矩形颜色。");
        parameter.Scale = float.NaN; parameter.NeedConvert = true;
        bool rejected = false;
        try { Parse(parameter, detections); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "非法换算比例未拒绝。");
    }

    /// <summary>验证学习完成、节点隔离、重复学习、缺失尺寸及配置切换。</summary>
    private static void VerifyStudy()
    {
        var first = Parameter("学习一"); var second = Parameter("学习二");
        first.IsAutoStudy = second.IsAutoStudy = true;
        Parse(first, Detections()); Parse(second, Detections());
        Check(first.IsAutoStudy && second.IsAutoStudy, "不同节点串用了学习次数。");
        Parse(first, Detections());
        Check(!first.IsAutoStudy && second.IsAutoStudy, "学习完成状态错误。");
        var learned = Solution.Instance.DetectItemDic["学习一"];
        Check(learned.First(i => i.Name == "左线芯长度").MinValue == "27.00", "尺寸学习下限错误。");
        Check(learned.First(i => i.Name == "DetectItem.FlyingWire").MaxValue == "2", "飞丝学习数量错误。");
        first.IsAutoStudy = true; first.StudyNum = 1;
        var changed = Detections(); changed[2] = new DetResult { ClassId = 0, Box = new Rect(10, 20, 50, 10) };
        Parse(first, changed);
        Check(learned.First(i => i.Name == "左线芯长度").MinValue == "45.00", "新学习会话复用了旧样本。");
        var missing = Parameter("空学习"); missing.IsAutoStudy = true; missing.StudyNum = 1;
        Parse(missing, null);
        Check(Solution.Instance.DetectItemDic["空学习"].First(i => i.Name == "左线芯长度").MinValue == "1",
            "缺失尺寸污染了学习范围。");
        Parameter("切换配置"); second.CurDetectItemName = "切换配置";
        Parse(second, changed); Check(second.IsAutoStudy, "切换配置后未重置学习次数。");
        Parse(second, changed); Check(!second.IsAutoStudy, "新配置学习未完成。");
    }

    /// <summary>验证实际设计器选项、参数保存、序列化恢复和中文展示截图。</summary>
    private static void VerifyForm()
    {
        var parameter = Parameter("界面模板");
        var owner = new NodeBase(1, "超声波验证", new TDJS_Vision.Process("离线验证"), NodeType.AITD);
        using (var form = new ParamFormTDAI())
        {
            form.SetNodeBelong(owner);
            var combo = Field<ComboBox>(form, "comboBoxModelName");
            Check(combo.Items.Contains(parameter.ModelName.ToString()), "设计器缺少超声波选项。");
            combo.SelectedItem = parameter.ModelName.ToString();
            Field<RadioButton>(form, "radioButton1").Checked = true;
            var configs = Field<ComboBox>(form, "comboBoxDetectConfig1");
            configs.Items.Add("界面模板"); configs.SelectedItem = "界面模板";
            Field<TextBox>(form, "textBoxConfigPath").Text = "离线验证配置";
            Field<TextBox>(form, "textBox_StudyNum").Text = "2";
            Field<TextBox>(form, "textBox_studyPercentage").Text = "10";
            Field<TextBox>(form, "textBox_Scale").Text = "1";
            var subscription = Field<object>(form, "nodeSubscription1");
            subscription.GetType().GetMethod("SetText").Invoke(subscription, new object[] { "1.图像源", "图像" });
            // 不存在的模型路径在托管校验阶段失败，不触发原生模型加载。
            typeof(ParamFormTDAI).GetField("aIInputInfo", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(form, new AIInputInfo { ModelInfo = new ModelInfo { ModelType = ModelType.DET, ModelPath = "不存在的离线模型.onnx" } });
            Check((bool)typeof(ParamFormTDAI).GetMethod("SaveParams", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null),
                "实际保存入口未通过。");
            var saved = (NodeParamTDAI)form.Params;
            saved.ModelLoadTask.Wait();
            Check(saved.ModelName == parameter.ModelName && saved.DetectItemName1 == "界面模板", "保存的模型或模板错误。");
            form.Params = JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(saved));
            var gate = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            Check(combo.Text == parameter.ModelName.ToString(), "序列化恢复丢失超声波选择。");
            form.Show(); Application.DoEvents(); form.PerformLayout();
            Check(TextRenderer.MeasureText(combo.Text, combo.Font).Width <= combo.ClientSize.Width - SystemInformation.VerticalScrollBarWidth, "模型名称在默认窗体中被截断。");
            using (var screenshot = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(screenshot, new Rectangle(0, 0, form.Width, form.Height));
                screenshot.Save("Ultrasound-parameters.png");
            }
            Check((int)ModelName.多端子模型 == 4 && (int)ModelName.超声波焊接侧面三类模型 == 5, "破坏了旧方案枚举值。");
        }
    }

    /// <summary>执行全部离线场景。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyParsing(); VerifyStudy(); VerifyForm();
            Console.WriteLine("超声波解析、学习隔离、中文模板、实际参数保存恢复及窗体截图验证通过。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
