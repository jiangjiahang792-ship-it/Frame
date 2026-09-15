using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._1_Acquisition.CameraExposureGain
{
    /// <summary>
    /// 相机曝光增益设置节点，负责在流程中写入指定相机的曝光和增益。
    /// </summary>
    public class NodeCameraExposureGain : NodeBase
    {
        /// <summary>
        /// 初始化相机曝光增益设置节点。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeCameraExposureGain(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormCameraExposureGain(nodeName, process);
            Result = new NodeResultCameraExposureGain();
        }

        /// <summary>
        /// 运行节点并把界面保存的曝光、增益写入相机。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回标志。</returns>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }

            if (!(ParamForm.Params is NodeParamCameraExposureGain param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                await base.CheckTokenCancel(token);

                if (param.Camera == null)
                {
                    throw new Exception("相机对象无效！");
                }

                if (!param.Camera.IsOpen)
                {
                    throw new Exception($"相机({param.CameraName})尚未连接！");
                }

                param.Camera.SetExposureTime(param.ExposureTime);
                param.Camera.SetGain(param.Gain);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if (showLog)
                {
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！曝光={param.ExposureTime}，增益={param.Gain}，耗时({time} ms)", true);
                }
                await Task.CompletedTask;
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
}
