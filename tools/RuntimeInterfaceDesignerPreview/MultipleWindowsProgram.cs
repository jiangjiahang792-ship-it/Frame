using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

namespace TDJS_Vision.Tools.RuntimeInterfaceDesignerPreview
{
    /// <summary>独立的多窗口模拟体验入口，不构造方案、设备或生产运行适配器。</summary>
    internal static class MultipleWindowsProgram
    {
        /// <summary>启动多页设计器及两个独立模拟窗口，全部保存仅存在于本次进程内存中。</summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                using (var demo = new DemoSession()) Application.Run(demo.Editor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("模拟体验启动失败：" + ex.Message, "多窗体体验（模拟数据，不连接设备）",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>统一持有模拟控制器、设计器和独立窗口，不进行任何文件保存。</summary>
        private sealed class DemoSession : IDisposable
        {
            /// <summary>两个模拟流程的轻量目录。</summary>
            private readonly RuntimeProcessChoice[] processes = {
                new RuntimeProcessChoice { Id = 1, Name = "流程1（模拟）" },
                new RuntimeProcessChoice { Id = 2, Name = "流程2（模拟）" }
            };
            /// <summary>仅由体验会话释放的共享模拟状态机。</summary>
            private readonly RuntimePreviewRunController simulation;
            /// <summary>窗口借用的接口，不把共享模拟状态机的所有权交给单个窗口。</summary>
            private readonly BorrowedController controller;
            /// <summary>纯内存图像、耗时与区域目录。</summary>
            private readonly DemoCatalog catalog = new DemoCatalog();
            /// <summary>本次体验保存的页面集合。</summary>
            private List<RuntimeInterfacePage> pages;
            /// <summary>当前打开的独立窗口，关闭设计器时统一释放。</summary>
            private readonly List<RuntimeInterfacePreviewForm> windows = new List<RuntimeInterfacePreviewForm>();
            /// <summary>复用正式多页设计器，不添加体验专属固定控件。</summary>
            public RuntimeInterfaceEditorForm Editor { get; }

            /// <summary>准备两个页面；设计器显示时打开对应的独立模拟运行窗体。</summary>
            public DemoSession()
            {
                simulation = new RuntimePreviewRunController(processes);
                controller = new BorrowedController(simulation);
                pages = new List<RuntimeInterfacePage> {
                    new RuntimeInterfacePage { Layout = CreateDocument(1) },
                    new RuntimeInterfacePage { Layout = CreateDocument(2) }
                };
                Editor = RuntimeInterfaceEditorForm.CreateForPages(pages, "模拟数据，不连接设备；保存仅内存",
                    SavePages, catalog, layout => new DemoSource(layout), controller);
                MarkSimulation(Editor, "运行界面设计");
                Editor.TextChanged += (sender, args) =>
                {
                    const string title = "运行界面设计（模拟数据，不连接设备）";
                    if (Editor.Text != title) Editor.Text = title;
                };
                Editor.Shown += (sender, args) =>
                {
                    OpenWindows();
                    Editor.Activate();
                };
            }

            /// <summary>保存独立副本到内存并重建体验窗口，模拟执行状态仍由同一控制器持有。</summary>
            private bool SavePages(IReadOnlyList<RuntimeInterfacePage> saved)
            {
                pages = RuntimeInterfacePage.ClonePages(saved);
                if (Editor.Visible)
                {
                    OpenWindows();
                    Editor.Activate();
                }
                return true;
            }

            /// <summary>各窗口没有所有者依附关系，关闭其中一个不会关闭另一个或停止共享模拟运行。</summary>
            private void OpenWindows()
            {
                foreach (var window in windows) window.Dispose();
                windows.Clear();
                for (int index = 0; index < pages.Count; index++)
                {
                    var page = pages[index];
                    var source = new DemoSource(page.Layout);
                    RuntimeInterfacePreviewForm window;
                    try { window = new RuntimeInterfacePreviewForm(page.Layout, source, null); }
                    catch { source.Dispose(); throw; }
                    try
                    {
                        window.AttachRunController(controller);
                        window.ConfigureRuntimeWindow(page.Layout.Name + "（模拟数据，不连接设备）", index, pages.Count);
                        windows.Add(window);
                        window.Show();
                    }
                    catch { window.Dispose(); throw; }
                }
            }

