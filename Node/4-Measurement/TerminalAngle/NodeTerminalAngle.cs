using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>端子角度节点；算法服务与 UI 分离，运行只读取已保存参数。</summary>
    public sealed class NodeTerminalAngle : NodeBase
    {
        /// <summary>可替换的测量算法策略。</summary>
        private readonly ITerminalAngleMeasurer _measurer;
        /// <summary>框架工厂使用的默认构造入口。</summary>
        public NodeTerminalAngle(int nodeId, string nodeName, Process process, NodeType nodeType)
            : this(nodeId, nodeName, process, nodeType, new OpenCvTerminalAngleMeasurer()) { }
        /// <summary>注入算法策略，便于测试或未来替换实现。</summary>
        public NodeTerminalAngle(int nodeId, string nodeName, Process process, NodeType nodeType, ITerminalAngleMeasurer measurer)
            : base(nodeId, nodeName, process, nodeType)
        {
            _measurer = measurer ?? throw new ArgumentNullException("measurer");
            ParamForm = new NodeParamFormTerminalAngle(process, this, _measurer);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultTerminalAngle();
        }
        /// <summary>发布本轮角度与借用图像；无目标输出空角度，异常与取消清除旧结果。</summary>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            var timer = Stopwatch.StartNew();
            Result = new NodeResultTerminalAngle();
            if (!Active)
            {
                Result.RunTime = SetRunResult(timer, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }
            try
            {
                token.ThrowIfCancellationRequested();
                var parameters = ParamForm.Params as NodeParamTerminalAngle;
                if (parameters == null) throw new InvalidOperationException("请先绘制两个 ROI 并保存参数。");
                var settings = parameters.Copy();
                var form = (NodeParamFormTerminalAngle)ParamForm;
                OutputImage input = form.GetInputImage();
                var corrections = form.GetCorrections(settings, true);
                using (input.AcquireLease())
                {
                    var source = MeasurementNodeHelper.GetReadOnlyPreviewMat(input);
                    var items = TerminalAnglePositionCorrection.MeasureAll(_measurer, source, settings, corrections, token);
                    token.ThrowIfCancellationRequested();
                    var display = TerminalAngleDisplay.BuildAll(items);
                    // 通用单图包装器在缺灰度时会生成全图缓存；此节点仅借用已有图像，不触发该分配。
                    var gray = input.GrayImg;
                    var output = new OutputImage
                    {
                        SrcImg = source, Bitmaps = new System.Collections.Generic.List<OpenCvSharp.Mat> { source },
                        GrayImg = OutputImage.HasValidImage(gray) && gray.Width == source.Width && gray.Height == source.Height ? gray : null
                    };
                    try { output.TakeDependency(input); }
                    catch { output.Dispose(); throw; }
                    output.DisplayResult = display;
                    Result = new NodeResultTerminalAngle
                    {
                        Items = items, Success = items.Count > 0 && items.All(item => item.IsOk), Angle = items.FirstOrDefault()?.Angle,
                        Message = items.Count == 0 ? "没有有效定位目标" : string.Join("；", items.Where(item => !item.IsOk).Select(item => "端子" + item.TargetIndex + "：" + item.ErrorMessage)),
                        AlgorithmMilliseconds = items.Sum(item => item.AlgorithmMilliseconds), DisplayResult = display, OutputImage = output
                    };
                }
                // 已成功执行本轮算法即发布新结果；无目标通过“测量成功”区分，不复用上轮数值。
                Result.RunTime = SetRunResult(timer, NodeStatus.Successful);
                if (showLog)
                {
                    var value = (NodeResultTerminalAngle)Result;
                    LogHelper.AddLog(value.Success ? MsgLevel.Info : MsgLevel.Warn,
                        string.Format("节点({0}.{1})端子角度：{2}，耗时 {3} ms。{4}", ID, NodeName,
                            value.Angle.HasValue ? value.Angle.Value.ToString("F3") + "°" : "无效", value.RunTime, value.Message), true);
                }
                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                Result = new NodeResultTerminalAngle { Message = "测量已取消" };
                Result.RunTime = SetRunResult(timer, NodeStatus.Unexecuted);
                throw;
            }
            catch (Exception exception)
            {
                Result = new NodeResultTerminalAngle { Message = exception.Message };
                Result.RunTime = SetRunResult(timer, NodeStatus.Failed);
                LogHelper.AddLog(MsgLevel.Fatal, "端子角度节点运行失败：" + exception.Message, true);
                throw;
            }
        }
    }
}
