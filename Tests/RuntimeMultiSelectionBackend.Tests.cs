using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>多选编辑的无设备模型测试。</summary>
internal static class RuntimeMultiSelectionBackendTests
{
    /// <summary>已验证的行为断言数量。</summary>
    private static int assertions;
    /// <summary>测试批量事务、组合边界、排列及多组件剪贴板，不触碰生产或系统剪贴板。</summary>
    private static int Main()
    {
        try
        {
            TestBatchBoundsAndRemove();
            TestInsertCopies();
            TestInsertRollback();
            TestArrange();
            TestArrangeFailuresAndTies();
            TestCodec();
            Console.WriteLine("PASS: " + assertions + " multi-selection backend assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex); return 1; }
    }

    /// <summary>报告确定性失败而不依赖UI状态。</summary>
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); assertions++; }

    /// <summary>操作失败必须明确返回预期异常。</summary>
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { Check(true, message); return; }
        throw new InvalidOperationException(message);
    }

    /// <summary>创建合法文本组件，保持测试坐标可读。</summary>
    private static RuntimeLayoutElement Element(int x, int y, int width = 80, int height = 40)
    { return new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, X = x, Y = y, Width = width, Height = height }; }

    /// <summary>从若干组件创建没有历史的隔离会话。</summary>
    private static RuntimeLayoutSession Session(params RuntimeLayoutElement[] elements)
    { return new RuntimeLayoutSession(new RuntimeLayoutDocument { Elements = elements.ToList() }); }

    /// <summary>将完整文档序列化用于失败不变、撤销和重做验证。</summary>
    private static string Snapshot(RuntimeLayoutSession session) => RuntimeLayoutSerializer.Serialize(session.Document);

    /// <summary>批量坐标和删除只有一次通知及历史；空选择和失效标识不能产生编辑。</summary>
    private static void TestBatchBoundsAndRemove()
    {
        var a = Element(10, 20); var b = Element(120, 80); var c = Element(300, 140);
        var session = Session(a, b, c);
        string before = Snapshot(session); int changes = 0;
        session.Changed += (sender, args) => changes++;
        session.SetBounds(new Dictionary<string, Rectangle> { [a.Id] = new Rectangle(30, 40, 80, 40), [b.Id] = new Rectangle(140, 100, 80, 40), ["失效"] = Rectangle.Empty });
        Check(changes == 1 && session.CanUndo, "批量移动只提交一次通知和历史");
        Check(session.Find(a.Id).X == 30 && session.Find(b.Id).Y == 100 && session.Find(c.Id).Bounds == c.Bounds, "只移动目标组件，失效标识不影响未选组件");
        string after = Snapshot(session);
        session.Undo(); Check(Snapshot(session) == before && !session.CanUndo, "一步撤销整组移动");
        session.Redo(); Check(Snapshot(session) == after, "一步重做整组移动");
        int savedChanges = changes;
        session.SetBounds(session.Document.Elements.ToDictionary(value => value.Id, value => value.Bounds));
        session.SetBounds(new Dictionary<string, Rectangle>());
        session.Remove(new[] { "失效", "失效" });
        Check(changes == savedChanges, "无变化的批量命令不产生通知或历史");
        session.Remove(new[] { a.Id, b.Id, a.Id, "失效" });
        Check(changes == savedChanges + 1 && session.Document.Elements.Single().Id == c.Id, "批量删除按唯一标识删除并只通知一次");
        session.Undo(); Check(Snapshot(session) == after, "一步撤销批量删除");
        session.SetBounds(new Dictionary<string, Rectangle> { [a.Id] = new Rectangle(-99, int.MaxValue, 10, 10), [b.Id] = new Rectangle(0, 0, 5000, 5000) });
        Check(session.Find(a.Id).Bounds == new Rectangle(0, 688, 48, 32), "批量坐标沿用最小尺寸和页面约束");
        Check(session.Find(b.Id).Bounds == new Rectangle(0, 0, 1280, 720), "批量缩放沿用页面最大尺寸");
    }

    /// <summary>组合复制整体约束且保持相对坐标，路径与标识彻底隔离。</summary>
    private static void TestInsertCopies()
    {
        var a = Element(200, 80, 80, 40); var b = Element(320, 160, 60, 60);
        a.Kind = RuntimeWidgetKind.Image;
        a.Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, ProcessId = 1, NodePath = new[] { 2, 3 }, PropertyPath = "Image" };
        a.RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 4, NodePath = new[] { 5, 6 }, PropertyPath = "ROI" };
        a.Name = new string('名', 120);
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 320, Height = 240 });
        int changes = 0; session.Changed += (sender, args) => changes++;
        var copies = session.InsertCopies(new[] { a, b }, new Point(1000, 1000));
        Check(copies.Count == 2 && changes == 1, "批量插入只提交一个事务");
        Check(copies[0].Bounds == new Rectangle(140, 100, 80, 40) && copies[1].Bounds == new Rectangle(260, 180, 60, 60), "贴近右下边界时整体回移，不逐项挤压");
        Check(copies[1].X - copies[0].X == b.X - a.X && copies[1].Y - copies[0].Y == b.Y - a.Y, "复制保持组件间相对位置");
        Check(copies.All(value => value.Id != a.Id && value.Id != b.Id) && copies.Select(value => value.Id).Distinct().Count() == 2, "批量新标识互不相同且不同于来源");
        Check(copies[0].Name.Length <= 120 && copies[0].Name.EndsWith(" 副本", StringComparison.Ordinal), "批量名称也有界且保留后缀");
        Check(!ReferenceEquals(copies[0].Binding.NodePath, a.Binding.NodePath) && !ReferenceEquals(copies[0].RoiBinding.NodePath, a.RoiBinding.NodePath), "图像与ROI路径分别深复制");
        copies[0].Binding.NodePath[0] = 99; copies[0].RoiBinding.NodePath[0] = 98;
        Check(a.Binding.NodePath[0] == 2 && a.RoiBinding.NodePath[0] == 5 && a.X == 200 && b.X == 320, "副本编辑不污染来源数据和坐标");
        session.Undo(); Check(session.Document.Elements.Count == 0 && !session.CanUndo, "一步撤销全部粘贴");
        var negative = session.InsertCopies(new[] { a, b }, new Point(int.MinValue, int.MinValue));
        Check(negative[0].Bounds.Location == Point.Empty && negative[1].Bounds.Location == new Point(120, 80), "极端负偏移也整体贴到左上，不溢出");
        session.Undo();
        var single = session.InsertCopies(new[] { Element(20, 20, 500, 400) }, new Point(int.MaxValue, int.MaxValue)).Single();
        Check(single.Bounds == new Rectangle(0, 0, 320, 240), "单组件继续允许缩小到页面内且偏移不回绕");
        var roomy = Session();
        var offset = roomy.InsertCopies(new[] { a, b }, new Point(24, 35));
        Check(offset[0].X == a.X + 24 && offset[1].Y == b.Y + 35, "空间充足时偏移相对原来源坐标");
        int count = roomy.Document.Elements.Count;
        Check(roomy.InsertCopies(new RuntimeLayoutElement[0], Point.Empty).Count == 0 && roomy.Document.Elements.Count == count, "空批量插入无操作");
    }

    /// <summary>大组、数量溢出和非法绑定都原子拒绝，并保留已有重做历史。</summary>
    private static void TestInsertRollback()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 320, Height = 240 });
        var initial = session.Add(RuntimeWidgetKind.Text, Point.Empty); session.Undo();
        string before = Snapshot(session);
        Reject<InvalidOperationException>(() => session.InsertCopies(new[] { Element(0, 0, 200), Element(220, 0, 200) }, Point.Empty), "整体宽度超过页面必须拒绝");
        Reject<InvalidOperationException>(() => session.InsertCopies(new[] { Element(0, 0, 80, 200), Element(0, 220, 80, 200) }, Point.Empty), "整体高度超过页面必须拒绝");
        Check(Snapshot(session) == before && session.CanRedo && !session.CanUndo, "放不下的组不修改文档或清空重做分支");
        var invalid = Element(100, 0); invalid.Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, NodePath = new[] { 1 }, PropertyPath = "Image" };
        Reject<InvalidDataException>(() => session.InsertCopies(new[] { Element(0, 0), invalid }, Point.Empty), "无效绑定必须拒绝整个组");
        Check(Snapshot(session) == before && session.CanRedo && !session.CanUndo, "中途验证失败回滚整个组并保留历史");
        Reject<ArgumentException>(() => session.InsertCopies(new RuntimeLayoutElement[] { null }, Point.Empty), "空组件不能进入批量插入");
        var many = Enumerable.Range(0, 201).Select(index => Element(0, 0)).ToArray();
        Reject<InvalidOperationException>(() => session.InsertCopies(many, Point.Empty), "批量不能超过200个");
        var crowded = Session(many.Take(199).ToArray()); string crowdedBefore = Snapshot(crowded);
        Reject<InvalidOperationException>(() => crowded.InsertCopies(many.Take(2), Point.Empty), "已有199个时不能再粘贴两个");
        Check(Snapshot(crowded) == crowdedBefore && !crowded.CanUndo, "数量超限不能部分插入");
        Check(crowded.InsertCopies(many.Take(1), Point.Empty).Count == 1 && crowded.Document.Elements.Count == 200, "恰好200个允许提交");
    }

    /// <summary>两轴均按边缘间距排列，保持尺寸、跨轴对齐且只有一个历史项。</summary>
    private static void TestArrange()
    {
        foreach (bool horizontal in new[] { true, false })
        {
            var a = Element(100, 30, 80, 50); var b = Element(400, 200, 100, 60); var c = Element(720, 500, 120, 70); var other = Element(30, 600);
            var session = Session(a, b, c, other); string before = Snapshot(session); int changes = 0;
            session.Changed += (sender, args) => changes++;
            session.Arrange(new[] { c.Id, a.Id, b.Id }, horizontal);
            var values = new[] { session.Find(a.Id), session.Find(b.Id), session.Find(c.Id) };
            Check(changes == 1 && values.Select(value => value.Bounds.Size).SequenceEqual(new[] { a, b, c }.Select(value => value.Bounds.Size)), "排列不改变尺寸且只通知一次");
            if (horizontal)
                Check(values[0].X == 100 && values[1].X == 400 && values[2].X == 720 && values.All(value => value.Y == 30), "横排沿用较大原跨度，按上边对齐及220像素边缘间距");
            else
                Check(values[0].Y == 30 && values[1].Y == 260 && values[2].Y == 500 && values.All(value => value.X == 100), "纵排沿用原跨度，按左边对齐及180像素边缘间距");
            Check(session.Find(other.Id).Bounds == other.Bounds, "排列不影响未选组件");
            string after = Snapshot(session);
            session.Undo(); Check(Snapshot(session) == before && !session.CanUndo, "一次撤销整次排列");
            session.Redo(); Check(Snapshot(session) == after, "一次重做恢复排列");
            int saved = changes; session.Arrange(new[] { a.Id, b.Id, c.Id }, horizontal);
            Check(changes == saved, "已经排列的组件重复操作不产生空编辑");
        }
        var x = Element(300, 30); var y = Element(310, 50); var z = Element(320, 80);
        var smallSpan = Session(x, y, z); smallSpan.Arrange(new[] { x.Id, y.Id, z.Id }, true);
        Check(smallSpan.Find(x.Id).X == 300 && smallSpan.Find(y.Id).X == 392 && smallSpan.Find(z.Id).X == 484, "原跨度不足时扩展到总尺寸加12像素间距");
        var edgeA = Element(190, 50, 100); var edgeB = Element(195, 60, 100); var edgeC = Element(200, 80, 100);
        var edge = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 320, Height = 240, Elements = new List<RuntimeLayoutElement> { edgeA, edgeB, edgeC } });
        edge.Arrange(new[] { edgeA.Id, edgeB.Id, edgeC.Id }, true);
        Check(edge.Find(edgeA.Id).X == 0 && edge.Find(edgeB.Id).X == 110 && edge.Find(edgeC.Id).X == 220, "跨度裁到页长后均分剩余空白并整体移回页内");
        var r1 = Element(0, 20, 80); var r2 = Element(200, 40, 80); var r3 = Element(403, 60, 80);
        var rounded = Session(r1, r2, r3); rounded.Arrange(new[] { r1.Id, r2.Id, r3.Id }, true);
        Check(rounded.Find(r2.Id).X == 202 && rounded.Find(r3.Id).X == 403, "半像素间距累计舍入，末端不能漂移");
    }

    /// <summary>稳定平局、过大尺寸与不足两个选择的边界。</summary>
    private static void TestArrangeFailuresAndTies()
    {
        var a = Element(0, 0, 200, 130); var b = Element(0, 0, 200, 130);
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 320, Height = 240, Elements = new List<RuntimeLayoutElement> { a, b } });
        string before = Snapshot(session); int changes = 0; session.Changed += (sender, args) => changes++;
        Reject<InvalidOperationException>(() => session.Arrange(new[] { a.Id, b.Id }, true), "总宽度不够必须拒绝横排");
        Reject<InvalidOperationException>(() => session.Arrange(new[] { a.Id, b.Id }, false), "总高度不够必须拒绝纵排");
        Check(Snapshot(session) == before && changes == 0 && !session.CanUndo, "排列失败无文档、事件或历史变化");
        session.Arrange(new string[0], true); session.Arrange(new[] { a.Id, a.Id, "无效" }, false);
        Check(changes == 0 && !session.CanUndo, "去重后不足两个有效组件时无操作");
        var first = Element(100, 100); var second = Element(100, 100);
        var tied = Session(first, second); tied.Arrange(new[] { second.Id, first.Id }, true);
        Check(tied.Find(first.Id).X == 100 && tied.Find(second.Id).X == 192, "相同原坐标时稳定保留文档顺序");
    }

    /// <summary>多组件包装沿用所有安全校验，旧单项解码仍严格只接收一个组件。</summary>
    private static void TestCodec()
    {
        var document = new RuntimeLayoutDocument { Width = 2000, Height = 1000 };
        var elements = Enum.GetValues(typeof(RuntimeWidgetKind)).Cast<RuntimeWidgetKind>().Select(RuntimeLayoutSession.CreateElement).ToArray();
        for (int index = 0; index < elements.Length; index++) { elements[index].X = 1000 + index * 20; elements[index].Y = 50 + index * 10; }
        var image = elements.Single(value => value.Kind == RuntimeWidgetKind.Image);
        using (var bitmap = new Bitmap(2, 3))
        using (var data = new MemoryStream()) { bitmap.Save(data, ImageFormat.Png); image.ImageData = Convert.ToBase64String(data.ToArray()); }
        image.Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, NodePath = new[] { 7, 8 }, PropertyPath = "Image" };
        image.RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, NodePath = new[] { 9, 10 }, PropertyPath = "ROI" };
        document.Elements.AddRange(elements);
        string payload = RuntimeWidgetClipboardCodec.Encode(document, elements);
        var decoded = RuntimeWidgetClipboardCodec.DecodeMany(payload);
        Check(decoded.Count == elements.Length && decoded.Select(value => value.Id).SequenceEqual(elements.Select(value => value.Id)), "所有组件类型按输入顺序一起往返");
        Check(JToken.DeepEquals(JToken.FromObject(elements), JToken.FromObject(decoded)), "批量往返完整保留所有组件属性与坐标");
        var wrapper = RuntimeLayoutSerializer.Deserialize(payload);
        Check(wrapper.Width == 2000 && wrapper.Height == 1000, "批量包装保留源页面尺寸");
        decoded[0].Binding.NodePath[0] = 99; decoded[0].RoiBinding.NodePath[0] = 98;
        Check(image.Binding.NodePath[0] == 7 && image.RoiBinding.NodePath[0] == 9, "批量解码不共享来源图像与ROI数组");
        Check(RuntimeWidgetClipboardCodec.DecodeMany(payload)[0].Binding.NodePath[0] == 7, "每次解码返回独立快照");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Decode(payload), "旧Decode必须拒绝多组件载荷");
        string single = RuntimeWidgetClipboardCodec.Encode(document, image);
        Check(RuntimeWidgetClipboardCodec.Decode(single).Id == image.Id && RuntimeWidgetClipboardCodec.DecodeMany(single).Count == 1, "旧单项编码和解码保持兼容");
        var twoHundred = Enumerable.Range(0, 201).Select(index => Element(0, 0)).ToArray();
        Check(RuntimeWidgetClipboardCodec.DecodeMany(RuntimeWidgetClipboardCodec.Encode(document, twoHundred.Take(200))).Count == 200, "剪贴板允许恰好200个组件");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Encode(document, twoHundred), "编码超过200个明确拒绝");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Encode(document, new RuntimeLayoutElement[0]), "空集合不能编码");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.Encode(document, new RuntimeLayoutElement[] { image, null }), "空项不能编码");
        var token = JObject.Parse(payload); token["Elements"] = new JArray();
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.DecodeMany(token.ToString()), "空集合不能解码");
        token = JObject.Parse(payload); token["Elements"][1]["Id"] = token["Elements"][0]["Id"].DeepClone();
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.DecodeMany(token.ToString()), "批量重复标识必须拒绝");
        token = JObject.Parse(payload); token["Elements"][1]["$type"] = "任意类型";
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.DecodeMany(token.ToString()), "批量任意项的类型元数据必须拒绝");
        token = JObject.Parse(payload); token["Elements"][1]["ImageData"] = "损坏图片";
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.DecodeMany(token.ToString()), "批量后续项也必须验证内嵌图片");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.DecodeMany(new string('x', RuntimeLayoutSerializer.MaximumDocumentBytes + 1)), "批量同样严格限制16MB");
        Reject<InvalidDataException>(() => RuntimeWidgetClipboardCodec.DecodeMany("{"), "损坏JSON必须拒绝");
    }
}