            /// <summary>结束体验时释放所有窗口及其数据源，再取消共享模拟运行。</summary>
            public void Dispose()
            {
                foreach (var window in windows) window.Dispose();
                windows.Clear();
                Editor.Dispose();
                simulation.Dispose();
            }
        }

        /// <summary>提供借用语义，让多个正式预览窗口共享同一模拟状态机。</summary>
        private sealed class BorrowedController : IRuntimeRunController
        {
            /// <summary>由外部体验会话拥有的唯一模拟状态机。</summary>
            private readonly RuntimePreviewRunController simulation;
            /// <summary>只保存借用引用，不实现释放接口。</summary>
            public BorrowedController(RuntimePreviewRunController simulation) { this.simulation = simulation; }
            /// <summary>共享运行状态。</summary>
            public bool IsRunning => simulation.IsRunning;
            /// <summary>共享停止状态。</summary>
            public bool IsStopping => simulation.IsStopping;
            /// <summary>返回模拟流程目录。</summary>
            public IReadOnlyList<RuntimeProcessChoice> GetProcesses() => simulation.GetProcesses();
            /// <summary>派发到纯延迟模拟，不调用任何生产接口。</summary>
            public Task RunAsync(int? processId, bool continuous) => simulation.RunAsync(processId, continuous);
            /// <summary>停止所有体验窗口共用的模拟任务。</summary>
            public Task StopAsync() => simulation.StopAsync();
        }

