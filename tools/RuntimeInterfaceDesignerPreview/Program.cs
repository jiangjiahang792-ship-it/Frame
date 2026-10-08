using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

namespace TDJS_Vision.Tools.RuntimeInterfaceDesignerPreview
{
    /// <summary>独立体验入口，复用框架编辑器，不构造方案或通信设备。</summary>
    internal static class Program
    {
        /// <summary>打开编辑器，编辑内容只保存到体验目录内的模板。</summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "运行控制体验.runtime-ui.json");
                var document = File.Exists(path) ? RuntimeLayoutSerializer.Load(path) : DemoDocument();
                using (var runController = new RuntimePreviewRunController(new[] {
                    new RuntimeProcessChoice { Id = 1, Name = "工位1（演示）" },
                    new RuntimeProcessChoice { Id = 2, Name = "工位2（演示）" } }))
                using (var editor = new RuntimeInterfaceEditorForm(document, "运行控制体验（模拟数据，不连接设备）", layout =>
                {
                    RuntimeLayoutSerializer.Save(path, layout);
                    return true;
                }, new DemoCatalog(), layout => new DemoSource(layout), runController)) Application.Run(editor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("设计器体验启动失败：" + ex.Message, "运行界面设计", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>仅供独立体验的新文档，不覆盖用户此前保存的布局。</summary>
        private static RuntimeLayoutDocument DemoDocument()
        {
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Name = "工位1图像",
                X = 24, Y = 24, Width = 920, Height = 672, Binding = DemoCatalog.ImageBinding() });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed, Name = "工位1耗时",
                Text = "运行耗时", X = 968, Y = 24, Width = 288, Height = 110, Binding = DemoCatalog.ElapsedBinding() });
            var control = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.RunControl);
            control.X = 968; control.Y = 160; control.Width = 192; control.Height = 72;
            document.Elements.Add(control);
            return document;
        }

        /// <summary>纯内存演示目录，不创建生产流程、相机或通信对象。</summary>
        private sealed class DemoCatalog : IRuntimeBindingCatalog
        {
            /// <summary>演示图像绑定，包含组合模块作用域。</summary>
            public static RuntimeDataBinding ImageBinding() => new RuntimeDataBinding { Kind = RuntimeBindingKind.Image,
                ProcessId = 1, NodePath = new[] { 62, 8 }, PropertyPath = "OutputImage", DisplayPath = "工位1（演示） / 配方组合模块 / 图像源 / 输出图像" };
            /// <summary>演示流程耗时绑定。</summary>
            public static RuntimeDataBinding ElapsedBinding() => new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed,
                ProcessId = 1, DisplayPath = "工位1（演示） / 流程运行耗时" };
            /// <summary>按控件种类返回可点选目录。</summary>
            public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind)
            {
                var root = new RuntimeBindingChoice { Name = "工位1（演示）" };
                if (kind == RuntimeWidgetKind.Image)
                    root.Children.Add(new RuntimeBindingChoice { Name = "62.配方组合模块", Children = new List<RuntimeBindingChoice>
                    { new RuntimeBindingChoice { Name = "8.图像源", Children = new List<RuntimeBindingChoice>
                        { new RuntimeBindingChoice { Name = "输出图像", Binding = ImageBinding() } } } } });
                else if (kind == RuntimeWidgetKind.Elapsed)
                    root.Children.Add(new RuntimeBindingChoice { Name = "流程运行耗时", Binding = ElapsedBinding() });
                return new[] { root };
            }
            /// <summary>演示目录的中文路径。</summary>
            public string Describe(RuntimeDataBinding binding) => binding?.DisplayPath ?? "未绑定";
        }

        /// <summary>只在独立体验工具中生成模拟结果，生产程序集不含此类。</summary>
        private sealed class DemoSource : IRuntimeBindingDataSource
        {
            /// <summary>本次预览涉及的控件副本。</summary>
            private readonly RuntimeLayoutElement[] elements;
            /// <summary>模拟帧编号。</summary>
            private int frame;
            /// <summary>上次演示帧生成时间。</summary>
            private DateTime lastFrame = DateTime.MinValue;
            /// <summary>记录释放状态。</summary>
            private volatile bool disposed;
            /// <summary>接收演示布局。</summary>
            public DemoSource(RuntimeLayoutDocument document) { elements = document.Elements.Select(e => e.Copy()).ToArray(); }
            /// <summary>每半秒生成一组模拟图像和耗时，便于验证绑定和刷新。</summary>
            public IReadOnlyList<RuntimeValueUpdate> Drain()
            {
                var updates = new List<RuntimeValueUpdate>();
                if (disposed || (DateTime.UtcNow - lastFrame).TotalMilliseconds < 500) return updates;
                lastFrame = DateTime.UtcNow; frame++;
                foreach (var element in elements.Where(e => e.Binding != null))
                {
                    var update = new RuntimeValueUpdate { ElementId = element.Id };
                    if (element.Kind == RuntimeWidgetKind.Elapsed) update.ElapsedMilliseconds = 18 + frame % 9 * 0.7;
                    else if (element.Kind == RuntimeWidgetKind.Image)
                    {
                        update.Image = new Bitmap(800, 560);
                        using (var graphics = Graphics.FromImage(update.Image))
                        using (var font = new Font("Microsoft YaHei UI", 22))
                        using (var pen = new Pen(Color.FromArgb(54, 197, 140), 3))
                        {
                            graphics.Clear(Color.FromArgb(36, 38, 40));
                            graphics.DrawString("模拟图像   " + frame.ToString("D4"), font, Brushes.White, 24, 24);
                            graphics.FillRectangle(Brushes.Silver, 150, 190, 490, 160);
                            graphics.FillRectangle(Brushes.DimGray, 230, 190, 18, 160);
                            graphics.FillRectangle(Brushes.DimGray, 535, 190, 18, 160);
                            graphics.DrawRectangle(pen, 138 + frame % 3, 178, 514, 184);
                        }
                    }
                    updates.Add(update);
                }
                return updates;
            }
            /// <summary>停止生成模拟数据。</summary>
            public void Dispose() { disposed = true; }
        }
    }
}
