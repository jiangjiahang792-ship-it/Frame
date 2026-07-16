# 大模型训练强制重建模型链实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让每次大模型训练都生成本次专用的新 ONNX，并在 GPU 模式下使用当前电脑显卡生成新 TensorRT Engine，随后重新训练 Bank，杜绝跨显卡复用旧 Engine。

**Architecture:** 保留现有 `LargeModelTrainingService` 的训练入口、数据集准备和模板打包结构，只把模型准备阶段从“文件存在即复用”改为“每次安全重建”。ONNX 与 Engine 均先生成到同目录临时文件，成功后通过同卷替换写入正式文件；任一步失败都终止本次训练，不回退到旧模型继续生成 Bank。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、PowerShell 5.1 源码契约测试、DINOv2 ONNX 导出脚本、TensorRT 8.6.1 `trtexec.exe`、Visual Studio 2022 MSBuild。

## Global Constraints

- GPU 每次训练固定执行“重新导出 ONNX → 当前显卡重新生成 Engine → 重新训练 Bank → 打包模板”。
- CPU 每次训练固定执行“重新导出 ONNX → 重新训练 Bank → 打包模板”，不生成 Engine。
- 同名 ONNX 或 Engine 已存在时不得跳过重建。
- ONNX 和 Engine 必须先写临时文件，成功后再替换正式文件；失败后不得复用旧模型继续本次训练。
- 所有新增用户可见文本和任务记录使用简体中文。
- 新增或修改的 C# 方法必须保留完整 XML 注释，关键失败保护逻辑添加说明。
- 不修改 native DLL，不处理无监督与大模型的进程级 DLL 冲突。
- 保留工作区中用户已有的相机、诊断和任务记录改动，不覆盖、不回退、不混入本功能提交。

---

### Task 1: 以测试驱动方式实现强制重建模型链

**Files:**
- Create: `Tests/LargeModelTrainingFreshArtifacts.Tests.ps1`
- Modify: `Forms/AiTrainForm/LargeModelTrainingService.cs:65`
- Modify: `Forms/AiTrainForm/LargeModelTrainingService.cs:168-440`
- Modify: `Tests/LargeModelIntegration.Tests.ps1`
- Test: `Tests/LargeModelTrainingFreshArtifacts.Tests.ps1`
- Test: `Tests/LargeModelIntegration.Tests.ps1`

**Interfaces:**
- Consumes: `LargeModelTrainingRequest`、`ExportOnnxModel`、`BuildTensorRtEngine`、`LargeModelDinov2Detector.TrainMemoryBank` 和 `LargeModelTrainingService.cs` 的源码文本。
- Produces: `RebuildTrainingModelPath(...)`、`RebuildOnnxModelPath(...)`、`ReplaceGeneratedFile(...)`、`TryDeleteGeneratedFile(...)`，以及临时文件替换和禁止旧文件短路的自动检查。

- [ ] **Step 1: 新建专项失败测试**

使用 `apply_patch` 创建 `Tests/LargeModelTrainingFreshArtifacts.Tests.ps1`，内容如下：

