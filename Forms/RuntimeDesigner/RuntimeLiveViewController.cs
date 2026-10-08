using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>独立的显示调度器：后台串行转换、界面定时取最新批次，绝不串行化生产通信。</summary>
    public sealed class RuntimeLiveViewController : IDisposable
    {
        /// <summary>注入的运行结果来源。</summary>
        private readonly IRuntimeBindingDataSource source;
        /// <summary>目标画布。</summary>
        private readonly RuntimeDesignCanvas canvas;
        /// <summary>界面线程持有的已显示结果。</summary>
        private readonly Dictionary<string, RuntimeValueUpdate> displayed = new Dictionary<string, RuntimeValueUpdate>();
        /// <summary>后台与界面间只交换一个完成批次。</summary>
        private readonly object sync = new object();
        /// <summary>尚未显示的后台结果。</summary>
        private IReadOnlyList<RuntimeValueUpdate> completed;
        /// <summary>各控件最后一次清除的批次代数，仅用于丢弃清除前已开始的转换。</summary>
        private readonly Dictionary<string, long> clearedEpochs = new Dictionary<string, long>(StringComparer.Ordinal);
        /// <summary>全局递增的显示清除序号，不改变生产或订阅序号。</summary>
        private long clearEpoch;
        /// <summary>当前是否有转换任务，最多一个。</summary>
        private int working;
        /// <summary>是否已释放。</summary>
        private bool disposed;
        /// <summary>显示帧总预算，防止大量大图控件常驻内存失控。</summary>
        private const long MaximumImageBytes = 128L * 1024 * 1024;
        /// <summary>当前显示帧占用估算。</summary>
        private long imageBytes;
        /// <summary>ROI独立总预算，不挤占图像的显示配额。</summary>
        private const long MaximumRoiBytes = 32L * 1024 * 1024;
        /// <summary>当前显示ROI快照占用。</summary>
        private long roiBytes;
        /// <summary>最近一次读取异常，由宿主在界面线程读取并呈现，不依赖生产日志控件。</summary>
        public string LastError { get; private set; }

        /// <summary>连接运行渲染器，不启动任何生产流程。</summary>
        public RuntimeLiveViewController(RuntimeDesignCanvas canvas, IRuntimeBindingDataSource source)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            canvas.Register(new RuntimeLiveWidgetRenderer(RuntimeWidgetKind.Image, Find, canvas.GetImageViewport));
            canvas.Register(new RuntimeLiveWidgetRenderer(RuntimeWidgetKind.Elapsed, Find));
            canvas.ImageClearRequested += ClearImage;
        }

        /// <summary>供运行渲染器读取当前帧，调用方不得释放或修改返回值。</summary>
        private RuntimeValueUpdate Find(string id)
        { RuntimeValueUpdate value; return displayed.TryGetValue(id, out value) ? value : null; }

        /// <summary>界面线程清除显示副本及已完成批次，不退订来源或修改生产结果。</summary>
        private void ClearImage(string id)
        {
            var discarded = new List<RuntimeValueUpdate>();
            lock (sync)
            {
                if (disposed) return;
                clearedEpochs[id] = ++clearEpoch;
                if (completed != null)
                {
                    var retained = new List<RuntimeValueUpdate>();
                    foreach (var value in completed)
                        if (value.ElementId == id) discarded.Add(value); else retained.Add(value);
                    completed = retained;
                }
            }
            RuntimeValueUpdate current;
            if (displayed.TryGetValue(id, out current))
            {
                imageBytes -= ImageBytes(current);
                roiBytes -= current.Roi?.EstimatedBytes ?? 0;
                displayed.Remove(id);
                current.Dispose();
            }
            foreach (var value in discarded) value.Dispose();
        }

        /// <summary>由界面定时器调用，先消费结果再按需安排一个后台任务。</summary>
        public void Tick()
        {
            IReadOnlyList<RuntimeValueUpdate> ready;
            lock (sync)
            {
                if (disposed) return;
                ready = completed; completed = null;
            }
            if (ready != null)
            {
                var changed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in ready)
                {
                    changed.Add(value.ElementId);
                    RuntimeValueUpdate old;
                    if (!displayed.TryGetValue(value.ElementId, out old))
                        displayed[value.ElementId] = old = new RuntimeValueUpdate
                        { ElementId = value.ElementId, Status = "等待数据", RoiStatus = "等待ROI" };
                    if (value.IsRoiUpdate)
                    {
                        roiBytes -= old.Roi?.EstimatedBytes ?? 0;
                        old.Roi?.Dispose(); old.Roi = null;
                        old.RoiStatus = value.Status;
                        long bytes = value.Roi?.EstimatedBytes ?? 0;
                        if (bytes < 0 || bytes > MaximumRoiBytes - roiBytes)
                            old.RoiStatus = "ROI超出显示内存预算";
                        else
                        {
                            old.Roi = value.Roi; value.Roi = null;
                            roiBytes += bytes;
                        }
                    }
                    else
                    {
                        imageBytes -= ImageBytes(old);
                        old.Image?.Dispose(); old.Image = null;
                        old.ImageSourceSize = value.ImageSourceSize;
                        old.ElapsedMilliseconds = value.ElapsedMilliseconds;
                        old.Status = value.Status;
                        long bytes = ImageBytes(value);
                        if (bytes > MaximumImageBytes - imageBytes)
                            old.Status = "显示图像超出内存预算";
                        else
                        {
                            old.Image = value.Image; value.Image = null;
                            imageBytes += bytes;
                            if (old.Image != null && string.IsNullOrEmpty(old.Status)) canvas.NotifyImageFrame(value.ElementId);
                        }
                    }
                    value.Dispose();
                }
                canvas.InvalidateElements(changed);
            }
            long startedEpoch;
            lock (sync)
            {
                if (disposed || completed != null || Interlocked.CompareExchange(ref working, 1, 0) != 0) return;
                startedEpoch = clearEpoch;
            }
            Task.Run(() => ConvertLatest(startedEpoch));
        }

        /// <summary>后台转换期间不持有界面同步锁；关闭后生成的结果立即释放。</summary>
        private void ConvertLatest(long startedEpoch)
        {
            IReadOnlyList<RuntimeValueUpdate> updates = null;
            try
            {
                updates = source.Drain();
                LastError = null;
                lock (sync)
                {
                    if (!disposed)
                    {
                        var retained = new List<RuntimeValueUpdate>();
                        foreach (var value in updates ?? Array.Empty<RuntimeValueUpdate>())
                        {
                            long cleared;
                            if (clearedEpochs.TryGetValue(value.ElementId, out cleared) && cleared > startedEpoch)
                                value.Dispose();
                            else retained.Add(value);
                        }
                        completed = retained;
                        updates = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                System.Diagnostics.Trace.TraceWarning("运行界面刷新失败：" + ex.Message);
            }
            finally
            {
                if (updates != null) foreach (var value in updates) value.Dispose();
                Interlocked.Exchange(ref working, 0);
            }
        }

        /// <summary>按四字节像素保守估算显示帧占用。</summary>
        private static long ImageBytes(RuntimeValueUpdate value) => value.Image == null ? 0 : (long)value.Image.Width * value.Image.Height * 4;

        /// <summary>界面线程关闭时退订并释放已显示及等待显示的资源，不等待后台转换完成。</summary>
        public void Dispose()
        {
            IReadOnlyList<RuntimeValueUpdate> pending;
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                pending = completed; completed = null;
                clearedEpochs.Clear();
            }
            canvas.ImageClearRequested -= ClearImage;
            source.Dispose();
            if (pending != null) foreach (var value in pending) value.Dispose();
            foreach (var value in displayed.Values) value.Dispose();
            displayed.Clear();
            imageBytes = 0;
            roiBytes = 0;
        }
    }
}
