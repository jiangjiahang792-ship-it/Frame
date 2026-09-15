#requires -Version 7.0
$ErrorActionPreference = 'Stop'

# 使用生产方法原文和内存中的SDK替身验证并发与所有权，不打开设备或启动界面。
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $root 'Device\Camera\CameraHik.cs') -Raw -Encoding UTF8

# 读取待测试方法原文，避免替身另行实现生产控制流程。
function Get-MethodText([string]$Start, [string]$End) {
    $startIndex = $source.IndexOf($Start, [StringComparison]::Ordinal)
    if ($startIndex -lt 0) { throw "Missing method: $Start" }
    $endIndex = $source.IndexOf($End, $startIndex, [StringComparison]::Ordinal)
    if ($endIndex -le $startIndex) { throw "Missing method end: $End" }
    return $source.Substring($startIndex, $endIndex - $startIndex)
}

if ($source.Contains('AcquireCpuWorkAsync(')) { throw 'Camera conversion must not acquire shared CPU permits.' }
if (-not $source.Contains('private readonly object _callbackSyncRoot = new object();')) { throw 'Conversion locking must remain per-camera.' }
if (-not $source.Contains('_pendingRawFrames.TryDequeue(out packet)') -or
    -not $source.Contains('packet.WaitUntilCallbackCompleted();') -or
    -not $source.Contains('_availableRawFrames.Enqueue(packet);')) { throw 'The ordered raw-frame worker must retain its handoff and return boundaries.' }

