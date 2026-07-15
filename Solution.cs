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
        }

        public static Solution Instance => _lazy.Value;

        #endregion

        #region 私有字段、属性

        /// <summary>
        /// 方案运行取消源，通过它控制方案的停止
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

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

            ReleaseAiNodeModel(node);
            DisposeNodeParamForm(node);
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

            string validationMessage;
            if (!TryValidateCameraConfiguration(AllProcesses, out validationMessage))
            {
                LogHelper.AddLog(MsgLevel.Exception, validationMessage, true);
                return;
            }

            IsRunning = true;
            Solution.Instance.ResetTokenSource();
            var _cts = CancellationTokenSource.CreateLinkedTokenSource(Solution.Instance.CancellationToken);
            List<ICamera> startedCallbackCameras = new List<ICamera>();

            try
            {
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
                           
                            var passiveTasks = new ConcurrentBag<Task>(); // 本轮被动任务

                            // 创建本轮的 triggerAction
                            Action<Process> triggerAction = (targetProcess) =>
                            {
                                if (targetProcess == null ||
                                    !targetProcess.IsPassiveTriggered ||
                                    targetProcess.Group != groupCopy[0].Group)
                                {
                                    return;
                                }

                                var task = targetProcess.RunInternal(
                                    isCyclical: isCyclical,
                                    isTriggered: true,
                                    ct: groupCts.Token
                                );
                                passiveTasks.Add(task);
                            };

                            // 注入 triggerAction 到该组所有流程
                            foreach (var proc in groupCopy)
                            {
                                proc.SetTriggerAction(triggerAction);
                            }

                            // 按优先级执行主动流程
                            var priorityLevels = groupCopy.Select(p => p.RunLv).Distinct().OrderBy(lv => lv);
                            foreach (var priority in priorityLevels)
                            {
                                var activeProcesses = groupCopy
                                    .Where(p => p.RunLv == priority &&
                                                !p.IsPassiveTriggered)
                                    .ToList();

                                if (!activeProcesses.Any()) continue;

                                var activeTasks = activeProcesses.Select(async p =>
                                {
                                    try
                                    {
                                        await p.RunInternal(
                                            isCyclical: isCyclical,
                                            isTriggered: false, // 注意：主动运行的流程不是被触发的
                                            ct: groupCts.Token
                                        );
                                    }
                                    catch (Exception ex) {/* 忽略异常 */ }
                                });

                                // 等待所有任务完成（即使有异常，也会继续等待）
                                await Task.WhenAll(activeTasks);
                            }

                            // 等待被动流程完成
                            if (passiveTasks.Count > 0)
                            {
                                await Task.WhenAll(passiveTasks.ToArray());
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
                IsRunning = false;
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

            HashSet<ICamera> visitedCameras = new HashSet<ICamera>();
            foreach (Process process in processes)
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

                    ParamFormImageSource form = imageSource.ParamForm as ParamFormImageSource;
                    if (form != null)
                        form.SyncCameraCallbackBinding();

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
        /// 从图像源参数中获取用于相机回调的相机。
        /// </summary>
        /// <param name="param">图像源参数。</param>
        /// <returns>可用相机对象。</returns>
        private static ICamera GetCameraCallbackCamera(NodeParamImageSoucre param)
        {
            if (param == null || param.ImageSource != "相机")
                return null;

            return param.Camera;
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

            camera.SetTriggerSource(param.TriggerSource);
            camera.SetTriggerEdge(param.TriggerEdge);
            camera.SetTriggerMode(param.TriggerModel);
            camera.SetTriggerDelay(param.TriggerDelay);
            camera.SetExposureTime(param.ExposureTime);
            camera.SetGain(param.Gain);
            camera.GetImageTimeOut = param.TimeOut;
        }

        public async Task Run(bool isCyclical = false,bool is1=false)
        {
            if (AllProcesses == null || AllProcesses.Count == 0)
                return;

            IsRunning = true;
            Solution.Instance.ResetTokenSource();
            var _cts = CancellationTokenSource.CreateLinkedTokenSource(Solution.Instance.CancellationToken);

            try
            {
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
                IsRunning = false;
            }
        }

        /// <summary>
        /// 方案运行停止
        /// </summary>
        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            IsRunning = false;
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
            if(_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource = new CancellationTokenSource();
        }


        /// <summary>
        /// 取消令牌
        /// </summary>
        public void CancelToken()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        #endregion

        /// <summary>
        /// 重置方案
        /// </summary>
        public void SolReset()
        {
            ReleaseAllProcessResources();

            try
            {
                // 释放旧方案的硬件资源（光源、相机、PLC、Modbus、TCP）
                foreach (var dev in Solution.Instance.AllDevices)
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
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }

            // 清空设备
            SingleLight.SingleLights.Clear();
            SingleCamera.SingleCameraList.Clear();
            SingleCamera3D.SingleCamera3DList.Clear();
            SinglePLC.SinglePLCs.Clear();
            SingleModbus.SingleModbuss.Clear();
            SingleTcp.SingleTCPs.Clear();
            SingleCOM.SingleComs.Clear();
            Solution.Instance.AllDevices.Clear();

            // 释放AI节点的模型句柄
            ModelHandleManager.DestroyAllModel();

            // 清空方案共享变量、流程信号
            Solution.Instance.SharedVariable.ClearAll();
            Solution.Instance.ProcessSignalDic.Clear();

            // 清空流程和节点
            Solution.Instance.ProcessCount = 0;
            Solution.Instance.AllProcesses.Clear();
            Solution.Instance.NodeCount = 0;
            Solution.Instance.Nodes.Clear();

            // 发送清除结果窗口事件
            RemoveResultData?.Invoke(this, new EventArgs());
        }

        #endregion
    }
}
