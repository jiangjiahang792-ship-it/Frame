using Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.CameraAdd;
using TDJS_Vision.Forms.LightAdd;
using TDJS_Vision.Forms.ModbusAdd;
using TDJS_Vision.Forms.PLCAdd;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Device.Light;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node;
using TDJS_Vision.Device.TCP;
using TDJS_Vision.Forms.TCPAdd;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Forms.GlobalSignalSettings;
using System.Collections.Concurrent;
using TDJS_Vision.Device.COM;
using TDJS_Vision.Forms.COMAdd;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using System.Windows.Forms;
using ServiceStack;
using System.Diagnostics;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;
using TDJS_Vision.ResourceManagement;
using TDJS_Vision.Startup;

namespace TDJS_Vision
{
    public class Solution
    {
        #region 使用 Lazy<T> 实现线程安全的单例模式

        private static readonly Lazy<Solution> _lazy = new Lazy<Solution>(() => new Solution());

        private Solution() 
        {
            AllDevices = new List<IDevice>();
            CameraRawFrameMemoryBudgetManager = new CameraRawFrameMemoryBudgetManager();
            WorkpieceContextAccessor = new WorkpieceContextAccessor();
            WorkpieceIdentityGenerator = new WorkpieceIdentityGenerator();
            _orderedSignalCoordinator = CreateOrderedSignalCoordinator();
            RenewCameraProductionPipeline();
            _cpuWorkScheduler = new CpuWorkScheduler(new CpuWorkSchedulerOptions());
            _cpuResourceMonitor = new CpuResourceMonitor(
                _cpuWorkScheduler,
                new WindowsSystemCpuUsageSampler(),
                5000);
            StartCameraReconnectWatchdog();
        }

        public static Solution Instance => _lazy.Value;

        #endregion

        #region 私有字段、属性

        /// <summary>
        /// 方案运行取消源，通过它控制方案的停止
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>保护全方案共享保存工作池创建、重配与停止的一致性锁。</summary>
        private readonly object _imageSaveQueueSync = new object();

        /// <summary>全方案共享的有界图像保存工作池。</summary>
        private ImageQueueProcessor _imageSaveQueueProcessor;

        /// <summary>全方案共享的CPU重任务调度器。</summary>
        private readonly ICpuWorkScheduler _cpuWorkScheduler;

        /// <summary>不占用UI消息循环的全机CPU低频监控器。</summary>
        private readonly CpuResourceMonitor _cpuResourceMonitor;

        /// <summary>保护方案运行会话开始、结束与重置之间的状态一致性。</summary>
        private readonly object _runLifecycleSync = new object();

        /// <summary>全部方案运行会话退出时保持有信号。</summary>
        private readonly ManualResetEventSlim _allRunsStopped = new ManualResetEventSlim(true);

        /// <summary>当前尚未退出的方案运行会话数量。</summary>
        private int _activeRunSessionCount;

        /// <summary>方案重置期间禁止启动新的运行会话。</summary>
        private bool _isResetting;

        /// <summary>当前生产会话是否已提交不可恢复故障停机，活动会话退出前拒绝新入口。</summary>
        private bool _runFaultStopRequested;

        /// <summary>串行化方案重置以及“重置后恢复新方案”的完整变更过程。</summary>
        private readonly object _solutionMutationSync = new object();

        /// <summary>当前方案保存任务完整图像资源字节数的滚动采样器。</summary>
        private IImageResourceSizeSampler _imageSaveSizeSampler =
            new RollingImageResourceSizeSampler(RollingImageResourceSizeSampler.DefaultCapacity);

        /// <summary>保护相机生产帧预算和票据注册表成对替换。</summary>
        private readonly object _cameraProductionPipelineSync = new object();

        /// <summary>当前方案使用的生产帧实际字节预算管理器。</summary>
        private WorkpieceFrameMemoryBudgetManager _workpieceFrameMemoryBudgetManager;

        /// <summary>当前方案使用的多相机触发票据注册表。</summary>
        private CameraTriggerTicketRegistry _cameraTriggerTicketRegistry;

        /// <summary>当前注册表专属故障处理委托，用于替换时准确取消订阅。</summary>
        private Action<CameraProductionFault> _cameraProductionFaultHandler;

        /// <summary>保护生产会话有序信号协调器的原子替换。</summary>
        private readonly object _orderedSignalCoordinatorSync = new object();

        /// <summary>当前生产会话唯一的有序信号协调器。</summary>
        private OrderedSignalCoordinator _orderedSignalCoordinator;

        /// <summary>相机自动重连守护线程检查间隔，单位毫秒。</summary>
        private const int CameraReconnectWatchdogIntervalMilliseconds = 1000;

        /// <summary>保护相机自动重连守护线程启动和断线日志去重状态。</summary>
        private readonly object _cameraReconnectWatchdogSync = new object();

        /// <summary>后台检测相机连接并按需自动重连的守护线程。</summary>
        private Thread _cameraReconnectWatchdogThread;

        /// <summary>请求相机自动重连守护线程退出的原子标记。</summary>
        private int _cameraReconnectWatchdogStopRequested;

        /// <summary>已经报告过断线且尚未恢复的相机键，避免每秒重复刷同一条断线日志。</summary>
        private readonly HashSet<string> _reportedDisconnectedCameraKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region 公有字段、属性

        /// <summary>
        /// 方案包含的所有流程
        /// </summary>
        public List<Process> AllProcesses = new List<Process>();

        /// <summary>
        /// 方案添加的设备总数
        /// </summary>
        public int DeviceCount => AllDevices.Count;

        /// <summary>
        /// 方案已经添加的流程总数
        /// </summary>
        public int ProcessCount = 0;

        /// <summary>
        /// 方案节点统计（包含删除的节点）
        /// </summary>
        public int NodeCount = 0;

        /// <summary>
        /// 方案的全局信号
        /// </summary>
        public GlobalSignal GlobalSignal = new GlobalSignal();

        /// <summary>
        /// 方案已经添加的节点
        /// </summary>
        public List<NodeBase> Nodes = new List<NodeBase>();

        /// <summary>
        /// 所有设备
        /// </summary>
        public List<IDevice> AllDevices { get; set; }
        /// <summary>
        /// 光源设备
        /// </summary>
        public List<ILight> LightDevices => AllDevices.OfType<ILight>().ToList();

        /// <summary>
        /// 相机设备
        /// </summary>
        public List<ICamera> CameraDevices => AllDevices.OfType<ICamera>().ToList();

        /// <summary>
        /// 流程运行前的相机触发配置校验器，允许后续替换为自定义实现。
        /// </summary>
        public IProcessCameraConfigurationValidator CameraConfigurationValidator { get; set; } = new ProcessCameraConfigurationValidator();

        /// <summary>
        /// 3D 相机设备
        /// </summary>
        public List<I3DCamera> Camera3DDevices => AllDevices.OfType<I3DCamera>().ToList();

        /// <summary>
        /// 运行资源档案提供器，测试或扩展模块可以替换硬件探针和计算策略。
        /// </summary>
        public IRuntimeResourceProfileProvider ResourceProfileProvider { get; set; } = RuntimeResourceProfileProvider.CreateDefault();

        /// <summary>
        /// 最近一次方案运行前生成的自动资源建议档案。
        /// </summary>
        public AutomaticResourceProfile CurrentResourceProfile { get; private set; }

        /// <summary>全方案唯一的相机原始帧非托管内存预算管理器。</summary>
        public ICameraRawFrameMemoryBudgetManager CameraRawFrameMemoryBudgetManager { get; }

        /// <summary>全方案唯一的异步工件上下文访问器。</summary>
        public IWorkpieceContextAccessor WorkpieceContextAccessor { get; }

        /// <summary>全方案生产会话内按顺序域分配工件身份的生成器。</summary>
        internal WorkpieceIdentityGenerator WorkpieceIdentityGenerator { get; }

        /// <summary>获取当前生产会话唯一的有序信号协调器。</summary>
        public IOrderedSignalCoordinator OrderedSignalCoordinator
        {
            get
            {
                lock (_orderedSignalCoordinatorSync)
                    return _orderedSignalCoordinator;
            }
        }

        /// <summary>获取当前方案的生产帧实际字节预算管理器。</summary>
        public WorkpieceFrameMemoryBudgetManager WorkpieceFrameMemoryBudgetManager
        {
            get
            {
                lock (_cameraProductionPipelineSync)
                    return _workpieceFrameMemoryBudgetManager;
            }
        }

        /// <summary>获取当前方案的多相机触发票据注册表。</summary>
        public CameraTriggerTicketRegistry CameraTriggerTicketRegistry
        {
            get
            {
                lock (_cameraProductionPipelineSync)
                    return _cameraTriggerTicketRegistry;
            }
        }

        /// <summary>原子取得相机帧路由注册表及其生产活动状态，预览帧也使用冻结的注册表归还触发计数。</summary>
        /// <param name="registry">回调进入时的相机帧路由注册表。</param>
        /// <param name="productionActive">该注册表当前是否属于活动生产会话。</param>
        internal void GetCameraFrameRoutingSnapshot(
            out CameraTriggerTicketRegistry registry,
            out bool productionActive)
        {
            lock (_runLifecycleSync)
            {
                lock (_cameraProductionPipelineSync)
                    registry = _cameraTriggerTicketRegistry;
                productionActive = _activeRunSessionCount != 0 &&
                    !_runFaultStopRequested &&
                    registry != null &&
                    registry.IsAcceptingProductionFrames;
            }
        }

        /// <summary>
        /// 流程节点CPU工作分类器；插件可以替换分类策略，但AI和通信节点默认不受限。
        /// </summary>
        public ICpuWorkloadClassifier CpuWorkloadClassifier { get; set; } = new DefaultCpuWorkloadClassifier();

        /// <summary>
        /// Plc设备
        /// </summary>
        public List<IPlc> PlcDevices => AllDevices.OfType<IPlc>().ToList();
        /// <summary>
        /// Modbus设备
        /// </summary>
        public List<IModbus> ModbusDevices => AllDevices.OfType<IModbus>().ToList();
        /// <summary>
        /// TCP设备
        /// </summary>
        public List<ITcpDevice> TcpDevices => AllDevices.OfType<ITcpDevice>().ToList();
        /// <summary>
        /// 串口设备
        /// </summary>
        public List<ComDevice> ComDevices => AllDevices.OfType<ComDevice>().ToList();

        /// <summary>
        /// 方案是否正在运行
        /// </summary>
        public bool IsRunning { get; set; } = false;
        
        /// <summary>
        /// 方案总耗时
        /// </summary>
        public long RunTime {  get; set; } = -1;

        /// <summary>
        /// 流程运行间隔
        /// </summary>
        public int RunInterval { get; set; } = 0;

        /// <summary>
        /// 方案被修改
        /// </summary>
        public bool IsModify { get; set; } = false;

