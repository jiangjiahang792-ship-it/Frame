# 卡尺找圆多目标并发实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让卡尺找圆的多个位置修正目标共用一张只读灰度图并发执行算法，单目标保持直接路径，最终按原目标顺序统一输出。

**Architecture:** 在现有 `MultiTargetMeasurementRunner` 中增加同步等待的目标级受限并发入口，以固定下标数组保证顺序，以既有失败工厂隔离单目标异常。卡尺找圆只获取一次灰度图，然后通过并发入口执行每个目标的参数仿射和 `FindCircle`；节点总耗时改用高精度 `Stopwatch`，其他测量工具继续使用原串行入口。

**Tech Stack:** C#、.NET Framework 4.8、TPL `Parallel.For`、OpenCvSharp、PowerShell 行为测试、MSBuild Debug|x64。

## Global Constraints

- 多个目标必须共享同一个只读 `Mat`，不得按目标复制、转换或释放灰度图。
- 目标数量为 1 时必须走直接串行路径，不创建并发循环。
- 默认最大并发度为 `min(目标数量, max(1, Environment.ProcessorCount - 1))`。
- 结果数量和顺序必须与位置修正集合一致，不能按任务完成顺序排列。
- 普通单目标异常生成同位置全零 NG 项；取消异常必须向上层传播。
- 本次只切换卡尺找圆，卡尺找线、卡尺找椭圆及其他测量工具继续串行。
- 不修改算法数学逻辑、ROI 交互、方案参数或 WinForms Designer 文件。
- 所有新增类、方法和重要并发思路使用简体中文 XML 注释。

---

## 文件结构

- `Node/4-Measurement/Common/MultiTargetMeasurementRunner.cs`：新增可复用的目标级受限并发入口，并复用单目标结果填充逻辑。
- `Tests/MultiTargetMeasurementRunner.Tests.ps1`：验证真实执行重叠、并发上限、单目标快路径、顺序、失败隔离和取消。
- `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`：保留单次灰度图获取，改用公共并发入口。
- `Node/4-Measurement/CaliperCircle/NodeCaliperCircle.cs`：使用高精度 `Stopwatch` 统计并发后的墙钟耗时。
- `Tests/MultiTargetCaliper.Tests.ps1`：约束只有卡尺找圆启用并发、灰度图位于并发循环外及高精度计时。
- `FLOW_CANVAS_B_PLAN_TASKS.md`：记录实施、测试、编译和性能验证结果。

### Task 1: 公共多目标受限并发执行器

**Files:**
- Modify: `Tests/MultiTargetMeasurementRunner.Tests.ps1`
- Modify: `Node/4-Measurement/Common/MultiTargetMeasurementRunner.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<PositionCorrectionInfo>`、`CancellationToken`、`Func<PositionCorrectionInfo, TItem>`、`Func<PositionCorrectionInfo, Exception, TItem>`。
- Produces: `MultiTargetMeasurementRunner.RunParallel<TItem>(IReadOnlyList<PositionCorrectionInfo>, CancellationToken, Func<PositionCorrectionInfo, TItem>, Func<PositionCorrectionInfo, Exception, TItem>, int maxDegreeOfParallelism = 0)`。

- [ ] **Step 1: 扩展行为测试，先声明期望的并发接口**

在测试夹具的 `RunnerBehaviorHarness.Verify()` 末尾调用 `VerifyParallel()`，并加入以下方法。测试通过两个工作单元互相等待证明真实执行重叠，通过固定并发度 2 验证上限，通过目标 3 抛错验证失败隔离和顺序：

