using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeImageSource : NodeBase
    {
        /// <summary>
        /// 用于保存上次访问的图片索引
        /// </summary>
        public int LastIndex = 0;

        /// <summary>
        /// 当前图片索引
        /// </summary>
        private string _nextImagePath;

        /// <summary>
        /// 当前图片
        /// </summary>
        private Mat _mat;
        /// <summary>
        /// 当前图像源的一次性相机回调等待器。
        /// </summary>
        private readonly ICameraFrameAwaiter _cameraFrameAwaiter;

        /// <summary>
        /// 创建使用默认相机帧等待器的图像源节点。
        /// </summary>
        public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType)
            : this(nodeId, nodeName, process, nodeType, new CameraFrameAwaiter())
        {
        }

        /// <summary>
        /// 创建使用指定相机帧等待器的图像源节点，便于替换和测试回调协作实现。
        /// </summary>
        internal NodeImageSource(
            int nodeId,
            string nodeName,
            Process process,
            NodeType nodeType,
            ICameraFrameAwaiter cameraFrameAwaiter) : base(nodeId, nodeName, process, nodeType)
        {
            _cameraFrameAwaiter = cameraFrameAwaiter ?? throw new ArgumentNullException(nameof(cameraFrameAwaiter));
            ParamForm = new ParamFormImageSource(this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSource();
            NodeBase.NodeDeletedEvent += NodeImageSource_NodeDeletedEvent;
            Disposed += NodeImageSource_Disposed;
        }

        public string LoadTestImage(NodeParamImageSoucre param, NodeResultImageSource res)
        {
            if (!string.IsNullOrEmpty(param.ImagePath))
            {
                // 单图模式
                _mat = LoadImage(param.ImagePath);
                res.OutputImage = BuildOutputImage(_mat);
                return param.ImagePath;
            }

            // 多图模式
            if (param.ImagePaths == null || param.ImagePaths.Count == 0)
                throw new ArgumentException("未设置图像路径列表");

            string nextImagePath;

            if (param.IsAutoLoop)
            {
                // 自动循环模式：依次取下一张图像
                nextImagePath = param.ImagePaths[LastIndex];
                LastIndex = (LastIndex + 1) % param.ImagePaths.Count;
            }
            else
            {
                // 手动模式：重复使用上一张图像
                int index = LastIndex == 0 ? 0 : LastIndex - 1;
                nextImagePath = param.ImagePaths[index];
            }

            _mat = LoadImage(nextImagePath);
            res.OutputImage = BuildOutputImage(_mat);
            return nextImagePath;
        }

        // 封装图像加载逻辑
        private Mat LoadImage(string imagePath)
        {
            try
            {
                return Cv2.ImRead(imagePath, ImreadModes.Color);
            }
            catch (Exception ex)
            {
                throw new IOException($"无法加载图像：{imagePath}", ex);
            }
        }

        /// <summary>
        /// 处理相机回调图像。回调已经绑定到当前图像源节点，不需要再全局查找节点。
        /// </summary>
        /// <param name="bitmap">相机回调帧。</param>
        public void HandleCameraCallbackFrame(Bitmap bitmap)
        {
            if (bitmap == null)
                return;

            Mat callbackMat = null;
            try
            {
                callbackMat = bitmap.ToMat();
                LogHelper.AddLog(MsgLevel.Debug, $"流程【{Process?.ProcessName}】Bitmap转Mat完毕, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
                HandleCameraCallbackFrame(callbackMat);
                callbackMat = null;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"相机回调图像转换失败：{ex.Message}", true);
            }
            finally
            {
                callbackMat?.Dispose();
            }
        }

        /// <summary>
        /// 处理相机回调 Mat 图像，Mat 由当前方法接管生命周期，忙碌或异常时会立即释放。
        /// </summary>
        /// <param name="mat">相机回调 Mat 帧。</param>
        public void HandleCameraCallbackFrame(Mat mat)
        {
            if (mat == null || mat.Empty())
            {
                mat?.Dispose();
                return;
            }

            if (_cameraFrameAwaiter.TrySupplyFrame(mat))
            {
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"流程【{Process?.ProcessName}】图像源节点({ID}.{NodeName})已接收当前 RunId={Process?.CurrentRunId} 的相机回调帧。",
                    true);
                return;
            }

            mat.Dispose();
            PerformanceSpikeDiagnostics.LogIfEnabled(
                MsgLevel.Debug,
                () => $"流程【{Process?.ProcessName}】图像源节点({ID}.{NodeName})没有有效等待者，已丢弃迟到回调帧。",
                true);
        }

        /// <summary>
        /// 节点删除时取消尚未完成的相机回调等待。
        /// </summary>
        private void NodeImageSource_NodeDeletedEvent(object sender, NodeBase node)
        {
            if (!ReferenceEquals(node, this))
                return;

            _cameraFrameAwaiter.CancelPendingWait();
        }

        /// <summary>
        /// 节点释放时清理相机回调等待器和静态事件订阅。
        /// </summary>
        private void NodeImageSource_Disposed(object sender, EventArgs e)
        {
            NodeBase.NodeDeletedEvent -= NodeImageSource_NodeDeletedEvent;
            Disposed -= NodeImageSource_Disposed;
            _cameraFrameAwaiter.Dispose();
        }

        /// <summary>
        /// 构建图像源输出，统一提供原图、兼容旧节点的图像列表和灰度图缓存。
        /// </summary>
        private static OutputImage BuildOutputImage(Mat source)
        {
            return OutputImage.FromSingleImage(source);
        }

        /// <summary>
        /// 从方案共享变量读取图像，并转换为图像源节点统一输出。
        /// </summary>
        /// <param name="variableName">共享变量名称。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage LoadSharedVariableImage(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName) || variableName == "[未选择]")
                throw new Exception("共享变量名称未设置！");

            SharedVarValue sharedValue = Solution.Instance.SharedVariable.GetValue(variableName);
            object data = UnwrapSharedVariableData(sharedValue);
            OutputImage outputImage = ConvertSharedVariableToOutputImage(variableName, data);
            if (!HasRunnableOutputImage(outputImage))
                throw new Exception($"共享变量“{variableName}”中没有有效图像！");

            return outputImage;
        }

        /// <summary>
        /// 解除嵌套的共享变量值包装，兼容共享变量再次写入共享变量结果的场景。
        /// </summary>
        /// <param name="sharedValue">共享变量值包装对象。</param>
        /// <returns>实际数据对象。</returns>
        private static object UnwrapSharedVariableData(SharedVarValue sharedValue)
        {
            object data = sharedValue?.Data;
            while (data is SharedVarValue nestedValue)
            {
                data = nestedValue.Data;
            }

            return data;
        }

        /// <summary>
        /// 将共享变量中的常见图像类型转换为图像源输出。
        /// </summary>
        /// <param name="variableName">共享变量名称，用于异常提示。</param>
        /// <param name="data">共享变量数据。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage ConvertSharedVariableToOutputImage(string variableName, object data)
        {
            if (data is OutputImage outputImage)
                return NormalizeOutputImage(outputImage);

            if (data is Mat mat)
                return BuildOutputImage(mat);

            if (data is Bitmap bitmap)
                return BuildOutputImage(bitmap.ToMat());

            if (data is IEnumerable<Mat> matList)
                return BuildOutputImageList(matList);

            if (data is IEnumerable<Bitmap> bitmapList)
                return BuildOutputImageList(bitmapList);

            string typeName = data == null ? "空值" : data.GetType().Name;
            throw new Exception($"共享变量“{variableName}”的类型为{typeName}，不是可用图像类型！");
        }

        /// <summary>
        /// 修正共享变量中已有的图像源输出，保证原图、图像列表和灰度缓存可用。
        /// </summary>
        /// <param name="outputImage">共享变量中的图像源输出。</param>
        /// <returns>归一化后的图像源输出。</returns>
        private static OutputImage NormalizeOutputImage(OutputImage outputImage)
        {
            if (outputImage == null)
                return new OutputImage();

            if (outputImage.Bitmaps == null)
                outputImage.Bitmaps = new List<Mat>();

            if (outputImage.Bitmaps.Count == 0)
            {
                if (OutputImage.HasValidImage(outputImage.SrcImg))
                    outputImage.Bitmaps.Add(outputImage.SrcImg);
            }
            else if (!OutputImage.HasValidImage(outputImage.Bitmaps[0]) && OutputImage.HasValidImage(outputImage.SrcImg))
            {
                outputImage.Bitmaps[0] = outputImage.SrcImg;
            }

            if (!OutputImage.HasValidImage(outputImage.SrcImg) && outputImage.Bitmaps.Count > 0)
                outputImage.SrcImg = outputImage.Bitmaps[0];

            if (!OutputImage.HasValidImage(outputImage.GrayImg))
                outputImage.GrayImg = OutputImage.BuildGrayImage(outputImage.SrcImg);

            if (outputImage.Rectangles == null)
                outputImage.Rectangles = new List<Rect>();

            return outputImage;
        }

        /// <summary>
        /// 用 Mat 列表构建图像源输出。
        /// </summary>
        /// <param name="images">Mat 图像列表。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage BuildOutputImageList(IEnumerable<Mat> images)
        {
            List<Mat> mats = new List<Mat>();
            if (images != null)
            {
                foreach (Mat image in images)
                {
                    if (OutputImage.HasValidImage(image))
                        mats.Add(image);
                }
            }

            if (mats.Count == 0)
                return new OutputImage();

            return new OutputImage
            {
                SrcImg = mats[0],
                Bitmaps = mats,
                GrayImg = OutputImage.BuildGrayImage(mats[0])
            };
        }

        /// <summary>
        /// 用 Bitmap 列表构建图像源输出。
        /// </summary>
        /// <param name="images">Bitmap 图像列表。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage BuildOutputImageList(IEnumerable<Bitmap> images)
        {
            List<Mat> mats = new List<Mat>();
            if (images != null)
            {
                foreach (Bitmap image in images)
                {
                    if (image != null)
                        mats.Add(image.ToMat());
                }
            }

            return BuildOutputImageList(mats);
        }

        /// <summary>
        /// 判断图像源输出是否有可继续流转的主图。
        /// </summary>
        /// <param name="outputImage">图像源输出对象。</param>
        /// <returns>主图有效时返回 true。</returns>
        private static bool HasRunnableOutputImage(OutputImage outputImage)
        {
            return outputImage != null &&
                outputImage.Bitmaps != null &&
                outputImage.Bitmaps.Count > 0 &&
                OutputImage.HasValidImage(outputImage.Bitmaps[0]);
        }

        /// <summary>
        /// 在当前流程批次中等待相机回调图像，软触发时只发送一次采图命令。
        /// </summary>
        /// <param name="param">当前图像源相机参数。</param>
        /// <param name="token">流程停止时使用的取消令牌。</param>
        /// <returns>由相机回调帧构建的标准图像输出。</returns>
        private async Task<OutputImage> AcquireCameraImageAsync(NodeParamImageSoucre param, CancellationToken token)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));
            if (param.Camera == null)
                throw new Exception("相机对象无效！");
            if (!param.Camera.IsOpen)
                throw new Exception("相机尚未连接！");

            Task<Mat> frameTask = _cameraFrameAwaiter.BeginWaitAsync(token);
            Mat callbackMat = null;
            try
            {
                if (param.TriggerSource == TriggerSource.SOFT)
                    param.Camera.GrabOne();

                callbackMat = await frameTask.ConfigureAwait(false);
                OutputImage outputImage = BuildOutputImage(callbackMat);
                callbackMat = null;
                return outputImage;
            }
            catch
            {
                callbackMat?.Dispose();
                _cameraFrameAwaiter.CancelPendingWait();
                throw;
            }
        }

        /// <summary>
        /// 节点运行方法
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
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

            if (ParamForm.Params is NodeParamImageSoucre param)
            {
                if (Result is NodeResultImageSource res)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        string fileName = ", 当前图像：";
                        if (param.ImageSource == "本地图像")
                        {
                            var ss = LoadTestImage(param, res);
#if DEBUG
                            fileName += ss;
#endif
                        }
                        else if (param.ImageSource == "相机")
                        {
                            if (param.Camera == null)
                                throw new Exception("相机对象无效！");
                            if (!param.Camera.IsOpen)
                                throw new Exception("相机尚未连接！");

                            // 是否需要每次设置相机参数
                            if (param.IsEveryTime)
                            {
                                param.Camera.SetTriggerDelay(param.TriggerDelay);
                                param.Camera.SetExposureTime(param.ExposureTime);
                                param.Camera.SetGain(param.Gain);
                                param.Camera.GetImageTimeOut = param.TimeOut;
                                param.Camera.SetTriggerSource(param.TriggerSource);
                                param.Camera.SetTriggerEdge(param.TriggerEdge);
                                param.Camera.SetTriggerMode(TriggerModel.On);
                            }

                            res.OutputImage = await AcquireCameraImageAsync(param, token).ConfigureAwait(false);
                        }
                        else if (param.ImageSource == "共享变量")
                        {
                            res.OutputImage = LoadSharedVariableImage(param.SharedVariableName);
#if DEBUG
                            fileName += param.SharedVariableName;
#endif
                        }
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        //Console.WriteLine("时间: "+ DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);

                        if (!HasRunnableOutputImage(res.OutputImage))
                            return new NodeReturn(NodeRunFlag.StopRun);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception e)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{e.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败！");
                    }
                }
            }

            return new NodeReturn(NodeRunFlag.StopRun);
        }
    }
}