        /// <summary>构造图像、独立区域来源、耗时和运行按钮的初始页面。</summary>
        private static RuntimeLayoutDocument CreateDocument(int processId)
        {
            var document = new RuntimeLayoutDocument { Name = "工位" + processId + "模拟界面" };
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Name = "模拟图像",
                X = 24, Y = 24, Width = 896, Height = 672, Binding = DemoCatalog.Binding(processId, RuntimeBindingKind.Image),
                RoiBinding = DemoCatalog.Binding(3 - processId, RuntimeBindingKind.Roi) });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed, Name = "模拟耗时",
                Text = "运行耗时", X = 944, Y = 24, Width = 312, Height = 110,
                Binding = DemoCatalog.Binding(processId, RuntimeBindingKind.ProcessElapsed) });
            var run = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.RunControl);
            run.X = 944; run.Y = 160; run.Width = 256; run.Height = 80;
            run.RunProcessId = processId == 1 ? (int?)null : 2;
            document.Elements.Add(run);
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, Name = "模拟说明",
                X = 944, Y = 280, Width = 312, Height = 128, FontSize = 18,
                Text = "模拟数据\r\n不连接设备\r\n保存仅本次内存" });
            return document;
        }

        /// <summary>复用既有标题控件标明模拟性质，不新增固定控件。</summary>
        private static void MarkSimulation(Form form, string original)
        {
            form.Text = original + "（模拟数据，不连接设备）";
            MarkTitle(form, original);
        }

        /// <summary>定位现有中文标题标签，保留原设计器布局与窗口按钮。</summary>
        private static void MarkTitle(Control parent, string original)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label && control.Text == original) control.Text = original + "（模拟数据，仅内存）";
                MarkTitle(control, original);
            }
        }

        /// <summary>具有两个流程与组合模块的纯内存目录，图像与区域输出独立选择。</summary>
        private sealed class DemoCatalog : IRuntimeRoiBindingCatalog
        {
            /// <summary>创建不同种类的稳定模拟标识。</summary>
            public static RuntimeDataBinding Binding(int processId, RuntimeBindingKind kind)
            {
                string output = kind == RuntimeBindingKind.Image ? "输出图像" : kind == RuntimeBindingKind.Roi ? "输出区域" : "运行耗时";
                return new RuntimeDataBinding { ProcessId = processId, Kind = kind,
                    NodePath = kind == RuntimeBindingKind.ProcessElapsed ? new int[0] : new[] { 10, kind == RuntimeBindingKind.Roi ? 22 : 21 },
                    PropertyPath = kind == RuntimeBindingKind.Image ? "OutputImage" : kind == RuntimeBindingKind.Roi ? "OutputRegion" : string.Empty,
                    DisplayPath = "流程" + processId + "（模拟） / " + output };
            }
            /// <summary>保持旧目录的图像与耗时筛选语义。</summary>
            public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind) =>
                kind == RuntimeWidgetKind.Image ? Choices(RuntimeBindingKind.Image) :
                kind == RuntimeWidgetKind.Elapsed ? Choices(RuntimeBindingKind.ProcessElapsed) : new RuntimeBindingChoice[0];
            /// <summary>只返回区域目录。</summary>
            public IReadOnlyList<RuntimeBindingChoice> GetRoiSources() => Choices(RuntimeBindingKind.Roi);
            /// <summary>根据稳定标识描述模拟来源，不修改保存路径。</summary>
            public string Describe(RuntimeDataBinding binding) => binding == null ? "未绑定" :
                binding.ProcessId == 1 || binding.ProcessId == 2 ? Binding(binding.ProcessId, binding.Kind).DisplayPath : "已失效：模拟流程不存在";
            /// <summary>使用真实树形契约构造两个独立流程的叶子。</summary>
            private static IReadOnlyList<RuntimeBindingChoice> Choices(RuntimeBindingKind kind)
            {
                return new[] { 1, 2 }.Select(id => new RuntimeBindingChoice {
                    Name = "流程" + id + "（模拟）", Children = new List<RuntimeBindingChoice> {
                        new RuntimeBindingChoice { Name = "组合模块", Children = new List<RuntimeBindingChoice> {
                            new RuntimeBindingChoice { Name = kind == RuntimeBindingKind.Roi ? "区域节点" : "图像节点",
                                Children = new List<RuntimeBindingChoice> {
                                    new RuntimeBindingChoice { Name = kind == RuntimeBindingKind.Roi ? "输出区域" :
                                        kind == RuntimeBindingKind.Image ? "输出图像" : "运行耗时", Binding = Binding(id, kind) }
                                } }
                        } }
                    }
                }).ToArray();
            }
        }

        /// <summary>按独立节拍生成图像和区域增量；没有计时器、线程或外部资源订阅。</summary>
        private sealed class DemoSource : IRuntimeBindingDataSource
        {
            /// <summary>冻结本窗口的来源配置。</summary>
            private readonly RuntimeLayoutElement[] elements;
            /// <summary>仅用于模拟节拍的单调时钟。</summary>
            private readonly Stopwatch clock = Stopwatch.StartNew();
            /// <summary>最近已生成的图像节拍。</summary>
            private long imageFrame = -1;
            /// <summary>最近已生成的区域节拍。</summary>
            private long roiFrame = -1;
            /// <summary>关闭时允许正在生成的批次正常交给显示控制器释放。</summary>
            private volatile bool disposed;
            /// <summary>只保留布局副本，不读取当前方案。</summary>
            public DemoSource(RuntimeLayoutDocument layout) { elements = layout.Elements.Select(element => element.Copy()).ToArray(); }
            /// <summary>图像每九百毫秒更新，区域每两百毫秒更新，分别派发并移交所有权。</summary>
            public IReadOnlyList<RuntimeValueUpdate> Drain()
            {
                var values = new List<RuntimeValueUpdate>();
                if (disposed) return values;
                long image = clock.ElapsedMilliseconds / 900;
                long roi = clock.ElapsedMilliseconds / 200;
                bool imageChanged = image != imageFrame;
                bool roiChanged = roi != roiFrame;
                imageFrame = image; roiFrame = roi;
                try
                {
                    foreach (var element in elements)
                    {
                        if (imageChanged && element.Binding != null)
                        {
                            if (element.Kind == RuntimeWidgetKind.Image)
                                values.Add(new RuntimeValueUpdate { ElementId = element.Id, Image = CreateImage(element.Binding.ProcessId, image),
                                    ImageSourceSize = new Size(800, 560) });
                            else if (element.Kind == RuntimeWidgetKind.Elapsed)
                                values.Add(new RuntimeValueUpdate { ElementId = element.Id, ElapsedMilliseconds = 12 + element.Binding.ProcessId * 3 + image % 7 * 0.6 });
                        }
                        if (roiChanged && element.Kind == RuntimeWidgetKind.Image && element.RoiBinding != null)
                            values.Add(new RuntimeValueUpdate { ElementId = element.Id, IsRoiUpdate = true,
                                Roi = new DemoRoiOverlay(element.RoiBinding.ProcessId, roi) });
                    }
                    return values;
                }
                catch { foreach (var value in values) value.Dispose(); throw; }
            }
            /// <summary>图像不包含区域线条，方便实测两条来源的独立性。</summary>
            private static Bitmap CreateImage(int processId, long frame)
            {
                var bitmap = new Bitmap(800, 560);
                try
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    using (var font = new Font("Microsoft YaHei UI", 22))
                    using (var accent = new SolidBrush(processId == 1 ? Color.SteelBlue : Color.MediumSeaGreen))
                    {
                        graphics.Clear(Color.FromArgb(36, 38, 40));
                        graphics.DrawString("流程" + processId + " 模拟图像 " + frame.ToString("D4"), font, Brushes.White, 24, 24);
                        graphics.FillRectangle(Brushes.Silver, 145, 180, 510, 190);
                        graphics.FillRectangle(accent, 170 + (int)(frame % 10) * 8, 210, 100, 130);
                        graphics.FillEllipse(Brushes.DimGray, 480, 220, 105, 105);
                    }
                    return bitmap;
                }
                catch { bitmap.Dispose(); throw; }
            }
            /// <summary>标记关闭，不会停止任何生产或其它窗口的数据源。</summary>
            public void Dispose() { disposed = true; }
        }

        /// <summary>只保存坐标与来源编号的内存区域快照，绘制时才创建短生命周期画笔。</summary>
        private sealed class DemoRoiOverlay : IRuntimeRoiOverlay
        {
            /// <summary>区域来源的模拟流程编号。</summary>
            private readonly int processId;
            /// <summary>独立于图像帧的区域节拍。</summary>
            private readonly long frame;
            /// <summary>快照的释放标志。</summary>
            private bool disposed;
            /// <summary>冻结该批区域坐标。</summary>
            public DemoRoiOverlay(int processId, long frame) { this.processId = processId; this.frame = frame; }
            /// <summary>此快照不持有位图，仅保留少量数值。</summary>
            public long EstimatedBytes => 64;
            /// <summary>把模拟原图坐标映射到实际图像区域，不随窗体尺寸漂移。</summary>
            public void Draw(Graphics graphics, RectangleF imageBounds, Size sourceImageSize, Size controlSize)
            {
                if (disposed || imageBounds.Width <= 0 || imageBounds.Height <= 0) return;
                float x = imageBounds.Width / 800f;
                float y = imageBounds.Height / 560f;
                var bounds = new RectangleF(imageBounds.Left + (130 + frame % 12 * 3) * x,
                    imageBounds.Top + (160 + processId * 4) * y, 530 * x, 220 * y);
                using (var pen = new Pen(processId == 1 ? Color.LimeGreen : Color.Gold, 2))
                using (var font = new Font("Microsoft YaHei UI", 12))
                using (var brush = new SolidBrush(pen.Color))
                {
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    graphics.DrawString("流程" + processId + " ROI " + frame, font, brush, bounds.Left, Math.Max(imageBounds.Top, bounds.Top - 24));
                }
            }
            /// <summary>区域快照没有外部资源，释放后停止绘制。</summary>
            public void Dispose() { disposed = true; }
        }
    }
}
