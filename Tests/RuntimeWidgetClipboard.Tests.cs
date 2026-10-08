using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>组件复制后端的无设备、无系统剪贴板测试入口。</summary>
internal static class RuntimeWidgetClipboardTests
{
    /// <summary>已通过的断言数。</summary>
    private static int assertions;
    /// <summary>只执行模型与编码测试，不调用系统剪贴板实现。</summary>
    private static int Main()
    {
        try
        {
            TestEveryKind();
            TestBindingsAndImages();
            TestBoundsNamesAndHistory();
            TestRollback();
            TestInvalidPayloads();
            Check(typeof(IRuntimeWidgetClipboard).IsAssignableFrom(typeof(RuntimeSystemWidgetClipboard)), "系统实现遵守可替换字符串契约");
            Console.WriteLine("PASS: " + assertions + " runtime widget clipboard assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex); return 1; }
    }

    /// <summary>业务失败立即报告，便于保留红绿测试证据。</summary>
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); assertions++; }

    /// <summary>确认恶意或不合法输入明确拒绝。</summary>
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { Check(true, message); return; }
        throw new InvalidOperationException(message);
    }

    /// <summary>所有可用组件保留配置且每次粘贴只产生一个编辑事务。</summary>
    private static void TestEveryKind()
    {
        foreach (RuntimeWidgetKind kind in Enum.GetValues(typeof(RuntimeWidgetKind)))
        {
            var source = RuntimeLayoutSession.CreateElement(kind);
            source.X = 30; source.Y = 40;
            if (kind == RuntimeWidgetKind.RunControl) source.RunProcessId = 17;
            if (kind == RuntimeWidgetKind.Log) { source.LogProcessId = 19; source.LogCapacity = 321; source.LogMinimumLevel = RuntimeLogLevel.Warn; source.LogAutoScroll = false; }
            var document = new RuntimeLayoutDocument { Width = 1600, Height = 900 };
            document.Elements.Add(source);
            var clipboard = new MemoryClipboard();
            Check(clipboard.Read() == null, "空剪贴板返回空值");
            clipboard.Write(RuntimeWidgetClipboardCodec.Encode(document, source));
            var wrapped = RuntimeLayoutSerializer.Deserialize(clipboard.Read());
            Check(wrapped.Width == 1600 && wrapped.Height == 900 && wrapped.Elements.Count == 1, "单组件包装保留原页面尺寸");
            var decoded = RuntimeWidgetClipboardCodec.Decode(clipboard.Read());
            Check(JToken.DeepEquals(JToken.FromObject(source), JToken.FromObject(decoded)), "所有类型完整往返序列化：" + kind);
            Check(!ReferenceEquals(decoded, source), "解码得到独立快照");
            var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
            int changes = 0; session.Changed += (sender, args) => changes++;
            var copy = session.InsertCopy(decoded, new Point(70, 90));
            Guid identity;
            Check(copy.Id != decoded.Id && Guid.TryParseExact(copy.Id, "N", out identity), "粘贴生成独立标识");
            Check(copy.Kind == kind && copy.X == 70 && copy.Y == 90 && changes == 1, "粘贴位置正确且只产生一次编辑");
            Check(copy.Name.EndsWith(" 副本", StringComparison.Ordinal), "复制名称保留副本后缀");
            if (kind == RuntimeWidgetKind.Log)
                Check(copy.LogProcessId == 19 && copy.LogCapacity == 321 && copy.LogMinimumLevel == RuntimeLogLevel.Warn && !copy.LogAutoScroll, "日志配置不能退回默认值");
            if (kind == RuntimeWidgetKind.RunControl) Check(copy.RunProcessId == 17, "运行控制目标保留");
            string copiedId = copy.Id;
            session.Undo();
            Check(session.Document.Elements.Count == 0 && !session.CanUndo && session.CanRedo, "一次撤销移除整个副本");
            session.Redo();
            Check(session.Document.Elements.Single().Id == copiedId, "重做恢复同一个副本标识");
            Check(source.X == 30 && source.Y == 40 && source.Id == decoded.Id, "复制不能修改来源页面坐标或标识");
        }
    }

    /// <summary>在内存生成有效图片，不读取用户图片或写文件。</summary>
    private static string ImageData()
    {
        using (var bitmap = new Bitmap(2, 3))
        using (var stream = new MemoryStream())
        { bitmap.SetPixel(0, 0, Color.Red); bitmap.Save(stream, ImageFormat.Png); return Convert.ToBase64String(stream.ToArray()); }
    }

    /// <summary>图片、独立ROI、组内路径在来源、解码、插入和重复粘贴之间隔离。</summary>
    private static void TestBindingsAndImages()
    {
        var source = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.Image);
        source.ImageData = ImageData();
        source.Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, ProcessId = 7, NodePath = new[] { 2, 3 }, PropertyPath = "Result.Image", DisplayPath = "流程/模块/图像" };
        source.RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 8, NodePath = new[] { 4, 5 }, PropertyPath = "Result.ROI", DisplayPath = "另一流程/ROI" };
        var document = new RuntimeLayoutDocument(); document.Elements.Add(source);
        var decoded = RuntimeWidgetClipboardCodec.Decode(RuntimeWidgetClipboardCodec.Encode(document, source));
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var first = session.InsertCopy(decoded, Point.Empty);
        var second = session.InsertCopy(decoded, new Point(50, 50));
        Check(first.ImageData == source.ImageData && first.RoiBinding.ProcessId == 8 && first.Binding.ProcessId == 7, "图像及ROI独立配置完整复制");
        Check(!ReferenceEquals(first.Binding, decoded.Binding) && !ReferenceEquals(first.Binding.NodePath, decoded.Binding.NodePath), "图像路径深复制");
        Check(!ReferenceEquals(first.RoiBinding, decoded.RoiBinding) && !ReferenceEquals(first.RoiBinding.NodePath, decoded.RoiBinding.NodePath), "ROI路径深复制");
        first.Binding.NodePath[0] = 99; first.RoiBinding.NodePath[0] = 98;
        decoded.Binding.NodePath[1] = 97; decoded.RoiBinding.NodePath[1] = 96;
        Check(source.Binding.NodePath.SequenceEqual(new[] { 2, 3 }) && source.RoiBinding.NodePath.SequenceEqual(new[] { 4, 5 }), "修改任意副本不能污染来源绑定");
        Check(second.Binding.NodePath.SequenceEqual(new[] { 2, 3 }) && second.RoiBinding.NodePath.SequenceEqual(new[] { 4, 5 }), "多次粘贴不能互相共享路径数组");
        using (var image = RuntimeLayoutSerializer.DecodeImage(second.ImageData))
            Check(image.Size == new Size(2, 3) && image.GetPixel(0, 0).R == 255, "内嵌图片仍可解码且像素保留");
    }

    /// <summary>小页面约束、长名称、旧复制偏移和撤销分支保持兼容。</summary>
    private static void TestBoundsNamesAndHistory()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 320, Height = 240 });
        var source = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.Image);
        source.Name = new string('名', 120); source.X = 2000; source.Y = 1000;
        var copy = session.InsertCopy(source, new Point(int.MaxValue, int.MinValue));
        Check(copy.Bounds == new Rectangle(0, 0, 320, 240), "大控件粘入小页面必须整体约束到页面内");
        Check(copy.Name.Length <= 120 && copy.Name.EndsWith(" 副本", StringComparison.Ordinal), "长名称保留后缀且不超过120字符");
        Check(source.Width == 440 && source.Height == 300 && source.Name.Length == 120, "约束和改名不能修改外部源");
        var normal = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var original = normal.Add(RuntimeWidgetKind.Text, new Point(10, 20));
        var duplicate = normal.Duplicate(original.Id);
        Check(duplicate.Bounds.Location == new Point(34, 44), "原复制按钮继续使用24像素偏移");
        normal.Undo(); normal.Undo();
        Check(normal.Document.Elements.Count == 0 && !normal.CanUndo, "原复制仍恰好占用一个撤销事务");
        normal.Redo(); normal.InsertCopy(source, Point.Empty);
        Check(!normal.CanRedo, "新粘贴应截断旧重做分支");
        Check(normal.Duplicate("不存在") == null, "原复制的缺失选择行为不变");
        Reject<ArgumentNullException>(() => normal.InsertCopy(null, Point.Empty), "空来源必须明确拒绝");
    }

    /// <summary>达到组件上限或绑定无效时原子回滚，不污染历史或源对象。</summary>
    private static void TestRollback()
    {
        var document = new RuntimeLayoutDocument();
        for (int index = 0; index < 200; index++) document.Elements.Add(RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.Text));
        var session = new RuntimeLayoutSession(document);
        string before = RuntimeLayoutSerializer.Serialize(session.Document);
        var source = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.Text);
        string sourceId = source.Id;
        Reject<InvalidDataException>(() => session.InsertCopy(source, Point.Empty), "第201个组件必须拒绝");
        Check(RuntimeLayoutSerializer.Serialize(session.Document) == before && !session.CanUndo && !session.CanRedo, "上限失败必须完整回滚且不添加历史");
        Check(source.Id == sourceId, "失败插入不能改写外部源标识");
        session.Remove(session.Document.Elements[0].Id); session.Undo();
        Check(session.CanRedo, "准备有重做分支的上限场景");
        Reject<InvalidDataException>(() => session.InsertCopy(source, Point.Empty), "有历史时仍拒绝第201个组件");
        Check(session.CanRedo && session.Document.Elements.Count == 200, "失败插入保留既有重做历史");
        var empty = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        source.Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 1, NodePath = new[] { 1 }, PropertyPath = "ROI" };
        Reject<InvalidDataException>(() => empty.InsertCopy(source, Point.Empty), "不兼容绑定不能进入目标文档");
        Check(empty.Document.Elements.Count == 0 && !empty.CanUndo, "非法副本回滚后仍为空文档");
    }

    /// <summary>剪贴板是外部输入，大小、数量、类型元数据和嵌入图片必须校验。</summary>
    private static void TestInvalidPayloads()
    {
        var source = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.Image);
        var document = new RuntimeLayoutDocument { Width = 2000, Height = 1000 };
        source.X = 1500; source.Y = 650; document.Elements.Add(source);
        string valid = RuntimeWidgetClipboardCodec.Encode(document, source);
        Check(RuntimeWidgetClipboardCodec.Decode(valid).X == 1500, "编码校验必须使用源页面而不是默认页面尺寸");
        foreach (string invalid in new[] { null, "", " ", "{", "null", "[]", valid + "{}" })
            Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(invalid), "非法或空剪贴板必须拒绝");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(new string('x', RuntimeLayoutSerializer.MaximumDocumentBytes + 1)), "超过16MB文本必须拒绝");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(new string('中', RuntimeLayoutSerializer.MaximumDocumentBytes / 3 + 1)), "16MB按UTF8字节而非字符限制");
        var token = JObject.Parse(valid);
        token["Elements"] = new JArray();
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(token.ToString()), "零组件剪贴板必须拒绝");
        token = JObject.Parse(valid);
        var second = token["Elements"][0].DeepClone(); second["Id"] = Guid.NewGuid().ToString("N");
        ((JArray)token["Elements"]).Add(second);
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(token.ToString()), "多组件包装不能偷偷粘入第一个组件");
        token = JObject.Parse(valid); token["$type"] = "System.Diagnostics.Process, System";
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(token.ToString()), "根级类型元数据必须拒绝");
        token = JObject.Parse(valid); token["Elements"][0]["$type"] = "任意插件类型";
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(token.ToString()), "嵌套类型元数据也必须拒绝");
        token = JObject.Parse(valid); token["Elements"][0]["ImageData"] = "损坏图片";
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(token.ToString()), "解码必须验证内嵌图片");
        source.ImageData = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Encode(document, source), "编码时也必须拒绝无法解码的图片");
        source.ImageData = null; source.X = 1999;
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Encode(document, source), "源控件越界不能被编码静默修正");
        Reject<ArgumentNullException>(() => RuntimeWidgetClipboardCodec.Encode(null, source), "编码必须提供原页面");
        Reject<ArgumentNullException>(() => RuntimeWidgetClipboardCodec.Encode(document, (RuntimeLayoutElement)null), "编码必须提供源组件");
    }

    /// <summary>只存内存字符串，不创建或操作用户剪贴板。</summary>
    private sealed class MemoryClipboard : IRuntimeWidgetClipboard
    {
        /// <summary>当前测试载荷。</summary>
        private string payload;
        /// <summary>复制不可变文本。</summary>
        public void Write(string value) { payload = value; }
        /// <summary>读取当前文本，未写入时为空。</summary>
        public string Read() { return payload; }
    }
}
