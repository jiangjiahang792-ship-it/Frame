using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>运行显示刷新范围测试，不连接方案、设备或实际运行控制器。</summary>
internal static class RuntimePreviewRefreshTests
{
    /// <summary>累计行为断言数。</summary>
    private static int checks;

    /// <summary>通过真实显示控制器消费确定的后台完成批次。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyRefresh(new[] { new RuntimeValueUpdate { ElementId = "elapsed", ElapsedMilliseconds = 48 } },
                new Rectangle(20, 610, 140, 60), "耗时更新不得重绘图像和按钮");
            VerifyRefresh(new[] { new RuntimeValueUpdate { ElementId = "image", IsRoiUpdate = true, Status = "等待ROI" },
                new RuntimeValueUpdate { ElementId = "image", Status = "等待图像" } },
                new Rectangle(20, 56, 360, 544), "同一图像与ROI批次只重绘一次图像区域");
            VerifyRefresh(new RuntimeValueUpdate[0], Rectangle.Empty, "空批次不应触发重绘");
            VerifyRefresh(new[] { new RuntimeValueUpdate { ElementId = "removed", Status = "失效" } },
                Rectangle.Empty, "不在页面中的结果不应触发全屏重绘");
            Console.WriteLine("通过：运行显示局部刷新，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>按用户页面比例检查实际失效区域，包含窗口填充缩放。</summary>
    private static void VerifyRefresh(IReadOnlyList<RuntimeValueUpdate> updates, Rectangle expected, string message)
    {
        foreach (int scale in new[] { 1, 2 })
        {
            var document = new RuntimeLayoutDocument { Width = 1280, Height = 720 };
            document.Elements.Add(new RuntimeLayoutElement { Id = "image", Kind = RuntimeWidgetKind.Image,
                X = 20, Y = 56, Width = 360, Height = 544 });
            document.Elements.Add(new RuntimeLayoutElement { Id = "elapsed", Kind = RuntimeWidgetKind.Elapsed,
                X = 20, Y = 610, Width = 140, Height = 60 });
            document.Elements.Add(new RuntimeLayoutElement { Id = "run", Kind = RuntimeWidgetKind.RunControl,
                X = 540, Y = 0, Width = 128, Height = 56 });
            using (var canvas = new RuntimeDesignCanvas { PreviewMode = true, Size = new Size(1280 * scale, 720 * scale) })
            using (var controller = new RuntimeLiveViewController(canvas, new EmptySource()))
            {
                canvas.Bind(new RuntimeLayoutSession(document));
                IntPtr handle = canvas.Handle;
                var invalidated = new List<Rectangle>();
                canvas.Invalidated += (sender, args) => invalidated.Add(args.InvalidRect);
                typeof(RuntimeLiveViewController).GetField("completed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, updates);
                controller.Tick();
                var scaled = expected.IsEmpty ? Rectangle.Empty : new Rectangle(expected.X * scale, expected.Y * scale,
                    expected.Width * scale, expected.Height * scale);
                Check(scaled.IsEmpty ? invalidated.Count == 0 : invalidated.Count == 1 && invalidated[0] == scaled,
                    message + "；实际=" + string.Join(";", invalidated));
            }
        }
    }

    /// <summary>后台没有新数据，避免测试依赖线程调度时序。</summary>
    private sealed class EmptySource : IRuntimeBindingDataSource
    {
        /// <summary>不产生额外显示更新。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain() => new RuntimeValueUpdate[0];
        /// <summary>没有外部资源需要释放。</summary>
        public void Dispose() { }
    }

    /// <summary>断言失败时报告具体重绘范围。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
}
