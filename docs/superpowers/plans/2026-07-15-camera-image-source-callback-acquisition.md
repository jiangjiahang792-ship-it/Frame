# 相机图像源统一回调取图实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将二维相机图像源统一改为同一轮流程内等待相机回调取图，软触发保留上游执行，硬触发非法上游配置在运行前统一拦截，并保证停止操作可取消等待和停止本次取流。

**Architecture:** 新增单帧回调等待接口与线程安全实现，`NodeImageSource` 在调用软触发或等待硬触发前登记等待，回调只交付 `Mat` 并唤醒同一轮节点。新增可插拔流程相机配置校验器，由 `Solution` 提供统一校验和相机取流会话，流程编辑页与主窗体只负责展示中文错误。

**Tech Stack:** C# 7.3、.NET Framework 4.8、WinForms、OpenCvSharp `Mat`、`TaskCompletionSource<T>`、`CancellationToken`、PowerShell 回归检查、MSBuild。

## 全局约束

- 所有中文文本使用简体中文并按 UTF-8 保存。
- 新增类、接口、字段、属性和方法必须使用 XML 或行内中文注释说明职责。
- WinForm 控件和布局变更必须写入 `.Designer.cs`，保证设计器中可见。
- 相机回调线程不得运行算法、阻塞等待或调用 `Process.Run()`。
- 相机图像源运行路径不得调用 `GetOneFrameImage()`，也不得设置同步取图回退。
- 软件触发必须先登记等待，再且只调用一次 `GrabOne()`。
- 不建立无界帧队列；每个图像源同一时间最多存在一个待处理回调。
- 保留 `IsCameraCallbackTriggered` 配置字段用于旧方案反序列化，但运行逻辑不得依赖它。
- 修改每一阶段后同步更新 `FLOW_CANVAS_B_PLAN_TASKS.md`。

---

## 文件结构映射

- 新建 `Node/1-Acquisition/ImageSource/CameraFrameAwaiter.cs`：定义 `ICameraFrameAwaiter` 与默认线程安全实现，独立管理一帧等待、提交、取消和释放。
- 新建 `ProcessCameraConfigurationValidator.cs`：定义校验接口、错误模型和默认实现，判断硬触发图像源是否存在上游。
- 修改 `Node/1-Acquisition/ImageSource/NodeImageSource.cs`：删除回调重入流程工作线程，改为在当前 `Run` 中等待回调图像。
- 修改 `Node/1-Acquisition/ImageSource/ParamFormImageSource.cs`：始终绑定相机回调、固定 `TriggerModel.On`、迁移旧 `Off` 参数。
- 修改 `Node/1-Acquisition/ImageSource/ParamFormImageSource.Designer.cs`：删除“触发模式”标签和 `Off/On` 下拉框并回收布局行。
- 修改 `Forms/ProcessNew/ProcessEditPanel.cs` 与 `.Designer.cs`：删除相机回调菜单，运行前校验，统一单次/循环相机取流会话。
- 修改 `Solution.cs`：提供统一校验入口；单次和循环运行都启动、跟踪并停止相机取流；删除循环额外软触发。
- 修改 `FormMain.cs`：主窗体单次、循环运行前展示汇总校验错误。
- 修改 `TDJS-Vision.csproj`：登记两个新增 C# 文件。
- 新建 `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`：对关键结构、调用顺序、界面和运行入口执行静态回归检查。
- 修改 `FLOW_CANVAS_B_PLAN_TASKS.md`：记录测试驱动阶段、实现和验证结果。

---

### 任务 1：建立统一回调取图失败测试

**Files:**
- Create: `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`

**Interfaces:**
- Consumes: 当前源码文本。
- Produces: 后续任务必须满足的静态架构契约。

- [ ] **步骤 1：创建失败测试脚本**

