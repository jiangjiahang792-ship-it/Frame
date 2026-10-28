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
    /// <summary>删除六项后保留的九项检测，顺序与发布模板一致。</summary>
    private static readonly string[] Names = { "护套内导体位置", "出线芯宽度", "出线芯长度",
        "编组反转代-1", "编组反转代-2", "L5_绝缘体长度", "飞丝", "L6_箔剥宽度", "L6_箔剥长度" };
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
            new DetResult { ClassId = 5, Box = new Rect(100, 410, 40, 60) },
            new DetResult { ClassId = 7, Box = new Rect(100, 260, 36, 20) } };
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
    /// <summary>验证新旧ROI文本链路保留标注位置，并用真实显示控件生成离线预览。</summary>
    private static void VerifyAnnotationProjection(AlgorithmResult source)
    {
        var assembly = typeof(NodeParamTDAI).Assembly;
        foreach (string suffix in new[] { "ResultOverlayDraw", "ResultOverlayDraw2" })
        {
            string ns = "TDJS_Vision.Node._7_ResultProcessing." + suffix + ".";
            var nodeType = assembly.GetType(ns + suffix + "Builder");
            var itemType = assembly.GetType(ns + suffix + "Item");
            Check(nodeType != null && itemType != null, suffix + "绘图类型不存在。");
            var projected = new AlgorithmResult();
            var append = nodeType.GetMethod("AppendAlgorithmResultTexts", BindingFlags.Static | BindingFlags.NonPublic);
            Check(append != null, suffix + "文本投影方法不存在。");
            append.Invoke(null, new[] { (object)projected, source, Activator.CreateInstance(itemType) });
            var original = source.Texts.Where(t => t.UseImagePosition).ToList();
            var actual = projected.Texts.Where(t => t.UseImagePosition).ToList();
            Check(actual.Count == original.Count && actual.Select(t => t.ImagePosition).SequenceEqual(original.Select(t => t.ImagePosition)), suffix + "丢失箭头文字的图像位置。");
        }
        using (var control = new TDJS_Vision.Forms.DispShowImage.ShowImageControl())
        using (var bitmap = new Bitmap(640, 850))
        {
            control.Size = bitmap.Size;
            control.CreateControl();
            var background = new Bitmap(400, 550);
            using (var graphics = Graphics.FromImage(background)) graphics.Clear(Color.FromArgb(45, 45, 45));
            control.SetImage(background, source);
            control.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(Environment.CurrentDirectory, "ASTMeasurementArrows.preview.png"));
        }
    }

    /// <summary>验证固定上下边缘距离、长度方向、缺失值、换算、颜色和多目标。</summary>
    private static void VerifyParsing()
    {
        var parameter = Parameter("解析");
        var detections = Detections();
        var result = Parse(parameter, detections);
        float[] expected = { 250, 18, 27, 120, 85, 200, 1, 30, 40 };
        Check(result.DetectItemCount == 9 && result.IsOk, "九项结果不完整或判定错误。");
        for (int i = 0; i < Names.Length; i++) Check(Value(result, Names[i]) == expected[i], Names[i] + "数值错误。");
        // 旧模板加载后必须输出新名称，同时旧订阅经公共归一化仍能读取相同数值。
        string[] legacyNames = { "胶皮顶部到屏蔽网顶部的距离", "胶皮顶部到屏蔽网底部的距离",
            "胶皮顶部到绝缘体顶部的距离", "绝缘体宽度", "绝缘体长度" };
        int[] renamedIndices = { 3, 4, 5, 7, 8 };
        var legacyParameter = Parameter("旧名称兼容");
        for (int i = 0; i < legacyNames.Length; i++)
            Solution.Instance.DetectItemDic[legacyParameter.CurDetectItemName][renamedIndices[i]].Name = legacyNames[i];
        var legacyResult = Parse(legacyParameter, detections);
        for (int i = 0; i < legacyNames.Length; i++)
        {
            Check(legacyResult.AlgorithmResult.DetectResults.ContainsKey(Names[renamedIndices[i]]), "旧模板没有输出新名称。");
            Check(Value(legacyResult, legacyNames[i]) == expected[renamedIndices[i]], "旧名称读取结果错误。");
            Check(DetectItemLanguage.GetConfigName(legacyNames[i]) == Names[renamedIndices[i]], "导出没有使用新名称。");
        }
        Check(result.AlgorithmResult.Rects.Count == 6, "参与框去重错误或绘制了已删除的铝箔项。");
        Check(result.AlgorithmResult.Lines.Count == 40 && result.AlgorithmResult.Lines.All(l => l.Color == Color.Yellow && !l.ShowCenterCross), "工位2尺寸箭头缺失或错误地绘制了数量项。");
        Check(result.AlgorithmResult.Lines.Any(l => l.P1.Y == 160 && l.P2.Y == 410), "端子头底边到胶皮顶边箭头端点错误。");
        Check(!result.AlgorithmResult.Lines.Any(l => l.P1.Y == 260 && l.P2.Y == 380), "已删除的铝箔距离仍绘制箭头。");
        Check(!result.AlgorithmResult.Texts.Any(t => t.Color == Color.Yellow), "箭头不应显示黄色数值。");
        var foilFilter = Parameter("铝箔区域飞丝筛选");
        var foilFilterDetections = Detections();
        foilFilterDetections.Add(new DetResult { ClassId = 6, Box = new Rect(110, 265, 4, 4) });
        result = Parse(foilFilter, foilFilterDetections);
        Check(Value(result, "飞丝") == 1, "铝箔区域内部的飞丝没有被筛除。");
        foilFilterDetections.Add(new DetResult { ClassId = 6, Box = new Rect(95, 265, 10, 4) });
        result = Parse(foilFilter, foilFilterDetections);
        Check(Value(result, "飞丝") == 1, "与铝箔交叠50%的飞丝没有被筛除。");
        foilFilterDetections.Add(new DetResult { ClassId = 6, Box = new Rect(93, 265, 10, 4) });
        result = Parse(foilFilter, foilFilterDetections);
        Check(Value(result, "飞丝") == 1, "与铝箔交叠正好30%的飞丝没有按阈值筛除。");
        foilFilterDetections.Add(new DetResult { ClassId = 6, Box = new Rect(88, 265, 15, 4) });
        result = Parse(foilFilter, foilFilterDetections);
        Check(Value(result, "飞丝") == 2, "与铝箔交叠低于30%的飞丝被错误屏蔽。");
        foilFilterDetections.RemoveAll(d => d.ClassId == 7);
        result = Parse(foilFilter, foilFilterDetections);
        Check(Value(result, "飞丝") == 5, "未检出铝箔区域时不应屏蔽飞丝。");
        VerifyAnnotationProjection(result.AlgorithmResult);
        Check(result.AlgorithmResult.DetectResults.ContainsKey("出线芯长度"), "出线芯长度未按新检测项名称输出。");
        parameter.NeedConvert = true; parameter.Scale = 0.1f;
        result = Parse(parameter, detections);
        Check(Value(result, Names[0]) == 25, "Y距离换算错误。");
        Check(Math.Abs(Value(result, "出线芯长度") - 2.7f) < 0.001f, "出线芯长度换算错误。");
        Check(Value(result, "绝缘体宽度") == 3 && Value(result, "绝缘体长度") == 4, "绝缘体尺寸换算错误。");
        Check(Value(result, "编组反转代-1") == 12 && Value(result, "编组反转代-2") == 8.5f && Value(result, "L5_绝缘体长度") == 20, "胶皮距离换算错误。");
        Check(result.AlgorithmResult.Texts.Any(t => !t.UseImagePosition && t.Text.Contains("25.00mm")), "检测汇总文字未使用换算后的mm值。");
        parameter.NeedConvert = false;
        detections.RemoveAll(d => d.ClassId == 4);
        result = Parse(parameter, detections);
        Check(result.IsOk && Value(result, Names[0]) == 250, "护套内导体位置不应依赖铜环。");
        Check(result.AlgorithmResult.Lines.Count == 40, "缺少铜环不应影响距离箭头。");
        Check(Value(result, "出线芯宽度") == 18 && Value(result, "出线芯长度") == 27, "缺少铜环中断了出线芯解析。");
        Check(Value(result, "编组反转代-1") == 120 && Value(result, "L5_绝缘体长度") == 200, "缺少铜环影响了胶皮距离解析。");
        Check(Value(result, "绝缘体宽度") == 30 && Value(result, "绝缘体长度") == 40, "缺少铜环影响了绝缘体尺寸解析。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 5);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Names.Skip(3).Take(3).All(n => Value(result, n) == 0), "缺少胶皮未将三项清零。");
        Check(Value(result, Names[0]) == 0 && Value(result, "出线芯宽度") == 18, "缺少胶皮影响了其他检测项。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 6);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Value(result, "飞丝") == 0, "缺少飞丝未将数量项清零。");
        Check(Value(result, "编组反转代-1") == 120 && Value(result, "编组反转代-2") == 85, "缺少飞丝影响了屏蔽网距离。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 3);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Value(result, "编组反转代-1") == 0 &&
            Value(result, "编组反转代-2") == 0, "缺少屏蔽网未将关联项清零。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 2);
        result = Parse(parameter, detections);
        Check(!result.IsOk && Value(result, "L5_绝缘体长度") == 0 &&
            Value(result, "L6_箔剥宽度") == 0 && Value(result, "L6_箔剥长度") == 0,
            "缺少绝缘体未将关联项和绝缘体尺寸清零。");
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 7);
        result = Parse(parameter, detections);
        Check(result.IsOk && result.DetectItemCount == 9, "已删除的铝箔项影响了判定。");
        var items = Solution.Instance.DetectItemDic["解析"];
        items[0].MinValue = items[0].MaxValue = "0";
        detections = Detections();
        detections.RemoveAll(d => d.ClassId == 5);
        Check(Parse(parameter, detections).AlgorithmResult.DetectResults[Names[0]][0].IsOk, "未检出0值上下限判定错误。");
        items[0].Enable = false; items[1].MaxValue = "10";
        result = Parse(parameter, Detections());
        Check(result.DetectItemCount == 8, "禁用项仍输出。");
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 18).Color == Color.Red, "同框尺寸NG未合并。");
        result = Parse(parameter, null);
        Check(items.Where(i => i.Enable).All(i => i.CurValue == "0"), "未检出保留旧值。");
        var edges = Parameter("边缘");
        detections = Detections();
        detections[1] = new DetResult { ClassId = 0, Box = new Rect(800, 330, 35, 60) };
        Check(Value(Parse(edges, detections), Names[0]) == 20, "X偏移不得改变端子头底边到胶皮顶边的Y距离。");
        detections[1] = new DetResult { ClassId = 0, Box = new Rect(100, 420, 35, 60) };
        Check(Value(Parse(edges, detections), Names[0]) == 70, "反向目标应取指定Y边缘之差绝对值。");
        detections[1] = new DetResult { ClassId = 0, Box = new Rect(100, 350, 35, 60) };
        Check(Value(Parse(edges, detections), Names[0]) == 0, "指定两条边重合应为0。");
        detections = Detections();
        detections.Add(new DetResult { ClassId = 5, Box = new Rect(1000, 410, 40, 60) });
        detections.Add(new DetResult { ClassId = 0, Box = new Rect(1000, 180, 35, 60) });
        result = Parse(edges, detections);
        Check(result.AlgorithmResult.DetectResults[Names[0]].Select(r => float.Parse(r.Value)).SequenceEqual(new[] { 250f, 170f }), "多目标胶皮匹配错误。");
        detections.RemoveAll(d => d.ClassId == 0);
        result = Parse(edges, detections);
        Check(Value(result, Names[0]) == 0 && Value(result, "L5_绝缘体长度") == 200, "缺少端子头影响了其他距离。");
    }
    /// <summary>检测项模板中出现暂未接入的名称时，只记录日志并继续解析其它检测项。</summary>
    private static void VerifyUnsupportedItemDoesNotStopParsing()
    {
        var parameter = Parameter("未支持项容错");
        Solution.Instance.DetectItemDic["未支持项容错"].Add(new DetectItemInfo {
            Name = "未接入的新检测项", Enable = true, MinValue = "1", MaxValue = "99999", CurValue = "0" });
        var result = Parse(parameter, Detections());
        Check(result.IsOk && Value(result, Names[0]) == 250 && Value(result, "编组反转代-1") == 120,
            "未支持检测项影响了其它检测项解析。");
        Check(!result.AlgorithmResult.DetectResults.ContainsKey("未接入的新检测项"),
            "未支持检测项不应输出伪造结果。");
        // 旧方案可能仍保存已删除项及旧名称，它们不得再输出检测结果或影响其余九项。
        string[] removedNames = { "L1-FiLay(绝缘体长度)", "铜环底部到屏蔽网顶部距离",
            "胶皮顶部到绝缘体底部的距离", "屏蔽网高度", "屏蔽网宽度", "L1-ALFoiLay(箔长度)",
            "铜环底部到绝缘体顶部的距离", "铜环底部到铝箔顶部的距离" };
        foreach (string name in removedNames)
            Solution.Instance.DetectItemDic[parameter.CurDetectItemName].Add(new DetectItemInfo {
                Name = name, Enable = true, MinValue = "99999", MaxValue = "99999" });
        result = Parse(parameter, Detections());
        Check(result.IsOk && result.DetectItemCount == 9 &&
            removedNames.All(name => !result.AlgorithmResult.DetectResults.ContainsKey(name)), "已删除项仍参与检测。");
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
        Check(Solution.Instance.DetectItemDic["学习一"][0].MinValue == "225.00", "距离学习范围错误。");
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
                new AIInputInfo { ModelSize = 640, ClassNames = new List<string> { "terminal head", "exposed_wire_core", "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line", "aluminum_foi" },
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
        Check(template.Select(i => i.Name).SequenceEqual(Names.Concat(new[] { "线芯长度", "线芯宽度" })), "发布模板检测项错误。");
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
        Check(config.ClassNames.SequenceEqual(new[] { "terminal head", "exposed_wire_core", "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line", "aluminum_foi", "line" }),
            "配置标签顺序与模型不一致。");
        Check(config.ModelName == "AST-工位2模型" && config.ModelInfo.ModelPath.EndsWith("model_encrypted.onnx"),
            "配置模型名称或文件错误。");
        LanguageManager.SetLanguage("en-US", false);
        Check(DetectItemLanguage.GetDisplayName(Names[0]) == "Sheath inner conductor position", "英文显示未使用中文键。");
        LanguageManager.SetLanguage("zh-CN", false);
        Check(DetectItemLanguage.GetDisplayName(Names[0]) == Names[0], "切回中文后显示错误。");
    }
    /// <summary>验证line与露线芯独立，覆盖多目标、缺失、换算、判定和学习。</summary>
    private static void VerifyLineMeasurements()
    {
        var parameter = Parameter("线芯尺寸");
        var items = Solution.Instance.DetectItemDic[parameter.CurDetectItemName];
        items.Add(new DetectItemInfo { Name = "线芯长度", Enable = true, MinValue = "1", MaxValue = "100" });
        items.Add(new DetectItemInfo { Name = "线芯宽度", Enable = true, MinValue = "1", MaxValue = "20" });
        var detections = Detections();
        detections.Add(new DetResult { ClassId = 8, Box = new Rect(200, 100, 12, 65) });
        var result = Parse(parameter, detections);
        Check(result.DetectItemCount == 11 && result.IsOk, "新增线芯项未完整参与判定。");
        Check(Value(result, "线芯长度") == 65 && Value(result, "线芯宽度") == 12, "line尺寸或中文键归一化错误。");
        Check(Value(result, "出线芯长度") == 27 && Value(result, "出线芯宽度") == 18, "line与露线芯类别混淆。");
        Check(result.AlgorithmResult.Lines.Count == 50, "新增尺寸箭头缺失。");
        parameter.NeedConvert = true; parameter.Scale = 0.1f;
        result = Parse(parameter, detections);
        Check(Value(result, "线芯长度") == 6.5f && Value(result, "线芯宽度") == 1.2f, "line尺寸换算错误。");
        parameter.NeedConvert = false;
        detections.Add(new DetResult { ClassId = 8, Box = new Rect(250, 100, 25, 80) });
        result = Parse(parameter, detections);
        Check(result.AlgorithmResult.DetectResults["线芯宽度"].Count == 2 && !result.IsOk, "多线芯明细或宽度超限判定错误。");
        Check(result.AlgorithmResult.Rects.Single(r => r.RotatedRect.size.width == 25).Color == Color.Red, "线芯同框NG颜色错误。");
        detections.RemoveAll(r => r.ClassId == 8);
        result = Parse(parameter, detections);
        Check(Value(result, "线芯长度") == 0 && Value(result, "线芯宽度") == 0 && !result.IsOk, "缺少line没有清零判定。");
        items[10].Enable = false;
        Check(!Parse(parameter, detections).AlgorithmResult.DetectResults.ContainsKey("线芯宽度"), "禁用线芯宽度仍输出。");
        items[10].Enable = true;
        parameter.IsAutoStudy = true; parameter.StudyNum = 1;
        Parse(parameter, detections);
        Check(items[9].MaxValue == "100" && items[10].MaxValue == "20", "缺失line污染学习范围。");
        detections.Add(new DetResult { ClassId = 8, Box = new Rect(200, 100, 12, 65) });
        parameter.IsAutoStudy = true;
        Parse(parameter, detections);
        Check(items[9].MinValue == "58.50" && items[10].MinValue == "10.80", "line自动学习范围错误。");
    }

    /// <summary>执行离线回归。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyParsing(); VerifyUnsupportedItemDoesNotStopParsing(); VerifyStudy(); VerifySameNameStudyForms(); VerifyForm();
            Console.WriteLine("工位2解析、模板和窗体验证通过，共" + _checks + "项断言；继续验证模型配置。");
            VerifyConfiguration(); VerifyLineMeasurements();
            Console.WriteLine("AST工位2离线验证通过，共" + _checks + "项断言。"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
