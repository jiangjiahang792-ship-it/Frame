using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp.Extensions;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>订阅现有运行通知，每个控件仅保留一份待显示结果。</summary>
    public sealed class RuntimeBindingDataSource : IRuntimeBindingDataSource
    {
        /// <summary>一个绑定的独立收件槽，不持有通信锁。</summary>
        private sealed class Slot
        {
            /// <summary>目标控件。</summary>
            public string ElementId;
            /// <summary>完整绑定副本。</summary>
            public RuntimeDataBinding Binding;
            /// <summary>固定根流程实例，防止换方案后串入同编号流程。</summary>
            public Process Root;
            /// <summary>保护租约交换和关闭；不覆盖转换或绘制。</summary>
            public readonly object Sync = new object();
            /// <summary>待转换的最新图像租约。</summary>
            public IImageResourceLease Lease;
            /// <summary>待显示的最新ROI快照，与图像租约分别交换。</summary>
            public IRuntimeRoiOverlay Roi;
            /// <summary>最新耗时。</summary>
            public double? Elapsed;
            /// <summary>最新状态。</summary>
            public string Status = "等待数据";
            /// <summary>是否有未提取更新。</summary>
            public bool Dirty = true;
            /// <summary>是否已经关闭。</summary>
            public bool Closed;
            /// <summary>上次可用的节点实例，用于识别模块重建和删除。</summary>
            public NodeBase LastNode;
        }

        /// <summary>本视图涉及的有限绑定集合。</summary>
        private readonly Slot[] slots;

        /// <summary>建立绑定快照并接收后续运行事件，不执行任何流程。</summary>
        public RuntimeBindingDataSource(RuntimeLayoutDocument document, IEnumerable<Process> processes)
        {
            RuntimeLayoutSerializer.Validate(document);
            var roots = processes.ToArray();
            slots = document.Elements.SelectMany(element => new[] { element.Binding, element.RoiBinding }
                .Where(binding => binding != null).Select(binding => new Slot
                {
                    ElementId = element.Id, Binding = binding.Copy(),
                    Root = roots.FirstOrDefault(p => p.ID == binding.ProcessId)
                })).ToArray();
            Process.UpdateRunStatus += ProcessChanged;
            foreach (var slot in slots)
            {
                if (slot.Root == null) { Publish(slot, null, null, "数据源已失效"); continue; }
                if (slot.Binding.Kind == RuntimeBindingKind.ProcessElapsed)
                {
                    if (slot.Root.CurrentRunId > 0 && !slot.Root.IsRuning)
                        Publish(slot, null, slot.Root.RunTime, null);
                }
                else
                {
                    var node = RuntimeBindingCatalog.ResolveNode(slot.Root, slot.Binding);
                    slot.LastNode = node;
                    if (node == null) Publish(slot, null, null, "数据源已失效");
                    else if (!node.Process.IsRuning && node.RuntimeStatus == NodeStatus.Successful) Capture(slot, node);
                }
            }
        }

        /// <summary>流程完成后结果已就绪；节点成功状态通知可能早于结果替换，因此不在该通知中取图。</summary>
        private void ProcessChanged(object sender, ProcessRunResult result)
        {
            if (result.IsRunning) return;
            var process = sender as Process;
            if (process == null) return;
            bool hasTerminalResult = !result.IsCancelled;
            foreach (var slot in slots)
            {
                try
                {
                    NodeBase node = null;
                    if (slot.Binding.Kind == RuntimeBindingKind.ProcessElapsed)
                    {
                        if (!ReferenceEquals(slot.Root, process)) continue;
                    }
                    else
                    {
                        node = RuntimeBindingCatalog.ResolveNode(slot.Root, slot.Binding);
                        if (node == null || !ReferenceEquals(node.Process, process)) continue;
                    }
                    // 仅明确取消时整轮保留，避免半轮新图配旧ROI；未标记取消的异常必须显示失败。
                    // 仅检查已匹配的流程实例且每次通知最多扫描一次，不按可能重复的全局流程编号关联终态。
                    if (!hasTerminalResult)
                    {
                        if (!process.Nodes.Any(item => item.RuntimeStatus == NodeStatus.Failed)) return;
                        hasTerminalResult = true;
                    }
                    if (slot.Binding.Kind == RuntimeBindingKind.ProcessElapsed)
                    {
                        Publish(slot, null, process.RunTime, result.IsSuccess ? null : "流程运行失败");
                        continue;
                    }
                    if (node.RuntimeStatus == NodeStatus.Successful && result.IsSuccess) Capture(slot, node);
                    else if (node.RuntimeStatus == NodeStatus.Failed) Publish(slot, null, null, "节点运行失败", node);
                    else if (!result.IsSuccess && !result.IsCancelled) Publish(slot, null, null, "流程运行失败", node);
                    // 成功流程中的未执行节点没有新结果；保留已显示数据和槽内尚未消费的成功帧。
                    // 来源删除及模块重建仍由Drain的身份检查清除，真实失败不走保留路径。
                }
                catch (Exception) { Publish(slot, null, null, "数据源暂不可用"); }
            }
        }

        /// <summary>提取已完成节点的公开输出，未匹配的属性不尝试按同名回退。</summary>
        private static void Capture(Slot slot, NodeBase node)
        {
            IImageResourceLease lease = null;
            IRuntimeRoiOverlay roi = null;
            try
            {
                if (slot.Binding.Kind == RuntimeBindingKind.NodeElapsed)
                {
                    double elapsed;
                    if (double.TryParse(node.RuntimeTimeText, out elapsed)) Publish(slot, null, elapsed, null, node);
                    return;
                }
                var result = node.Result;
                bool isRoi = slot.Binding.Kind == RuntimeBindingKind.Roi;
                var descriptor = (isRoi ? RuntimeBindingCatalog.GetRoiOutputs(node) : RuntimeBindingCatalog.GetImageOutputs(node))
                    .FirstOrDefault(o => o.PropertyPath == slot.Binding.PropertyPath);
                if (descriptor == null) { Publish(slot, null, null, "输出项已失效", node); return; }
                object value;
                if (descriptor.IsDynamic) DynamicResultVariableResolver.TryGetValue(result, descriptor.PropertyPath, out value);
                else value = descriptor.Property?.GetValue(result, null);
                if (isRoi)
                {
                    var algorithm = value as AlgorithmResult;
                    if (algorithm == null) { Publish(slot, null, null, "等待ROI", node); return; }
                    roi = RuntimeRoiSnapshot.Create(algorithm);
                    Publish(slot, null, null, null, node, roi);
                    roi = null;
                    return;
                }
                var output = value as OutputImage;
                if (output == null) { Publish(slot, null, null, "等待图像", node); return; }
                lease = output.AcquireLease();
                Publish(slot, lease, null, null, node);
                lease = null;
            }
            catch (ObjectDisposedException) { Publish(slot, null, null, "等待下一帧"); }
            catch (Exception) { Publish(slot, null, null, "读取结果失败"); }
            finally { lease?.Dispose(); roi?.Dispose(); }
        }

        /// <summary>交换最新结果；被后续帧替代的租约立即释放，收件槽长度始终为一。</summary>
        private static void Publish(Slot slot, IImageResourceLease lease, double? elapsed, string status, NodeBase node = null,
            IRuntimeRoiOverlay roi = null)
        {
            IImageResourceLease previous;
            IRuntimeRoiOverlay previousRoi;
            lock (slot.Sync)
            {
                if (slot.Closed || (node != null && !ReferenceEquals(node, RuntimeBindingCatalog.ResolveNode(slot.Root, slot.Binding))))
                { previous = lease; previousRoi = roi; }
                else
                {
                    if (node != null) slot.LastNode = node;
                    previous = slot.Lease;
                    previousRoi = slot.Roi; slot.Roi = roi;
                    slot.Lease = lease; slot.Elapsed = elapsed; slot.Status = status; slot.Dirty = true;
                }
            }
            previous?.Dispose();
            previousRoi?.Dispose();
        }

        /// <summary>后台提取最新结果，在租约保护下转换首张图片，显示最长边限制为 1600。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain()
        {
            var updates = new List<RuntimeValueUpdate>();
            foreach (var slot in slots)
            {
                IImageResourceLease lease;
                IImageResourceLease discarded = null;
                IRuntimeRoiOverlay discardedRoi = null;
                RuntimeValueUpdate update;
                lock (slot.Sync)
                {
                    if (slot.Closed) continue;
                    // 身份检查与结果交换共用收件槽锁，不能让等待状态覆盖并发到达的新帧。
                    if (slot.Binding.Kind != RuntimeBindingKind.ProcessElapsed)
                    {
                        try
                        {
                            var node = RuntimeBindingCatalog.ResolveNode(slot.Root, slot.Binding);
                            bool missingRoiOutput = node != null && slot.Binding.Kind == RuntimeBindingKind.Roi &&
                                !RuntimeBindingCatalog.GetRoiOutputs(node).Any(output => output.PropertyPath == slot.Binding.PropertyPath);
                            if (!ReferenceEquals(slot.LastNode, node) || (missingRoiOutput && slot.Status != "输出项已失效"))
                            {
                                slot.LastNode = node;
                                discarded = slot.Lease; slot.Lease = null; slot.Elapsed = null;
                                discardedRoi = slot.Roi; slot.Roi = null;
                                slot.Status = node == null ? "数据源已失效" : missingRoiOutput ? "输出项已失效" : "等待数据";
                                slot.Dirty = true;
                            }
                        }
                        catch (Exception)
                        {
                            // 节点列表正在编辑时只使当前槽位失效，保留本批已转换的其他图像。
                            discarded = slot.Lease; slot.Lease = null; slot.Elapsed = null;
                            discardedRoi = slot.Roi; slot.Roi = null;
                            slot.Status = "数据源暂不可用"; slot.Dirty = true;
                        }
                    }
                    if (!slot.Dirty) continue;
                    slot.Dirty = false;
                    lease = slot.Lease; slot.Lease = null;
                    update = new RuntimeValueUpdate { ElementId = slot.ElementId, ElapsedMilliseconds = slot.Elapsed, Status = slot.Status,
                        IsRoiUpdate = slot.Binding.Kind == RuntimeBindingKind.Roi, Roi = slot.Roi };
                    slot.Roi = null;
                }
                discarded?.Dispose();
                discardedRoi?.Dispose();
                try
                {
                    if (lease != null)
                    {
                        var image = lease.Image.Bitmaps?.FirstOrDefault();
                        if (image == null || image.Empty()) update.Status = "等待图像";
                        else
                        {
                            update.ImageSourceSize = new System.Drawing.Size(image.Width, image.Height);
                            if (Math.Max(image.Width, image.Height) <= 1600) update.Image = image.ToBitmap();
                            else
                            {
                                double scale = 1600.0 / Math.Max(image.Width, image.Height);
                                using (var reduced = new OpenCvSharp.Mat())
                                {
                                    OpenCvSharp.Cv2.Resize(image, reduced, new OpenCvSharp.Size(
                                        Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale))), 0, 0, OpenCvSharp.InterpolationFlags.Area);
                                    update.Image = reduced.ToBitmap();
                                }
                            }
                        }
                    }
                }
                catch (Exception) { update.Dispose(); update.Status = "图像转换失败"; }
                finally { lease?.Dispose(); }
                updates.Add(update);
            }
            return updates;
        }

        /// <summary>退订并释放全部待转换租约；关闭后迟到的通知不会再进入收件槽。</summary>
        public void Dispose()
        {
            Process.UpdateRunStatus -= ProcessChanged;
            foreach (var slot in slots)
            {
                IImageResourceLease lease;
                IRuntimeRoiOverlay roi;
                lock (slot.Sync) { slot.Closed = true; lease = slot.Lease; slot.Lease = null; roi = slot.Roi; slot.Roi = null; }
                lease?.Dispose();
                roi?.Dispose();
            }
        }
    }
}
