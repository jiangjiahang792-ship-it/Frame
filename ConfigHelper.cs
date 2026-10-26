using Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TDJS_Vision.Forms.GlobalSignalSettings;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Startup;

namespace TDJS_Vision
{
    public class ConfigHelper
    {
        /// <summary>
        /// 方案文件保存锁，避免自动保存、关闭保存和手动保存同时替换同一个方案文件。
        /// </summary>
        private static readonly object SolutionFileSaveLocker = new object();

        /// <summary>
        /// 保存失败日志标记键，用于避免同一次保存异常被内外层重复记录。
        /// </summary>
        private const string SolutionSaveFailureLoggedKey = "TDJS_Vision.SolutionSaveFailureLogged";

        public static SolConfig SolConfig = new SolConfig();
        public static EventHandler<bool> DeserializationCompletionEvent;
        /// <summary>全部设备连接结束后，在方案加载线程恢复流程和节点。</summary>
        public static event EventHandler<bool> DevicesRestored;
        /// <summary>
        /// 【方案保存】
        /// 只需要保存反序列化后能够还原软件的必要配置
        /// 即可，其他内部对象通过参数还原
        /// </summary>
        public static void SolSave(string solFile)
        {
            try
            {
                //保存窗体布局
                CanvasSet.SaveDockPanelEvent?.Invoke(null, EventArgs.Empty);
                // 保存已打开组合模块的内部编辑，未打开模块不实例化，按层级递归保留快照。
                foreach (var process in Solution.Instance.AllProcesses)
                    foreach (var node in process.Nodes)
                        (node as TDJS_Vision.Node._6_LogicTool.CompositeModule.NodeCompositeModule)?.SaveEditableProcess();

                SolConfig.SolVer = VersionInfo.VersionInfo.GetExeVer();
                SolConfig.SolName = solFile;
                SolConfig.RunInterval = Solution.Instance.RunInterval;
                SolConfig.Devices = Solution.Instance.AllDevices;
                SolConfig.NodeCount = Solution.Instance.NodeCount;
                SolConfig.ProcessCount = Solution.Instance.ProcessCount;

                SolConfig.ProcessInfos = new List<ProcessConfig>();
                foreach (var process in Solution.Instance.AllProcesses)
                {
                    ProcessConfig processConfig = new ProcessConfig();
                    processConfig.ID = process.ID;
                    processConfig.ProcessName = process.ProcessName;
                    processConfig.ShowLog = process.ShowLog;
                    processConfig.Enable = process.Enable;
                    processConfig.Level = process.RunLv;
                    processConfig.Group = process.Group;
                    processConfig.IsPassiveTriggered = process.IsPassiveTriggered;
                    processConfig.IsCameraCallbackTriggered = process.IsCameraCallbackTriggered;
                    processConfig.OKNumber = process.OKNumber;
                    processConfig.NGNumber = process.NGNumber;
                    processConfig.HasCanvasGraph = process.HasCanvasGraph;
                    processConfig.NodeInfos.Clear();
                    foreach (var nodeConfig in process.Nodes)
                    {
                        NodeConfig node = new NodeConfig();
                        node.NodeType = nodeConfig.NodeType;
                        node.NodeName = nodeConfig.NodeName;
                        node.Notes = nodeConfig.Notes;
                        node.ID = nodeConfig.ID;
                        node.Active = nodeConfig.Active;
                        node.OutputLog = nodeConfig.OutputLog;
                        node.Selected = nodeConfig.Selected;
                        node.HasCanvasLayout = true;
                        node.CanvasX = nodeConfig.CanvasLocation.X;
                        node.CanvasY = nodeConfig.CanvasLocation.Y;
                        node.CanvasWidth = nodeConfig.CanvasSize.Width;
                        node.CanvasHeight = nodeConfig.CanvasSize.Height;
                        node.IsStartNode = nodeConfig.IsStartNode;
                        if (nodeConfig.ParamForm != null && nodeConfig.ParamForm.Params != null)
                        {
                            // 手动图像测试的临时来源只用于运行，任何保存入口都持久化原始配置。
                            NodeParamImageSoucre imageParam = nodeConfig.ParamForm.Params as NodeParamImageSoucre;
                            node.NodeParam = imageParam != null
                                ? imageParam.CreatePersistenceSnapshot()
                                : nodeConfig.ParamForm.Params;
                        }
                        processConfig.NodeInfos.Add(node);
                    }
                    processConfig.ConnectionInfos.Clear();
                    foreach (var connection in process.Connections)
                    {
                        processConfig.ConnectionInfos.Add(new ProcessConnectionConfig
                        {
                            ID = connection.ID,
                            FromNodeId = connection.FromNodeId,
                            ToNodeId = connection.ToNodeId,
                            FromAnchor = connection.FromAnchor,
                            ToAnchor = connection.ToAnchor,
                            Branch = connection.Branch
                        });
                    }
                    SolConfig.ProcessInfos.Add(processConfig);
                }
                SolConfig.GlobalSignal = Solution.Instance.GlobalSignal;
                SolConfig.DetectItemDic = Solution.Instance.DetectItemDic;
                SolConfig.RuntimeInterfaces = Forms.RuntimeDesigner.RuntimeInterfacePage.ClonePages(Solution.Instance.RuntimeInterfaces);
                SolConfig.RuntimeInterface = null;

                // 配置持久化到本地文件
                string json = JsonConvert.SerializeObject(SolConfig, Formatting.Indented);
                SafeWriteSolutionFile(solFile, json, "方案保存");
                Forms.SolutionBrowser.FileSolutionCatalog.Default.Remember(solFile);

            }
            catch (Exception ex)
            {
                if (!IsSolutionSaveFailureLogged(ex))
                    WriteSolutionSaveFailureLog("方案保存", solFile, null, ex);
                throw;
            }
        }

