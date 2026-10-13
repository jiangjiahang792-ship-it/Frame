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
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>AST工位3真实解析及窗体离线回归，不加载现场模型。</summary>
internal static class ASTStation3Tests
{
    /// <summary>已通过的断言数量。</summary>
    private static int _checks;
    /// <summary>共用解析器的六个检测项及输出顺序。</summary>
    private static readonly string[] Names = { "露线芯宽度", "露线芯高度", "压接区宽度", "压接区域高度", "飞丝", "压接不良" };
    /// <summary>验证业务行为，失败时输出场景。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++;
    }
    /// <summary>创建独立检测配置，解析测试使用宽松范围，避免阈值干扰数值断言。</summary>
    private static NodeParamTDAI Parameter(string name)
    {
        Solution.Instance.DetectItemDic[name] = Names.Select(n => new DetectItemInfo {
            Name = n, Enable = true, MinValue = "0", MaxValue = "99999", IsCountItem = n == "飞丝" || n == "压接不良", CurValue = "0" }).ToList();
        return new NodeParamTDAI { CurDetectItemName = name, DetectItemName1 = name, IsFixed = true,
            NodeName = name, StudyNum = 2, StudyPercentage = 10, Scale = 1 };
    }
    /// <summary>模拟一组工位3检出：0露线芯、1压接区域、2飞丝。</summary>
    private static List<DetResult> Detections()
    {
        return new List<DetResult> {
            new DetResult { ClassId = 0, Box = new Rect(100, 120, 18, 27) },
            new DetResult { ClassId = 1, Box = new Rect(160, 130, 31, 22) },
            new DetResult { ClassId = 2, Box = new Rect(210, 140, 3, 4) } };
    }
    /// <summary>通过实际程序集调用解析器，旧版本缺少模型时给出明确失败。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI parameter, List<DetResult> detections)
    {
        var type = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Node._3_Detection.TDAI.Parse.ASTStation3Parse");
        Check(type != null, "目标分类尚未接入AST工位3解析器。");
        object[] args = { detections, detections == null ? 0 : detections.Count, parameter,
            new TDJS_Vision.Process("离线验证"), new NodeResultTDAI() };
        type.GetMethod("Parse").Invoke(null, args);
        return (NodeResultTDAI)args[4];
    }
    /// <summary>读取实际尺寸或数量值。</summary>
    private static float Value(NodeResultTDAI result, string name)
    {
        return float.Parse(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName(name)][0].Value);
    }
    /// <summary>覆盖类别映射、飞丝数量、缺失清零、换算、禁用和颜色合并。</summary>
    private static void VerifyParsing()
    {
        var parameter = Parameter("解析");
        var result = Parse(parameter, Detections());
        float[] expected = { 18, 27, 31, 22, 1, 0 };
        Check(result.DetectItemCount == 6 && result.IsOk, "六项结果不完整或判定错误。");
        Check(result.AlgorithmResult.Texts.Count == result.DetectItemCount,
            "画面应只包含检测项文字，不应追加当前产品判定结果。");
        for (int i = 0; i < Names.Length; i++) Check(Value(result, Names[i]) == expected[i], Names[i] + "数值错误。");
        Check(result.AlgorithmResult.Rects.Count == 3, "检测框未绘制或去重错误。");
        Check(result.AlgorithmResult.Lines.Count == 20 && result.AlgorithmResult.Lines.All(l => l.Color == Color.Yellow && !l.ShowCenterCross), "工位3尺寸箭头缺失或错误地绘制了飞丝长度。");
        Check(Math.Abs(result.AlgorithmResult.Lines[0].P2.X - result.AlgorithmResult.Lines[0].P1.X) == 18, "宽度箭头方向或长度错误。");
        Check(Math.Abs(result.AlgorithmResult.Lines[5].P2.Y - result.AlgorithmResult.Lines[5].P1.Y) == 27, "高度箭头方向或长度错误。");
        Check(result.AlgorithmResult.DetectResults.ContainsKey("DetectItem.FlyingWire"), "飞丝未兼容既有存储键。");
        parameter.NeedConvert = true; parameter.Scale = 0.1f;
        result = Parse(parameter, Detections());
        Check(Math.Abs(Value(result, "露线芯宽度") - 1.8f) < 0.001f, "露线芯宽度换算错误。");
        Check(Value(result, "飞丝") == 1, "数量型飞丝不应做物理换算。");
        parameter.NeedConvert = false;
        var detections = Detections();
        detections.RemoveAll(d => d.ClassId == 0);
        result = Parse(parameter, detections);
        Check(Value(result, "露线芯宽度") == 0 && Value(result, "露线芯高度") == 0, "缺少露线芯未清零。");
        Check(Value(result, "压接区宽度") == 31 && Value(result, "飞丝") == 1, "缺少露线芯影响了其他检测项。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 2);
        result = Parse(parameter, detections);
        Check(Value(result, "飞丝") == 0, "缺少飞丝未输出数量0。");
        var items = Solution.Instance.DetectItemDic["解析"];
        items.Single(i => i.Name == "飞丝" || i.Name == "DetectItem.FlyingWire").MaxValue = "0";
        result = Parse(parameter, Detections());
        Check(!result.IsOk && !result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].IsOk, "飞丝超上限未判NG。");
        items[0].Enable = false; items[2].MaxValue = "10";
        result = Parse(parameter, Detections());
        Check(result.DetectItemCount == 5, "禁用项仍输出。");
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 31).Color == Color.Red,
            "同框压接区宽度NG未合并颜色。");
        result = Parse(parameter, null);
        Check(items.Where(i => i.Enable).All(i => i.CurValue == "0"), "未检出保留了上次值。");
    }
    /// <summary>验证新增类别的零值、多目标计数、阈值、绘制、禁用及尺寸换算隔离。</summary>
    private static void VerifyCrimpDefect()
    {
        var parameter = Parameter("压接不良数量");
        var item = Solution.Instance.DetectItemDic[parameter.CurDetectItemName].Single(i => i.Name == "压接不良");
        item.MaxValue = "0";
        item.IsCountItem = false;
        parameter.NeedConvert = true;
        parameter.Scale = 0.1f;
        var detections = Detections();
        var result = Parse(parameter, detections);
        Check(result.IsOk && Value(result, "压接不良") == 0, "无压接不良时应输出0并判OK。");
        detections.Add(new DetResult { ClassId = 3, Box = new Rect(250, 180, 11, 12) });
        result = Parse(parameter, detections);
        Check(!result.IsOk && Value(result, "压接不良") == 1, "单个压接不良未按数量判NG。");
        Check(result.AlgorithmResult.Texts.Count == result.DetectItemCount,
            "NG时也不应追加当前产品判定结果文字。");
        detections.Add(new DetResult { ClassId = 3, Box = new Rect(280, 180, 13, 14) });
        detections.Add(new DetResult { ClassId = 3, Box = new Rect(310, 180, 0, 14) });
        result = Parse(parameter, detections);
        Check(Value(result, "压接不良") == 2 && !result.IsOk, "数量被换算或计入了无效框。");
        Check(Value(result, "飞丝") == 1, "压接不良混入飞丝数量。");
        Check(result.AlgorithmResult.Rects.Count(r => r.Color == Color.Red) == 2, "压接不良框未标红。");
        Check(result.AlgorithmResult.Lines.Count == 20, "压接不良不应绘制尺寸箭头。");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.EndsWith("压接不良: 2")), "数量显示不应带mm或小数。");
        item.MaxValue = "2";
        Check(Parse(parameter, detections).IsOk, "压接不良没有遵循配置上限。");
        item.Enable = false;
        Check(!Parse(parameter, detections).AlgorithmResult.DetectResults.ContainsKey("压接不良"), "禁用后仍输出压接不良。");
        item.Enable = true;
        item.MaxValue = "0";
        Check(Value(Parse(parameter, null), "压接不良") == 0 && item.CurValue == "0", "空结果未清零压接不良数量。");
        LanguageManager.SetLanguage("en-US", false);
        Check(DetectItemLanguage.GetDisplayName("压接不良") == "Crimp defect", "压接不良英文翻译缺失。");
        LanguageManager.SetLanguage("ja-JP", false);
        Check(DetectItemLanguage.GetDisplayName("压接不良") == "圧着不良", "压接不良日文翻译缺失。");
        LanguageManager.SetLanguage("zh-CN", false);
    }
    /// <summary>读取设计器实际控件。</summary>
    private static T Field<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
    /// <summary>验证下拉中文连字符名称可选、可保存并从方案恢复。</summary>
    private static void VerifyForm()
    {
        var parameter = Parameter("界面");
        using (var form = new ParamFormTDAI())
        {
            form.SetNodeBelong(new NodeBase(1, "离线验证", new TDJS_Vision.Process("验证"), NodeType.AITD));
            var combo = Field<ComboBox>(form, "comboBoxModelName");
            Check(combo.Items.Contains("AST-工位3模型"), "Designer未提供指定名称。");
            combo.SelectedItem = "AST-工位3模型";
            Field<RadioButton>(form, "radioButton1").Checked = true;
            var configs = Field<ComboBox>(form, "comboBoxDetectConfig1");
            configs.Items.Add("界面"); configs.SelectedItem = "界面";
            Field<TextBox>(form, "textBoxConfigPath").Text = "离线配置";
            Field<TextBox>(form, "textBox_StudyNum").Text = "2";
            Field<TextBox>(form, "textBox_studyPercentage").Text = "10";
            Field<TextBox>(form, "textBox_Scale").Text = "1";
            var subscription = Field<object>(form, "nodeSubscription1");
            subscription.GetType().GetMethod("SetText").Invoke(subscription, new object[] { "1.图像源", "图像" });
            typeof(ParamFormTDAI).GetField("aIInputInfo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form,
                new AIInputInfo { ModelSize = 640, ClassNames = new List<string> { "female_copper_wire", "closed_opening", "fly_line" },
                    ModelInfo = new ModelInfo { ModelType = ModelType.DET, ModelPath = "不存在的离线模型" } });
            Check((bool)typeof(ParamFormTDAI).GetMethod("SaveParams", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null), "参数保存失败。");
            var saved = (NodeParamTDAI)form.Params; saved.ModelLoadTask.Wait();
            Check(saved.ModelName.ToString() == "AST_工位3模型", "模型枚举保存错误。");
            form.Params = JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(saved));
            var gate = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            Check(combo.Text == "AST-工位3模型", "方案恢复丢失了连字符显示名称。");
        }
        var template = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/AST-工位3检测项.items"));
        Check(template.Select(i => i.Name).SequenceEqual(Names), "发布模板检测项错误。");
        Check(template.Single(i => i.Name == "飞丝").IsCountItem, "飞丝模板未设置为数量型。");
        var defect = template.Single(i => i.Name == "压接不良");
        Check(defect.IsCountItem && defect.Enable && defect.MinValue == "0" && defect.MaxValue == "0", "压接不良模板应启用数量型且上下限为0。");
        Check((int)ModelName.AST_工位2模型 == 7, "旧模型枚举值发生变化。");
    }
    /// <summary>验证参数窗体可直接读取的加密配置、输入尺寸及完整类别顺序。</summary>
    private static void VerifyConfiguration()
    {
        string source = File.ReadAllText("Template/TDAI/AST-工位3模型配置.json");
        string decoded = StringCipher.Decrypt(File.ReadAllText("Template/TDAI/AST-工位3模型.ai"));
        Check(source == decoded, "可导入配置与可读配置不一致。");
        var config = JsonConvert.DeserializeObject<AIInputInfo>(decoded);
        Check(config.ModelSize == 640 && config.ModelInfo.ModelType == ModelType.DET && config.ModelInfo.DeviceType == DeviceType.CPU && config.ModelInfo.IsEncrypted,
            "模型类型、尺寸或加密标志错误。");
        Check(config.ClassNames.SequenceEqual(new[] { "female_copper_wire", "closed_opening", "fly_line" }),
            "配置标签顺序与模型不一致。");
        Check(config.ModelName == "AST-工位3模型" && config.ModelInfo.ModelPath.EndsWith("model_encrypted.onnx"),
            "配置模型名称或文件错误。");
        LanguageManager.SetLanguage("en-US", false);
        Check(DetectItemLanguage.GetDisplayName("压接区宽度") == "Crimp area width", "英文显示未使用中文键。");
        LanguageManager.SetLanguage("zh-CN", false);
        Check(DetectItemLanguage.GetDisplayName("压接区宽度") == "压接区宽度", "切回中文后显示错误。");
    }
    /// <summary>执行离线回归。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyParsing(); VerifyCrimpDefect(); VerifyForm();
            Console.WriteLine("工位3/4解析及模板窗体验证通过，共" + _checks + "项断言；继续验证模型配置。");
            VerifyConfiguration();
            Console.WriteLine("AST工位3离线验证通过，共" + _checks + "项断言。"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
