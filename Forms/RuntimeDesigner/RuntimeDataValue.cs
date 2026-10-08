using System;
using System.Collections.Generic;
using System.Drawing;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>已从生产结果复制的显示叠加层；纯界面代码不依赖节点或算法程序集。</summary>
    public interface IRuntimeRoiOverlay : IDisposable
    {
        /// <summary>显示快照的保守占用，用于视图总预算。</summary>
        long EstimatedBytes { get; }
        /// <summary>在图像实际显示区域绘制，原始尺寸不受显示位图降采样影响。</summary>
        void Draw(Graphics graphics, RectangleF imageBounds, Size sourceImageSize, Size controlSize);
    }

    /// <summary>已经从生产图像隔离的显示结果，由接收方负责释放。</summary>
    public sealed class RuntimeValueUpdate : IDisposable
    {
        /// <summary>目标控件编号。</summary>
        public string ElementId { get; set; }
        /// <summary>独立显示位图，不属于节点结果。</summary>
        public Bitmap Image { get; set; }
        /// <summary>图像降采样前的像素尺寸，ROI坐标始终以原图为基准。</summary>
        public Size ImageSourceSize { get; set; }
        /// <summary>本次增量只替换ROI及其状态，不触及图像或耗时。</summary>
        public bool IsRoiUpdate { get; set; }
        /// <summary>独立的显示叠加快照，由当前结果持有。</summary>
        public IRuntimeRoiOverlay Roi { get; set; }
        /// <summary>合并到显示帧后的ROI状态，与图像状态分别保留。</summary>
        public string RoiStatus { get; set; }
        /// <summary>框架已记录的毫秒值。</summary>
        public double? ElapsedMilliseconds { get; set; }
        /// <summary>等待、失败或失效状态；成功时为空。</summary>
        public string Status { get; set; }
        /// <summary>释放本次显示图像。</summary>
        public void Dispose() { Image?.Dispose(); Image = null; Roi?.Dispose(); Roi = null; }
    }

    /// <summary>运行结果读取接口；读取仅转换已接收结果，不执行节点或设备指令。</summary>
    public interface IRuntimeBindingDataSource : IDisposable
    {
        /// <summary>提取各控件最新结果，允许后台线程调用，同一实例不并发调用。</summary>
        IReadOnlyList<RuntimeValueUpdate> Drain();
    }
}