        /// <summary>
        /// 【方案加载】
        /// 配置中涉及到接口类、抽象类等多态成员
        /// 能够反序列化为对应派生类型对象，首先得保证
        /// 此类成员前面声明为:
        /// [JsonConverter(typeof(DeviceConverter))]
        /// 列表成员前面声明为：
        /// [JsonConverter(typeof(DeviceListConverter<ICamera>))]
        /// 否则会因为无法解析派生类的Json字符串导致反序列化失败
        /// </summary>
        /// <param name="solFile"></param>
        public static void SolLoad(string solFile, bool flag)
        {
            using (Solution.Instance.EnterSolutionMutationScope())
            {
                bool resetAttempted = false;
                bool resetCompleted = false;
                bool openedGateOwnedByLoad = Solution.Instance.BeginSolutionLoadRunGate();
                try
                {
                    string solutionName = Path.GetFileName(solFile);
                    StartupProgressContext.ReportItem("正在读取方案文件", solutionName, 1, 1, 30, 32);
                    StartupProgressContext.ReportItem("正在解析方案配置", solutionName, 1, 1, 32, 35);
                    SolConfig = ReadSolutionWithRecovery(solFile);

                    SolConfig.SolName = solFile;

                    #region 清理旧方案设备和流程节点

                    StartupProgressContext.ReportStage("正在清理旧方案数据", solutionName, 35);
                    resetAttempted = true;
                    if (!Solution.Instance.SolResetForSolutionLoad())
                        throw new TimeoutException("旧方案仍有运行流程或图像保存任务未退出，本次方案加载已中止。");
                    resetCompleted = true;

                    #endregion

                    Solution.Instance.SolVersion = SolConfig.SolVer;
                    Solution.Instance.SolFileName = solFile;
                    Solution.Instance.RuntimeInterfaces = SolConfig.RuntimeInterfaces ?? new List<Forms.RuntimeDesigner.RuntimeInterfacePage>();
                    Solution.Instance.RunInterval = SolConfig.RunInterval;
                    Solution.Instance.NodeCount = SolConfig.NodeCount;
                    Solution.Instance.ProcessCount = SolConfig.ProcessCount;
                    Solution.Instance.GlobalSignal = SolConfig.GlobalSignal;
                    Solution.Instance.DetectItemDic = SolConfig.DetectItemDic;

                    using (StartupAiRuntimeLoadGate.BeginTDAIDeferral())
                    {
                        // 设备控件在主线程恢复；并发尝试连接后恢复流程，设备离线不阻止方案打开。
                        using (var connections = new StartupDeviceConnectionBatch())
                        {
                            DeserializationCompletionEvent?.Invoke(null, flag);
                            connections.Complete();
                        }
                        DevicesRestored?.Invoke(null, flag);
                        // 所有流程节点和参数恢复完成后顺序预加载 AI 运行时，覆盖软件启动和手动打开方案入口。
                        SolutionAiRuntimePreloader.PreloadEnabledNodes();
                        // TDAI/YOLO 会加载另一套 native 依赖，必须等无监督和大模型预加载完成后再启动。
                        StartupAiRuntimeLoadGate.FlushDeferredTDAILoads();
                    }
                    Solution.Instance.NodeCount = SolConfig.NodeCount;
                    FormGlobalSignal.StartHeartbeatSignals();
                }
                finally
                {
                    // 重置超时继续保持门禁，供用户重试；读文件失败或旧方案已清理则恢复可运行状态。
                    if (resetCompleted || (!resetAttempted && openedGateOwnedByLoad))
                        Solution.Instance.EndSolutionLoadRunGate();
                }
            }
        }


