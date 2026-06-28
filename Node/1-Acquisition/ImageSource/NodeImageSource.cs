using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeImageSource : NodeBase, ISkippedNodeRunResultProvider
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
        /// 相机回调触发运行状态，0 表示空闲，1 表示已有一帧正在转换或驱动流程运行。
        /// </summary>
        private int _callbackRunState;
        /// <summary>
        /// 待写入当前流程批次结果的相机回调图像。
        /// </summary>
        private Mat _pendingCallbackMat;

        public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageSource(this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSource();
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
            if (bitmap == null || Process == null)
                return;

            if (!Process.IsCameraCallbackTriggered)
                return;

            if (!Active || !Process.Enable)
                return;

            if (!Solution.Instance.IsRunning && !Process.IsHandRun)
                return;

            if (Process.IsRuning)
                return;

            if (!TryEnterCallbackRunState())
            {
                LogHelper.AddLog(MsgLevel.Warn, $"流程【{Process.ProcessName}】仍在运行，本次相机回调帧丢弃。", true);
                return;
            }

            Mat callbackMat = null;
            try
            {
                LogHelper.AddLog(MsgLevel.Exception, $"流程【{Process.ProcessName}】准备执行流程, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
                callbackMat = bitmap.ToMat();
                LogHelper.AddLog(MsgLevel.Exception, $"流程【{Process.ProcessName}】开始转换图像完毕, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);

                StorePendingCallbackMat(callbackMat);
                callbackMat = null;

                LogHelper.AddLog(MsgLevel.Exception, $"流程【{Process.ProcessName}】执行StorePendingCallbackMat方法完毕, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"流程【{Process.ProcessName}】执行Run方法, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
                        await Process.Run(this, false);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"相机回调流程【{Process.ProcessName}】运行异常：{ex.Message}", true);
                    }
                    finally
                    {
                        ClearPendingCallbackMat();
                        ReleaseCallbackRunState();
                    }
                });
            }
            catch (Exception ex)
            {
                callbackMat?.Dispose();
                ClearPendingCallbackMat();
                ReleaseCallbackRunState();
                LogHelper.AddLog(MsgLevel.Exception, $"相机回调图像转换失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 为跳过执行的图像源节点准备当前流程批次的回调图像结果。
        /// </summary>
        public bool TryPrepareSkippedRunResult(DateTime startTime, out string message)
        {
            message = string.Empty;
            Mat callbackMat = TakePendingCallbackMat();
            if (callbackMat == null || callbackMat.Empty())
            {
                callbackMat?.Dispose();
                message = "没有可用的相机回调图像。";
                SetRunResult(startTime, NodeStatus.Failed);
                return false;
            }

            NodeResultImageSource result = Result as NodeResultImageSource;
            if (result == null)
            {
                result = new NodeResultImageSource();
                Result = result;
            }

            result.OutputImage = BuildOutputImage(callbackMat);
            int time = SetRunResult(startTime, NodeStatus.Successful);
            result.RunTime = time;
            Result = result;
            return true;
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
        /// 保存待处理回调图像，并释放未消费的旧帧。
        /// </summary>
        private void StorePendingCallbackMat(Mat mat)
        {
            Mat oldMat = Interlocked.Exchange(ref _pendingCallbackMat, mat);
            oldMat?.Dispose();
        }

        /// <summary>
        /// 取走待处理回调图像，调用方接管 Mat 生命周期。
        /// </summary>
        private Mat TakePendingCallbackMat()
        {
            return Interlocked.Exchange(ref _pendingCallbackMat, null);
        }

        /// <summary>
        /// 清理未被流程消费的回调图像。
        /// </summary>
        private void ClearPendingCallbackMat()
        {
            Mat mat = Interlocked.Exchange(ref _pendingCallbackMat, null);
            mat?.Dispose();
        }

        /// <summary>
        /// 尝试进入相机回调运行态，不等待上一帧，失败时调用方直接丢弃当前帧。
        /// </summary>
        /// <returns>成功进入运行态返回 true。</returns>
        private bool TryEnterCallbackRunState()
        {
            return Interlocked.CompareExchange(ref _callbackRunState, 1, 0) == 0;
        }

        /// <summary>
        /// 释放相机回调运行态。
        /// </summary>
        private void ReleaseCallbackRunState()
        {
            Volatile.Write(ref _callbackRunState, 0);
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
                            if (param.Camera == null) throw new Exception("相机对象无效！");
                            if (!param.Camera.IsOpen) throw new Exception("相机尚未连接！");

                            // 是否需要每次设置相机参数
                            if (param.IsEveryTime)
                            {
                                //TODO: param.Camera.SetTriggerMode(true);
                                param.Camera.SetTriggerDelay(param.TriggerDelay);
                                param.Camera.SetExposureTime(param.ExposureTime);
                                param.Camera.SetGain(param.Gain);
                                param.Camera.GetImageTimeOut = param.TimeOut;
                                param.Camera.SetTriggerSource(param.TriggerSource);
                                param.Camera.SetTriggerEdge(param.TriggerEdge);
                            }
                            try
                            {
                                // 获取一帧图像并设置结果
                                using (var img = param.Camera.GetOneFrameImage())
                                {
                                    res.OutputImage = BuildOutputImage(img?.ToMat());
                                }
                            }
                            catch (Exception ex)
                            {
                                // 获取一帧图像并设置结果
                                res.OutputImage = BuildOutputImage(null);
                            }
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
