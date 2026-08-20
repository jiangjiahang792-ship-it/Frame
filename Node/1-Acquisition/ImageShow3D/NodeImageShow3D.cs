using Logger;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._1_Acquisition.ImageShow3D
{
    /// <summary>
    /// 3D 图像显示节点，负责将上游 3D 深度预览图推送到普通图像窗口。
    /// </summary>
    public class NodeImageShow3D : NodeBase
    {
        /// <summary>
        /// 旧版 3D 图像显示事件，保留给历史独立 3D 窗口代码编译兼容。
        /// </summary>
#pragma warning disable CS0067
        public static event EventHandler<Image3DShowParam> Image3DShowChanged;

        /// <summary>
        /// 旧版 3D 图像显示窗口名称变化事件，保留给历史独立 3D 窗口代码编译兼容。
        /// </summary>
        public static event Action<Process, string> Image3DShowWindowNameChanged;
#pragma warning restore CS0067

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
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
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
                await CheckTokenCancel(token);

                ParamFormImageShow3D form = ParamForm as ParamFormImageShow3D;
                if (form == null)
                    throw new InvalidOperationException("3D图像显示参数窗体类型错误。");

                param.WindowName = ParamFormImageShow3D.NormalizeImageWindowKey(param.WindowName);
                OutputImage outputImage = form.GetDepthOutputImage();
                OpenCvSharp.Mat firstMat = outputImage?.Bitmaps != null && outputImage.Bitmaps.Count > 0 ? outputImage.Bitmaps[0] : null;
                if (firstMat == null || firstMat.Empty())
                    throw new Exception("未获取到可显示的深度预览图！");

                bool refreshGranted = NodeImageShow.TryAcquireWindowRefresh(
                    param.WindowName,
                    out int maximumFramesPerSecond);
                string displayStrategy;
                if (refreshGranted)
                {
                    Bitmap image;
                    using (ICpuWorkLease conversionLease = await Solution.Instance.AcquireCpuWorkAsync(
                        CpuWorkloadKind.ImageConversion,
                        token))
                    {
                        image = firstMat.ToBitmap();
                    }
                    bool bitmapClaimed = NodeImageShow.PublishImageShowChanged(
                        this,
                        param.WindowName,
                        image,
                        outputImage.DisplayResult);
                    if (bitmapClaimed)
                        NodeImageShow.PublishImageShowWindowNameChanged(Process, param.WindowName);
                    displayStrategy = bitmapClaimed ? "已发布" : "无人接管并释放";
                }
                else
                {
                    displayStrategy = $"刷新限速跳过({maximumFramesPerSecond}FPS)";
                }

                int time = SetRunResult(startTime, NodeStatus.Successful);
                ((NodeResultImageShow3D)Result).RunTime = time;
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！图像窗口={FrmSingleImage.GetWindowDisplayName(param.WindowName)}，显示策略={displayStrategy}，深度图={GetMatDiagnosticText(firstMat)}，耗时={time} ms", true);

                return new NodeReturn(NodeRunFlag.ContinueRun);
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

        /// <summary>
        /// 生成深度预览图诊断文本。
        /// </summary>
        /// <param name="mat">深度预览图。</param>
        /// <returns>尺寸和通道摘要。</returns>
        private static string GetMatDiagnosticText(OpenCvSharp.Mat mat)
        {
            if (mat == null)
                return "空";

            if (mat.Empty())
                return "空Mat";

            return $"{mat.Width}x{mat.Height}x{Math.Max(1, mat.Channels())}";
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