```csharp
private static void VerifyParallel()
{
    var corrections = new List<PositionCorrectionInfo>();
    for (int i = 1; i <= 5; i++)
        corrections.Add(new PositionCorrectionInfo { TargetIndex = i });

    int activeCount = 0;
    int maximumActiveCount = 0;
    int startedCount = 0;
    var firstWave = new CountdownEvent(2);
    var releaseFirstWave = new ManualResetEventSlim(false);

    List<RunnerTestItem> items = MultiTargetMeasurementRunner.RunParallel(
        corrections,
        CancellationToken.None,
        correction =>
        {
            int active = Interlocked.Increment(ref activeCount);
            UpdateMaximum(ref maximumActiveCount, active);
            int started = Interlocked.Increment(ref startedCount);
            try
            {
                if (started <= 2)
                {
                    if (firstWave.Signal())
                        releaseFirstWave.Set();
                    if (!releaseFirstWave.Wait(2000))
                        throw new TimeoutException("Parallel workers did not overlap.");
                }

                if (correction.TargetIndex == 3)
                    throw new InvalidOperationException("expected parallel failure");

                return new RunnerTestItem
                {
                    IsOk = true,
                    Value = correction.TargetIndex * 10.0
                };
            }
            finally
            {
                Interlocked.Decrement(ref activeCount);
            }
        },
        (correction, exception) => new RunnerTestItem
        {
            IsOk = false,
            Value = 0.0,
            ErrorMessage = exception.Message
        },
        2);

    Assert(maximumActiveCount == 2, "Parallel execution must overlap without exceeding the configured limit.");
    Assert(items.Count == 5, "Parallel output count must equal the correction count.");
    for (int i = 0; i < items.Count; i++)
        Assert(items[i].TargetIndex == i + 1, "Parallel output order must match correction order.");
    Assert(!items[2].IsOk && items[2].Value == 0.0, "The failed parallel target must retain a zero-valued item.");
    Assert(items[4].IsOk && items[4].Value == 50.0, "A later target must complete after another target fails.");

    int callerThreadId = Thread.CurrentThread.ManagedThreadId;
    int singleWorkerThreadId = 0;
    List<RunnerTestItem> single = MultiTargetMeasurementRunner.RunParallel(
        new List<PositionCorrectionInfo> { new PositionCorrectionInfo { TargetIndex = 1 } },
        CancellationToken.None,
        correction =>
        {
            singleWorkerThreadId = Thread.CurrentThread.ManagedThreadId;
            return new RunnerTestItem { IsOk = true, Value = 1.0 };
        },
        (correction, exception) => new RunnerTestItem(),
        4);
    Assert(single.Count == 1 && singleWorkerThreadId == callerThreadId, "A single target must execute directly on the caller thread.");

    var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    bool cancellationThrown = false;
    try
    {
        MultiTargetMeasurementRunner.RunParallel(
            corrections,
            cancellation.Token,
            correction => new RunnerTestItem(),
            (correction, exception) => new RunnerTestItem(),
            2);
    }
    catch (OperationCanceledException)
    {
        cancellationThrown = true;
    }
    Assert(cancellationThrown, "Parallel cancellation must propagate instead of becoming a failure item.");
}

private static void UpdateMaximum(ref int maximum, int candidate)
{
    int snapshot;
    do
    {
        snapshot = maximum;
        if (candidate <= snapshot)
            return;
    }
    while (Interlocked.CompareExchange(ref maximum, candidate, snapshot) != snapshot);
}
```

- [ ] **Step 2: 运行测试并确认按预期失败**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\MultiTargetMeasurementRunner.Tests.ps1"
```

Expected: FAIL，编译错误明确指出 `MultiTargetMeasurementRunner` 不存在 `RunParallel`。

- [ ] **Step 3: 实现最小并发入口并复用结果填充逻辑**

在生产文件增加 `using System.Threading.Tasks;`。将串行循环中的单目标执行、失败转换、`TargetIndex` 和 `Correction` 回填抽取为私有 `ExecuteTarget`，然后增加：

```csharp
/// <summary>
/// 使用受限并发逐目标执行测量，并按输入位置统一返回结果。
/// </summary>
public static List<TItem> RunParallel<TItem>(
    IReadOnlyList<PositionCorrectionInfo> corrections,
    CancellationToken token,
    Func<PositionCorrectionInfo, TItem> execute,
    Func<PositionCorrectionInfo, Exception, TItem> createFailure,
    int maxDegreeOfParallelism = 0)
    where TItem : IMultiTargetMeasurementItem
{
    ValidateArguments(corrections, execute, createFailure);
    token.ThrowIfCancellationRequested();

    if (corrections.Count <= 1)
        return Run(corrections, token, execute, createFailure);

    int automaticDegree = Math.Max(1, Environment.ProcessorCount - 1);
    int requestedDegree = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : automaticDegree;
    int degree = Math.Min(corrections.Count, Math.Max(1, requestedDegree));
    if (degree <= 1)
        return Run(corrections, token, execute, createFailure);

    var items = new TItem[corrections.Count];
    var options = new ParallelOptions
    {
        CancellationToken = token,
        MaxDegreeOfParallelism = degree
    };

    Parallel.For(0, corrections.Count, options, index =>
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        items[index] = ExecuteTarget(corrections[index], index, execute, createFailure);
    });

    return new List<TItem>(items);
}
```

`Run` 继续预分配 `List<TItem>`，但每次迭代改为调用同一个 `ExecuteTarget`。`ValidateArguments` 和 `ExecuteTarget` 都添加完整简体中文 XML 注释，保持原有异常文本和取消语义。

- [ ] **Step 4: 运行公共执行器测试并确认通过**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\MultiTargetMeasurementRunner.Tests.ps1"
```