        /// <summary>
        /// 方案保存,只在软件关闭时保存对应的OKNG数量
        /// </summary>
        /// <param name="solFile"></param>
        public static void SolNumberSave(string solFile) {
            try
            {
                for (int i =0;i<Solution.Instance.AllProcesses.Count;i++)
                {
                    SolConfig.ProcessInfos[i].OKNumber = Solution.Instance.AllProcesses[i].OKNumber;
                    SolConfig.ProcessInfos[i].NGNumber = Solution.Instance.AllProcesses[i].NGNumber;
                }

                // 配置持久化到本地文件
                string json = JsonConvert.SerializeObject(SolConfig, Formatting.Indented);
                SafeWriteSolutionFile(solFile, json, "方案数量保存");

            }
            catch (Exception ex)
            {
                if (!IsSolutionSaveFailureLogged(ex))
                    WriteSolutionSaveFailureLog("方案数量保存", solFile, null, ex);
                throw;
            }
        }

        /// <summary>
        /// 安全写入方案文件，先写同目录临时文件并完成反序列化校验，确认无误后再替换原方案。
        /// </summary>
        /// <param name="solFile">目标方案文件路径。</param>
        /// <param name="json">已经序列化完成的方案JSON内容。</param>
        /// <param name="operationName">保存动作名称，用于诊断日志。</param>
        private static void SafeWriteSolutionFile(string solFile, string json, string operationName)
        {
            if (string.IsNullOrWhiteSpace(solFile))
                throw new ArgumentException("方案文件路径不能为空。", nameof(solFile));

            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("方案保存内容为空，已取消写入。");

            string targetPath = Path.GetFullPath(solFile);
            string directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Environment.CurrentDirectory;

            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(directory, Path.GetFileName(targetPath) + ".tmp-" + Guid.NewGuid().ToString("N"));
            string backupPath = BuildSingleSolutionBackupPath(targetPath);

            lock (SolutionFileSaveLocker)
            {
                try
                {
                    WriteTextWithFlush(temporaryPath, json);
                    ValidateSolutionTemporaryFile(temporaryPath);
                    ReplaceSolutionFile(targetPath, temporaryPath, backupPath);
                }
                catch (Exception ex)
                {
                    WriteSolutionSaveFailureLog(operationName, targetPath, temporaryPath, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// 写入文本并强制刷新到磁盘，降低大模板图像数据写入中断导致文件半截落盘的风险。
        /// </summary>
        /// <param name="filePath">需要写入的临时文件路径。</param>
        /// <param name="content">需要写入的文本内容。</param>
        private static void WriteTextWithFlush(string filePath, string content)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 64, FileOptions.WriteThrough))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }
        }