```powershell
$ErrorActionPreference = 'Stop'

function Get-ProjectSource([string]$RelativePath) {
    return Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\$RelativePath") -Raw -Encoding UTF8
}

function Assert-Contains([string]$Text, [string]$Pattern, [string]$Message) {
    if (-not $Text.Contains($Pattern)) { throw $Message }
}

function Assert-NotContains([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text.Contains($Pattern)) { throw $Message }
}

function Assert-Order([string]$Text, [string]$First, [string]$Second, [string]$Message) {
    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) { throw $Message }
}

$nodeSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'
$awaiterSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\CameraFrameAwaiter.cs'
$paramSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\ParamFormImageSource.cs'
$designerSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\ParamFormImageSource.Designer.cs'
$validatorSource = Get-ProjectSource 'ProcessCameraConfigurationValidator.cs'
$processPanelSource = Get-ProjectSource 'Forms\ProcessNew\ProcessEditPanel.cs'
$processDesignerSource = Get-ProjectSource 'Forms\ProcessNew\ProcessEditPanel.Designer.cs'
$solutionSource = Get-ProjectSource 'Solution.cs'
$mainSource = Get-ProjectSource 'FormMain.cs'

Assert-Contains $awaiterSource 'public interface ICameraFrameAwaiter' '必须提供可替换的相机帧等待接口。'
Assert-Contains $awaiterSource 'Task<Mat> BeginWaitAsync(CancellationToken cancellationToken)' '等待接口必须支持取消令牌。'
Assert-Contains $awaiterSource 'bool TrySupplyFrame(Mat frame)' '回调必须通过非阻塞方法提交帧。'
Assert-Contains $nodeSource '_cameraFrameAwaiter.BeginWaitAsync(token)' '图像源必须在当前运行中等待回调。'
Assert-Contains $nodeSource 'param.Camera.GrabOne();' '软触发必须调用一次 GrabOne。'
Assert-Order $nodeSource '_cameraFrameAwaiter.BeginWaitAsync(token)' 'param.Camera.GrabOne();' '必须先登记等待再执行软触发。'
Assert-NotContains $nodeSource 'GetOneFrameImage()' '相机图像源不得再同步取图。'
Assert-NotContains $nodeSource 'Process.Run(this, false)' '回调不得重新启动流程。'
Assert-Contains $paramSource 'param.TriggerModel = TriggerModel.On;' '旧 Off 参数必须迁移为 On。'
Assert-NotContains $designerSource '"Off"' '参数界面不得显示 Off。'
Assert-NotContains $designerSource 'CamerModelComboBox' '参数界面必须删除触发模式控件。'
Assert-Contains $validatorSource 'public interface IProcessCameraConfigurationValidator' '必须提供可插拔校验接口。'
Assert-Contains $validatorSource '硬触发图像源不能存在上游节点' '校验错误必须使用明确中文提示。'
Assert-NotContains $processDesignerSource '是否为相机回调触发流程ToolStripMenuItem' '流程菜单必须移除旧回调开关。'
Assert-Contains $processPanelSource 'TryValidateCameraConfiguration' '流程编辑页必须在运行前校验。'
Assert-Contains $mainSource 'TryValidateCameraConfiguration' '主窗体必须在运行前校验。'
Assert-NotContains $solutionSource 'TriggerSoftCameraCallbacksForProcesses(groupCopy)' '方案循环不得重复发送软触发。'

Write-Host '相机图像源统一回调取图回归检查通过。'
```

- [ ] **步骤 2：运行测试并确认失败**

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1`

Expected: FAIL，首个失败为不存在 `CameraFrameAwaiter.cs` 或缺少 `ICameraFrameAwaiter`。

- [ ] **步骤 3：提交失败测试**

```powershell
git add -- Tests/CameraImageSourceUnifiedCallback.Tests.ps1 FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "test: 定义相机图像源统一回调取图规则"
```

---

### 任务 2：实现可取消的单帧回调等待器

**Files:**
- Create: `Node/1-Acquisition/ImageSource/CameraFrameAwaiter.cs`
- Modify: `TDJS-Vision.csproj:1198`
- Test: `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`

**Interfaces:**
- Consumes: `OpenCvSharp.Mat`、`CancellationToken`。
- Produces: `ICameraFrameAwaiter.BeginWaitAsync(CancellationToken)`、`TrySupplyFrame(Mat)`、`CancelPendingWait()`、`Dispose()`。

- [ ] **步骤 1：在等待器文件中写入接口和最小线程安全实现**

```csharp
public interface ICameraFrameAwaiter : IDisposable
{
    Task<Mat> BeginWaitAsync(CancellationToken cancellationToken);
    bool TrySupplyFrame(Mat frame);
    void CancelPendingWait();
}

