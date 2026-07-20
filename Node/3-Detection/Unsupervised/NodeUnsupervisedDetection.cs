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
    public class NodeUnsupervisedDetection : NodeBase, INodeRuntimePreloader
    {
        /// <summary>
        /// 无监督模板加载与推理运行适配器。
        /// </summary>
        private readonly UnsupervisedDetectionRuntime _runtime = new UnsupervisedDetectionRuntime();

        /// <summary>
        /// 模型预加载提交与流程推理共用的配置门，保证检测器和节点参数成对切换。
        /// </summary>
        private readonly UnsupervisedRuntimeConfigurationGate _configurationGate = new UnsupervisedRuntimeConfigurationGate();

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

            ParamFormUnsupervisedDetection form = ParamForm as ParamFormUnsupervisedDetection;
            NodeResultUnsupervisedDetection res = Result as NodeResultUnsupervisedDetection;
            if (form == null || res == null)
            {
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})参数窗体或结果类型不匹配！");
            }

            OutputImage inputImage = null;
            NodeParamUnsupervisedDetection param = null;
            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);
                res.AlgorithmResult.Clear();

                inputImage = form.GetOutputImage();
                Mat sourceImage = GetFirstImage(inputImage);
                if (sourceImage == null || sourceImage.Empty())
                    throw new InvalidOperationException("无监督检测输入图像为空。");

                UnsupervisedDetectionRunResult runResult = _configurationGate.Execute(() =>
                {
                    NodeParamUnsupervisedDetection currentParam = form.Params as NodeParamUnsupervisedDetection;
                    if (currentParam == null)
                        throw new InvalidOperationException($"节点({NodeName})运行参数未设置或保存！");

                    param = currentParam;
                    LogHelper.AddLog(MsgLevel.Debug, $"流程{Process.ProcessName}无监督检测开始，模板={currentParam.TemplatePath}", true);
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}；模板={templatePath}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>
        /// 在后台线程预先加载并预热无监督运行环境。
        /// </summary>
        /// <param name="param">待预加载的节点参数。</param>
        /// <returns>预加载任务。</returns>
        public Task PreloadRuntimeAsync(NodeParamUnsupervisedDetection param)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            ParamFormUnsupervisedDetection form = ParamForm as ParamFormUnsupervisedDetection;
            if (form == null)
                throw new InvalidOperationException("当前节点的无监督参数窗体无效。");

            return Task.Run(() => _configurationGate.PreloadAndCommit(
                () => _runtime.Preload(param),
                () => form.Params = param));
        }

        /// <summary>
        /// 获取当前节点是否存在可用于方案打开预加载的已保存无监督参数。
        /// </summary>
        public bool HasSavedRuntimeConfiguration
        {
            get
            {
                ParamFormUnsupervisedDetection form = ParamForm as ParamFormUnsupervisedDetection;
                NodeParamUnsupervisedDetection param = form == null ? null : form.Params as NodeParamUnsupervisedDetection;
                return param != null && !string.IsNullOrWhiteSpace(param.TemplatePath);
            }
        }

        /// <summary>
        /// 从方案恢复的节点参数预加载并预热无监督运行时。
        /// </summary>
        /// <returns>预加载任务。</returns>
        public Task PreloadSavedRuntimeAsync()
        {
            ParamFormUnsupervisedDetection form = ParamForm as ParamFormUnsupervisedDetection;
            NodeParamUnsupervisedDetection param = form == null ? null : form.Params as NodeParamUnsupervisedDetection;
            if (param == null || string.IsNullOrWhiteSpace(param.TemplatePath))
                return Task.CompletedTask;

            return PreloadRuntimeAsync(param);
        }

        /// <summary>
        /// 释放无监督 native 模型句柄。
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

    /// <summary>
    /// 协调无监督配置提交和流程推理，避免检测器与节点参数出现跨版本组合。
    /// </summary>
    internal sealed class UnsupervisedRuntimeConfigurationGate
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