```powershell
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingService.cs"

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -lt 0) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -ge 0) {
        throw $Message
    }
}

function Assert-Matches {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
        $Text,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw $Message
    }
}

if (-not (Test-Path -LiteralPath $servicePath)) {
    throw "Missing large-model training service."
}

$service = Get-Content -LiteralPath $servicePath -Encoding UTF8 -Raw

Assert-Contains $service 'string modelPath = RebuildTrainingModelPath(runtime.RootPath, request, log, token);' "Training must rebuild the model for every run."
Assert-Contains $service 'string onnxPath = RebuildOnnxModelPath(root, request.InputWidth, request.InputHeight, sizeText, log, token);' "Every training run must rebuild ONNX before selecting CPU or GPU."
Assert-NotContains $service 'if (File.Exists(enginePath))' "GPU training must not reuse an existing TensorRT engine."
Assert-NotContains $service 'if (File.Exists(onnxPath))' "Training must not reuse an existing ONNX model."
Assert-Contains $service 'Path.GetFileNameWithoutExtension(onnxPath) + ".building.onnx"' "ONNX export must use a temporary file."
Assert-Contains $service 'ExportOnnxModel(root, tempOnnxPath, width, height, sizeText, log, token);' "ONNX export must target the temporary file."
Assert-Contains $service 'ReplaceGeneratedFile(tempOnnxPath, onnxPath);' "A completed ONNX file must replace the previous formal model."
Assert-Contains $service 'ReplaceGeneratedFile(tempEnginePath, enginePath);' "A completed engine must replace the previous formal engine."
Assert-Contains $service 'TryDeleteGeneratedFile(tempOnnxPath);' "Failed ONNX export must clean its temporary file."
Assert-Contains $service 'TryDeleteGeneratedFile(tempEnginePath);' "Failed engine generation must clean its temporary file."
Assert-Matches $service 'private static string RebuildTrainingModelPath\(.*?string onnxPath = RebuildOnnxModelPath\(.*?if \(string.Equals\(normalizedDevice, "CPU".*?return onnxPath;.*?BuildTensorRtEngine\(root, trtexecPath, onnxPath, enginePath, normalizedPrecision, log, token\);' "CPU must return the new ONNX while GPU must build a new engine from it."
Assert-Matches $service 'PrepareCleanDirectory\(modelOutputRoot, workRoot\);.*?string bankPath = Path.Combine\(modelOutputRoot, "memory.bank"\);.*?detector.TrainMemoryBank\(modelPath, bankPath' "Every run must clean the bank output and train a new bank."

Write-Host "Large-model fresh-artifact training checks passed."
```

- [ ] **Step 2: 更新现有集成测试中的模型准备语义**

在 `Tests/LargeModelIntegration.Tests.ps1` 中将旧断言：

```powershell
Assert-Contains $service "EnsureTrainingModelPath" "Training service must prepare the ONNX or engine matching the selected resolution."
Assert-Contains $service "EnsureOnnxModelPath" "Training service must auto-export the selected-resolution ONNX before failing."
```

替换为：

```powershell
Assert-Contains $service "RebuildTrainingModelPath" "Training service must rebuild the ONNX or engine for every training run."
Assert-Contains $service "RebuildOnnxModelPath" "Training service must export a fresh selected-resolution ONNX for every training run."
```

- [ ] **Step 3: 运行专项测试并确认按预期失败**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingFreshArtifacts.Tests.ps1
```

Expected: FAIL，首个错误为 `Training must rebuild the model for every run.`，证明测试捕获了当前复用旧模型的行为。

- [ ] **Step 4: 运行现有大模型集成测试并确认语义更新尚未实现**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelIntegration.Tests.ps1
```

Expected: FAIL，错误为 `Training service must rebuild the ONNX or engine for every training run.`。

- [ ] **Step 5: 让训练入口调用强制重建方法**

在 `LargeModelTrainingService.Train` 中将模型路径语句替换为：

```csharp
string modelPath = RebuildTrainingModelPath(runtime.RootPath, request, log, token);
```

- [ ] **Step 6: 用每次重建逻辑替换现有模型复用逻辑**

使用下面的完整方法替换现有 `EnsureTrainingModelPath` 和 `EnsureOnnxModelPath`：