$workerMethod = Get-MethodText '        private void ProcessRawFrame(' '        /// <summary>在相机转换线程中失败'
$convertMethod = Get-MethodText '        private Mat ConvertFrameToIndependentMat(' '        /// <summary>将独立Mat分发'
$harness = @'
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace IndependentCameraProbe
{
    // 可计数的Mat替身：验证所有权而非真实像素转换性能。
    public sealed class Mat : IDisposable
    {
        // 跨相机的替身Mat存活计数。
        public static int Live;
        // 当前替身的释放状态。
        private int disposed;
        // 登记新创建的替身对象。
        public Mat() { Interlocked.Increment(ref Live); }
        // 模拟原始缓冲视图，不执行真实像素操作。
        public static Mat FromPixelData(int h, int w, MatType type, IntPtr p) { return new Mat(); }
        // 为生产克隆路径提供独立所有权对象。
        public Mat Clone() { return new Mat(); }
        // 按Mat接口语义幂等归还计数。
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Decrement(ref Live); }
    }
    // 生产方法使用的像素通道类型替身。
    public enum MatType { CV_8UC1, CV_8UC3 }
    // 不写实际日志的等级替身。
    public enum MsgLevel { Debug, Exception }
    // 仅提供转换入口和同步屏障的SDK替身。
    public sealed class MyCamera
    {
        // 模拟Bayer输入与BGR输出格式。
        public enum MvGvspPixelType { Bayer, Bgr }
        // 保留生产代码需要的帧尺寸、字节数、序号及格式字段。
        public struct MV_FRAME_OUT_INFO_EX { public int nWidth, nHeight; public uint nFrameLen, nFrameNum; public MvGvspPixelType enPixelType; }
        // 保留SDK转换参数布局中的被访问字段。
        public struct MV_PIXEL_CONVERT_PARAM { public int nWidth, nHeight; public IntPtr pSrcData, pDstBuffer; public uint nSrcDataLen, nDstBufferSize; public MvGvspPixelType enSrcPixelType, enDstPixelType; }
        // 由用例注入转换屏障或取消动作。
        public Action DuringConversion;
        // 记录转换调用数、当前并发及峰值。
        public int Calls, Active, Peak;
        // 执行注入动作以验证生产代码的互斥和取消边界。
        public int MV_CC_ConvertPixelType_NET(ref MV_PIXEL_CONVERT_PARAM p)
        {
            Interlocked.Increment(ref Calls);
            int active = Interlocked.Increment(ref Active);
            int previous;
            do { previous = Peak; if (previous >= active) break; }
            while (Interlocked.CompareExchange(ref Peak, active, previous) != previous);
            try { DuringConversion?.Invoke(); return 0; }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    // 诊断关闭场景的元数据替身，不将模拟耗时作为真实性能。
    public sealed class CameraFrameTraceInfo
    {
        // 接受生产路径创建诊断元数据时的参数。
        public CameraFrameTraceInfo(string name, long id, long start, long end, int w, int h, string type) { }
        // 当前测试不统计回调耗时。
        public long CallbackToConversionMilliseconds { get { return 0; } }
        // 当前测试不执行生产计时器换算。
        public static long GetElapsedMilliseconds(long start, long end) { return 0; }
    }
    // 默认不接管图像的路由替身，用于验证发布和取消的所有权边界。
    public sealed class CameraTriggerTicketRegistry
    {
        // 本专项让图像走普通订阅者发布分支。
        public bool TryRouteFrame(string key, long id, uint num, long ts, Mat image, CameraFrameTraceInfo trace) { return false; }
        // 本专项不吞掉模拟转换错误。
        public bool TryAcknowledgePreviewFrameFailure(string key) { return false; }
    }
    // 不分配原始像素缓冲的帧包替身。
    public sealed class HikRawFramePacket
    {
        // SDK帧信息。
        public MyCamera.MV_FRAME_OUT_INFO_EX FrameInfo;
        // 本专项关闭详细诊断。
        public bool DiagnosticEnabled;
        // 帧身份和阶段时间戳。
        public long FrameId, CallbackStartedTimestamp, CallbackCompletedTimestamp, CopyStartedTimestamp, CopyCompletedTimestamp;
        // SDK回调线程身份。
        public int CallbackThreadId;
        // 不访问真实内存的模拟指针。
        public IntPtr Buffer;
        // 图像路由和故障归属。
        public CameraTriggerTicketRegistry FrameRoutingRegistry, ProductionRegistry;
    }
    // 返回测试身份，不访问方案或设备配置。
    public static class CameraProductionIdentity { public static string GetStableKey(object camera) { return "fake-camera"; } }
    // 诊断替身不执行日志闭包，保证测试只覆盖转换控制逻辑。
    public static class PerformanceSpikeDiagnostics
    {
        // 供生产代码编译使用的慢耗时阈值。
        public const int FrameConvertSlowMs = 100;
        // 提供诊断字符串替身。
        public static string GetRuntimeText() { return "fake-sdk"; }
        // 禁用常规诊断输出。
        public static void LogIfEnabled(MsgLevel level, Func<string> message, bool force) { }
        // 禁用慢诊断输出。
        public static void LogSlowIfEnabled(MsgLevel level, int threshold, Func<string> message, bool force, params long[] elapsed) { }
    }
    // 测试不向应用日志目录写入内容。
    public static class LogHelper { public static void AddLog(MsgLevel level, string message, bool force) { } }

    // 两个实际生产方法直接插入本宿主，其余依赖均为不连接设备的替身。
    public sealed class CameraProbe
    {
        // 与生产类一致的实例级转换锁。
        private readonly object _callbackSyncRoot = new object();
        // 当前相机独有的SDK替身。
        private readonly MyCamera _device = new MyCamera();
        // 模拟转换缓冲指针及容量。
        private IntPtr _conversionBuffer = IntPtr.Zero;
        private uint _conversionBufferSize;
        // 日志使用的相机名称。
        private string DevName = "test-camera";
        // 供用例制造同相机锁竞争。
        public object Gate { get { return _callbackSyncRoot; } }
        // 供用例注入SDK屏障。
        public MyCamera Device { get { return _device; } }
        // 成功发布和生产故障计数。
        public int Published, Faults;
        // 不分配内存，仅模拟缓冲容量。
        private void EnsureConversionBuffer(uint size) { _conversionBufferSize = size; }
        // 固定返回三通道，避免依赖真实SDK枚举。
        private static MyCamera.MvGvspPixelType ResolveDestinationPixelType(MyCamera.MvGvspPixelType source, out int channels) { channels = 3; return MyCamera.MvGvspPixelType.Bgr; }
        // 保留生产异常传播约定。
        private static void EnsureSuccess(int result, string operation) { if (result != 0) throw new InvalidOperationException(operation); }
        // 接管并立即释放模拟订阅者收到的Mat。
        private void RaiseMatImageReceived(Mat image, CameraFrameTraceInfo trace) { Interlocked.Increment(ref Published); image.Dispose(); }
        // 记录误进入的生产故障路径。
        private void TryFailProductionFrame(long id, long ts, Exception error, CameraTriggerTicketRegistry registry) { Interlocked.Increment(ref Faults); }
        // 使用最小帧信息执行原始生产方法。
        public void Run(CancellationToken token)
        {
            ProcessRawFrame(new HikRawFramePacket { FrameId = 1, FrameInfo = new MyCamera.MV_FRAME_OUT_INFO_EX { nWidth = 16, nHeight = 16, nFrameLen = 256 } }, token);
        }
__WORKER_METHOD__
__CONVERT_METHOD__
    }

    // 多相机并发与取消专项用例入口。
    public static class Harness
    {
        // 断言失败立即终止专项测试。
        private static void Check(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }

        // 屏障要求所有相机同时进入转换，单个共享转换名额无法通过此测试。
        public static void MultiCamera(int count, int frames)
        {
            using (var entered = new CountdownEvent(count))
            using (var release = new ManualResetEventSlim(false))
            {
                var cameras = new CameraProbe[count];
                var threads = new Thread[count];
                var failures = new ConcurrentQueue<Exception>();
                for (int i = 0; i < count; i++)
                {
                    var camera = cameras[i] = new CameraProbe();
                    camera.Device.DuringConversion = () => { if (camera.Device.Calls == 1) { entered.Signal(); if (!release.Wait(5000)) throw new TimeoutException(); } };
                    threads[i] = new Thread(() => { try { for (int f = 0; f < frames; f++) camera.Run(CancellationToken.None); } catch (Exception e) { failures.Enqueue(e); } });
                    threads[i].IsBackground = true;
                    threads[i].Start();
                }
                bool parallel = entered.Wait(4000);
                release.Set();
                foreach (var thread in threads) Check(thread.Join(5000), "Camera worker did not complete.");
                Check(parallel, "Cameras could not enter conversion concurrently.");
                Check(failures.IsEmpty, "Concurrent camera operation threw.");
                foreach (var camera in cameras) Check(camera.Published == frames && camera.Faults == 0 && camera.Device.Peak == 1, "Camera frames or ordering boundary were lost.");
                Check(Mat.Live == 0, "Concurrent Mat leak.");
            }
        }

        // 同一实例的主动/后台转换仍共享实例锁，不能覆盖彼此的缓冲。
        public static void SameCamera()
        {
            var camera = new CameraProbe();
            camera.Device.DuringConversion = () => Thread.Sleep(1);
            var threads = new Thread[16];
            for (int i = 0; i < threads.Length; i++) { threads[i] = new Thread(() => camera.Run(CancellationToken.None)); threads[i].IsBackground = true; threads[i].Start(); }
            foreach (var thread in threads) Check(thread.Join(5000), "Same-camera worker did not complete.");
            Check(camera.Device.Peak == 1 && camera.Published == 16 && camera.Faults == 0 && Mat.Live == 0, "Same-camera buffer protection failed.");
        }

        // 停止前、原生转换中及等待本相机锁时取消，都不得发布或误报生产故障。
        public static void Cancellation()
        {
            using (var cts = new CancellationTokenSource())
            {
                var camera = new CameraProbe();
                cts.Cancel();
                bool canceled = false;
                try { camera.Run(cts.Token); } catch (OperationCanceledException) { canceled = true; }
                Check(canceled && camera.Device.Calls == 0 && camera.Published == 0 && camera.Faults == 0, "Pre-canceled frame entered the SDK.");
            }
            using (var cts = new CancellationTokenSource())
            {
                var camera = new CameraProbe();
                camera.Device.DuringConversion = cts.Cancel;
                bool canceled = false;
                try { camera.Run(cts.Token); } catch (OperationCanceledException) { canceled = true; }
                Check(canceled && camera.Device.Calls == 1 && camera.Published == 0 && camera.Faults == 0 && Mat.Live == 0, "Canceled converted Mat was published or leaked.");
            }
            using (var cts = new CancellationTokenSource())
            using (var started = new ManualResetEventSlim(false))
            {
                var blocked = new CameraProbe();
                bool canceled = false;
                Exception unexpected = null;
                var thread = new Thread(() => { started.Set(); try { blocked.Run(cts.Token); } catch (OperationCanceledException) { canceled = true; } catch (Exception e) { unexpected = e; } });
                thread.IsBackground = true;
                lock (blocked.Gate)
                {
                    thread.Start();
                    Check(started.Wait(4000), "Blocked camera thread did not start.");
                    var clock = Stopwatch.StartNew();
                    while ((thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == 0 && clock.ElapsedMilliseconds < 4000) Thread.Sleep(1);
                    Check((thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, "Camera did not wait for its instance lock.");
                    var other = new CameraProbe();
                    other.Run(CancellationToken.None);
                    Check(other.Published == 1, "One camera lock blocked another camera.");
                    cts.Cancel();
                }
                Check(thread.Join(5000), "Canceled lock waiter did not finish.");
                Check(canceled && unexpected == null && blocked.Device.Calls == 0 && blocked.Faults == 0 && Mat.Live == 0, "Lock-wait cancellation entered SDK or leaked.");
            }
        }
    }
}
'@
$harness = $harness.Replace('__WORKER_METHOD__', $workerMethod).Replace('__CONVERT_METHOD__', $convertMethod)
Add-Type -TypeDefinition $harness -Language CSharp
foreach ($count in @(2, 4, 8)) {
    [IndependentCameraProbe.Harness]::MultiCamera($count, 100)
    Write-Host "PASS: $count independent cameras x 100 simulated frames."
}
[IndependentCameraProbe.Harness]::SameCamera()
[IndependentCameraProbe.Harness]::Cancellation()
Write-Host 'PASS: per-camera mutual exclusion, cancellation and Mat disposal; no physical SDK or camera was used.'
