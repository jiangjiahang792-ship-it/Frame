# 日志界面限频批量刷新实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将日志界面从逐条 UI 调度改为有界缓冲和 100ms 限量批量刷新，避免高频日志占满 WinForms UI 线程，同时保持日志文件完整写入。

**Architecture:** 后台日志线程仍负责完整写文件，需要显示的日志只进入实例级 `ILogUiBuffer`。`LogHelper.Designer.cs` 中的 WinForms 定时器在 UI 线程每 100ms 最多消费 100 条，统一批量更新列表并限制历史行数；窗口隐藏时不消费缓冲区。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、`ConcurrentQueue<T>`、PowerShell 回归测试、MSBuild。

## Global Constraints

- 所有中文内容使用简体中文。
- WinForms 定时器必须在 `LogHelper.Designer.cs` 中创建和配置。
- 新增接口、类、字段、属性和方法必须有 XML 中文注释。
- 日志文件写入格式、目录、等级过滤和保留规则不变。
- UI 缓冲容量固定为 2000 条，每次最多刷新 100 条，刷新间隔固定为 100ms。
- “全部”列表最多保留 1000 条，各等级列表最多保留 300 条。
- 每次代码修改同步记录到 `FLOW_CANVAS_B_PLAN_TASKS.md`。

---

### Task 1: 实现有界日志界面缓冲区

**Files:**
- Create: `Forms/Logger/LogUiBuffer.cs`
- Modify: `TDJS-Vision.csproj`
- Create: `Tests/LogUiThrottledRefresh.Tests.ps1`

**Interfaces:**
- Consumes: `Logger.MsgLevel`。
- Produces: `LogUiEntry`、`ILogUiBuffer.Count`、`ILogUiBuffer.Enqueue(LogUiEntry)`、`ILogUiBuffer.DequeueBatch(int)`、`ILogUiBuffer.Clear()`、`BoundedLogUiBuffer(int)`。

- [ ] **Step 1: 编写缓冲区失败测试**

在 `Tests/LogUiThrottledRefresh.Tests.ps1` 中动态编译 `Forms/Logger/LogUiBuffer.cs`，使用测试用 `MsgLevel` 枚举执行以下断言：

```powershell
$buffer = [Logger.BoundedLogUiBuffer]::new(3)
1..5 | ForEach-Object {
    $buffer.Enqueue([Logger.LogUiEntry]::new([Logger.MsgLevel]::Info, "日志$_"))
}

Assert-Equal 3 $buffer.Count '缓冲区必须保持容量上限。'
$batch = $buffer.DequeueBatch(2)
Assert-Equal '日志3' $batch[0].Info '超限时必须淘汰最旧日志。'
Assert-Equal '日志4' $batch[1].Info '批量取出必须保持顺序。'
Assert-Equal 1 $buffer.Count '每次只能取出指定批量。'
```

再使用 `System.Threading.Tasks.Parallel.For` 并发写入 10000 条，断言无异常且 `Count -le 2000`。

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LogUiThrottledRefresh.Tests.ps1`

Expected: FAIL，提示 `Forms\Logger\LogUiBuffer.cs` 不存在或 `BoundedLogUiBuffer` 类型不存在。

- [ ] **Step 3: 实现最小有界缓冲区**

在 `Forms/Logger/LogUiBuffer.cs` 中实现：

```csharp
internal sealed class LogUiEntry
{
    public LogUiEntry(MsgLevel level, string info)
    {
        Level = level;
        Info = info ?? string.Empty;
    }

    public MsgLevel Level { get; }
    public string Info { get; }
}

internal interface ILogUiBuffer
{
    int Count { get; }
    void Enqueue(LogUiEntry entry);
    IReadOnlyList<LogUiEntry> DequeueBatch(int maxCount);
    void Clear();
}
```

`BoundedLogUiBuffer` 使用 `ConcurrentQueue<LogUiEntry>`、`Interlocked` 计数和构造函数容量校验。`Enqueue` 后当计数超过容量时持续尝试从队首淘汰；`DequeueBatch` 校验 `maxCount > 0` 并最多取出指定数量；`Clear` 排空队列并同步修正计数。所有类型和成员补充中文 XML 注释。

- [ ] **Step 4: 注册源码并运行测试确认通过**

在 `TDJS-Vision.csproj` 的 Logger 编译项中加入：

```xml
<Compile Include="Forms\Logger\LogUiBuffer.cs" />
```

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LogUiThrottledRefresh.Tests.ps1`

