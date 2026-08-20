using Logger;
using Sunny.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.ResourceManagement;
using static TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageQueueProcessor;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageSave
{
    public class NodeImageSave : NodeBase
    {
        /// <summary>保护节点停止与任务投递之间的所有权边界。</summary>
        private readonly object _queueOwnershipSync = new object();

        /// <summary>共享保存队列中用于定向取消当前节点任务的唯一标识。</summary>
        private readonly string _queueOwnerKey;

        /// <summary>当前保存节点是否已经释放。</summary>
        private int _disposed;

        /// <summary>
        /// 初始化保存图像节点；后台工作池延迟到首次运行时按本机资源档案创建。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属可编辑流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeImageSave(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType) 
        {
            ParamForm = new ParamFormImageSave();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSave();;
            _queueOwnerKey = $"{process?.ID ?? 0}.{nodeId}.{nodeName}";
        }

        /// <summary>
        /// 释放保存图像节点时停止后台保存队列，避免反复加载方案后遗留工作线程。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                lock (_queueOwnershipSync)
                    Solution.Instance.CancelPendingImageSaveTasks(_queueOwnerKey);
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is ParamFormImageSave form)
            {
                if(form.Params is NodeParamSaveImage param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        await base.CheckTokenCancel(token);

                        Stopwatch enqueueStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                        string barcode = string.Empty;
                        ImageSaveJudgment imageSaveJudgment = null;
                        OutputImage outputImage;

                        // 参数获取订阅控件的值
                        if (param.IsBarCode)
                        {
                            barcode = form.GetBarCode();
                            if (barcode.IsNullOrEmpty())
                                throw new Exception($"获取条码结果失败！");
                        }
                        if (param.NeedOkNg)
                            imageSaveJudgment = form.GetImageSaveJudgment();
                        outputImage = form.GetOutputImageForSave();
                        PerformanceTraceContext traceContext = PerformanceTraceContext.CreateIfEnabled(Process, ID, NodeName);

                        // 参数合法性判断
                        if (param.SavePath.IsNullOrEmpty())
                            throw new Exception($"存图路径未设置！");

                        // 保存图像节点只做轻量任务投递，耗时的 ToBitmap、标注绘制、压缩和写盘都由后台队列处理。
                        SaveImageEnqueueResult enqueueResult = EnqueueSaveImage(param, outputImage, imageSaveJudgment, barcode, traceContext);
                        long enqueueElapsed = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(enqueueStopwatch);
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (traceContext != null)
                        {
                            PerformanceSpikeDiagnostics.LogIfEnabled(
                                MsgLevel.Debug,
                                () => $"【性能诊断-保存图像快速入队】【完整耗时】{traceContext.ToLogText()}；阶段=后台任务入队；节点计时={time}ms；快速入队={enqueueElapsed}ms；路径决策={enqueueResult.PathDecisionMs}ms；队列投递={enqueueResult.EnqueueMs}ms；目标路径={enqueueResult.TargetPathCount}；入队任务={enqueueResult.EnqueuedTaskCount}；已跳过={enqueueResult.Skipped}；队列状态={enqueueResult.AdmissionStatus}；淘汰普通任务={enqueueResult.EvictedNormalTaskCount}；当前队列={enqueueResult.QueuedCount}；排队字节={enqueueResult.QueuedBytes}；图像={PerformanceSpikeDiagnostics.GetOutputImageText(outputImage)}",
                                true);
                        }
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }

                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>
        /// 保存图像主流程入队结果，用于诊断主流程是否只做轻量投递。
        /// </summary>
        private struct SaveImageEnqueueResult
        {
            /// <summary>实际投递到后台队列的任务数量。</summary>
            public int EnqueuedTaskCount;
            /// <summary>当前保存规则命中的目标目录数量。</summary>
            public int TargetPathCount;
            /// <summary>保存路径规则判断耗时。</summary>
            public long PathDecisionMs;
            /// <summary>队列投递耗时。</summary>
            public long EnqueueMs;
            /// <summary>当前图片是否因保存规则未命中而跳过。</summary>
            public bool Skipped;
            /// <summary>有界队列的入队状态。</summary>
            public BoundedQueueAdmissionStatus AdmissionStatus;
            /// <summary>为保留当前NG图而淘汰的普通任务数量。</summary>
            public int EvictedNormalTaskCount;
            /// <summary>本次投递结束后的共享等待任务数量。</summary>
            public int QueuedCount;
            /// <summary>本次投递结束后的共享等待任务估算字节数。</summary>
            public long QueuedBytes;
        }

        /// <summary>
        /// 将保存图像任务快速投递到后台队列；当前方法不做 ToBitmap、不绘制标注、不写磁盘。
        /// </summary>
        /// <param name="param">保存图像节点参数。</param>
        /// <param name="outputImage">订阅得到的输出图像。</param>
        /// <param name="imageSaveJudgment">用于 OK/NG 目录判断的统一判定结果。</param>
        /// <param name="barcode">条码命名值。</param>
        /// <param name="traceContext">跨线程流程诊断上下文。</param>
        /// <returns>主流程入队诊断结果。</returns>
        private SaveImageEnqueueResult EnqueueSaveImage(
            NodeParamSaveImage param,
            OutputImage outputImage,
            ImageSaveJudgment imageSaveJudgment,
            string barcode,
            PerformanceTraceContext traceContext)
        {
            Stopwatch stopwatch = traceContext == null ? null : Stopwatch.StartNew();
            DateTime time = DateTime.Now;
            // 1.存图路径（不包含图片名称）
            string imageSavePath;

            // 2.拼接日期路径
            string nowDataTime = time.ToString("yyyyMMdd");
            imageSavePath = Path.Combine(param.SavePath, nowDataTime);

            // 3.图片名称
            string imageName = param.IsBarCode ? barcode : time.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            if (param.NeedCompress)
                imageName += "-CAN"; // 压缩图


            // 4.是否区分早晚班
            if (param.IsDayNight)
            {
                string dayNight = IsDayWorking(time.TimeOfDay, param.DayDataTime, param.NightDataTime) ? "早班" : "晚班";
                imageSavePath = Path.Combine(imageSavePath, dayNight);
            }

            List<string> paths = new List<string>();

            // 5.是否区分OK/NG
            if (param.NeedOkNg)
            {
                if (imageSaveJudgment == null)
                    throw new Exception($"无法获取或解析 OK/NG 判定结果！");

                // 根据统一判定结果创建 OK/NG 文件夹，兼容 AI 结果和多条件布尔结果。
                bool allAreOk = imageSaveJudgment.IsOk;
                string okNg = allAreOk ? "OK" : "NG";
                imageSavePath = Path.Combine(imageSavePath, okNg);

                //要保存什么图片
                switch (param.ImageTypeToSave)
                {
                    case ImageTypeToSave.OkAndNg:
                        if (allAreOk)
                            paths.Add(imageSavePath);
                        else
                            AddNgImagePaths(paths, imageSavePath, imageSaveJudgment);
                        break;
                    case ImageTypeToSave.OnlyOk:
                        if (allAreOk)
                            paths.Add(imageSavePath);
                        break;
                    case ImageTypeToSave.OnlyNg:
                        if (!allAreOk)
                            AddNgImagePaths(paths, imageSavePath, imageSaveJudgment);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                paths.Add(imageSavePath);
            }

            long afterPathDecision = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
            if (paths.Count == 0)
            {
                return new SaveImageEnqueueResult
                {
                    EnqueuedTaskCount = 0,
                    TargetPathCount = 0,
                    PathDecisionMs = afterPathDecision,
                    EnqueueMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch) - afterPathDecision,
                    Skipped = true,
                    AdmissionStatus = BoundedQueueAdmissionStatus.Accepted
                };
            }

            bool isHighPriority = param.NeedOkNg && imageSaveJudgment != null && !imageSaveJudgment.IsOk;
            SaveImageTask saveImageTask = null;
            BoundedQueueAdmissionResult admissionResult;
            ImageQueueProcessor queueProcessor;
            try
            {
                saveImageTask = CreateSaveImageTask(
                    outputImage,
                    outputImage.DisplayResult,
                    paths,
                    imageName,
                    param.NeedCompress,
                    traceContext,
                    isHighPriority,
                    param.CompressValue);

                lock (_queueOwnershipSync)
                {
                    if (Volatile.Read(ref _disposed) == 1)
                        throw new ObjectDisposedException(nameof(NodeImageSave), "保存图像节点已经释放，不能继续投递任务。");

                    Solution.Instance.RecordImageSaveResourceSample(saveImageTask.EstimatedBytes);
                    queueProcessor = Solution.Instance.GetOrCreateImageSaveQueueProcessor();
                    admissionResult = queueProcessor.EnqueueImage(
                        saveImageTask,
                        () => outputImage.AcquireSaveSnapshot());
                    if (admissionResult.Accepted)
                        saveImageTask = null;
                }
            }
            finally
            {
                saveImageTask?.Dispose();
            }

            return new SaveImageEnqueueResult
            {
                EnqueuedTaskCount = admissionResult.Accepted ? 1 : 0,
                TargetPathCount = paths.Count,
                PathDecisionMs = afterPathDecision,
                EnqueueMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch) - afterPathDecision,
                Skipped = !admissionResult.Accepted,
                AdmissionStatus = admissionResult.Status,
                EvictedNormalTaskCount = admissionResult.EvictedLowPriorityCount,
                QueuedCount = queueProcessor.QueuedCount,
                QueuedBytes = queueProcessor.QueuedBytes
            };
        }

        /// <summary>
        /// 将 NG 检测项对应的子目录追加到目标路径集合；没有检测项分类时按配置回落到通用 NG 目录。
        /// </summary>
        /// <param name="paths">目标路径集合。</param>
        /// <param name="imageSavePath">OK/NG 根路径。</param>
        /// <param name="imageSaveJudgment">统一保存判定结果。</param>
        private static void AddNgImagePaths(List<string> paths, string imageSavePath, ImageSaveJudgment imageSaveJudgment)
        {
            if (paths == null || imageSaveJudgment == null)
                return;

            if (imageSaveJudgment.NgCategoryNames.Count == 0)
            {
                if (imageSaveJudgment.UseGenericNgDirectoryWhenNoCategory)
                    paths.Add(imageSavePath);

                return;
            }

            foreach (string categoryName in imageSaveJudgment.NgCategoryNames)
                paths.Add(Path.Combine(imageSavePath, categoryName));
        }

        /// <summary>
        /// 从当前同步流程仍持有的输出图像生成待接纳任务；队列确认可接收后才取得后台租约。
        /// </summary>
        /// <param name="outputImage">待保存的输出图像，后台任务会持有其资源租约。</param>
        /// <param name="displayResult">需要写入保存图的显示结果。</param>
        /// <param name="targetDirectories">保存目标目录集合。</param>
        /// <param name="imageName">图片名称，不含扩展名。</param>
        /// <param name="needCompress">是否压缩。</param>
        /// <param name="traceContext">跨线程流程诊断上下文。</param>
        /// <param name="isHighPriority">当前任务是否为优先保留的NG图。</param>
        /// <param name="compressValue">压缩质量。</param>
        /// <returns>后台保存任务。</returns>
        private SaveImageTask CreateSaveImageTask(
            OutputImage outputImage,
            AlgorithmResult displayResult,
            List<string> targetDirectories,
            string imageName,
            bool needCompress,
            PerformanceTraceContext traceContext,
            bool isHighPriority,
            long compressValue = 100)
        {
            if (outputImage == null)
                throw new ArgumentException("待保存的输出图像为空。", nameof(outputImage));

            long estimatedBytes = outputImage.GetRetainedImageBytesEstimateForAdmission();

            return new SaveImageTask
            {
                SourceImage = null,
                DisplayResult = displayResult,
                TargetDirectories = targetDirectories == null ? new List<string>() : new List<string>(targetDirectories),
                ImageName = imageName,
                NeedCompress = needCompress,
                TraceContext = traceContext,
                CompressValue = compressValue,
                IsHighPriority = isHighPriority,
                EstimatedBytes = estimatedBytes,
                OwnerKey = _queueOwnerKey
            };
        }

        /// <summary>
        /// 判断是否早班
        /// </summary>
        /// <param name="time"></param>
        /// <param name="daydateTime"></param>
        /// <param name="nightdataTime"></param>
        /// <returns></returns>
        private bool IsDayWorking(TimeSpan time, DateTime daydateTime, DateTime nightdataTime)
        {
            TimeSpan dayStartTime = daydateTime.TimeOfDay;
            TimeSpan nightStartTime = nightdataTime.TimeOfDay;
            return time >= dayStartTime && time < nightStartTime;
        }

        /// <summary>
        /// 获取保存图片类型的中文文本，便于现场日志阅读。
        /// </summary>
        /// <param name="imageTypeToSave">保存图片类型。</param>
        /// <returns>保存图片类型中文文本。</returns>
        private static string GetImageTypeText(ImageTypeToSave imageTypeToSave)
        {
            switch (imageTypeToSave)
            {
                case ImageTypeToSave.OkAndNg:
                    return "OK和NG";
                case ImageTypeToSave.OnlyOk:
                    return "仅OK";
                case ImageTypeToSave.OnlyNg:
                    return "仅NG";
                default:
                    return imageTypeToSave.ToString();
            }
        }

    }

    /// <summary>
    /// 全方案共享的图像保存工作池，统一限制后台并发、排队数量和图像持有字节数。
    /// </summary>
    public class ImageQueueProcessor
    {
        /// <summary>保存任务使用的容量与字节双重有界队列。</summary>
        private readonly BoundedPriorityWorkQueue<SaveImageTask> _imageQueue;

        /// <summary>保存队列淘汰后等待专用线程释放的任务集合。</summary>
        private readonly ConcurrentQueue<RetiredSaveImageTask> _retiredTasks = new ConcurrentQueue<RetiredSaveImageTask>();

        /// <summary>保护淘汰回收通道任务数、字节数与峰值的一致性。</summary>
        private readonly object _retiredTaskSync = new object();

        /// <summary>唤醒淘汰任务专用释放线程的信号。</summary>
        private readonly AutoResetEvent _retiredTaskSignal = new AutoResetEvent(false);

        /// <summary>后台保存工作线程数量。</summary>
        private readonly int _workerCount;

        /// <summary>工作池停止时等待正在写盘任务的最长毫秒数。</summary>
        private readonly int _shutdownTimeoutMs;

        /// <summary>空闲重配时仅用于唤醒并退出固定工作线程的最小保护时间。</summary>
        private const int IdleWorkerShutdownGraceMilliseconds = 1000;

        /// <summary>工作池诊断日志中的所有者文本。</summary>
        private readonly string _ownerText;

        /// <summary>在保存消费者线程执行磁盘空间准入和慢写阈值判断的保护器。</summary>
        private readonly IImageSaveStorageGuard _storageGuard;

        /// <summary>保存转换和JPEG编码共享的全方案CPU重任务调度器；空表示兼容独立测试宿主。</summary>
        private readonly ICpuWorkScheduler _cpuWorkScheduler;

        /// <summary>停止超时前用于取消CPU许可等待并释放已出队图像租约的独立令牌源。</summary>
        private readonly CancellationTokenSource _shutdownCancellationSource = new CancellationTokenSource();

        /// <summary>串行化停止与重复停止，确保等待句柄只在全部线程退出后释放一次。</summary>
        private readonly object _stopSync = new object();

        /// <summary>保存工作任务句柄，停止时用于确定全部消费者已经退出。</summary>
        private Task[] _workerTasks = new Task[0];

        /// <summary>淘汰任务专用释放线程的任务句柄。</summary>
        private Task _retiredTaskWorker;

        /// <summary>淘汰任务专用释放线程是否继续等待新任务。</summary>
        private int _retiredTaskWorkerAccepting = 1;

        /// <summary>淘汰任务回收通道允许持有的最大任务数，防止回收速度落后时形成第二个无界队列。</summary>
        private readonly int _retiredTaskCapacity;

        /// <summary>淘汰任务回收通道允许持有的最大估算字节数。</summary>
        private readonly long _retiredTaskMemoryBudgetBytes;

        /// <summary>正在排队或释放中的淘汰任务数量。</summary>
        private int _retiredTaskBacklogCount;

        /// <summary>正在排队或释放中的淘汰任务估算字节数。</summary>
        private long _retiredTaskBacklogBytes;

        /// <summary>淘汰回收通道观察到的任务数量峰值。</summary>
        private int _peakRetiredTaskCount;

        /// <summary>淘汰回收通道观察到的估算字节峰值。</summary>
        private long _peakRetiredTaskBytes;

        /// <summary>淘汰回收通道已满时在投递线程直接释放的任务累计数量。</summary>
        private long _synchronousRetiredReleaseCount;

        /// <summary>当前正在转换、绘制或写盘的任务数量。</summary>
        private int _activeWorkerCount;

        /// <summary>工作池本次生命周期内观察到的实际保存并发峰值。</summary>
        private int _peakActiveWorkerCount;

        /// <summary>因队列达到边界而拒绝的任务累计数量。</summary>
        private long _rejectedTaskCount;

        /// <summary>为保留NG任务而淘汰的普通任务累计数量。</summary>
        private long _evictedNormalTaskCount;

        /// <summary>已经成功处理全部目标路径的保存任务累计数量。</summary>
        private long _completedTaskCount;

        /// <summary>转换、绘制或写盘失败的保存任务累计数量。</summary>
        private long _failedTaskCount;

        /// <summary>工作池停止时取消CPU许可等待的保存任务累计数量。</summary>
        private long _canceledTaskCount;

        /// <summary>磁盘低空间时跳过的普通图片累计数量。</summary>
        private long _lowSpaceSkippedNormalTaskCount;

        /// <summary>磁盘严重低空间时跳过的全部图片累计数量。</summary>
        private long _criticalSpaceSkippedTaskCount;

        /// <summary>磁盘低空间时继续保留的NG图片累计数量。</summary>
        private long _lowSpaceAllowedHighPriorityTaskCount;

        /// <summary>磁盘空间探测失败但继续执行保存的累计数量。</summary>
        private long _storageProbeFailureCount;

        /// <summary>达到慢写阈值的目标路径累计数量。</summary>
        private long _slowWriteCount;

        /// <summary>观察到的单个目标路径最大写盘耗时。</summary>
        private long _maximumWriteElapsedMilliseconds;

        /// <summary>保证工作池只启动一次。</summary>
        private int _started;

        /// <summary>保证工作池只停止一次。</summary>
        private int _stopped;

        /// <summary>停止完成后底层队列与等待句柄是否已经释放。</summary>
        private int _infrastructureDisposed;

        /// <summary>保证每个工作池生命周期只写一次停止汇总。</summary>
        private int _summaryLogged;

        /// <summary>当前等待保存的图像数量。</summary>
        public int QueuedCount => _imageQueue.Count;

        /// <summary>当前等待保存任务累计持有的估算字节数。</summary>
        public long QueuedBytes => _imageQueue.QueuedBytes;

        /// <summary>有界保存队列允许等待的最大任务数量。</summary>
        public int Capacity => _imageQueue.Capacity;

        /// <summary>有界保存队列允许等待任务累计持有的最大字节数。</summary>
        public long MemoryBudgetBytes => _imageQueue.MemoryBudgetBytes;

        /// <summary>工作池本次生命周期内观察到的等待任务峰值。</summary>
        public int PeakQueuedCount => _imageQueue.PeakCount;

        /// <summary>工作池本次生命周期内观察到的等待字节峰值。</summary>
        public long PeakQueuedBytes => _imageQueue.PeakQueuedBytes;

        /// <summary>后台保存工作线程数量。</summary>
        public int WorkerCount => _workerCount;

        /// <summary>当前正在转换、绘制或写盘的任务数量。</summary>
        public int ActiveWorkerCount => Volatile.Read(ref _activeWorkerCount);

        /// <summary>工作池本次生命周期内观察到的实际保存并发峰值。</summary>
        public int PeakActiveWorkerCount => Volatile.Read(ref _peakActiveWorkerCount);

        /// <summary>工作池是否已经停止接收任务。</summary>
        public bool IsStopped => Volatile.Read(ref _stopped) == 1;

        /// <summary>保存消费者和淘汰资源回收线程是否都已经退出。</summary>
        public bool IsFullyStopped => IsStopped && AreAllWorkersCompleted();

        /// <summary>因达到队列边界而被拒绝的任务累计数量。</summary>
        public long RejectedTaskCount => Interlocked.Read(ref _rejectedTaskCount);

        /// <summary>为接收NG任务而淘汰的普通任务累计数量。</summary>
        public long EvictedNormalTaskCount => Interlocked.Read(ref _evictedNormalTaskCount);

        /// <summary>已经成功处理全部目标路径的保存任务累计数量。</summary>
        public long CompletedTaskCount => Interlocked.Read(ref _completedTaskCount);

        /// <summary>转换、绘制或写盘失败的保存任务累计数量。</summary>
        public long FailedTaskCount => Interlocked.Read(ref _failedTaskCount);

        /// <summary>工作池停止时取消CPU许可等待的保存任务累计数量。</summary>
        public long CanceledTaskCount => Interlocked.Read(ref _canceledTaskCount);

        /// <summary>磁盘低空间时跳过的普通图片累计数量。</summary>
        public long LowSpaceSkippedNormalTaskCount => Interlocked.Read(ref _lowSpaceSkippedNormalTaskCount);

        /// <summary>磁盘严重低空间时跳过的全部图片累计数量。</summary>
        public long CriticalSpaceSkippedTaskCount => Interlocked.Read(ref _criticalSpaceSkippedTaskCount);

        /// <summary>磁盘低空间时继续保留的NG图片累计数量。</summary>
        public long LowSpaceAllowedHighPriorityTaskCount => Interlocked.Read(ref _lowSpaceAllowedHighPriorityTaskCount);

        /// <summary>磁盘空间探测失败但继续保存的累计数量。</summary>
        public long StorageProbeFailureCount => Interlocked.Read(ref _storageProbeFailureCount);

        /// <summary>达到慢写阈值的目标路径写入累计数量。</summary>
        public long SlowWriteCount => Interlocked.Read(ref _slowWriteCount);

        /// <summary>观察到的单个目标路径最大写盘耗时。</summary>
        public long MaximumWriteElapsedMilliseconds => Interlocked.Read(ref _maximumWriteElapsedMilliseconds);

        /// <summary>淘汰回收通道已满后在投递线程直接释放的累计数量。</summary>
        public long SynchronousRetiredReleaseCount => Interlocked.Read(ref _synchronousRetiredReleaseCount);

        /// <summary>淘汰回收通道观察到的任务数量峰值。</summary>
        public int PeakRetiredTaskCount => Volatile.Read(ref _peakRetiredTaskCount);

        /// <summary>淘汰回收通道观察到的估算字节峰值。</summary>
        public long PeakRetiredTaskBytes => Interlocked.Read(ref _peakRetiredTaskBytes);

        /// <summary>工作池当前是否没有等待、活动或淘汰回收任务。</summary>
        public bool IsIdle => QueuedCount == 0 &&
            ActiveWorkerCount == 0 &&
            Volatile.Read(ref _retiredTaskBacklogCount) == 0;

        /// <summary>
        /// 获取当前保存队列、资源边界和磁盘保护计数的不可变快照。
        /// </summary>
        /// <returns>可供Debug智能体和压力测试读取的诊断快照。</returns>
        public ImageSaveQueueDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            return new ImageSaveQueueDiagnosticsSnapshot
            {
                Timestamp = DateTime.Now,
                QueuedCount = QueuedCount,
                Capacity = Capacity,
                PeakQueuedCount = PeakQueuedCount,
                QueuedBytes = QueuedBytes,
                MemoryBudgetBytes = MemoryBudgetBytes,
                PeakQueuedBytes = PeakQueuedBytes,
                ActiveWorkerCount = ActiveWorkerCount,
                WorkerCount = WorkerCount,
                PeakActiveWorkerCount = PeakActiveWorkerCount,
                RejectedTaskCount = RejectedTaskCount,
                EvictedNormalTaskCount = EvictedNormalTaskCount,
                CompletedTaskCount = CompletedTaskCount,
                FailedTaskCount = FailedTaskCount,
                CanceledTaskCount = CanceledTaskCount,
                LowSpaceSkippedNormalTaskCount = LowSpaceSkippedNormalTaskCount,
                CriticalSpaceSkippedTaskCount = CriticalSpaceSkippedTaskCount,
                LowSpaceAllowedHighPriorityTaskCount = LowSpaceAllowedHighPriorityTaskCount,
                StorageProbeFailureCount = StorageProbeFailureCount,
                SlowWriteCount = SlowWriteCount,
                MaximumWriteElapsedMilliseconds = MaximumWriteElapsedMilliseconds
            };
        }

        /// <summary>
        /// 后台保存任务；通过租约借用流程输出Mat，后台线程负责释放租约和生成的Bitmap。
        /// </summary>
        public sealed class SaveImageTask : IPrioritizedResourceWorkItem
        {
            /// <summary>待保存的源图像引用，生命周期由ImageLease保护。</summary>
            public Mat SourceImage { get; set; }
            /// <summary>需要写入保存图的显示叠加结果。</summary>
            public AlgorithmResult DisplayResult { get; set; }
            /// <summary>保存目标目录集合。</summary>
            public List<string> TargetDirectories { get; set; } = new List<string>();
            /// <summary>图片名称，不包含扩展名。</summary>
            public string ImageName { get; set; }
            /// <summary>是否需要压缩。</summary>
            public bool NeedCompress { get; set; }
            /// <summary>跨线程流程诊断上下文，Debug关闭时为空。</summary>
            public PerformanceTraceContext TraceContext { get; set; }
            /// <summary>压缩质量。</summary>
            public long CompressValue { get; set; }
            /// <summary>当前任务是否为队列满时优先保留的NG图。</summary>
            public bool IsHighPriority { get; set; }
            /// <summary>当前任务继续排队会持有的源Mat估算字节数。</summary>
            public long EstimatedBytes { get; set; }
            /// <summary>当前任务所属保存节点的唯一标识。</summary>
            public string OwnerKey { get; set; }

            /// <summary>
            /// 实际保存的图像租约字段，用于原子保证只释放一次。
            /// </summary>
            private IImageResourceLease _imageLease;

            /// <summary>保护SourceImage所属输出图像的资源租约。</summary>
            public IImageResourceLease ImageLease
            {
                get { return Volatile.Read(ref _imageLease); }
                set { _imageLease = value; }
            }

            /// <summary>
            /// 释放保存任务持有的图像租约；重复调用安全。
            /// </summary>
            public void Dispose()
            {
                IImageResourceLease imageLease = Interlocked.Exchange(ref _imageLease, null);
                imageLease?.Dispose();
            }

        }

        /// <summary>
        /// 淘汰回收通道内部的不可变条目，冻结原主队列任务的资源字节数。
        /// </summary>
        private sealed class RetiredSaveImageTask
        {
            /// <summary>需要在专用回收线程释放的保存任务。</summary>
            public SaveImageTask Task { get; private set; }

            /// <summary>进入回收通道时冻结的资源估算字节数。</summary>
            public long EstimatedBytes { get; private set; }

            /// <summary>
            /// 创建淘汰回收条目。
            /// </summary>
            /// <param name="task">待释放任务。</param>
            /// <param name="estimatedBytes">规范化后的估算字节数。</param>
            public RetiredSaveImageTask(SaveImageTask task, long estimatedBytes)
            {
                Task = task;
                EstimatedBytes = estimatedBytes;
            }
        }

        /// <summary>
        /// 使用自动资源档案给出的全局参数初始化保存工作池。
        /// </summary>
        /// <param name="workerCount">全方案保存工作线程数量。</param>
        /// <param name="capacity">允许等待的最大任务数量。</param>
        /// <param name="memoryBudgetBytes">允许等待任务累计持有的最大字节数。</param>
        /// <param name="shutdownTimeoutMs">停止时等待活动任务的最长毫秒数。</param>
        /// <param name="ownerText">工作池诊断名称。</param>
        public ImageQueueProcessor(
            int workerCount,
            int capacity,
            long memoryBudgetBytes,
            int shutdownTimeoutMs,
            string ownerText)
            : this(
                workerCount,
                capacity,
                memoryBudgetBytes,
                shutdownTimeoutMs,
                ownerText,
                CachedImageSaveStorageGuard.CreateDefault(),
                null)
        {
        }

        /// <summary>
        /// 使用自动资源档案和可替换磁盘保护器初始化保存工作池。
        /// </summary>
        /// <param name="workerCount">全方案保存工作线程数量。</param>
        /// <param name="capacity">允许等待的最大任务数量。</param>
        /// <param name="memoryBudgetBytes">允许等待任务累计持有的最大字节数。</param>
        /// <param name="shutdownTimeoutMs">停止时等待活动任务的最长毫秒数。</param>
        /// <param name="ownerText">工作池诊断名称。</param>
        /// <param name="storageGuard">磁盘空间与慢写保护器。</param>
        public ImageQueueProcessor(
            int workerCount,
            int capacity,
            long memoryBudgetBytes,
            int shutdownTimeoutMs,
            string ownerText,
            IImageSaveStorageGuard storageGuard)
            : this(
                workerCount,
                capacity,
                memoryBudgetBytes,
                shutdownTimeoutMs,
                ownerText,
                storageGuard,
                null)
        {
        }

        /// <summary>
        /// 使用自动资源档案、磁盘保护器和共享CPU调度器初始化保存工作池。
        /// </summary>
        /// <param name="workerCount">全方案保存工作线程数量。</param>
        /// <param name="capacity">允许等待的最大任务数量。</param>
        /// <param name="memoryBudgetBytes">允许等待任务累计持有的最大字节数。</param>
        /// <param name="shutdownTimeoutMs">停止时等待活动任务的最长毫秒数。</param>
        /// <param name="ownerText">工作池诊断名称。</param>
        /// <param name="storageGuard">磁盘空间与慢写保护器。</param>
        /// <param name="cpuWorkScheduler">保存转换和编码使用的共享CPU调度器。</param>
        public ImageQueueProcessor(
            int workerCount,
            int capacity,
            long memoryBudgetBytes,
            int shutdownTimeoutMs,
            string ownerText,
            IImageSaveStorageGuard storageGuard,
            ICpuWorkScheduler cpuWorkScheduler)
        {
            if (workerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(workerCount), "保存工作线程数量必须大于零。");
            if (shutdownTimeoutMs < 0)
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeoutMs), "停止等待时间不能小于零。");

            _workerCount = workerCount;
            _shutdownTimeoutMs = shutdownTimeoutMs;
            _ownerText = ownerText;
            _storageGuard = storageGuard ?? throw new ArgumentNullException(nameof(storageGuard));
            _cpuWorkScheduler = cpuWorkScheduler;
            _retiredTaskCapacity = Math.Max(1, capacity);
            _retiredTaskMemoryBudgetBytes = Math.Max(1L, memoryBudgetBytes);
            _imageQueue = new BoundedPriorityWorkQueue<SaveImageTask>(
                capacity,
                memoryBudgetBytes,
                ex => TryWriteLog(MsgLevel.Exception, $"释放保存排队任务失败：{ex.Message}", true),
                QueueRetiredTaskForRelease);
        }

        /// <summary>
        /// 启动固定数量的后台保存消费者，并保留任务句柄供停止等待。
        /// </summary>
        public void StartProcessing()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            _retiredTaskWorker = Task.Factory.StartNew(
                ProcessRetiredTasks,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            _workerTasks = new Task[_workerCount];
            for (int i = 0; i < _workerCount; i++)
            {
                _workerTasks[i] = Task.Factory.StartNew(
                    ProcessImages,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            if (PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug))
            {
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【内存诊断-保存队列启动】工作池({_ownerText}) 工作线程={_workerCount}；容量={Capacity}；字节预算={MemoryBudgetBytes}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                    true);
            }
        }

        /// <summary>
        /// 零等待尝试投递保存任务；只有成功时工作池才接管任务所有权。
        /// </summary>
        /// <param name="saveImagedata">待保存任务。</param>
        /// <returns>有界队列入队结果。</returns>
        public BoundedQueueAdmissionResult EnqueueImage(SaveImageTask saveImagedata)
        {
            return EnqueueImage(saveImagedata, null);
        }

        /// <summary>
        /// 零等待尝试投递保存任务，确定队列可接收后才取得图像资源租约。
        /// </summary>
        /// <param name="saveImagedata">尚未发布给消费者的保存任务。</param>
        /// <param name="snapshotFactory">确定可接收后调用的原子图像快照工厂；已有租约的测试任务可传null。</param>
        /// <returns>有界队列入队结果。</returns>
        public BoundedQueueAdmissionResult EnqueueImage(
            SaveImageTask saveImagedata,
            Func<OutputImage.OutputImageSaveSnapshot> snapshotFactory)
        {
            if (saveImagedata == null) throw new ArgumentNullException(nameof(saveImagedata), "保存任务不能为空。");
            if (snapshotFactory == null && !OutputImage.HasValidImage(saveImagedata.SourceImage)) throw new ArgumentException("保存任务的源图像为空。", nameof(saveImagedata));
            if (saveImagedata.TargetDirectories == null || saveImagedata.TargetDirectories.Count == 0) throw new ArgumentException("保存任务的目标目录为空。", nameof(saveImagedata));

            BoundedQueueAdmissionResult result = snapshotFactory == null
                ? _imageQueue.TryEnqueue(saveImagedata)
                : _imageQueue.TryEnqueueWithUpdatedEstimate(
                    saveImagedata,
                    () =>
                    {
                        using (OutputImage.OutputImageSaveSnapshot snapshot = snapshotFactory())
                        {
                            if (snapshot == null || !OutputImage.HasValidImage(snapshot.SourceImage))
                                throw new InvalidOperationException("保存任务快照工厂返回了空图像。");

                            saveImagedata.EstimatedBytes = snapshot.EstimatedBytes;
                            saveImagedata.SourceImage = snapshot.SourceImage;
                            saveImagedata.ImageLease = snapshot.TransferLease();
                            return saveImagedata.EstimatedBytes;
                        }
                    });
            if (result.EvictedLowPriorityCount > 0)
            {
                long evictedCount = Interlocked.Add(ref _evictedNormalTaskCount, result.EvictedLowPriorityCount);
                if (evictedCount == result.EvictedLowPriorityCount || evictedCount % 100 == 0)
                {
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        $"保存队列达到边界，为保留NG图已淘汰普通图片；本次={result.EvictedLowPriorityCount}；累计={evictedCount}；队列={QueuedCount}/{Capacity}；字节={QueuedBytes}/{MemoryBudgetBytes}。",
                        true);
                }
            }

            if (!result.Accepted)
            {
                long rejectedCount = Interlocked.Increment(ref _rejectedTaskCount);
                if (rejectedCount == 1 || rejectedCount % 100 == 0)
                {
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        $"保存队列拒绝当前图片；状态={result.Status}；NG={saveImagedata.IsHighPriority}；累计拒绝={rejectedCount}；队列={QueuedCount}/{Capacity}；字节={QueuedBytes}/{MemoryBudgetBytes}。",
                        true);
                }
            }

            return result;
        }

        /// <summary>
        /// 将被NG任务淘汰的普通任务转交给专用回收线程，避免检测流程同步释放整条Mat链。
        /// </summary>
        /// <param name="retiredTask">队列已经移除并仍拥有的任务。</param>
        /// <param name="estimatedBytes">主队列入队时冻结的资源估算字节数。</param>
        private void QueueRetiredTaskForRelease(SaveImageTask retiredTask, long estimatedBytes)
        {
            if (retiredTask == null)
                return;

            if (Volatile.Read(ref _retiredTaskWorkerAccepting) == 0)
            {
                DisposeSaveTaskNoThrow(retiredTask, "停止后的淘汰任务");
                return;
            }

            estimatedBytes = Math.Max(1L, estimatedBytes);
            RetiredSaveImageTask retiredEntry = new RetiredSaveImageTask(retiredTask, estimatedBytes);
            bool acceptedForBackgroundRelease;
            lock (_retiredTaskSync)
            {
                acceptedForBackgroundRelease = Volatile.Read(ref _retiredTaskWorkerAccepting) == 1 &&
                    _retiredTaskBacklogCount < _retiredTaskCapacity &&
                    estimatedBytes <= _retiredTaskMemoryBudgetBytes - _retiredTaskBacklogBytes;
                if (acceptedForBackgroundRelease)
                {
                    _retiredTaskBacklogCount++;
                    _retiredTaskBacklogBytes += estimatedBytes;
                    UpdateMaximum(ref _peakRetiredTaskCount, _retiredTaskBacklogCount);
                    UpdateMaximum(ref _peakRetiredTaskBytes, _retiredTaskBacklogBytes);
                }
            }

            if (!acceptedForBackgroundRelease)
            {
                long synchronousReleaseCount = Interlocked.Increment(ref _synchronousRetiredReleaseCount);
                if (synchronousReleaseCount == 1 || synchronousReleaseCount % 100 == 0)
                {
                    TryWriteLog(
                        MsgLevel.Warn,
                        $"淘汰任务回收通道达到边界，当前任务改由投递线程同步释放；累计={synchronousReleaseCount}；回收队列={Volatile.Read(ref _retiredTaskBacklogCount)}/{_retiredTaskCapacity}；回收字节={Interlocked.Read(ref _retiredTaskBacklogBytes)}/{_retiredTaskMemoryBudgetBytes}。",
                        true);
                }
                DisposeSaveTaskNoThrow(retiredTask, "回收通道已满的淘汰任务");
                return;
            }

            _retiredTasks.Enqueue(retiredEntry);
            _retiredTaskSignal.Set();
        }

        /// <summary>
        /// 在独立长期线程中释放被淘汰任务，不占用检测流程或通用线程池。
        /// </summary>
        private void ProcessRetiredTasks()
        {
            while (Volatile.Read(ref _retiredTaskWorkerAccepting) == 1 ||
                Volatile.Read(ref _retiredTaskBacklogCount) > 0 ||
                !_retiredTasks.IsEmpty)
            {
                if (_retiredTasks.TryDequeue(out RetiredSaveImageTask retiredTask))
                {
                    try
                    {
                        DisposeSaveTaskNoThrow(retiredTask.Task, "被NG淘汰的普通任务");
                    }
                    finally
                    {
                        lock (_retiredTaskSync)
                        {
                            _retiredTaskBacklogCount--;
                            _retiredTaskBacklogBytes -= retiredTask.EstimatedBytes;
                            if (_retiredTaskBacklogCount < 0)
                                _retiredTaskBacklogCount = 0;
                            if (_retiredTaskBacklogBytes < 0)
                                _retiredTaskBacklogBytes = 0;
                        }
                    }
                    continue;
                }

                _retiredTaskSignal.WaitOne(100);
            }
        }

        /// <summary>
        /// 持续消费保存任务，直到工作池停止并排空等待队列。
        /// </summary>
        private void ProcessImages()
        {
            using (MemoryStream encodingStream = new MemoryStream())
            {
                ProcessImages(encodingStream);
            }
        }

        /// <summary>
        /// 使用当前工作线程独占的编码缓冲区逐项处理保存任务。
        /// </summary>
        /// <param name="encodingStream">当前工作线程复用的JPEG编码缓冲区。</param>
        private void ProcessImages(MemoryStream encodingStream)
        {
            while (!_shutdownCancellationSource.IsCancellationRequested &&
                _imageQueue.TryTake(out SaveImageTask saveImagedata, RegisterActiveWorker))
            {
                bool saveCompleted = false;
                bool taskFailed = false;
                bool taskCanceled = false;
                bool storageSkipped = false;
                bool diagnosticEnabled = false;
                string imageInfo = string.Empty;
                Bitmap bitmap = null;
                EncodedImageBuffer encodedImage = null;
                double conversionPermitWaitMilliseconds = 0D;
                double encodingPermitWaitMilliseconds = 0D;
                List<string> committedImagePaths = new List<string>();
                MemorySnapshot beforeSaveSnapshot = null;
                Stopwatch stopwatch = null;
                try
                {
                    diagnosticEnabled = saveImagedata.TraceContext != null &&
                        PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
                    if (diagnosticEnabled)
                    {
                        imageInfo = PerformanceSpikeDiagnostics.GetMatText(saveImagedata.SourceImage);
                        beforeSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                        stopwatch = Stopwatch.StartNew();
                    }

                    ImageSaveStorageDecision storageDecision = _storageGuard.Evaluate(
                        saveImagedata.TargetDirectories,
                        saveImagedata.IsHighPriority);
                    if (storageDecision.Status == ImageSaveStorageStatus.ProbeFailedAllowed)
                    {
                        long probeFailureCount = Interlocked.Increment(ref _storageProbeFailureCount);
                        LogStorageGuardEvent(
                            probeFailureCount,
                            $"保存磁盘空间探测失败，本任务继续保存；根目录={storageDecision.StorageRoot}；原因={storageDecision.Reason}");
                    }
                    else if (storageDecision.Status == ImageSaveStorageStatus.AllowedHighPriorityUnderLowSpace)
                    {
                        long allowedHighPriorityCount = Interlocked.Increment(ref _lowSpaceAllowedHighPriorityTaskCount);
                        LogStorageGuardEvent(
                            allowedHighPriorityCount,
                            $"保存磁盘空间不足但继续保留NG图片；根目录={storageDecision.StorageRoot}；可用={storageDecision.AvailableSpaceMb}MB；低水位={_storageGuard.LowSpaceThresholdMb}MB");
                    }
                    else if (storageDecision.Status == ImageSaveStorageStatus.SkippedLowSpace)
                    {
                        storageSkipped = true;
                        long skippedCount = Interlocked.Increment(ref _lowSpaceSkippedNormalTaskCount);
                        LogStorageGuardEvent(
                            skippedCount,
                            $"保存磁盘空间不足，已跳过普通图片；根目录={storageDecision.StorageRoot}；可用={storageDecision.AvailableSpaceMb}MB；低水位={_storageGuard.LowSpaceThresholdMb}MB");
                    }
                    else if (storageDecision.Status == ImageSaveStorageStatus.SkippedCriticalSpace)
                    {
                        storageSkipped = true;
                        long skippedCount = Interlocked.Increment(ref _criticalSpaceSkippedTaskCount);
                        LogStorageGuardEvent(
                            skippedCount,
                            $"保存磁盘达到严重低空间，已跳过当前图片；根目录={storageDecision.StorageRoot}；可用={storageDecision.AvailableSpaceMb}MB；严重水位={_storageGuard.CriticalSpaceThresholdMb}MB；NG={saveImagedata.IsHighPriority}");
                    }

                    if (storageDecision.ShouldSkip)
                        continue;

                    using (ICpuWorkLease conversionLease = AcquireCpuWorkLease(CpuWorkloadKind.ImageConversion))
                    {
                        conversionPermitWaitMilliseconds = conversionLease?.WaitElapsedMilliseconds ?? 0D;
                        bitmap = CreateBitmapForSave(saveImagedata, stopwatch, out SaveImageRenderTiming renderTiming);
                        renderTiming.CpuPermitWaitMilliseconds = conversionPermitWaitMilliseconds;
                        if (diagnosticEnabled)
                        {
                            MemorySnapshot afterRenderSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                            long traceToRenderMilliseconds = saveImagedata.TraceContext.GetElapsedMilliseconds(Stopwatch.GetTimestamp());
                            PerformanceSpikeDiagnostics.LogIfEnabled(
                                MsgLevel.Debug,
                                () => $"【内存诊断-保存图像后台取图】【完整耗时】{saveImagedata.TraceContext.ToLogText()}；阶段=后台取图完成；源图={imageInfo}；待保存Bitmap={PerformanceSpikeDiagnostics.GetBitmapText(bitmap)}；有标注={renderTiming.HasDrawableDisplayResult}；CPU许可等待={renderTiming.CpuPermitWaitMilliseconds:F3}ms；ToBitmap={renderTiming.ToBitmapMs}ms；确保可绘制={renderTiming.EnsureDrawableMs}ms；写入标注={renderTiming.DrawDisplayResultMs}ms；后台取图总耗时={renderTiming.TotalMs}ms；节点到后台取图={traceToRenderMilliseconds}ms；目标路径={saveImagedata.TargetDirectories.Count}；私有内存变化={afterRenderSnapshot.PrivateMemoryMb - beforeSaveSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterRenderSnapshot.GdiObjectCount - beforeSaveSnapshot.GdiObjectCount}；前={beforeSaveSnapshot.ToLogText()}；后={afterRenderSnapshot.ToLogText()}",
                                true);
                        }
                    }

                    long beforeEncode = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                    using (ICpuWorkLease encodingLease = AcquireCpuWorkLease(CpuWorkloadKind.ImageEncoding))
                    {
                        encodingPermitWaitMilliseconds = encodingLease?.WaitElapsedMilliseconds ?? 0D;
                        encodedImage = EncodeBitmapToJpeg(
                            bitmap,
                            saveImagedata.NeedCompress,
                            saveImagedata.CompressValue,
                            encodingStream);
                    }
                    long afterEncode = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);

                    foreach (string targetDirectory in saveImagedata.TargetDirectories)
                    {
                        long beforePath = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                        long writeStartedTimestamp = Stopwatch.GetTimestamp();
                        _storageGuard.PrepareWrite(targetDirectory);
                        Directory.CreateDirectory(targetDirectory);
                        long afterPath = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);

                        string fileName = SaveEncodedImage(
                            encodedImage,
                            targetDirectory,
                            saveImagedata.ImageName);
                        committedImagePaths.Add(fileName);
                        long writeElapsedMilliseconds = GetElapsedMilliseconds(writeStartedTimestamp);
                        UpdateMaximum(ref _maximumWriteElapsedMilliseconds, writeElapsedMilliseconds);
                        if (_storageGuard.IsSlowWrite(writeElapsedMilliseconds))
                        {
                            long slowWriteCount = Interlocked.Increment(ref _slowWriteCount);
                            LogStorageGuardEvent(
                                slowWriteCount,
                                $"保存目标路径写入缓慢；目录={targetDirectory}；耗时={writeElapsedMilliseconds}ms；阈值={_storageGuard.SlowWriteThresholdMs}ms；队列={QueuedCount}/{Capacity}");
                        }
                        long afterSave = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                        if (diagnosticEnabled)
                        {
                            MemorySnapshot afterSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                            long traceToSaveMilliseconds = saveImagedata.TraceContext.GetElapsedMilliseconds(Stopwatch.GetTimestamp());
                            PerformanceSpikeDiagnostics.LogIfEnabled(
                                MsgLevel.Debug,
                                () => $"【内存诊断-保存队列消费】【完整耗时】{saveImagedata.TraceContext.ToLogText()}；阶段=文件保存完成；路径={fileName}；源图={imageInfo}；保存Bitmap={PerformanceSpikeDiagnostics.GetBitmapText(bitmap)}；压缩={saveImagedata.NeedCompress}；编码许可等待={encodingPermitWaitMilliseconds:F3}ms；JPEG编码={afterEncode - beforeEncode}ms；编码字节={encodedImage.Length}；路径准备={afterPath - beforePath}ms；文件写入={afterSave - afterPath}ms；后台总耗时={afterSave}ms；节点到保存完成={traceToSaveMilliseconds}ms；队列剩余={QueuedCount}；私有内存变化={afterSaveSnapshot.PrivateMemoryMb - beforeSaveSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterSaveSnapshot.GdiObjectCount - beforeSaveSnapshot.GdiObjectCount}；前={beforeSaveSnapshot.ToLogText()}；后={afterSaveSnapshot.ToLogText()}",
                                true);
                        }
                    }

                    saveCompleted = true;
                }
                catch (OperationCanceledException) when (_shutdownCancellationSource.IsCancellationRequested)
                {
                    taskCanceled = true;
                }
                catch (Exception ex)
                {
                    taskFailed = true;
                    foreach (string committedImagePath in committedImagePaths)
                    {
                        if (!TryDeleteIncompleteFile(committedImagePath))
                        {
                            TryWriteLog(
                                MsgLevel.Warn,
                                $"保存任务回滚文件失败，磁盘上可能残留不完整结果：{committedImagePath}",
                                true);
                        }
                    }
                    TryWriteLog(
                        MsgLevel.Exception,
                        $"保存图像失败：{saveImagedata.TraceContext?.ToLogText() ?? $"节点={_ownerText}"}；{ex.Message}",
                        true);
                }
                finally
                {
                    try
                    {
                        MemorySnapshot beforeDisposeSnapshot = null;
                        if (diagnosticEnabled)
                        {
                            try
                            {
                                beforeDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                            }
                            catch (Exception diagnosticEx)
                            {
                                TryWriteLog(MsgLevel.Warn, $"保存释放前诊断采集失败：{diagnosticEx.Message}", true);
                            }
                        }

                        try
                        {
                            bitmap?.Dispose();
                        }
                        catch (Exception bitmapDisposeEx)
                        {
                            taskFailed = true;
                            TryWriteLog(MsgLevel.Exception, $"释放保存Bitmap失败：{bitmapDisposeEx.Message}", true);
                        }

                        if (!DisposeSaveTaskNoThrow(saveImagedata, "保存工作任务"))
                            taskFailed = true;

                        if (diagnosticEnabled && beforeDisposeSnapshot != null)
                        {
                            try
                            {
                                MemorySnapshot afterDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                                PerformanceSpikeDiagnostics.LogIfEnabled(
                                    MsgLevel.Debug,
                                    () => $"【内存诊断-保存队列释放】{saveImagedata.TraceContext.ToLogText()}；已释放后台生成Bitmap；源图={imageInfo}；释放后队列={QueuedCount}；私有内存变化={afterDisposeSnapshot.PrivateMemoryMb - beforeDisposeSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterDisposeSnapshot.GdiObjectCount - beforeDisposeSnapshot.GdiObjectCount}；前={beforeDisposeSnapshot.ToLogText()}；后={afterDisposeSnapshot.ToLogText()}",
                                    true);
                            }
                            catch (Exception diagnosticEx)
                            {
                                TryWriteLog(MsgLevel.Warn, $"保存释放后诊断记录失败：{diagnosticEx.Message}", true);
                            }
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        taskFailed = true;
                        TryWriteLog(MsgLevel.Exception, $"保存任务清理出现未预期异常：{cleanupEx.Message}", true);
                    }
                    finally
                    {
                        if (storageSkipped)
                        {
                            // 低空间跳过属于明确降级，不计入成功写盘或写盘失败。
                        }
                        else if (taskCanceled)
                            Interlocked.Increment(ref _canceledTaskCount);
                        else if (saveCompleted && !taskFailed)
                            Interlocked.Increment(ref _completedTaskCount);
                        else
                            Interlocked.Increment(ref _failedTaskCount);
                        Interlocked.Decrement(ref _activeWorkerCount);
                    }
                }
            }
        }

        /// <summary>
        /// 后台生成保存 Bitmap 的分段耗时。
        /// </summary>
        private struct SaveImageRenderTiming
        {
            /// <summary>取得图像转换CPU许可前的等待毫秒数。</summary>
            public double CpuPermitWaitMilliseconds;

            /// <summary>Mat 转 Bitmap 耗时。</summary>
            public long ToBitmapMs;
            /// <summary>确保 Bitmap 可绘制耗时。</summary>
            public long EnsureDrawableMs;
            /// <summary>写入显示叠加层耗时。</summary>
            public long DrawDisplayResultMs;
            /// <summary>后台取图总耗时。</summary>
            public long TotalMs;
            /// <summary>是否存在可绘制显示结果。</summary>
            public bool HasDrawableDisplayResult;
        }

        /// <summary>
        /// 在保存后台线程中将 Mat 转成待保存 Bitmap，并按需写入显示标注。
        /// </summary>
        /// <param name="task">后台保存任务。</param>
        /// <param name="stopwatch">当前任务计时器。</param>
        /// <param name="timing">输出分段耗时。</param>
        /// <returns>后台线程独占并负责释放的 Bitmap。</returns>
        private static Bitmap CreateBitmapForSave(SaveImageTask task, Stopwatch stopwatch, out SaveImageRenderTiming timing)
        {
            timing = new SaveImageRenderTiming();
            Bitmap bitmap = null;
            try
            {
                long beforeToBitmap = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                bitmap = task.SourceImage.ToBitmap();
                long afterToBitmap = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                timing.ToBitmapMs = afterToBitmap - beforeToBitmap;

                timing.HasDrawableDisplayResult = ParamFormImageSave.HasDrawableDisplayResult(task.DisplayResult);
                if (timing.HasDrawableDisplayResult)
                {
                    bitmap = ParamFormImageSave.EnsureDrawableBitmap(bitmap);
                    long afterEnsureDrawable = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                    timing.EnsureDrawableMs = afterEnsureDrawable - afterToBitmap;
                    ShowImageControl.DrawDisplayResultToBitmap(bitmap, task.DisplayResult);
                    long afterDrawDisplayResult = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(stopwatch);
                    timing.DrawDisplayResultMs = afterDrawDisplayResult - afterEnsureDrawable;
                    timing.TotalMs = afterDrawDisplayResult;
                }
                else
                {
                    timing.TotalMs = afterToBitmap;
                }

                Bitmap result = bitmap;
                bitmap = null;
                return result;
            }
            finally
            {
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// 在保存任务离开主队列前登记活动消费者，关闭空闲判断的认领竞态窗口。
        /// </summary>
        private void RegisterActiveWorker()
        {
            int activeWorkerCount = Interlocked.Increment(ref _activeWorkerCount);
            UpdateMaximum(ref _peakActiveWorkerCount, activeWorkerCount);
        }

        /// <summary>
        /// 低频记录磁盘保护事件，首条和每累计100条保留一次，避免磁盘故障反向刷爆日志。
        /// </summary>
        /// <param name="eventCount">当前事件累计数量。</param>
        /// <param name="message">不包含累计次数的日志正文。</param>
        private static void LogStorageGuardEvent(long eventCount, string message)
        {
            if (eventCount == 1 || eventCount % 100 == 0)
                TryWriteLog(MsgLevel.Warn, $"{message}；累计={eventCount}。", true);
        }

        /// <summary>
        /// 将高精度计时器时间戳转换为向上取整的毫秒数，确保亚毫秒写入不会误记为负数。
        /// </summary>
        /// <param name="startedTimestamp">操作开始时的Stopwatch时间戳。</param>
        /// <returns>非负的经过毫秒数。</returns>
        private static long GetElapsedMilliseconds(long startedTimestamp)
        {
            long elapsedTicks = Math.Max(0L, Stopwatch.GetTimestamp() - startedTimestamp);
            double elapsedMilliseconds = elapsedTicks * 1000D / Stopwatch.Frequency;
            return Math.Max(0L, (long)Math.Ceiling(elapsedMilliseconds));
        }

        /// <summary>
        /// 在保存专用工作线程上同步等待CPU许可；独立测试宿主未提供调度器时保持旧行为。
        /// </summary>
        /// <param name="workloadKind">图像转换或JPEG编码类型。</param>
        /// <returns>需要释放的CPU许可；未配置调度器时返回null。</returns>
        private ICpuWorkLease AcquireCpuWorkLease(CpuWorkloadKind workloadKind)
        {
            CancellationToken cancellationToken = _shutdownCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            return _cpuWorkScheduler == null
                ? null
                : _cpuWorkScheduler.AcquireAsync(workloadKind, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// JPEG编码后的托管缓冲区；同一图片保存到多个目录时复用同一份编码结果。
        /// </summary>
        private sealed class EncodedImageBuffer
        {
            /// <summary>包含JPEG内容的缓冲区。</summary>
            public byte[] Buffer { get; private set; }

            /// <summary>缓冲区中的有效JPEG字节数。</summary>
            public int Length { get; private set; }

            /// <summary>创建编码结果。</summary>
            public EncodedImageBuffer(byte[] buffer, int length)
            {
                Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                if (length <= 0 || length > buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(length), "JPEG有效字节数必须位于缓冲区范围内。");
                Length = length;
            }
        }

        /// <summary>
        /// 把Bitmap编码到内存JPEG；调用方只在持有图片编码CPU许可时调用。
        /// </summary>
        /// <param name="bitmap">待编码位图。</param>
        /// <param name="isCompress">是否使用指定JPEG质量。</param>
        /// <param name="compressValue">JPEG质量。</param>
        /// <param name="memoryStream">当前固定保存线程独占并复用的编码缓冲区。</param>
        /// <returns>可供多个保存目录复用的JPEG字节。</returns>
        private EncodedImageBuffer EncodeBitmapToJpeg(
            Bitmap bitmap,
            bool isCompress,
            long compressValue,
            MemoryStream memoryStream)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));
            if (memoryStream == null)
                throw new ArgumentNullException(nameof(memoryStream));

            memoryStream.Position = 0L;
            memoryStream.SetLength(0L);
            if (isCompress)
            {
                using (EncoderParameters encoderParams = new EncoderParameters(1))
                using (EncoderParameter qualityParameter = new EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality,
                    compressValue))
                {
                    encoderParams.Param[0] = qualityParameter;
                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                    if (jpegCodec == null)
                        throw new InvalidOperationException("当前系统没有可用的JPEG编码器。");
                    bitmap.Save(memoryStream, jpegCodec, encoderParams);
                }
            }
            else
            {
                bitmap.Save(memoryStream, ImageFormat.Jpeg);
            }

            return new EncodedImageBuffer(
                memoryStream.GetBuffer(),
                checked((int)memoryStream.Length));
        }

        /// <summary>
        /// 把已经编码的JPEG写入独占临时文件，再原子移动到唯一文件名。
        /// </summary>
        /// <param name="encodedImage">已经完成CPU编码的JPEG缓冲区。</param>
        /// <param name="targetDirectory">目标目录。</param>
        /// <param name="imageName">不含扩展名的基础图片名。</param>
        /// <returns>实际写入的完整文件路径。</returns>
        private static string SaveEncodedImage(
            EncodedImageBuffer encodedImage,
            string targetDirectory,
            string imageName)
        {
            if (encodedImage == null)
                throw new ArgumentNullException(nameof(encodedImage));

            string temporaryPath = Path.Combine(
                targetDirectory,
                $".{Guid.NewGuid():N}.saving");
            try
            {
                using (FileStream outputStream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    outputStream.Write(encodedImage.Buffer, 0, encodedImage.Length);
                }

                for (int suffix = 0; suffix < int.MaxValue; suffix++)
                {
                    string candidateName = suffix == 0 ? imageName : $"{imageName}_{suffix}";
                    string imagePath = Path.Combine(targetDirectory, candidateName + ".jpg");
                    try
                    {
                        File.Move(temporaryPath, imagePath);
                        temporaryPath = null;
                        return imagePath;
                    }
                    catch (IOException)
                    {
                        if (File.Exists(imagePath))
                            continue;
                        throw;
                    }
                }

                throw new IOException("无法为保存图片生成唯一文件名。");
            }
            finally
            {
                if (!TryDeleteIncompleteFile(temporaryPath))
                {
                    TryWriteLog(
                        MsgLevel.Warn,
                        $"清理图像保存临时文件失败，磁盘上可能残留.saving文件：{temporaryPath}",
                        true);
                }
            }
        }

        /// <summary>
        /// 尽力删除保存失败后留下的不完整文件，清理失败不覆盖原始写盘异常。
        /// </summary>
        /// <param name="imagePath">不完整文件路径。</param>
        private static bool TryDeleteIncompleteFile(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return true;

            try
            {
                if (File.Exists(imagePath))
                    File.Delete(imagePath);
                return !File.Exists(imagePath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 释放保存任务并隔离第三方租约释放异常，保证后台消费者继续工作。
        /// </summary>
        /// <param name="task">需要释放的保存任务。</param>
        /// <param name="stage">用于日志定位的释放阶段。</param>
        /// <returns>释放过程没有抛出异常返回true。</returns>
        private static bool DisposeSaveTaskNoThrow(SaveImageTask task, string stage)
        {
            try
            {
                task?.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                TryWriteLog(MsgLevel.Exception, $"{stage}释放图像租约失败：{ex.Message}", true);
                return false;
            }
        }

        /// <summary>
        /// 尽力写入日志，日志系统异常不得终止保存消费者或资源回收线程。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志正文。</param>
        /// <param name="showLog">是否同步到界面日志。</param>
        private static void TryWriteLog(MsgLevel level, string message, bool showLog)
        {
            try
            {
                LogHelper.AddLog(level, message, showLog);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 查找指定MIME类型的系统图像编码器。
        /// </summary>
        /// <param name="mimeType">编码器MIME类型。</param>
        /// <returns>找到的编码器；不存在时返回null。</returns>
        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        /// <summary>
        /// 取消全部待处理任务并等待正在写盘的任务在限定时间内退出。
        /// </summary>
        /// <returns>全部工作任务已退出返回 true；等待超时返回 false。</returns>
        public bool StopProcessing()
        {
            lock (_stopSync)
            {
                if (IsFullyStopped)
                {
                    DisposeInfrastructureNoThrow();
                    return true;
                }

                Stopwatch shutdownStopwatch = Stopwatch.StartNew();
                int shutdownTimeoutMilliseconds = IsIdle
                    ? Math.Max(_shutdownTimeoutMs, IdleWorkerShutdownGraceMilliseconds)
                    : _shutdownTimeoutMs;
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                    _imageQueue.CompleteAdding();
                int cancellationGraceMilliseconds = Math.Min(500, shutdownTimeoutMilliseconds);
                bool saveWorkersStopped = WaitForWorkers(
                    Math.Max(0, shutdownTimeoutMilliseconds - cancellationGraceMilliseconds));
                int disposedPendingCount = 0;
                if (!saveWorkersStopped)
                {
                    TryCancelShutdownNoThrow();
                    disposedPendingCount = _imageQueue.CompleteAndDisposePending();
                    saveWorkersStopped = WaitForWorkers(
                        GetRemainingShutdownMilliseconds(shutdownStopwatch, shutdownTimeoutMilliseconds));
                }
                Volatile.Write(ref _retiredTaskWorkerAccepting, 0);
                _retiredTaskSignal.Set();
                bool releaseWorkerStopped = WaitForRetiredTaskWorker(
                    GetRemainingShutdownMilliseconds(shutdownStopwatch, shutdownTimeoutMilliseconds));
                bool allWorkersStopped = saveWorkersStopped && releaseWorkerStopped;
                LogDiagnosticsSummaryOnce(disposedPendingCount, allWorkersStopped);
                if (PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug))
                {
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【内存诊断-保存队列停止】工作池({_ownerText}) 停止保存队列；已释放待保存图片={disposedPendingCount}；活动任务={ActiveWorkerCount}；工作任务已退出={allWorkersStopped}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                        true);
                }

                if (!allWorkersStopped)
                {
                    TryWriteLog(
                        MsgLevel.Warn,
                        $"保存工作池停止等待超过{_shutdownTimeoutMs}ms；仍在写盘的任务={ActiveWorkerCount}；待回收淘汰任务={Volatile.Read(ref _retiredTaskBacklogCount)}，任务完成后会自行释放图像资源。",
                        true);
                }
                else
                {
                    DisposeInfrastructureNoThrow();
                }

                return allWorkersStopped;
            }
        }

        /// <summary>
        /// 每个工作池生命周期只写一次文件级汇总，Debug关闭时也保留低频验收证据但不刷新日志窗口。
        /// </summary>
        /// <param name="disposedPendingCount">停止超时后直接释放的等待任务数。</param>
        /// <param name="allWorkersStopped">保存和淘汰回收线程是否全部退出。</param>
        private void LogDiagnosticsSummaryOnce(int disposedPendingCount, bool allWorkersStopped)
        {
            if (Interlocked.Exchange(ref _summaryLogged, 1) == 1)
                return;

            ImageSaveQueueDiagnosticsSnapshot snapshot = GetDiagnosticsSnapshot();
            TryWriteLog(
                MsgLevel.Info,
                $"【保存队列汇总】工作池={_ownerText}；{snapshot.ToLogText()}；停止释放等待={disposedPendingCount}；全部线程退出={allWorkersStopped}",
                false);
        }

        /// <summary>
        /// 在全部后台线程退出后释放队列和等待句柄；重复调用安全。
        /// </summary>
        private void DisposeInfrastructureNoThrow()
        {
            if (Interlocked.Exchange(ref _infrastructureDisposed, 1) == 1)
                return;

            try
            {
                _imageQueue.Dispose();
            }
            catch (Exception ex)
            {
                TryWriteLog(MsgLevel.Warn, $"释放保存队列基础资源失败：{ex.Message}", true);
            }

            try
            {
                _retiredTaskSignal.Dispose();
            }
            catch (Exception ex)
            {
                TryWriteLog(MsgLevel.Warn, $"释放保存回收线程等待句柄失败：{ex.Message}", true);
            }

            TryCancelShutdownNoThrow();
            try
            {
                _shutdownCancellationSource.Dispose();
            }
            catch (Exception ex)
            {
                TryWriteLog(MsgLevel.Warn, $"释放保存停止令牌失败：{ex.Message}", true);
            }
        }

        /// <summary>取消保存工作线程的CPU许可等待；重复调用或释放竞态不向外抛出。</summary>
        private void TryCancelShutdownNoThrow()
        {
            try
            {
                _shutdownCancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (AggregateException ex)
            {
                TryWriteLog(
                    MsgLevel.Warn,
                    $"取消保存CPU许可等待时有回调异常：{ex.Flatten().InnerException?.Message ?? ex.Message}",
                    true);
            }
        }

        /// <summary>
        /// 取消指定保存节点尚未开始处理的任务，并释放相应图像租约。
        /// </summary>
        /// <param name="ownerKey">保存节点唯一标识。</param>
        /// <returns>取消并释放的任务数量。</returns>
        public int CancelOwner(string ownerKey)
        {
            return _imageQueue.CancelOwner(ownerKey);
        }

        /// <summary>
        /// 判断现有工作池参数是否与新的自动资源档案一致。
        /// </summary>
        /// <param name="workerCount">保存工作线程数量。</param>
        /// <param name="capacity">队列任务容量。</param>
        /// <param name="memoryBudgetBytes">队列字节预算。</param>
        /// <param name="lowSpaceThresholdMb">普通图片停止保存水位。</param>
        /// <param name="criticalSpaceThresholdMb">全部图片停止保存水位。</param>
        /// <param name="slowWriteThresholdMs">单路径慢写阈值。</param>
        /// <param name="probeIntervalMs">磁盘空间探测缓存时间。</param>
        /// <param name="shutdownTimeoutMs">工作池停止排空等待毫秒数。</param>
        /// <returns>工作池和磁盘保护关键边界均一致返回 true。</returns>
        public bool ConfigurationMatches(
            int workerCount,
            int capacity,
            long memoryBudgetBytes,
            int lowSpaceThresholdMb,
            int criticalSpaceThresholdMb,
            int slowWriteThresholdMs,
            int probeIntervalMs,
            int shutdownTimeoutMs)
        {
            return WorkerCount == workerCount &&
                Capacity == capacity &&
                MemoryBudgetBytes == memoryBudgetBytes &&
                _shutdownTimeoutMs == shutdownTimeoutMs &&
                _storageGuard.LowSpaceThresholdMb == lowSpaceThresholdMb &&
                _storageGuard.CriticalSpaceThresholdMb == criticalSpaceThresholdMb &&
                _storageGuard.SlowWriteThresholdMs == slowWriteThresholdMs &&
                _storageGuard.ProbeIntervalMs == probeIntervalMs;
        }

        /// <summary>
        /// 在限定时间内等待全部后台工作任务退出。
        /// </summary>
        /// <returns>全部退出返回 true；等待超时或任务异常返回 false。</returns>
        private bool WaitForWorkers(int timeoutMilliseconds)
        {
            Task[] workers = _workerTasks;
            if (workers == null || workers.Length == 0)
                return true;

            try
            {
                return Task.WaitAll(workers, timeoutMilliseconds);
            }
            catch (AggregateException ex)
            {
                TryWriteLog(
                    MsgLevel.Exception,
                    $"保存工作池退出异常：{ex.Flatten().InnerException?.Message ?? ex.Message}",
                    true);
                return false;
            }
        }

        /// <summary>
        /// 在剩余停止预算内等待淘汰任务专用回收线程退出。
        /// </summary>
        /// <param name="timeoutMilliseconds">剩余等待毫秒数。</param>
        /// <returns>回收线程已经退出返回true。</returns>
        private bool WaitForRetiredTaskWorker(int timeoutMilliseconds)
        {
            Task worker = _retiredTaskWorker;
            if (worker == null)
                return true;

            try
            {
                return worker.Wait(timeoutMilliseconds);
            }
            catch (AggregateException ex)
            {
                TryWriteLog(
                    MsgLevel.Exception,
                    $"保存淘汰资源回收线程退出异常：{ex.Flatten().InnerException?.Message ?? ex.Message}",
                    true);
                return false;
            }
        }

        /// <summary>
        /// 计算本次停止时限扣除已用时间后的剩余毫秒数。
        /// </summary>
        /// <param name="stopwatch">停止阶段计时器。</param>
        /// <param name="shutdownTimeoutMilliseconds">本次停止允许使用的总毫秒数。</param>
        /// <returns>不小于零的剩余等待毫秒数。</returns>
        private static int GetRemainingShutdownMilliseconds(
            Stopwatch stopwatch,
            int shutdownTimeoutMilliseconds)
        {
            return Math.Max(
                0,
                shutdownTimeoutMilliseconds - (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds));
        }

        /// <summary>
        /// 判断全部保存消费者与淘汰资源回收线程是否已经完成。
        /// </summary>
        /// <returns>全部后台任务均完成返回true。</returns>
        private bool AreAllWorkersCompleted()
        {
            Task[] workers = _workerTasks;
            if (workers != null)
            {
                foreach (Task worker in workers)
                {
                    if (worker != null && !worker.IsCompleted)
                        return false;
                }
            }

            return _retiredTaskWorker == null || _retiredTaskWorker.IsCompleted;
        }

        /// <summary>
        /// 以无锁方式更新整数峰值。
        /// </summary>
        /// <param name="target">需要更新的峰值字段。</param>
        /// <param name="value">本次观察值。</param>
        private static void UpdateMaximum(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }

        /// <summary>
        /// 以无锁方式更新长整型峰值。
        /// </summary>
        /// <param name="target">需要更新的峰值字段。</param>
        /// <param name="value">本次观察值。</param>
        private static void UpdateMaximum(ref long target, long value)
        {
            long current;
            do
            {
                current = Interlocked.Read(ref target);
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
