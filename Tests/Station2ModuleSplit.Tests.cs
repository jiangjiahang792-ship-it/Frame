using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.ContourMatch;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.Login;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.LineLineAngle;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;

/// <summary>读取真实方案快照验证模块关联与逐目标判定，不连接设备或加载AI模型。</summary>
internal static class Station2ModuleSplitTests
{
    /// <summary>已通过断言数量。</summary>
    private static int checks;
    /// <summary>设置无设备测试对象的私有状态。</summary>
    private static void Set(object obj, Type owner, string field, object value) => owner.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    /// <summary>反射调用实际调参方法。</summary>
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(obj, args);
    /// <summary>校验业务约束。</summary>
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    /// <summary>读取实际参数，按原层级重建仅供关联检查的节点对象。</summary>
    private static TDJS_Vision.Process ReadScope(JObject scope)
    {
        var process = (TDJS_Vision.Process)FormatterServices.GetUninitializedObject(typeof(TDJS_Vision.Process));
        process.ProcessName = (string)(scope["ProcessName"] ?? scope["ModuleName"]);
        Set(process, typeof(TDJS_Vision.Process), "_nodes", new List<NodeBase>());
        Set(process, typeof(TDJS_Vision.Process), "_connections", scope["ConnectionInfos"].ToObject<List<ProcessConnection>>());
        foreach (JObject config in scope["NodeInfos"])
        {
            var wrapped = ((JObject)config["NodeParam"]).Properties().Single();
            var paramType = typeof(NodeBase).Assembly.GetType(wrapped.Name, true);
            var param = (INodeParam)wrapped.Value.ToObject(paramType);
            var type = typeof(NodeBase);
            switch ((string)config["NodeType"])
            {
                case "AITD": type = typeof(NodeTDAI); ((NodeParamTDAI)param).AIInputInfo = new AIInputInfo(); break;
                case "CompositeModule": type = typeof(NodeCompositeModule); break;
                case "ImageShow": type = typeof(NodeImageShow); break;
                case "ImageCrop": type = typeof(NodeImageCrop); break;
                case "ImageRotate": type = typeof(NodeImageRotate); break;
                case "ResultOverlayDraw": type = typeof(NodeResultOverlayDraw); break;
                case "MultiCondition": type = typeof(NodeMultiCondition); break;
            }
            var node = (NodeBase)FormatterServices.GetUninitializedObject(type);
            Set(node, typeof(NodeBase), "_id", (int)config["ID"]);
            Set(node, typeof(NodeBase), "_nodeName", (string)config["NodeName"]);
            Set(node, typeof(NodeBase), "_active", (bool)config["Active"]);
            node.Process = process;
            node.ParamForm = new Parameter { Params = param };
            process.Nodes.Add(node);
            if (node is NodeCompositeModule)
            {
                Set(node, typeof(NodeCompositeModule), "_runtimeSync", new object());
                Set(node, typeof(NodeCompositeModule), "_cachedParam", param);
                Set(node, typeof(NodeCompositeModule), "_runtimeProcess", ReadScope((JObject)wrapped.Value));
            }
        }
        return process;
    }
    /// <summary>验证真实方案各配方、各视角只关联自身AI与角度行。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            LanguageManager.Initialize();
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyMeasurementTypes();
            var scheme = JObject.Parse(File.ReadAllText(args[0]));
            if (args.Contains("--load"))
            {
                VerifyRealLoad(scheme, args.Contains("--run-angles"));
                Console.WriteLine("真实模块参数加载通过: " + checks);
                return 0;
            }
            Solution.Instance.DetectItemDic = scheme["DetectItemDic"].ToObject<Dictionary<string, List<DetectItemInfo>>>();
            var root = ReadScope((JObject)scheme["ProcessInfos"].Single(p => (int)p["ID"] == 5));
            if (args.Contains("--failure-isolation"))
            {
                VerifyLatestSchemeViewToggle(ReadScope((JObject)scheme["ProcessInfos"].Single(p => (int)p["ID"] == 5)));
                VerifyViewFailureIsolation(root);
                Console.WriteLine("单视角失败隔离检查通过: " + checks);
                return 0;
            }
            var viewer = (FrmSingleImage)FormatterServices.GetUninitializedObject(typeof(FrmSingleImage));
            foreach (int id in new[] { 79, 358, 591 })
            {
                var recipe = ((NodeCompositeModule)root.Nodes.Single(n => n.ID == id)).GetEditableProcess();
                var aiModule = recipe.Nodes.OfType<NodeCompositeModule>().SingleOrDefault(n => n.NodeName == "组合模块_AI学习");
                var aiScope = aiModule == null ? recipe : aiModule.GetEditableProcess();
                foreach (int overlayId in id == 591 ? new[] { 295 } : new[] { 295, 296, 297, 298 })
                {
                    var show = recipe.Nodes.OfType<NodeImageShow>().Single(n => ((NodeParamImageShow)n.ParamForm.Params).Text1.StartsWith(overlayId + "."));
                    viewer.FormName = ((NodeParamImageShow)show.ParamForm.Params).WindowName;
                    Set(viewer, typeof(FrmSingleImage), "_lastImageShowNodeId", (int?)show.ID);
                    var expectedAi = aiScope.Nodes.Single(n => n.ID == overlayId - 5);
                    Check(ReferenceEquals(Call(viewer, "ResolveTargetAiNode", recipe, false), expectedAi), "画面AI串用: " + id + "/" + overlayId);
                    var filter = (RunParamDisplayFilter)Call(viewer, "ResolveRunParamDisplayFilter", recipe);
                    Check(ReferenceEquals(Call(viewer, "ResolveManualTuningTarget", recipe, filter), expectedAi), "手动调参目标不一致");
                    using (var control = new SolRunParamControl())
                    {
                        control.Init(recipe, (NodeTDAI)expectedAi, filter);
                        var field = typeof(SolRunParamControl).GetField("_nodeCrop", BindingFlags.Instance | BindingFlags.NonPublic);
                        Check(((NodeBase)field.GetValue(control)).ID == overlayId - 9, "模块输入未追踪到原裁剪节点");
                        var rows = (IList)Call(control, "BuildRunParamGridItems");
                        int actualAngles = rows.Cast<object>().Count(row => row.GetType().GetProperty("SourceType").GetValue(row).ToString() == "MultiCondition");
                        var overlay = (NodeParamResultOverlayDraw)recipe.Nodes.Single(n => n.ID == overlayId).ParamForm.Params;
                        int expectedAngles = overlay.Items.Where(item => item.Enabled).Select(item => CompositeSubscriptionResolver.Find(recipe, 0, item.SourceText1))
                            .OfType<NodeCompositeModule>().Any(m => m.NodeName.Contains("角度")) ? 1 : 0;
                        Check(actualAngles == expectedAngles, "角度参数跨视角串用: " + id + "/" + overlayId + " 实际=" + actualAngles);
                    }
                }
                Console.WriteLine("配方关联通过: " + id);
            }
            VerifyJudgment(root);
            VerifyLearning(root);
            Console.WriteLine("Station2ModuleSplit: " + checks + " assertions passed");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>用真实节点及参数窗体恢复嵌套模块，延迟模型加载并禁止设备连接。</summary>
    private static void VerifyRealLoad(JObject scheme, bool runAngles)
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        foreach (string window in scheme.Descendants().OfType<JProperty>().Where(p => p.Name == "WindowName")
            .Select(p => (string)p.Value).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct())
            FrmImageViewer.FrmSingleImages.Add(new FrmSingleImage(window));
        var gate = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
        using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null))
        {
            var station = scheme["ProcessInfos"].Single(p => (int)p["ID"] == 5);
            foreach (JObject recipe in station["NodeInfos"].Where(n => new[] { 79, 358, 591 }.Contains((int)n["ID"])))
            {
                var recipeParam = ((JObject)recipe["NodeParam"]).Properties().Single().Value;
                var process = new TDJS_Vision.Process("模块恢复验证");
                using (var module = new NodeCompositeModule((int)recipe["ID"], (string)recipe["NodeName"], process, NodeType.CompositeModule))
                {
                    module.ParamForm.Params = recipeParam.ToObject<NodeParamCompositeModule>();
                    process.Nodes.Add(module);
                    var inner = module.GetEditableProcess();
                    foreach (var group in inner.Nodes.OfType<NodeCompositeModule>().Where(n => n.NodeName == "组合模块_AI学习" || n.NodeName.Contains("角度")))
                    {
                        var restored = group.GetEditableProcess();
                        var configuration = (NodeParamCompositeModule)group.ParamForm.Params;
                        VerifyRoiCatalog(group);
                        Check(restored.Nodes.Count == configuration.NodeInfos.Count, "模块内部节点恢复不完整");
                        foreach (var output in configuration.OutputPorts)
                        {
                            var source = restored.Nodes.Single(n => n.ID == output.SourceNodeId);
                            Check(source.Result.GetType().GetProperty(output.PropertyPath) != null, "输出属性不存在: " + output.Name);
                        }
                        foreach (var ai in restored.Nodes.OfType<NodeTDAI>())
                        {
                            var subscription = ai.ParamForm.GetType().GetField("nodeSubscription1", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ai.ParamForm);
                            var selected = subscription.GetType().GetMethod("GetSelectedNode").Invoke(subscription, null) as NodeBase;
                            Check(selected != null && selected.ID == int.MaxValue, "AI图像输入没有绑定模块端口");
                        }
                        if (runAngles)
                        {
                            Check(group.CanvasSize == new System.Drawing.Size(240, 64), "模块尺寸不符合默认尺寸");
                            Check(restored.Nodes.Count(n => n.IsStartNode) == 1, "角度模块不是单入口");
                            var contour = (NodeParamContourMatch)restored.Nodes.First(n => n.ParamForm.Params is NodeParamContourMatch).ParamForm.Params;
                            var pixels = contour.Templates.First(t => t.Enabled).Model.ModelImageBytes;
                            var imageSource = inner.Nodes.Single(n => n.ID == configuration.InputBindings.Single().SourceNodeId);
                            ((NodeResultImageRotate)imageSource.Result).OutputImage = OutputImage.FromOwnedSingleImage(Mat.FromImageData(pixels, ImreadModes.Color));
                            SynchronizationContext.SetSynchronizationContext(null);
                            group.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                            Check(group.RuntimeStatus == NodeStatus.Successful, "模板原图运行失败: " + group.NodeName);
                            VerifyRoiCatalog(group);
                            Console.WriteLine("模板原图运行通过: " + recipe["ID"] + "/" + group.NodeName);
                        }
                        Console.WriteLine("真实加载通过: " + recipe["ID"] + "/" + group.NodeName);
                    }
                }
            }
        }
    }
    /// <summary>使用绘制窗体实际契约验证模块ROI候选，覆盖未运行、运行后和保存恢复后的声明类型。</summary>
    private static void VerifyRoiCatalog(NodeCompositeModule group)
    {
        var method = typeof(NodeParamFormResultOverlayDraw).GetMethod("CreateRoiInputContract", BindingFlags.NonPublic | BindingFlags.Static);
        var contract = (SubscriptionInputContract)method.Invoke(null, null);
        var candidates = SubscriptionPortCatalog.GetOutputs(group, contract, false, null);
        var parameter = (NodeParamCompositeModule)group.ParamForm.Params;
        foreach (var port in parameter.OutputPorts.Where(p => p.PropertyPath == "Items" && p.ValueTypeName.Contains("LineLineAngleTargetResult")))
        {
            var descriptor = candidates.SingleOrDefault(p => p.PropertyPath == DynamicResultVariableResolver.ToPropertyPath(port.Name));
            Check(descriptor != null, "ROI候选丢失夹角集合: " + group.ID);
            Check(descriptor.Category == SubscriptionDataCategory.MeasurementResult && descriptor.Multiplicity == SubscriptionValueMultiplicity.MultiTarget, "夹角集合分类或数量形态丢失");
        }
        Check(!candidates.Any(p => p.Category == SubscriptionDataCategory.Number || p.Category == SubscriptionDataCategory.Boolean), "ROI错误接受标量结果");
        var explanation = SubscriptionPortCatalog.ExplainOutputs(group, contract, null);
        Check(explanation.Contains("组合模块仅公开已配置") && explanation.Contains("当前输入不支持"), "缺少模块导出范围或不兼容原因");
        Check(SubscriptionPortCatalog.ExplainOutputs(group, contract, "$variable:不存在的端口").Contains("已不存在"), "原端口缺失未说明原因");
        var generic = SubscriptionPortCatalog.GetOutputs(group, SubscriptionInputContract.ForType(typeof(object)), false, null);
        Check(candidates.All(p => generic.Any(q => q.PropertyPath == p.PropertyPath)), "通用输入错误过滤高级ROI结果");
        Check(SubscriptionPortCatalog.ExplainOutputs(null, contract, null).Contains("请先选择"), "空来源缺少说明");
    }

    /// <summary>所有实现公共接口的测量工具都应支持集合输出，避免只修复夹角工具。</summary>
    private static void VerifyMeasurementTypes()
    {
        var contract = typeof(TDJS_Vision.Node._4_Measurement.Common.IMultiTargetMeasurementItem);
        foreach (var type in typeof(NodeBase).Assembly.GetTypes().Where(t => !t.IsAbstract && contract.IsAssignableFrom(t)))
        {
            foreach (var collection in new[] { type.MakeArrayType(), typeof(List<>).MakeGenericType(type) })
            {
                Check(SubscriptionTypeCompatibility.ResolveCategory(collection) == SubscriptionDataCategory.MeasurementResult, "测量集合分类丢失: " + type.Name);
                Check(SubscriptionTypeCompatibility.ResolveMultiplicity(collection) == SubscriptionValueMultiplicity.MultiTarget, "测量集合数量形态丢失: " + type.Name);
            }
        }
        Check(SubscriptionTypeCompatibility.ResolveCategory(typeof(List<object>)) == SubscriptionDataCategory.StructuredObject, "未知集合被误认为ROI");
    }
    /// <summary>通过实际学习服务保存临时方案，确认仅选中的内部AI开启学习。</summary>
    private static void VerifyLearning(TDJS_Vision.Process root)
    {
        var recipe = ((NodeCompositeModule)root.Nodes.Single(n => n.ID == 358)).GetEditableProcess();
        var module = recipe.Nodes.OfType<NodeCompositeModule>().SingleOrDefault(n => n.NodeName == "组合模块_AI学习");
        var child = module == null ? recipe : module.GetEditableProcess();
        var targets = child.Nodes.OfType<NodeTDAI>().ToList();
        Solution.Instance.AllProcesses.Clear();
        Solution.Instance.AllProcesses.Add(root);
        string temporary = Path.Combine(Path.GetTempPath(), "Station2ModuleLearning-" + Guid.NewGuid().ToString("N") + ".Sol");
        Solution.Instance.SolFileName = temporary;
        UserPermissionContext.UpdateRole(UserRole.Hight);
        Check(ProcessTuningService.StartOneClickLearning(recipe, targets[1]), "组合内部学习入口未执行");
        Check(targets.Select(n => ((NodeParamTDAI)n.ParamForm.Params).IsAutoStudy).SequenceEqual(new[] { false, true, false, false }), "学习串用其他视角");
        var saved = JObject.Parse(File.ReadAllText(temporary));
        Check(saved.Descendants().OfType<JProperty>().Count(p => p.Name == "IsAutoStudy" && p.Value.Type == JTokenType.Boolean && (bool)p.Value) == 1, "学习状态未准确保存到嵌套方案");
        Console.WriteLine("学习持久化通过，使用临时方案: " + temporary);
    }
    /// <summary>读取最新方案的完整0/90度绘制配置，以确定性检测结果验证独立切换，不执行设备通信或保存方案。</summary>
    private static void VerifyLatestSchemeViewToggle(TDJS_Vision.Process root)
    {
        var scope = ((NodeCompositeModule)root.Nodes.Single(n => n.ID == 358)).GetEditableProcess();
        Set(scope, typeof(TDJS_Vision.Process), "<CurrentRunId>k__BackingField", 321);
        scope.IsRuning = true;
        foreach (var ai in scope.Nodes.OfType<NodeTDAI>())
        {
            var result = new NodeResultTDAI();
            result.AlgorithmResult.Rects.Add(new ColorRotatedRect(100 + ai.ID, 20, 10, 10));
            result.AlgorithmResult.Texts.Add(new ColorText("AI学习绘制" + ai.ID, System.Drawing.Color.Green));
            Set(ai, typeof(NodeBase), "_result", result);
            Set(ai, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
            Set(ai, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 321);
        }
        var judge = scope.Nodes.OfType<NodeMultiCondition>().Single();
        var parameter = (NodeParamMultiCondition)judge.ParamForm.Params;
        var selected = new[] { parameter.Conditions.Single(c => c.Name == "条件7"), parameter.Conditions.Single(c => c.Name == "条件8") };
        var collect = typeof(NodeCompositeModule).GetMethod("CollectOutputValues", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var condition in selected)
        {
            var module = (NodeCompositeModule)scope.Nodes.Single(n => n.ID == condition.SourceNodeId);
            var inner = module.GetEditableProcess();
            Set(inner, typeof(TDJS_Vision.Process), "<CurrentRunId>k__BackingField", 51);
            inner.IsRuning = true;
            foreach (var port in ((NodeParamCompositeModule)module.ParamForm.Params).OutputPorts)
            {
                var leaf = inner.Nodes.Single(n => n.ID == port.SourceNodeId);
                if (leaf.Result != null) continue;
                INodeResult result;
                if (leaf.ParamForm.Params.GetType().Name.Contains("BlobAnalysis"))
                {
                    var blob = new TDJS_Vision.Node._4_Measurement.BlobAnalysis.NodeResultBlobAnalysis { IsOk = true, Area = 0 };
                    blob.Items.Add(new TDJS_Vision.Node._4_Measurement.BlobAnalysis.NodeResultBlobAnalysis { IsOk = true, Area = 0, TargetIndex = 1 });
                    blob.DetectionRegions = new List<ColorRotatedRect> { new ColorRotatedRect(2000 + module.ID, 20, 10, 10) };
                    result = blob;
                }
                else
                {
                    var angle = new NodeResultLineLineAngle { IsOk = true, Angle = 0.5 };
                    angle.Result.Rects.Add(new ColorRotatedRect(1000 + module.ID, 20, 10, 10));
                    result = angle;
                }
                Set(leaf, typeof(NodeBase), "_result", result);
                Set(leaf, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
                Set(leaf, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 51);
            }
            var published = new NodeResultCompositeModule { InternalSuccess = true, OutputRunId = 321 };
            collect.Invoke(null, new object[] { published, inner, (NodeParamCompositeModule)module.ParamForm.Params, false });
            Set(module, typeof(NodeBase), "_result", published);
            Set(module, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
            Set(module, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 321);
        }
        var evaluator = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionEvaluator").GetMethod("Evaluate");
        var build = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder").GetMethod("BuildDisplayResult", BindingFlags.NonPublic | BindingFlags.Static);
        var baselines = new string[2];
        // 先建立全部禁用的基线，再分别启用、同时启用、再次禁用及恢复。
        foreach (var state in new[] { new[] { false, false }, new[] { true, false }, new[] { false, true }, new[] { true, true }, new[] { false, false }, new[] { true, true } })
        {
            selected[0].Enabled = state[0]; selected[1].Enabled = state[1];
            var arguments = new object[] { judge, parameter, null };
            bool ok = (bool)evaluator.Invoke(null, arguments);
            Set(judge, typeof(NodeBase), "_result", new NodeResultMultiCondition { ConditionResult = ok, Details = (List<NodeConditionEvaluation>)arguments[2] });
            Set(judge, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
            Set(judge, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 321);
            for (int view = 0; view < 2; view++)
            {
                var draw = scope.Nodes.Single(n => n.ID == (view == 0 ? 295 : 298));
                var drawParam = (NodeParamResultOverlayDraw)draw.ParamForm.Params;
                var actual = (AlgorithmResult)build.Invoke(null, new object[] { draw, drawParam, null });
                var verdict = drawParam.Items.Single(i => i.ItemType == ResultOverlayDrawItemType.BooleanText && i.SourceText2 == "变量.单项结果：" + selected[view].Name);
                Check(actual.Rects.Count(r => r.RotatedRect.center.x > 2000) == (state[view] ? 1 : 0), "最新方案Blob ROI切换不正确，视角=" + view);
                Check(actual.Texts.Count(t => t.Text == verdict.BooleanOkText || t.Text == verdict.BooleanNgText) == (state[view] ? 1 : 0), "最新方案Blob判定文字切换不正确，视角=" + view);
                Check(!actual.Texts.Any(t => t.Text.Contains("未查到") || t.Text.Contains("组合判定异常")), "最新方案切换产生绘制异常");
                string unaffected = Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    Rects = actual.Rects.Where(r => r.RotatedRect.center.x < 2000).ToList(),
                    Texts = actual.Texts.Where(t => t.Text != verdict.BooleanOkText && t.Text != verdict.BooleanNgText).ToList(),
                    actual.IsAllOk
                });
                if (baselines[view] == null) baselines[view] = unaffected;
                Check(baselines[view] == unaffected, "切换Blob影响角度、AI学习或整体绘制，视角=" + view);
            }
            Console.WriteLine("最新方案完整绘制切换通过：0度=" + state[0] + "，90度=" + state[1] + "；角度与AI学习绘制不变。");
        }
    }

    /// <summary>按客户实际条件7/8复现90度失败，0度单项必须保留，总结果仍为NG。</summary>
    private static void VerifyViewFailureIsolation(TDJS_Vision.Process root)
    {
        var scope = ((NodeCompositeModule)root.Nodes.Single(n => n.ID == 358)).GetEditableProcess();
        var judge = scope.Nodes.OfType<NodeMultiCondition>().Single();
        var conditions = ((NodeParamMultiCondition)judge.ParamForm.Params).Conditions.Where(c => c.Name == "条件7" || c.Name == "条件8").ToList();
        Check(conditions.Count == 2, "缺少客户实际飞丝条件7/8");
        // 客户方案可能已禁用判定；仅在测试内启用副本，继续验证异常隔离场景。
        foreach (var condition in conditions) condition.Enabled = true;
        var good = scope.Nodes.Single(n => n.ID == conditions[0].SourceNodeId);
        var bad = scope.Nodes.Single(n => n.ID == conditions[1].SourceNodeId);
        var leaf = CompositeSubscriptionResolver.Resolve(good, conditions[0].PropertyPath, out var path);
        var blob = new TDJS_Vision.Node._4_Measurement.BlobAnalysis.NodeResultBlobAnalysis { IsOk = true, Area = 0 };
        blob.Items.Add(new TDJS_Vision.Node._4_Measurement.BlobAnalysis.NodeResultBlobAnalysis { IsOk = true, Area = 0, TargetIndex = 1 });
        Set(leaf, typeof(NodeBase), "_result", blob);
        Set(good, typeof(NodeBase), "_result", new NodeResultCompositeModule { InternalSuccess = true });
        Set(bad, typeof(NodeBase), "_result", new NodeResultCompositeModule());
        Set(bad, typeof(NodeBase), "_runtimeStatus", NodeStatus.Failed);
        Set(scope, typeof(TDJS_Vision.Process), "<CurrentRunId>k__BackingField", 123);
        scope.IsRuning = true;
        Set(good, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
        Set(good, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 123);
        Set(judge, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
        Set(judge, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 123);

        var evaluator = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionEvaluator");
        foreach (var mode in new[] { MultiConditionMatchMode.All, MultiConditionMatchMode.Any })
        {
            var args = new object[] { judge, new NodeParamMultiCondition { Conditions = conditions, MatchMode = mode }, null };
            Check(!(bool)evaluator.GetMethod("Evaluate").Invoke(null, args), "失败上游未强制总NG");
            var details = (List<NodeConditionEvaluation>)args[2];
            var result = new NodeResultMultiCondition { Details = details, ConditionResult = false };
            Check(result.TryGetDynamicVariable("单项结果：条件7", out var goodValue) && goodValue is bool && (bool)goodValue, "90度失败串扰0度正常条件7");
            Check(result.TryGetDynamicVariable("单项结果：条件8", out var badValue) && badValue is bool && !(bool)badValue, "90度异常条件未输出false");
            Check(details.Any(d => d.Name == "上游节点失败"), "上游失败诊断丢失");
            Set(judge, typeof(NodeBase), "_result", result);
            var draw = scope.Nodes.Single(n => n.ID == 295);
            var builder = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder");
            var readArgs = new object[] { draw, new ResultOverlayDrawItem { SourceText1 = "294.多条件判断", SourceText2 = "变量.单项结果：条件7" }, null, null };
            Check((bool)builder.GetMethod("TryReadDrawItemValue", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, readArgs) && (bool)readArgs[2], "0度绘制仍读不到条件7");
        }
        // 真实布尔文本绘制消费异常条件的false，不应因空值类型错误中断。
        var overlayBuilder = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder");
        var display = new AlgorithmResult { IsAllOk = true };
        overlayBuilder.GetMethod("AppendBooleanText", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] {
            scope.Nodes.Single(n => n.ID == 298), display,
            new ResultOverlayDrawItem { SourceText1 = "294.多条件判断", SourceText2 = "变量.单项结果：条件8", BooleanNgText = "NG" },
            new NodeParamResultOverlayDraw() });
        Check(!display.IsAllOk && display.Texts.Any(t => t.Text == "NG"), "异常单项false未完成布尔文本绘制");
        // 其他来源确实返回空值时仍显示未查到，不抛出布尔类型异常。
        var missingDisplay = new AlgorithmResult { IsAllOk = true };
        overlayBuilder.GetMethod("AppendBooleanText", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] {
            scope.Nodes.Single(n => n.ID == 298), missingDisplay,
            new ResultOverlayDrawItem { SourceText1 = "294.多条件判断", SourceText2 = "诊断信息" },
            new NodeParamResultOverlayDraw() });
        Check(!missingDisplay.IsAllOk && missingDisplay.Texts.Any(t => t.Text.Contains("未查到")), "空值未显示未查到提示");
        blob.Items[0].Area = 100;
        var failedArgs = new object[] { judge, new NodeParamMultiCondition { Conditions = conditions }, null };
        evaluator.GetMethod("Evaluate").Invoke(null, failedArgs);
        var failedResult = new NodeResultMultiCondition { Details = (List<NodeConditionEvaluation>)failedArgs[2] };
        Check(failedResult.TryGetDynamicVariable("单项结果：条件7", out var ng) && ng is bool && !(bool)ng, "0度自身超限被错误隐藏或变成未查到");
        // 不依赖失败模块兜底，独立验证配置/类型异常本身也强制总NG且不打断其他项。
        Set(bad, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
        Set(bad, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 123);
        blob.Items[0].Area = 0;
        ((NodeResultCompositeModule)good.Result).ModuleName = "不能转换成布尔的文本";
        var errors = new[] {
            new MultiConditionItem { Name = "异常项", SourceNodeId = 999999, PropertyPath = "Area" },
            new MultiConditionItem { Name = "异常项", SourceNodeId = good.ID, PropertyPath = "不存在属性" },
            new MultiConditionItem { Name = "异常项", SourceNodeId = good.ID, PropertyPath = "$variable:不存在端口", Operator = MultiConditionOperator.IsNull },
            new MultiConditionItem { Name = "异常项", SourceNodeId = good.ID, PropertyPath = "" },
            new MultiConditionItem { Name = "异常项", SourceNodeId = good.ID, PropertyPath = "ModuleName", Operator = MultiConditionOperator.IsTrue },
            new MultiConditionItem { Name = "异常项", SourceNodeId = good.ID, PropertyPath = "InternalNodeCount", Operator = (MultiConditionOperator)999 }
        };
        foreach (var error in errors)
        foreach (var mode in new[] { MultiConditionMatchMode.All, MultiConditionMatchMode.Any })
        foreach (bool first in new[] { true, false })
        {
            var list = first ? new List<MultiConditionItem> { error, conditions[0] } : new List<MultiConditionItem> { conditions[0], error };
            var isolatedArgs = new object[] { judge, new NodeParamMultiCondition { Conditions = list, MatchMode = mode }, null };
            Check(!(bool)evaluator.GetMethod("Evaluate").Invoke(null, isolatedArgs), "单项异常未强制总NG: " + error.PropertyPath);
            var details = (List<NodeConditionEvaluation>)isolatedArgs[2];
            Check(details.Count == 2, "单项异常导致明细丢失或重复");
            var output = new NodeResultMultiCondition { Details = details };
            Check(output.TryGetDynamicVariable("单项结果：条件7", out var valid) && valid is bool && (bool)valid, "异常条件影响正常条件");
            Check(output.TryGetDynamicVariable("单项结果：异常项", out var invalid) && invalid is bool && !(bool)invalid && details.Any(d => d.SourceUnavailable && !string.IsNullOrEmpty(d.NullReason)), "异常项未保留不可用原因");
        }
        // 同模块角度失败、Blob正常：从实际端口收集到条件计算及绘制读取均须独立。
        var badLeaf = CompositeSubscriptionResolver.Resolve(bad, conditions[1].PropertyPath, out _);
        var inner = badLeaf.Process;
        Set(inner, typeof(TDJS_Vision.Process), "<CurrentRunId>k__BackingField", 41);
        inner.IsRuning = true;
        Set(badLeaf, typeof(NodeBase), "_result", blob);
        blob.JudgeOk = true;
        blob.Area = 0;
        blob.Items[0].Area = 0;
        blob.DetectionRegions = new List<ColorRotatedRect> { new ColorRotatedRect(10, 10, 20, 20) };
        Set(badLeaf, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
        Set(badLeaf, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 41);
        Set(bad, typeof(NodeBase), "_runtimeStatus", NodeStatus.Failed);
        var partial = new NodeResultCompositeModule { InternalSuccess = false, OutputRunId = 123 };
        typeof(NodeCompositeModule).GetMethod("CollectOutputValues", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { partial, inner, (NodeParamCompositeModule)bad.ParamForm.Params, true });
        Set(bad, typeof(NodeBase), "_result", partial);
        Check(partial.OutputErrors.Count > 0, "失败角度端口未保留原因");
        Check(partial.TryGetDynamicVariable("下棱镜飞丝检测面积", out var area) && Convert.ToDouble(area) == 0, "同模块Blob面积丢失");
        Check(partial.TryGetDynamicVariable("下棱镜飞丝检测ROI", out var roi) && ReferenceEquals(roi, blob.DetectionRegions), "同模块Blob ROI丢失");
        foreach (var mode in new[] { MultiConditionMatchMode.All, MultiConditionMatchMode.Any })
        {
            var partialArgs = new object[] { judge, new NodeParamMultiCondition { Conditions = conditions, MatchMode = mode }, null };
            Check(!(bool)evaluator.GetMethod("Evaluate").Invoke(null, partialArgs), "部分端口可用掩盖模块总NG");
            var partialResult = new NodeResultMultiCondition { Details = (List<NodeConditionEvaluation>)partialArgs[2] };
            Check(partialResult.TryGetDynamicVariable("单项结果：条件8", out var matched) && matched is bool && (bool)matched, "角度失败串扰同模块Blob条件");
        }
        foreach (string port in new[] { "下棱镜飞丝检测面积", "下棱镜飞丝检测ROI" })
        {
            var read = new object[] { scope.Nodes.Single(n => n.ID == 298), new ResultOverlayDrawItem { SourceText1 = "654." + bad.NodeName, SourceText2 = "变量." + port }, null, null };
            Check((bool)overlayBuilder.GetMethod("TryReadDrawItemValue", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, read) && read[2] != null, "绘制未读到部分有效端口:" + port);
        }
        var blobDisplay = new AlgorithmResult { IsAllOk = true };
        overlayBuilder.GetMethod("AppendAutomaticRoi", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] {
            scope.Nodes.Single(n => n.ID == 298), blobDisplay,
            new ResultOverlayDrawItem { SourceText1 = "654." + bad.NodeName, SourceText2 = "变量.下棱镜飞丝检测ROI" },
            new NodeParamResultOverlayDraw() });
        Check(blobDisplay.IsAllOk && blobDisplay.Rects.Count == 1, "角度失败使正常Blob ROI绘制缺失或变NG");
        var subscription = (NodeSubscription)FormatterServices.GetUninitializedObject(typeof(NodeSubscription));
        Set(subscription, typeof(NodeSubscription), "_node", judge);
        Set(subscription, typeof(NodeSubscription), "_selectedNode", bad);
        Set(subscription, typeof(NodeSubscription), "_text2", "变量.下棱镜飞丝检测面积");
        Check(subscription.GetValue<double>() == 0, "通用订阅控件拒绝正常Blob面积");
        VerifyDisabledOverlay(scope, judge, bad, conditions[1]);
        VerifyNestedPartialOutputs(bad, badLeaf, blob);
        blob.Area = blob.Items[0].Area = 100;
        var blobNgArgs = new object[] { judge, new NodeParamMultiCondition { Conditions = conditions }, null };
        evaluator.GetMethod("Evaluate").Invoke(null, blobNgArgs);
        var blobNgResult = new NodeResultMultiCondition { Details = (List<NodeConditionEvaluation>)blobNgArgs[2] };
        Check(blobNgResult.TryGetDynamicVariable("单项结果：条件8", out var blobNg) && blobNg is bool && !(bool)blobNg,
            "部分输出错误地放过Blob本身的超限");
        partial.OutputRunId = 122;
        Check(!CompositeSubscriptionResolver.CanRead(bad, conditions[1].PropertyPath, true), "读取了旧批次部分结果");
        partial.OutputRunId = 123;
        Set(badLeaf, typeof(NodeBase), "_runtimeStatus", NodeStatus.Failed);
        var unavailable = new NodeResultCompositeModule();
        typeof(NodeCompositeModule).GetMethod("CollectOutputValues", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { unavailable, inner, (NodeParamCompositeModule)bad.ParamForm.Params, true });
        Check(!unavailable.TryGetDynamicVariable("下棱镜飞丝检测面积", out _), "Blob本身失败仍发布旧面积");
        inner.IsRuning = false;
        scope.IsRuning = false;
        Set(bad, typeof(NodeBase), "_runtimeStatus", NodeStatus.Unexecuted);
        Console.WriteLine("视角失败隔离通过：90度失败、0度条件7可读、All/Any总NG、失败项输出false。");
    }

    /// <summary>真实绘制入口验证禁用隐藏、重新启用恢复及共享ROI不误隐藏，不修改方案文件。</summary>
    private static void VerifyDisabledOverlay(TDJS_Vision.Process scope, NodeMultiCondition judge, NodeBase module, MultiConditionItem condition)
    {
        var owner = scope.Nodes.Single(n => n.ID == 298);
        var parameter = new NodeParamResultOverlayDraw { Items = new List<ResultOverlayDrawItem> {
            new ResultOverlayDrawItem { ItemType = ResultOverlayDrawItemType.Roi, SourceText1 = module.ID + "." + module.NodeName, SourceText2 = "变量.下棱镜飞丝检测ROI" },
            new ResultOverlayDrawItem { ItemType = ResultOverlayDrawItemType.BooleanText, SourceText1 = judge.ID + "." + judge.NodeName, SourceText2 = "变量.单项结果：" + condition.Name, BooleanOkText = "无飞丝", BooleanNgText = "飞丝" },
            new ResultOverlayDrawItem { ItemType = ResultOverlayDrawItemType.Text, UseManualText = true, ManualText = "其他检测项" }
        } };
        Set(judge, typeof(NodeBase), "_result", new NodeResultMultiCondition { Details = new List<NodeConditionEvaluation> {
            new NodeConditionEvaluation { Name = condition.Name, IsEnabled = true, IsMatched = true }
        } });
        var build = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder").GetMethod("BuildDisplayResult", BindingFlags.NonPublic | BindingFlags.Static);
        Func<AlgorithmResult> render = () => (AlgorithmResult)build.Invoke(null, new object[] { owner, parameter, null });
        condition.Enabled = false;
        var hidden = render();
        Check(hidden.Rects.Count == 0 && hidden.Texts.Count == 1 && hidden.Texts[0].Text == "其他检测项" && hidden.IsAllOk, "禁用Blob未同时隐藏ROI和文字或影响其他项: ROI=" + hidden.Rects.Count + ", OK=" + hidden.IsAllOk + ", 文本=" + string.Join("|", hidden.Texts.Select(t => t.Text)));
        var shared = new MultiConditionItem { Name = "同源其他判断", SourceNodeId = module.ID, PropertyPath = condition.PropertyPath, Enabled = true };
        var conditions = ((NodeParamMultiCondition)judge.ParamForm.Params).Conditions;
        conditions.Add(shared);
        Check(render().Rects.Count == 1 && render().Texts.Count == 1, "共享ROI被禁用的单项误隐藏");
        conditions.Remove(shared);
        condition.Enabled = true;
        var restored = render();
        Check(restored.Rects.Count == 1 && restored.Texts.Any(t => t.Text == "无飞丝") && restored.Texts.Any(t => t.Text == "其他检测项"), "重新启用未恢复ROI和文字");
        Check(parameter.Items.All(i => i.Enabled), "运行时跳过修改了绘制配置");
    }

    /// <summary>使用任意编号和三层嵌套，验证新旧输出方式混合时正常端口独立向外发布。</summary>
    private static void VerifyNestedPartialOutputs(NodeBase source, NodeBase leaf,
        TDJS_Vision.Node._4_Measurement.BlobAnalysis.NodeResultBlobAnalysis blob)
    {
        string areaName = "下棱镜飞丝检测面积";
        string roiName = "下棱镜飞丝检测ROI";
        string failedName = "线到线夹角_2_夹角";
        var collect = typeof(NodeCompositeModule).GetMethod("CollectOutputValues", BindingFlags.NonPublic | BindingFlags.Static);
        for (int depth = 1; depth <= 3; depth++)
        {
            var child = source.Process;
            var ports = new List<CompositeOutputPortDefinition> {
                new CompositeOutputPortDefinition { Name = "正常面积", SourceNodeId = source.ID, PropertyPath = "$variable:" + areaName },
                new CompositeOutputPortDefinition { Name = "正常ROI", SourceNodeId = source.ID, PropertyPath = "$variable:" + roiName },
                new CompositeOutputPortDefinition { Name = "失败角度", SourceNodeId = source.ID, PropertyPath = "$variable:" + failedName }
            };
            var parameter = new NodeParamCompositeModule { UsesDirectOutputs = depth != 2, OutputPorts = ports };
            if (depth == 2)
            {
                // 旧式输出节点整体失败，不能因此丢失其声明的正常端口。
                var legacy = (NodeBase)FormatterServices.GetUninitializedObject(typeof(NodeBase));
                Set(legacy, typeof(NodeBase), "_active", true);
                Set(legacy, typeof(NodeBase), "_runtimeStatus", NodeStatus.Failed);
                legacy.Process = child;
                legacy.ParamForm = new Parameter { Params = new NodeParamCompositeOutput { Ports = ports } };
                child.Nodes.Add(legacy);
            }
            var parent = (TDJS_Vision.Process)FormatterServices.GetUninitializedObject(typeof(TDJS_Vision.Process));
            Set(parent, typeof(TDJS_Vision.Process), "_nodes", new List<NodeBase>());
            Set(parent, typeof(TDJS_Vision.Process), "_connections", new List<ProcessConnection>());
            Set(parent, typeof(TDJS_Vision.Process), "<CurrentRunId>k__BackingField", 700 + depth);
            parent.IsRuning = true;
            var wrapper = (NodeCompositeModule)FormatterServices.GetUninitializedObject(typeof(NodeCompositeModule));
            wrapper.Process = parent;
            wrapper.ParamForm = new Parameter { Params = parameter };
            Set(wrapper, typeof(NodeBase), "_id", 8000 + depth);
            Set(wrapper, typeof(NodeBase), "_active", true);
            Set(wrapper, typeof(NodeBase), "_runtimeStatus", NodeStatus.Failed);
            Set(wrapper, typeof(NodeCompositeModule), "_runtimeProcess", child);
            Set(wrapper, typeof(NodeCompositeModule), "_cachedParam", parameter);
            Set(wrapper, typeof(NodeCompositeModule), "_runtimeSync", new object());
            var result = new NodeResultCompositeModule { InternalSuccess = false, OutputRunId = parent.CurrentRunId };
            collect.Invoke(null, new object[] { result, child, parameter, true });
            Set(wrapper, typeof(NodeBase), "_result", result);
            parent.Nodes.Add(wrapper);
            Check(result.TryGetDynamicVariable("正常面积", out var value) && Convert.ToDouble(value) == 0, "嵌套面积丢失，层数=" + depth);
            Check(result.TryGetDynamicVariable("正常ROI", out var roi) && ReferenceEquals(roi, blob.DetectionRegions), "嵌套ROI丢失，层数=" + depth);
            Check(!result.TryGetDynamicVariable("失败角度", out _) && result.OutputErrors.ContainsKey("失败角度"), "嵌套失败端口未隔离");
            Check(CompositeSubscriptionResolver.CanRead(wrapper, "变量.正常面积", true) && !CompositeSubscriptionResolver.CanRead(wrapper, "变量.失败角度", true), "嵌套端口可用性错误");
            Check(ReferenceEquals(CompositeSubscriptionResolver.Resolve(wrapper, "$variable:正常面积", out var path), leaf) && path == "Area", "嵌套未解析到真实测量节点");
            result.OutputRunId--;
            Check(!CompositeSubscriptionResolver.CanRead(wrapper, "变量.正常面积", true), "嵌套旧批次被误用");
            result.OutputRunId++;
            // 外层禁用应控制该层ROI；内部仍启用的同源判断不能阻止外层隐藏。
            var judge = (NodeMultiCondition)FormatterServices.GetUninitializedObject(typeof(NodeMultiCondition));
            judge.Process = parent;
            Set(judge, typeof(NodeBase), "_id", 9000 + depth);
            Set(judge, typeof(NodeBase), "_active", true);
            var condition = new MultiConditionItem { Name = "嵌套判定", SourceNodeId = wrapper.ID, PropertyPath = "$variable:正常面积", Enabled = false };
            judge.ParamForm = new Parameter { Params = new NodeParamMultiCondition { Conditions = new List<MultiConditionItem> { condition } } };
            parent.Nodes.Add(judge);
            var owner = (NodeBase)FormatterServices.GetUninitializedObject(typeof(NodeBase));
            owner.Process = parent;
            Set(owner, typeof(NodeBase), "_id", 10000 + depth);
            parent.Nodes.Add(owner);
            parent.Connections.Add(new ProcessConnection { FromNodeId = wrapper.ID, ToNodeId = owner.ID });
            var drawParam = new NodeParamResultOverlayDraw { Items = new List<ResultOverlayDrawItem> {
                new ResultOverlayDrawItem { ItemType = ResultOverlayDrawItemType.Roi, SourceText1 = wrapper.ID + "." + wrapper.NodeName, SourceText2 = "变量.正常ROI" }
            } };
            var build = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder").GetMethod("BuildDisplayResult", BindingFlags.NonPublic | BindingFlags.Static);
            Check(((AlgorithmResult)build.Invoke(null, new object[] { owner, drawParam, null })).Rects.Count == 0, "嵌套禁用未隐藏ROI，层数=" + depth);
            condition.Enabled = true;
            Check(((AlgorithmResult)build.Invoke(null, new object[] { owner, drawParam, null })).Rects.Count == 1, "嵌套重新启用未恢复ROI，层数=" + depth);
            source = wrapper;
            areaName = "正常面积"; roiName = "正常ROI"; failedName = "失败角度";
        }
        Console.WriteLine("任意编号、三层嵌套及新旧输出混合隔离通过。");
    }

    /// <summary>验证第二目标超限仍判NG，并回写内部结果供图像着色。</summary>
    private static void VerifyJudgment(TDJS_Vision.Process root)
    {
        var scope = ((NodeCompositeModule)root.Nodes.Single(n => n.ID == 79)).GetEditableProcess();
        var conditionNode = scope.Nodes.OfType<NodeMultiCondition>().Single();
        var original = ((NodeParamMultiCondition)conditionNode.ParamForm.Params).Conditions.Last();
        var module = scope.Nodes.Single(n => n.ID == original.SourceNodeId);
        var angle = CompositeSubscriptionResolver.Resolve(module, original.PropertyPath, out var path);
        var result = new NodeResultLineLineAngle { Angle = 0, JudgeOk = true, IsOk = true };
        result.Items.Add(new LineLineAngleTargetResult { Angle = 0, IsOk = true });
        result.Items.Add(new LineLineAngleTargetResult { Angle = 8, IsOk = true });
        Set(angle, typeof(NodeBase), "_result", result);
        Set(module, typeof(NodeBase), "_result", new NodeResultCompositeModule { InternalSuccess = true });
        var evaluator = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionEvaluator");
        var method = evaluator.GetMethod("Evaluate", BindingFlags.Static | BindingFlags.Public);
        var args = new object[] { conditionNode, new NodeParamMultiCondition { Conditions = new List<MultiConditionItem> { original } }, null };
        Check(!(bool)method.Invoke(null, args), "模块输出把多目标条件降级成首目标");
        Check(!result.JudgeOk, "没有回写内部角度判定");
        var moduleResult = (NodeResultCompositeModule)module.Result;
        var definition = ((NodeParamCompositeModule)module.ParamForm.Params).OutputPorts.First(p => p.SourceNodeId == angle.ID && (p.PropertyPath == "Items" || p.PropertyPath == "Result"));
        var geometry = result.GetType().GetProperty(definition.PropertyPath).GetValue(result);
        moduleResult.OutputValues[definition.Name] = geometry;
        var drawing = scope.Nodes.OfType<NodeResultOverlayDraw>().First();
        var drawArgs = new object[] { drawing, module.ID + "." + module.NodeName, "变量." + definition.Name, null };
        var builder = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder");
        var drawn = builder.GetMethod("ReadSubscribedValue").Invoke(null, drawArgs);
        Check(ReferenceEquals(drawn, geometry) && ReferenceEquals(drawArgs[3], angle), "绘制没有保留原始测量与判定颜色来源");
        result.Items[1].Angle = 1;
        result.JudgeOk = true;
        Check((bool)method.Invoke(null, args), "全部目标合格却判NG");
        original.Enabled = false;
        result.Items[1].Angle = 8;
        Check((bool)method.Invoke(null, args), "禁用条件仍参与判定");
    }
    /// <summary>仅存放参数，避免测试触发相机与AI模型。</summary>
    private sealed class Parameter : INodeParamForm
    {
        /// <summary>原始节点参数。</summary>
        public INodeParam Params { get; set; }
        /// <summary>无需绑定实际参数窗体。</summary>
        public void SetNodeBelong(NodeBase node) { }
        /// <summary>无需加载实际模型。</summary>
        public void SetParam2Form() { }
    }
}
