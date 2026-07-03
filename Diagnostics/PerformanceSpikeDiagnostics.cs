using Logger;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Diagnostics
{
    /// <summary>
    /// 性能毛刺诊断工具，用于在节点偶发慢耗时时补充线程、GC、图像和结果对象现场信息。
    /// </summary>
    public static class PerformanceSpikeDiagnostics
    {
        /// <summary>链路诊断编译标记，用于从现场日志确认当前运行程序是否包含本轮诊断代码。</summary>
        public const string DiagnosticBuildMark = "2026-07-02-链路诊断V2";

        /// <summary>通用慢耗时阈值，超过该值时输出额外慢诊断日志。</summary>
        public const long CommonSlowMs = 20;

        /// <summary>UI排队慢耗时阈值，超过该值时输出窗口消息队列现场。</summary>
        public const long UiQueueSlowMs = 20;

        /// <summary>私有内存水位日志步长，超过该增长量时记录当前节点。</summary>
        private const double MemoryGrowthLogStepMb = 256D;

        /// <summary>GDI对象水位日志步长，Bitmap或Graphics泄漏时通常会持续上涨。</summary>
        private const int GdiObjectGrowthLogStep = 500;

        /// <summary>句柄水位日志步长，用于观察线程、文件、GDI等系统句柄增长。</summary>
        private const int HandleGrowthLogStep = 1000;

        /// <summary>私有内存高水位阈值，超过后会按时间间隔持续输出内存现场。</summary>
        private const double PrivateMemoryHighWatermarkMb = 1024D;

        /// <summary>高水位持续输出的最小间隔，避免每个节点都刷内存日志。</summary>
        private static readonly TimeSpan HighWatermarkLogInterval = TimeSpan.FromMinutes(1);

        /// <summary>节点级内存水位采样的节点间隔，避免诊断本身影响每个节点耗时。</summary>
        private const long NodeMemorySampleNodeInterval = 100;

        /// <summary>节点级内存水位采样的最长时间间隔。</summary>
        private static readonly TimeSpan NodeMemorySampleTimeInterval = TimeSpan.FromSeconds(1);

        /// <summary>内存水位日志的同步锁。</summary>
        private static readonly object MemoryLogLock = new object();

        /// <summary>进程内存诊断的基线快照。</summary>
        private static MemorySnapshot _memoryBaseline;

        /// <summary>上一次输出内存水位日志时的快照。</summary>
        private static MemorySnapshot _lastMemoryLogSnapshot;

        /// <summary>上一次输出内存水位日志的时间。</summary>
        private static DateTime _lastMemoryLogTime = DateTime.MinValue;

        /// <summary>是否已经初始化内存诊断基线。</summary>
        private static bool _hasMemoryBaseline;

        /// <summary>节点内存采样计数器。</summary>
        private static long _nodeMemorySampleCounter;

        /// <summary>上次执行节点内存采样的高精度计时器刻度。</summary>
        private static long _lastNodeMemorySampleTicks;

        /// <summary>
        /// 判断一组耗时中是否存在需要额外记录的毛刺。
        /// </summary>
        /// <param name="thresholdMs">慢耗时阈值，单位毫秒。</param>
        /// <param name="elapsedItems">需要检查的耗时集合。</param>
        /// <returns>任意耗时大于等于阈值时返回 true。</returns>
        public static bool ShouldLog(long thresholdMs, params long[] elapsedItems)
        {
            if (elapsedItems == null)
                return false;

            foreach (long elapsed in elapsedItems)
            {
                if (elapsed >= thresholdMs)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断当前诊断日志等级是否允许记录，供高频入口在拼接重诊断文本前快速退出。
        /// </summary>
        /// <param name="level">需要记录的日志等级。</param>
        /// <returns>允许记录返回 true，否则返回 false。</returns>
        public static bool IsDiagnosticLogEnabled(MsgLevel level)
        {
            return LogHelper.CanRecord(level);
        }

        /// <summary>
        /// 仅在日志等级开启后才生成并记录诊断文本，避免关闭 Debug 时仍采集运行时现场。
        /// </summary>
        /// <param name="level">需要记录的日志等级。</param>
        /// <param name="messageFactory">诊断文本生成器，只有需要记录时才会执行。</param>
        /// <param name="isDisplay">是否刷新日志界面。</param>
        public static void LogIfEnabled(MsgLevel level, Func<string> messageFactory, bool isDisplay = false)
        {
            if (!IsDiagnosticLogEnabled(level) || messageFactory == null)
                return;

            LogHelper.AddLog(level, messageFactory(), isDisplay);
        }

        /// <summary>
        /// 仅在日志等级开启且存在慢耗时后才生成并记录诊断文本，用于现场毛刺定位。
        /// </summary>
        /// <param name="level">需要记录的日志等级。</param>
        /// <param name="thresholdMs">慢耗时阈值，单位毫秒。</param>
        /// <param name="messageFactory">诊断文本生成器，只有需要记录时才会执行。</param>
        /// <param name="isDisplay">是否刷新日志界面。</param>
        /// <param name="elapsedItems">需要参与慢耗时判定的耗时集合。</param>
        public static void LogSlowIfEnabled(MsgLevel level, long thresholdMs, Func<string> messageFactory, bool isDisplay = false, params long[] elapsedItems)
        {
            if (!IsDiagnosticLogEnabled(level) || !ShouldLog(thresholdMs, elapsedItems) || messageFactory == null)
                return;

            LogHelper.AddLog(level, messageFactory(), isDisplay);
        }

        /// <summary>
        /// 生成当前线程、线程池、进程内存和GC状态文本。
        /// </summary>
        /// <returns>运行时现场诊断文本。</returns>
        public static string GetRuntimeText()
        {
            int availableWorkerThreads;
            int availableCompletionPortThreads;
            int maxWorkerThreads;
            int maxCompletionPortThreads;
            ThreadPool.GetAvailableThreads(out availableWorkerThreads, out availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionPortThreads);

            Thread currentThread = Thread.CurrentThread;
            return $"线程ID={currentThread.ManagedThreadId}；线程池线程={currentThread.IsThreadPoolThread}；线程池可用={availableWorkerThreads}/{maxWorkerThreads},IO={availableCompletionPortThreads}/{maxCompletionPortThreads}；{GetMemoryText()}";
        }

        /// <summary>
        /// 生成当前进程内存、句柄和GC状态文本。
        /// </summary>
        /// <returns>内存诊断文本。</returns>
        public static string GetMemoryText()
        {
            return CaptureMemorySnapshot().ToLogText();
        }

        /// <summary>
        /// 获取当前进程主程序路径，用于确认现场运行的到底是哪一个编译产物。
        /// </summary>
        /// <returns>当前进程主模块文件路径。</returns>
        public static string GetCurrentProcessPathText()
        {
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return process.MainModule?.FileName ?? "未知";
                }
            }
            catch (Exception ex)
            {
                return $"获取失败:{ex.GetType().Name}";
            }
        }

        /// <summary>
        /// 生成当前程序身份文本，定位现场运行路径、当前目录和诊断编译标记。
        /// </summary>
        /// <returns>当前程序身份诊断文本。</returns>
        public static string GetExecutableIdentityText()
        {
            return $"诊断版本={DiagnosticBuildMark}；主程序={GetCurrentProcessPathText()}；入口程序集={GetEntryAssemblyPathText()}；当前目录={Environment.CurrentDirectory}";
        }

        /// <summary>
        /// 获取入口程序集路径，用于和主程序路径互相校验。
        /// </summary>
        /// <returns>入口程序集路径。</returns>
        private static string GetEntryAssemblyPathText()
        {
            try
            {
                return Assembly.GetEntryAssembly()?.Location ?? "未知";
            }
            catch (Exception ex)
            {
                return $"获取失败:{ex.GetType().Name}";
            }
        }

        /// <summary>
        /// 获取对象类型和程序集路径，用于确认运行时加载的节点实现版本。
        /// </summary>
        /// <param name="instance">需要诊断的对象实例。</param>
        /// <returns>类型全名和程序集路径。</returns>
        public static string GetAssemblyText(object instance)
        {
            if (instance == null)
            {
                return "空";
            }

            try
            {
                Type type = instance.GetType();
                string assemblyLocation = string.IsNullOrWhiteSpace(type.Assembly.Location) ? "未知" : type.Assembly.Location;
                return $"{type.FullName}；程序集={assemblyLocation}";
            }
            catch (Exception ex)
            {
                return $"获取失败:{ex.GetType().Name}";
            }
        }

        /// <summary>
        /// 捕获当前进程内存和资源对象快照。
        /// </summary>
        /// <returns>内存诊断快照。</returns>
        public static MemorySnapshot CaptureMemorySnapshot()
        {
            MemorySnapshot snapshot = new MemorySnapshot
            {
                Time = DateTime.Now,
                ManagedMemoryMb = GC.GetTotalMemory(false) / 1024D / 1024D,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };

            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    snapshot.WorkingSetMb = process.WorkingSet64 / 1024D / 1024D;
                    snapshot.PrivateMemoryMb = process.PrivateMemorySize64 / 1024D / 1024D;
                    snapshot.ThreadCount = process.Threads.Count;
                    snapshot.HandleCount = process.HandleCount;
                    snapshot.GdiObjectCount = GetGuiResources(process.Handle, 0);
                    snapshot.UserObjectCount = GetGuiResources(process.Handle, 1);
                }
            }
            catch (Exception ex)
            {
                snapshot.Error = ex.Message;
            }

            return snapshot;
        }

        /// <summary>
        /// 在节点完成后按内存增长步长输出低频水位日志，用于定位泄漏增长发生在哪个节点之后。
        /// </summary>
        /// <param name="processName">流程名称。</param>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="statusText">节点状态。</param>
        /// <param name="elapsedMilliseconds">节点耗时。</param>
        public static void LogNodeMemoryIfNeeded(string processName, int nodeId, string nodeName, string statusText, int elapsedMilliseconds)
        {
            // Debug 关闭时不做进程资源查询，避免内存诊断本身影响节点耗时。
            if (!IsDiagnosticLogEnabled(MsgLevel.Debug))
                return;

            if (!ShouldSampleNodeMemory())
                return;

            MemorySnapshot snapshot = CaptureMemorySnapshot();
            MemorySnapshot baseline;
            MemorySnapshot previousLogSnapshot;
            string reason = string.Empty;
            bool shouldLog = false;

            lock (MemoryLogLock)
            {
                if (!_hasMemoryBaseline)
                {
                    _memoryBaseline = snapshot;
                    _lastMemoryLogSnapshot = snapshot;
                    _lastMemoryLogTime = snapshot.Time;
                    _lastNodeMemorySampleTicks = Stopwatch.GetTimestamp();
                    _hasMemoryBaseline = true;
                    shouldLog = true;
                    reason = "建立基线";
                    previousLogSnapshot = snapshot;
                }
                else
                {
                    previousLogSnapshot = _lastMemoryLogSnapshot;
                    double privateGrowthFromLast = snapshot.PrivateMemoryMb - _lastMemoryLogSnapshot.PrivateMemoryMb;
                    int gdiGrowthFromLast = snapshot.GdiObjectCount - _lastMemoryLogSnapshot.GdiObjectCount;
                    int handleGrowthFromLast = snapshot.HandleCount - _lastMemoryLogSnapshot.HandleCount;
                    bool highWatermarkDue = snapshot.PrivateMemoryMb >= PrivateMemoryHighWatermarkMb
                        && snapshot.Time - _lastMemoryLogTime >= HighWatermarkLogInterval;

                    if (privateGrowthFromLast >= MemoryGrowthLogStepMb)
                    {
                        shouldLog = true;
                        reason = $"私有内存较上次日志增长{privateGrowthFromLast:F1}MB";
                    }
                    else if (gdiGrowthFromLast >= GdiObjectGrowthLogStep)
                    {
                        shouldLog = true;
                        reason = $"GDI对象较上次日志增长{gdiGrowthFromLast}";
                    }
                    else if (handleGrowthFromLast >= HandleGrowthLogStep)
                    {
                        shouldLog = true;
                        reason = $"句柄数较上次日志增长{handleGrowthFromLast}";
                    }
                    else if (highWatermarkDue)
                    {
                        shouldLog = true;
                        reason = $"私有内存超过{PrivateMemoryHighWatermarkMb:F0}MB后的定期水位";
                    }

                    if (shouldLog)
                    {
                        _lastMemoryLogSnapshot = snapshot;
                        _lastMemoryLogTime = snapshot.Time;
                    }
                }

                baseline = _memoryBaseline;
            }

            if (!shouldLog)
                return;

            LogIfEnabled(
                MsgLevel.Debug,
                () => $"【内存诊断-节点水位】原因={reason}；流程={processName ?? "未知"}；节点=({nodeId}.{nodeName})；状态={statusText}；节点耗时={elapsedMilliseconds}ms；较基线私有内存={snapshot.PrivateMemoryMb - baseline.PrivateMemoryMb:F1}MB；较上次日志私有内存={snapshot.PrivateMemoryMb - previousLogSnapshot.PrivateMemoryMb:F1}MB；当前={snapshot.ToLogText()}；基线={baseline.ToLogText()}",
                true);
        }

        /// <summary>
        /// 判断当前节点是否需要执行内存采样，避免每个节点都查询进程资源对象。
        /// </summary>
        /// <returns>需要采样时返回 true。</returns>
        private static bool ShouldSampleNodeMemory()
        {
            if (!_hasMemoryBaseline)
                return true;

            long sampleCount = Interlocked.Increment(ref _nodeMemorySampleCounter);
            if (sampleCount % NodeMemorySampleNodeInterval == 0)
            {
                Interlocked.Exchange(ref _lastNodeMemorySampleTicks, Stopwatch.GetTimestamp());
                return true;
            }

            long nowTicks = Stopwatch.GetTimestamp();
            long lastTicks = Interlocked.Read(ref _lastNodeMemorySampleTicks);
            double elapsedSeconds = (nowTicks - lastTicks) / (double)Stopwatch.Frequency;
            if (elapsedSeconds < NodeMemorySampleTimeInterval.TotalSeconds)
                return false;

            return Interlocked.CompareExchange(ref _lastNodeMemorySampleTicks, nowTicks, lastTicks) == lastTicks;
        }

        /// <summary>
        /// 生成Mat图像尺寸和估算内存文本。
        /// </summary>
        /// <param name="mat">待描述的Mat图像。</param>
        /// <returns>Mat诊断文本。</returns>
        public static string GetMatText(Mat mat)
        {
            if (mat == null)
                return "空";

            try
            {
                if (mat.Empty())
                    return "空Mat";

                int channels = Math.Max(1, mat.Channels());
                double megaBytes = mat.Width * mat.Height * channels / 1024D / 1024D;
                return $"{mat.Width}x{mat.Height}x{channels}，约{megaBytes:F2}MB";
            }
            catch (Exception ex)
            {
                return "读取失败：" + ex.Message;
            }
        }

        /// <summary>
        /// 生成Bitmap图像尺寸、像素格式和估算内存文本。
        /// </summary>
        /// <param name="bitmap">待描述的Bitmap图像。</param>
        /// <returns>Bitmap诊断文本。</returns>
        public static string GetBitmapText(Bitmap bitmap)
        {
            if (bitmap == null)
                return "空";

            try
            {
                double megaBytes = bitmap.Width * bitmap.Height * Math.Max(1, Image.GetPixelFormatSize(bitmap.PixelFormat) / 8D) / 1024D / 1024D;
                return $"{bitmap.Width}x{bitmap.Height}，{bitmap.PixelFormat}，约{megaBytes:F2}MB";
            }
            catch (Exception ex)
            {
                return "读取失败：" + ex.Message;
            }
        }

        /// <summary>
        /// 生成节点图像输出对象摘要，便于确认是否存在原图、显示图和灰度图重复持有。
        /// </summary>
        /// <param name="outputImage">待描述的输出图像对象。</param>
        /// <returns>输出图像摘要文本。</returns>
        public static string GetOutputImageText(OutputImage outputImage)
        {
            if (outputImage == null)
                return "空";

            int bitmapCount = outputImage.Bitmaps == null ? 0 : outputImage.Bitmaps.Count;
            Mat firstBitmap = bitmapCount > 0 ? outputImage.Bitmaps[0] : null;
            int rectangleCount = outputImage.Rectangles == null ? 0 : outputImage.Rectangles.Count;
            return $"SrcImg={GetMatText(outputImage.SrcImg)}；Bitmaps={bitmapCount}；Bitmaps[0]={GetMatText(firstBitmap)}；GrayImg={GetMatText(outputImage.GrayImg)}；Rectangles={rectangleCount}；DisplayResult={GetAlgorithmResultText(outputImage.DisplayResult)}";
        }

        /// <summary>
        /// 生成算法显示结果的元素数量摘要。
        /// </summary>
        /// <param name="result">待描述的算法显示结果。</param>
        /// <returns>算法结果摘要文本。</returns>
        public static string GetAlgorithmResultText(AlgorithmResult result)
        {
            if (result == null)
                return "空";

            int ngRectCount = result.RectsNgMap == null ? 0 : result.RectsNgMap.Values.Where(items => items != null).Sum(items => items.Count);
            return $"矩形={GetCount(result.Rects)}，NG矩形={ngRectCount}，线={GetCount(result.Lines)}，圆={GetCount(result.Circles)}，圆弧={GetCount(result.Arcs)}，椭圆={GetCount(result.Ellipses)}，轮廓={GetCount(result.Contours)}，文本={GetCount(result.Texts)}";
        }

        /// <summary>
        /// 安全获取集合数量。
        /// </summary>
        /// <typeparam name="T">集合元素类型。</typeparam>
        /// <param name="items">集合对象。</param>
        /// <returns>集合数量。</returns>
        private static int GetCount<T>(System.Collections.Generic.ICollection<T> items)
        {
            return items == null ? 0 : items.Count;
        }

        /// <summary>
        /// 获取当前进程的GDI或USER对象数量。
        /// </summary>
        /// <param name="hProcess">进程句柄。</param>
        /// <param name="uiFlags">0表示GDI对象，1表示USER对象。</param>
        /// <returns>对象数量，失败时通常为0。</returns>
        [DllImport("user32.dll")]
        private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);
    }

    /// <summary>
    /// 进程内存、句柄、GDI对象和GC状态快照。
    /// </summary>
    public sealed class MemorySnapshot
    {
        /// <summary>快照时间。</summary>
        public DateTime Time { get; set; }
        /// <summary>进程工作集，单位MB。</summary>
        public double WorkingSetMb { get; set; }
        /// <summary>进程私有内存，单位MB。</summary>
        public double PrivateMemoryMb { get; set; }
        /// <summary>托管堆内存，单位MB。</summary>
        public double ManagedMemoryMb { get; set; }
        /// <summary>进程线程数量。</summary>
        public int ThreadCount { get; set; }
        /// <summary>进程句柄数量。</summary>
        public int HandleCount { get; set; }
        /// <summary>GDI对象数量，Bitmap或Graphics泄漏时通常会升高。</summary>
        public int GdiObjectCount { get; set; }
        /// <summary>USER对象数量，窗口控件泄漏时通常会升高。</summary>
        public int UserObjectCount { get; set; }
        /// <summary>第0代GC次数。</summary>
        public int Gen0Collections { get; set; }
        /// <summary>第1代GC次数。</summary>
        public int Gen1Collections { get; set; }
        /// <summary>第2代GC次数。</summary>
        public int Gen2Collections { get; set; }
        /// <summary>采集快照时的异常信息。</summary>
        public string Error { get; set; }

        /// <summary>
        /// 生成日志友好的内存快照文本。
        /// </summary>
        /// <returns>内存快照文本。</returns>
        public string ToLogText()
        {
            string errorText = string.IsNullOrEmpty(Error) ? string.Empty : $"；错误={Error}";
            return $"工作集={WorkingSetMb:F1}MB；私有内存={PrivateMemoryMb:F1}MB；托管堆={ManagedMemoryMb:F1}MB；线程={ThreadCount}；句柄={HandleCount}；GDI对象={GdiObjectCount}；USER对象={UserObjectCount}；GC={Gen0Collections}/{Gen1Collections}/{Gen2Collections}{errorText}";
        }
    }
}