```csharp
/// <summary>
/// 为本次训练重新生成与所选设备、分辨率和精度匹配的模型。
/// </summary>
/// <param name="runtimeRoot">大模型运行环境目录。</param>
/// <param name="request">训练请求。</param>
/// <param name="log">训练日志回调。</param>
/// <param name="token">取消令牌。</param>
/// <returns>本次训练实际使用的新 ONNX 或 TensorRT Engine 路径。</returns>
private static string RebuildTrainingModelPath(string runtimeRoot, LargeModelTrainingRequest request, IProgress<string> log, CancellationToken token)
{
    string root = LargeModelDinov2Native.ResolveRuntimeRoot(runtimeRoot);
    string sizeText = BuildSizeText(request.InputWidth, request.InputHeight);
    string normalizedDevice = LargeModelRuntimeBootstrapper.NormalizeDevice(request.Device);
    string normalizedPrecision = LargeModelRuntimeBootstrapper.NormalizePrecision(request.Precision);
    string onnxPath = RebuildOnnxModelPath(root, request.InputWidth, request.InputHeight, sizeText, log, token);

    if (string.Equals(normalizedDevice, "CPU", StringComparison.OrdinalIgnoreCase))
    {
        return onnxPath;
    }

    string enginePath = Path.Combine(root, "dinov2_" + sizeText + "_" + normalizedPrecision + ".engine");
    string trtexecPath = Path.Combine(root, "trtexec.exe");
    if (!File.Exists(trtexecPath))
        throw new FileNotFoundException("LargeModelDll 缺少 trtexec.exe，无法从 ONNX 生成 TensorRT Engine。", trtexecPath);

    log?.Report("开始使用当前电脑显卡重新生成 TensorRT Engine：" + Path.GetFileName(enginePath));
    BuildTensorRtEngine(root, trtexecPath, onnxPath, enginePath, normalizedPrecision, log, token);
    return enginePath;
}

/// <summary>
/// 为本次训练重新导出所选分辨率的 ONNX，并在成功后替换正式模型。
/// </summary>
/// <param name="root">大模型运行环境目录。</param>
/// <param name="width">输入宽度。</param>
/// <param name="height">输入高度。</param>
/// <param name="sizeText">分辨率文本。</param>
/// <param name="log">训练日志回调。</param>
/// <param name="token">取消令牌。</param>
/// <returns>本次训练新生成的 ONNX 完整路径。</returns>
private static string RebuildOnnxModelPath(string root, int width, int height, string sizeText, IProgress<string> log, CancellationToken token)
{
    string onnxPath = Path.Combine(root, "dinov2_" + sizeText + ".onnx");
    string tempOnnxPath = Path.Combine(root, Path.GetFileNameWithoutExtension(onnxPath) + ".building.onnx");
    TryDeleteGeneratedFile(tempOnnxPath);

    try
    {
        log?.Report("开始为本次训练重新导出 DINOv2 ONNX：" + Path.GetFileName(onnxPath));
        ExportOnnxModel(root, tempOnnxPath, width, height, sizeText, log, token);
        if (!File.Exists(tempOnnxPath))
            throw new FileNotFoundException("DINOv2 ONNX 导出完成后仍未找到临时模型文件：" + tempOnnxPath, tempOnnxPath);

        ReplaceGeneratedFile(tempOnnxPath, onnxPath);
        log?.Report("本次训练 ONNX 已更新：" + onnxPath);
        return onnxPath;
    }
    catch
    {
        TryDeleteGeneratedFile(tempOnnxPath);
        throw;
    }
}
```

- [ ] **Step 7: 增加安全替换和临时文件清理方法**

在 `LargeModelTrainingService` 的模型生成辅助方法区域增加：

```csharp
/// <summary>
/// 使用同目录生成完成的临时文件替换正式模型文件。
/// </summary>
/// <param name="temporaryPath">已经完整生成的临时文件路径。</param>
/// <param name="targetPath">正式文件路径。</param>
private static void ReplaceGeneratedFile(string temporaryPath, string targetPath)
{
    if (File.Exists(targetPath))
    {
        // 同目录替换由文件系统原子完成；替换失败时保留原正式文件。
        File.Replace(temporaryPath, targetPath, null, true);
        return;
    }

    File.Move(temporaryPath, targetPath);
}

/// <summary>
/// 尽力清理模型生成过程中遗留的临时文件。
/// </summary>
/// <param name="path">待清理的受控临时文件路径。</param>
private static void TryDeleteGeneratedFile(string path)
{
    try
    {
        if (File.Exists(path))
            File.Delete(path);
    }
    catch
    {
        // 临时文件清理失败不能覆盖真正的 ONNX 或 Engine 生成异常。
    }
}
```

- [ ] **Step 8: 让 Engine 构建失败时清理临时文件并安全替换正式文件**

保留 `BuildTensorRtEngine` 的参数和 `trtexec` 启动参数，将方法体改为下面的完整实现：

