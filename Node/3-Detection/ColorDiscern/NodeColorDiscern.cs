using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.ColorDiscern;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public class NodeColorDiscern : NodeBase
    {
        public NodeColorDiscern(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormColorDiscern(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultColorDiscern();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            IImageResourceLease inputLease = null;
            NodeResultColorDiscern pendingResult = null;
            LogRuntimeStep(showLog, "开始运行");
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

            if (ParamForm is NodeParamFormColorDiscern form)
            {
                if (ParamForm.Params is NodeParamColorDiscern param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        await base.CheckTokenCancel(token);
                        // 获取干净原图并执行颜色识别，运行链路不再生成烧录标注图。
                        LogRuntimeStep(showLog, "开始获取输入图像");
                        OutputImage inputImage = form.GetInputOutputImage();
                        inputLease = inputImage.AcquireLease();
                        LogRuntimeStep(showLog, "获取输入图像完毕");
                        Mat sourceImage = MeasurementNodeHelper.GetReadOnlyPreviewMat(inputImage);
                        LogRuntimeStep(showLog, "获取只读Mat完毕，开始颜色匹配");
                        var (resultList, isOk) = await form.MatchColorResultsAsync(sourceImage);
                        LogRuntimeStep(showLog, $"颜色匹配完毕，结果数量:{(resultList == null ? 0 : resultList.Count)}");

                        pendingResult = new NodeResultColorDiscern();
                        pendingResult.OutputImage = OutputImage.FromBorrowedSingleImage(inputImage, sourceImage, inputImage?.GrayImg);
                        pendingResult.OutputImage.Rectangles = BuildRectangleList(resultList);
                        pendingResult.Result = BuildDisplayResult(resultList, isOk);
                        pendingResult.IsOk = isOk;
                        pendingResult.JudgeOk = isOk;
                        pendingResult.OutputImage.DisplayResult = pendingResult.Result;
                        LogRuntimeStep(showLog, "输出结果构建完毕");

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        pendingResult.RunTime = time;
                        Result = pendingResult;
                        pendingResult = null;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms, 匹配是否成功: {isOk}", true);

                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        Result = new NodeResultColorDiscern();
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        Result = new NodeResultColorDiscern();
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                    finally
                    {
                        NodeResultResourceManager.Release(pendingResult);
                        inputLease?.Dispose();
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);

        }

        /// <summary>
        /// 输出颜色识别运行分段诊断日志，用于定位线程池排队或内部检测耗时。
        /// </summary>
        /// <param name="showLog">当前流程是否允许输出日志。</param>
        /// <param name="step">诊断步骤名称。</param>
        private void LogRuntimeStep(bool showLog, string step)
        {
            if (!showLog)
                return;

            LogHelper.AddLog(MsgLevel.Debug, $"节点({ID}.{NodeName}){step}，{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
        }

        /// <summary>
        /// 根据颜色识别结果构建标准显示叠加层，供显示控件和下游模块读取。
        /// </summary>
        /// <param name="resultList">颜色识别结果集合。</param>
        /// <param name="isOk">整体颜色识别 OK/NG。</param>
        /// <returns>标准算法显示结果。</returns>
        internal static NodetColorResult BuildDisplayResult(List<DetectionResult> resultList, bool isOk)
        {
            var displayResult = new NodetColorResult();
            displayResult.IsAllOk = isOk;
            List<DetectionResult> detections = resultList ?? new List<DetectionResult>();

            if (detections.Count == 0)
                displayResult.Texts.Add(new ColorText("未识别到任何颜色", Color.Red));

            foreach (DetectionResult detection in detections)
            {
                ColorRotatedRect roiRect = new ColorRotatedRect(detection.Region);
                roiRect.AddFlag(detection.IsOk);
                displayResult.Rects.Add(roiRect);
                displayResult.Texts.Add(new ColorText($"颜色:{detection.Name} 面积:{detection.Area} 面积占比:{detection.AreaB}%", detection.IsOk ? Color.Green : Color.Red));
                AddDetectResult(displayResult, detection);
            }

            displayResult.Texts.Add(new ColorText($"颜色匹配结果个数：{detections.Count}个", isOk ? Color.Green : Color.Red));
            return displayResult;
        }

        /// <summary>
        /// 构建兼容旧模块使用的矩形列表。
        /// </summary>
        /// <param name="resultList">颜色识别结果集合。</param>
        /// <returns>识别矩形集合。</returns>
        private static List<Rect> BuildRectangleList(List<DetectionResult> resultList)
        {
            var rectangles = new List<Rect>();
            if (resultList == null)
                return rectangles;

            foreach (DetectionResult detection in resultList)
                rectangles.Add(detection.Region);

            return rectangles;
        }

        /// <summary>
        /// 追加检测项结果，同名颜色归并到同一个结果列表，避免多目标同名时重复键异常。
        /// </summary>
        /// <param name="displayResult">标准算法显示结果。</param>
        /// <param name="detection">单个颜色识别结果。</param>
        private static void AddDetectResult(AlgorithmResult displayResult, DetectionResult detection)
        {
            if (!displayResult.DetectResults.TryGetValue(detection.Name, out List<SingleDetectResult> detectResults))
            {
                detectResults = new List<SingleDetectResult>();
                displayResult.DetectResults[detection.Name] = detectResults;
            }

            detectResults.Add(new SingleDetectResult(detection.Name, detection.Area + "", detection.IsOk));
        }
    }
}
