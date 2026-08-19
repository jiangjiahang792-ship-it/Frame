using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Forms.SolRunParam;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node;
using TDJS_Vision.Node._7_ResultProcessing.ImageDraw;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;
using TDJS_Vision.Forms.Login;

namespace TDJS_Vision.Forms.ImageViewer
{
    public partial class FrmSingleImage : DockContent
    {

        public string FormName;
        private bool _isBoundToProcess;
        private Process _process;
        private int? _lastImageShowNodeId;
        // 记录每个流程窗口上一次选择的AI节点，避免同一窗口反复弹出选择框。
        private static readonly Dictionary<string, int> AiNodeSelectionCache = new Dictionary<string, int>();

        /// <summary>
        /// 当前窗口等待UI显示的最新帧，容量固定为1。
        /// </summary>
        private PendingViewerFrame _pendingViewerFrame;

        /// <summary>
        /// 当前窗口是否已经向UI消息队列投递刷新任务。
        /// </summary>
        private int _viewerDispatchScheduled;

        /// <summary>
        /// 窗口事件和待显示资源是否已经释放。
        /// </summary>
        private int _runtimeResourcesReleased;

        public FrmSingleImage(string name)
        {
            InitializeComponent();
            BindLanguage();
            FormName = NormalizeWindowKey(name);
            this.Text = GetWindowDisplayName(FormName);
            NodeImageShow.ImageShowChanged += NodeImageShow_ImageShowChanged;
            NodeImageShow.ImageShowWindowNameChanged += NodeImageShow_ImageShowWindowNameChanged1;
            SolRunParamControl.ImageShowChanged += NodeImageShow_ImageShowChanged;
            button1.Click += button1_Click;
            button2.Click += button2_Click;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            UserPermissionContext.RoleChanged += UserPermissionContext_RoleChanged;
            ApplyManualTuningPermission();
            FormClosed += FrmSingleImage_FormClosed;
        }

        /// <summary>
        /// 窗口释放时解除全局事件，避免关闭后的窗口被静态事件长期引用。
        /// </summary>
        private void FrmSingleImage_FormClosed(object sender, FormClosedEventArgs e)
        {
            ReleaseRuntimeResources();
        }

        /// <summary>
        /// 登录角色变化后立即刷新手动调参与一键学习按钮。
        /// </summary>
        private void UserPermissionContext_RoleChanged(object sender, UserRole role)
        {
            ApplyManualTuningPermission();
        }

        /// <summary>
        /// 按当前权限设置工艺调试按钮的可用状态，按钮保持可见便于操作员理解权限限制。
        /// </summary>
        private void ApplyManualTuningPermission()
        {
            bool canUseManualTuning = UserPermissionContext.CanUseManualTuning;
            button1.Enabled = canUseManualTuning;
            button2.Enabled = canUseManualTuning;
        }

        /// <summary>
        /// 对按钮事件执行权限兜底，防止通过代码或快捷入口绕过控件禁用状态。
        /// </summary>
        /// <returns>当前用户具有最高权限时返回 true。</returns>
        private static bool EnsureManualTuningPermission()
        {
            if (!UserPermissionContext.CanUseManualTuning)
            {
                MessageBoxTD.Show("当前用户没有权限使用手动调参与一键学习，请登录最高权限账号！");
                return false;
            }

            return true;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(button1, "ImageViewer.OneClickLearning");
            LanguageManager.Bind(button2, "ImageViewer.ManualParam");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(button1);
            LanguageManager.Apply(button2);
            if (!_isBoundToProcess)
                Text = GetWindowDisplayName(FormName);
        }

        public static string GetWindowKey(int index)
        {
            return "ImageWindow" + index;
        }

        public static string NormalizeWindowKey(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
                return string.Empty;

            if (windowName.StartsWith("ImageWindow", StringComparison.OrdinalIgnoreCase))
                return windowName;

            bool isImageWindowName = windowName.StartsWith("图像窗口", StringComparison.OrdinalIgnoreCase) ||
                windowName.StartsWith("Image Window", StringComparison.OrdinalIgnoreCase);
            if (!isImageWindowName)
                return windowName;

            string numberText = string.Empty;
            foreach (char c in windowName)
            {
                if (char.IsDigit(c))
                    numberText += c;
            }

            if (int.TryParse(numberText, out int index) && index > 0)
                return GetWindowKey(index);

            return windowName;
        }

