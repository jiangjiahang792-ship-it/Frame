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
    public class NodeLargeModelDetection : NodeBase, INodeRuntimePreloader
    {
        /// <summary>
        /// 大模型模板加载与推理运行适配器。
        /// </summary>
        private readonly LargeModelDetectionRuntime _runtime = new LargeModelDetectionRuntime();

        /// <summary>
        /// 模型预加载提交与流程推理共用的配置门，保证检测器和节点参数成对切换。
        /// </summary>
        private readonly LargeModelRuntimeConfigurationGate _configurationGate = new LargeModelRuntimeConfigurationGate();

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

            ParamFormLargeModelDetection form = ParamForm as ParamFormLargeModelDetection;
            NodeResultLargeModelDetection res = Result as NodeResultLargeModelDetection;
            if (form == null || res == null)
            {
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})参数窗体或结果类型不匹配！");
            }

            OutputImage inputImage = null;
            NodeParamLargeModelDetection param = null;
            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);
                res.AlgorithmResult.Clear();

                inputImage = form.GetOutputImage();
                Mat sourceImage = GetFirstImage(inputImage);
                if (sourceImage == null || sourceImage.Empty())
                    throw new InvalidOperationException("大模型调用输入图像为空。");

                LargeModelDetectionRunResult runResult = _configurationGate.Execute(() =>
                {
                    NodeParamLargeModelDetection currentParam = form.Params as NodeParamLargeModelDetection;
                    if (currentParam == null)
                        throw new InvalidOperationException($"节点({NodeName})运行参数未设置或保存！");

                    param = currentParam;
                    LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}大模型调用开始，模板={currentParam.TemplatePath}", true);
                    return _runtime.Infer(sourceImage, currentParam);
                });
                PublishResult(res, runResult);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                ApplyResultOutcomeStatus();
                if (showLog)
                {
                    string judgeText = runResult.IsOk ? "OK" : "NG";
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})运行成功！({time} ms，结果：{judgeText}，异常框：{runResult.Boxes.Count}，分数：{runResult.NativeResult.Score:F4}，实际设备：{runResult.ActualDevice}，输入：{runResult.InputWidth}x{runResult.InputHeight}，图像准备：{runResult.ImagePreparationMilliseconds} ms，native推理：{runResult.NativeInferenceMilliseconds} ms，结果处理：{runResult.PostprocessMilliseconds} ms)",
                        true);
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
                string templatePath = param == null ? "未设置" : param.TemplatePath;
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}；模板={templatePath}；输入图像：{BuildOutputImageSummary(inputImage)}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>
        /// 在后台线程预先加载并预热大模型运行环境。
        /// </summary>
        /// <param name="param">待预加载的节点参数。</param>
        /// <returns>预加载任务。</returns>
        public Task PreloadRuntimeAsync(NodeParamLargeModelDetection param)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            ParamFormLargeModelDetection form = ParamForm as ParamFormLargeModelDetection;
            if (form == null)
                throw new InvalidOperationException("当前节点的大模型参数窗体无效。");

            return Task.Run(() => _configurationGate.PreloadAndCommit(
                () => _runtime.Preload(param),
                () => form.Params = param));
        }

        /// <summary>
        /// 获取当前节点是否存在可用于方案打开预加载的已保存大模型参数。
        /// </summary>
        public bool HasSavedRuntimeConfiguration
        {
            get
            {
                ParamFormLargeModelDetection form = ParamForm as ParamFormLargeModelDetection;
                NodeParamLargeModelDetection param = form == null ? null : form.Params as NodeParamLargeModelDetection;
                return param != null && !string.IsNullOrWhiteSpace(param.TemplatePath);
            }
        }

        /// <summary>
        /// 从方案恢复的节点参数预加载并预热大模型运行时。
        /// </summary>
        /// <returns>预加载任务。</returns>
        public Task PreloadSavedRuntimeAsync()
        {
            ParamFormLargeModelDetection form = ParamForm as ParamFormLargeModelDetection;
            NodeParamLargeModelDetection param = form == null ? null : form.Params as NodeParamLargeModelDetection;
            if (param == null || string.IsNullOrWhiteSpace(param.TemplatePath))
                return Task.CompletedTask;

            return PreloadRuntimeAsync(param);
        }

        /// <summary>
        /// 释放大模型 native 模型句柄。
        /// </summary>
        public void DisposeRuntime()
        {
            _configurationGate.Execute(() =>
            {
                _runtime.Dispose();
                return true;
            });
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

    /// <summary>
    /// 协调大模型配置提交和流程推理，避免检测器与节点参数出现跨版本组合。
    /// </summary>
    internal sealed class LargeModelRuntimeConfigurationGate
    {
        /// <summary>
        /// 配置提交和推理共用的同步锁。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 在配置稳定期间执行一次读取或推理操作。
        /// </summary>
        /// <typeparam name="TResult">操作结果类型。</typeparam>
        /// <param name="action">待执行操作。</param>
        /// <returns>操作结果。</returns>
        public TResult Execute<TResult>(Func<TResult> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (_syncRoot)
            {
                return action();
            }
        }

        /// <summary>
        /// 在同一个配置门内完成候选模型预加载和参数提交；预加载失败时不执行提交。
        /// </summary>
        /// <param name="preloadAction">候选模型预加载操作。</param>
        /// <param name="commitAction">预加载成功后的参数提交操作。</param>
        public void PreloadAndCommit(Action preloadAction, Action commitAction)
        {
            if (preloadAction == null)
                throw new ArgumentNullException(nameof(preloadAction));
            if (commitAction == null)
                throw new ArgumentNullException(nameof(commitAction));

            lock (_syncRoot)
            {
                preloadAction();
                commitAction();
            }
        }
    }
}