Expected: PASS，容量、顺序、批量、清空和并发断言全部通过。

- [ ] **Step 5: 提交缓冲区实现**

```powershell
git add -- Forms/Logger/LogUiBuffer.cs TDJS-Vision.csproj Tests/LogUiThrottledRefresh.Tests.ps1
git commit -m "feat: 添加有界日志界面缓冲区"
```

---

### Task 2: 将日志显示改为设计器定时批量刷新

**Files:**
- Modify: `Forms/Logger/LogHelper.cs`
- Modify: `Forms/Logger/LogHelper.Designer.cs`
- Modify: `Tests/LogUiThrottledRefresh.Tests.ps1`

**Interfaces:**
- Consumes: Task 1 的 `ILogUiBuffer` 和 `BoundedLogUiBuffer`。
- Produces: `LogHelper_LogRefreshTimerTick(object, EventArgs)`、`FlushPendingLogs()`、批量列表更新和有界历史裁剪。

- [ ] **Step 1: 添加 UI 调度失败契约**

在专项测试中读取 `LogHelper.cs` 和 `LogHelper.Designer.cs`，断言：

```powershell
Assert-Contains $helperSource 'new BoundedLogUiBuffer(2000)' '界面缓冲容量必须为 2000。'
Assert-Contains $helperSource '_logUiBuffer.Enqueue' '日志事件必须只进入界面缓冲区。'
Assert-NotContains $helperSource 'TDJS_Vision.Solution.Instance.IsRunning' '界面调度不能依赖方案运行状态。'
Assert-NotContains $helperSource 'this.BeginInvoke' '高频日志不能逐条投递 UI 委托。'
Assert-Contains $helperSource 'const int UiRefreshBatchSize = 100' '单次刷新上限必须为 100。'
Assert-Contains $designerSource 'this.logRefreshTimer.Interval = 100;' '设计器定时器间隔必须为 100ms。'
Assert-Contains $designerSource 'this.logRefreshTimer.Tick +=' '设计器必须绑定刷新事件。'
```

- [ ] **Step 2: 运行专项测试确认失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LogUiThrottledRefresh.Tests.ps1`

Expected: FAIL，指出仍存在逐条 `BeginInvoke` 或缺少设计器定时器。

- [ ] **Step 3: 在设计器中添加刷新定时器**

修改 `LogHelper.Designer.cs`：

```csharp
this.logRefreshTimer = new System.Windows.Forms.Timer(this.components);
this.logRefreshTimer.Enabled = true;
this.logRefreshTimer.Interval = 100;
this.logRefreshTimer.Tick += new System.EventHandler(this.LogHelper_LogRefreshTimerTick);
```

并在设计器字段区加入带设计器生命周期的字段：

```csharp
private System.Windows.Forms.Timer logRefreshTimer;
```

- [ ] **Step 4: 将日志事件改为仅入队**

在 `LogHelper.cs` 中加入实例字段和常量：

```csharp
private const int UiRefreshBatchSize = 100;
private const int AllLogDisplayLimit = 1000;
private const int LevelLogDisplayLimit = 300;
private readonly ILogUiBuffer _logUiBuffer = new BoundedLogUiBuffer(2000);
```

将 `LogHelper_LogAddEvent` 简化为：

```csharp
private void LogHelper_LogAddEvent(object sender, LevelAndInfo levelAndInfo)
{
    _logUiBuffer.Enqueue(new LogUiEntry(levelAndInfo.Level, levelAndInfo.Info));
}
```

删除静态 `_pendingLogs`、基于 `Solution.IsRunning` 的分支和逐条 `BeginInvoke`。

- [ ] **Step 5: 实现单批 UI 刷新**

`LogHelper_LogRefreshTimerTick` 先检查 `IsDisposed`、`Disposing`、`IsHandleCreated` 和 `Visible`，再调用 `FlushPendingLogs()`。`FlushPendingLogs()` 必须只在 UI 线程调用；非 UI 调用直接返回，避免重新引入逐条调度。

每批通过 `_logUiBuffer.DequeueBatch(UiRefreshBatchSize)` 取得最多 100 条。六个列表进入 `BeginUpdate`，逐条加入“全部”和对应等级列表，随后分别裁剪到固定上限；在 `finally` 中对六个列表执行 `EndUpdate`。批次完成后只对当前标签页对应列表设置一次 `TopIndex`。

- [ ] **Step 6: 运行专项测试和现有日志回归**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LogUiThrottledRefresh.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LogLevelSettings.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\DiagnosticLogLevel.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\DiagnosticLazyEvaluation.Tests.ps1
```