        /// <summary>
        /// 校验临时方案文件能够被完整读取和解析，避免把损坏JSON替换成正式方案。
        /// </summary>
        /// <param name="temporaryPath">待校验的临时方案文件路径。</param>
        private static void ValidateSolutionTemporaryFile(string temporaryPath)
        {
            ReadValidatedSolution(temporaryPath);
        }

        /// <summary>完整解析并验证方案，校验失败前不修改当前运行方案或连接设备。</summary>
        private static SolConfig ReadValidatedSolution(string temporaryPath)
        {
            string json = File.ReadAllText(temporaryPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("临时方案文件为空。");

            if (json.IndexOf('\0') >= 0)
                throw new InvalidOperationException("临时方案文件包含空字节，疑似写入中断。");

            SolConfig config = JsonConvert.DeserializeObject<SolConfig>(json);
            if (config == null)
                throw new InvalidOperationException("临时方案文件无法反序列化为方案配置。");

            ValidateSolutionGraph(config);
            return config;
        }

        /// <summary>仅将文件缺失或内容校验失败视为可恢复故障，设备连接失败不得触发旧版本回滚。</summary>
        private static bool IsRecoverableSolutionError(Exception error) => error is JsonException ||
            error is InvalidOperationException || error is FileNotFoundException || error is DirectoryNotFoundException;

        /// <summary>主方案损坏时使用经过校验的备份；无有效备份则保留原文件并明确拒绝加载。</summary>
        private static SolConfig ReadSolutionWithRecovery(string solFile)
        {
            string target = Path.GetFullPath(solFile);
            lock (SolutionFileSaveLocker)
            {
                Exception original;
                try { return ReadValidatedSolution(target); }
                catch (Exception ex) when (IsRecoverableSolutionError(ex)) { original = ex; }

                var candidates = new List<string> { BuildSingleSolutionBackupPath(target) };
                // 兼容旧版本位于程序目录的时间戳备份，必须核对完整原路径，防止同名客户方案串用。
                candidates.AddRange(Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,
                    Path.GetFileName(target) + ".bak-*").OrderByDescending(File.GetLastWriteTimeUtc));
                foreach (string backup in candidates)
                {
                    if (!File.Exists(backup)) continue;
                    SolConfig recovered;
                    try
                    {
                        recovered = ReadValidatedSolution(backup);
                        if (!string.Equals(backup, candidates[0], StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrWhiteSpace(recovered.SolName) ||
                            !string.Equals(recovered.SolName, target, StringComparison.OrdinalIgnoreCase))) continue;
                    }
                    catch (Exception ex) when (IsRecoverableSolutionError(ex) || ex is IOException || ex is UnauthorizedAccessException)
                    { Properties.RecoverableSettingsFile.Report("跳过无效方案备份：" + backup + "；" + ex.Message); continue; }

                    string temporary = target + ".restore-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        // 不序列化重写客户参数；恢复备份的原始文本，损坏现场独立保留。
                        if (File.Exists(target)) File.Copy(target, target + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".bak");
                        WriteTextWithFlush(temporary, File.ReadAllText(backup, Encoding.UTF8));
                        ValidateSolutionTemporaryFile(temporary);
                        if (File.Exists(target)) File.Replace(temporary, target, null, true);
                        else File.Move(temporary, target);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        Properties.RecoverableSettingsFile.Report("方案恢复写盘失败，本次从备份加载：" + target + "；" + ex.Message);
                    }
                    finally { TryDeleteSolutionTemporary(temporary); }
                    LogHelper.AddLog(MsgLevel.Warn, "方案文件损坏或缺失，已从备份加载：" + backup + "。请核对最近修改的检测参数。", true);
                    return recovered;
                }
                throw new InvalidDataException("方案损坏或缺失，未找到有效备份，已保留原文件，未创建或覆盖为空方案：" + target, original);
            }
        }

        /// <summary>清理临时文件失败只写诊断，不掩盖保存或恢复的原始异常。</summary>
        private static void TryDeleteSolutionTemporary(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { Properties.RecoverableSettingsFile.Report("方案临时文件清理失败：" + ex.Message); }
        }

