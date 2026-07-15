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

namespace TDJS_Vision.Node._3_Detection.LargeModel
{
    /// <summary>
    /// 大模型异常检测节点。
    /// </summary>
    public class NodeLargeModelDetection : NodeBase
    {
        /// <summary>
        /// 大模型模板加载与推理运行适配器。
        /// </summary>
        private readonly LargeModelDetectionRuntime _runtime = new LargeModelDetectionRuntime();

        /// <summary>
        /// 初始化大模型异常检测节点。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeLargeModelDetection(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormLargeModelDetection();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultLargeModelDetection();
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

            NodeParamLargeModelDetection param = ParamForm == null ? null : ParamForm.Params as NodeParamLargeModelDetection;
            if (param == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            ParamFormLargeModelDetection form = ParamForm as ParamFormLargeModelDetection;
            NodeResultLargeModelDetection res = Result as NodeResultLargeModelDetection;
            if (form == null || res == null)
            {
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})参数窗体或结果类型不匹配！");
            }

            OutputImage inputImage = null;
            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);
                res.AlgorithmResult.Clear();

                inputImage = form.GetOutputImage();
                Mat sourceImage = GetFirstImage(inputImage);
                if (sourceImage == null || sourceImage.Empty())
                    throw new InvalidOperationException("大模型调用输入图像为空。");

                LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}大模型调用开始，模板={param.TemplatePath}", true);
                LargeModelDetectionRunResult runResult = _runtime.Infer(sourceImage, param);
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}；模板={param.TemplatePath}；输入图像：{BuildOutputImageSummary(inputImage)}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>
        /// 释放大模型 native 模型句柄。
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
        /// 构建输入图像摘要，便于定位订阅为空、图像为空或上游输出异常。
        /// </summary>
        /// <param name="inputImage">订阅到的输入图像。</param>
        /// <returns>输入图像摘要文本。</returns>
        private static string BuildOutputImageSummary(OutputImage inputImage)
        {
            if (inputImage == null)
                return "OutputImage=null";

            int bitmapCount = inputImage.Bitmaps == null ? -1 : inputImage.Bitmaps.Count;
            int rectangleCount = inputImage.Rectangles == null ? -1 : inputImage.Rectangles.Count;
            string firstBitmap = bitmapCount > 0 ? FormatMat(inputImage.Bitmaps[0]) : "无";
            string firstRectangle = rectangleCount > 0 ? FormatRect(inputImage.Rectangles[0]) : "无";

            return $"SrcImg={FormatMat(inputImage.SrcImg)}，GrayImg={FormatMat(inputImage.GrayImg)}，Bitmaps数量={FormatCount(bitmapCount)}，Bitmaps[0]={firstBitmap}，Rectangles数量={FormatCount(rectangleCount)}，Rectangles[0]={firstRectangle}";
        }

        /// <summary>
        /// 格式化 OpenCV 图像尺寸。
        /// </summary>
        /// <param name="image">待格式化图像。</param>
        /// <returns>图像尺寸文本。</returns>
        private static string FormatMat(Mat image)
        {
            if (!OutputImage.HasValidImage(image))
                return "空";

            return image.Width.ToString(CultureInfo.InvariantCulture) + "x" +
                image.Height.ToString(CultureInfo.InvariantCulture) + "x" +
                image.Channels().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 格式化数量，-1 表示集合为空引用。
        /// </summary>
        /// <param name="count">数量。</param>
        /// <returns>数量文本。</returns>
        private static string FormatCount(int count)
        {
            return count < 0 ? "null" : count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 格式化 ROI 矩形。
        /// </summary>
        /// <param name="rect">待格式化矩形。</param>
        /// <returns>矩形文本。</returns>
        private static string FormatRect(Rect rect)
        {
            return rect.X.ToString(CultureInfo.InvariantCulture) + "," +
                rect.Y.ToString(CultureInfo.InvariantCulture) + "," +
                rect.Width.ToString(CultureInfo.InvariantCulture) + "," +
                rect.Height.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 发布大模型调用结果。
        /// </summary>
        /// <param name="res">节点结果对象。</param>
        /// <param name="runResult">运行结果。</param>
        private static void PublishResult(NodeResultLargeModelDetection res, LargeModelDetectionRunResult runResult)
        {
            res.IsOk = runResult.IsOk;
            res.Score = runResult.NativeResult == null ? 0F : runResult.NativeResult.Score;
            res.BoxCount = runResult.Boxes.Count;

            AlgorithmResult algorithmResult = res.AlgorithmResult;
            algorithmResult.Clear();
            algorithmResult.IsAllOk = runResult.IsOk;
            algorithmResult.DetectResults["大模型调用"] = new List<SingleDetectResult>
            {
                new SingleDetectResult("大模型调用", runResult.Message, runResult.IsOk)
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

            algorithmResult.RectsNgMap["大模型异常"] = ngRects;
        }

        /// <summary>
        /// 生成结果显示文本。
        /// </summary>
        /// <param name="runResult">运行结果。</param>
        /// <returns>显示文本。</returns>
        private static string BuildDisplayText(LargeModelDetectionRunResult runResult)
        {
            string judgeText = runResult.IsOk ? "OK" : "NG";
            float score = runResult.NativeResult == null ? 0F : runResult.NativeResult.Score;
            return string.Format(CultureInfo.InvariantCulture, "大模型调用：{0}，分数 {1:F4}，异常框 {2}，{3}", judgeText, score, runResult.Boxes.Count, runResult.Message);
        }
    }
}