public sealed class CameraFrameAwaiter : ICameraFrameAwaiter
{
    private readonly object _syncRoot = new object();
    private TaskCompletionSource<Mat> _pendingSource;
    private CancellationTokenRegistration _cancellationRegistration;
    private bool _disposed;

    public Task<Mat> BeginWaitAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (_pendingSource != null)
                throw new InvalidOperationException("当前图像源已经在等待相机回调帧。");

            TaskCompletionSource<Mat> source =
                new TaskCompletionSource<Mat>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingSource = source;
            CancellationTokenRegistration registration = cancellationToken.Register(CancelPendingWait);
            if (_pendingSource == null)
                registration.Dispose();
            else
                _cancellationRegistration = registration;
            return source.Task;
        }
    }

    public bool TrySupplyFrame(Mat frame)
    {
        TaskCompletionSource<Mat> source;
        CancellationTokenRegistration registration;
        lock (_syncRoot)
        {
            if (_disposed || _pendingSource == null)
                return false;
            source = _pendingSource;
            _pendingSource = null;
            registration = _cancellationRegistration;
            _cancellationRegistration = default(CancellationTokenRegistration);
        }
        registration.Dispose();
        return source.TrySetResult(frame);
    }

    public void CancelPendingWait()
    {
        TaskCompletionSource<Mat> source;
        CancellationTokenRegistration registration;
        lock (_syncRoot)
        {
            source = _pendingSource;
            _pendingSource = null;
            registration = _cancellationRegistration;
            _cancellationRegistration = default(CancellationTokenRegistration);
        }
        source?.TrySetCanceled();
        registration.Dispose();
    }

    public void Dispose()
    {
        lock (_syncRoot) { _disposed = true; }
        CancelPendingWait();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CameraFrameAwaiter));
    }
}
```

- [ ] **步骤 2：将新文件加入工程**

```xml
<Compile Include="Node\1-Acquisition\ImageSource\CameraFrameAwaiter.cs" />
```

- [ ] **步骤 3：运行专项测试，确认只剩后续集成断言失败**

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1`

Expected: FAIL 于 `NodeImageSource` 尚未使用 `_cameraFrameAwaiter`，不再因文件不存在失败。

- [ ] **步骤 4：提交等待器**

```powershell
git add -- Node/1-Acquisition/ImageSource/CameraFrameAwaiter.cs TDJS-Vision.csproj
git commit -m "feat: 添加相机单帧回调等待器"
```

---

### 任务 3：让 NodeImageSource 在同一轮流程等待回调

**Files:**
- Modify: `Node/1-Acquisition/ImageSource/NodeImageSource.cs:15-760`
- Test: `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`
- Test: `Tests/DiagnosticLazyEvaluation.Tests.ps1`

**Interfaces:**
- Consumes: `ICameraFrameAwaiter`、`ICamera.OnMatReceived`、`ICamera.GrabOne()`。
- Produces: `NodeImageSource` 相机路径的统一异步回调结果。

- [ ] **步骤 1：用等待器替换旧回调工作线程字段**

```csharp
/// <summary>
/// 当前图像源的一次性相机回调等待器。
/// </summary>
private readonly ICameraFrameAwaiter _cameraFrameAwaiter;

public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType)
    : this(nodeId, nodeName, process, nodeType, new CameraFrameAwaiter())
{
}

internal NodeImageSource(
    int nodeId,
    string nodeName,
    Process process,
    NodeType nodeType,
    ICameraFrameAwaiter cameraFrameAwaiter) : base(nodeId, nodeName, process, nodeType)
{
    _cameraFrameAwaiter = cameraFrameAwaiter ?? throw new ArgumentNullException(nameof(cameraFrameAwaiter));
    ParamForm = new ParamFormImageSource(this);
    ParamForm.SetNodeBelong(this);
    Result = new NodeResultImageSource();
    NodeBase.NodeDeletedEvent += NodeImageSource_NodeDeletedEvent;
    Disposed += NodeImageSource_Disposed;
}
```

