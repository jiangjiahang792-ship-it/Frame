using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;

namespace TDJS_Vision.Node._7_ResultProcessing.NamedImageSave
{
    /// <summary>按用户逐行配置的目录和文件名冻结保存任务，复用共享后台工作池。</summary>
    public sealed class NodeNamedImageSave : NodeBase, INodeSubscriptionDependencyProvider
    {
        /// <summary>保护任务所有权与节点释放的互斥锁。</summary>
        private readonly object _ownershipSync = new object();
        /// <summary>当前实例独立的队列所有者，避免同编号节点重新加载时互相取消。</summary>
        private readonly string _ownerKey = "命名存图." + Guid.NewGuid().ToString("N");
        /// <summary>是否已释放。</summary>
        private bool _disposed;

        /// <summary>创建命名存图节点、结果及设计器参数窗体。</summary>
        public NodeNamedImageSave(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            Result = new NodeResultNamedImageSave();
            ParamForm = new ParamFormNamedImageSave();
            ParamForm.SetNodeBelong(this);
        }

        /// <summary>向调度器声明目录、文件名和图像的所有订阅依赖。</summary>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds()
        {
            var param = ParamForm.Params as NodeParamNamedImageSave;
            var sources = (param?.Folders ?? new List<NamedImageValue>()).Select(row => row?.Source)
                .Concat((param?.FileNameParts ?? new List<NamedImageValue>()).Select(row => row?.Source))
                .Concat(new[] { param?.ImageSource, param?.FileName?.Source });
            return sources.Where(source => source != null && source.SourceNodeId > 0 && source.SourceNodeId != ID)
                .Select(source => source.SourceNodeId).Distinct().ToArray();
        }

        /// <summary>冻结订阅值和命名规则，接纳后才借用图像，不在流程线程编码或写盘。</summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime start = DateTime.Now;
            var result = new NodeResultNamedImageSave();
            Result = result;
            if (!Active)
            {
                SetRunResult(start, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            try
            {
                await CheckTokenCancel(token);
                var param = (ParamForm.Params as NodeParamNamedImageSave)?.Copy();
                NamedImagePathBuilder.Validate(param);
                var upstream = Process.GetUpstreamNodes(this).ToDictionary(node => node.ID);
                Func<NamedImageSource, object> read = source => NamedImageSubscriptions.Read(this, source, upstream, true);
                var image = read(param.ImageSource) as OutputImage;
                if (image == null) throw new InvalidOperationException("图像订阅未返回有效的输出图像。");
                NamedImagePath path = NamedImagePathBuilder.Build(param, read, start,
                    message => LogHelper.AddLog(MsgLevel.Exception, "命名存图命名异常：节点=" + ID + "." + NodeName + "；" + message, true));
                token.ThrowIfCancellationRequested();
                var task = new ImageQueueProcessor.SaveImageTask
                {
                    TargetDirectories = new List<string> { path.DirectoryPath },
                    ImageName = path.ImageName,
                    NeedCompress = true,
                    CompressValue = param.JpegQuality,
                    DisplayResult = image.DisplayResult,
                    TraceContext = PerformanceTraceContext.CreateIfEnabled(Process, ID, NodeName),
                    EstimatedBytes = image.GetRetainedImageBytesEstimateForAdmission(),
                    OwnerKey = _ownerKey
                };
                try
                {
                    lock (_ownershipSync)
                    {
                        if (_disposed) throw new ObjectDisposedException(nameof(NodeNamedImageSave));
                        Solution.Instance.RecordImageSaveResourceSample(task.EstimatedBytes);
                        var admission = Solution.Instance.GetOrCreateImageSaveQueueProcessor()
                            .EnqueueImage(task, () => image.AcquireSaveSnapshot());
                        if (!admission.Accepted) throw new InvalidOperationException("保存队列未接纳图像：" + admission.Status);
                        task = null; // 入队成功后由工作池负责释放租约。
                    }
                }
                finally { task?.Dispose(); }
                result.Enqueued = true;
                result.DirectoryPath = path.DirectoryPath;
                result.ImageName = path.ImageName;
                result.RunTime = SetRunResult(start, NodeStatus.Successful);
                if (showLog) LogHelper.AddLog(MsgLevel.Info, "节点(" + ID + "." + NodeName + ")图像已入队：" + path.DirectoryPath + "\\" + path.ImageName + ".jpg", true);
                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (OperationCanceledException)
            {
                SetRunResult(start, NodeStatus.Unexecuted);
                throw;
            }
            catch (Exception exception)
            {
                SetRunResult(start, NodeStatus.Failed);
                LogHelper.AddLog(MsgLevel.Exception, "命名存图失败：节点=" + ID + "." + NodeName + "；" + exception.Message, true);
                throw;
            }
        }

        /// <summary>释放节点时只取消自己尚未开始的任务，由工作池回收图像租约。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_ownershipSync)
                {
                    if (!_disposed)
                    {
                        _disposed = true;
                        Solution.Instance.CancelPendingImageSaveTasks(_ownerKey);
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
