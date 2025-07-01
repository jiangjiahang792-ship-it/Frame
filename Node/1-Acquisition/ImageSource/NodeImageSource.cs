//using Logger;
//using System;
//using System.Drawing;
//using System.Threading;
//using System.Threading.Tasks;
//using TDJS_Vision.Device.Camera;

//namespace TDJS_Vision.Node._1_Acquisition.ImageSource
//{
//    internal class NodeImageSource : NodeBase
//    {
//        /// <summary>
//        /// 自动重置事件，用于确保软触发或者硬触发
//        /// 采集到图像才继续往下执行其他节点
//        /// </summary>
//        private AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
//        /// <summary>
//        /// 静态变量用于保存上次访问的图片索引
//        /// </summary>
//        public static int LastIndex = 0;
//        /// <summary>
//        /// 当前图片索引
//        /// </summary>
//        private string _nextImagePath;
//        /// <summary>
//        /// 当前图片
//        /// </summary>
//        private Bitmap _bitmap;

//        public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
//        {
//            ParamForm = new ParamFormImageSource(this);
//            ParamForm.SetNodeBelong(this);
//            Result = new NodeResultImageSource();
//            ParamFormImageSource.HardTriggerCompleted += ParamFormShot_HardTriggerCompleted;
//        }

//        /// <summary>
//        /// 硬触发完成
//        /// </summary>
//        /// <param name="sender"></param>
//        /// <param name="e"></param>
//        /// <exception cref="NotImplementedException"></exception>
//        private async void ParamFormShot_HardTriggerCompleted(object sender, HardTriggerResult e)
//        {
//            // 流程没启用不运行
//            if(!Process.Enable)
//                return;

//            // 主窗口设置按钮运行状态
//            Program.MainForm?.SetRunStatus(true);

//            // 设置图像结果
//            ((NodeResultImageSource)Result).Bitmap = e.Bitmap;
//            // 设置本节点的运行状态
//            long time = SetRunResult(e.StartTime, NodeStatus.Successful);
//            LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{Process.ProcessName}】（开始）  -----------------------------------------------------", true);
//            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
//            try
//            {
//                // 继续在该节点往下执行
//                await Process.RunForHardTrigger(this, (long)(DateTime.Now - e.StartTime).TotalMilliseconds);
//            }
//            catch (Exception) { }
//            finally
//            {
//                // 主窗口设置按钮运行状态
//                Program.MainForm?.SetRunStatus(false);
//            }
//        }

//        /// <summary>
//        /// 节点拍照的图片
//        /// </summary>
//        /// <param name="sender"></param>
//        /// <param name="e"></param>
//        public async void CameraSoft_PublishImageEvent(object sender, Bitmap e)
//        {
//            ((NodeResultImageSource)Result).Bitmap = e;
//            _autoResetEvent.Set();
//        }

//        /// <summary>
//        /// 节点运行方法
//        /// </summary>
//        /// <param name="token"></param>
//        /// <returns></returns>
//        /// <exception cref="Exception"></exception>
//        /// <exception cref="OperationCanceledException"></exception>
//        public override async Task Run(CancellationToken token)
//        {
//            DateTime startTime = DateTime.Now;
//            if (!Active)
//            {
//                SetRunResult(startTime, NodeStatus.Unexecuted);
//                return;
//            }
//            if (ParamForm.Params == null)
//            {
//                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
//                SetRunResult(startTime, NodeStatus.Failed);
//                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
//            }

//            if (ParamForm.Params is NodeParamImageSoucre param)
//            {
//                if (Result is NodeResultImageSource res)
//                {
//                    try
//                    {
//                        SetStatus(NodeStatus.Unexecuted, "*");
//                        base.CheckTokenCancel(token);