Expected: PASS，输出 `Multi-target measurement runner behavior checks passed.`。

- [ ] **Step 5: 提交公共执行器**

```powershell
git add -- "Tests/MultiTargetMeasurementRunner.Tests.ps1" "Node/4-Measurement/Common/MultiTargetMeasurementRunner.cs"
git commit -m "feat: 增加多目标受限并发执行器"
```

### Task 2: 卡尺找圆并发接入与高精度计时

**Files:**
- Modify: `Tests/MultiTargetCaliper.Tests.ps1`
- Modify: `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeCaliperCircle.cs`

**Interfaces:**
- Consumes: Task 1 的 `MultiTargetMeasurementRunner.RunParallel<TItem>(..., int maxDegreeOfParallelism = 0)`。
- Produces: 共享单张灰度图的卡尺找圆多目标并发运行路径，以及高精度节点墙钟耗时。

- [ ] **Step 1: 增加卡尺找圆接入失败约束**

在现有三种卡尺循环之后单独读取找圆窗体和节点源码，并加入：

```powershell
$circleFormPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperCircle\NodeParamFormCaliperCircle.cs'
$circleNodePath = Join-Path $projectRoot 'Node\4-Measurement\CaliperCircle\NodeCaliperCircle.cs'
$circleForm = Get-Content -LiteralPath $circleFormPath -Raw -Encoding UTF8
$circleNode = Get-Content -LiteralPath $circleNodePath -Raw -Encoding UTF8

Assert-ContainsText $circleForm 'MultiTargetMeasurementRunner.RunParallel(' 'CaliperCircle must use target-level parallel execution.'
Assert-NotContainsText $circleForm 'Parallel.For' 'CaliperCircle must delegate concurrency to the common runner.'

$executeMarker = 'internal List<CaliperCircleTargetResult> ExecuteMeasures(NodeParamCaliperCircle param, CancellationToken token)'
$executeStart = $circleForm.IndexOf($executeMarker)
$executeEnd = $circleForm.IndexOf('private ', $executeStart + $executeMarker.Length)
$executeBody = $circleForm.Substring($executeStart, $executeEnd - $executeStart)
$grayReadCount = ([regex]::Matches($executeBody, 'GetInputGrayMat\(')).Count
if ($grayReadCount -ne 1) {
    throw 'CaliperCircle must acquire exactly one gray image outside parallel target execution.'
}
Assert-ContainsText $circleNode 'Stopwatch.StartNew()' 'CaliperCircle node runtime must use a high-resolution stopwatch.'
Assert-ContainsText $circleNode 'SetRunResult(stopwatch,' 'CaliperCircle must report wall-clock time from the stopwatch.'
Assert-NotContainsText $circleNode 'DateTime startTime = DateTime.Now;' 'CaliperCircle must not use DateTime for short runtime measurement.'
```

- [ ] **Step 2: 运行卡尺测试并确认按预期失败**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\MultiTargetCaliper.Tests.ps1"
```

Expected: FAIL，首先指出卡尺找圆尚未调用 `RunParallel`。

- [ ] **Step 3: 将卡尺找圆切换到公共并发入口**

保持 `ReadCorrections` 在灰度图获取前运行、空集合直接返回、灰度图只获取一次及原有 `finally` 释放逻辑，仅将执行调用替换为：

```csharp
return MultiTargetMeasurementRunner.RunParallel(
    corrections,
    token,
    correction => ExecuteOne(gray, BuildRuntimeParam(param, correction), correction),
    CreateFailure);