        /// <summary>
        /// 方案的共享变量
        /// </summary>
        public SharedVariable SharedVariable { get; set; } = new SharedVariable();
        /// <summary>
        /// 方案的流程信号
        /// </summary>
        public Dictionary<string, CountdownEvent> ProcessSignalDic { get; set; } = new Dictionary<string, CountdownEvent>();

        /// <summary>
        /// 方案文件名
        /// </summary>
        public string SolFileName { get; set; }

        /// <summary>
        /// 方案适配的软件版本
        /// </summary>
        public string SolVersion { get; set; }

        /// <summary>
        /// 方案运行取消令牌，嵌入到流程和节点中，实现对它们的控制
        /// </summary>
        public CancellationToken CancellationToken
        {
            get
            {
                lock (_runLifecycleSync)
                    return _cancellationTokenSource.Token;
            }
        }

        /// <summary>
        /// 流程运行完更新流程界面和主界面的运行按钮Enable
        /// </summary>
        public EventHandler<ProcessRunResult> UpdateRunStatus;

        /// <summary>
        /// 新建方案、加载方案要清除结果窗口数据
        /// </summary>
        public EventHandler RemoveResultData;

        /// <summary>
        /// 方案包含的所有检测项配置
        /// </summary>
        public Dictionary<string, List<DetectItemInfo>> DetectItemDic = new Dictionary<string, List<DetectItemInfo>>();

        #endregion

        #region 私有方法

        /// <summary>
        /// 释放当前方案所有流程的节点资源，清空流程前必须先调用，避免旧节点继续持有设备回调。
        /// </summary>
        private void ReleaseAllProcessResources()
        {
            foreach (Process process in AllProcesses.ToList())
                ReleaseProcessResources(process);
        }

        /// <summary>
        /// 释放单个流程内所有节点持有的事件订阅、参数窗体和模型句柄。
        /// </summary>
        /// <param name="process">需要释放资源的流程。</param>
        private void ReleaseProcessResources(Process process)
        {
            if (process == null || process.Nodes == null)
                return;

            foreach (NodeBase node in process.Nodes.ToList())
                ReleaseNodeResources(node);
        }

        /// <summary>
        /// 释放节点级资源，重点用于图像源节点参数窗体 Dispose 时解除相机回调绑定。
        /// </summary>
        /// <param name="node">需要释放资源的节点。</param>
        private void ReleaseNodeResources(NodeBase node)
        {
            if (node == null)
                return;

            node.ReleaseResultResources();
            ReleaseAiNodeModel(node);
            DisposeNodeParamForm(node);
            DisposeNodeControl(node);
        }

        /// <summary>
        /// 释放 AI 节点独占的模型句柄，避免删除流程后模型仍占用显存或文件句柄。
        /// </summary>
        /// <param name="node">待检查的节点。</param>
        private void ReleaseAiNodeModel(NodeBase node)
        {
            if (node.NodeType != NodeType.AITD || !(node.ParamForm?.Params is NodeParamTDAI param))
                return;

            try
            {
                ModelHandleManager.Destroy(param.Yolo8);
                param.Yolo8 = null;
                param.LoadedModelPath = null;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"释放节点({node.ID}.{node.NodeName})AI模型资源失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 释放节点参数窗体资源，参数窗体内部会处理相机回调等事件解绑。
        /// </summary>
        /// <param name="node">参数窗体所属节点。</param>
        private void DisposeNodeParamForm(NodeBase node)
        {
            IDisposable disposable = node.ParamForm as IDisposable;
            if (disposable == null)
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"释放节点({node.ID}.{node.NodeName})参数窗体资源失败：{ex.Message}", true);
            }
        }

        #endregion

        #region 公有方法

        #region 流程相关操作

        /// <summary>
        /// 添加流程
        /// </summary>
        /// <param name="process"></param>
        public void AddProcess(Process process)
        {
            AllProcesses.Add(process);
        }

        /// <summary>
        /// 删除流程
        /// </summary>
        /// <param name="process"></param>
        public void RemoveProcess(Process process)
        {
            if (process == null)
                return;

            ReleaseProcessResources(process);
            AllProcesses.Remove(process);

            foreach (var node in process.Nodes.ToList())
            {
                Solution.Instance.Nodes.Remove(node);
            }
        }

        /// <summary>
        /// 根据流程名称删除流程
        /// </summary>
        /// <param name="process"></param>
        public void RemoveProcess(string processName)
        {
            // 查找名称匹配的流程
            Process processToRemove = AllProcesses.Find(p => $"{p.ID}.{p.ProcessName}" == processName);

            if (processToRemove != null)
            {
                ReleaseProcessResources(processToRemove);

                // 从列表中移除流程
                AllProcesses.Remove(processToRemove);

                foreach (var node in processToRemove.Nodes.ToList())
                {
                    Solution.Instance.Nodes.Remove(node);
                }
            }
        }

        /// <summary>
        /// 根据流程名称找到对应流程对象
        /// </summary>
        /// <param name="process"></param>
        public Process FindProcess(string processName)
        {
            // 查找名称匹配的流程
            return AllProcesses.Find(p => $"{p.ID}.{p.ProcessName}" == processName);
        }

        /// <summary>
        /// 获取所有流程名称
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllProcessName()
        {
            List<string> result = new List<string>();
            foreach (var process in AllProcesses)
            {
                result.Add(process.ProcessName);
            }
            return result;
        }

        #endregion

        #region 方案运行/停止、配置加载/保存等相关操作

        /// <summary>
        /// 方案运行，默认不是循环运行
        /// </summary>
        /// <param name="isCyclical"></param>
        /// <returns></returns>
        public async Task Run(bool isCyclical = false)
        {
            if (AllProcesses == null || AllProcesses.Count == 0)
                return;

            if (!TryBeginRunSession())
            {
                LogHelper.AddLog(MsgLevel.Warn, "方案正在重置，已拒绝启动新的运行任务。", true);
                return;
            }

            CancellationTokenSource _cts = null;
            List<ICamera> startedCallbackCameras = new List<ICamera>();

            try
            {
                string validationMessage;
                if (!TryValidateCameraConfiguration(AllProcesses, out validationMessage))
                {
                    LogHelper.AddLog(MsgLevel.Exception, validationMessage, true);
                    return;
                }

                _cts = CancellationTokenSource.CreateLinkedTokenSource(Solution.Instance.CancellationToken);
                startedCallbackCameras = StartCameraCallbackGrabbingForProcesses(AllProcesses);
                var groupTasks = new List<Task>();

                foreach (var group in AllProcesses.GroupBy(p => p.Group))
                {
                    var groupCopy = group.ToList(); // 立即缓存
                    var groupCts = _cts; // 使用外部取消令牌

                    // 每组启动一个独立任务，自行循环
                    var groupTask = Task.Run(async () =>
                    {
                        while (!groupCts.Token.IsCancellationRequested)
                        {
                            string orderDomain = $"GROUP:{groupCopy[0].Group}";
                            long flowRevision = groupCopy.Max(process => process.FlowRevision);
                            WorkpieceExecutionContext workpieceContext = CreateWorkpieceExecutionContext(
                                orderDomain,
                                flowRevision,
                                "方案流程组运行",
                                groupCts.Token);

                            bool workpieceSucceeded = false;
                            try
                            {
                                using (WorkpieceContextAccessor.Push(workpieceContext))
                                {
                                    List<ProcessInvocationResult> activeResults = new List<ProcessInvocationResult>();
                                    // 按优先级执行主动流程
                                    var priorityLevels = groupCopy.Select(p => p.RunLv).Distinct().OrderBy(lv => lv);
                                    foreach (var priority in priorityLevels)
                                    {
                                        var activeProcesses = groupCopy
                                            .Where(p => p.RunLv == priority &&
                                                        p.Enable &&
                                                        !p.IsPassiveTriggered)
                                            .ToList();

                                        if (!activeProcesses.Any()) continue;

                                        ProcessInvocationResult[] priorityResults = await Task.WhenAll(
                                            activeProcesses.Select(p => p.RunInternalWithResultAsync(
                                                    isCyclical: isCyclical,
                                                    isTriggered: false,
                                                    ct: groupCts.Token)));
                                        activeResults.AddRange(priorityResults);
                                    }

                                    // 被动流程结果由触发节点同步传播到所属主动流程结果，避免共享状态覆盖。
                                    workpieceSucceeded = activeResults.All(result => result.Succeeded);
                                }
                            }
                            finally
                            {
                                WorkpieceTerminalState terminalState = groupCts.Token.IsCancellationRequested
                                    ? WorkpieceTerminalState.Cancelled
                                    : workpieceSucceeded
                                        ? WorkpieceTerminalState.Succeeded
                                        : WorkpieceTerminalState.Faulted;
                                await CompleteWorkpieceExecutionAsync(
                                    workpieceContext,
                                    terminalState).ConfigureAwait(false);
                            }

                            // 🔁 如果不是循环模式，本组只运行一次
                            if (!isCyclical)
                                break;

                            // ⏳ 循环模式：等待间隔后开始下一轮
                            try
                            {
                                await Task.Delay(RunInterval, groupCts.Token);
                            }
                            catch (OperationCanceledException) when (groupCts.Token.IsCancellationRequested)
                            {
                                break;
                            }
                            
                        }
                    }, _cts.Token);

                    groupTasks.Add(groupTask);
                }

                // ✅ 等待所有组的任务结束（可能是取消或异常）
                await Task.WhenAll(groupTasks);

            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"方案运行异常: {ex.Message}", true);
            }
            finally
            {
                StopStartedCameraCallbackGrabbing(startedCallbackCameras);
                _cts?.Dispose();
                EndRunSession();
            }
        }