//                        if (param.ImageSource == "本地图像")
//                        {
//                            if (param.ImagePath != null)
//                            {
//                                using (var tempImage = Image.FromFile(param.ImagePath))
//                                {
//                                    _bitmap = new Bitmap(tempImage);
//                                }
//                                res.Bitmap = _bitmap;
//                            }
//                            else
//                            {
//                                // 是否选择自动选择下一张图片
//                                if (param.IsAutoLoop && param.ImagePaths.Count > 0)
//                                {
//                                    // 获取下一张图片
//                                    _nextImagePath = param.ImagePaths[LastIndex];
//                                    using (var tempImage = Image.FromFile(_nextImagePath))
//                                    {
//                                        _bitmap = new Bitmap(tempImage);
//                                    }
//                                    res.Bitmap = _bitmap;
//                                    // 计算下次运行的索引位置
//                                    LastIndex = (LastIndex + 1) % param.ImagePaths.Count;
//                                }
//                                else
//                                {
//                                    if (LastIndex == 0)
//                                    {
//                                        _nextImagePath = param.ImagePaths[LastIndex];
//                                        using (var tempImage = Image.FromFile(_nextImagePath))
//                                        {
//                                            _bitmap = new Bitmap(tempImage);
//                                        }
//                                        res.Bitmap = _bitmap;
//                                        return;
//                                    }
//                                    // 获取当前路径图片
//                                    _nextImagePath = param.ImagePaths[LastIndex - 1];
//                                    using (var tempImage = Image.FromFile(_nextImagePath))
//                                    {
//                                        _bitmap = new Bitmap(tempImage);
//                                    }
//                                    res.Bitmap = _bitmap;
//                                }

//                            }
//                            Result = res;
//                        }
//                        else if (param.ImageSource == "相机")
//                        {
//                            if (param.Camera == null) { throw new Exception("相机对象无效！"); }
//                            if (!param.Camera.IsOpen) { throw new Exception("相机尚未连接！"); }

//                            if (param.TriggerSource != TriggerSource.SOFT) { throw new Exception("只有软触发拍照才能手动点击运行！"); }

//                            // 绑定软触发取流事件
//                            param.Camera.Subscribe(CameraSoft_PublishImageEvent);
//                            param.Camera.SetActiveSubscriber(CameraSoft_PublishImageEvent);

//                            // 频闪才需要每次设置相机参数，不频闪的话在参数界面设置一次即可
//                            if (param.IsEveryTime)
//                            {
//                                param.Camera.SetTriggerMode(true);
//                                param.Camera.SetTriggerDelay(param.TriggerDelay);       // 设置触发延迟
//                                param.Camera.SetExposureTime(param.ExposureTime);       // 设置曝光时间
//                                param.Camera.SetGain(param.Gain);                       // 设置增益
//                                param.Camera.SetTriggerSource(param.TriggerSource);     // 设置触发源
//                                param.Camera.SetTriggerEdge(param.TriggerEdge);         // 设置硬触发边沿
//                            }
//                            param.Camera.GrabOne(); // 软触发
//                            bool result = _autoResetEvent.WaitOne(1000);// 软触发1s内取不到图也得返回
//                            if (!result) { throw new Exception("软触发取图超时！"); }
//                        }

//                        long time = SetRunResult(startTime, NodeStatus.Successful);
//                        LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
//                    }
//                    catch (OperationCanceledException)
//                    {
//                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
//                        SetRunResult(startTime, NodeStatus.Unexecuted);
//                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
//                    }
//                    catch (Exception e)
//                    {
//                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{e.Message}", true);
//                        SetRunResult(startTime, NodeStatus.Failed);
//                        throw new Exception($"节点({ID}.{NodeName})运行失败！");
//                    }
//                }
//            }
//        }
//    }
//}