        /// <summary>
        /// 校验方案流程图连线端点都能找到对应节点，避免明显断层的流程图被保存为正式方案。
        /// </summary>
        /// <param name="config">已经反序列化的方案配置。</param>
        private static void ValidateSolutionGraph(SolConfig config)
        {
            if (config.Devices == null)
                throw new InvalidOperationException("方案设备配置缺失或无法解析。");
            if (config.ProcessInfos == null)
                throw new InvalidOperationException("方案缺少流程配置。");

            foreach (ProcessConfig process in config.ProcessInfos)
            {
                if (process == null)
                    throw new InvalidOperationException("方案存在空流程配置。");

                if (process.NodeInfos == null)
                    throw new InvalidOperationException($"流程“{process.ProcessName}”缺少节点配置。");

                if (process.ConnectionInfos == null)
                    continue;

                HashSet<int> nodeIds = new HashSet<int>();
                foreach (NodeConfig node in process.NodeInfos)
                {
                    if (node == null)
                        throw new InvalidOperationException($"流程“{process.ProcessName}”存在空节点配置。");
                    nodeIds.Add(node.ID);
                }

                foreach (ProcessConnectionConfig connection in process.ConnectionInfos)
                {
                    if (connection == null)
                        continue;

                    if (!nodeIds.Contains(connection.FromNodeId) || !nodeIds.Contains(connection.ToNodeId))
                    {
                        throw new InvalidOperationException(
                            $"流程“{process.ProcessName}”存在断开的连线：{connection.FromNodeId}->{connection.ToNodeId}。");
                    }
                }
            }
        }

        /// <summary>
        /// 生成与方案一同保存的固定备份路径，避免不同目录下的同名方案互相覆盖备份。
        /// </summary>
        /// <param name="targetPath">正式方案文件路径。</param>
        /// <returns>与方案同目录的固定备份文件路径。</returns>
        private static string BuildSingleSolutionBackupPath(string targetPath)
        {
            return targetPath + ".bak";
        }

        /// <summary>
        /// 使用同目录临时文件替换正式方案；已有正式方案时保留上一版有效备份，首次保存也创建备份。
        /// </summary>
        /// <param name="targetPath">正式方案文件路径。</param>
        /// <param name="temporaryPath">已经校验通过的临时方案文件路径。</param>
        /// <param name="backupPath">与方案同目录的备份路径。</param>
        private static void ReplaceSolutionFile(string targetPath, string temporaryPath, string backupPath)
        {
            if (File.Exists(targetPath))
            {
                SaveSingleSolutionBackup(targetPath, backupPath);
                File.Replace(temporaryPath, targetPath, null, true);
            }
            else
            {
                SaveSingleSolutionBackup(temporaryPath, backupPath);
                File.Move(temporaryPath, targetPath);
            }
        }

