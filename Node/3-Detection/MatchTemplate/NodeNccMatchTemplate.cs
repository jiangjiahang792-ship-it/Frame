using Logger;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    /// <summary>
    /// NCC模板匹配节点，强制使用Fastest_Image_Pattern_Matching demo一致的native MatchTool算法路径。
    /// </summary>
    public class NodeNccMatchTemplate : NodeMatchTemplate
    {
        /// <summary>
        /// 创建NCC模板匹配节点，并保留与原模板匹配一致的运行参数界面。
        /// </summary>
        public NodeNccMatchTemplate(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType, true, "NCC模板匹配")
        {
        }

        /// <summary>
        /// 节点运行，直接从订阅输出读取 Mat，保持 demo native 匹配算法不变并减少框架转换耗时。
        /// </summary>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            FastTemplateMatchResult matchResult = null;
            NodeResultMatchTemplate pendingResult = null;
            IImageResourceLease inputLease = null;
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

                var form = ParamForm as NodeParamFormMatchTemplate;
                var param = ParamForm.Params as NodeParamMatchTemplate;
                if (form == null || param == null)
                    throw new Exception("NCC模板匹配参数异常！");

                OpenCvSharp.Mat outputMat;
                OpenCvSharp.Mat outputGrayMat;
                OutputImage outputOwner;
                matchResult = form.MatchTemplateFromSubscribedMat(false, out outputMat, out outputGrayMat, out outputOwner, out inputLease);
                pendingResult = BuildResult(matchResult, param, "NCC模板匹配", outputMat, outputGrayMat, outputOwner);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                pendingResult.RunTime = time;
                Result = pendingResult;
                pendingResult = null;
                if (showLog && matchResult != null && matchResult.IsOk)
                {
                    double minScore = matchResult.Matches.Count == 0 ? 0 : matchResult.Matches.Min(m => m.Score) * 100.0;
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，当前结果最低匹配得分：{minScore:F2}, 匹配是否成功: {matchResult.IsOk}", true);
                }
                else if (showLog)
                {
                    int matchCount = matchResult == null ? 0 : matchResult.Matches.Count;
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})未匹配到有效模板结果！({time} ms，匹配数量：{matchCount})", true);
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
            finally
            {
                NodeResultResourceManager.Release(pendingResult);
                inputLease?.Dispose();
                matchResult?.OutputBitmap?.Dispose();
                if (matchResult != null)
                    matchResult.OutputBitmap = null;
            }
        }
    }
}
