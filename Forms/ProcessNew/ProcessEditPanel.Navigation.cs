using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class ProcessEditPanel
    {
        /// <summary>顶层流程，进入组合模块时始终保持不变。</summary>
        private Process _rootProcess;
        /// <summary>从顶层到当前组合模块的实例路径。</summary>
        private readonly List<NodeCompositeModule> _compositePath = new List<NodeCompositeModule>();
        /// <summary>单独调试组合模块期间禁止编辑与重复运行。</summary>
        private bool _compositeRunInProgress;
        /// <summary>获取此选项卡所属顶层流程。</summary>
        public Process RootProcess { get { return _rootProcess; } }
        /// <summary>获取画布当前显示的流程。</summary>
        public Process CurrentProcess { get { return _process; } }
        /// <summary>当前完整层级路径，用于顶部导航显示。</summary>
        public string NavigationPath { get { return string.Join(" > ", new[] { _rootProcess.ProcessName }.Concat(_compositePath.Select(n => n.NodeName))); } }
        /// <summary>编辑器切换层级后通知顶部刷新标题。</summary>
        public event EventHandler NavigationChanged;

        /// <summary>绑定原位编辑事件，不创建额外顶层流程。</summary>
        private void InitializeCompositeNavigation()
        {
            _rootProcess = _process;
            processFlowCanvas.CompositeModuleOpenRequested += OpenCompositeModule;
            processFlowCanvas.CompositeModulePortsRequested += ConfigureCompositeVariables;
            UpdateNavigationControls();
        }

        /// <summary>齿轮直接打开模块变量配置，保存后更新当前画布和订阅目录。</summary>
        private void ConfigureCompositeVariables(NodeBase node)
        {
            var module = node as NodeCompositeModule;
            if (module == null || Solution.Instance.IsRunning) return;
            module.ConfigurePorts(false, this);
            processFlowCanvas.Invalidate();
            NavigationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>双击组合模块后进入其内部画布。</summary>
        private void OpenCompositeModule(NodeBase node)
        {
            var module = node as NodeCompositeModule;
            if (module == null) return;
            NavigateToComposite(_compositePath.Select(n => n.ID).Concat(new[] { module.ID }).ToArray());
        }

        /// <summary>按照稳定节点编号路径定位组合模块；空路径返回根流程。</summary>
        /// <param name="nodeIds">从顶层开始的组合节点编号。</param>
        /// <returns>路径有效且已完成切换时返回真。</returns>
        public bool NavigateToComposite(int[] nodeIds)
        {
            if (nodeIds == null || Solution.Instance.IsRunning || _compositeRunInProgress) return false;
            try
            {
                FlushCompositeEdits();
                var nextPath = new List<NodeCompositeModule>();
                Process target = _rootProcess;
                foreach (int id in nodeIds)
                {
                    var module = target.Nodes.OfType<NodeCompositeModule>().FirstOrDefault(n => n.ID == id);
                    if (module == null) return false;
                    target = module.GetEditableProcess();
                    nextPath.Add(module);
                }
                _compositePath.Clear();
                _compositePath.AddRange(nextPath);
                BindNavigationProcess(target);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, "打开组合模块失败：" + ex.Message, true);
                return false;
            }
        }

        /// <summary>返回上一层，同时保留内部编辑。</summary>
        public bool NavigateToParent()
        {
            return _compositePath.Count > 0 && NavigateToComposite(_compositePath.Take(_compositePath.Count - 1).Select(n => n.ID).ToArray());
        }

        /// <summary>由内向外提交已打开模块快照，供方案保存和导航共用。</summary>
        public void FlushCompositeEdits()
        {
            if (_compositeRunInProgress || Solution.Instance.IsRunning) return;
            foreach (var module in _rootProcess.Nodes.OfType<NodeCompositeModule>()) module.SaveEditableProcess();
        }

        /// <summary>切换画布模型，重建当前节点索引并刷新路径。</summary>
        private void BindNavigationProcess(Process process)
        {
            _process = process;
            _stack = new Stack<NodeBase>(process.Nodes);
            processFlowCanvas.BindProcess(process);
            SetProcessEnable(process.Enable, true, false);
            UpdateNodeCountText();
            UpdateRunTimeText();
            UpdateNavigationControls();
            NavigationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>同步组合模块工具栏和路径提示。</summary>
        private void UpdateNavigationControls()
        {
            bool inside = _compositePath.Count > 0;
            panelCompositeNavigation.Visible = inside;
            labelCompositePath.Text = _rootProcess == null ? string.Empty : NavigationPath;
            buttonCompositeBack.Enabled = !_compositeRunInProgress;
            buttonCompositeInputs.Enabled = !_compositeRunInProgress;
            buttonCompositeOutputs.Enabled = !_compositeRunInProgress;
        }

        /// <summary>返回按钮保留当前编辑后回到父层。</summary>
        private void buttonCompositeBack_Click(object sender, EventArgs e) { NavigateToParent(); }
        /// <summary>打开当前组合模块输入端口配置。</summary>
        private void buttonCompositeInputs_Click(object sender, EventArgs e) { ConfigureCurrentCompositePorts(false); }
        /// <summary>打开当前组合模块输出端口配置。</summary>
        private void buttonCompositeOutputs_Click(object sender, EventArgs e) { ConfigureCurrentCompositePorts(true); }

        /// <summary>配置端口后更新快照和当前内部画布。</summary>
        private void ConfigureCurrentCompositePorts(bool output)
        {
            if (_compositePath.Count == 0 || Solution.Instance.IsRunning) return;
            try
            {
                var module = _compositePath.Last();
                FlushCompositeEdits();
                module.ConfigurePorts(output, FindForm());
                BindNavigationProcess(module.GetEditableProcess());
                FlushCompositeEdits();
            }
            catch (Exception ex) { LogHelper.AddLog(MsgLevel.Exception, "配置组合端口失败：" + ex.Message, true); }
        }

        /// <summary>在工作线程运行当前组合，完整执行输入注入及对外输出发布。</summary>
        private async Task RunCurrentCompositeAsync(bool loop)
        {
            if (_compositeRunInProgress || Solution.Instance.IsRunning || _compositePath.Count == 0) return;
            var module = _compositePath.Last();
            FlushCompositeEdits();
            // 在界面线程准备内部节点，避免后台创建参数窗体。
            module.GetEditableProcess();
            if (!ValidateCameraConfigurationBeforeRun() || !Solution.Instance.TryBeginExternalRunSession()) return;
            _compositeRunInProgress = true;
            _isLoopRunRequested = loop;
            processFlowCanvas.Enabled = false;
            buttonRun.Enabled = buttonLoop.Enabled = false;
            buttonStop.Enabled = true;
            UpdateNavigationControls();
            System.Collections.Generic.List<TDJS_Vision.Device.Camera.ICamera> startedCameras = null;
            try
            {
                startedCameras = Solution.Instance.StartCameraCallbackGrabbingForProcess(_process);
                do
                {
                    // 组合模块内部调试运行时沿用顶层流程和当前组合节点的日志开关，
                    // 避免进入模块后运行子节点却完全看不到节点运行日志。
                    bool showLog = _rootProcess != null && _rootProcess.ShowLog && module.OutputLog;
                    await Task.Run(() => module.Run(Solution.Instance.CancellationToken, showLog));
                    RefreshRuntimeCanvasState();
                    if (!_isLoopRunRequested) break;
                    await Task.Delay(Math.Max(1, Solution.Instance.RunInterval), Solution.Instance.CancellationToken);
                } while (!Solution.Instance.CancellationToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { LogHelper.AddLog(MsgLevel.Exception, "组合模块运行失败：" + ex.Message, true); }
            finally
            {
                try { Solution.Instance.StopStartedCameraCallbackGrabbing(startedCameras); }
                finally
                {
                    Solution.Instance.EndExternalRunSession();
                    _compositeRunInProgress = _isLoopRunRequested = false;
                    if (!IsDisposed)
                    {
                        processFlowCanvas.Enabled = true;
                        buttonRun.Enabled = buttonLoop.Enabled = true;
                        buttonStop.Enabled = false;
                        UpdateNavigationControls();
                        RefreshRuntimeCanvasState();
                    }
                }
            }
        }
    }
}