```csharp
private static void BuildTensorRtEngine(string runtimeRoot, string trtexecPath, string onnxPath, string enginePath, string precision, IProgress<string> log, CancellationToken token)
{
    string tempEnginePath = Path.Combine(Path.GetDirectoryName(enginePath), Path.GetFileNameWithoutExtension(enginePath) + ".building.engine");
    TryDeleteGeneratedFile(tempEnginePath);

    try
    {
        string precisionArgs = string.Equals(precision, "fp16", StringComparison.OrdinalIgnoreCase) ? " --fp16" : " --noTF32";
        string arguments = "--onnx=\"" + onnxPath + "\" --saveEngine=\"" + tempEnginePath + "\"" + precisionArgs + " --workspace=4096";
        StringBuilder processOutput = new StringBuilder();

        using (System.Diagnostics.Process process = new System.Diagnostics.Process())
        {
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = trtexecPath,
                Arguments = arguments,
                WorkingDirectory = runtimeRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.OutputDataReceived += (sender, args) => AppendProcessLog(processOutput, log, args.Data);
            process.ErrorDataReceived += (sender, args) => AppendProcessLog(processOutput, log, args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.WaitForExit(500))
            {
                if (token.IsCancellationRequested)
                {
                    TryKillProcess(process);
                    token.ThrowIfCancellationRequested();
                }
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("TensorRT Engine 生成失败，trtexec 返回码：" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + "\r\n" + processOutput);
        }

        if (!File.Exists(tempEnginePath))
            throw new FileNotFoundException("TensorRT 未生成临时 Engine 文件。", tempEnginePath);

        ReplaceGeneratedFile(tempEnginePath, enginePath);
        log?.Report("TensorRT Engine 重新生成完成：" + enginePath);
    }
    catch
    {
        TryDeleteGeneratedFile(tempEnginePath);
        throw;
    }
}
```

- [ ] **Step 9: 运行专项测试并确认通过**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingFreshArtifacts.Tests.ps1
```

Expected: PASS，并输出 `Large-model fresh-artifact training checks passed.`。

- [ ] **Step 10: 运行大模型集成测试并确认通过**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelIntegration.Tests.ps1
```

Expected: PASS，并输出现有大模型集成检查成功信息。

- [ ] **Step 11: 单独提交功能代码与自动测试**

```powershell
git add -- Forms/AiTrainForm/LargeModelTrainingService.cs Tests/LargeModelTrainingFreshArtifacts.Tests.ps1 Tests/LargeModelIntegration.Tests.ps1
git diff --cached --check
git commit -m "fix: 大模型训练每次重建模型链"
```

提交前确认暂存区只包含上述三个文件，不包含相机、诊断、项目文件或任务记录中的既有修改。

---

### Task 2: 更新任务记录并完成构建验证

**Files:**
- Modify: `任务记录.md`
- Verify: `TDJS-Vision.csproj`

**Interfaces:**
- Consumes: Task 2 已通过的专项测试与集成测试结果。
- Produces: 项目任务记录和 x64 Debug 可编译证据。

- [ ] **Step 1: 在任务记录末尾追加本次变更**

使用 `apply_patch` 在 `任务记录.md` 末尾追加：

```markdown
## 2026-07-16：大模型训练每次强制重建模型链

- 状态：已完成。
- GPU 训练：每次固定重新导出所选分辨率 ONNX，再使用当前电脑显卡及所选精度重新生成 TensorRT Engine，最后重新训练 memory bank，不再复用同名旧模型。
- CPU 训练：每次固定重新导出 ONNX 后重新训练 memory bank，不生成 TensorRT Engine。
- 失败保护：ONNX 和 Engine 均先写入同目录临时文件，生成成功后再替换正式文件；任一步失败都会清理临时文件并终止本次训练，不会回退旧模型继续训练。
- 自动检查：大模型强制重建专项检查和大模型集成检查均通过。
- 编译检查：Visual Studio 2022 MSBuild 的 x64 Debug 构建结果为 0 个错误。
```

- [ ] **Step 2: 运行两个大模型专项检查作为最终回归验证**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingFreshArtifacts.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelIntegration.Tests.ps1
```

Expected: 两个脚本均返回退出码 0。

- [ ] **Step 3: 编译 x64 Debug 项目**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "TDJS-Vision.csproj" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: `Build succeeded.` 且 `0 Error(s)`；既有警告不作为本次失败，但不得新增由本次三个功能文件引起的编译警告。

- [ ] **Step 4: 检查最终差异没有覆盖用户已有改动**

Run:

```powershell
git status --short
git diff --check
git diff -- Forms/AiTrainForm/LargeModelTrainingService.cs Tests/LargeModelTrainingFreshArtifacts.Tests.ps1 Tests/LargeModelIntegration.Tests.ps1 任务记录.md
```

Expected: 功能文件只包含强制重建模型链相关改动；相机、诊断、项目文件及其它已有修改保持原样。`任务记录.md` 已有用户内容与本次新增记录共同保留，且不纳入功能代码提交。
