using System;
using MvCamCtrl.NET;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 相机触发方式。
    /// </summary>
    internal enum CameraTriggerMode
    {
        /// <summary>
        /// 关闭触发模式，由相机连续输出图像。
        /// </summary>
        Continuous,

        /// <summary>
        /// 使用Line0硬件输入触发，与当前视觉主程序测试条件一致。
        /// </summary>
        Line0
    }

    /// <summary>
    /// 单次相机回调各阶段的高精度计时记录。
    /// </summary>
    internal struct FrameTimingRecord
    {
        /// <summary>从本轮启动到进入回调的计时器刻度。</summary>
        public long ElapsedTicks;

        /// <summary>相机SDK帧号。</summary>
        public uint FrameNumber;

        /// <summary>SDK像素格式转换耗时刻度。</summary>
        public long ConvertTicks;

        /// <summary>用转换缓冲区建立Mat头的耗时刻度。</summary>
        public long MatWrapTicks;

        /// <summary>把Mat复制为独立内存对象的耗时刻度。</summary>
        public long MatCloneTicks;

        /// <summary>把最新Mat发布给UI槽位的耗时刻度。</summary>
        public long PublishTicks;

        /// <summary>进入回调到完成业务处理的总耗时刻度。</summary>
        public long CallbackTicks;

        /// <summary>输入图像宽度。</summary>
        public uint Width;

        /// <summary>输入图像高度。</summary>
        public uint Height;

        /// <summary>输入像素格式。</summary>
        public MyCamera.MvGvspPixelType SourcePixelType;

        /// <summary>输出像素格式。</summary>
        public MyCamera.MvGvspPixelType DestinationPixelType;

        /// <summary>SDK像素转换返回码。</summary>
        public int ResultCode;
    }

    /// <summary>
    /// 用户可调的基准测试参数。
    /// </summary>
    internal sealed class BenchmarkSettings
    {
        /// <summary>正式统计前忽略的预热帧数。</summary>
        public int WarmupFrames { get; set; }

        /// <summary>每台相机需要采集的正式样本数。</summary>
        public int SampleFrames { get; set; }

        /// <summary>相机触发方式。</summary>
        public CameraTriggerMode TriggerMode { get; set; }

        /// <summary>是否启用实时图像显示。</summary>
        public bool DisplayEnabled { get; set; }

        /// <summary>UI尝试取得最新帧的刷新频率。</summary>
        public int DisplayFps { get; set; }
    }

    /// <summary>
    /// 分位数统计结果。
    /// </summary>
    internal sealed class TimingStatistics
    {
        /// <summary>参与统计的样本数。</summary>
        public int Count { get; set; }

        /// <summary>平均耗时，单位毫秒。</summary>
        public double AverageMs { get; set; }

        /// <summary>第50百分位耗时。</summary>
        public double P50Ms { get; set; }

        /// <summary>第95百分位耗时。</summary>
        public double P95Ms { get; set; }

        /// <summary>第99百分位耗时。</summary>
        public double P99Ms { get; set; }

        /// <summary>第99.9百分位耗时。</summary>
        public double P999Ms { get; set; }

        /// <summary>最大耗时。</summary>
        public double MaximumMs { get; set; }

        /// <summary>超过20毫秒的样本数量。</summary>
        public int Over20Ms { get; set; }

        /// <summary>超过30毫秒的样本数量。</summary>
        public int Over30Ms { get; set; }

        /// <summary>超过50毫秒的样本数量。</summary>
        public int Over50Ms { get; set; }
    }

    /// <summary>
    /// UI显示阶段的轻量计时快照。
    /// </summary>
    internal struct UiTimingRecord
    {
        /// <summary>Mat转换为Bitmap的耗时刻度。</summary>
        public long ToBitmapTicks;

        /// <summary>更换PictureBox图像的耗时刻度。</summary>
        public long SetImageTicks;
    }
}
