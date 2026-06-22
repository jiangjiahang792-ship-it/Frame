using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Forms.ImageViewer;

namespace TDJS_Vision.Node._1_Acquisition.ImageShow3D
{
    /// <summary>
    /// 3D 图像显示节点，负责将上游 3D 帧推送到 3D 图像窗口。
    /// </summary>
    public class NodeImageShow3D : NodeBase
    {
        /// <summary>
        /// 3D 图像显示事件。
        /// </summary>
        public static event EventHandler<Image3DShowParam> Image3DShowChanged;

        /// <summary>
        /// 3D 图像显示窗口名称变化事件。
        /// </summary>
        public static event Action<Process, string> Image3DShowWindowNameChanged;

        /// <summary>
        /// 创建 3D 图像显示节点。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeImageShow3D(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageShow3D();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageShow3D();
        }

        /// <summary>
        /// 运行 3D 图像显示节点。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出日志。</param>
        /// <returns>节点运行控制结果。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (!(ParamForm.Params is NodeParamImageShow3D param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                CheckTokenCancel(token);

                ParamFormImageShow3D form = ParamForm as ParamFormImageShow3D;
                if (form == null)
                    throw new InvalidOperationException("3D图像显示参数窗体类型错误。");

                param.WindowName = FrmSingle3DImage.NormalizeWindowKey(param.WindowName);
                Camera3DFrameData frameData = form.GetFrameData();
                if (frameData == null)
                    throw new Exception("未获取到3D帧数据！");

                FrmImageViewer.Show3DWindow(param.WindowName);
                Image3DShowChanged?.Invoke(this, new Image3DShowParam(param.WindowName, frameData));
                Image3DShowWindowNameChanged?.Invoke(Process, param.WindowName);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                ((NodeResultImageShow3D)Result).RunTime = time;
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！3D窗口={FrmSingle3DImage.GetWindowDisplayName(param.WindowName)}，帧号={frameData.FrameNumber}，耗时={time} ms", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 3D 图像显示事件参数。
    /// </summary>
    public class Image3DShowParam
    {
        /// <summary>
        /// 创建 3D 图像显示事件参数。
        /// </summary>
        /// <param name="winName">窗口名称。</param>
        /// <param name="frameData">3D 帧数据。</param>
        public Image3DShowParam(string winName, Camera3DFrameData frameData)
        {
            WinName = winName;
            FrameData = frameData;
        }

        /// <summary>
        /// 窗口名称。
        /// </summary>
        public string WinName { get; }

        /// <summary>
        /// 3D 帧数据。
        /// </summary>
        public Camera3DFrameData FrameData { get; }
    }
}
