using Logger;
using OpenCvSharp.Extensions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.ImageViewer;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{

    public partial class NodeImageShow : NodeBase
    {
        public static event EventHandler<ImageShowPamra> ImageShowChanged;
        public static event Action<Process, string> ImageShowWindowNameChanged;

        public NodeImageShow(int id, string nodeName, Process process, NodeType nodeType) : base(id, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageShow();
            Result = new NodeResultImageShow();
            ParamForm.SetNodeBelong(this);

            //if (ParamForm is ParamFormImageShow form)
            //{
            //    if (ParamForm.Params is NodeParamImageShow param)
            //    {
                    
            //    }
            //}
            
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            if(ParamForm is ParamFormImageShow form)
            {
                if(ParamForm.Params is NodeParamImageShow param)
                {
                    try
                    {
                        param.WindowName = FrmSingleImage.NormalizeWindowKey(param.WindowName);
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        Stopwatch performanceStopwatch = Stopwatch.StartNew();
                        OutputImage outputImage = form.GetOutputImage();
                        long afterGetOutput = performanceStopwatch.ElapsedMilliseconds;
                        OpenCvSharp.Mat firstMat = outputImage?.Bitmaps != null && outputImage.Bitmaps.Count > 0 ? outputImage.Bitmaps[0] : null;
                        string imageInfo = GetMatDiagnosticText(firstMat);
                        Bitmap image = firstMat.ToBitmap();
                        long afterToBitmap = performanceStopwatch.ElapsedMilliseconds;
                        ImageShowChanged?.Invoke(this, new ImageShowPamra(param.WindowName, image, outputImage.DisplayResult));
                        long afterImageEvent = performanceStopwatch.ElapsedMilliseconds;
                        ImageShowWindowNameChanged?.Invoke(Process, param.WindowName);
                        long afterWindowEvent = performanceStopwatch.ElapsedMilliseconds;
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        long afterSetResult = performanceStopwatch.ElapsedMilliseconds;
                        ((NodeResultImageShow)Result).RunTime = time;
                        if (showLog)
                        {
                            LogHelper.AddLog(
                                MsgLevel.Info,
                                $"【性能诊断-图像显示】节点({ID}.{NodeName}) 图像={imageInfo}；订阅读取={afterGetOutput}ms；ToBitmap={afterToBitmap - afterGetOutput}ms；图像事件={afterImageEvent - afterToBitmap}ms；窗口事件={afterWindowEvent - afterImageEvent}ms；状态={afterSetResult - afterWindowEvent}ms；节点总耗时={afterSetResult}ms；状态耗时={time}ms",
                                true);
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        }
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
        /// 生成图像显示性能诊断所需的Mat尺寸、通道和估算大小文本。
        /// </summary>
        /// <param name="mat">待显示图像。</param>
        /// <returns>图像诊断文本。</returns>
        private static string GetMatDiagnosticText(OpenCvSharp.Mat mat)
        {
            if (mat == null)
                return "空";

            try
            {
                if (mat.Empty())
                    return "空Mat";

                int channels = Math.Max(1, mat.Channels());
                double megaBytes = mat.Width * mat.Height * channels / 1024D / 1024D;
                return $"{mat.Width}x{mat.Height}x{channels}，约{megaBytes:F2}MB";
            }
            catch (Exception ex)
            {
                return "读取失败：" + ex.Message;
            }
        }
    }
}
