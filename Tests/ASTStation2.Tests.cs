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

/// <summary>AST工位2真实解析及窗体离线回归，不加载现场模型。</summary>
internal static class ASTStation2Tests
{
    /// <summary>已通过的断言数量。</summary>
    private static int _checks;
    /// <summary>用户要求的十二个检测项，前九项保持既有顺序，末尾补齐飞丝数量和屏蔽网尺寸。</summary>
    private static readonly string[] Names = { "铜环底部到端子头顶部的距离", "铜环底部到绝缘体顶部的距离",
        "铜环底部到屏蔽网顶部距离", "露线芯宽度", "露线芯长度", "胶皮顶部到屏蔽网顶部的距离",
        "胶皮顶部到屏蔽网底部的距离", "胶皮顶部到绝缘体顶部的距离", "胶皮顶部到绝缘体底部的距离",
        "飞丝", "屏蔽网高度", "屏蔽网宽度" };
    /// <summary>验证业务行为，失败时输出场景。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++;
    }
    /// <summary>创建独立检测配置。</summary>
    private static NodeParamTDAI Parameter(string name)
    {
        Solution.Instance.DetectItemDic[name] = Names.Select(n => new DetectItemInfo {
            Name = n, Enable = true, MinValue = "1", MaxValue = "99999", CurValue = "0" }).ToList();
        return new NodeParamTDAI { CurDetectItemName = name, DetectItemName1 = name, IsFixed = true,
            NodeName = name, StudyNum = 2, StudyPercentage = 10, Scale = 1 };
    }
    /// <summary>铜环底边380，胶皮顶边410，用固定Y边缘差验证距离项。</summary>
    private static List<DetResult> Detections()
    {
        return new List<DetResult> {
            new DetResult { ClassId = 4, Box = new Rect(100, 300, 40, 80) },
            new DetResult { ClassId = 0, Box = new Rect(100, 100, 35, 60) },
            new DetResult { ClassId = 2, Box = new Rect(100, 210, 30, 40) },
            new DetResult { ClassId = 6, Box = new Rect(150, 180, 3, 4) },
            new DetResult { ClassId = 3, Box = new Rect(100, 290, 38, 35) },
            new DetResult { ClassId = 1, Box = new Rect(100, 175, 18, 27) },
            new DetResult { ClassId = 5, Box = new Rect(100, 410, 40, 60) } };
    }
    /// <summary>通过实际程序集调用解析器，旧版本缺少模型时给出明确失败。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI parameter, List<DetResult> detections)
    {
        var type = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Node._3_Detection.TDAI.Parse.ASTStation2Parse");
        Check(type != null, "目标分类尚未接入AST工位2解析器。");
        object[] args = { detections, detections == null ? 0 : detections.Count, parameter,
            new TDJS_Vision.Process("离线验证"), new NodeResultTDAI() };
        type.GetMethod("Parse").Invoke(null, args);
        return (NodeResultTDAI)args[4];
    }
    /// <summary>读取实际尺寸数值。</summary>
    private static float Value(NodeResultTDAI result, string name)
    {
        return float.Parse(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName(name)][0].Value);
    }
    /// <summary>验证固定上下边缘距离、长度方向、缺失值、换算、颜色和多目标。</summary>
    private static void VerifyParsing()
    {
        var parameter = Parameter("解析");
        var detections = Detections();
        var result = Parse(parameter, detections);
        float[] expected = { 280, 170, 90, 18, 27, 120, 85, 200, 160, 1, 35, 38 };
        Check(result.DetectItemCount == 12 && result.IsOk, "十二项结果不完整或判定错误。");
        for (int i = 0; i < Names.Length; i++) Check(Value(result, Names[i]) == expected[i], Names[i] + "数值错误。");
        Check(result.AlgorithmResult.Rects.Count == 7, "参与框去重错误或漏绘了飞丝。");
        Check(result.AlgorithmResult.DetectResults.ContainsKey("DetectItem.ExposedCoreLength"), "露线芯长度未兼容既有存储键。");
        parameter.NeedConvert = true; parameter.Scale = 0.1f;
        result = Parse(parameter, detections);
        Check(Value(result, Names[0]) == 28, "Y距离换算错误。");
        Check(Math.Abs(Value(result, Names[4]) - 2.7f) < 0.001f, "露线芯长度换算错误。");
        Check(Value(result, Names[5]) == 12 && Value(result, Names[8]) == 16, "胶皮距离换算错误。");
        parameter.NeedConvert = false;
        detections.RemoveAll(d => d.ClassId == 4);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Names.Take(3).All(n => Value(result, n) == 0), "缺少铜环未将三项清零。");
        Check(Value(result, Names[3]) == 18 && Value(result, Names[4]) == 27, "缺少铜环中断了露线芯解析。");
        Check(Value(result, Names[5]) == 120 && Value(result, Names[8]) == 160, "缺少铜环影响了胶皮距离解析。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 5);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Names.Skip(5).Take(4).All(n => Value(result, n) == 0), "缺少胶皮未将四项清零。");
        Check(Value(result, Names[0]) == 280 && Value(result, Names[3]) == 18, "缺少胶皮影响了其他检测项。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 6);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Value(result, "飞丝") == 0, "缺少飞丝未将数量项清零。");
        Check(Value(result, "屏蔽网高度") == 35 && Value(result, "屏蔽网宽度") == 38, "缺少飞丝影响了屏蔽网尺寸。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 3);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Value(result, Names[2]) == 0 && Value(result, Names[5]) == 0 &&
            Value(result, "屏蔽网高度") == 0 && Value(result, "屏蔽网宽度") == 0, "缺少屏蔽网未将关联项清零。");
        var items = Solution.Instance.DetectItemDic["解析"];
        items[0].MinValue = items[0].MaxValue = "0";
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 4);
        Check(Parse(parameter, detections).AlgorithmResult.DetectResults[Names[0]][0].IsOk, "未检出0值上下限判定错误。");
        items[0].Enable = false; items[3].MaxValue = "10";
        result = Parse(parameter, Detections());
        Check(result.DetectItemCount == 11, "禁用项仍输出。");
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 18).Color == Color.Red, "同框尺寸NG未合并。");
        result = Parse(parameter, null);
        Check(items.Where(i => i.Enable).All(i => i.CurValue == "0"), "未检出保留旧值。");
        var edges = Parameter("边缘");
        detections = Detections();
        detections[1] = new DetResult { ClassId = 0, Box = new Rect(800, 330, 35, 60) };
        Check(Value(Parse(edges, detections), Names[0]) == 50, "Y重叠时固定铜环底边到目标顶边不能被归零。");
        detections[1] = new DetResult { ClassId = 0, Box = new Rect(100, 420, 35, 60) };
        Check(Value(Parse(edges, detections), Names[0]) == 40, "反向目标应取指定Y边缘之差绝对值。");
        detections[1] = new DetResult { ClassId = 0, Box = new Rect(100, 380, 35, 60) };
        Check(Value(Parse(edges, detections), Names[0]) == 0, "指定两条边重合应为0。");
        detections = Detections();
        detections.Add(new DetResult { ClassId = 4, Box = new Rect(1000, 300, 40, 80) });
        detections.Add(new DetResult { ClassId = 0, Box = new Rect(1000, 180, 35, 60) });
        result = Parse(edges, detections);
        Check(result.AlgorithmResult.DetectResults[Names[0]].Select(r => float.Parse(r.Value)).SequenceEqual(new[] { 280f, 200f }), "多目标铜环匹配错误。");
        detections.RemoveAll(d => d.ClassId == 0);
        result = Parse(edges, detections);
        Check(Value(result, Names[0]) == 0 && Value(result, Names[1]) == 170, "缺少端子头影响了其他距离。");
    }
    /// <summary>不同节点学习样本不得串用，未检出不得进入尺寸学习。</summary>
    private static void VerifyStudy()
    {
        var first = Parameter("学习一"); var second = Parameter("学习二");
        first.IsAutoStudy = second.IsAutoStudy = true;
        Parse(first, Detections()); Parse(second, Detections());
        Check(first.IsAutoStudy && second.IsAutoStudy, "节点串用了学习计数。");
        Parse(first, Detections());
        Check(!first.IsAutoStudy && second.IsAutoStudy, "学习完成状态未隔离。");
        Check(Solution.Instance.DetectItemDic["学习一"][0].MinValue == "252.00", "距离学习范围错误。");
        var missing = Parameter("空学习"); missing.IsAutoStudy = true; missing.StudyNum = 1;
        Parse(missing, null);
        Check(Solution.Instance.DetectItemDic["空学习"][0].MinValue == "1", "缺失值污染学习范围。");
    }
    /// <summary>真实窗体订阅完成事件时，同名节点也必须只关闭发出事件的节点。</summary>
    private static void VerifySameNameStudyForms()
    {
        var first = Parameter("同名配置一"); var second = Parameter("同名配置二");
        first.NodeName = second.NodeName = "目标分类";
        first.IsAutoStudy = second.IsAutoStudy = true;
        first.StudyNum = 1;
        using (var firstForm = new ParamFormTDAI())
        using (var secondForm = new ParamFormTDAI())
        {
            firstForm.SetNodeBelong(new NodeBase(1, "目标分类", new TDJS_Vision.Process("流程一"), NodeType.AITD));
            secondForm.SetNodeBelong(new NodeBase(1, "目标分类", new TDJS_Vision.Process("流程二"), NodeType.AITD));
            firstForm.Params = first; secondForm.Params = second;
            Parse(first, Detections());
            Check(!first.IsAutoStudy && second.IsAutoStudy, "学习完成事件错误关闭了其他流程的同名节点。");
            TDJS_Vision.Node._3_Detection.TDAI.Parse.ParseCommon.CloseAutoStudyEvent?.Invoke(null, "目标分类");
            Check(!second.IsAutoStudy, "旧解析器按名称发送的学习完成通知失效。");
        }
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
            Check(combo.Items.Contains("AST-工位2模型"), "Designer未提供指定名称。");
            combo.SelectedItem = "AST-工位2模型";
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
                new AIInputInfo { ModelSize = 640, ClassNames = new List<string> { "terminal head", "exposed_wire_core", "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line" },
                    ModelInfo = new ModelInfo { ModelType = ModelType.DET, ModelPath = "不存在的离线模型" } });
            Check((bool)typeof(ParamFormTDAI).GetMethod("SaveParams", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null), "参数保存失败。");
            var saved = (NodeParamTDAI)form.Params; saved.ModelLoadTask.Wait();
            Check(saved.ModelName.ToString() == "AST_工位2模型", "模型枚举保存错误。");
            form.Params = JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(saved));
            var gate = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            Check(combo.Text == "AST-工位2模型", "方案恢复丢失了连字符显示名称。");
            form.Show(); Application.DoEvents();
            using (var shot = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(shot, new Rectangle(0, 0, form.Width, form.Height));
                shot.Save("AST2-parameters.png");
            }
        }
        var template = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/AST-工位2检测项.items"));
        Check(template.Select(i => i.Name).SequenceEqual(Names), "发布模板检测项错误。");
        Check(template.Single(i => i.Name == "飞丝").IsCountItem, "飞丝模板未设置为数量型。");
        Check((int)ModelName.超声波焊接侧面三类模型 == 5, "旧模型枚举值发生变化。");
    }
    /// <summary>验证参数窗体可直接读取的加密配置、输入尺寸及完整类别顺序。</summary>
    private static void VerifyConfiguration()
    {
        string source = File.ReadAllText("Template/TDAI/AST-工位2模型配置.json");
        string decoded = StringCipher.Decrypt(File.ReadAllText("Template/TDAI/AST-工位2模型.ai"));
        Check(source == decoded, "可导入配置与可读配置不一致。");
        var config = JsonConvert.DeserializeObject<AIInputInfo>(decoded);
        Check(config.ModelSize == 640 && config.ModelInfo.ModelType == ModelType.DET && config.ModelInfo.DeviceType == DeviceType.CPU && config.ModelInfo.IsEncrypted,
            "模型类型、尺寸或加密标志错误。");
        Check(config.ClassNames.SequenceEqual(new[] { "terminal head", "exposed_wire_core", "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line" }),
            "配置标签顺序与模型不一致。");
        Check(config.ModelName == "AST-工位2模型" && config.ModelInfo.ModelPath.EndsWith("model_encrypted.onnx"),
            "配置模型名称或文件错误。");
        LanguageManager.SetLanguage("en-US", false);
        Check(DetectItemLanguage.GetDisplayName(Names[0]) == "Copper ring bottom to terminal head top distance", "英文显示未使用中文键。");
        LanguageManager.SetLanguage("zh-CN", false);
        Check(DetectItemLanguage.GetDisplayName(Names[0]) == Names[0], "切回中文后显示错误。");
    }
    /// <summary>执行离线回归。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyParsing(); VerifyStudy(); VerifySameNameStudyForms(); VerifyForm(); VerifyConfiguration();
            Console.WriteLine("AST工位2离线验证通过，共" + _checks + "项断言。"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
