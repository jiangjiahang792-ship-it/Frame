using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using DrawingRect = System.Drawing.Rectangle;

namespace TDJS_Vision.Node._3_Detection.Unsupervised
{
    /// <summary>
    /// 无监督异常检测节点。
    /// </summary>
    public class NodeUnsupervisedDetection : NodeBase
    {
        /// <summary>
        /// 无监督模板加载与推理运行适配器。
        /// </summary>
        private readonly UnsupervisedDetectionRuntime _runtime = new UnsupervisedDetectionRuntime();

        /// <summary>
        /// 初始化无监督异常检测节点。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeUnsupervisedDetection(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormUnsupervisedDetection();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultUnsupervisedDetection();
        }

        /// <summary>
        /// 获取自由画布节点卡片上显示的无监督运行参数。
        /// </summary>
        /// <returns>运行参数摘要。</returns>
        public override string GetCanvasParameterSummary()
        {
            NodeParamUnsupervisedDetection param = ParamForm == null ? null : ParamForm.Params as NodeParamUnsupervisedDetection;
            if (param == null)
                return "未设置运行参数";

            string batchText = param.InferenceBatchSize <= 0
                ? "模板"
                : param.InferenceBatchSize.ToString(CultureInfo.InvariantCulture);
            string thresholdText = param.Threshold.ToString("0.####", CultureInfo.InvariantCulture);
            string areaText = param.MiniArea.ToString("0.####", CultureInfo.InvariantCulture);
            string maxBoxesText = param.MaxBoxes.ToString(CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "阈值:{0} 批次:{1} 面积:{2} 框:{3}", thresholdText, batchText, areaText, maxBoxesText);
        }

        /// <summary>
        /// 节点运行。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回值。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            NodeParamUnsupervisedDetection param = ParamForm == null ? null : ParamForm.Params as NodeParamUnsupervisedDetection;
            if (param == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            ParamFormUnsupervisedDetection form = ParamForm as ParamFormUnsupervisedDetection;
            NodeResultUnsupervisedDetection res = Result as NodeResultUnsupervisedDetection;
            if (form == null || res == null)
            {
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})参数窗体或结果类型不匹配！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);
                res.AlgorithmResult.Clear();

                OutputImage inputImage = form.GetOutputImage();
                Mat sourceImage = GetFirstImage(inputImage);
                if (sourceImage == null || sourceImage.Empty())
                    throw new InvalidOperationException("无监督检测输入图像为空。");

                LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}无监督检测开始，模板={param.TemplatePath}", true);
                UnsupervisedDetectionRunResult runResult = _runtime.Infer(sourceImage, param);
                PublishResult(res, runResult);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                ApplyResultOutcomeStatus();
                if (showLog)
                {
                    string judgeText = runResult.IsOk ? "OK" : "NG";
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，结果：{judgeText}，异常框：{runResult.Boxes.Count}，分数：{runResult.NativeResult.Score:F4})", true);
                }

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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>
        /// 释放无监督 native 模型句柄。
        /// </summary>
        public void DisposeRuntime()
        {
            _runtime.Dispose();
        }

        /// <summary>
        /// 获取输入图像中的第一张 OpenCV 图。
        /// </summary>
        /// <param name="inputImage">订阅图像。</param>
        /// <returns>第一张图像。</returns>
        private static Mat GetFirstImage(OutputImage inputImage)
        {
            if (inputImage == null || inputImage.Bitmaps == null || inputImage.Bitmaps.Count == 0)
                return null;
            return inputImage.Bitmaps[0];
        }

        /// <summary>
        /// 发布无监督检测结果。
        /// </summary>
        /// <param name="res">节点结果对象。</param>
        /// <param name="runResult">运行结果。</param>
        private static void PublishResult(NodeResultUnsupervisedDetection res, UnsupervisedDetectionRunResult runResult)
        {
            res.IsOk = runResult.IsOk;
            res.Score = runResult.NativeResult == null ? 0F : runResult.NativeResult.Score;
            res.BoxCount = runResult.Boxes.Count;

            AlgorithmResult algorithmResult = res.AlgorithmResult;
            algorithmResult.Clear();
            algorithmResult.IsAllOk = runResult.IsOk;
            algorithmResult.DetectResults["无监督检测"] = new List<SingleDetectResult>
            {
                new SingleDetectResult("无监督检测", runResult.Message, runResult.IsOk)
            };

            Color drawColor = runResult.IsOk ? Color.Lime : Color.Red;
            algorithmResult.Texts.Add(new ColorText(BuildDisplayText(runResult), drawColor));
            if (runResult.Boxes.Count == 0)
                return;

            List<ColorRotatedRect> ngRects = new List<ColorRotatedRect>();
            foreach (DrawingRect box in runResult.Boxes)
            {
                Rect cvRect = new Rect(box.X, box.Y, box.Width, box.Height);
                ColorRotatedRect drawRect = new ColorRotatedRect(cvRect, Color.Red);
                algorithmResult.Rects.Add(drawRect);
                ngRects.Add(new ColorRotatedRect(cvRect, Color.Red));
            }

            algorithmResult.RectsNgMap["无监督异常"] = ngRects;
        }

        /// <summary>
        /// 生成结果显示文本。
        /// </summary>
        /// <param name="runResult">运行结果。</param>
        /// <returns>显示文本。</returns>
        private static string BuildDisplayText(UnsupervisedDetectionRunResult runResult)
        {
            string judgeText = runResult.IsOk ? "OK" : "NG";
            float score = runResult.NativeResult == null ? 0F : runResult.NativeResult.Score;
            return string.Format(CultureInfo.InvariantCulture, "无监督检测：{0}，分数 {1:F4}，异常框 {2}，{3}", judgeText, score, runResult.Boxes.Count, runResult.Message);
        }
    }
}