        public static string GetWindowDisplayName(string windowName)
        {
            string key = NormalizeWindowKey(windowName);
            if (key.StartsWith("ImageWindow", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(key.Substring("ImageWindow".Length), out int index))
            {
                return LanguageManager.Format("ImageViewer.ImageWindow", index);
            }

            return windowName;
        }

        private void NodeImageShow_ImageShowWindowNameChanged1(Process process, string winName)
        {
            if (FormName != NormalizeWindowKey(winName))
                return;

            _process = process;
            _isBoundToProcess = true;
            
            // 封装 UI 更新操作
            Action updateImage = () =>
            {
                this.Text = process.ProcessName;
            };

            // 跨线程安全调用
            if (this.InvokeRequired)
            {
                this.BeginInvoke(updateImage);
            }
            else
            {
                updateImage();
            }
        }


        public static void ShowImage(string name, Mat mat)
        {
            var win = new FrmSingleImage(name);
            win.SetImage(mat);
            win.Show();
        }
        public void SetImage(Mat mat)
        {
            SetViewerImage(mat == null ? null : BitmapConverter.ToBitmap(mat));
        }

        private void NodeImageShow_ImageShowChanged(object sender, ImageShowPamra e)
        {
            if (FormName != NormalizeWindowKey(e.WinName))
                return;

            if (!e.TryTakeBitmap(out Bitmap bitmap))
                return;

            // 同一个窗口可能被多个图像显示节点复用，这里记录真正刷新当前画面的节点。
            if (sender is NodeImageShow nodeShow)
            {
                _process = nodeShow.Process;
                _lastImageShowNodeId = nodeShow.ID;
                _isBoundToProcess = true;
            }

            SetViewerImage(bitmap, e.DisplayResult, e.TraceContext);
            e.ConfirmBitmapOwnershipTransfer(bitmap);
        }

        private void SetViewerImage(
            Bitmap bitmap,
            AlgorithmResult displayResult = null,
            PerformanceTraceContext traceContext = null)
        {
            if (Volatile.Read(ref _runtimeResourcesReleased) != 0 || IsDisposed)
            {
                bitmap?.Dispose();
                return;
            }

            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            Stopwatch scheduleStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            var frame = new PendingViewerFrame(
                bitmap,
                displayResult,
                traceContext,
                diagnosticEnabled,
                scheduleStopwatch);
            bitmap = null;

            PendingViewerFrame superseded = Interlocked.Exchange(ref _pendingViewerFrame, frame);
            if (superseded != null)
            {
                if (diagnosticEnabled || superseded.DiagnosticEnabled)
                {
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【完整耗时-图像显示】{GetTraceContextText(superseded.TraceContext)}；阶段=容量1槽被新帧替换；窗口={FormName}；图像={superseded.BitmapDiagnosticText}；显示结果={superseded.DisplayResultDiagnosticText}",
                        true);
                }

                superseded.Dispose();
            }

            ScheduleLatestViewerFrame(frame);
        }

