using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        /// 相机回调帧缓存锁，回调线程和流程运行线程都会访问。
        /// </summary>
        private readonly object _callbackFrameLock = new object();
        /// <summary>
        /// 相机回调触发运行锁，流程未跑完时新帧直接丢弃，避免同流程重入。
        /// </summary>
        private readonly SemaphoreSlim _callbackRunLock = new SemaphoreSlim(1, 1);
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

            if (!_callbackRunLock.Wait(0))
            {
                LogHelper.AddLog(MsgLevel.Warn, $"流程【{Process.ProcessName}】仍在运行，本次相机回调帧丢弃。", true);
                return;
            }

            Mat callbackMat = null;
            try
            {
                callbackMat = bitmap.ToMat();

                StorePendingCallbackMat(callbackMat);
                callbackMat = null;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Process.Run(this, false);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"相机回调流程【{Process.ProcessName}】运行异常：{ex.Message}", true);
                    }
                    finally
                    {
                        ClearPendingCallbackMat();
                        _callbackRunLock.Release();
                    }
                });
            }
            catch (Exception ex)
            {
                callbackMat?.Dispose();
                _callbackRunLock.Release();
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
        /// 保存待处理回调图像，并释放未消费的旧帧。
        /// </summary>
        private void StorePendingCallbackMat(Mat mat)
        {
            Mat oldMat = null;
            lock (_callbackFrameLock)
            {
                oldMat = _pendingCallbackMat;
                _pendingCallbackMat = mat;
            }

            oldMat?.Dispose();
        }

        /// <summary>
        /// 取走待处理回调图像，调用方接管 Mat 生命周期。
        /// </summary>
        private Mat TakePendingCallbackMat()
        {
            lock (_callbackFrameLock)
            {
                Mat mat = _pendingCallbackMat;
                _pendingCallbackMat = null;
                return mat;
            }
        }

        /// <summary>
        /// 清理未被流程消费的回调图像。
        /// </summary>
        private void ClearPendingCallbackMat()
        {
            Mat mat = null;
            lock (_callbackFrameLock)
            {
                mat = _pendingCallbackMat;
                _pendingCallbackMat = null;
            }

            mat?.Dispose();
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
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        //Console.WriteLine("时间: "+ DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);

                        if (res.OutputImage.Bitmaps.Count == 0 || res.OutputImage.Bitmaps[0] == null)
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