- [ ] **步骤 2：将回调处理缩减为帧交付**

```csharp
public void HandleCameraCallbackFrame(Mat mat)
{
    if (mat == null || mat.Empty())
    {
        mat?.Dispose();
        return;
    }

    if (!_cameraFrameAwaiter.TrySupplyFrame(mat))
    {
        mat.Dispose();
        PerformanceSpikeDiagnostics.LogIfEnabled(
            MsgLevel.Debug,
            () => $"流程【{Process?.ProcessName}】图像源节点({ID}.{NodeName})没有有效等待者，已丢弃迟到回调帧。",
            true);
    }
}
```

- [ ] **步骤 3：新增当前运行内的相机回调取图方法**

```csharp
private async Task<OutputImage> AcquireCameraImageAsync(NodeParamImageSoucre param, CancellationToken token)
{
    if (param.Camera == null)
        throw new Exception("相机对象无效！");
    if (!param.Camera.IsOpen)
        throw new Exception("相机尚未连接！");

    Task<Mat> frameTask = _cameraFrameAwaiter.BeginWaitAsync(token);
    try
    {
        if (param.TriggerSource == TriggerSource.SOFT)
            param.Camera.GrabOne();

        Mat callbackMat = await frameTask.ConfigureAwait(false);
        return BuildOutputImage(callbackMat);
    }
    catch
    {
        _cameraFrameAwaiter.CancelPendingWait();
        throw;
    }
}
```

- [ ] **步骤 4：替换相机分支并保留每次参数应用**

```csharp
else if (param.ImageSource == "相机")
{
    if (param.IsEveryTime)
    {
        param.Camera.SetTriggerDelay(param.TriggerDelay);
        param.Camera.SetExposureTime(param.ExposureTime);
        param.Camera.SetGain(param.Gain);
        param.Camera.GetImageTimeOut = param.TimeOut;
        param.Camera.SetTriggerSource(param.TriggerSource);
        param.Camera.SetTriggerEdge(param.TriggerEdge);
        param.Camera.SetTriggerMode(TriggerModel.On);
    }
    res.OutputImage = await AcquireCameraImageAsync(param, token).ConfigureAwait(false);
}
```

- [ ] **步骤 5：删除旧回调重入流程代码并在释放时取消等待**

删除 `_callbackWorkerThread`、`CallbackWorkerLoop()`、`RunPendingCallbackFrame()`、`Process.Run(this, false)`、旧 `_pendingCallbackMat` 与回调运行状态方法；在节点删除和释放路径调用：

```csharp
_cameraFrameAwaiter.CancelPendingWait();
_cameraFrameAwaiter.Dispose();
```