        /// <summary>
        /// 保证每个窗口的UI消息队列中最多存在一个图像刷新任务。
        /// </summary>
        /// <param name="submittedFrame">触发本次调度的帧，用于Debug投递诊断。</param>
        private void ScheduleLatestViewerFrame(PendingViewerFrame submittedFrame)
        {
            if (Interlocked.CompareExchange(ref _viewerDispatchScheduled, 1, 0) != 0)
                return;

            if (Volatile.Read(ref _runtimeResourcesReleased) != 0 || IsDisposed)
            {
                Interlocked.Exchange(ref _viewerDispatchScheduled, 0);
                DisposePendingViewerFrame();
                return;
            }

            if (!InvokeRequired)
            {
                ProcessLatestViewerFrame();
                return;
            }

            if (!IsHandleCreated)
            {
                Interlocked.Exchange(ref _viewerDispatchScheduled, 0);
                DisposePendingViewerFrame();
                return;
            }

            try
            {
                BeginInvoke(new MethodInvoker(ProcessLatestViewerFrame));
                if (submittedFrame != null && submittedFrame.DiagnosticEnabled)
                {
                    long postMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(submittedFrame.ScheduleStopwatch);
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【完整耗时-图像显示】{GetTraceContextText(submittedFrame.TraceContext)}；阶段=已投递UI；窗口={FormName}；图像={submittedFrame.BitmapDiagnosticText}；显示结果={submittedFrame.DisplayResultDiagnosticText}；投递耗时={postMs}ms",
                        true);
                    PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                        MsgLevel.Debug,
                        PerformanceSpikeDiagnostics.CommonSlowMs,
                        () => $"【慢诊断-窗口投递】{GetTraceContextText(submittedFrame.TraceContext)}；窗口={FormName}；图像={submittedFrame.BitmapDiagnosticText}；显示结果={submittedFrame.DisplayResultDiagnosticText}；投递耗时={postMs}ms；句柄已创建={IsHandleCreated}；InvokeRequired={InvokeRequired}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                        true,
                        postMs);
                }
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref _viewerDispatchScheduled, 0);
                DisposePendingViewerFrame();
            }
        }

        /// <summary>
        /// 在UI线程取出并显示当前最新帧，完成后处理调度期间到达的新帧。
        /// </summary>
        private void ProcessLatestViewerFrame()
        {
            PendingViewerFrame frame = Interlocked.Exchange(ref _pendingViewerFrame, null);
            try
            {
                if (frame != null && Volatile.Read(ref _runtimeResourcesReleased) == 0 && !IsDisposed)
                    DisplayViewerFrame(frame);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, "图像窗口刷新失败：" + ex.Message, true);
            }
            finally
            {
                frame?.Dispose();
                Interlocked.Exchange(ref _viewerDispatchScheduled, 0);
                PendingViewerFrame pending = Volatile.Read(ref _pendingViewerFrame);
                if (pending != null)
                    ScheduleLatestViewerFrame(pending);
            }
        }

        /// <summary>
        /// 将一组图像、叠加结果和追踪上下文显示到当前窗口。
        /// </summary>
        /// <param name="frame">当前窗口已取出的最新帧。</param>
        private void DisplayViewerFrame(PendingViewerFrame frame)
        {
            if (showImageControl1.IsDisposed)
                return;

            long queueDelay = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(frame.ScheduleStopwatch);
            Bitmap bitmap = frame.Bitmap;
            Stopwatch uiStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            if (!showImageControl1.TrySetImage(bitmap, frame.DisplayResult))
                return;

            frame.TransferBitmapOwnership();
            long setImageMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(uiStopwatch);
            if (frame.DiagnosticEnabled)
            {
                long traceToUiMilliseconds = frame.TraceContext?.GetElapsedMilliseconds(Stopwatch.GetTimestamp()) ?? 0L;
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-图像显示】{GetTraceContextText(frame.TraceContext)}；阶段=UI刷新完成；窗口={FormName}；图像={GetBitmapDiagnosticText(bitmap)}；显示结果={GetDisplayResultDiagnosticText(frame.DisplayResult)}；节点投递到UI={traceToUiMilliseconds}ms；UI排队={queueDelay}ms；SetImage={setImageMs}ms",
                    true);
                PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                    MsgLevel.Debug,
                    PerformanceSpikeDiagnostics.UiQueueSlowMs,
                    () => $"【慢诊断-窗口图像显示】{GetTraceContextText(frame.TraceContext)}；窗口={FormName}；图像={PerformanceSpikeDiagnostics.GetBitmapText(bitmap)}；显示结果={PerformanceSpikeDiagnostics.GetAlgorithmResultText(frame.DisplayResult)}；节点投递到UI={traceToUiMilliseconds}ms；排队等待={queueDelay}ms；SetImage={setImageMs}ms；窗体可见={Visible}；句柄已创建={IsHandleCreated}；InvokeRequired={InvokeRequired}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                    true,
                    traceToUiMilliseconds,
                    queueDelay,
                    setImageMs);
            }
        }

        /// <summary>
        /// 释放尚未显示的最新帧。
        /// </summary>
        private void DisposePendingViewerFrame()
        {
            PendingViewerFrame pending = Interlocked.Exchange(ref _pendingViewerFrame, null);
            pending?.Dispose();
        }

        /// <summary>
        /// 解除窗口静态事件并排空容量1显示槽。
        /// </summary>
        private void ReleaseRuntimeResources()
        {
            if (Interlocked.Exchange(ref _runtimeResourcesReleased, 1) != 0)
                return;

            NodeImageShow.ImageShowChanged -= NodeImageShow_ImageShowChanged;
            NodeImageShow.ImageShowWindowNameChanged -= NodeImageShow_ImageShowWindowNameChanged1;
            SolRunParamControl.ImageShowChanged -= NodeImageShow_ImageShowChanged;
            LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
            UserPermissionContext.RoleChanged -= UserPermissionContext_RoleChanged;
            DisposePendingViewerFrame();
            Interlocked.Exchange(ref _viewerDispatchScheduled, 0);
            NodeImageShow.ResetWindowRefreshState(FormName);
        }

        /// <summary>
        /// 一次待显示帧，将Bitmap、叠加结果和诊断上下文作为不可拆分的一组保存。
        /// </summary>
        private sealed class PendingViewerFrame : IDisposable
        {
            /// <summary>
            /// 初始化一组待显示帧并接管Bitmap所有权。
            /// </summary>
            /// <param name="bitmap">待显示Bitmap。</param>
            /// <param name="displayResult">同一帧的叠加结果。</param>
            /// <param name="traceContext">同一帧的性能追踪上下文。</param>
            /// <param name="diagnosticEnabled">是否记录完整Debug诊断。</param>
            /// <param name="scheduleStopwatch">UI排队计时器。</param>
            public PendingViewerFrame(
                Bitmap bitmap,
                AlgorithmResult displayResult,
                PerformanceTraceContext traceContext,
                bool diagnosticEnabled,
                Stopwatch scheduleStopwatch)
            {
                Bitmap = bitmap;
                DisplayResult = displayResult;
                TraceContext = traceContext;
                DiagnosticEnabled = diagnosticEnabled;
                ScheduleStopwatch = scheduleStopwatch;
                BitmapDiagnosticText = diagnosticEnabled ? GetBitmapDiagnosticText(bitmap) : string.Empty;
                DisplayResultDiagnosticText = diagnosticEnabled ? GetDisplayResultDiagnosticText(displayResult) : string.Empty;
            }

            /// <summary>
            /// 当前待显示Bitmap，由本对象持有直到成功交给显示控件。
            /// </summary>
            public Bitmap Bitmap { get; private set; }

            /// <summary>
            /// 当前帧对应的算法叠加结果。
            /// </summary>
            public AlgorithmResult DisplayResult { get; }

            /// <summary>
            /// 当前帧对应的性能追踪上下文。
            /// </summary>
            public PerformanceTraceContext TraceContext { get; }

            /// <summary>
            /// 当前帧是否启用完整Debug诊断。
            /// </summary>
            public bool DiagnosticEnabled { get; }

            /// <summary>
            /// 当前帧的UI排队计时器，Debug关闭时为空。
            /// </summary>
            public Stopwatch ScheduleStopwatch { get; }

            /// <summary>
            /// Debug开启时固化的Bitmap摘要，避免帧被替换释放后再读取原对象。
            /// </summary>
            public string BitmapDiagnosticText { get; }

            /// <summary>
            /// Debug开启时固化的叠加结果摘要。
            /// </summary>
            public string DisplayResultDiagnosticText { get; }

            /// <summary>
            /// Bitmap成功交给显示控件后解除本对象的释放责任。
            /// </summary>
            public void TransferBitmapOwnership()
            {
                Bitmap = null;
            }

            /// <summary>
            /// 释放仍由容量1槽持有的Bitmap。
            /// </summary>
            public void Dispose()
            {
                Bitmap bitmap = Bitmap;
                Bitmap = null;
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// 生成图像显示性能诊断所需的Bitmap尺寸文本。
        /// </summary>
        /// <param name="bitmap">待显示位图。</param>
        /// <returns>位图诊断文本。</returns>
        private static string GetBitmapDiagnosticText(Bitmap bitmap)
        {
            if (bitmap == null)
                return "空";

            double megaBytes = bitmap.Width * bitmap.Height * Math.Max(1, Image.GetPixelFormatSize(bitmap.PixelFormat) / 8D) / 1024D / 1024D;
            return $"{bitmap.Width}x{bitmap.Height}，{bitmap.PixelFormat}，约{megaBytes:F2}MB";
        }

        /// <summary>
        /// 生成显示结果的数量摘要，用于判断静态ROI构建是否过重。
        /// </summary>
        /// <param name="displayResult">显示结果。</param>
        /// <returns>显示结果诊断文本。</returns>
        private static string GetDisplayResultDiagnosticText(AlgorithmResult displayResult)
        {
            if (displayResult == null)
                return "空";

            int rectCount = displayResult.Rects == null ? 0 : displayResult.Rects.Count;
            int ngRectCount = displayResult.RectsNgMap == null ? 0 : displayResult.RectsNgMap.Values.Where(list => list != null).Sum(list => list.Count);
            int lineCount = displayResult.Lines == null ? 0 : displayResult.Lines.Count;
            int contourCount = displayResult.Contours == null ? 0 : displayResult.Contours.Count;
            int textCount = displayResult.Texts == null ? 0 : displayResult.Texts.Count;
            return $"矩形={rectCount}，NG缓存矩形={ngRectCount}，线={lineCount}，轮廓={contourCount}，文本={textCount}";
        }

        private void FrmSingleImage_FormClosing(object sender, FormClosingEventArgs e)
        {
            FrmImageViewer.CurWindowsNum--;
            e.Cancel = true;
            Hide();
        }

        private Process GetCurrentProcess()
        {
            if (_process != null)
                return _process;

            foreach (var process in Solution.Instance.AllProcesses)
            {
                foreach (var node in process.Nodes)
                {
                    if (node is NodeImageShow nodeShow &&
                        nodeShow.ParamForm.Params is NodeParamImageShow showParam &&
                        NormalizeWindowKey(showParam.WindowName) == FormName)
                    {
                        _process = process;
                        return _process;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 根据当前图像窗口解析按钮应该操作的AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="selectionCanceled"></param>
        /// <returns></returns>
        private NodeTDAI ResolveTargetAiNode(Process process, out bool selectionCanceled)
        {
            selectionCanceled = false;
            if (process == null)
                return null;

            List<NodeTDAI> candidates = ResolveAiNodesByWindow(process);
            if (candidates.Count == 0)
                candidates = GetAllAiNodes(process);

            return ResolveTargetAiNode(process, candidates, out selectionCanceled);
        }

        /// <summary>
        /// 在指定候选集合中解析按钮应该操作的AI检测节点。
        /// </summary>
        /// <param name="process">当前图像窗口所属流程。</param>
        /// <param name="candidates">允许选择的AI节点候选集合。</param>
        /// <param name="selectionCanceled">用户是否取消了多AI节点选择。</param>
        /// <returns>最终选择的AI检测节点。</returns>
        private NodeTDAI ResolveTargetAiNode(Process process, List<NodeTDAI> candidates, out bool selectionCanceled)
        {
            selectionCanceled = false;
            candidates = DistinctAiNodes(candidates);
            if (candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
            {
                CacheSelectedAiNode(process, candidates[0]);
                return candidates[0];
            }

            NodeTDAI cachedNode = GetCachedAiNode(process, candidates);
            if (cachedNode != null)
                return cachedNode;

            NodeTDAI selectedNode = FrmSelectAiNode.SelectNode(this, process.ProcessName, candidates);
            if (selectedNode == null)
            {
                selectionCanceled = true;
                return null;
            }

            CacheSelectedAiNode(process, selectedNode);
            return selectedNode;
        }

        /// <summary>
        /// 从当前图像窗口的显示链路反查AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private List<NodeTDAI> ResolveAiNodesByWindow(Process process)
        {
            var result = new List<NodeTDAI>();

            if (_lastImageShowNodeId.HasValue)
            {
                NodeImageShow lastShowNode = process.Nodes
                    .OfType<NodeImageShow>()
                    .FirstOrDefault(node => node.ID == _lastImageShowNodeId.Value);

                if (lastShowNode?.ParamForm.Params is NodeParamImageShow lastShowParam &&
                    NormalizeWindowKey(lastShowParam.WindowName) == FormName)
                {
                    AddAiNodesFromSubscription(process, lastShowParam.Text1, result);
                    return DistinctAiNodes(result);
                }
            }

            foreach (var node in process.Nodes)
            {
                if (!(node is NodeImageShow nodeShow) ||
                    !(nodeShow.ParamForm.Params is NodeParamImageShow showParam) ||
                    NormalizeWindowKey(showParam.WindowName) != FormName)
                {
                    continue;
                }

                AddAiNodesFromSubscription(process, showParam.Text1, result);
            }

            return DistinctAiNodes(result);
        }

        /// <summary>
        /// 从订阅节点继续解析AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="nodeText"></param>
        /// <param name="result"></param>
        private void AddAiNodesFromSubscription(Process process, string nodeText, List<NodeTDAI> result)
        {
            if (!TryGetNodeByText(process, nodeText, out NodeBase sourceNode))
                return;

            if (sourceNode is NodeTDAI aiNode)
            {
                result.Add(aiNode);
                return;
            }

            if (sourceNode is NodeImageDraw &&
                sourceNode.ParamForm.Params is NodeParamImageDraw drawParam)
            {
                AddAiNodeByText(process, drawParam.Text3, result);
                AddAiNodeByText(process, drawParam.Text5, result);
            }
        }

        /// <summary>
        /// 根据节点显示文本添加AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="nodeText"></param>
        /// <param name="result"></param>
        private void AddAiNodeByText(Process process, string nodeText, List<NodeTDAI> result)
        {
            if (TryGetNodeByText(process, nodeText, out NodeBase node) && node is NodeTDAI aiNode)
                result.Add(aiNode);
        }

        /// <summary>
        /// 解析当前图像窗口的运行参数显示范围，优先跟随图像显示订阅的ROI绘制节点。
        /// </summary>
        /// <param name="process">当前图像窗口所属流程。</param>
        /// <returns>ROI绘制链路过滤范围；没有ROI绘制链路时返回空。</returns>
        private RunParamDisplayFilter ResolveRunParamDisplayFilter(Process process)
        {
            if (process == null)
                return null;

            var filter = new RunParamDisplayFilter { WindowName = FormName };

            if (_lastImageShowNodeId.HasValue)
            {
                NodeImageShow lastShowNode = process.Nodes
                    .OfType<NodeImageShow>()
                    .FirstOrDefault(node => node.ID == _lastImageShowNodeId.Value);

                if (lastShowNode?.ParamForm.Params is NodeParamImageShow lastShowParam &&
                    NormalizeWindowKey(lastShowParam.WindowName) == FormName)
                {
                    TryAddRunParamFilterFromImageShow(process, lastShowNode, lastShowParam, filter);
                    return filter.IsSourceNodeLimited ? filter : null;
                }
            }

            foreach (var node in process.Nodes)
            {
                if (!(node is NodeImageShow nodeShow) ||
                    !(nodeShow.ParamForm.Params is NodeParamImageShow showParam) ||
                    NormalizeWindowKey(showParam.WindowName) != FormName)
                {
                    continue;
                }

                TryAddRunParamFilterFromImageShow(process, nodeShow, showParam, filter);
            }

            return filter.IsSourceNodeLimited ? filter : null;
        }

        /// <summary>
        /// 根据单个图像显示节点补充运行参数过滤范围。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="nodeShow">图像显示节点。</param>
        /// <param name="showParam">图像显示参数。</param>
        /// <param name="filter">待补充的过滤范围。</param>
        private void TryAddRunParamFilterFromImageShow(
            Process process,
            NodeImageShow nodeShow,
            NodeParamImageShow showParam,
            RunParamDisplayFilter filter)
        {
            if (process == null || nodeShow == null || showParam == null || filter == null)
                return;

            filter.AddImageShowNodeId(nodeShow.ID);
            TryAddRunParamFilterFromSubscription(process, showParam.Text1, filter);
        }

        /// <summary>
        /// 根据图像显示订阅节点解析ROI绘制来源。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="nodeText">图像显示订阅节点文本。</param>
        /// <param name="filter">待补充的过滤范围。</param>
        private void TryAddRunParamFilterFromSubscription(Process process, string nodeText, RunParamDisplayFilter filter)
        {
            if (process == null || filter == null || !TryGetNodeByText(process, nodeText, out NodeBase sourceNode))
                return;

            if (sourceNode is NodeResultOverlayDraw &&
                sourceNode.ParamForm.Params is NodeParamResultOverlayDraw overlayParam)
            {
                AddResultOverlayDrawSources(filter, sourceNode.ID, overlayParam);
                return;
            }

            if (sourceNode is NodeResultOverlayDraw2 &&
                sourceNode.ParamForm.Params is NodeParamResultOverlayDraw2 overlayParam2)
            {
                AddResultOverlayDraw2Sources(filter, sourceNode.ID, overlayParam2);
            }
        }

        /// <summary>
        /// 收集旧ROI结果绘制节点中实际绘制项订阅的来源节点。
        /// </summary>
        /// <param name="filter">待补充的过滤范围。</param>
        /// <param name="overlayNodeId">ROI绘制节点ID。</param>
        /// <param name="param">ROI绘制节点参数。</param>
        private static void AddResultOverlayDrawSources(
            RunParamDisplayFilter filter,
            int overlayNodeId,
            NodeParamResultOverlayDraw param)
        {
            if (filter == null)
                return;

            filter.AddOverlayDrawNodeId(overlayNodeId);
            if (param == null)
                return;

            if (param.Items != null)
            {
                foreach (ResultOverlayDrawItem item in param.Items)
                {
                    if (item == null || !item.Enabled)
                        continue;

                    bool needSource = item.ItemType != ResultOverlayDrawItemType.Text || !item.UseManualText;
                    if (needSource)
                        AddSourceNodeTextToFilter(filter, item.SourceText1);
                }
            }

        }

        /// <summary>
        /// 获取UI刷新日志使用的跨线程流程关联文本。
        /// </summary>
        /// <param name="traceContext">图像显示节点创建的诊断上下文。</param>
        /// <returns>上下文不存在时返回手动或非流程来源标记。</returns>
        private static string GetTraceContextText(PerformanceTraceContext traceContext)
        {
            return traceContext?.ToLogText() ?? "流程=非流程来源；TraceId=无；RunId=0；来源节点=无";
        }

        /// <summary>
        /// 收集新版ROI结果绘制节点中绘制项、单布尔判定或旧颜色规则订阅的来源节点。
        /// </summary>
        /// <param name="filter">待补充的过滤范围。</param>
        /// <param name="overlayNodeId">ROI绘制节点ID。</param>
        /// <param name="param">ROI绘制2节点参数。</param>
        private static void AddResultOverlayDraw2Sources(
            RunParamDisplayFilter filter,
            int overlayNodeId,
            NodeParamResultOverlayDraw2 param)
        {
            if (filter == null)
                return;

            filter.AddOverlayDrawNodeId(overlayNodeId);
            if (param == null)
                return;

            if (param.Items != null)
            {
                foreach (ResultOverlayDraw2Item item in param.Items)
                {
                    if (item == null || !item.Enabled)
                        continue;

                    bool needSource = item.ItemType != ResultOverlayDraw2ItemType.Text || !item.UseManualText;
                    if (needSource)
                        AddSourceNodeTextToFilter(filter, item.SourceText1);
                }
            }

            if (param.HasNewJudgeSubscription)
                AddSourceNodeTextToFilter(filter, param.JudgeText1);

            if (!param.HasNewJudgeSubscription && param.ColorRules != null)
            {
                foreach (ResultOverlayDraw2ColorRule rule in param.ColorRules)
                {
                    if (rule != null && rule.Enabled)
                        AddSourceNodeTextToFilter(filter, rule.SourceText1);
                }
            }
        }

        /// <summary>
        /// 从订阅文本中解析节点ID并加入过滤范围。
        /// </summary>
        /// <param name="filter">待补充的过滤范围。</param>
        /// <param name="nodeText">订阅节点文本。</param>
        private static void AddSourceNodeTextToFilter(RunParamDisplayFilter filter, string nodeText)
        {
            if (filter == null)
                return;

            if (RunParamDisplayFilter.TryGetNodeId(nodeText, out int nodeId))
                filter.AddSourceNodeId(nodeId);
        }

        /// <summary>
        /// 获取当前过滤范围内直接被ROI绘制订阅的AI节点。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="filter">ROI绘制运行参数过滤范围。</param>
        /// <returns>过滤范围内的AI节点集合。</returns>
        private static List<NodeTDAI> GetAiNodesByRunParamFilter(Process process, RunParamDisplayFilter filter)
        {
            if (process == null || filter == null || !filter.IsSourceNodeLimited)
                return new List<NodeTDAI>();

            return process.Nodes
                .OfType<NodeTDAI>()
                .Where(node => filter.ContainsSourceNode(node.ID))
                .ToList();
        }

        /// <summary>
        /// 获取流程中所有AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private static List<NodeTDAI> GetAllAiNodes(Process process)
        {
            return process.Nodes.OfType<NodeTDAI>().ToList();
        }

        /// <summary>
        /// 按节点ID去重AI检测节点。
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private static List<NodeTDAI> DistinctAiNodes(IEnumerable<NodeTDAI> nodes)
        {
            return nodes
                .Where(node => node != null)
                .GroupBy(node => node.ID)
                .Select(group => group.First())
                .ToList();
        }

        /// <summary>
        /// 通过“ID.节点名”文本查找节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="nodeText"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private static bool TryGetNodeByText(Process process, string nodeText, out NodeBase node)
        {
            node = null;
            if (process == null || string.IsNullOrWhiteSpace(nodeText))
                return false;

            int dotIndex = nodeText.IndexOf('.');
            if (dotIndex > 0 && int.TryParse(nodeText.Substring(0, dotIndex), out int nodeId))
                node = process.Nodes.FirstOrDefault(item => item.ID == nodeId);

            if (node == null)
                node = process.Nodes.FirstOrDefault(item => $"{item.ID}.{item.NodeName}" == nodeText);

            return node != null;
        }

        /// <summary>
        /// 获取当前窗口对应的AI节点缓存键。
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private string GetAiNodeCacheKey(Process process)
        {
            return $"{process.ProcessName}|{FormName}";
        }

        /// <summary>
        /// 缓存当前窗口选择的AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="aiNode"></param>
        private void CacheSelectedAiNode(Process process, NodeTDAI aiNode)
        {
            if (process == null || aiNode == null)
                return;

            AiNodeSelectionCache[GetAiNodeCacheKey(process)] = aiNode.ID;
        }

        /// <summary>
        /// 获取当前窗口缓存的AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="candidates"></param>
        /// <returns></returns>
        private NodeTDAI GetCachedAiNode(Process process, List<NodeTDAI> candidates)
        {
            if (process == null || candidates == null)
                return null;

            if (!AiNodeSelectionCache.TryGetValue(GetAiNodeCacheKey(process), out int nodeId))
                return null;

            return candidates.FirstOrDefault(node => node.ID == nodeId);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!EnsureManualTuningPermission())
                return;

            var process = GetCurrentProcess();
            if (process == null)
            {
                MessageBoxTD.Show(LanguageManager.T("ImageViewer.ProcessNotFound"));
                return;
            }

            try
            {
                NodeTDAI targetAiNode = ResolveTargetAiNode(process, out bool selectionCanceled);
                if (selectionCanceled)
                    return;
                if (targetAiNode == null)
                {
                    MessageBoxTD.Show(LanguageManager.T("ImageViewer.AINodeNotConfigured"));
                    return;
                }

                using (var solRunParam = new SolRunParamControl())
                {
                    solRunParam.Init(process, targetAiNode);
                    if (!solRunParam.StartAutoLearning())
                    {
                        MessageBoxTD.Show(LanguageManager.T("ImageViewer.AINodeNotConfigured"));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(LanguageManager.Format("ImageViewer.OneClickLearningFailed", ex.Message));
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!EnsureManualTuningPermission())
                return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            LogHelper.AddLog(MsgLevel.Info, $"【手动调参诊断】点击手动调参按钮，窗口={FormName}。", true);
            try
            {
                var process = GetCurrentProcess();
                if (process == null)
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"【手动调参诊断】未找到窗口对应流程，窗口={FormName}。", true);
                    MessageBoxTD.Show(LanguageManager.T("ImageViewer.ProcessNotFound"));
                    return;
                }

                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【手动调参诊断】已解析流程，流程={process.ProcessName}，节点数={process.Nodes.Count}，运行中={process.IsRuning}。",
                    true);

                if (process.IsRuning)
                {
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        $"【手动调参诊断】流程仍在运行，已阻止打开手动调参，流程={process.ProcessName}。",
                        true);
                    MessageBoxTD.Show("当前流程仍在运行，请等待运行结束后再打开手动调参！");
                    return;
                }

                RunParamDisplayFilter runParamDisplayFilter = ResolveRunParamDisplayFilter(process);
                if (runParamDisplayFilter != null && runParamDisplayFilter.IsSourceNodeLimited)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【手动调参诊断】已启用ROI绘制运行参数过滤，{runParamDisplayFilter.ToLogText()}。",
                        true);
                }
                else
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【手动调参诊断】当前窗口未解析到ROI绘制过滤范围，继续使用原AI参数解析逻辑，窗口={FormName}。",
                        true);
                }

                LogHelper.AddLog(MsgLevel.Info, $"【手动调参诊断】开始解析AI节点，流程={process.ProcessName}。", true);
                NodeTDAI targetAiNode;
                bool selectionCanceled;
                if (runParamDisplayFilter != null && runParamDisplayFilter.IsSourceNodeLimited)
                {
                    List<NodeTDAI> filteredAiNodes = GetAiNodesByRunParamFilter(process, runParamDisplayFilter);
                    targetAiNode = ResolveTargetAiNode(process, filteredAiNodes, out selectionCanceled);
                }
                else
                {
                    targetAiNode = ResolveTargetAiNode(process, out selectionCanceled);
                }

                if (selectionCanceled)
                {
                    LogHelper.AddLog(MsgLevel.Info, $"【手动调参诊断】用户取消AI节点选择，流程={process.ProcessName}。", true);
                    return;
                }

                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【手动调参诊断】AI节点解析完成，节点={(targetAiNode == null ? "空" : targetAiNode.ID + "." + targetAiNode.NodeName)}，耗时={stopwatch.ElapsedMilliseconds}ms。",
                    true);

                using (var form = new FormSolRunParam(process.ProcessName, targetAiNode, runParamDisplayFilter))
                {
                    LogHelper.AddLog(MsgLevel.Info, $"【手动调参诊断】开始打开运行参数窗体，流程={process.ProcessName}。", true);
                    form.ShowDialog(this);
                    LogHelper.AddLog(MsgLevel.Info, $"【手动调参诊断】运行参数窗体已关闭，流程={process.ProcessName}，总耗时={stopwatch.ElapsedMilliseconds}ms。", true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"【手动调参诊断】打开手动调参异常：{ex.Message}", true);
                MessageBoxTD.Show("打开手动调参失败：" + ex.Message);
            }
        }
    }
    /// <summary>
    /// 图像显示事件参数，包含显示内容和可选的跨线程流程诊断上下文。
    /// </summary>
    public class ImageShowPamra
    {
        /// <summary>尚未被目标窗口认领的位图。</summary>
        private Bitmap _bitmap;

        /// <summary>位图是否已经被一个目标窗口认领。</summary>
        private int _bitmapClaimed;

        /// <summary>位图是否已经由认领窗口确认接管。</summary>
        private int _bitmapOwnershipTransferred;

        /// <summary>目标图像窗口名称。</summary>
        public string WinName;
        /// <summary>需要叠加到图像窗口的算法显示结果。</summary>
        public AlgorithmResult DisplayResult;
        /// <summary>跨线程流程诊断上下文，Debug关闭时为空。</summary>
        public PerformanceTraceContext TraceContext;

        /// <summary>当前位图是否已经由一个目标窗口确认接管。</summary>
        public bool IsBitmapClaimed => Volatile.Read(ref _bitmapOwnershipTransferred) != 0;

        /// <summary>当前是否存在尚未确认转交的位图认领。</summary>
        public bool HasUnconfirmedBitmapClaim =>
            Volatile.Read(ref _bitmapClaimed) != 0 &&
            Volatile.Read(ref _bitmapOwnershipTransferred) == 0;

        /// <summary>
        /// 创建图像显示事件参数。
        /// </summary>
        /// <param name="winname">目标窗口名称。</param>
        /// <param name="bitmap">待显示位图。</param>
        /// <param name="displayResult">显示叠加结果。</param>
        /// <param name="traceContext">跨线程流程诊断上下文。</param>
        public ImageShowPamra(
            string winname,
            Bitmap bitmap,
            AlgorithmResult displayResult = null,
            PerformanceTraceContext traceContext = null)
        {
            WinName = FrmSingleImage.NormalizeWindowKey(winname);
            _bitmap = bitmap;
            DisplayResult = displayResult;
            TraceContext = traceContext;
        }

        /// <summary>
        /// 由匹配的目标窗口原子预认领位图，同一位图最多成功预认领一次。
        /// </summary>
        /// <param name="bitmap">成功时返回由调用窗口负责释放的位图。</param>
        /// <returns>成功预认领位图返回 true；窗口接管后还需确认转交。</returns>
        public bool TryTakeBitmap(out Bitmap bitmap)
        {
            bitmap = null;
            if (Interlocked.CompareExchange(ref _bitmapClaimed, 1, 0) != 0)
                return false;

            bitmap = Volatile.Read(ref _bitmap);
            if (bitmap == null)
                return false;

            return true;
        }

        /// <summary>
        /// 在目标窗口已经接管Bitmap后确认转交，发布器随后不再负责释放。
        /// </summary>
        /// <param name="bitmap">由本次认领取得并已交给窗口的Bitmap。</param>
        /// <returns>成功确认本次转交返回 true。</returns>
        public bool ConfirmBitmapOwnershipTransfer(Bitmap bitmap)
        {
            if (bitmap == null || Volatile.Read(ref _bitmapClaimed) == 0)
                return false;

            Bitmap transferred = Interlocked.CompareExchange(ref _bitmap, null, bitmap);
            if (!ReferenceEquals(transferred, bitmap))
                return false;

            Volatile.Write(ref _bitmapOwnershipTransferred, 1);
            return true;
        }

        /// <summary>
        /// 发布结束后释放未认领或认领后尚未确认转交的位图。
        /// </summary>
        public void DisposeUnclaimedBitmap()
        {
            Bitmap bitmap = Interlocked.Exchange(ref _bitmap, null);
            bitmap?.Dispose();
        }
    }
}