        /// <summary>
        /// 启动指定流程集合中相机图像源的回调取流，并返回本方法实际启动的相机。
        /// </summary>
        /// <param name="processes">待检查的流程集合。</param>
        /// <returns>本次启动的相机集合，调用方退出运行态时需要停止。</returns>
        public List<ICamera> StartCameraCallbackGrabbingForProcesses(IEnumerable<Process> processes)
        {
            List<ICamera> startedCameras = new List<ICamera>();
            if (processes == null)
                return startedCameras;

            List<Process> processList = processes.Where(process => process != null).ToList();
            // 单流程入口也必须看见全方案中的共享相机配置，禁止两个流程分别运行时互相改写硬件状态。
            List<Process> validationProcessList = (AllProcesses ?? new List<Process>())
                .Concat(processList)
                .Where(process => process != null)
                .Distinct()
                .ToList();
            ValidateSharedCameraParameterConsistency(validationProcessList);

            HashSet<ICamera> visitedCameras = new HashSet<ICamera>();
            foreach (Process process in processList)
            {
                if (process == null ||
                    !process.Enable ||
                    process.Nodes == null)
                {
                    continue;
                }

                foreach (NodeBase node in process.Nodes)
                {
                    NodeImageSource imageSource = node as NodeImageSource;
                    if (imageSource == null || !imageSource.Active)
                        continue;

                    NodeParamImageSoucre param = imageSource.ParamForm == null
                        ? null
                        : imageSource.ParamForm.Params as NodeParamImageSoucre;
                    ICamera camera = GetCameraCallbackCamera(param);
                    if (camera == null)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"流程【{process.ProcessName}】的图像源节点【{imageSource.NodeName}】没有可用相机，无法启动相机回调。", true);
                        continue;
                    }

                    if (!camera.IsOpen)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"相机【{camera.UserDefinedName}】尚未连接，无法启动相机回调流程【{process.ProcessName}】。", true);
                        continue;
                    }

                    if (!visitedCameras.Add(camera))
                        continue;

                    bool wasGrabbing = false;
                    try
                    {
                        wasGrabbing = camera.GetGrabStatus();
                        if (wasGrabbing)
                        {
                            // 相机可能已由设备面板提前取流，重启一次可保证 SDK 帧事件在 StartGrabbing 前完成订阅。
                            camera.StopGrabbing();
                        }

                        ApplyCameraCallbackParameters(camera, param);
                        camera.StartGrabbing();
                        if (!wasGrabbing)
                            startedCameras.Add(camera);

                        string actionText = wasGrabbing ? "已重启取流" : "已启动取流";
                        LogHelper.AddLog(MsgLevel.Info, $"相机【{camera.UserDefinedName}】{actionText}，用于流程【{process.ProcessName}】的图像源回调取图。", true);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Exception, $"启动相机【{camera.UserDefinedName}】回调取流失败：{ex.Message}", true);
                        if (wasGrabbing)
                            TryResumeCameraGrabbing(camera);
                    }
                }
            }

            return startedCameras;
        }

        /// <summary>
        /// 启动单个流程中相机图像源的回调取流。
        /// </summary>
        /// <param name="process">待检查的流程。</param>
        /// <returns>本次启动的相机集合。</returns>
        public List<ICamera> StartCameraCallbackGrabbingForProcess(Process process)
        {
            return StartCameraCallbackGrabbingForProcesses(new[] { process });
        }

        /// <summary>
        /// 回调启动失败时尽量恢复相机取流，避免已在取流的相机被异常中断。
        /// </summary>
        /// <param name="camera">需要恢复的相机对象。</param>
        private static void TryResumeCameraGrabbing(ICamera camera)
        {
            if (camera == null || !camera.IsOpen)
                return;

            try
            {
                if (!camera.GetGrabStatus())
                    camera.StartGrabbing();
            }
            catch (Exception resumeEx)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"恢复相机【{camera.UserDefinedName}】取流失败：{resumeEx.Message}", true);
            }
        }

        /// <summary>
        /// 停止本次运行态中由系统主动启动的相机取流。
        /// </summary>
        /// <param name="startedCameras">本次启动的相机集合。</param>
        public void StopStartedCameraCallbackGrabbing(IEnumerable<ICamera> startedCameras)
        {
            if (startedCameras == null)
                return;

            HashSet<ICamera> visitedCameras = new HashSet<ICamera>();
            foreach (ICamera camera in startedCameras)
            {
                if (camera == null || !visitedCameras.Add(camera))
                    continue;

                try
                {
                    if (camera.IsOpen && camera.GetGrabStatus())
                    {
                        camera.StopGrabbing();
                        LogHelper.AddLog(MsgLevel.Info, $"相机【{camera.UserDefinedName}】已停止相机回调取流。", true);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"停止相机【{camera.UserDefinedName}】回调取流失败：{ex.Message}", true);
                }
            }
        }

        /// <summary>
        /// 按方案保存的相机名称解析当前设备对象，并刷新图像源中的运行时引用。
        /// </summary>
        /// <param name="param">图像源方案参数。</param>
        /// <returns>当前方案设备列表中的相机；找不到时返回null。</returns>
        public ICamera ResolveImageSourceCamera(NodeParamImageSoucre param)
        {
            if (param == null ||
                param.ImageSource != "相机" ||
                string.IsNullOrWhiteSpace(param.CameraName))
            {
                return null;
            }

            List<ICamera> cameras = CameraDevices;
            if (param.Camera != null &&
                cameras.Contains(param.Camera) &&
                string.Equals(param.Camera.UserDefinedName, param.CameraName, StringComparison.Ordinal))
            {
                return param.Camera;
            }

            ICamera resolvedCamera = cameras.FirstOrDefault(camera =>
                camera != null &&
                string.Equals(camera.UserDefinedName, param.CameraName, StringComparison.Ordinal));
            param.Camera = resolvedCamera;
            return resolvedCamera;
        }

        /// <summary>
        /// 相机连接后尝试应用第一个匹配图像源的方案参数。
        /// </summary>
        /// <param name="camera">刚完成连接的相机。</param>
        /// <returns>找到匹配方案且参数写入成功时返回true。</returns>
        public bool TryApplyImageSourceParametersForCamera(ICamera camera)
        {
            if (camera == null || !camera.IsOpen)
                return false;

            NodeParamImageSoucre selectedParam = null;
            string selectedOwner = null;
            foreach (Process process in AllProcesses ?? new List<Process>())
            {
                if (process?.Nodes == null || !process.Enable)
                    continue;

                foreach (NodeBase node in process.Nodes)
                {
                    NodeImageSource imageSource = node as NodeImageSource;
                    NodeParamImageSoucre param = imageSource?.ParamForm?.Params as NodeParamImageSoucre;
                    if (imageSource == null ||
                        !imageSource.Active ||
                        param == null ||
                        param.ImageSource != "相机" ||
                        !string.Equals(param.CameraName, camera.UserDefinedName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string currentOwner = $"流程【{process.ProcessName}】节点【{imageSource.ID}.{imageSource.NodeName}】";
                    if (selectedParam != null && !HaveEquivalentCameraParameters(selectedParam, param))
                    {
                        LogHelper.AddLog(
                            MsgLevel.Exception,
                            $"相机【{camera.UserDefinedName}】存在互相冲突的图像源参数：{selectedOwner}与{currentOwner}。连接后未选择任意一组参数，请先统一配置。",
                            true);
                        return false;
                    }

                    param.Camera = camera;
                    if (selectedParam == null)
                    {
                        selectedParam = param;
                        selectedOwner = currentOwner;
                    }
                }
            }

            if (selectedParam == null)
                return false;

            try
            {
                ApplyCameraCallbackParameters(camera, selectedParam);
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"相机【{camera.UserDefinedName}】连接后已应用{selectedOwner}的图像源方案参数：曝光={selectedParam.ExposureTime}us，增益={selectedParam.Gain}。",
                    true);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"相机【{camera.UserDefinedName}】连接后应用{selectedOwner}的图像源方案参数失败：{ex.Message}",
                    true);
                return false;
            }
        }

        /// <summary>启动相机自动重连守护线程。</summary>
        private void StartCameraReconnectWatchdog()
        {
            lock (_cameraReconnectWatchdogSync)
            {
                if (_cameraReconnectWatchdogThread != null && _cameraReconnectWatchdogThread.IsAlive)
                    return;

                Volatile.Write(ref _cameraReconnectWatchdogStopRequested, 0);
                _cameraReconnectWatchdogThread = new Thread(CameraReconnectWatchdogLoop)
                {
                    IsBackground = true,
                    Name = "CameraReconnectWatchdog",
                    Priority = ThreadPriority.BelowNormal
                };
                _cameraReconnectWatchdogThread.Start();
            }
        }

        /// <summary>循环检查需要保持连接的相机，断线后尝试重新枚举打开。</summary>
        private void CameraReconnectWatchdogLoop()
        {
            while (Volatile.Read(ref _cameraReconnectWatchdogStopRequested) == 0)
            {
                try
                {
                    CheckCameraReconnectWatchdogOnce();
                }
                catch (Exception exception)
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"相机自动重连守护线程异常：{exception.Message}", true);
                }

                Thread.Sleep(CameraReconnectWatchdogIntervalMilliseconds);
            }
        }

        /// <summary>执行一次相机连接检查和自动重连处理。</summary>
        private void CheckCameraReconnectWatchdogOnce()
        {
            foreach (ICamera camera in GetCameraReconnectCandidates())
            {
                bool connected;
                try
                {
                    connected = camera.IsDeviceConnected();
                }
                catch
                {
                    connected = false;
                }

                if (connected && !IsCameraReconnecting(camera))
                {
                    continue;
                }

                ReconnectDisconnectedCamera(camera);
            }
        }

        /// <summary>获取需要守护连接状态的相机快照。</summary>
        /// <returns>本轮需要检查的相机列表。</returns>
        private List<ICamera> GetCameraReconnectCandidates()
        {
            try
            {
                if (_isResetting || AllDevices == null)
                    return new List<ICamera>();

                return AllDevices
                    .OfType<ICamera>()
                    .Where(camera => camera != null && (camera.IsOpen || camera.RestoreConnectionRequested))
                    .ToList();
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"获取相机自动重连列表失败：{exception.Message}", true);
                return new List<ICamera>();
            }
        }

        /// <summary>隔离断线相机的旧票据并恢复连接和取流，不停止整个方案。</summary>
        /// <param name="camera">断线相机。</param>
        private void ReconnectDisconnectedCamera(ICamera camera)
        {
            if (camera == null)
                return;

            bool shouldRestoreGrabbing = ShouldRestoreCameraGrabbing(camera);
            string cameraKey = GetCameraReconnectKey(camera);
            if (MarkCameraDisconnected(cameraKey))
            {
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"相机【{camera.UserDefinedName}】连接异常，自动重连守护已介入：SN={camera.SN}，IP={camera.IP}。",
                    true);
            }

            CameraTriggerTicketRegistry.SuspendCameraForReconnect(
                CameraProductionIdentity.GetStableKey(camera));

            if (!camera.TryReconnect())
                return;

            bool parametersApplied = TryApplyImageSourceParametersForCamera(camera);
            if (IsCameraUsedByActiveImageSource(camera) && !parametersApplied)
                return;
            if (!shouldRestoreGrabbing)
            {
                ClearReportedDisconnectedCamera(cameraKey);
                return;
            }

            try
            {
                if (camera.IsOpen && !camera.GetGrabStatus())
                {
                    camera.StartGrabbing();
                    LogHelper.AddLog(MsgLevel.Info, $"相机【{camera.UserDefinedName}】自动重连后已恢复取流。", true);
                }
                if (camera.IsOpen && camera.GetGrabStatus())
                    ClearReportedDisconnectedCamera(cameraKey);
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"相机【{camera.UserDefinedName}】自动重连后恢复取流失败：{exception.Message}",
                    true);
            }
        }

        /// <summary>判断相机重连成功后是否需要恢复取流。</summary>
        /// <param name="camera">相机对象。</param>
        /// <returns>重连后应继续取流时返回true。</returns>
        private bool ShouldRestoreCameraGrabbing(ICamera camera)
        {
            try
            {
                if (camera.GetGrabStatus())
                    return true;
            }
            catch
            {
            }

            return IsCameraUsedByActiveImageSource(camera);
        }

        /// <summary>判断当前方案中是否存在启用的图像源节点引用该相机。</summary>
        /// <param name="camera">相机对象。</param>
        /// <returns>存在启用图像源节点时返回true。</returns>
        private bool IsCameraUsedByActiveImageSource(ICamera camera)
        {
            if (camera == null || AllProcesses == null)
                return false;

            foreach (Process process in AllProcesses)
            {
                if (process?.Nodes == null || !process.Enable)
                    continue;

                foreach (NodeBase node in process.Nodes)
                {
                    NodeImageSource imageSource = node as NodeImageSource;
                    NodeParamImageSoucre param = imageSource?.ParamForm?.Params as NodeParamImageSoucre;
                    if (imageSource != null &&
                        imageSource.Active &&
                        param != null &&
                        param.ImageSource == "相机" &&
                        string.Equals(param.CameraName, camera.UserDefinedName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>异步等待单相机恢复，避免断线期间循环取图失败和日志空转。</summary>
        /// <param name="camera">当前图像源使用的相机。</param>
        /// <param name="token">用户停止流程时取消等待。</param>
        internal async Task WaitForCameraReconnectAsync(ICamera camera, CancellationToken token)
        {
            while (IsCameraReconnecting(camera) && camera.RestoreConnectionRequested)
                await Task.Delay(100, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }

        /// <summary>查询相机是否仍在恢复连接、参数或取流，恢复完成前不放行新取图。</summary>
        /// <param name="camera">待查询相机。</param>
        private bool IsCameraReconnecting(ICamera camera)
        {
            lock (_cameraReconnectWatchdogSync)
                return _reportedDisconnectedCameraKeys.Contains(GetCameraReconnectKey(camera));
        }

        /// <summary>生成用于断线去重的稳定相机键。</summary>
        /// <param name="camera">相机对象。</param>
        /// <returns>优先使用品牌和序列号组成的键。</returns>
        private static string GetCameraReconnectKey(ICamera camera)
        {
            if (camera == null)
                return "UNKNOWN";

            if (!string.IsNullOrWhiteSpace(camera.SN))
                return $"{camera.Brand}|SN|{camera.SN}";

            if (!string.IsNullOrWhiteSpace(camera.UserDefinedName))
                return $"{camera.Brand}|NAME|{camera.UserDefinedName}";

            return camera.GetHashCode().ToString();
        }

        /// <summary>登记相机断线状态。</summary>
        /// <param name="cameraKey">相机去重键。</param>
        /// <returns>本次首次登记断线时返回true。</returns>
        private bool MarkCameraDisconnected(string cameraKey)
        {
            lock (_cameraReconnectWatchdogSync)
                return _reportedDisconnectedCameraKeys.Add(cameraKey ?? "UNKNOWN");
        }

        /// <summary>清除相机断线报告状态。</summary>
        /// <param name="cameraKey">相机去重键。</param>
        private void ClearReportedDisconnectedCamera(string cameraKey)
        {
            lock (_cameraReconnectWatchdogSync)
                _reportedDisconnectedCameraKeys.Remove(cameraKey ?? "UNKNOWN");
        }

        /// <summary>
        /// 在触碰相机硬件前验证同一相机的全部启用图像源使用一致参数。
        /// </summary>
        /// <param name="processes">本次准备运行的流程。</param>
        private static void ValidateSharedCameraParameterConsistency(IEnumerable<Process> processes)
        {
            Dictionary<string, NodeParamImageSoucre> parametersByCamera =
                new Dictionary<string, NodeParamImageSoucre>(StringComparer.Ordinal);
            Dictionary<string, string> ownersByCamera =
                new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Process process in processes)
            {
                if (process?.Nodes == null || !process.Enable)
                    continue;

                foreach (NodeBase node in process.Nodes)
                {
                    NodeImageSource imageSource = node as NodeImageSource;
                    NodeParamImageSoucre param = imageSource?.ParamForm?.Params as NodeParamImageSoucre;
                    if (imageSource == null ||
                        !imageSource.Active ||
                        param == null ||
                        param.ImageSource != "相机" ||
                        string.IsNullOrWhiteSpace(param.CameraName))
                    {
                        continue;
                    }

                    string currentOwner = $"流程【{process.ProcessName}】节点【{imageSource.ID}.{imageSource.NodeName}】";
                    if (!parametersByCamera.TryGetValue(param.CameraName, out NodeParamImageSoucre existingParam))
                    {
                        parametersByCamera.Add(param.CameraName, param);
                        ownersByCamera.Add(param.CameraName, currentOwner);
                        continue;
                    }

                    if (!HaveEquivalentCameraParameters(existingParam, param))
                    {
                        throw new InvalidOperationException(
                            $"相机【{param.CameraName}】被多个启用图像源配置为不同参数：{ownersByCamera[param.CameraName]}与{currentOwner}。请统一曝光、增益、触发、延迟和超时后再运行。");
                    }
                }
            }
        }

        /// <summary>
        /// 比较两个图像源是否会向同一相机写入完全一致的硬件参数。
        /// </summary>
        /// <param name="left">第一组图像源参数。</param>
        /// <param name="right">第二组图像源参数。</param>
        /// <returns>相机硬件参数一致时返回true。</returns>
        private static bool HaveEquivalentCameraParameters(
            NodeParamImageSoucre left,
            NodeParamImageSoucre right)
        {
            TriggerSource leftSource = NormalizeImageSourceTriggerSource(left.TriggerSource);
            TriggerSource rightSource = NormalizeImageSourceTriggerSource(right.TriggerSource);
            bool triggerEdgeMatches = !IsImageSourceHardwareTrigger(leftSource) ||
                left.TriggerEdge == right.TriggerEdge;
            return leftSource == rightSource &&
                triggerEdgeMatches &&
                left.TriggerDelay == right.TriggerDelay &&
                left.ExposureTime.Equals(right.ExposureTime) &&
                left.Gain.Equals(right.Gain) &&
                left.TimeOut == right.TimeOut;
        }

        /// <summary>
        /// 把旧方案Auto触发源归一为明确的软件触发源。
        /// </summary>
        /// <param name="triggerSource">方案保存的触发源。</param>
        /// <returns>相机实际使用的触发源。</returns>
        private static TriggerSource NormalizeImageSourceTriggerSource(TriggerSource triggerSource)
        {
            return triggerSource == TriggerSource.Auto ? TriggerSource.SOFT : triggerSource;
        }

        /// <summary>
        /// 判断图像源触发源是否为线路硬触发。
        /// </summary>
        /// <param name="triggerSource">归一后的触发源。</param>
        /// <returns>Line0至Line4时返回true。</returns>
        private static bool IsImageSourceHardwareTrigger(TriggerSource triggerSource)
        {
            return triggerSource >= TriggerSource.LINE0 && triggerSource <= TriggerSource.LINE4;
        }

        /// <summary>
        /// 从图像源参数中获取用于相机回调的当前相机对象。
        /// </summary>
        /// <param name="param">图像源参数。</param>
        /// <returns>可用相机对象。</returns>
        private ICamera GetCameraCallbackCamera(NodeParamImageSoucre param)
        {
            if (param == null || param.ImageSource != "相机")
                return null;

            return ResolveImageSourceCamera(param);
        }

        /// <summary>
        /// 启动取流前应用图像源中保存的相机参数，保证频闪配置和回调模式一致。
        /// </summary>
        /// <param name="camera">相机对象。</param>
        /// <param name="param">图像源参数。</param>
        private static void ApplyCameraCallbackParameters(ICamera camera, NodeParamImageSoucre param)
        {
            if (camera == null || param == null)
                return;

            TriggerSource effectiveTriggerSource = param.TriggerSource == TriggerSource.Auto
                ? TriggerSource.SOFT
                : param.TriggerSource;
            camera.SetTriggerMode(param.TriggerModel);
            camera.SetTriggerSource(effectiveTriggerSource);
            if (effectiveTriggerSource >= TriggerSource.LINE0 && effectiveTriggerSource <= TriggerSource.LINE4)
                camera.SetTriggerEdge(param.TriggerEdge);
            camera.SetTriggerDelay(param.TriggerDelay);
            camera.SetExposureTime(param.ExposureTime);
            camera.SetGain(param.Gain);
            camera.GetImageTimeOut = param.TimeOut;
        }

        public async Task Run(bool isCyclical = false,bool is1=false)
        {
            if (AllProcesses == null || AllProcesses.Count == 0)
                return;

            if (!TryBeginRunSession())
                return;

            CancellationTokenSource _cts = null;

            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(Solution.Instance.CancellationToken);
                await Task.Run(() =>
                {
                    while (!_cts.IsCancellationRequested) { }
                });
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"方案运行异常: {ex.Message}", true);
            }
            finally
            {
                _cts?.Dispose();
                EndRunSession();
            }
        }

        /// <summary>
        /// 尝试登记一个方案运行会话，重置期间拒绝新会话。
        /// </summary>
        /// <returns>成功登记返回true。</returns>
        private bool TryBeginRunSession()
        {
            bool isFirstSession;
            return TryBeginRunSession(out isFirstSession);
        }

        /// <summary>
        /// 尝试登记方案运行会话，并原子返回当前会话是否为第一条活动会话。
        /// </summary>
        /// <param name="isFirstSession">登记成功且此前没有活动会话时返回true。</param>
        /// <returns>成功登记返回true。</returns>
        private bool TryBeginRunSession(out bool isFirstSession)
        {
            CancellationTokenSource retiredTokenSource = null;
            isFirstSession = false;
            try
            {
                lock (_runLifecycleSync)
                {
                    if (_isResetting)
                        return false;

                    if (_activeRunSessionCount != 0 &&
                        (_runFaultStopRequested || _cancellationTokenSource.IsCancellationRequested))
                    {
                        return false;
                    }

                    if (_activeRunSessionCount == 0 &&
                        (_runFaultStopRequested || _cancellationTokenSource.IsCancellationRequested))
                    {
                        retiredTokenSource = _cancellationTokenSource;
                        _cancellationTokenSource = new CancellationTokenSource();
                        _runFaultStopRequested = false;
                    }
                    isFirstSession = _activeRunSessionCount == 0;

                    // 首会话初始化全部成功后才登记活动计数；任一步异常都保持零活动并允许重试。
                    if (isFirstSession)
                    {
                        try
                        {
                            WorkpieceIdentityGenerator.ResetSession();
                            RenewOrderedSignalCoordinator();
                            RenewCameraProductionPipeline();
                            RefreshRuntimeResourceProfile(true);
                            CameraTriggerTicketRegistry.UpdateBufferedHardwareFrameBudget(
                                GetBufferedHardwareFrameBudgetBytes(CurrentResourceProfile));
                        }
                        catch
                        {
                            isFirstSession = false;
                            IsRunning = false;
                            _allRunsStopped.Set();
                            throw;
                        }
                    }

                    _activeRunSessionCount++;
                    _allRunsStopped.Reset();
                    IsRunning = true;
                }
                return true;
            }
            finally
            {
                retiredTokenSource?.Dispose();
            }
        }

        /// <summary>
        /// 结束一个方案运行会话，并在最后一个会话退出时通知重置线程。
        /// </summary>
        private void EndRunSession()
        {
            lock (_runLifecycleSync)
            {
                if (_activeRunSessionCount > 0)
                    _activeRunSessionCount--;
                if (_activeRunSessionCount != 0)
                    return;

                CameraTriggerTicketRegistry registry;
                lock (_cameraProductionPipelineSync)
                    registry = _cameraTriggerTicketRegistry;
                registry?.StopAcceptingProductionFrames();
                IsRunning = false;
                _allRunsStopped.Set();
            }
        }

        /// <summary>
        /// 为手动流程运行、脚本运行和参数页预览登记统一运行会话。
        /// </summary>
        /// <returns>当前不处于方案重置阶段时返回true。</returns>
        internal bool TryBeginExternalRunSession()
        {
            bool isFirstSession;
            return TryBeginRunSession(out isFirstSession);
        }

        /// <summary>
        /// 结束手动流程运行、脚本运行或参数页预览登记的运行会话。
        /// </summary>
        internal void EndExternalRunSession()
        {
            EndRunSession();
        }

        /// <summary>仅在没有生产会话和方案重置时执行一次手动外设动作。</summary>
        /// <param name="action">必须具备自身有限超时的手动设备动作。</param>
        /// <returns>取得运行生命周期门禁并执行返回true；生产运行或重置中返回false。</returns>
        internal bool TryExecuteManualExternalSignal(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            lock (_runLifecycleSync)
            {
                if (_isResetting || _activeRunSessionCount != 0)
                    return false;
                action();
                return true;
            }
        }

        /// <summary>
        /// 进入独占方案变更作用域，供加载逻辑覆盖从旧方案清理到新方案恢复的完整过程。
        /// </summary>
        /// <returns>释放时退出方案变更锁的作用域。</returns>
        internal IDisposable EnterSolutionMutationScope()
        {
            return new SolutionMutationScope(_solutionMutationSync);
        }

        /// <summary>
        /// 在方案文件读取前关闭运行入口，使门禁覆盖读取、旧方案清理和新方案恢复全过程。
        /// </summary>
        /// <returns>本次调用前运行入口处于开放状态时返回true。</returns>
        internal bool BeginSolutionLoadRunGate()
        {
            lock (_runLifecycleSync)
            {
                bool acquiredOpenGate = !_isResetting;
                _isResetting = true;
                return acquiredOpenGate;
            }
        }

        /// <summary>
        /// 方案读取失败但尚未清理旧方案，或新方案恢复已经结束时重新开放运行入口。
        /// </summary>
        internal void EndSolutionLoadRunGate()
        {
            EndSolutionReset();
        }

        /// <summary>
        /// 使用可释放作用域管理Monitor，避免异常路径遗留方案变更锁。
        /// </summary>
        private sealed class SolutionMutationScope : IDisposable
        {
            /// <summary>当前作用域持有的同步对象；释放后原子置空。</summary>
            private object _syncRoot;

            /// <summary>
            /// 进入指定方案变更锁。
            /// </summary>
            /// <param name="syncRoot">方案变更同步对象。</param>
            public SolutionMutationScope(object syncRoot)
            {
                _syncRoot = syncRoot ?? throw new ArgumentNullException(nameof(syncRoot));
                Monitor.Enter(_syncRoot);
            }

            /// <summary>退出当前作用域持有的方案变更锁；重复调用安全。</summary>
            public void Dispose()
            {
                object syncRoot = Interlocked.Exchange(ref _syncRoot, null);
                if (syncRoot != null)
                    Monitor.Exit(syncRoot);
            }
        }

        /// <summary>
        /// 禁止新运行会话、取消当前流程，并等待全部运行线程退出。
        /// </summary>
        /// <param name="timeoutMilliseconds">最长等待毫秒数。</param>
        /// <returns>全部运行会话已经退出返回true。</returns>
        private bool TryStopRunsForReset(int timeoutMilliseconds)
        {
            CancellationTokenSource tokenSource;
            lock (_runLifecycleSync)
            {
                _isResetting = true;
                if (_activeRunSessionCount == 0)
                    _allRunsStopped.Set();
                tokenSource = _cancellationTokenSource;
            }

            CancelTokenSourceNoThrow(tokenSource, "方案重置");
            return _allRunsStopped.Wait(Math.Max(0, timeoutMilliseconds));
        }

        /// <summary>
        /// 结束方案重置状态，允许后续启动新运行会话。
        /// </summary>
        private void EndSolutionReset()
        {
            lock (_runLifecycleSync)
                _isResetting = false;
        }

        /// <summary>
        /// 请求取消指定令牌源，并隔离第三方取消回调异常，避免停止或重置流程被回调打断。
        /// </summary>
        /// <param name="tokenSource">需要取消的令牌源。</param>
        /// <param name="stage">用于日志定位的停止阶段。</param>
        private static void CancelTokenSourceNoThrow(CancellationTokenSource tokenSource, string stage)
        {
            if (tokenSource == null)
                return;

            try
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                tokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                try
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"{stage}触发取消回调时出现异常：{ex.Message}", true);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 方案运行停止；IsRunning会在真实运行任务退出后由运行会话统一更新。
        /// </summary>
        public void Stop()
        {
            TryCommitRunStop(out CancellationTokenSource tokenSource);
            CancelTokenSourceNoThrow(tokenSource, "方案停止");
        }

        /// <summary>在执行可能较慢的取消回调前提交停机门闩，阻止新入口复用当前会话令牌。</summary>
        private bool TryCommitRunStop(out CancellationTokenSource tokenSource)
        {
            lock (_runLifecycleSync)
            {
                _runFaultStopRequested = true;
                tokenSource = _cancellationTokenSource;
                return true;
            }
        }

        /// <summary>生产帧身份或资源边界失效时记录证据并请求整套方案停止。</summary>
        /// <param name="fault">相机票据注册表生成的不可恢复生产故障。</param>
        private void HandleCameraProductionFault(
            CameraTriggerTicketRegistry sourceRegistry,
            CameraProductionFault fault)
        {
            if (fault != null && !fault.RequiresSolutionStop)
            {
                lock (_cameraProductionPipelineSync)
                {
                    if (!ReferenceEquals(_cameraTriggerTicketRegistry, sourceRegistry))
                        return;
                }
                try
                {
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        $"相机生产队列报警，方案继续运行：相机={fault.CameraKey}；原因={fault.Reason}",
                        true);
                }
                catch
                {
                }
                return;
            }

            if (!TryCommitCameraProductionFaultStop(
                sourceRegistry,
                out CancellationTokenSource tokenSource))
                return;

            try
            {
                string identity = fault?.Identity?.ToString() ?? "无";
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"相机生产帧故障，已停止方案：相机={fault?.CameraKey ?? "未知"}；工件={identity}；原因={fault?.Reason ?? "未知"}",
                    true);
            }
            catch
            {
            }
            // 只取消来源注册表确认时对应的方案令牌；注册表换代后不得重新读取并误停新方案。
            CancelTokenSourceNoThrow(tokenSource, "相机生产帧故障");
        }

        /// <summary>原子确认相机故障来源并提交本会话停机门闩，阻止取消动作前新入口复用旧令牌。</summary>
        private bool TryCommitCameraProductionFaultStop(
            CameraTriggerTicketRegistry sourceRegistry,
            out CancellationTokenSource tokenSource)
        {
            lock (_runLifecycleSync)
            {
                lock (_cameraProductionPipelineSync)
                {
                    if (!ReferenceEquals(_cameraTriggerTicketRegistry, sourceRegistry))
                    {
                        tokenSource = null;
                        return false;
                    }
                    _runFaultStopRequested = true;
                    tokenSource = _cancellationTokenSource;
                    return true;
                }
            }
        }

        /// <summary>
        /// 为新方案创建独立的生产帧预算和票据注册表，并在替换后关闭旧注册表。
        /// 调用方必须已经停止流程并物理释放旧方案全部相机。
        /// </summary>
        private void RenewCameraProductionPipeline()
        {
            WorkpieceFrameMemoryBudgetManager newMemoryBudgetManager =
                new WorkpieceFrameMemoryBudgetManager();
            CameraTriggerTicketRegistry newRegistry =
                new CameraTriggerTicketRegistry(newMemoryBudgetManager);
            Action<CameraProductionFault> newFaultHandler =
                fault => HandleCameraProductionFault(newRegistry, fault);
            newRegistry.ProductionFaulted += newFaultHandler;

            CameraTriggerTicketRegistry retiredRegistry;
            Action<CameraProductionFault> retiredFaultHandler;
            lock (_cameraProductionPipelineSync)
            {
                retiredRegistry = _cameraTriggerTicketRegistry;
                retiredFaultHandler = _cameraProductionFaultHandler;
            }

            if (retiredRegistry != null)
            {
                if (retiredFaultHandler != null)
                    retiredRegistry.ProductionFaulted -= retiredFaultHandler;
                try
                {
                    // 先完成旧表关闭再发布新表；失败时恢复订阅并保留旧表，允许下次重试关闭。
                    retiredRegistry.Dispose();
                }
                catch
                {
                    if (retiredFaultHandler != null)
                        retiredRegistry.ProductionFaulted += retiredFaultHandler;
                    newRegistry.ProductionFaulted -= newFaultHandler;
                    newRegistry.Dispose();
                    throw;
                }
            }

            lock (_cameraProductionPipelineSync)
            {
                _workpieceFrameMemoryBudgetManager = newMemoryBudgetManager;
                _cameraTriggerTicketRegistry = newRegistry;
                _cameraProductionFaultHandler = newFaultHandler;
            }
        }

        /// <summary>按运行资源档案计算全部相机待执行Mat的参考报警预算。</summary>
        private static long GetBufferedHardwareFrameBudgetBytes(AutomaticResourceProfile profile)
        {
            int flowImageBudgetMb = profile?.FlowImageMemoryBudgetMb ?? 0;
            if (flowImageBudgetMb <= 0)
                return CameraTriggerTicketRegistry.DefaultBufferedHardwareFrameBudgetBytes;
            return Math.Max(
                1L,
                (long)Math.Floor(flowImageBudgetMb * 1024D * 1024D * 0.25D));
        }

        /// <summary>
        /// 加载方案；数据恢复失败只写入日志，不显示阻断操作的弹窗。
        /// </summary>
        /// <param name="configFile">需要加载的方案文件。</param>
        /// <param name="flag">是否输出详细加载信息。</param>
        /// <returns>方案及全部数据完整恢复返回 true，否则返回 false。</returns>
        public bool Load(string configFile, bool flag)
        {
            try
            {
                using (StartupProgressContext.Begin(new NullStartupProgressReporter()))
                {
                    try
                    {
                        if (!File.Exists(configFile))
                            throw new FileNotFoundException("方案文件不存在！", configFile);

                        ConfigHelper.SolLoad(configFile, flag);
                        IReadOnlyList<StartupFailure> failures = StartupProgressContext.DrainFailures();
                        if (failures.Count > 0)
                        {
                            WriteSolutionLoadLogSafely(
                                $"方案数据恢复失败，已忽略：{new StartupInitializationException(failures)}");
                            return false;
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        WriteSolutionLoadLogSafely($"方案反序列化异常，已忽略：{ex}");
                        IReadOnlyList<StartupFailure> pendingFailures = StartupProgressContext.DrainFailures();
                        if (pendingFailures.Count > 0)
                        {
                            WriteSolutionLoadLogSafely(
                                $"方案数据恢复附加异常，已忽略：{new StartupInitializationException(pendingFailures)}");
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteSolutionLoadLogSafely($"方案加载上下文异常，已忽略：{ex}");
                return false;
            }
        }

        /// <summary>
        /// 释放节点控件自身资源，使保存节点等派生节点能够停止专属后台状态。
        /// </summary>
        /// <param name="node">需要释放的节点控件。</param>
        private void DisposeNodeControl(NodeBase node)
        {
            try
            {
                node.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"释放节点({node.ID}.{node.NodeName})控件资源失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 获取或创建全方案共享的图像保存工作池。
        /// </summary>
        /// <returns>已经按当前资源档案启动的保存工作池。</returns>
        internal ImageQueueProcessor GetOrCreateImageSaveQueueProcessor()
        {
            lock (_imageSaveQueueSync)
            {
                if (_imageSaveQueueProcessor != null)
                {
                    if (!_imageSaveQueueProcessor.IsStopped)
                        return _imageSaveQueueProcessor;

                    if (!_imageSaveQueueProcessor.IsFullyStopped)
                    {
                        throw new InvalidOperationException(
                            "上一保存工作池仍在停止中，禁止创建重叠工作池。请等待当前保存任务退出后重试。");
                    }

                    _imageSaveQueueProcessor.StopProcessing();
                    _imageSaveQueueProcessor = null;
                }

                _imageSaveQueueProcessor = CreateImageSaveQueueProcessor(CurrentResourceProfile);
                _imageSaveQueueProcessor.StartProcessing();
                return _imageSaveQueueProcessor;
            }
        }

        /// <summary>
        /// 取消指定保存节点仍在共享队列中等待的任务。
        /// </summary>
        /// <param name="ownerKey">保存节点唯一标识。</param>
        /// <returns>取消并释放的等待任务数量。</returns>
        internal int CancelPendingImageSaveTasks(string ownerKey)
        {
            ImageQueueProcessor processor;
            lock (_imageSaveQueueSync)
                processor = _imageSaveQueueProcessor;

            return processor?.CancelOwner(ownerKey) ?? 0;
        }

        /// <summary>
        /// 获取当前共享保存工作池的只读诊断快照；工作池尚未创建时返回空。
        /// </summary>
        /// <returns>保存队列与磁盘保护快照，或空。</returns>
        internal ImageSaveQueueDiagnosticsSnapshot GetImageSaveQueueDiagnosticsSnapshot()
        {
            lock (_imageSaveQueueSync)
                return _imageSaveQueueProcessor?.GetDiagnosticsSnapshot();
        }

        /// <summary>
        /// 为明确的CPU重任务取得全方案共享许可。
        /// </summary>
        /// <param name="workloadKind">CPU工作类型。</param>
        /// <param name="cancellationToken">取消等待令牌。</param>
        /// <returns>必须释放的CPU工作许可。</returns>
        internal Task<ICpuWorkLease> AcquireCpuWorkAsync(
            CpuWorkloadKind workloadKind,
            CancellationToken cancellationToken)
        {
            return _cpuWorkScheduler.AcquireAsync(workloadKind, cancellationToken);
        }

        /// <summary>
        /// 创建已经绑定规范化顺序域和当前生产会话身份的工件执行上下文。
        /// </summary>
        /// <param name="orderDomain">生产线或流程组顺序域。</param>
        /// <param name="flowRevision">工件准入时冻结的流程版本。</param>
        /// <param name="triggerSource">触发来源。</param>
        /// <param name="cancellationToken">本轮停止令牌。</param>
        /// <returns>尚未封口的新工件上下文。</returns>
        internal WorkpieceExecutionContext CreateWorkpieceExecutionContext(
            string orderDomain,
            long flowRevision,
            string triggerSource,
            CancellationToken cancellationToken)
        {
            WorkpieceIdentity identity = WorkpieceIdentityGenerator.Next(orderDomain);
            WorkpieceExecutionContext context = new WorkpieceExecutionContext(
                identity,
                flowRevision,
                identity.OrderDomain,
                triggerSource,
                DateTime.UtcNow,
                cancellationToken);
            OrderedSignalCoordinator.RegisterWorkpiece(context);
            return context;
        }

        /// <summary>请求当前工件封口，并由有序协调器在信号排空后推进同顺序域下一工件。</summary>
        /// <param name="context">本入口创建并已登记的工件上下文。</param>
        /// <param name="terminalState">流程请求的明确终态。</param>
        /// <returns>全部在途信号结束且协调器完成顺序推进后完成的任务。</returns>
        internal Task CompleteWorkpieceExecutionAsync(
            WorkpieceExecutionContext context,
            WorkpieceTerminalState terminalState)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return OrderedSignalCoordinator.CompleteWorkpieceAsync(context, terminalState);
        }

        /// <summary>建立新的生产会话协调器，并停止旧协调器全部尚未开始的设备动作。</summary>
        private void RenewOrderedSignalCoordinator()
        {
            OrderedSignalCoordinator retiredCoordinator;
            lock (_orderedSignalCoordinatorSync)
            {
                retiredCoordinator = _orderedSignalCoordinator;
                _orderedSignalCoordinator = CreateOrderedSignalCoordinator();
            }
            if (retiredCoordinator != null)
                retiredCoordinator.OrderGapDetected -= HandleOrderedSignalGapDetected;
            retiredCoordinator?.Dispose();
        }

        /// <summary>创建绑定当前方案停机处理器的有序信号协调器。</summary>
        private OrderedSignalCoordinator CreateOrderedSignalCoordinator()
        {
            var coordinator = new OrderedSignalCoordinator(
                TDJS_Vision.ResourceManagement.OrderedSignalCoordinator.DefaultSignalOrderGapTimeoutMs);
            coordinator.OrderGapDetected += HandleOrderedSignalGapDetected;
            return coordinator;
        }

        /// <summary>当前会话出现持续顺序缺口时记录完整证据并取消整套方案。</summary>
        private void HandleOrderedSignalGapDetected(
            object sender,
            OrderedSignalGapEventArgs eventArgs)
        {
            if (!TryCommitOrderedSignalGapStop(sender, out CancellationTokenSource tokenSource))
                return;

            try
            {
                LogHelper.AddLog(
                    MsgLevel.Fatal,
                    eventArgs?.Message ?? "有序信号出现未知顺序缺口，已停止方案。",
                    true);
            }
            catch
            {
            }
            CancelTokenSourceNoThrow(tokenSource, "有序信号顺序缺口");
        }

        /// <summary>原子确认报警来源并提交本会话故障停机状态，返回必须取消的旧令牌源。</summary>
        private bool TryCommitOrderedSignalGapStop(
            object sender,
            out CancellationTokenSource tokenSource)
        {
            lock (_runLifecycleSync)
            {
                lock (_orderedSignalCoordinatorSync)
                {
                    if (!ReferenceEquals(sender, _orderedSignalCoordinator))
                    {
                        tokenSource = null;
                        return false;
                    }
                    _runFaultStopRequested = true;
                    tokenSource = _cancellationTokenSource;
                    return true;
                }
            }
        }

        /// <summary>
        /// 判断异步分支携带的上下文是否仍属于当前活动生产会话并允许继续继承。
        /// </summary>
        /// <param name="context">异步分支携带的候选上下文。</param>
        /// <returns>上下文仍活动、未取消且会话纪元一致时返回true。</returns>
        internal bool CanInheritWorkpieceContext(WorkpieceExecutionContext context)
        {
            return context != null &&
                context.TerminalState == WorkpieceTerminalState.Active &&
                !context.IsCompletionRequested &&
                !context.CancellationToken.IsCancellationRequested &&
                context.Identity.SessionEpoch == WorkpieceIdentityGenerator.SessionEpoch;
        }

        /// <summary>
        /// 原子取得当前会话工件的在途流程分支租约；会话重置竞态下会立即归还租约。
        /// </summary>
        /// <param name="context">待继承的工件上下文。</param>
        /// <param name="lease">成功时返回必须释放的分支租约。</param>
        /// <returns>上下文属于当前会话且成功阻止父工件提前封口时返回true。</returns>
        internal bool TryAcquireInheritedWorkpieceContext(
            WorkpieceExecutionContext context,
            out IWorkpieceExecutionLease lease)
        {
            lease = null;
            if (!CanInheritWorkpieceContext(context))
                return false;
            if (!context.TryAcquireExecutionLease(out lease))
                return false;
            if (context.Identity.SessionEpoch == WorkpieceIdentityGenerator.SessionEpoch)
                return true;

            lease.Dispose();
            lease = null;
            return false;
        }

        /// <summary>
        /// 获取当前CPU调度诊断快照，供Debug智能体和专项测试读取。
        /// </summary>
        /// <returns>当前并发、排队、CPU和等待耗时快照。</returns>
        internal CpuWorkSchedulerSnapshot GetCpuWorkSchedulerSnapshot()
        {
            return _cpuWorkScheduler.GetSnapshot();
        }

        /// <summary>
        /// 记录保存任务将持有的完整图像资源字节数，供后续运行自动计算队列容量。
        /// </summary>
        /// <param name="estimatedBytes">完整OutputImage租约的估算字节数。</param>
        internal void RecordImageSaveResourceSample(long estimatedBytes)
        {
            _imageSaveSizeSampler.Record(estimatedBytes);
        }

        /// <summary>
        /// 使用当前资源档案重配空闲保存工作池；存在活动任务时保持原边界直至下一次重置。
        /// </summary>
        /// <param name="profile">最新自动资源档案。</param>
        /// <param name="allowFirstRunSession">当前是否处于首个会话开始节点执行前的安全初始化阶段。</param>
        /// <returns>保存工作池应用最新档案的状态。</returns>
        private ImageSaveProfileApplyState ReconfigureIdleImageSaveQueueProcessor(
            AutomaticResourceProfile profile,
            bool allowFirstRunSession)
        {
            lock (_runLifecycleSync)
            {
                bool canReconfigure = _activeRunSessionCount == 0 ||
                    (allowFirstRunSession && _activeRunSessionCount == 1);
                if (_isResetting || !canReconfigure)
                    return ImageSaveProfileApplyState.Pending;

                lock (_imageSaveQueueSync)
                {
                    if (_imageSaveQueueProcessor == null)
                        return ImageSaveProfileApplyState.UseLatestWhenCreated;

                    int workerCount = GetImageSaveWorkerCount(profile);
                    int capacity = GetImageSaveQueueCapacity(profile);
                    long memoryBudgetBytes = GetImageSaveQueueMemoryBudgetBytes(profile);
                    int lowSpaceThresholdMb = GetSaveDiskLowSpaceMb(profile);
                    int criticalSpaceThresholdMb = GetSaveDiskCriticalSpaceMb(profile, lowSpaceThresholdMb);
                    int slowWriteThresholdMs = GetSaveSlowThresholdMs(profile);
                    int probeIntervalMs = GetSaveDiskProbeIntervalMs(profile);
                    int shutdownTimeoutMs = GetShutdownResourceDrainTimeoutMs(profile);
                    if (_imageSaveQueueProcessor.ConfigurationMatches(
                        workerCount,
                        capacity,
                        memoryBudgetBytes,
                        lowSpaceThresholdMb,
                        criticalSpaceThresholdMb,
                        slowWriteThresholdMs,
                        probeIntervalMs,
                        shutdownTimeoutMs))
                        return ImageSaveProfileApplyState.Applied;

                    if (!_imageSaveQueueProcessor.IsIdle)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Warn,
                            "保存工作池仍有待处理任务，本轮继续沿用原资源边界；方案重置后应用最新档案。",
                            true);
                        return ImageSaveProfileApplyState.Pending;
                    }

                    if (!_imageSaveQueueProcessor.StopProcessing() || !_imageSaveQueueProcessor.IsFullyStopped)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Warn,
                            "保存工作池在资源重配期限内未完全退出，本轮保留旧工作池并拒绝创建重叠工作池。",
                            true);
                        return ImageSaveProfileApplyState.Pending;
                    }

                    _imageSaveQueueProcessor = CreateImageSaveQueueProcessor(profile);
                    _imageSaveQueueProcessor.StartProcessing();
                    return ImageSaveProfileApplyState.Applied;
                }
            }
        }

        /// <summary>
        /// 停止并清空全方案共享保存工作池。
        /// </summary>
        private bool StopImageSaveQueueProcessor()
        {
            ImageQueueProcessor processor;
            lock (_imageSaveQueueSync)
                processor = _imageSaveQueueProcessor;

            if (processor == null)
                return true;

            bool stopped = processor.StopProcessing() && processor.IsFullyStopped;
            if (!stopped)
                return false;

            lock (_imageSaveQueueSync)
            {
                if (ReferenceEquals(_imageSaveQueueProcessor, processor))
                    _imageSaveQueueProcessor = null;
            }

            return true;
        }

        /// <summary>
        /// 根据自动资源档案创建尚未启动的保存工作池。
        /// </summary>
        /// <param name="profile">自动资源档案；为空时使用保守值。</param>
        /// <returns>尚未启动的保存工作池。</returns>
        private ImageQueueProcessor CreateImageSaveQueueProcessor(AutomaticResourceProfile profile)
        {
            int lowSpaceThresholdMb = GetSaveDiskLowSpaceMb(profile);
            IImageSaveStorageGuard storageGuard = new CachedImageSaveStorageGuard(
                new DriveImageSaveDiskSpaceProbe(),
                lowSpaceThresholdMb,
                GetSaveDiskCriticalSpaceMb(profile, lowSpaceThresholdMb),
                GetSaveSlowThresholdMs(profile),
                GetSaveDiskProbeIntervalMs(profile));
            return new ImageQueueProcessor(
                GetImageSaveWorkerCount(profile),
                GetImageSaveQueueCapacity(profile),
                GetImageSaveQueueMemoryBudgetBytes(profile),
                GetShutdownResourceDrainTimeoutMs(profile),
                "全方案共享",
                storageGuard,
                _cpuWorkScheduler);
        }

        /// <summary>
        /// 取得自动档案中的保存工作线程数量。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <returns>至少为1的保存线程数。</returns>
        private static int GetImageSaveWorkerCount(AutomaticResourceProfile profile)
        {
            return Math.Max(1, profile?.ImageSaveWorkerCount ?? 1);
        }

        /// <summary>取得停止运行和保存工作池时使用的资源排空等待毫秒数。</summary>
        private static int GetShutdownResourceDrainTimeoutMs(AutomaticResourceProfile profile)
        {
            return Math.Max(0, Math.Min(60000, profile?.ShutdownResourceDrainTimeoutMs ?? 5000));
        }

        /// <summary>
        /// 取得自动档案中的保存队列任务容量。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <returns>至少为1的任务容量。</returns>
        private static int GetImageSaveQueueCapacity(AutomaticResourceProfile profile)
        {
            return Math.Max(1, profile?.SaveQueueCalculatedCapacity ?? 8);
        }

        /// <summary>
        /// 取得自动档案中的保存队列字节预算。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <returns>至少为1MB的字节预算。</returns>
        private static long GetImageSaveQueueMemoryBudgetBytes(AutomaticResourceProfile profile)
        {
            int budgetMb = Math.Max(1, profile?.SaveQueueMemoryBudgetMb ?? 128);
            return (long)budgetMb * 1024L * 1024L;
        }

        /// <summary>
        /// 取得普通图片停止保存的磁盘低空间水位。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <returns>至少为2MB的低空间水位。</returns>
        private static int GetSaveDiskLowSpaceMb(AutomaticResourceProfile profile)
        {
            return Math.Max(2, profile?.SaveDiskLowSpaceMb ?? CachedImageSaveStorageGuard.DefaultLowSpaceThresholdMb);
        }

        /// <summary>
        /// 取得全部图片停止保存的磁盘严重空间水位。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <param name="lowSpaceThresholdMb">已经规范化的低空间水位。</param>
        /// <returns>至少为1MB且低于低空间水位的严重水位。</returns>
        private static int GetSaveDiskCriticalSpaceMb(AutomaticResourceProfile profile, int lowSpaceThresholdMb)
        {
            int configuredValue = profile?.SaveDiskCriticalSpaceMb ?? CachedImageSaveStorageGuard.DefaultCriticalSpaceThresholdMb;
            return Math.Max(1, Math.Min(configuredValue, lowSpaceThresholdMb - 1));
        }

        /// <summary>
        /// 取得单个目标路径慢写阈值。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <returns>至少为1ms的慢写阈值。</returns>
        private static int GetSaveSlowThresholdMs(AutomaticResourceProfile profile)
        {
            return Math.Max(1, profile?.SaveSlowThresholdMs ?? CachedImageSaveStorageGuard.DefaultSlowWriteThresholdMs);
        }

        /// <summary>
        /// 取得磁盘空间探测缓存时间。
        /// </summary>
        /// <param name="profile">自动资源档案。</param>
        /// <returns>非负的缓存毫秒数。</returns>
        private static int GetSaveDiskProbeIntervalMs(AutomaticResourceProfile profile)
        {
            return Math.Max(0, profile?.SaveDiskProbeIntervalMs ?? CachedImageSaveStorageGuard.DefaultProbeIntervalMs);
        }

        /// <summary>
        /// 生成系统设置界面使用的自动推荐与最终档案预览，不修改当前运行组件。
        /// </summary>
        /// <param name="resetHardwareCache">是否重新读取本机硬件信息。</param>
        /// <returns>自动值、机器设置和最终值的同批快照。</returns>
        public ResourceProfileBuildResult BuildRuntimeResourceProfilePreview(
            MachinePerformanceResourceSettings settings,
            bool resetHardwareCache)
        {
            IRuntimeResourceProfileProvider provider;
            ResourceWorkloadSnapshot workload;
            lock (_runLifecycleSync)
            {
                provider = ResourceProfileProvider ?? RuntimeResourceProfileProvider.CreateDefault();
                ResourceProfileProvider = provider;
                workload = CreateResourceWorkloadSnapshot();
            }

            if (resetHardwareCache)
                provider.ResetHardwareCache();
            return provider.BuildProfileResult(workload, settings);
        }

        /// <summary>
        /// 把已经保存的机器性能设置应用到允许安全热更新的运行组件。
        /// </summary>
        /// <returns>CPU、显示、采样和保存工作池各自的应用状态。</returns>
        public RuntimeResourceProfileApplyResult ApplyMachinePerformanceResourceSettings(
            MachinePerformanceResourceSettings settings)
        {
            lock (_runLifecycleSync)
            {
                if (_isResetting)
                {
                    return new RuntimeResourceProfileApplyResult
                    {
                        Success = true,
                        EffectiveProfile = CurrentResourceProfile,
                        CpuAndDisplayApplied = false,
                        ImageSampleApplied = false,
                        ImageSaveState = ImageSaveProfileApplyState.Pending,
                        Message = "机器设置已保存；方案正在重置，全部运行参数将在下一轮安全应用。"
                    };
                }

                RuntimeResourceProfileApplyResult result =
                    ApplyRuntimeResourceProfileCore(false, settings);
                if (!result.Success)
                    LogHelper.AddLog(MsgLevel.Warn, result.Message, true);
                return result;
            }
        }

        /// <summary>
        /// 按当前启用流程和相机规模刷新运行资源档案；失败只告警，不阻止方案运行。
        /// </summary>
        /// <param name="allowFirstRunSession">是否处于首个会话执行节点前的安全初始化阶段。</param>
        private void RefreshRuntimeResourceProfile(bool allowFirstRunSession)
        {
            RuntimeResourceProfileApplyResult result =
                ApplyRuntimeResourceProfileCore(allowFirstRunSession, null);
            if (!result.Success)
                LogHelper.AddLog(MsgLevel.Warn, result.Message, true);
        }

        /// <summary>生成最终档案并更新运行组件；调用方必须持有运行生命周期锁。</summary>
        private RuntimeResourceProfileApplyResult ApplyRuntimeResourceProfileCore(
            bool allowFirstRunSession,
            MachinePerformanceResourceSettings settings)
        {
            try
            {
                IRuntimeResourceProfileProvider provider = ResourceProfileProvider ?? RuntimeResourceProfileProvider.CreateDefault();
                ResourceProfileProvider = provider;
                ResourceWorkloadSnapshot workload = CreateResourceWorkloadSnapshot();
                ResourceProfileBuildResult buildResult = settings == null
                    ? provider.BuildProfileResult(workload)
                    : provider.BuildProfileResult(workload, settings);
                AutomaticResourceProfile profile = buildResult?.EffectiveProfile;
                if (profile == null)
                {
                    return new RuntimeResourceProfileApplyResult
                    {
                        Success = false,
                        Message = "自动资源档案提供器返回空结果，本次继续按现有运行策略执行。"
                    };
                }

                CurrentResourceProfile = profile;
                bool imageSampleApplied = _activeRunSessionCount == 0 || allowFirstRunSession;
                if (imageSampleApplied)
                    EnsureImageResourceSizeSamplerCapacity(profile.ImageSizeSampleCount);

                _cpuWorkScheduler.Reconfigure(CpuWorkSchedulerOptions.FromProfile(profile));
                _cpuResourceMonitor.Reconfigure(profile.ResourceAdjustmentIntervalMs);

                HardwareResourceSnapshot hardware = profile.Hardware;
                if (hardware != null && hardware.ProbeWarnings != null)
                {
                    foreach (string warning in hardware.ProbeWarnings.Where(value => !string.IsNullOrWhiteSpace(value)))
                        LogHelper.AddLog(MsgLevel.Warn, "自动资源档案采用降级值：" + warning, true);
                }

                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【资源档案】硬件={profile.Hardware?.ToLogText()}；建议={profile.ToLogText()}；CPU调度={_cpuWorkScheduler.GetSnapshot().ToLogText()}；说明=CPU重任务与保存工作池应用档案边界，不修改线程池、CPU亲和性或AI并发",
                    true);
                ImageSaveProfileApplyState saveState =
                    ReconfigureIdleImageSaveQueueProcessor(profile, allowFirstRunSession);
                string message = saveState == ImageSaveProfileApplyState.Pending || !imageSampleApplied
                    ? "CPU与显示参数已应用；图像采样或保存工作池参数将在当前任务结束后的下一轮安全应用。"
                    : "性能与资源设置已应用。";
                return new RuntimeResourceProfileApplyResult
                {
                    Success = true,
                    EffectiveProfile = profile,
                    CpuAndDisplayApplied = true,
                    ImageSampleApplied = imageSampleApplied,
                    ImageSaveState = saveState,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                if (allowFirstRunSession)
                    CurrentResourceProfile = null;
                return new RuntimeResourceProfileApplyResult
                {
                    Success = false,
                    Message = "自动资源档案生成失败，本次继续按现有运行策略执行。原因：" + ex.Message
                };
            }
        }

        /// <summary>读取当前方案规模和图像样本，形成一次资源计算工作负载。</summary>
        private ResourceWorkloadSnapshot CreateResourceWorkloadSnapshot()
        {
            return new ResourceWorkloadSnapshot
            {
                EnabledProcessCount = AllProcesses == null
                    ? 0
                    : AllProcesses.Count(process => process != null && process.Enable),
                CameraCount = CameraDevices.Count + Camera3DDevices.Count,
                AverageImageBytes = _imageSaveSizeSampler.AverageBytes
            };
        }

        /// <summary>
        /// 当机器级采样参数变化时替换滚动采样器；参数不变时保留当前方案已采集样本。
        /// </summary>
        /// <param name="capacity">新的滚动样本容量。</param>
        private void EnsureImageResourceSizeSamplerCapacity(int capacity)
        {
            int normalizedCapacity = Math.Max(1, Math.Min(500, capacity));
            if (_imageSaveSizeSampler.Capacity == normalizedCapacity)
                return;

            _imageSaveSizeSampler = new RollingImageResourceSizeSampler(normalizedCapacity);
        }

        /// <summary>
        /// 校验指定流程能否按当前相机触发配置运行。
        /// </summary>
        /// <param name="processes">待校验的流程集合。</param>
        /// <param name="message">校验失败时供界面展示的简体中文消息。</param>
        /// <returns>全部流程配置正确时返回 true。</returns>
        public bool TryValidateCameraConfiguration(IEnumerable<Process> processes, out string message)
        {
            IProcessCameraConfigurationValidator validator = CameraConfigurationValidator ?? new ProcessCameraConfigurationValidator();
            IReadOnlyList<ProcessCameraConfigurationError> errors = validator.Validate(processes);
            if (errors.Count == 0)
            {
                message = string.Empty;
                return true;
            }

            message = "以下流程的相机触发配置有误，已停止运行：\r\n\r\n" +
                string.Join("\r\n", errors.Select(error => error.ToDisplayText()));
            return false;
        }

        /// <summary>
        /// 安全记录方案加载失败，确保日志组件异常也不会重新阻断界面操作。
        /// </summary>
        /// <param name="message">需要记录的完整错误信息。</param>
        private static void WriteSolutionLoadLogSafely(string message)
        {
            try
            {
                LogHelper.AddLog(MsgLevel.Exception, message, true);
            }
            catch
            {
                // 加载失败后不再传播日志异常，调用方可以继续使用软件。
            }
        }

        /// <summary>
        /// 方案保存
        /// </summary>
        public void Save(string solFile)
        {
            try
            {
                if (solFile.IsNullOrEmpty())
                {
                    MessageBoxTD.Show("尚未保存方案！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                ConfigHelper.SolSave(solFile);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// 方案总数量保存,只在软件关闭时保存OKNG总数
        /// </summary>
        public void SaveNumber(string solFile)
        {
            try
            {
                if (!solFile.IsNullOrEmpty())
                {
                    ConfigHelper.SolNumberSave(solFile);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion

        #region 控制停止运行的取消令牌

        /// <summary>
        /// 重置令牌源
        /// </summary>
        public void ResetTokenSource()
        {
            CancellationTokenSource retiredTokenSource = null;
            lock (_runLifecycleSync)
            {
                if (_isResetting ||
                    _activeRunSessionCount != 0 ||
                    !_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                retiredTokenSource = _cancellationTokenSource;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            retiredTokenSource.Dispose();
        }


        /// <summary>
        /// 取消令牌
        /// </summary>
        public void CancelToken()
        {
            Stop();
        }

        #endregion

        /// <summary>
        /// 停止当前运行和共享后台任务后重置方案。
        /// </summary>
        /// <returns>全部运行与后台任务均在期限内退出并完成重置时返回true。</returns>
        public bool SolReset()
        {
            lock (_solutionMutationSync)
                return SolResetCore(true);
        }

        /// <summary>
        /// 为方案加载清理旧方案，成功后继续保持运行门禁直到新方案恢复结束。
        /// </summary>
        /// <returns>旧方案资源已经安全清理返回true。</returns>
        internal bool SolResetForSolutionLoad()
        {
            lock (_solutionMutationSync)
                return SolResetCore(false);
        }

        /// <summary>
        /// 在调用方持有方案变更锁时执行真实重置过程。
        /// </summary>
        /// <param name="releaseRunGateOnSuccess">成功后是否立即重新开放运行入口。</param>
        /// <returns>全部运行与后台任务均退出并完成资源清理时返回true。</returns>
        private bool SolResetCore(bool releaseRunGateOnSuccess)
        {
            int drainTimeoutMilliseconds = Math.Max(
                0,
                Math.Min(60000, CurrentResourceProfile?.ShutdownResourceDrainTimeoutMs ?? 5000));
            if (!TryStopRunsForReset(drainTimeoutMilliseconds))
            {
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"方案重置等待运行流程退出超时（{drainTimeoutMilliseconds}ms），已中止资源释放并继续禁止新流程启动。",
                    true);
                return false;
            }

            if (!StopImageSaveQueueProcessor())
            {
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"方案重置等待图像保存工作池退出超时（{drainTimeoutMilliseconds}ms），已中止资源释放并保留原工作池。",
                    true);
                return false;
            }

            bool resetCompleted = false;
            try
            {
                ReleaseAllProcessResources();

                // 逐台释放旧方案硬件；一台失败不能阻止后续设备释放，也不能丢失失败设备的托管引用。
                List<string> deviceReleaseFailures = new List<string>();
                foreach (var dev in Solution.Instance.AllDevices.ToArray())
                {
                    try
                    {
                        if (dev is ILight light)
                            light.Disconnect();
                        if (dev is I3DCamera camera3D)
                            camera3D.Dispose();
                        if (dev is ICamera camera)
                            camera.Dispose();
                        if (dev is IPlc plc)
                            plc.Disconnect();
                        if (dev is IModbus modbus)
                            modbus.Disconnect();
                        if (dev is ITcpDevice tcpDev)
                            tcpDev.Disconnect();
                        if (dev is ComDevice com)
                            com.Close();
                    }
                    catch (Exception deviceException)
                    {
                        string deviceName = dev?.DevName ?? dev?.GetType().Name ?? "未知设备";
                        string failure = $"设备【{deviceName}】释放失败：{deviceException.Message}";
                        deviceReleaseFailures.Add(failure);
                        LogHelper.AddLog(MsgLevel.Exception, failure, true);
                    }
                }

                if (deviceReleaseFailures.Count > 0)
                    throw new InvalidOperationException(
                        $"方案重置有{deviceReleaseFailures.Count}台设备未安全释放，已保留全部设备引用并继续禁止运行。{string.Join("；", deviceReleaseFailures)}");

                // 清空设备
                SingleLight.SingleLights.Clear();
                SingleCamera.SingleCameraList.Clear();
                SingleCamera3D.SingleCamera3DList.Clear();
                SinglePLC.SinglePLCs.Clear();
                SingleModbus.SingleModbuss.Clear();
                SingleTcp.SingleTCPs.Clear();
                SingleCOM.SingleComs.Clear();
                Solution.Instance.AllDevices.Clear();

                // 旧相机已经停流、排空回调并释放句柄，此时才能关闭旧票据并为新方案建立干净纪元。
                RenewCameraProductionPipeline();
                RenewOrderedSignalCoordinator();

                // 释放AI节点的模型句柄
                ModelHandleManager.DestroyAllModel();

                // 清空方案共享变量、流程信号
                Solution.Instance.SharedVariable.ClearAll();
                Solution.Instance.ProcessSignalDic.Clear();
                _imageSaveSizeSampler.Reset();

                // 清空流程和节点
                Solution.Instance.ProcessCount = 0;
                Solution.Instance.AllProcesses.Clear();
                Solution.Instance.NodeCount = 0;
                Solution.Instance.Nodes.Clear();

                // 发送清除结果窗口事件
                RemoveResultData?.Invoke(this, new EventArgs());
                resetCompleted = true;
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"方案资源重置失败：{ex.Message}", true);
                }
                catch
                {
                }
                return false;
            }
            finally
            {
                if (resetCompleted && releaseRunGateOnSuccess)
                    EndSolutionReset();
            }
        }

        #endregion
    }
}