        /// <summary>
        /// 验证旧方案并原子更新备份，完成前不删除既有备份。
        /// </summary>
        /// <param name="targetPath">正式方案文件路径。</param>
        /// <param name="backupPath">与方案同目录的固定备份路径。</param>
        private static void SaveSingleSolutionBackup(string targetPath, string backupPath)
        {
            // 原文件损坏时禁止用它覆盖好备份，先恢复或人工处理后才能继续保存。
            ValidateSolutionTemporaryFile(targetPath);
            string temporary = backupPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                WriteTextWithFlush(temporary, File.ReadAllText(targetPath, Encoding.UTF8));
                ValidateSolutionTemporaryFile(temporary);
                if (File.Exists(backupPath)) File.Replace(temporary, backupPath, null, true);
                else File.Move(temporary, backupPath);
            }
            finally { TryDeleteSolutionTemporary(temporary); }
        }

        /// <summary>
        /// 安全记录方案保存失败原因，日志组件异常时不再影响原始保存异常。
        /// </summary>
        /// <param name="operationName">保存动作名称。</param>
        /// <param name="targetPath">正式方案文件路径。</param>
        /// <param name="temporaryPath">失败时保留的临时文件路径。</param>
        /// <param name="exception">保存失败异常。</param>
        private static void WriteSolutionSaveFailureLog(string operationName, string targetPath, string temporaryPath, Exception exception)
        {
            try
            {
                MarkSolutionSaveFailureLogged(exception);
                string temporaryInfo = string.IsNullOrWhiteSpace(temporaryPath) ? "无" : temporaryPath;
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    $"{operationName}失败，原方案文件未直接覆盖。方案：{targetPath}；临时文件：{temporaryInfo}；原因：{exception.Message}",
                    true);
            }
            catch
            {
                // 保存失败时不再传播日志异常，避免掩盖真正的文件写入问题。
            }
        }

        /// <summary>
        /// 标记当前保存异常已经写过日志。
        /// </summary>
        /// <param name="exception">需要标记的保存异常。</param>
        private static void MarkSolutionSaveFailureLogged(Exception exception)
        {
            if (exception == null)
                return;

            exception.Data[SolutionSaveFailureLoggedKey] = true;
        }

        /// <summary>
        /// 判断当前保存异常是否已经写过日志。
        /// </summary>
        /// <param name="exception">需要检查的保存异常。</param>
        /// <returns>已经写过日志时返回true。</returns>
        private static bool IsSolutionSaveFailureLogged(Exception exception)
        {
            return exception != null &&
                   exception.Data.Contains(SolutionSaveFailureLoggedKey) &&
                   exception.Data[SolutionSaveFailureLoggedKey] is bool logged &&
                   logged;
        }
    }

    public class SolConfig
    {
        /// <summary>可选的运行界面配置；缺少此字段的历史方案保持兼容。</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Forms.RuntimeDesigner.RuntimeLayoutDocument RuntimeInterface { get; set; }

        /// <summary>多个独立运行界面的持久化集合，缺失时从旧单界面字段迁移。</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Forms.RuntimeDesigner.RuntimeInterfacePage> RuntimeInterfaces { get; set; }

        /// <summary>新格式只保存集合，避免首个布局重复写入。</summary>
        public bool ShouldSerializeRuntimeInterface() => RuntimeInterfaces == null && RuntimeInterface != null;

        /// <summary>没有配置界面的旧方案不增加空集合字段。</summary>
        public bool ShouldSerializeRuntimeInterfaces() => RuntimeInterfaces != null && RuntimeInterfaces.Count > 0;

        /// <summary>在方案反序列化完成时校验布局，避免清理现有方案后才发现非法界面配置。</summary>
        [System.Runtime.Serialization.OnDeserialized]
        private void ValidateRuntimeInterface(System.Runtime.Serialization.StreamingContext context)
        {
            if (RuntimeInterfaces == null && RuntimeInterface != null)
                RuntimeInterfaces = new List<Forms.RuntimeDesigner.RuntimeInterfacePage>
                { new Forms.RuntimeDesigner.RuntimeInterfacePage { Layout = RuntimeInterface } };
            if (RuntimeInterfaces != null)
                RuntimeInterfaces = Forms.RuntimeDesigner.RuntimeInterfacePage.ClonePages(RuntimeInterfaces);
            // 仅供原单界面读取者使用，新配置始终以集合为唯一持久化来源。
            RuntimeInterface = RuntimeInterfaces?.FirstOrDefault()?.Layout;
        }

        /// <summary>
        /// 方案版本
        /// </summary>
        public string SolVer;
        public string SolName;
        public int RunInterval;     // 流程循环运行间隔时间
        public int NodeCount;       // 开始创建节点使用的ID-1
        public int ProcessCount;    // 开始创建流程使用的ID-1
        /// <summary>
        /// 方案下的所有设备(光源、相机、PLC)
        /// </summary>
        [JsonConverter(typeof(DeviceListConverter<Device.IDevice>))]
        public List<Device.IDevice> Devices;
        /// <summary>
        /// 方案下所有流程配置
        /// </summary>
        public List<ProcessConfig> ProcessInfos;
        /// <summary>
        /// 全局信号
        /// </summary>
        public GlobalSignal GlobalSignal;
        /// <summary>
        /// 所有检测项配置
        /// </summary>
        public Dictionary<string, List<DetectItemInfo>> DetectItemDic;
    }

    public class ProcessConfig
    {
        /// <summary>
        /// 流程ID
        /// </summary>
        public int ID;
        /// <summary>
        /// 流程名称
        /// </summary>
        public string ProcessName;
        /// <summary>
        /// 流程启用/禁用状态
        /// </summary>
        public bool Enable;
        /// <summary>
        /// 流程是否显示日志
        /// </summary>
        public bool ShowLog;
        /// <summary>
        /// 是否是被动触发流程
        /// </summary>
        public bool IsPassiveTriggered;
        /// <summary>
        /// 是否由相机回调触发流程
        /// </summary>
        public bool IsCameraCallbackTriggered;
        /// <summary>
        /// 流程运行优先级
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ProcessLvEnum Level;

        /// <summary>
        /// 流程运行组别
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ProcessGroup Group;
        /// <summary>
        /// 流程中所有节点的名称
        /// </summary>
        public List<NodeConfig> NodeInfos = new List<NodeConfig>();

        /// <summary>
        /// 指示流程配置已使用画布图结构保存。
        /// </summary>
        public bool HasCanvasGraph;

        /// <summary>
        /// 流程画布中的连接关系。
        /// </summary>
        public List<ProcessConnectionConfig> ConnectionInfos = new List<ProcessConnectionConfig>();

        /// <summary>
        /// OK数量
        /// </summary>
        public int OKNumber;
        /// <summary>
        /// NG数量
        /// </summary>
        public int NGNumber;
    }

    public class NodeConfig
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public NodeType NodeType;
        public string NodeName;
        /// <summary>节点备注，随方案持久化；旧方案缺失时使用默认备注。</summary>
        public string Notes = "无备注";
        public int ID;
        public bool Active;
        /// <summary>
        /// 节点是否允许输出运行日志；为空时表示旧配置，按默认开启处理。
        /// </summary>
        public bool? OutputLog;
        public bool Selected;
        public bool HasCanvasLayout;
        public int CanvasX;
        public int CanvasY;
        public int CanvasWidth;
        public int CanvasHeight;
        public bool IsStartNode;
        [JsonConverter(typeof(PolyConverter))]
        public INodeParam NodeParam;
    }

    public class ProcessConnectionConfig
    {
        public string ID;
        public int FromNodeId;
        public int ToNodeId;
        public string FromAnchor;
        public string ToAnchor;
        [JsonConverter(typeof(StringEnumConverter))]
        public ProcessConnectionBranch Branch;
    }

    /// <summary>
    /// 字符串加解密工具类（基于AES算法）
    /// </summary>
    public class StringCipher
    {
        private static readonly string DefaultKey = "12345678901234567890123456789012"; // 32字节 = 256位
        private static readonly string DefaultIV = "1234567890123456"; // 16字节 = 128位

        /// <summary>
        /// 加密字符串
        /// </summary>
        /// <param name="plainText">明文字符串</param>
        /// <param name="key">密钥（32字节，Base64或32字符）</param>
        /// <param name="iv">初始化向量（16字节，Base64或16字符）</param>
        /// <returns>Base64编码的密文</returns>
        public static string Encrypt(string plainText, string key = null, string iv = null)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentException("明文不能为空");

            byte[] keyBytes = GetKeyBytes(key ?? DefaultKey);
            byte[] ivBytes = GetIVBytes(iv ?? DefaultIV);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// 解密字符串
        /// </summary>
        /// <param name="cipherText">Base64编码的密文</param>
        /// <param name="key">密钥（32字节）</param>
        /// <param name="iv">初始化向量（16字节）</param>
        /// <returns>解密后的明文</returns>
        public static string Decrypt(string cipherText, string key = null, string iv = null)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentException("密文不能为空");

            byte[] keyBytes = GetKeyBytes(key ?? DefaultKey);
            byte[] ivBytes = GetIVBytes(iv ?? DefaultIV);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (MemoryStream ms = new MemoryStream(cipherBytes))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cs, Encoding.UTF8))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        // 确保密钥为32字节
        private static byte[] GetKeyBytes(string key)
        {
            if (key.Length == 32)
                return Encoding.UTF8.GetBytes(key);
            else if (key.Length == 44 && key.EndsWith("=")) // 可能是Base64
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(key);
                    if (bytes.Length == 32) return bytes;
                }
                catch { }
            }
            throw new ArgumentException("密钥必须是32字节的字符串或32字节的Base64编码");
        }

        // 确保IV为16字节
        private static byte[] GetIVBytes(string iv)
        {
            if (iv.Length == 16)
                return Encoding.UTF8.GetBytes(iv);
            else if (iv.Length == 24 && iv.EndsWith("==")) // Base64 编码的16字节
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(iv);
                    if (bytes.Length == 16) return bytes;
                }
                catch { }
            }
            throw new ArgumentException("IV必须是16字节的字符串或16字节的Base64编码");
        }
    }


    /// <summary>
    /// 用于多态序列化
    /// </summary>
    public class PolyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }
        #pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
        #pragma warning disable CS8765 // 参数类型的为 Null 性与重写成员不匹配(可能是由于为 Null 性特性)。
        #pragma warning disable CS8604 // 引用类型参数可能为 null。
        #pragma warning disable CS8603 // 可能返回 null 引用。
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                var jObject = JObject.Load(reader);
                foreach (var item in jObject.Properties())
                {

                    Type type = Type.GetType(item.Name);

                    var value = item.Value.ToObject(type);

                    return value;

                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, ex.Message, true);
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)

        {
            JObject jObject = new JObject();

            jObject.Add(value.GetType().FullName, JToken.FromObject(value));

            serializer.Serialize(writer, jObject);
        }
        #pragma warning restore CS8603 // 可能返回 null 引用。
        #pragma warning restore CS8604 // 引用类型参数可能为 null。
        #pragma warning restore CS8765 // 参数类型的为 Null 性与重写成员不匹配(可能是由于为 Null 性特性)。
        #pragma warning restore CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
    }

    /// <summary>
    /// 用于多态类型IDevice列表的转化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DeviceListConverter<T> : JsonConverter
    {
        #pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
        #pragma warning disable CS8765 // 参数类型的为 Null 性与重写成员不匹配(可能是由于为 Null 性特性)。
        #pragma warning disable CS8604 // 引用类型参数可能为 null。
        #pragma warning disable CS8603 // 可能返回 null 引用。
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                var jObject = JObject.Load(reader);
                List<T> values = new List<T>();
                foreach (var item in jObject.Properties())
                {
                    Type type = Type.GetType((string)item.Value["ClassName"]);
                    var value = item.Value.ToObject(type);
                    values.Add((T)value);
                }
                return values;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var values = (List<T>)value;
            JObject jObject = new JObject();
            foreach (var item in values)
            {
                jObject.Add(((Device.IDevice)item).UserDefinedName, JToken.FromObject(item));
            }
            serializer.Serialize(writer, jObject);
        }
        #pragma warning restore CS8603 // 可能返回 null 引用。
        #pragma warning restore CS8604 // 引用类型参数可能为 null。
        #pragma warning restore CS8765 // 参数类型的为 Null 性与重写成员不匹配(可能是由于为 Null 性特性)。
        #pragma warning restore CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
    }
    /// <summary>
    /// 用于多态类型ROI列表的转化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ROIListConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        /// <summary>还原旧版 ROI 列表，允许新版参数将不再使用的旧字段保存为空值。</summary>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // null 是合法的旧字段状态，必须在按对象读取前处理，并保留其原始语义。
            if (reader.TokenType == JsonToken.Null)
                return null;

            try
            {
                var jObject = JObject.Load(reader);
                List<T> values = new List<T>();
                foreach (var item in jObject.Properties())
                {
                    Type type = Type.GetType((string)item.Value["ClassName"]);
                    var value = item.Value.ToObject(type);
                    values.Add((T)value);
                }
                return values;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var values = (List<T>)value;
            JObject jObject = new JObject();
            int i = 0;
            foreach (var item in values)
            {
                jObject.Add(typeof(T).Name + $"{++i}", JToken.FromObject(item));
            }
            serializer.Serialize(writer, jObject);
        }
    }
}
