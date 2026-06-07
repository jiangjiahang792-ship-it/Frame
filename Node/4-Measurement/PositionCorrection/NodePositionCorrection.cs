using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    public class NodePositionCorrection : NodeBase
    {
        public NodePositionCorrection(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormPositionCorrection();
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultPositionCorrection();
        }

        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                var form = ParamForm as NodeParamFormPositionCorrection;
                var param = ParamForm.Params as NodeParamPositionCorrection;
                if (form == null || param == null)
                    throw new Exception("位置修正参数异常。");

                PositionCorrectionInfo correctionInfo = form.BuildCorrectionInfo(param);
                NodeResultPositionCorrection nodeResult = BuildResult(correctionInfo);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})运行成功！({time} ms，X偏移:{nodeResult.DeltaX:F3}，Y偏移:{nodeResult.DeltaY:F3}，角度偏移:{nodeResult.DeltaAngle:F3})",
                        true);
                }

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        internal void PublishPreviewResult(PositionCorrectionInfo correctionInfo)
        {
            Result = BuildResult(correctionInfo);
        }

        internal static NodeResultPositionCorrection BuildResult(PositionCorrectionInfo correctionInfo)
        {
            PositionCorrectionHelper.EnsureValid(correctionInfo);
            return new NodeResultPositionCorrection
            {
                CorrectionInfo = correctionInfo,
                IsOk = true,
                CurrentX = MeasurementResultRounder.Round(correctionInfo.CurrentX),
                CurrentY = MeasurementResultRounder.Round(correctionInfo.CurrentY),
                CurrentAngle = MeasurementResultRounder.Round(correctionInfo.CurrentAngle),
                DeltaX = MeasurementResultRounder.Round(correctionInfo.DeltaX),
                DeltaY = MeasurementResultRounder.Round(correctionInfo.DeltaY),
                DeltaAngle = MeasurementResultRounder.Round(correctionInfo.DeltaAngle)
            };
        }
    }
}