using Logger;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    internal class NodeImageSource : NodeBase
    {
        /// <summary>
        /// 自动重置事件，用于确保软触发或者硬触发采集到图像才继续往下执行其他节点
        /// </summary>
        private static readonly SemaphoreSlim _cameraSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 静态变量用于保存上次访问的图片索引
        /// </summary>
        public static int LastIndex = 0;

        /// <summary>
        /// 当前图片索引
        /// </summary>
        private string _nextImagePath;

        /// <summary>
        /// 当前图片
        /// </summary>
        private Bitmap _bitmap;

        public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageSource(this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSource();
            ParamFormImageSource.HardTriggerCompleted += ParamFormShot_HardTriggerCompleted;
        }

        /// <summary>
        /// 硬触发完成
        /// </summary>
        private async void ParamFormShot_HardTriggerCompleted(object sender, HardTriggerResult e)
        {
            if (!Process.Enable) return;

            Program.MainForm?.SetRunStatus(true);

            ((NodeResultImageSource)Result).Bitmap = e.Bitmap;
            long time = SetRunResult(e.StartTime, NodeStatus.Successful);
            LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{Process.ProcessName}】（开始）  -----------------------------------------------------", true);
            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
            try
            {
                await Process.RunForHardTrigger(this, (long)(DateTime.Now - e.StartTime).TotalMilliseconds);
            }
            catch (Exception) { }
            finally
            {
                Program.MainForm?.SetRunStatus(false);
            }
        }

        /// <summary>
        /// 节点拍照的图片
        /// </summary>
        public async void CameraSoft_PublishImageEvent(object sender, Bitmap e, AutoResetEvent localWaitHandle)
        {
            ((NodeResultImageSource)Result).Bitmap = e;
            localWaitHandle.Set();
        }

        /// <summary>
        /// 节点运行方法
        /// </summary>
        public override async Task<NodeReturnFlag> Run(CancellationToken token)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return NodeReturnFlag.StopRun;
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

                        if (param.ImageSource == "本地图像")
                        {
                            if (param.ImagePath != null)
                            {
                                // 单图模式：直接加载指定路径的图像
                                using (var tempImage = Image.FromFile(param.ImagePath))
                                {
                                    _bitmap = new Bitmap(tempImage);
                                }
                                res.Bitmap = _bitmap;
                            }
                            else
                            {
                                // 多图模式
                                if (param.ImagePaths == null || param.ImagePaths.Count == 0)
                                {
                                    throw new Exception("图像路径列表为空或未设置！");
                                }

                                // 是否选择自动选择下一张图片
                                if (param.IsAutoLoop && param.ImagePaths.Count > 0)
                                {
                                    // 获取下一张图片
                                    _nextImagePath = param.ImagePaths[LastIndex];
                                    using (var tempImage = Image.FromFile(_nextImagePath))
                                    {
                                        _bitmap = new Bitmap(tempImage);
                                    }
                                    res.Bitmap = _bitmap;

                                    // 计算下次运行的索引位置
                                    LastIndex = (LastIndex + 1) % param.ImagePaths.Count;
                                }
                                else
                                {
                                    // 手动模式：只取当前索引 -1 的那张图（即上一次使用的图）
                                    if (LastIndex == 0)
                                    {
                                        // 如果是第一次运行，则取第一张图
                                        _nextImagePath = param.ImagePaths[0];
                                        using (var tempImage = Image.FromFile(_nextImagePath))
                                        {
                                            _bitmap = new Bitmap(tempImage);
                                        }
                                        res.Bitmap = _bitmap;
                                    }
                                    else
                                    {
                                        // 否则取上次使用的那一张图
                                        _nextImagePath = param.ImagePaths[LastIndex - 1];
                                        using (var tempImage = Image.FromFile(_nextImagePath))
                                        {
                                            _bitmap = new Bitmap(tempImage);
                                        }
                                        res.Bitmap = _bitmap;
                                    }
                                }
                            }
                            Result = res;
                        }
                        else if (param.ImageSource == "相机")
                        {
                            if (param.Camera == null) throw new Exception("相机对象无效！");
                            if (!param.Camera.IsOpen) throw new Exception("相机尚未连接！");
                            if (param.TriggerSource != TriggerSource.SOFT) throw new Exception("只有软触发拍照才能手动点击运行！");

                            var localWaitHandle = new AutoResetEvent(false);
                            EventHandler<Bitmap> localHandler = (sender, e) => CameraSoft_PublishImageEvent(sender, e, localWaitHandle);

                            await _cameraSemaphore.WaitAsync(token);
                            try
                            {
                                param.Camera.Subscribe(localHandler);
                                param.Camera.SetActiveSubscriber(localHandler);

                                if (param.IsEveryTime)
                                {
                                    param.Camera.SetTriggerMode(true);
                                    param.Camera.SetTriggerDelay(param.TriggerDelay);
                                    param.Camera.SetExposureTime(param.ExposureTime);
                                    param.Camera.SetGain(param.Gain);
                                    param.Camera.SetTriggerSource(param.TriggerSource);
                                    param.Camera.SetTriggerEdge(param.TriggerEdge);
                                }

                                param.Camera.GrabOne(); // 软触发

                                bool result = localWaitHandle.WaitOne(1000); // 软触发1s内取不到图也得返回
                                if (!result) throw new Exception("软触发取图超时！");
                            }
                            finally
                            {
                                param.Camera.Unsubscribe(localHandler);
                                localWaitHandle.Dispose();
                                _cameraSemaphore.Release();
                            }
                        }

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return NodeReturnFlag.ContinueRun;
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

            return NodeReturnFlag.StopRun;
        }
    }
}