- [ ] **步骤 6：运行专项与诊断测试**

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1`

Expected: FAIL 于参数界面或校验器尚未改造；`NodeImageSource` 相关断言通过。

Run: `powershell -ExecutionPolicy Bypass -File Tests\DiagnosticLazyEvaluation.Tests.ps1`

Expected: PASS。

- [ ] **步骤 7：提交节点改造**

```powershell
git add -- Node/1-Acquisition/ImageSource/NodeImageSource.cs Tests/CameraImageSourceUnifiedCallback.Tests.ps1
git commit -m "feat: 图像源在当前流程等待相机回调"
```

---

### 任务 4：固定触发模式并统一回调绑定

**Files:**
- Modify: `Node/1-Acquisition/ImageSource/ParamFormImageSource.cs:90-220,520-700`
- Modify: `Node/1-Acquisition/ImageSource/ParamFormImageSource.Designer.cs:220-490,600-625`
- Test: `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`

**Interfaces:**
- Consumes: `NodeParamImageSoucre.TriggerModel`、`TriggerSource`。
- Produces: 始终为 `On` 的相机参数和不依赖流程菜单的回调订阅。

- [ ] **步骤 1：先增加旧 Off 迁移和固定 On 的失败断言**

在专项测试中保留以下断言，并确认当前代码失败：

```powershell
Assert-Contains $paramSource 'if (param.TriggerModel == TriggerModel.Off)' '必须识别旧 Off 参数。'
Assert-Contains $paramSource 'param.TriggerModel = TriggerModel.On;' '旧参数必须迁移为 On。'
Assert-NotContains $paramSource 'IsCameraCallbackMenuEnabled' '回调绑定不得依赖流程菜单。'
```

- [ ] **步骤 2：加载相机参数时迁移旧值**

```csharp
if (param.TriggerModel == TriggerModel.Off)
{
    param.TriggerModel = TriggerModel.On;
    LogHelper.AddLog(
        MsgLevel.Info,
        $"流程【{_imageSourceNode?.Process?.ProcessName}】图像源节点({_imageSourceNode?.ID}.{_imageSourceNode?.NodeName})的旧触发模式 Off 已迁移为 On。",
        true);
}
```

- [ ] **步骤 3：保存和设置相机时始终使用 On**

```csharp
_nodeParamImageSource.TriggerModel = TriggerModel.On;
SetCameraParams(
    _nodeParamImageSource.Camera,
    TriggerModel.On,
    _nodeParamImageSource.TriggerSource,
    _nodeParamImageSource.TriggerEdge,
    _nodeParamImageSource.TriggerDelay,
    _nodeParamImageSource.ExposureTime,
    _nodeParamImageSource.Gain,
    _nodeParamImageSource.TimeOut);
```

- [ ] **步骤 4：让回调绑定只取决于有效相机参数**

```csharp
public void SyncCameraCallbackBinding()
{
    ICamera camera = GetCallbackCameraFromParams();
    if (camera == null)
    {
        UnbindCameraCallback();
        return;
    }
    BindCameraCallback(camera);
}
```

- [ ] **步骤 5：在 Designer 中删除触发模式行**

删除 `label2` 和 `CamerModelComboBox` 的实例化、`Controls.Add`、属性初始化、事件绑定及字段声明；将原第 2～8 行控件上移一行，将 `RowCount` 从 9 调整为 8，保证“触发方式”紧接“选择相机”。

- [ ] **步骤 6：运行专项测试**

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1`

Expected: FAIL 于 `ProcessCameraConfigurationValidator.cs` 尚不存在；参数与 Designer 断言通过。

- [ ] **步骤 7：提交参数界面改造**

```powershell
git add -- Node/1-Acquisition/ImageSource/ParamFormImageSource.cs Node/1-Acquisition/ImageSource/ParamFormImageSource.Designer.cs
git commit -m "feat: 固定相机图像源触发模式"
```

---

### 任务 5：实现硬触发上游配置校验

**Files:**
- Create: `ProcessCameraConfigurationValidator.cs`
- Modify: `Solution.cs:35-180,350-760`
- Modify: `Forms/ProcessNew/ProcessEditPanel.cs:980-1080`
- Modify: `FormMain.cs:920-960`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`

**Interfaces:**
- Consumes: `Process.GetUpstreamNodes(NodeBase)`、`Process.HasCanvasGraph`、图像源参数。
- Produces: `IProcessCameraConfigurationValidator.Validate(IEnumerable<Process>)` 与 `Solution.TryValidateCameraConfiguration(...)`。

- [ ] **步骤 1：新增校验类型**

```csharp
public sealed class ProcessCameraConfigurationError
{
    public ProcessCameraConfigurationError(string processName, int nodeId, string nodeName, TriggerSource triggerSource)
    {
        ProcessName = processName;
        NodeId = nodeId;
        NodeName = nodeName;
        TriggerSource = triggerSource;
    }

    public string ProcessName { get; }
    public int NodeId { get; }
    public string NodeName { get; }
    public TriggerSource TriggerSource { get; }

