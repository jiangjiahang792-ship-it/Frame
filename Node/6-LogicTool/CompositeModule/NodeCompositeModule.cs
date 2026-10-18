using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>组合模块内部编辑与端口配置契约。</summary>
    public interface ICompositeModuleEditor
    {
        /// <summary>获取不注册到方案的内部编辑流程。</summary>
        Process GetEditableProcess();
        /// <summary>将已打开的内部编辑递归保存为快照。</summary>
        void SaveEditableProcess();
        /// <summary>编辑内部输入或输出端口，返回是否保存。</summary>
        bool ConfigurePorts(bool output, IWin32Window owner);
    }

    /// <summary>
    /// 组合模块节点，将已导入的流程快照作为当前节点的内部流程执行。
    /// </summary>
    public class NodeCompositeModule : NodeBase, ICompositeModuleEditor, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider, INodeSubscriptionDependencyProvider
    {
        /// <summary>保护缓存与运行入口之间的状态切换。</summary>
        private readonly object _runtimeSync = new object();

        /// <summary>创建模块参数窗体的界面上下文，内部窗体也必须归属这个线程。</summary>
        private readonly SynchronizationContext _editorContext;

        /// <summary>组合节点正在运行，包括内部流程启动前的输入注入阶段。</summary>
        private bool _executing;

        /// <summary>固定作用域编号不进入节点集合，仅用于保存模块输入订阅。</summary>
        private const int ModuleInputSourceId = int.MaxValue;

        /// <summary>复用现有动态变量协议的模块作用域输入，画布中没有对应节点。</summary>
        private sealed class ModuleInputSource : NodeCompositeInput, IProcessInputSource
        {
            /// <summary>在内部流程中建立独立的输入订阅来源。</summary>
            public ModuleInputSource(Process process) : base(ModuleInputSourceId, "模块输入", process, NodeType.CompositeInput) { }
            /// <summary>稳定来源实例，确保已打开订阅控件不持有旧对象。</summary>
            public NodeBase SubscriptionNode { get { return this; } }
            /// <summary>替换声明并清理已删除或类型变化变量的旧结果。</summary>
            public void SetDefinitions(List<CompositeInputPortDefinition> ports)
            {
                ParamForm.Params = new NodeParamCompositeInput { Ports = ports ?? new List<CompositeInputPortDefinition>() };
                Result = new NodeResultCompositeInput();
            }
            /// <summary>在流程批次建立后发布本轮输入。</summary>
            public void PrepareRun()
            {
                BeginProcessRun(Process.CurrentRunId);
                if (((NodeParamCompositeInput)ParamForm.Params).Ports.Count > 0)
                    Run(CancellationToken.None, false).GetAwaiter().GetResult();
            }
        }

        /// <summary>直接更新模块输入声明和外部绑定，并立即刷新内部订阅目录。</summary>
        public void UpdateInputPorts(List<CompositeInputPortDefinition> ports, List<CompositeInputBinding> bindings)
        {
            if (IsEditingBlocked || Solution.Instance.IsRunning) throw new InvalidOperationException("运行中不能修改模块输入。");
            GetEditableProcess();
            SaveEditableProcess();
            var param = (NodeParamCompositeModule)ParamForm.Params;
            param.InputPorts = ports.Select(port => new CompositeInputPortDefinition
                { Name = port.Name, ValueType = port.ValueType, DefaultValue = port.DefaultValue, Note = port.Note }).ToList();
            param.UsesDirectInputs = true;
            param.InputBindings = CompositeModuleSnapshotBuilder.SyncInputBindings(param.InputPorts, bindings);
            SynchronizeModuleInputs(param, true);
        }

        /// <summary>统一应用变量编辑结果，保留当前内部画布和已有订阅控件。</summary>
        public void UpdateModulePorts(List<CompositeInputPortDefinition> inputs, List<CompositeInputBinding> bindings,
            List<CompositeOutputPortDefinition> outputs)
        {
            UpdateInputPorts(inputs, bindings);
            var param = (NodeParamCompositeModule)ParamForm.Params;
            param.OutputPorts = outputs;
            param.UsesDirectOutputs = true;
            NotifyOutputDefinitionChanged();
        }

        /// <summary>同步声明而不替换内部流程，输入改名或改类型后立即广播。</summary>
        private void SynchronizeModuleInputs(NodeParamCompositeModule param, bool notify)
        {
            if (_runtimeProcess == null) return;
            var input = _runtimeProcess.ModuleInputSource?.SubscriptionNode as ModuleInputSource;
            if (input == null)
            {
                input = new ModuleInputSource(_runtimeProcess);
                _runtimeProcess.ModuleInputSource = input;
            }
            input.SetDefinitions(param.InputPorts);
            if (notify)
            {
                input.NotifyOutputDefinitionChanged();
                _runtimeProcess.NotifyConnectionsChanged();
            }
        }

        /// <summary>当前模块或所属流程是否正在执行。</summary>
        private bool IsEditingBlocked => _executing || (Process != null && Process.IsRuning) ||
            (_runtimeProcess != null && _runtimeProcess.IsRuning);

        /// <summary>获取同一个内部流程供画布编辑；空模块无需导入即可进入。</summary>
        public Process GetEditableProcess()
        {
            lock (_runtimeSync)
            {
                var param = ParamForm.Params as NodeParamCompositeModule;
                if (param == null)
                {
                    if (IsEditingBlocked) throw new InvalidOperationException("运行中不能创建内部流程。");
                    param = new NodeParamCompositeModule { ModuleName = NodeName };
                    ParamForm.Params = param;
                }
                if (IsEditingBlocked && (_runtimeProcess == null || !ReferenceEquals(_cachedParam, param)))
                    throw new InvalidOperationException("运行中不能替换内部流程。");
                if (IsEditingBlocked) return _runtimeProcess;
                return GetOrCreateRuntimeProcess(param, false);
            }
        }

        /// <summary>方案启动预加载时构建内部流程，使快照内的 AI 节点同步恢复参数并登记模型加载。</summary>
        /// <returns>用于启动预加载扫描的内部流程；未配置参数时返回 null。</returns>
        internal Process PrepareRuntimeProcessForStartupPreload()
        {
            lock (_runtimeSync)
            {
                var param = ParamForm.Params as NodeParamCompositeModule;
                if (param == null)
                    return null;

                return GetOrCreateRuntimeProcess(param, false);
            }
        }

        /// <summary>由内向外保存已打开的模块，保留来源、外部绑定和画布对象。</summary>
        public void SaveEditableProcess()
        {
            SaveEditableProcessCore(false);
        }

        /// <summary>运行入口可提交尚未执行的内部编辑，即使其父流程已经开始运行。</summary>
        private void SaveEditableProcessCore(bool preparingRun)
        {
            lock (_runtimeSync)
            {
                // 运行中自动保存方案时沿用最后一次可编辑快照。
                if (_runtimeProcess == null || _executing || _runtimeProcess.IsRuning ||
                    (!preparingRun && Process != null && Process.IsRuning)) return;
                var previous = ParamForm.Params as NodeParamCompositeModule;
                // 参数窗口重新导入后，旧画布不能覆盖新配置。
                if (!ReferenceEquals(previous, _cachedParam)) return;
                foreach (var child in _runtimeProcess.Nodes.OfType<NodeCompositeModule>()) child.SaveEditableProcessCore(preparingRun);
                EnsureOutputConnections(_runtimeProcess, !preparingRun);
                var snapshot = CompositeModuleSnapshotBuilder.CreateFromProcess(_runtimeProcess);
                snapshot.ModuleName = previous.ModuleName;
                snapshot.SourceProcessId = previous.SourceProcessId;
                snapshot.SourceProcessName = previous.SourceProcessName;
                snapshot.UsesDirectInputs = previous.UsesDirectInputs;
                snapshot.UsesDirectOutputs = previous.UsesDirectOutputs;
                if (previous.UsesDirectOutputs) snapshot.OutputPorts = previous.OutputPorts;
                if (previous.UsesDirectInputs) snapshot.InputPorts = previous.InputPorts;
                snapshot.InputBindings = CompositeModuleSnapshotBuilder.SyncInputBindings(snapshot.InputPorts, previous.InputBindings);
                ParamForm.Params = snapshot;
                _cachedParam = snapshot;
                // 运行线程只提交参数，不跨线程刷新配置窗体。
                if (!preparingRun) NotifyOutputDefinitionChanged();
            }
        }

        /// <summary>直接配置内部端口，只有保存成功才保留新建节点。</summary>
        public bool ConfigurePorts(bool output, IWin32Window owner)
        {
            if (IsEditingBlocked || Solution.Instance.IsRunning) throw new InvalidOperationException("运行中不能修改组合端口。");
            GetEditableProcess();
            SaveEditableProcess();
            var previous = ParamForm.Params;
            var form = (NodeParamFormCompositeModule)ParamForm;
            form.ShowVariablesOnly(true);
            form.SetParam2Form();
            form.SelectVariablePage(output);
            try { form.ShowDialog(owner); }
            finally { form.ShowVariablesOnly(false); }
            return !ReferenceEquals(previous, ParamForm.Params);
        }
        /// <summary>为输出订阅建立依赖连线，使内部结果无需手工连线即可被发布。</summary>
        private static void EnsureOutputConnections(Process process, bool notify = true)
        {
            bool changed = false;
            foreach (var output in process.Nodes.OfType<NodeCompositeOutput>())
            {
                string prefix = "组合端口自动连线_" + output.ID + "_";
                var sourceIds = new HashSet<int>(output.GetSubscriptionDependencyNodeIds());
                var desired = new Dictionary<string, ProcessConnection>();
                foreach (int sourceId in sourceIds)
                {
                    NodeBase source = process.Nodes.FirstOrDefault(item => item.ID == sourceId);
                    // 已在手工控制图中时只使用隐藏订阅依赖，不能直连绕过用户条件分支。
                    if (source == null || HasManualConnectionPath(process, sourceId, output.ID)) continue;
                    bool conditional = source.NodeType == NodeType.If || source.NodeType == NodeType.MultiCondition;
                    var branches = conditional ? new[] { ProcessConnectionBranch.True, ProcessConnectionBranch.False }
                        : new[] { ProcessConnectionBranch.Default };
                    foreach (var branch in branches)
                    {
                        string id = prefix + sourceId + "_" + branch;
                        // If 不支持公共默认出口；成对分支保证真假结果都发布，且不会修改其他分支。
                        desired[id] = new ProcessConnection { ID = id, FromNodeId = sourceId, ToNodeId = output.ID,
                            FromAnchor = "Legacy", ToAnchor = "Left", Branch = branch };
                    }
                }
                // 只维护本入口创建的连线，保留用户手工设置的分支与布局。
                foreach (var stale in process.Connections.Where(edge => edge.ID != null && edge.ID.StartsWith(prefix, StringComparison.Ordinal) && !desired.ContainsKey(edge.ID)).ToList())
                {
                    process.Connections.Remove(stale);
                    changed = true;
                }
                foreach (var edge in desired.Values)
                    if (!process.Connections.Any(existing => existing.ID == edge.ID))
                    {
                        process.Connections.Add(edge);
                        changed = true;
                    }
            }
            if (changed && notify) process.NotifyConnectionsChanged();
        }

        /// <summary>检查两个节点是否已在用户的同一控制组件，忽略自动生成的输出依赖边。</summary>
        private static bool HasManualConnectionPath(Process process, int sourceId, int targetId)
        {
            var visited = new HashSet<int> { sourceId };
            var pending = new Queue<int>();
            pending.Enqueue(sourceId);
            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                foreach (var edge in process.Connections)
                {
                    if (edge.ID != null && edge.ID.StartsWith("组合端口自动连线_", StringComparison.Ordinal)) continue;
                    int next = edge.FromNodeId == current ? edge.ToNodeId : edge.ToNodeId == current ? edge.FromNodeId : 0;
                    if (next == targetId) return true;
                    if (next > 0 && visited.Add(next)) pending.Enqueue(next);
                }
            }
            return false;
        }

        /// <summary>
        /// 最近一次构建内部流程所使用的参数对象。
        /// </summary>
        private NodeParamCompositeModule _cachedParam;

        /// <summary>
        /// 根据快照构建的内部流程，重复运行时复用以减少控件和参数对象创建成本。
        /// </summary>
        private Process _runtimeProcess;
        /// <summary>供只读运行界面访问已创建的内部流程；绝不触发初始化、参数保存或执行。</summary>
        internal Process ExistingRuntimeProcess => ReferenceEquals(_cachedParam, ParamForm?.Params)
            ? Volatile.Read(ref _runtimeProcess) : null;

        /// <summary>
        /// 创建组合模块节点。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeCompositeModule(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormCompositeModule(process);
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                _editorContext = SynchronizationContext.Current as WindowsFormsSynchronizationContext;
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultCompositeModule();
        }

        /// <summary>
        /// 获取输入绑定依赖的外部源节点 ID，供图运行器等待上游订阅结果。
        /// </summary>
        /// <returns>源节点 ID 集合。</returns>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds()
        {
            HashSet<int> nodeIds = new HashSet<int>();
            NodeParamCompositeModule param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeModule;
            if (param == null || param.InputBindings == null)
                return nodeIds;

            foreach (CompositeInputBinding binding in param.InputBindings)
            {
                if (binding == null ||
                    binding.SourceMode != CompositePortValueSourceMode.Subscription ||
                    binding.SourceNodeId <= 0 ||
                    binding.SourceNodeId == ID)
                {
                    continue;
                }

                nodeIds.Add(binding.SourceNodeId);
            }

            return nodeIds;
        }

        /// <summary>
        /// 获取组合模块对外声明的动态输出变量名。
        /// </summary>
        /// <returns>输出变量名集合。</returns>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NodeParamCompositeModule param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeModule;
            if (param != null && param.OutputPorts != null)
            {
                foreach (CompositeOutputPortDefinition port in param.OutputPorts)
                {
                    if (port == null || string.IsNullOrWhiteSpace(port.Name))
                        continue;

                    names.Add(port.Name.Trim());
                }
            }

            return names.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 获取组合模块输出变量类型。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <param name="valueType">值类型。</param>
        /// <returns>找到变量定义时返回 true。</returns>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            NodeParamCompositeModule param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeModule;
            if (param != null && param.OutputPorts != null)
            {
                foreach (CompositeOutputPortDefinition port in param.OutputPorts)
                {
                    if (port == null ||
                        !string.Equals(port.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    valueType = CompositePortValueHelper.GetOutputRuntimeType(port);
                    object actualValue;
                    if (port.ValueType == CompositePortValueType.Any && DynamicResultVariableResolver.TryGetValue(Result, name, out actualValue) && actualValue != null)
                        valueType = actualValue.GetType();
                    return true;
                }
            }

            NodeResultCompositeModule result = Result as NodeResultCompositeModule;
            return result != null && result.TryGetDynamicResultVariableType(name, out valueType);
        }

        /// <summary>
        /// 运行组合模块内部流程。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回值。</returns>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            await PrepareRuntimeProcessAsync(token).ConfigureAwait(false);
            lock (_runtimeSync)
            {
                if (_executing || (_runtimeProcess != null && _runtimeProcess.IsRuning))
                    throw new InvalidOperationException("组合模块正在运行，请等待本次运行结束。");
                // 顶层运行无需先退出内部画布：提交已打开的最新节点和端口后再校验执行。
                SaveEditableProcessCore(true);
                _executing = true;
            }
            try { return await RunCore(token, showLog).ConfigureAwait(false); }
            finally { lock (_runtimeSync) { _executing = false; } }
        }

        /// <summary>首次运行时在界面线程恢复内部控件，避免后台句柄使停止后打开参数窗体卡住。</summary>
        /// <param name="token">全局停止令牌。</param>
        private async Task PrepareRuntimeProcessAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_runtimeSync)
            {
                if (_runtimeProcess != null && ReferenceEquals(_cachedParam, ParamForm.Params)) return;
            }
            if (_editorContext == null || ReferenceEquals(_editorContext, SynchronizationContext.Current))
                return;

            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            // 0 表示排队，1 表示已开始构建，2 表示排队期间已取消。
            // 已开始的构建仍拥有控件和缓存，不能由取消回调提前释放运行会话。
            int preparationState = 0;
            using (token.Register(() =>
            {
                if (Interlocked.CompareExchange(ref preparationState, 2, 0) == 0)
                    completion.TrySetCanceled();
            }))
            {
                // 不持有模块锁投递到界面，避免界面导航和后台准备互相等待。
                _editorContext.Post(_ =>
                {
                    if (Interlocked.CompareExchange(ref preparationState, 1, 0) != 0) return;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        lock (_runtimeSync)
                        {
                            var param = ParamForm.Params as NodeParamCompositeModule;
                            ValidateParam(param);
                            GetOrCreateRuntimeProcess(param, false);
                        }
                        token.ThrowIfCancellationRequested();
                        completion.TrySetResult(null);
                    }
                    catch (OperationCanceledException) { completion.TrySetCanceled(); }
                    catch (Exception ex) { completion.TrySetException(ex); }
                }, null);
                await completion.Task.ConfigureAwait(false);
            }
        }

        /// <summary>执行输入注入、内部流程与结果收集。</summary>
        private async Task<NodeReturn> RunCore(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                return new NodeReturn(NodeRunFlag.StopRun);
            }

            if (!(ParamForm.Params is NodeParamCompositeModule param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                Result = BuildResult(null, _runtimeProcess, false, "运行参数未设置或保存！");
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                ValidateParam(param);
                SetStatus(NodeStatus.Unexecuted, "*");
                await base.CheckTokenCancel(token);

                Process runtimeProcess = GetOrCreateRuntimeProcess(param, showLog);
                InjectInputValues(runtimeProcess, param);
                bool internalSucceeded;
                // 仅显式模块调用继承父根，重复内部流程编号不参与日志归属判断。
                using (RuntimeLogContext.BeginComposite(runtimeProcess, ID + "." + NodeName))
                    internalSucceeded = await runtimeProcess.RunWithResultAsync(false, token).ConfigureAwait(false);
                await base.CheckTokenCancel(token);

                if (!internalSucceeded || !runtimeProcess.Success)
                    throw new Exception("组合模块内部流程运行失败！");

                NodeResultCompositeModule result = BuildResult(param, runtimeProcess, true, string.Empty);
                CollectOutputValues(result, runtimeProcess, param);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                result.RunTime = time;
                Result = result;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})运行成功！内部节点：{result.InternalNodeCount}，输出：{result.OutputValuesText}，内部耗时：{result.InternalRunTime} ms，节点耗时：{time} ms",
                        true);
                }

                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                Result = BuildResult(param, _runtimeProcess, false, "运行取消");
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                Result = BuildResult(param, _runtimeProcess, false, ex.Message);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 释放内部流程节点，避免组合模块删除后保留运行快照对象。
        /// </summary>
        /// <param name="disposing">是否释放托管对象。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeRuntimeProcess();

            base.Dispose(disposing);
        }

        /// <summary>
        /// 校验组合模块参数是否包含可运行的快照。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        private static void ValidateParam(NodeParamCompositeModule param)
        {
            if (param == null)
                throw new Exception("组合模块参数为空！");

            if ((param.NodeInfos == null || param.NodeInfos.Count == 0) && !(param.UsesDirectInputs && param.InputPorts.Count > 0))
                throw new Exception("组合模块未导入任何内部节点！");
        }

        /// <summary>
        /// 根据外部绑定解析输入值，并注入到内部组合输入节点。
        /// </summary>
        /// <param name="runtimeProcess">内部流程。</param>
        /// <param name="param">组合模块参数。</param>
        private void InjectInputValues(Process runtimeProcess, NodeParamCompositeModule param)
        {
            Dictionary<string, object> inputValues = ResolveInputValues(param);
            if (runtimeProcess == null || runtimeProcess.Nodes == null)
                return;
            ((NodeCompositeInput)runtimeProcess.ModuleInputSource.SubscriptionNode).SetRuntimeInputValues(inputValues);

            foreach (NodeBase node in runtimeProcess.Nodes)
            {
                NodeCompositeInput inputNode = node as NodeCompositeInput;
                if (inputNode != null)
                    inputNode.SetRuntimeInputValues(inputValues);
            }
        }

        /// <summary>
        /// 解析组合模块每个输入端口的外部绑定值。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <returns>输入端口值表。</returns>
        private Dictionary<string, object> ResolveInputValues(NodeParamCompositeModule param)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (param == null || param.InputPorts == null)
                return values;

            List<CompositeInputBinding> bindings = CompositeModuleSnapshotBuilder.SyncInputBindings(
                param.InputPorts,
                param.InputBindings);
            param.InputBindings = bindings;

            foreach (CompositeInputPortDefinition port in param.InputPorts)
            {
                if (port == null || string.IsNullOrWhiteSpace(port.Name))
                    continue;

                string name = port.Name.Trim();
                CompositeInputBinding binding = FindInputBinding(bindings, name);
                object value;
                if (binding == null || binding.SourceMode == CompositePortValueSourceMode.Constant)
                {
                    string constantText = binding == null ? port.DefaultValue : binding.ConstantText;
                    value = CompositePortValueHelper.ConvertConstant(constantText, port.ValueType, "输入端口“" + name + "”");
                }
                else
                {
                    object rawValue = CompositePortValueHelper.ReadSourceValue(
                        this,
                        binding.SourceNodeId,
                        binding.SourceNodeText,
                        binding.PropertyPath,
                        binding.PropertyDisplayName);
                    value = CompositePortValueHelper.ConvertValue(rawValue, port.ValueType, "输入端口“" + name + "”");
                }

                values[name] = value;
            }

            return values;
        }

        /// <summary>
        /// 查找指定输入端口的绑定。
        /// </summary>
        /// <param name="bindings">绑定列表。</param>
        /// <param name="portName">端口名称。</param>
        /// <returns>匹配的绑定。</returns>
        private static CompositeInputBinding FindInputBinding(List<CompositeInputBinding> bindings, string portName)
        {
            if (bindings == null || string.IsNullOrWhiteSpace(portName))
                return null;

            foreach (CompositeInputBinding binding in bindings)
            {
                if (binding != null &&
                    string.Equals(binding.PortName, portName, StringComparison.OrdinalIgnoreCase))
                {
                    return binding;
                }
            }

            return null;
        }

        /// <summary>
        /// 从内部组合输出节点收集外部可订阅输出变量。
        /// </summary>
        /// <param name="result">组合模块结果。</param>
        /// <param name="runtimeProcess">内部流程。</param>
        /// <param name="param">组合模块参数。</param>
        private static void CollectOutputValues(
            NodeResultCompositeModule result,
            Process runtimeProcess,
            NodeParamCompositeModule param)
        {
            if (result == null)
                return;

            result.OutputValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            result.OutputValueTypes = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase);

            if (runtimeProcess == null || runtimeProcess.Nodes == null)
                return;

            if (param.UsesDirectOutputs)
            {
                foreach (var port in param.OutputPorts)
                {
                    var source = runtimeProcess.Nodes.FirstOrDefault(node => node.ID == port.SourceNodeId);
                    if (source == null && runtimeProcess.ModuleInputSource.SubscriptionNode.ID == port.SourceNodeId)
                        source = runtimeProcess.ModuleInputSource.SubscriptionNode;
                    if (source == null || !source.HasSuccessfulResultForRun(runtimeProcess.CurrentRunId))
                        throw new InvalidOperationException("输出“" + port.Name + "”的来源本轮未成功运行。");
                    object value = CompositePortValueHelper.ReadSourceValue(runtimeProcess.ModuleInputSource.SubscriptionNode,
                        port.SourceNodeId, port.SourceNodeText, port.PropertyPath, port.PropertyDisplayName);
                    result.OutputValues[port.Name] = CompositePortValueHelper.ConvertValue(value, port.ValueType, "输出“" + port.Name + "”");
                    result.OutputValueTypes[port.Name] = port.ValueType;
                }
                result.OutputValuesText = NodeResultCompositeModule.BuildValuesText(result.OutputValues);
                return;
            }

            foreach (NodeBase node in runtimeProcess.Nodes)
            {
                if (node == null || !node.HasSuccessfulResultForRun(runtimeProcess.CurrentRunId))
                    continue;

                NodeResultCompositeOutput outputResult = node == null ? null : node.Result as NodeResultCompositeOutput;
                if (outputResult == null || outputResult.Values == null)
                    continue;

                foreach (KeyValuePair<string, object> pair in outputResult.Values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;

                    result.OutputValues[pair.Key.Trim()] = pair.Value;
                }

                if (outputResult.ValueTypes == null)
                    continue;

                foreach (KeyValuePair<string, CompositePortValueType> pair in outputResult.ValueTypes)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;

                    result.OutputValueTypes[pair.Key.Trim()] = pair.Value;
                }
            }

            if (param != null && param.OutputPorts != null)
            {
                foreach (CompositeOutputPortDefinition port in param.OutputPorts)
                {
                    if (port == null || string.IsNullOrWhiteSpace(port.Name))
                        continue;

                    string name = port.Name.Trim();
                    if (!result.OutputValueTypes.ContainsKey(name))
                        result.OutputValueTypes[name] = port.ValueType;
                }
            }

            result.OutputValuesText = NodeResultCompositeModule.BuildValuesText(result.OutputValues);
        }

        /// <summary>
        /// 获取内部流程，参数对象变化时重新构建。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <param name="showLog">是否输出内部流程日志。</param>
        /// <returns>可执行的内部流程。</returns>
        private Process GetOrCreateRuntimeProcess(NodeParamCompositeModule param, bool showLog)
        {
            if (_runtimeProcess == null || !ReferenceEquals(_cachedParam, param))
            {
                // 新快照先完整构建，避免构建失败时把当前画布对象提前释放。
                Process replacement = BuildRuntimeProcess(param);
                DisposeRuntimeProcess();
                _runtimeProcess = replacement;
                _cachedParam = param;
                SynchronizeModuleInputs(param, false);
            }

            _runtimeProcess.ShowLog = showLog;
            _runtimeProcess.Enable = true;
            return _runtimeProcess;
        }

        /// <summary>
        /// 根据快照构建内部流程。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <returns>内部流程实例。</returns>
        private Process BuildRuntimeProcess(NodeParamCompositeModule param)
        {
            // 首次全局运行可能在没有消息循环的工作线程上恢复参数控件。
            // 禁止 WinForms 自动安装同步上下文，否则节点 await 会把完成和取消
            // 回调投递到无人处理的隐藏窗口，永久占用整个方案的运行会话。
            SynchronizationContext previousContext = SynchronizationContext.Current;
            bool previousAutoInstall = WindowsFormsSynchronizationContext.AutoInstall;
            try
            {
                WindowsFormsSynchronizationContext.AutoInstall = false;
                return BuildRuntimeProcessCore(param);
            }
            finally
            {
                WindowsFormsSynchronizationContext.AutoInstall = previousAutoInstall;
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        /// <summary>在受保护的同步上下文范围内恢复内部节点及订阅。</summary>
        /// <param name="param">组合模块快照。</param>
        /// <returns>尚未运行的内部流程。</returns>
        private Process BuildRuntimeProcessCore(NodeParamCompositeModule param)
        {
            Stopwatch moduleWatch = Stopwatch.StartNew();
            Process runtimeProcess = new Process(BuildRuntimeProcessName(param))
            {
                ID = -Math.Abs(ID == 0 ? 1 : ID),
                Enable = true,
                ShowLog = false,
                HasCanvasGraph = param.HasCanvasGraph,
                Group = Process == null ? ProcessGroup.Group1 : Process.Group,
                RunLv = Process == null ? ProcessLvEnum.Lv5 : Process.RunLv
            };

            // 参数控件在节点构造及恢复时解析订阅，作用域必须先于任何内部节点存在。
            var moduleInput = new ModuleInputSource(runtimeProcess);
            moduleInput.SetDefinitions(param.InputPorts);
            runtimeProcess.ModuleInputSource = moduleInput;

            Dictionary<int, NodeBase> runtimeNodes = new Dictionary<int, NodeBase>();
            foreach (NodeConfig nodeConfig in param.NodeInfos ?? new List<NodeConfig>())
            {
                if (nodeConfig == null)
                    continue;

                Stopwatch nodeWatch = Stopwatch.StartNew();
                try
                {
                    NodeBase node = NodeFactory.CreateNode(nodeConfig.ID, nodeConfig.NodeName, runtimeProcess, nodeConfig.NodeType);
                    node.Active = nodeConfig.Active;
                    node.OutputLog = nodeConfig.OutputLog ?? true;
                    node.SetNotes(nodeConfig.Notes ?? "无备注");
                    node.Selected = false;
                    node.CanvasLocation = new Point(nodeConfig.CanvasX, nodeConfig.CanvasY);
                    if (nodeConfig.CanvasWidth > 0 && nodeConfig.CanvasHeight > 0)
                        node.CanvasSize = new Size(nodeConfig.CanvasWidth, nodeConfig.CanvasHeight);
                    node.IsStartNode = nodeConfig.IsStartNode;

                    if (node.ParamForm != null && nodeConfig.NodeParam != null)
                    {
                        node.ParamForm.Params = CompositeModuleSnapshotBuilder.CloneNodeParam(nodeConfig.NodeParam);
                        node.ParamForm.SetParam2Form();
                    }

                    runtimeProcess.AddNode(node);
                    runtimeNodes[node.ID] = node;
                    LogHelper.AddLog(MsgLevel.Info,
                        $"组合模块内部节点加载成功：模块={runtimeProcess.ProcessName}，节点={node.ID}.{node.NodeName}，类型={nodeConfig.NodeType}，耗时={nodeWatch.Elapsed.TotalMilliseconds:F2}ms。", true);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception,
                        $"组合模块内部节点加载失败：模块={runtimeProcess.ProcessName}，节点={nodeConfig.ID}.{nodeConfig.NodeName}，类型={nodeConfig.NodeType}，耗时={nodeWatch.Elapsed.TotalMilliseconds:F2}ms，原因：{ex.Message}", true);
                    throw;
                }
            }

            RestoreConnections(runtimeProcess, param, runtimeNodes);
            EnsureOutputConnections(runtimeProcess);
            LogHelper.AddLog(MsgLevel.Info,
                $"组合模块内部流程加载完成：模块={runtimeProcess.ProcessName}，节点数={runtimeNodes.Count}，总耗时={moduleWatch.Elapsed.TotalMilliseconds:F2}ms。", true);
            return runtimeProcess;
        }

        /// <summary>
        /// 还原内部流程连线，保持来源流程的图执行语义。
        /// </summary>
        /// <param name="runtimeProcess">内部流程。</param>
        /// <param name="param">组合模块参数。</param>
        /// <param name="runtimeNodes">内部节点索引。</param>
        private static void RestoreConnections(
            Process runtimeProcess,
            NodeParamCompositeModule param,
            Dictionary<int, NodeBase> runtimeNodes)
        {
            runtimeProcess.Connections.Clear();

            if (param.ConnectionInfos != null && param.ConnectionInfos.Count > 0)
            {
                foreach (ProcessConnectionConfig connectionInfo in param.ConnectionInfos)
                {
                    if (connectionInfo == null ||
                        !runtimeNodes.ContainsKey(connectionInfo.FromNodeId) ||
                        !runtimeNodes.ContainsKey(connectionInfo.ToNodeId))
                    {
                        continue;
                    }

                    ProcessConnection connection = new ProcessConnection
                    {
                        FromNodeId = connectionInfo.FromNodeId,
                        ToNodeId = connectionInfo.ToNodeId,
                        FromAnchor = string.IsNullOrEmpty(connectionInfo.FromAnchor) ? "Right" : connectionInfo.FromAnchor,
                        ToAnchor = string.IsNullOrEmpty(connectionInfo.ToAnchor) ? "Left" : connectionInfo.ToAnchor,
                        Branch = connectionInfo.Branch
                    };

                    if (!string.IsNullOrEmpty(connectionInfo.ID))
                        connection.ID = connectionInfo.ID;

                    runtimeProcess.NormalizeConnectionBranch(connection);
                    runtimeProcess.Connections.Add(connection);
                }
            }
            else if (!param.HasCanvasGraph)
            {
                CreateLegacyOrderedConnections(runtimeProcess);
            }

            runtimeProcess.NotifyConnectionsChanged();
        }

        /// <summary>
        /// 为旧顺序流程生成兼容连线。
        /// </summary>
        /// <param name="runtimeProcess">内部流程。</param>
        private static void CreateLegacyOrderedConnections(Process runtimeProcess)
        {
            for (int index = 0; index < runtimeProcess.Nodes.Count - 1; index++)
            {
                runtimeProcess.Connections.Add(new ProcessConnection
                {
                    FromNodeId = runtimeProcess.Nodes[index].ID,
                    ToNodeId = runtimeProcess.Nodes[index + 1].ID,
                    FromAnchor = "Legacy",
                    ToAnchor = "Left"
                });
            }
        }

        /// <summary>
        /// 构建内部流程名称，避免和用户流程名称冲突。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <returns>内部流程名称。</returns>
        private string BuildRuntimeProcessName(NodeParamCompositeModule param)
        {
            string moduleName = string.IsNullOrWhiteSpace(param.ModuleName)
                ? NodeName
                : param.ModuleName.Trim();

            int ownerProcessId = Process == null ? 0 : Process.ID;
            return $"__组合模块_{ownerProcessId}_{ID}_{moduleName}";
        }

        /// <summary>
        /// 构建组合模块运行结果。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <param name="runtimeProcess">内部流程。</param>
        /// <param name="success">内部流程是否成功。</param>
        /// <param name="message">运行消息。</param>
        /// <returns>组合模块结果。</returns>
        private static NodeResultCompositeModule BuildResult(
            NodeParamCompositeModule param,
            Process runtimeProcess,
            bool success,
            string message)
        {
            return new NodeResultCompositeModule
            {
                ModuleName = param == null ? string.Empty : param.ModuleName,
                InternalRunTime = runtimeProcess == null ? 0 : runtimeProcess.RunTime,
                InternalNodeCount = runtimeProcess == null ? 0 : runtimeProcess.Nodes.Count,
                InternalSuccess = success,
                Message = message ?? string.Empty,
                OutputValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                OutputValueTypes = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase),
                OutputValuesText = string.Empty
            };
        }

        /// <summary>
        /// 释放已缓存的内部流程节点。
        /// </summary>
        private void DisposeRuntimeProcess()
        {
            if (_runtimeProcess != null)
            {
                _runtimeProcess.ModuleInputSource?.SubscriptionNode.Dispose();
                foreach (NodeBase node in _runtimeProcess.Nodes)
                    node.Dispose();
            }

            _runtimeProcess = null;
            _cachedParam = null;
        }
    }
}