Expected: 全部 PASS。

- [ ] **Step 7: 提交定时批量刷新**

```powershell
git add -- Forms/Logger/LogHelper.cs Forms/Logger/LogHelper.Designer.cs Tests/LogUiThrottledRefresh.Tests.ps1
git commit -m "fix: 限频批量刷新日志界面"
```

---

### Task 3: 完善清空、释放与最终验证

**Files:**
- Modify: `Forms/Logger/LogHelper.cs`
- Modify: `Forms/Logger/FrmLogger.cs`
- Modify: `Tests/LogUiThrottledRefresh.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: Task 2 的定时批量刷新入口。
- Produces: 日志事件安全解绑、清空显示同步清空缓冲区、主界面停止运行后的有界单批刷新。

- [ ] **Step 1: 添加生命周期失败契约**

专项测试增加以下断言：

```powershell
Assert-Contains $helperSource 'LogAddEvent -= LogHelper_LogAddEvent;' '控件释放时必须解除静态日志事件。'
Assert-Contains $helperSource '_logUiBuffer.Clear();' '清空显示时必须同步清空待显示日志。'
Assert-Contains $helperSource 'finally' '列表批量更新必须保证 EndUpdate 执行。'
Assert-NotContains $helperSource 'Items.Clear();' '达到历史上限时不能整表清空。'
```

- [ ] **Step 2: 运行专项测试确认失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LogUiThrottledRefresh.Tests.ps1`

Expected: FAIL，指出缺少事件解绑或仍存在整表清空。

- [ ] **Step 3: 实现释放和清空行为**

构造函数订阅 `Disposed`，释放时执行：

```csharp
private void LogHelper_Disposed(object sender, EventArgs e)
{
    LogAddEvent -= LogHelper_LogAddEvent;
    Disposed -= LogHelper_Disposed;
    _logUiBuffer.Clear();
}
```

`btnClear_Click` 在清理当前标签页列表前调用 `_logUiBuffer.Clear()`。历史裁剪统一使用删除最旧项的方法，不调用 `Items.Clear()`。

保留 `FrmLogger.FlushLogs()`，但它只能触发 `LogHelper.FlushPendingLogs()` 的单批最多 100 条刷新，确保主界面停止运行时不会一次性排空队列。

- [ ] **Step 4: 更新任务记录**

在 `FLOW_CANVAS_B_PLAN_TASKS.md` 追加 `2026-07-16: 日志界面高频刷新防卡死`，记录根因、有界缓冲、100ms/100 条批处理、隐藏窗口策略、列表上限、事件解绑、测试和编译结果。

- [ ] **Step 5: 执行完整相关验证**

Run:

```powershell
$tests = @(
  'Tests\LogUiThrottledRefresh.Tests.ps1',
  'Tests\LogLevelSettings.Tests.ps1',
  'Tests\DiagnosticLogLevel.Tests.ps1',
  'Tests\DiagnosticLazyEvaluation.Tests.ps1',
  'Tests\SolutionResourceRelease.Tests.ps1'
)
foreach ($test in $tests) {
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test
  if ($LASTEXITCODE -ne 0) { throw "测试失败: $test" }
}
git diff --check
```

Expected: 所有测试 PASS，`git diff --check` 退出码为 0。

- [ ] **Step 6: 执行 Debug|x64 编译**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" `
  /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: 0 个错误；既有警告数量单独记录，不将既有警告误报为本次失败。

- [ ] **Step 7: 提交生命周期和任务记录**

```powershell
git add -- Forms/Logger/LogHelper.cs Forms/Logger/FrmLogger.cs Tests/LogUiThrottledRefresh.Tests.ps1 FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "test: 完成日志界面防卡死回归"
```