```

不得在委托中获取图像、更新界面、写入节点 `Result` 或释放 `gray`。

- [ ] **Step 4: 将节点总耗时改为高精度墙钟计时**

在 `NodeCaliperCircle.cs` 增加 `using System.Diagnostics;`，把入口改为：

```csharp
Stopwatch stopwatch = Stopwatch.StartNew();
```

成功、停用、取消和失败分支全部使用现有重载：

```csharp
int time = SetRunResult(stopwatch, NodeStatus.Successful);
```

其他状态传入对应原状态，保持日志、异常和结果结构不变。

- [ ] **Step 5: 运行卡尺与公共执行器测试并确认通过**

Run:

```powershell
$tests=@('Tests\MultiTargetMeasurementRunner.Tests.ps1','Tests\MultiTargetCaliper.Tests.ps1'); foreach($test in $tests){ powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test; if($LASTEXITCODE -ne 0){ throw "$test failed" } }
```

Expected: 2/2 PASS，分别输出公共执行器和多目标卡尺检查通过。

- [ ] **Step 6: 提交卡尺找圆接入**

```powershell
git add -- "Tests/MultiTargetCaliper.Tests.ps1" "Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs" "Node/4-Measurement/CaliperCircle/NodeCaliperCircle.cs"
git commit -m "perf: 并发执行多目标卡尺找圆"
```

### Task 3: 回归、编译、性能核验与任务记录

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: Task 1 的公共并发入口和 Task 2 的卡尺找圆接入。
- Produces: 可审计的测试、构建、性能验证记录与最终 Git 提交。

- [ ] **Step 1: 运行全部多目标专项测试**

Run:

```powershell
$tests=@(
  'Tests\PositionCorrectionSubscriptionCompatibility.Tests.ps1',
  'Tests\MultiTargetTransform.Tests.ps1',
  'Tests\MultiTargetPositionCorrectionNode.Tests.ps1',
  'Tests\MultiTargetMeasurementRunner.Tests.ps1',
  'Tests\MultiTargetCaliper.Tests.ps1',
  'Tests\MultiTargetFindPoint.Tests.ps1',
  'Tests\MultiTargetGeometryMeasurement.Tests.ps1'
)
foreach($test in $tests){
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test
  if($LASTEXITCODE -ne 0){ throw "$test failed with exit code $LASTEXITCODE" }
}
```

Expected: 7/7 PASS。

- [ ] **Step 2: 完整编译 Debug|x64**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: `0 个错误`；既有警告数量如实记录，不把无关警告纳入本次修复。

- [ ] **Step 3: 使用用户方案核对性能口径**

加载 `C:\Users\34652\Desktop\222.Sol`，确认卡尺找圆参数仍为 `Count=30`、`CaliperWidth=10`、`CaliperHeight=60`。在相同图像上先预热 5 次，再记录连续 20 次：

```text
节点 RunTime：并发开始到统一汇总结束的墙钟耗时
汇总 AlgorithmMs：5 个目标算法耗时之和
Items[n].AlgorithmMs：每个目标的独立算法耗时
```

Expected: 单目标继续使用直接路径；5 目标的墙钟耗时低于串行累计算法耗时，并如实记录中位数和 P95。若无法在自动化环境启动交互方案，则记录为需要用户在实际运行界面复测，不编造数据。

- [ ] **Step 4: 更新任务记录**

在 `FLOW_CANVAS_B_PLAN_TASKS.md` 追加：

```markdown
## 2026-07-21: 卡尺找圆多目标共享灰度图并发

- [x] 公共多目标执行器增加目标级受限并发，单目标保持直接路径。
- [x] 卡尺找圆只获取一次只读灰度图，并发执行各目标算法后按原顺序统一输出。
- [x] 单目标普通异常保留同位置全零 NG 项，取消继续向上层传播。
- [x] 节点墙钟耗时改用高精度 Stopwatch；汇总 AlgorithmMs 保持累计 CPU 工作量语义。
- [x] 记录专项测试、完整编译和实际性能核验结果。
```

- [ ] **Step 5: 检查差异并提交收尾记录**

Run:

```powershell
git diff --check
git status --short
```

Expected: `git diff --check` 返回 0，状态中只包含任务记录。

```powershell
git add -- "FLOW_CANVAS_B_PLAN_TASKS.md"
git commit -m "docs: 记录卡尺找圆并发验证"
```

- [ ] **Step 6: 最终检查**

Run:

```powershell
git status --short
git log -4 --oneline
```

Expected: 工作区干净，最近提交依次包含设计、实施计划、公共并发执行器、卡尺找圆接入和验证记录。