    public string ToDisplayText()
    {
        return $"流程【{ProcessName}】的图像源节点({NodeId}.{NodeName})使用硬触发源 {TriggerSource}，但该节点存在上游节点。硬触发图像源不能存在上游节点。";
    }
}

public interface IProcessCameraConfigurationValidator
{
    IReadOnlyList<ProcessCameraConfigurationError> Validate(IEnumerable<Process> processes);
}
```

- [ ] **步骤 2：实现图流程与旧顺序流程的上游判断**

```csharp
private static bool HasActiveUpstreamNode(Process process, NodeImageSource imageSource)
{
    if (process.HasCanvasGraph)
        return process.GetUpstreamNodes(imageSource).Any(node => node != null && node.Active);

    int imageIndex = process.Nodes.IndexOf(imageSource);
    return imageIndex > 0 && process.Nodes.Take(imageIndex).Any(node => node != null && node.Active);
}
```

默认实现只对 `ImageSource == "相机"`、节点有效且 `TriggerSource` 为 `LINE0`～`LINE4` 的节点生成错误。

- [ ] **步骤 3：在 Solution 中提供可替换校验器和汇总方法**

```csharp
public IProcessCameraConfigurationValidator CameraConfigurationValidator { get; set; }
    = new ProcessCameraConfigurationValidator();

