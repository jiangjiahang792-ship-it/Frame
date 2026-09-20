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

/// <summary>AST工位1真实解析及窗体离线回归，不加载现场模型。</summary>
internal static class ASTStation1Tests
{
    /// <summary>已通过的断言数量。</summary>
    private static int _checks;
    /// <summary>用户要求的九个检测项及输出顺序。</summary>
    private static readonly string[] Names = { "胶皮到铜环的距离", "胶皮到屏蔽网的距离", "胶皮到绝缘胶的位置",
        "屏蔽网高度", "屏蔽网宽度", "铜环高度", "铜环宽度", "绝缘胶高度", "绝缘胶宽度" };
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
    /// <summary>模拟胶皮在下的竖直线材，X投影重叠，三项Y间距分别为30、60、130。</summary>
    private static List<DetResult> Detections()
    {
        return new List<DetResult> {
            new DetResult { ClassId = 2, Box = new Rect(100, 254, 30, 16) },
            new DetResult { ClassId = 3, Box = new Rect(100, 300, 40, 25) },
            new DetResult { ClassId = 0, Box = new Rect(100, 158, 20, 12) },
            new DetResult { ClassId = 4, Box = new Rect(130, 50, 3, 4) },
            new DetResult { ClassId = 1, Box = new Rect(100, 226, 25, 14) } };
    }
    /// <summary>通过实际程序集调用解析器，旧版本缺少模型时给出明确失败。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI parameter, List<DetResult> detections)
    {
        var type = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Node._3_Detection.TDAI.Parse.ASTStation1Parse");
        Check(type != null, "目标分类尚未接入AST工位1解析器。");
        object[] args = { detections, detections == null ? 0 : detections.Count, parameter,
            new TDJS_Vision.Process("离线验证"), new NodeResultTDAI() };
        type.GetMethod("Parse").Invoke(null, args);
        return (NodeResultTDAI)args[4];
    }
    /// <summary>读取实际尺寸数值。</summary>
    private static float Value(NodeResultTDAI result, string name)
    {
        return float.Parse(result.AlgorithmResult.DetectResults[name][0].Value);
    }
    /// <summary>覆盖类别映射、缺失清零、上下限、换算、禁用和颜色合并。</summary>
    private static void VerifyParsing()
    {
        var parameter = Parameter("解析");
        var detections = Detections();
        var result = Parse(parameter, detections);
        float[] expected = { 30, 60, 130, 14, 25, 16, 30, 12, 20 };
        Check(result.DetectItemCount == 9 && result.IsOk, "九项结果不完整或判定错误。");
        for (int i = 0; i < Names.Length; i++) Check(Value(result, Names[i]) == expected[i], Names[i] + "数值错误。");
        Check(result.AlgorithmResult.Rects.Count == 4, "检测框未去重或加入了未要求的飞丝。");
        parameter.NeedConvert = true; parameter.Scale = 0.1f;
        result = Parse(parameter, detections);
        Check(Math.Abs(Value(result, Names[0]) - 3) < 0.001f, "距离换算错误。");
        Check(Math.Abs(Value(result, Names[3]) - 1.4f) < 0.001f, "高度换算错误。");
        parameter.NeedConvert = false;
        detections.RemoveAll(d => d.ClassId == 3);
        result = Parse(parameter, detections);
        Check(Value(result, Names[0]) == 0 && !result.IsOk, "缺少胶皮仍输出旧距离。");
        Check(Value(result, Names[3]) == 14, "缺少胶皮中断了独立尺寸解析。");
        var items = Solution.Instance.DetectItemDic["解析"];
        items[0].MinValue = "0"; items[0].MaxValue = "0";
        Check(Parse(parameter, detections).AlgorithmResult.DetectResults[Names[0]][0].IsOk, "缺失0未按上下限判定。");
        items[0].Enable = false;
        items[4].MaxValue = "10";
        result = Parse(parameter, Detections());
        Check(result.DetectItemCount == 8, "禁用项仍输出。");
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 25).Color == Color.Red,
            "同框宽度NG未合并颜色。");
        result = Parse(parameter, null);
        Check(items.Where(i => i.Enable).All(i => i.CurValue == "0"), "未检出保留了上次值。");
        detections = Detections();
        detections[0] = new DetResult { ClassId = 2, Box = new Rect(100, 305, 30, 16) };
        var overlap = Parameter("重叠");
        Check(Value(Parse(overlap, detections), Names[0]) == 0, "重叠框应输出零边缘间距。");
        detections[0] = new DetResult { ClassId = 2, Box = new Rect(100, 284, 30, 16) };
        Check(Value(Parse(overlap, detections), Names[0]) == 0, "Y轴接触应输出零边缘间距。");
        detections[0] = new DetResult { ClassId = 2, Box = new Rect(1000, 305, 30, 16) };
        Check(Value(Parse(overlap, detections), Names[0]) == 0, "X分离不能改变Y投影重叠时的零距离。");
        detections = Detections();
        detections[1] = new DetResult { ClassId = 3, Box = new Rect(100, 189, 40, 25) };
        Check(Value(Parse(overlap, detections), Names[0]) == 40, "胶皮位于上方时Y轴边缘距离错误。");
        detections = Detections();
        detections.Add(new DetResult { ClassId = 3, Box = new Rect(1000, 300, 40, 25) });
        detections.Add(new DetResult { ClassId = 2, Box = new Rect(1000, 162, 35, 18) });
        result = Parse(overlap, detections);
        Check(result.AlgorithmResult.DetectResults[Names[0]].Select(r => float.Parse(r.Value)).SequenceEqual(new[] { 30f, 120f }),
            "多目标距离匹配错误。");
        Check(result.AlgorithmResult.DetectResults[Names[6]].Count == 2, "多铜环尺寸明细丢失。");
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
        Check(Solution.Instance.DetectItemDic["学习一"][0].MinValue == "27.00", "距离学习范围错误。");
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
            Check(combo.Items.Contains("AST-工位1模型"), "Designer未提供指定名称。");
            combo.SelectedItem = "AST-工位1模型";
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
                new AIInputInfo { ModelSize = 640, ClassNames = new List<string> { "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line" },
                    ModelInfo = new ModelInfo { ModelType = ModelType.DET, ModelPath = "不存在的离线模型" } });
            Check((bool)typeof(ParamFormTDAI).GetMethod("SaveParams", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null), "参数保存失败。");
            var saved = (NodeParamTDAI)form.Params; saved.ModelLoadTask.Wait();
            Check(saved.ModelName.ToString() == "AST_工位1模型", "模型枚举保存错误。");
            form.Params = JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(saved));
            var gate = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            Check(combo.Text == "AST-工位1模型", "方案恢复丢失了连字符显示名称。");
            form.Show(); Application.DoEvents();
            using (var shot = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(shot, new Rectangle(0, 0, form.Width, form.Height));
                shot.Save("AST-parameters.png");
            }
        }
        var template = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/AST-工位1检测项.items"));
        Check(template.Select(i => i.Name).SequenceEqual(Names), "发布模板检测项错误。");
        Check((int)ModelName.超声波焊接侧面三类模型 == 5, "旧模型枚举值发生变化。");
    }
    /// <summary>验证参数窗体可直接读取的加密配置、输入尺寸及完整类别顺序。</summary>
    private static void VerifyConfiguration()
    {
        string source = File.ReadAllText("Template/TDAI/AST-工位1模型配置.json");
        string decoded = StringCipher.Decrypt(File.ReadAllText("Template/TDAI/AST-工位1模型.ai"));
        Check(source == decoded, "可导入配置与可读配置不一致。");
        var config = JsonConvert.DeserializeObject<AIInputInfo>(decoded);
        Check(config.ModelSize == 640 && config.ModelInfo.ModelType == ModelType.DET && config.ModelInfo.IsEncrypted,
            "模型类型、尺寸或加密标志错误。");
        Check(config.ClassNames.SequenceEqual(new[] { "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line" }),
            "配置标签顺序与模型不一致。");
        Check(config.ModelName == "AST-工位1模型" && config.ModelInfo.ModelPath.EndsWith("work1.onnx.encrypted"),
            "配置模型名称或文件错误。");
        LanguageManager.SetLanguage("en-US", false);
        Check(DetectItemLanguage.GetDisplayName(Names[0]) == "Rubber to copper ring distance", "英文显示未使用中文键。");
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
            Console.WriteLine("AST工位1离线验证通过，共" + _checks + "项断言。"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
