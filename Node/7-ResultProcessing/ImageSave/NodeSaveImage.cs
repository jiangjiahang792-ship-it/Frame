using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageSave
{
    /// <summary>
    /// 保存图片节点，只负责生成保存请求并投递到全局图像保存服务。
    /// </summary>
    public class NodeImageSave : NodeBase
    {
        /// <summary>
        /// 节点运行。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否显示节点运行日志。</param>
        /// <returns>节点运行返回值。</returns>
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is ParamFormImageSave form && form.Params is NodeParamSaveImage param)
            {
                try
                {
                    SetStatus(NodeStatus.Unexecuted, "*");
                    base.CheckTokenCancel(token);

                    PrepareRuntimeParameters(form, param);
                    ImageSaveEnqueueResult enqueueResult = EnqueueSaveRequest(param);

                    int time = SetRunResult(startTime, NodeStatus.Successful);
                    Result.RunTime = time;
                    LogEnqueueResult(enqueueResult, showLog, time);
                    return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
                }
                catch (OperationCanceledException)
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                    SetRunResult(startTime, NodeStatus.Unexecuted);
                    throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                }
                catch (Exception ex)
                {
                    DisposeRuntimeImage(param);
                    LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                    SetRunResult(startTime, NodeStatus.Failed);
                    throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                }
            }

            return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
        }

        /// <summary>
        /// 初始化保存图片节点。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeImageSave(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageSave();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSave();
        }

        /// <summary>
        /// 从参数窗体读取运行期订阅值。
        /// </summary>
        /// <param name="form">保存图片参数窗体。</param>
        /// <param name="param">保存图片参数。</param>
        private static void PrepareRuntimeParameters(ParamFormImageSave form, NodeParamSaveImage param)
        {
            if (param.IsBarCode)
            {
                param.Barcode = form.GetBarCode();
                if (string.IsNullOrEmpty(param.Barcode))
                    throw new Exception("获取条码结果失败！");
            }

            if (param.NeedOkNg)
                param.AlgorithmResult = form.GetAiResult();

            param.Image = form.GetImage();

            if (string.IsNullOrEmpty(param.SavePath))
                throw new Exception("存图路径未设置！");

            if (param.Image == null)
                throw new Exception("订阅的图片对象为空！");
        }

        /// <summary>
        /// 生成保存请求并投递到全局图像保存服务。
        /// </summary>
        /// <param name="param">保存图片参数。</param>
        /// <returns>入队结果。</returns>
        private ImageSaveEnqueueResult EnqueueSaveRequest(NodeParamSaveImage param)
        {
            DateTime time = DateTime.Now;
            string imageSavePath = BuildBaseSavePath(param, time);
            string imageName = BuildImageName(param, time);
            bool isNg = false;
            List<string> saveDirectories = BuildSaveDirectories(param, imageSavePath, out isNg);

            if (saveDirectories.Count == 0)
            {
                DisposeRuntimeImage(param);
                return new ImageSaveEnqueueResult(ImageSaveEnqueueStatus.SkippedByCondition, 0, "当前OK/NG条件不需要保存图片。");
            }

            ImageSaveRequest request = new ImageSaveRequest
            {
                Image = param.Image,
                SaveDirectories = saveDirectories,
                ImageName = imageName,
                NeedCompress = param.NeedCompress,
                CompressValue = param.CompressValue,
                Priority = isNg ? ImageSavePriority.High : ImageSavePriority.Normal,
                IsNg = isNg,
                ProcessId = Process == null ? 0 : Process.ID,
                ProcessName = Process == null ? string.Empty : Process.ProcessName,
                NodeId = ID,
                NodeName = NodeName,
                CreateTime = time
            };

            param.Image = null;
            return ImageSaveService.Instance.Enqueue(request);
        }

        /// <summary>
        /// 构建基础保存路径，包含日期和早晚班目录。
        /// </summary>
        /// <param name="param">保存图片参数。</param>
        /// <param name="time">当前时间。</param>
        /// <returns>基础保存路径。</returns>
        private static string BuildBaseSavePath(NodeParamSaveImage param, DateTime time)
        {
            string imageSavePath = Path.Combine(param.SavePath, time.ToString("yyyyMMdd"));
            if (param.IsDayNight)
            {
                string dayNight = IsDayWorking(time.TimeOfDay, param.DayDataTime, param.NightDataTime) ? "早班" : "晚班";
                imageSavePath = Path.Combine(imageSavePath, dayNight);
            }

            return imageSavePath;
        }

        /// <summary>
        /// 构建图片基础名称。
        /// </summary>
        /// <param name="param">保存图片参数。</param>
        /// <param name="time">当前时间。</param>
        /// <returns>不含扩展名的图片基础名称。</returns>
        private static string BuildImageName(NodeParamSaveImage param, DateTime time)
        {
            string imageName = param.IsBarCode ? param.Barcode : time.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            if (param.NeedCompress)
                imageName += "-CAN";

            return imageName;
        }

        /// <summary>
        /// 根据 OK/NG 设置构建保存目录集合。
        /// </summary>
        /// <param name="param">保存图片参数。</param>
        /// <param name="baseSavePath">基础保存路径。</param>
        /// <param name="isNg">输出是否为 NG 保存请求。</param>
        /// <returns>保存目录集合。</returns>
        private static List<string> BuildSaveDirectories(NodeParamSaveImage param, string baseSavePath, out bool isNg)
        {
            isNg = false;
            List<string> paths = new List<string>();
            if (!param.NeedOkNg)
            {
                paths.Add(baseSavePath);
                return paths;
            }

            if (param.AlgorithmResult == null)
                throw new Exception("无法获取/解析AI检测结果！");

            bool allAreOk = param.AlgorithmResult.DetectResults != null &&
                param.AlgorithmResult.DetectResults.Count > 0 &&
                param.AlgorithmResult.DetectResults.All(pair => pair.Value != null && pair.Value.All(item => item.IsOk));

            isNg = !allAreOk;
            string okNgPath = Path.Combine(baseSavePath, allAreOk ? "OK" : "NG");
            switch (param.ImageTypeToSave)
            {
                case ImageTypeToSave.OkAndNg:
                    AddOkOrNgDirectories(paths, param, okNgPath, allAreOk);
                    break;
                case ImageTypeToSave.OnlyOk:
                    if (allAreOk)
                        paths.Add(okNgPath);
                    break;
                case ImageTypeToSave.OnlyNg:
                    if (!allAreOk)
                        AddNgDirectories(paths, param, okNgPath);
                    break;
                default:
                    break;
            }

            return paths;
        }

        /// <summary>
        /// 根据 OK/NG 结果添加保存目录。
        /// </summary>
        /// <param name="paths">保存目录集合。</param>
        /// <param name="param">保存图片参数。</param>
        /// <param name="okNgPath">OK/NG 根目录。</param>
        /// <param name="allAreOk">是否全部 OK。</param>
        private static void AddOkOrNgDirectories(List<string> paths, NodeParamSaveImage param, string okNgPath, bool allAreOk)
        {
            if (allAreOk)
                paths.Add(okNgPath);
            else
                AddNgDirectories(paths, param, okNgPath);
        }

        /// <summary>
        /// 为所有 NG 检测项添加保存目录。
        /// </summary>
        /// <param name="paths">保存目录集合。</param>
        /// <param name="param">保存图片参数。</param>
        /// <param name="ngRootPath">NG 根目录。</param>
        private static void AddNgDirectories(List<string> paths, NodeParamSaveImage param, string ngRootPath)
        {
            if (param.AlgorithmResult == null || param.AlgorithmResult.DetectResults == null)
                return;

            foreach (var pair in param.AlgorithmResult.DetectResults)
            {
                if (pair.Value == null || !pair.Value.TrueForAll(item => item.IsOk))
                    paths.Add(Path.Combine(ngRootPath, pair.Key));
            }
        }

        /// <summary>
        /// 判断当前时间是否属于早班。
        /// </summary>
        /// <param name="time">当前时间。</param>
        /// <param name="daydateTime">早班开始时间。</param>
        /// <param name="nightdataTime">晚班开始时间。</param>
        /// <returns>属于早班时返回 true。</returns>
        private static bool IsDayWorking(TimeSpan time, DateTime daydateTime, DateTime nightdataTime)
        {
            TimeSpan dayStartTime = daydateTime.TimeOfDay;
            TimeSpan nightStartTime = nightdataTime.TimeOfDay;
            return time >= dayStartTime && time < nightStartTime;
        }

        /// <summary>
        /// 记录保存请求入队结果。
        /// </summary>
        /// <param name="enqueueResult">入队结果。</param>
        /// <param name="showLog">是否显示节点运行日志。</param>
        /// <param name="runTime">节点运行耗时。</param>
        private void LogEnqueueResult(ImageSaveEnqueueResult enqueueResult, bool showLog, int runTime)
        {
            if (enqueueResult != null && enqueueResult.Status == ImageSaveEnqueueStatus.SkippedByCondition)
            {
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})无需保存图片：{enqueueResult.Message}({runTime} ms)", true);
                return;
            }

            if (enqueueResult != null && !enqueueResult.IsAccepted)
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})保存请求未入队：{enqueueResult.Message} 队列长度:{enqueueResult.QueueLength}", true);

            if (showLog)
                LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({runTime} ms)", true);
        }

        /// <summary>
        /// 释放参数中尚未交给后台服务接管的运行期图像。
        /// </summary>
        /// <param name="param">保存图片参数。</param>
        private static void DisposeRuntimeImage(NodeParamSaveImage param)
        {
            if (param == null || param.Image == null)
                return;

            try
            {
                param.Image.Dispose();
            }
            catch
            {
            }
            finally
            {
                param.Image = null;
            }
        }
    }
}
