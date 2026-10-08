using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;

/// <summary>使用真实程序集验证单画面定位、参数隔离与学习；只保存独立临时方案。</summary>
internal static class SingleImageTuningTests
{
    /// <summary>断言计数。</summary>
    private static int checks;
    /// <summary>验证条件。</summary>
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    /// <summary>调用实际私有方法，不运行设备或模型。</summary>
    private static object Call(object obj, string name, params object[] args)
    {
        var type = obj as Type ?? obj.GetType();
        var method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        return method.Invoke(obj is Type ? null : obj, args);
    }
    /// <summary>读取私有字段以确认控件选中了正确对象。</summary>
    private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    /// <summary>设置测试对象内部状态。</summary>
    private static void Set(object obj, Type type, string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    /// <summary>创建不包含执行线程的流程目录。</summary>
    private static TDJS_Vision.Process CreateProcess()
    {
        var process = (TDJS_Vision.Process)FormatterServices.GetUninitializedObject(typeof(TDJS_Vision.Process));
        process.ProcessName = "工位2测试";
        Set(process, typeof(TDJS_Vision.Process), "_nodes", new List<NodeBase>());
        Set(process, typeof(TDJS_Vision.Process), "_connections", new List<ProcessConnection>());
        return process;
    }
    /// <summary>仅构造节点身份和参数，不创建模型、设备或参数编辑器。</summary>
    private static T Node<T>(TDJS_Vision.Process process, int id, string name, INodeParam param) where T : NodeBase
    {
        var node = (T)FormatterServices.GetUninitializedObject(typeof(T));
        Set(node, typeof(NodeBase), "_id", id);
        Set(node, typeof(NodeBase), "_nodeName", name);
        node.Process = process;
        node.ParamForm = new Parameter { Params = param };
        process.Nodes.Add(node);
        return node;
    }
    /// <summary>验证没有唯一绑定时拒绝，而不是打开选择窗口或借用别的AI。</summary>
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { checks++; return; }
        throw new Exception(message);
    }
    /// <summary>打开真实参数窗体，使用相同数据对比新旧版本，不运行学习或保存配置。</summary>
    private static void MeasureDialogs(TDJS_Vision.Process process, NodeTDAI target, RunParamDisplayFilter filter, string[] args)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var watch = Stopwatch.StartNew();
            using (var form = new FormSolRunParam(process, target, filter))
            {
                form.Shown += (s, e) => { Console.WriteLine("Manual dialog ready: " + watch.ElapsedMilliseconds + " ms"); if (!args.Contains("--preview")) form.BeginInvoke(new Action(form.Close)); };
                form.ShowDialog();
                if (!args.Contains("--benchmark")) Check(form.Text.Contains("左棱镜"), "标题没有标明视角");
            }
            if (args.Contains("--preview")) break;
        }
    }
    /// <summary>验证真实四视角参数路径，并记录参数窗体从构造到显示的耗时。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            LanguageManager.Initialize();
            LanguageManager.SetLanguage("zh-CN", false);
            var process = CreateProcess();
            var targets = new List<NodeTDAI>();
            var overlays = new List<NodeParamResultOverlayDraw2>();
            for (int i = 0; i < 4; i++)
            {
                string name = new[] { "左棱镜", "右棱镜", "上棱镜", "下棱镜" }[i];
                var param = new NodeParamTDAI { CurDetectItemName = name, AIInputInfo = new AIInputInfo(), Text1 = (30 + i) + ".裁剪" };
                targets.Add(Node<NodeTDAI>(process, 10 + i, name, param));
                Node<NodeImageCrop>(process, 30 + i, "裁剪", new NodeParamImageCrop());
                var overlay = new NodeParamResultOverlayDraw2();
                overlay.Items.Add(new ResultOverlayDraw2Item { Enabled = true, UseManualText = false, SourceText1 = (10 + i) + "." + name });
                overlays.Add(overlay);
                Node<NodeResultOverlayDraw2>(process, 20 + i, "绘制", overlay);
                Node<NodeImageShow>(process, 40 + i, "显示", new NodeParamImageShow { Text1 = (20 + i) + ".绘制", WindowName = "ImageWindow" + (i + 1) });
                Solution.Instance.DetectItemDic[name] = new List<DetectItemInfo> { new DetectItemInfo { Name = name + "尺寸", Enable = true, MinValue = "1", MaxValue = "99999" } };
            }
            var viewer = (FrmSingleImage)FormatterServices.GetUninitializedObject(typeof(FrmSingleImage));
            if (args.Contains("--benchmark"))
            {
                viewer.FormName = "ImageWindow1";
                MeasureDialogs(process, targets[0], (RunParamDisplayFilter)Call(viewer, "ResolveRunParamDisplayFilter", process), args);
                return 0;
            }
            for (int i = 0; i < 4; i++)
            {
                viewer.FormName = "ImageWindow" + (i + 1);
                Check(ReferenceEquals(Call(viewer, "ResolveTargetAiNode", process, false), targets[i]), "画面没有定位到自身AI");
                var filter = (RunParamDisplayFilter)Call(viewer, "ResolveRunParamDisplayFilter", process);
                using (var control = new SolRunParamControl())
                {
                    control.Init(process, targets[i], filter);
                    Check(ReferenceEquals(Field(control, "_nodeTDAI"), targets[i]), "参数页选错AI");
                    Check(((NodeBase)Field(control, "_nodeCrop")).ID == 30 + i, "参数页选错裁剪节点");
                    Check((string)Field(control, "_windowsName") == viewer.FormName, "参数页使用了其他画面");
                }
            }
            viewer.FormName = "ImageWindow99";
            Reject(() => Call(viewer, "ResolveTargetAiNode", process, false), "未绑定画面不应回退整个工位");
            viewer.FormName = "ImageWindow1";
            overlays[0].Items.Add(new ResultOverlayDraw2Item { Enabled = true, UseManualText = false, SourceText1 = "11.右棱镜" });
            Reject(() => Call(viewer, "ResolveTargetAiNode", process, false), "多AI歧义没有拒绝");
            overlays[0].Items.RemoveAt(1);
            overlays[0].Items[0].Enabled = false;
            Reject(() => Call(viewer, "ResolveTargetAiNode", process, false), "禁用绘制不应成为绑定");
            overlays[0].Items[0].Enabled = true;

            // 只写入测试进程的临时方案，绝不加载或改动现场Sol/AI文件。
            string temp = Path.Combine(Path.GetTempPath(), "SingleImageTuning-" + Guid.NewGuid().ToString("N") + ".Sol");
            Solution.Instance.SolFileName = temp;
            Solution.Instance.AllProcesses.Add(process);

            // 模拟方案重载：相同名称与编号不代表同一流程，画面不能继续操作旧实例。
            var stale = CreateProcess();
            var staleTarget = Node<NodeTDAI>(stale, 10, "旧左棱镜", new NodeParamTDAI { AIInputInfo = new AIInputInfo() });
            Set(viewer, typeof(FrmSingleImage), "_process", stale);
            Set(viewer, typeof(FrmSingleImage), "_lastImageShowNodeId", (int?)999);
            Check(ReferenceEquals(Call(viewer, "GetCurrentProcess"), process), "方案切换后仍返回旧流程");
            Check(Field(viewer, "_lastImageShowNodeId") == null, "未清除旧图像显示节点缓存");
            Reject(() => Call(typeof(SolRunParamControl), "StartAutoLearningForNode", stale, staleTarget), "已失效流程仍提示学习成功");
            Check(!((NodeParamTDAI)staleTarget.ParamForm.Params).IsAutoStudy && !File.Exists(temp), "失效流程被修改或保存");

            // 已实例化的内部模块属于当前方案；模块参数被替换后其旧内部流程立即失效。
            var child = CreateProcess();
            var moduleParam = new NodeParamCompositeModule();
            var module = Node<NodeCompositeModule>(process, 90, "模块", moduleParam);
            Set(module, typeof(NodeCompositeModule), "_cachedParam", moduleParam);
            Set(module, typeof(NodeCompositeModule), "_runtimeProcess", child);
            Check((bool)Call(typeof(SolRunParamControl), "IsCurrentSolutionProcess", child), "当前内部模块被误判失效");
            module.ParamForm.Params = new NodeParamCompositeModule();
            Check(!(bool)Call(typeof(SolRunParamControl), "IsCurrentSolutionProcess", child), "模块重建后仍接收旧内部流程");
            process.Nodes.Remove(module);

            var learningWatch = Stopwatch.StartNew();
            Check((bool)Call(typeof(SolRunParamControl), "StartAutoLearningForNode", process, targets[2]), "开启学习失败");
            Console.WriteLine("Learning with temporary solution save: " + learningWatch.ElapsedMilliseconds + " ms");
            Check(targets.Select(n => ((NodeParamTDAI)n.ParamForm.Params).IsAutoStudy).SequenceEqual(new[] { false, false, true, false }), "学习误改其他视角");
            Check(File.Exists(temp), "没有保存学习状态");
            var saved = JObject.Parse(File.ReadAllText(temp));
            Check(saved["ProcessInfos"][0]["NodeInfos"].Where(n => (int)n["ID"] >= 10 && (int)n["ID"] <= 13)
                .Select(n => (bool)n["NodeParam"][typeof(NodeParamTDAI).FullName]["IsAutoStudy"]).SequenceEqual(new[] { false, false, true, false }), "持久化学习目标错误");
            Check(((NodeParamTDAI)targets[0].ParamForm.Params).AIInputInfo.ScoreThreshold == new AIInputInfo().ScoreThreshold, "学习改动其他AI阈值");

            var duplicate = Node<NodeTDAI>(process, 10, "重复编号", new NodeParamTDAI { AIInputInfo = new AIInputInfo() });
            using (var control = new SolRunParamControl())
            {
                control.Init(process, targets[0]);
                Check(ReferenceEquals(Field(control, "_nodeTDAI"), targets[0]), "相同编号覆盖了指定AI实例");
            }
            process.Nodes.Remove(duplicate);

            var filter1 = (RunParamDisplayFilter)Call(viewer, "ResolveRunParamDisplayFilter", process);
            MeasureDialogs(process, targets[0], filter1, args);
            Console.WriteLine("SingleImageTuning: " + checks + " assertions passed. Test solution: " + temp);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>参数容器，不触发任何设备或模型加载。</summary>
    private sealed class Parameter : INodeParamForm
    {
        /// <summary>测试参数。</summary>
        public INodeParam Params { get; set; }
        /// <summary>无订阅控件。</summary>
        public void SetNodeBelong(NodeBase node) { }
        /// <summary>无参数编辑器。</summary>
        public void SetParam2Form() { }
    }
}
