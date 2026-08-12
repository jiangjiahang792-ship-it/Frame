using Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    /// <summary>
    /// 把模板匹配的全部目标位姿转换为相对于第一目标基准的位置修正集合。
    /// </summary>
    public class NodePositionCorrection : NodeBase
    {
        /// <summary>
        /// 初始化位置修正节点。
        /// </summary>
        public NodePositionCorrection(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormPositionCorrection();
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultPositionCorrection();
        }

        /// <summary>
        /// 运行位置修正并输出与当前模板目标一一对应的修正集合。
        /// </summary>
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

                List<PositionCorrectionInfo> items = form.BuildCorrectionItems(param);
                NodeResultPositionCorrection nodeResult = BuildResult(items);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                {
                    LogHelper.AddLog(
                        nodeResult.IsOk ? MsgLevel.Info : MsgLevel.Warn,
                        $"节点({ID}.{NodeName})运行成功！({time} ms，修正目标数量：{nodeResult.TargetCount}，总体：{(nodeResult.IsOk ? "OK" : "NG")})",
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

        /// <summary>
        /// 默认复制当前位姿列表中的第一目标作为固定基准。
        /// </summary>
        /// <param name="poses">当前模板匹配目标位姿。</param>
        /// <returns>独立保存的第一目标基准位姿。</returns>
        internal static TemplateMatchPose CreateBaseline(IReadOnlyList<TemplateMatchPose> poses)
        {
            if (poses == null || poses.Count == 0 || poses[0] == null || !poses[0].IsValid)
                throw new Exception("模板位姿列表没有有效的第一目标，无法创建基准。");

            return poses[0].Clone();
        }

        /// <summary>
        /// 根据固定基准和本次全部目标位姿创建修正集合。
        /// </summary>
        /// <param name="basePose">创建基准时保存的第一目标位姿。</param>
        /// <param name="poses">本次模板匹配的全部目标位姿。</param>
        /// <returns>与有效位姿一一对应的位置修正集合。</returns>
        internal static List<PositionCorrectionInfo> BuildCorrectionItems(
            TemplateMatchPose basePose,
            IReadOnlyList<TemplateMatchPose> poses)
        {
            if (basePose == null || !basePose.IsValid)
                throw new Exception("位置修正基准无效，请重新创建基准。");
            if (poses == null || poses.Count == 0)
                return new List<PositionCorrectionInfo>();

            var items = new List<PositionCorrectionInfo>(poses.Count);
            for (int i = 0; i < poses.Count; i++)
            {
                TemplateMatchPose pose = poses[i];
                if (pose == null || !pose.IsValid)
                    continue;

                items.Add(PositionCorrectionInfo.FromPoses(basePose, pose));
            }

            return items;
        }

        /// <summary>
        /// 发布参数窗口的预览结果。
        /// </summary>
        internal void PublishPreviewResult(IReadOnlyList<PositionCorrectionInfo> items)
        {
            Result = BuildResult(items);
        }

        /// <summary>
        /// 创建位置修正节点结果，并保留第一目标摘要以兼容旧工具。
        /// </summary>
        internal static NodeResultPositionCorrection BuildResult(IReadOnlyList<PositionCorrectionInfo> items)
        {
            var resultItems = new List<PositionCorrectionInfo>(items == null ? 0 : items.Count);
            if (items == null || items.Count == 0)
            {
                return new NodeResultPositionCorrection
                {
                    Items = resultItems,
                    IsValid = false,
                    CorrectionInfo = new PositionCorrectionInfo(),
                    TargetCount = 0,
                    IsOk = false,
                    JudgeOk = false
                };
            }

            for (int i = 0; i < items.Count; i++)
            {
                PositionCorrectionHelper.EnsureValid(items[i]);
                resultItems.Add(items[i]);
            }

            if (resultItems.Count == 0)
            {
                return new NodeResultPositionCorrection
                {
                    Items = resultItems,
                    IsValid = false,
                    CorrectionInfo = new PositionCorrectionInfo(),
                    TargetCount = 0,
                    IsOk = false,
                    JudgeOk = false
                };
            }

            PositionCorrectionInfo first = resultItems[0];
            return new NodeResultPositionCorrection
            {
                Items = resultItems,
                BasePose = new TemplateMatchPose
                {
                    TargetIndex = 1,
                    CenterX = first.BaseX,
                    CenterY = first.BaseY,
                    Angle = first.BaseAngle,
                    ScaleX = first.BaseScaleX,
                    ScaleY = first.BaseScaleY,
                    Width = first.TargetWidth,
                    Height = first.TargetHeight,
                    IsValid = true
                },
                IsValid = true,
                CorrectionInfo = first,
                TargetCount = resultItems.Count,
                IsOk = true,
                JudgeOk = true,
                CurrentX = MeasurementResultRounder.Round(first.CurrentX),
                CurrentY = MeasurementResultRounder.Round(first.CurrentY),
                CurrentAngle = MeasurementResultRounder.Round(first.CurrentAngle),
                DeltaX = MeasurementResultRounder.Round(first.DeltaX),
                DeltaY = MeasurementResultRounder.Round(first.DeltaY),
                DeltaAngle = MeasurementResultRounder.Round(first.DeltaAngle)
            };
        }
    }
}