public bool TryValidateCameraConfiguration(IEnumerable<Process> processes, out string message)
{
    IReadOnlyList<ProcessCameraConfigurationError> errors = CameraConfigurationValidator.Validate(processes);
    message = errors.Count == 0
        ? string.Empty
        : "以下流程的相机触发配置有误：\r\n\r\n" + string.Join("\r\n", errors.Select(error => error.ToDisplayText()));
    return errors.Count == 0;
}
```

- [ ] **步骤 4：四个界面入口在改变运行状态前校验**

流程编辑页对单个 `_process` 调用校验；主窗体对 `Solution.Instance.AllProcesses` 调用校验。失败时统一执行：

```csharp
if (!Solution.Instance.TryValidateCameraConfiguration(processes, out string validationMessage))
{
    MessageBoxTD.Show(validationMessage, "相机触发配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    return;
}
```

- [ ] **步骤 5：Solution.Run 增加无界面安全门**

```csharp
if (!TryValidateCameraConfiguration(AllProcesses, out string validationMessage))
{
    LogHelper.AddLog(MsgLevel.Exception, validationMessage, true);
    return;
}
```

- [ ] **步骤 6：登记新文件并运行专项测试**

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1`

Expected: FAIL 只剩旧菜单或 Solution 循环软触发断言；校验器和四入口断言通过。

- [ ] **步骤 7：提交校验功能**

```powershell
git add -- ProcessCameraConfigurationValidator.cs Solution.cs Forms/ProcessNew/ProcessEditPanel.cs FormMain.cs TDJS-Vision.csproj
git commit -m "feat: 拦截硬触发图像源上游配置"
```

---

### 任务 6：统一相机取流会话并删除旧菜单控制

**Files:**
- Modify: `Solution.cs:355-720`
- Modify: `Forms/ProcessNew/ProcessEditPanel.cs:130-300,980-1260`
- Modify: `Forms/ProcessNew/ProcessEditPanel.Designer.cs:40-340,530-555`
- Test: `Tests/CameraImageSourceUnifiedCallback.Tests.ps1`
- Test: `Tests/CameraCallbackFastPath.Tests.ps1`

**Interfaces:**
- Consumes: `StartCameraCallbackGrabbingForProcesses`、`StopStartedCameraCallbackGrabbing`。
- Produces: 单次/循环一致的相机回调取流生命周期。

- [ ] **步骤 1：让相机取流发现逻辑不再依赖旧菜单属性**

在 `StartCameraCallbackGrabbingForProcesses` 中移除：

```csharp
!process.IsCameraCallbackTriggered ||
```

保留节点有效、图像源为相机、相机已打开、去重启动和参数应用判断。

- [ ] **步骤 2：Solution 单次和循环运行都包裹取流会话**

```csharp
List<ICamera> startedCallbackCameras = StartCameraCallbackGrabbingForProcesses(AllProcesses);
try
{
    // 保留现有分组、优先级、被动流程和循环调度。
}
finally
{
    StopStartedCameraCallbackGrabbing(startedCallbackCameras);
}
```

从方案循环中删除 `TriggerSoftCameraCallbacksForProcesses(groupCopy)`；软触发只允许由 `NodeImageSource.Run()` 调用 `GrabOne()`。

- [ ] **步骤 3：流程编辑页单次和循环运行复用同一会话**

单次运行在调用 `RunCurrentProcessInBackgroundAsync(false)` 前启动当前流程相机并在 `finally` 停止；循环运行在进入循环前启动一次，在停止或异常后统一停止。删除 `_process.IsCameraCallbackTriggered` 专用分支和 `TriggerSoftCameraCallbacksForProcess` 调用。

- [ ] **步骤 4：删除流程菜单项及代码事件**

在 `.Designer.cs` 中删除 `是否为相机回调触发流程ToolStripMenuItem` 的创建、菜单集合项、属性、点击事件和字段；在 `ProcessEditPanel.cs` 中删除语言绑定、选中状态、点击处理和依赖菜单的同步方法。保留加载旧 `ProcessConfig.IsCameraCallbackTriggered` 的赋值以兼容反序列化。

- [ ] **步骤 5：运行专项及既有快速路径测试**

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1`

Expected: PASS，输出“相机图像源统一回调取图回归检查通过。”

Run: `powershell -ExecutionPolicy Bypass -File Tests\CameraCallbackFastPath.Tests.ps1`

Expected: PASS，现有兼容运行入口未被破坏。

- [ ] **步骤 6：提交运行会话和菜单清理**

```powershell
git add -- Solution.cs Forms/ProcessNew/ProcessEditPanel.cs Forms/ProcessNew/ProcessEditPanel.Designer.cs
git commit -m "refactor: 统一相机回调取流会话"
```

---

### 任务 7：完成回归验证与任务记录

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`
- Verify: 全部本次相关源文件。

**Interfaces:**
- Consumes: 前六项全部交付。
- Produces: 可编译、可追溯的最终结果。

- [ ] **步骤 1：运行专项与相关回归检查**

```powershell
powershell -ExecutionPolicy Bypass -File Tests\CameraImageSourceUnifiedCallback.Tests.ps1
powershell -ExecutionPolicy Bypass -File Tests\CameraCallbackFastPath.Tests.ps1
powershell -ExecutionPolicy Bypass -File Tests\DiagnosticLazyEvaluation.Tests.ps1
powershell -ExecutionPolicy Bypass -File Tests\DiagnosticLogLevel.Tests.ps1
powershell -ExecutionPolicy Bypass -File Tests\SolutionResourceRelease.Tests.ps1
```

Expected: 五个脚本全部退出码 0。

- [ ] **步骤 2：执行 Debug|x64 编译**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: `0 个错误`；既有警告数量如有变化须在任务文件中如实记录。

- [ ] **步骤 3：执行文本和差异检查**

```powershell
rg -n "GetOneFrameImage\(\)|Process.Run\(this, false\)|TriggerSoftCameraCallbacksForProcesses\(groupCopy\)|是否为相机回调触发流程ToolStripMenuItem|CamerModelComboBox" Node/1-Acquisition/ImageSource Forms/ProcessNew Solution.cs
git diff --check
git status --short
```

Expected: 第一条命令不在目标运行路径发现旧调用或旧控件；`git diff --check` 退出码 0；状态只包含本任务文件。

- [ ] **步骤 4：更新任务记录**

在 `FLOW_CANVAS_B_PLAN_TASKS.md` 将本任务实施和验证项标记为完成，并记录：专项测试、相关回归测试、MSBuild 错误/警告数量及未处理的既有失败。

- [ ] **步骤 5：提交最终记录**

```powershell
git add -- FLOW_CANVAS_B_PLAN_TASKS.md Tests/CameraImageSourceUnifiedCallback.Tests.ps1
git commit -m "test: 验证相机图像源统一回调取图"
```
