using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TDJS_Vision.Diagnostics
{
    /// <summary>
    /// 相机帧的轻量诊断元数据，用于关联SDK回调、图像转换和流程等待耗时。
    /// </summary>
    public sealed class CameraFrameTraceInfo
    {
        /// <summary>
        /// 创建一帧相机图像的诊断元数据。
        /// </summary>
        /// <param name="cameraName">相机名称。</param>
        /// <param name="frameId">相机实例内单调递增的帧编号。</param>
        /// <param name="callbackStartedTimestamp">SDK回调开始时的高精度计时器刻度。</param>
        /// <param name="conversionCompletedTimestamp">Mat转换完成时的高精度计时器刻度。</param>
        /// <param name="width">原始图像宽度。</param>
        /// <param name="height">原始图像高度。</param>
        /// <param name="sourcePixelType">相机原始像素格式。</param>
        public CameraFrameTraceInfo(
            string cameraName,
            long frameId,
            long callbackStartedTimestamp,
            long conversionCompletedTimestamp,
            int width,
            int height,
            string sourcePixelType)
        {
            CameraName = cameraName ?? string.Empty;
            FrameId = frameId;
            CallbackStartedTimestamp = callbackStartedTimestamp;
            ConversionCompletedTimestamp = conversionCompletedTimestamp;
            Width = width;
            Height = height;
            SourcePixelType = sourcePixelType ?? string.Empty;
        }

        /// <summary>获取产生当前帧的相机名称。</summary>
        public string CameraName { get; }

        /// <summary>获取相机实例内单调递增的帧编号。</summary>
        public long FrameId { get; }

        /// <summary>获取SDK回调开始时的高精度计时器刻度。</summary>
        public long CallbackStartedTimestamp { get; }

        /// <summary>获取Mat转换完成时的高精度计时器刻度。</summary>
        public long ConversionCompletedTimestamp { get; }

        /// <summary>获取原始图像宽度。</summary>
        public int Width { get; }

        /// <summary>获取原始图像高度。</summary>
        public int Height { get; }

        /// <summary>获取相机原始像素格式。</summary>
        public string SourcePixelType { get; }

        /// <summary>获取SDK回调开始到Mat转换完成的耗时，单位毫秒。</summary>
        public long CallbackToConversionMilliseconds =>
            GetElapsedMilliseconds(CallbackStartedTimestamp, ConversionCompletedTimestamp);

        /// <summary>
        /// 获取SDK回调开始到指定计时器刻度的耗时。
        /// </summary>
        /// <param name="completedTimestamp">完成阶段的高精度计时器刻度。</param>
        /// <returns>非负耗时，单位毫秒。</returns>
        public long GetElapsedFromCallbackStartMilliseconds(long completedTimestamp)
        {
            return GetElapsedMilliseconds(CallbackStartedTimestamp, completedTimestamp);
        }

        /// <summary>
        /// 获取Mat转换完成到指定计时器刻度的耗时。
        /// </summary>
        /// <param name="completedTimestamp">完成阶段的高精度计时器刻度。</param>
        /// <returns>非负耗时，单位毫秒。</returns>
        public long GetElapsedFromConversionMilliseconds(long completedTimestamp)
        {
            return GetElapsedMilliseconds(ConversionCompletedTimestamp, completedTimestamp);
        }

        /// <summary>
        /// 将两个高精度计时器刻度转换为毫秒耗时。
        /// </summary>
        /// <param name="startedTimestamp">开始阶段的高精度计时器刻度。</param>
        /// <param name="completedTimestamp">完成阶段的高精度计时器刻度。</param>
        /// <returns>非负耗时，单位毫秒。</returns>
        public static long GetElapsedMilliseconds(long startedTimestamp, long completedTimestamp)
        {
            if (startedTimestamp <= 0 || completedTimestamp <= startedTimestamp)
                return 0;

            double elapsedMilliseconds =
                (completedTimestamp - startedTimestamp) * 1000D / Stopwatch.Frequency;
            return elapsedMilliseconds >= long.MaxValue ? long.MaxValue : (long)elapsedMilliseconds;
        }
    }

    /// <summary>
    /// Mat与相机帧诊断元数据的弱引用关联表，不拥有也不释放任何图像资源。
    /// </summary>
    public static class CameraFrameTraceRegistry
    {
        /// <summary>按Mat包装对象保存帧元数据，Mat不可达后关联项会自动回收。</summary>
        private static readonly ConditionalWeakTable<Mat, CameraFrameTraceInfo> TraceTable =
            new ConditionalWeakTable<Mat, CameraFrameTraceInfo>();

        /// <summary>
        /// 将相机帧元数据关联到指定Mat包装对象。
        /// </summary>
        /// <param name="image">需要关联的Mat包装对象。</param>
        /// <param name="traceInfo">相机帧诊断元数据。</param>
        public static void Attach(Mat image, CameraFrameTraceInfo traceInfo)
        {
            if (image == null || traceInfo == null)
                return;

            TraceTable.Remove(image);
            TraceTable.Add(image, traceInfo);
        }

        /// <summary>
        /// 尝试读取指定Mat包装对象关联的相机帧元数据。
        /// </summary>
        /// <param name="image">需要查询的Mat包装对象。</param>
        /// <param name="traceInfo">成功时返回相机帧诊断元数据。</param>
        /// <returns>存在关联元数据时返回true。</returns>
        public static bool TryGet(Mat image, out CameraFrameTraceInfo traceInfo)
        {
            if (image == null)
            {
                traceInfo = null;
                return false;
            }

            return TraceTable.TryGetValue(image, out traceInfo);
        }
    }
}
