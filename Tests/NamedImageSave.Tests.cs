using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Logger;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._7_ResultProcessing.NamedImageSave;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;

/// <summary>通过真实节点工厂验证命名存图入口。</summary>
internal static class NamedImageSaveTests
{
    /// <summary>断言数量。</summary>
    private static int _checks;
    /// <summary>检查真实行为。</summary>
    private static void Check(bool passed, string message) { _checks++; if (!passed) throw new Exception(message); }
    /// <summary>确认明确拒绝失效配置。</summary>
    private static void Reject(Action action, string message)
    {
        try { action(); } catch { _checks++; return; }
        throw new Exception(message);
    }
    /// <summary>读取真实设计器控件。</summary>
    private static T Control<T>(object form, string name) where T : class =>
        typeof(ParamFormNamedImageSave).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form) as T;
    /// <summary>执行真实窗体按钮事件。</summary>
    private static void Click(object form, string name) => typeof(ParamFormNamedImageSave)
        .GetMethod(name + "_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
    /// <summary>用统一目录中的真实端口创建订阅引用。</summary>
    private static NamedImageSource Source(NodeBase node, Type type)
    {
        var output = NamedImageSubscriptions.GetOutputs(node).FirstOrDefault(item => item.ValueType == type);
        Check(output != null, "订阅目录缺少输出：" + node.NodeName + " / " + type.Name);
        return new NamedImageSource { SourceNodeId = node.ID, PropertyPath = output.PropertyPath, DisplayName = node.ID + "." + node.NodeName + " → " + output.DisplayName };
    }

    /// <summary>复制节点组时改写所有稳定订阅编号，跨流程缺失源必须清零。</summary>
    private static void VerifyCopiedReferences(NodeParamNamedImageSave param)
    {
        var method = typeof(NodeBase).Assembly.GetType("TDJS_Vision.Forms.ProcessNew.ProcessEditPanel")
            .GetMethod("RemapInternalNodeReferences", BindingFlags.Static | BindingFlags.NonPublic);
        var copy = param.Copy(); copy.FileName.Source = param.Folders.Last().Source.Copy();
        copy.FileNameParts.Add(new NamedImageValue { Source = param.Folders.First(row => row.Source?.SourceNodeId == 3).Source.Copy() });
        var nodeIds = new Dictionary<int, int> { { 1, 101 }, { 2, 102 }, { 3, 103 }, { 4, 104 } };
        var sourceTexts = nodeIds.Keys.ToDictionary(id => id, id => id + ".原节点");
        method.Invoke(null, new object[] { copy, new Dictionary<string, string>(), nodeIds, sourceTexts, new List<string>(), 0 });
        Check(copy.ImageSource.SourceNodeId == 101 && copy.FileName.Source.SourceNodeId == 104 &&
            copy.FileNameParts.Last().Source.SourceNodeId == 103 &&
            copy.Folders.Where(row => row.Source != null).Select(row => row.Source.SourceNodeId).SequenceEqual(new[] { 102, 103, 104, 104 }),
            "复制节点组后命名存图订阅没有重映射到新节点。");
        var orphan = param.Copy(); orphan.FileName.Source = param.Folders.Last().Source.Copy();
        orphan.FileNameParts.Add(new NamedImageValue { Source = param.Folders.First(row => row.Source?.SourceNodeId == 3).Source.Copy() });
        method.Invoke(null, new object[] { orphan, new Dictionary<string, string>(), new Dictionary<int, int>(), sourceTexts, new List<string>(), 0 });
        Check(orphan.ImageSource.SourceNodeId == 0 && orphan.FileName.Source.SourceNodeId == 0 &&
            orphan.FileNameParts.Where(row => row.Source != null).All(row => row.Source.SourceNodeId == 0) &&
            orphan.Folders.Where(row => row.Source != null).All(row => row.Source.SourceNodeId == 0),
            "跨流程未复制的订阅源没有清零。");
        var literal = new NamedImageValue { Text = "1.原节点" };
        method.Invoke(null, new object[] { literal, new Dictionary<string, string> { { "1.原节点", "101.复制节点" } }, nodeIds, sourceTexts, new List<string>(), 0 });
        Check(literal.Text == "1.原节点", "复制时误把手填文件夹当成节点引用修改。");
        var dateRow = new NamedImageValue { Mode = NamedImageValueMode.CurrentDate };
        method.Invoke(null, new object[] { dateRow, new Dictionary<string, string>(), nodeIds, sourceTexts, new List<string>(), 0 });
        Check(dateRow.Mode == NamedImageValueMode.CurrentDate && dateRow.Source == null, "复制时破坏当前日期目录行。");
    }

    /// <summary>测试保存草稿、单列行编辑、订阅与截图。</summary>
    private static void VerifyForm(NodeNamedImageSave node, NodeParamNamedImageSave param, string root)
    {
        var form = (ParamFormNamedImageSave)node.ParamForm;
        form.Params = param.Copy(); form.SetParam2Form();
        var list = Control<ListBox>(form, "listFolders");
        var fileList = Control<ListBox>(form, "listFileNames");
        var tree = Control<TreeView>(form, "treeSources");
        Check(tree.Nodes.Cast<TreeNode>().Any(item => item.Text.Contains("PLC")), "缺少PLC订阅树。");
        Check(tree.Nodes.Cast<TreeNode>().Any(item => item.Text.Contains("共享")), "缺少共享变量订阅树。");
        Check(tree.Nodes.Cast<TreeNode>().Any(item => item.Text.Contains("多条件")), "缺少多条件订阅树。");
        Check(list.Items.Count == param.Folders.Count, "目录行恢复数量不符。");
        Check(list.Items.Cast<NamedImageValue>().First(row => row.Source?.SourceNodeId == 2).Source.DisplayName.Contains("PLC已改名"), "参数界面仍显示来源改名前的名称。");
        list.SelectedIndex = list.Items.Count - 1;
        Click(form, "Insert");
        Control<TextBox>(form, "textRow").Text = "插入层";
        Check(((NamedImageValue)list.SelectedItem).Text == "插入层", "插入行文字未保存。");
        Click(form, "Up"); Click(form, "Down");
        Check(((NamedImageValue)list.SelectedItem).Text == "插入层", "移动目录行丢失内容。");
        Click(form, "Delete");
        Click(form, "Add");
        var leaf = tree.Nodes.Cast<TreeNode>().SelectMany(item => item.Nodes.Cast<TreeNode>())
            .First(item => item.Tag is NamedImageSource source && source.SourceNodeId == 2 && item.Name == "名称");
        tree.SelectedNode = leaf; Click(form, "SubscribeRow");
        Check(((NamedImageValue)list.SelectedItem).Source.SourceNodeId == 2, "选择订阅未绑定当前行。");
        int subscribedIndex = list.SelectedIndex;
        list.SelectedIndex = 0; list.SelectedIndex = subscribedIndex;
        Check(((NamedImageValue)list.SelectedItem).Source.SourceNodeId == 2, "切换行丢失订阅。");
        var fileLeaf = tree.Nodes.Cast<TreeNode>().SelectMany(item => item.Nodes.Cast<TreeNode>())
            .First(item => item.Tag is NamedImageSource source && source.SourceNodeId == 2 && item.Name == "名称");
        fileList.SelectedIndex = 0;
        tree.SelectedNode = fileLeaf; Click(form, "SubscribeFile");
        Click(form, "AddFile"); Click(form, "CurrentDateFile");
        Check(((NamedImageValue)fileList.SelectedItem).Mode == NamedImageValueMode.CurrentDate, "文件名当前日期按钮没有设置当前段。");
        Click(form, "AddFile"); Control<TextBox>(form, "textFileName").Text = "_后缀";
        Check(((NamedImageValue)fileList.SelectedItem).Text == "_后缀", "文件名手填段未保存。");
        Click(form, "Add"); Click(form, "CurrentDateRow");
        Check(((NamedImageValue)list.SelectedItem).Mode == NamedImageValueMode.CurrentDate, "当前日期按钮没有设置当前行。");
        var boolLeaf = tree.Nodes.Cast<TreeNode>().SelectMany(item => item.Nodes.Cast<TreeNode>())
            .First(item => item.Tag is NamedImageSource source && source.SourceNodeId == 4 && item.Name == "布尔");
        tree.SelectedNode = boolLeaf; Click(form, "OkNgRow");
        Check(((NamedImageValue)list.SelectedItem).Mode == NamedImageValueMode.BoolOkNg &&
            ((NamedImageValue)list.SelectedItem).Source.SourceNodeId == 4, "OK/NG按钮没有绑定布尔订阅。");
        Click(form, "Save");
        var saved = (NodeParamNamedImageSave)form.Params;
        Check(saved.Folders.Any(row => row.Mode == NamedImageValueMode.CurrentDate) &&
            saved.Folders.Last().Mode == NamedImageValueMode.BoolOkNg &&
            saved.Folders.Last().Source.SourceNodeId == 4 &&
            saved.FileNameParts.Any(row => row.Source?.SourceNodeId == 2) &&
            saved.FileNameParts.Any(row => row.Mode == NamedImageValueMode.CurrentDate), "保存丢失日期、OK/NG或文件名订阅。");
        Click(form, "ManualRow"); Control<TextBox>(form, "textRow").Text = "未保存修改";
        Check(saved.Folders.Last().Mode == NamedImageValueMode.BoolOkNg && saved.Folders.Last().Source.SourceNodeId == 4,
            "编辑草稿污染已保存参数。");
        form.SetParam2Form();
        var reopenedLast = (NamedImageValue)list.Items[list.Items.Count - 1];
        Check(reopenedLast.Mode == NamedImageValueMode.BoolOkNg && reopenedLast.Source.SourceNodeId == 4, "重新打开参数订阅恢复失败。");
        form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-30000, -30000); form.ShowInTaskbar = false;
        form.Show(); Application.DoEvents();
        foreach (string buttonName in new[] { "buttonCurrentDateFile", "buttonManualFile", "buttonAddFile", "buttonInsertFile", "buttonDeleteFile", "buttonUpFile", "buttonDownFile", "buttonSave" })
        {
            var button = Control<Button>(form, buttonName);
            var bounds = form.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
            Check(bounds.Right <= form.ClientSize.Width && bounds.Bottom <= form.ClientSize.Height && bounds.Left >= 0 && bounds.Top >= 0,
                "命名存图界面按钮超出可视区域：" + buttonName);
        }
        var previewBounds = form.RectangleToClient(Control<TextBox>(form, "textPreview").RectangleToScreen(Control<TextBox>(form, "textPreview").ClientRectangle));
        var fileListBounds = form.RectangleToClient(fileList.RectangleToScreen(fileList.ClientRectangle));
        Check(previewBounds.Top > fileListBounds.Bottom, "保存路径预览与文件名片段列表重叠。");
        using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(Path.Combine(root, "命名存图参数.png"));
        }
        form.Hide();
        form.Params = JsonConvert.DeserializeObject<NodeParamNamedImageSave>(JsonConvert.SerializeObject(param));
        form.SetParam2Form();
        Check(((NodeParamNamedImageSave)form.Params).Folders.Last().Source.SourceNodeId == 4, "JSON往返丢失多条件订阅。");
    }

    /// <summary>验证实际端口订阅、逐级命名、参数编辑、后台落盘和异常边界。</summary>
    [STAThread]
    private static int Main()
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        var nodes = new List<NodeBase>();
        ImageQueueProcessor queue = null;
        try
        {
            NodeType type;
            if (!Enum.TryParse("NamedImageSave", out type)) throw new Exception("缺少命名存图节点类型。");
            string root = Path.GetFullPath(Path.Combine("Tests", "Results", "NamedImageSave", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            Environment.CurrentDirectory = root;
            LogLevelSettings.SetEnabled(MsgLevel.Exception, true);
            var process = new TDJS_Vision.Process("命名存图验证");
            var imageNode = NodeFactory.CreateNode(1, "图像源", process, NodeType.ImageSource);
            var plc = new NodePlcRead(2, "PLC读取", process, NodeType.PLCRead);
            var shared = new NodeSharedVariable(3, "共享变量", process, NodeType.SharedVariable);
            var condition = new NodeMultiCondition(4, "多条件判定", process, NodeType.MultiCondition);
            var node = (NodeNamedImageSave)NodeFactory.CreateNode(5, "命名存图", process, type);
            nodes.AddRange(new NodeBase[] { imageNode, plc, shared, condition, node });
            process.Nodes.AddRange(nodes);
            foreach (var source in nodes.Take(4)) process.Connections.Add(new ProcessConnection { FromNodeId = source.ID, ToNodeId = node.ID });
            process.NotifyConnectionsChanged();
            plc.ParamForm.Params = new NodeParamPlcRead { Address = "DM100", DataType = "String" };
            ((NodeResultPlcRead)plc.Result).ReadResult = new PlcReadResult("0012工艺", "String", "DM100");
            shared.ParamForm.Params = new NodeParamSharedVariable { IsRead = true, ReadName = "测试工位" };
            Solution.Instance.SharedVariable.SetValue("测试工位", new SharedVarValue("工位 A", typeof(string)));
            ((NodeResultSharedVariable)shared.Result).Value = new SharedVarValue("工位 A", typeof(string));
            ((NodeResultMultiCondition)condition.Result).ConditionResult = true;
            ((NodeResultImageSource)imageNode.Result).OutputImage = OutputImage.FromOwnedSingleImage(new Mat(24, 32, MatType.CV_8UC3, Scalar.All(127)));
            var param = new NodeParamNamedImageSave {
                DriveRoot = Path.GetPathRoot(root), ImageSource = Source(imageNode, typeof(OutputImage)),
                Folders = root.Substring(Path.GetPathRoot(root).Length).Split(Path.DirectorySeparatorChar).Select(text => new NamedImageValue { Text = text }).ToList(),
                FileName = new NamedImageValue { Text = "固定图像" }
            };
            param.Folders.Add(new NamedImageValue { Source = Source(plc, typeof(string)) });
            param.Folders.Add(new NamedImageValue { Source = Source(shared, typeof(string)) });
            param.Folders.Add(new NamedImageValue { Source = Source(condition, typeof(bool)) });
            param.Folders.Add(new NamedImageValue { Mode = NamedImageValueMode.CurrentDate });
            param.Folders.Add(new NamedImageValue { Mode = NamedImageValueMode.BoolOkNg, Source = Source(condition, typeof(bool)) });
            param.FileNameParts = new List<NamedImageValue>
            {
                new NamedImageValue { Mode = NamedImageValueMode.CurrentDate },
                new NamedImageValue { Text = "_" },
                new NamedImageValue { Source = Source(plc, typeof(string)) },
                new NamedImageValue { Text = "_" },
                new NamedImageValue { Mode = NamedImageValueMode.BoolOkNg, Source = Source(condition, typeof(bool)) }
            };
            var schemeParam = JsonProjectSerializer.FromJson<INodeParam>(JsonProjectSerializer.ToJson<INodeParam>(param)) as NodeParamNamedImageSave;
            Check(schemeParam != null && schemeParam.Folders.Count == param.Folders.Count &&
                schemeParam.FileNameParts.Count == param.FileNameParts.Count && schemeParam.ImageSource.SourceNodeId == 1,
                "实际方案序列化器无法恢复命名存图参数，或额外添加了默认目录。");
            VerifyCopiedReferences(param);
            node.ParamForm.Params = param;
            Check(node.GetSubscriptionDependencyNodeIds().OrderBy(id => id).SequenceEqual(new[] { 1, 2, 3, 4 }), "订阅依赖声明不完整。");
            var upstream = process.GetUpstreamNodes(node).ToDictionary(item => item.ID);
            Func<NamedImageSource, object> read = source => NamedImageSubscriptions.Read(node, source, upstream, false);
            var time = new DateTime(2026, 9, 21, 12, 34, 56, 789);
            var path = NamedImagePathBuilder.Build(param, read, time, null);
            Check(path.DirectoryPath == Path.Combine(root, "0012工艺", "工位 A", "True", "2026-09-21", "OK"),
                "未按行首到末尾构成逐级目录，或日期/OKNG目录错误。");
            Check(path.ImageName == "2026-09-21_0012工艺_OK", "文件名没有按多段日期、订阅和OK/NG拼接。");
            ((NodeResultMultiCondition)condition.Result).ConditionResult = false;
            var ngPath = NamedImagePathBuilder.Build(param, read, time, null);
            Check(ngPath.DirectoryPath.EndsWith("\\NG", StringComparison.Ordinal) && ngPath.ImageName.EndsWith("_NG", StringComparison.Ordinal),
                "OK/NG目录或文件名没有按布尔False输出NG。");
            ((NodeResultMultiCondition)condition.Result).ConditionResult = true;
            var nonBoolOkNg = param.Copy();
            nonBoolOkNg.Folders.Add(new NamedImageValue { Mode = NamedImageValueMode.BoolOkNg, Source = Source(plc, typeof(string)) });
            Reject(() => NamedImagePathBuilder.Build(nonBoolOkNg, read, time, null), "OK/NG目录允许了非布尔订阅。");
            plc.NodeName = "PLC已改名";
            Check(NamedImagePathBuilder.Build(param, read, time, null).DirectoryPath == path.DirectoryPath, "来源改名破坏订阅。");
            VerifyForm(node, param, root);
            SynchronizationContext.SetSynchronizationContext(null);
            typeof(TDJS_Vision.Process).GetProperty("CurrentRunId").SetValue(process, 1);
            foreach (var source in nodes.Take(4)) source.SetRunResult(DateTime.Now, NodeStatus.Successful);
            queue = new ImageQueueProcessor(2, 32, 32L * 1024 * 1024, 10000, "命名存图测试");
            typeof(Solution).GetField("_imageSaveQueueProcessor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Solution.Instance, queue);
            queue.StartProcessing();
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(((NodeResultNamedImageSave)node.Result).Enqueued, "运行没有报告接纳状态。");
            Check(((NodeResultNamedImageSave)node.Result).DirectoryPath == path.DirectoryPath, "界面恢复后运行路径不一致：" + ((NodeResultNamedImageSave)node.Result).DirectoryPath);
            Check(SpinWait.SpinUntil(() => queue.CompletedTaskCount >= 2, 10000), "后台未完成命名存图。");
            Check(Directory.GetFiles(path.DirectoryPath, "*.jpg").Length == 2, "重复文件名覆盖了旧图。");
            // 下一张读取新结果，不沿用第一次冻结的路径。
            ((NodeResultPlcRead)plc.Result).ReadResult = new PlcReadResult("新工艺", "String", "DM100");
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(((NodeResultNamedImageSave)node.Result).DirectoryPath.Contains("新工艺"), "下一张没有使用当前订阅值。");
            var errors = new List<string>();
            ((NodeResultPlcRead)plc.Result).ReadResult = new PlcReadResult("../坏目录", "String", "DM100");
            var invalid = param.Copy(); invalid.FileNameParts = new List<NamedImageValue> { new NamedImageValue { Text = "CON" } };
            var safePath = NamedImagePathBuilder.Build(invalid, read, time, errors.Add);
            Check(errors.Count == 2 && safePath.DirectoryPath.StartsWith(root + "\\", StringComparison.Ordinal), "非法命名没有留在配置盘符内或缺少日志。");
            Check(safePath.ImageName == "非法字符_2026-09-21_12-34-56-789", "非法文件名备用格式不符。");
            node.ParamForm.Params = invalid;
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            // 发布者释放后仍依靠队列租约存图。
            ((NodeResultImageSource)imageNode.Result).OutputImage.Dispose();
            Check(SpinWait.SpinUntil(() => queue.CompletedTaskCount >= 4, 10000), "非法目录回退或后台租约未能保存图片。");
            Check(queue.FailedTaskCount == 0, "命名存图发生后台保存失败。");
            Check(Directory.GetFiles(root, "*.jpg", SearchOption.AllDirectories).Length == 4, "保存数量不符。");
            foreach (string file in Directory.GetFiles(root, "*.jpg", SearchOption.AllDirectories))
                using (var bitmap = new System.Drawing.Bitmap(file)) Check(bitmap.Width == 32 && bitmap.Height == 24, "保存图像无法正确解码。");
            // 旧结果、失效来源及取消不能被当作成功入队。
            plc.SetRunResult(DateTime.Now, NodeStatus.Failed);
            Reject(() => node.Run(CancellationToken.None, false).GetAwaiter().GetResult(), "使用了失败来源的旧结果。");
            Check(!((NodeResultNamedImageSave)node.Result).Enqueued, "失败后保留了上轮成功状态。");
            Reject(() => node.Run(new CancellationToken(true), false).GetAwaiter().GetResult(), "取消后仍投递图片。");
            shared.Active = false;
            Reject(() => NamedImageSubscriptions.Read(node, param.Folders.Last(item => item.Source?.SourceNodeId == 3).Source, upstream, false), "禁用来源仍允许读取。");
            shared.Active = true;
            var missing = param.Folders.Last().Source.Copy(); missing.SourceNodeId = 99;
            Reject(() => NamedImageSubscriptions.Read(node, missing, upstream, false), "删除来源未被拒绝。");
            var blank = param.Copy(); blank.FileName = new NamedImageValue(); blank.FileNameParts = new List<NamedImageValue> { new NamedImageValue { Text = "" } };
            Check(NamedImagePathBuilder.Build(blank, read, time, null).ImageName == "2026-09-21_12-34-56-789", "空文件名未使用时间戳。");
            Check(queue.StopProcessing(), "工作池未停止。");
            // 极小字节预算确定性触发拒绝，不能用已停止队列（方案会自动重建工作池）模拟。
            queue = new ImageQueueProcessor(1, 1, 1, 10000, "命名存图拒绝测试");
            typeof(Solution).GetField("_imageSaveQueueProcessor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Solution.Instance, queue);
            queue.StartProcessing();
            ((NodeResultImageSource)imageNode.Result).OutputImage = OutputImage.FromOwnedSingleImage(new Mat(24, 32, MatType.CV_8UC3, Scalar.All(63)));
            plc.SetRunResult(DateTime.Now, NodeStatus.Successful);
            Reject(() => node.Run(CancellationToken.None, false).GetAwaiter().GetResult(), "超过预算的队列未拒绝投递。");
            Check(!((NodeResultNamedImageSave)node.Result).Enqueued, "队列拒绝却报告成功。");
            using (var flushed = new ManualResetEventSlim())
            {
                var actions = (BlockingCollection<Action>)typeof(LogHelper).GetField("_logQueue", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                actions.Add(() => flushed.Set());
                Check(flushed.Wait(10000), "错误日志未排空。");
            }
            var logs = Directory.GetFiles(Path.Combine(root, "Logs"), "*.log").SelectMany(File.ReadAllLines).ToArray();
            Check(logs.Any(line => line.Contains("Exception") && line.Contains("命名存图命名异常") && line.Contains("行文件夹")), "非法目录没有写入错误日志。");
            Check(logs.Any(line => line.Contains("命名存图命名异常") && line.Contains("图像名称") && line.Contains("CON")), "非法图片名称没有记录原值。");
            Console.WriteLine("PASS: 命名存图{0}项断言；输出={1}", _checks, root);
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            queue?.StopProcessing();
            foreach (var node in nodes) { ((Form)node.ParamForm)?.Dispose(); node.Dispose(); }
        }
    }
}
