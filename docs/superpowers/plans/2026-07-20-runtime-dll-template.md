# Release 运行库模板与空方案复制实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把当前正确的 x64 Release 顶层手工运行库固化为唯一模板，并在所有 Debug、Release 生成后自动恢复这些 DLL 和空方案文件。

**Architecture:** 项目引用和 NuGet 继续负责可自动恢复的托管依赖；新增 `RuntimeDll` 只保存隔离 Release 生成无法产生的 35 个顶层 DLL。MSBuild 在 `Build` 完成后把该目录中的 DLL 复制到 `$(TargetDir)`，空方案通过 `CopyToOutputDirectory=Always` 复制。

**Tech Stack:** .NET Framework 4.8、经典 MSBuild 项目、PowerShell 5.1 回归检查、SHA-256 文件校验。

## Global Constraints

- 以当前 `bin\x64\Release` 顶层 DLL 环境为正确基准。
- `LargeModelDll`、`UnsupervisedDll`、`YoloGPUDll` 不纳入项目自动复制。
- AnyCPU/x64 的 Debug、Release 使用同一份 `RuntimeDll` 和同一份 `空方案.Sol`。
- 未变化的约 250 MB DLL 不重复复制，源或目标变化时必须恢复目标。
- 所有新增中文内容使用简体中文和 UTF-8。

---

### Task 1: 运行库模板规则回归检查

**Files:**
- Create: `Tests/RuntimeDependencyTemplate.Tests.ps1`
- Test: `Tests/RuntimeDependencyTemplate.Tests.ps1`

**Interfaces:**
- Consumes: `TDJS-Vision.csproj`、`空方案.Sol`、`RuntimeDll\*.dll`
- Produces: 静态回归检查入口 `powershell -File Tests\RuntimeDependencyTemplate.Tests.ps1`

- [ ] **Step 1: 写入失败检查**

检查必须覆盖：空方案 JSON 的 `SolVer`、空 `Devices`、空 `ProcessInfos`；项目存在 `RuntimeDependency` 通配项和 `CopyRuntimeDependencies` 目标；复制目标为 `$(TargetDir)`；启用 `SkipUnchangedFiles=true`；项目不绑定三个大型隔离目录；35 个 Release 基准 DLL 全部存在于 `RuntimeDll`。

- [ ] **Step 2: 运行检查并确认因功能尚未实现而失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\RuntimeDependencyTemplate.Tests.ps1`

Expected: FAIL，首个原因是根目录 `空方案.Sol`、`RuntimeDll` 或项目复制规则不存在。

### Task 2: 固化 Release 顶层运行库与空方案

**Files:**
- Create: `空方案.Sol`
- Create: `RuntimeDll/README.md`
- Create: `RuntimeDll/*.dll`，来源为当前 `bin/x64/Release` 中隔离生成无法恢复的 35 个顶层 DLL
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/RuntimeDependencyTemplate.Tests.ps1`

**Interfaces:**
- Consumes: 当前正确的 `bin\x64\Release` 顶层 DLL
- Produces: `@(RuntimeDependency)` MSBuild 项集合与 `CopyRuntimeDependencies` 生成目标

- [ ] **Step 1: 创建唯一空方案模板**

```json
{
  "SolVer": "1.0.0.0",
  "Devices": {},
  "ProcessInfos": []
}
```

- [ ] **Step 2: 从当前 Release 复制 35 个非自动生成 DLL 到 RuntimeDll**

只复制隔离生成目录中不存在的 Release 顶层 DLL；不得复制 `LargeModelDll`、`UnsupervisedDll`、`YoloGPUDll` 或运行数据目录。

- [ ] **Step 3: 添加 MSBuild 复制规则**

```xml
<Content Include="空方案.Sol">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
<RuntimeDependency Include="$(ProjectDir)RuntimeDll\*.dll" />
<Target Name="CopyRuntimeDependencies" AfterTargets="Build">
  <Copy SourceFiles="@(RuntimeDependency)"
        DestinationFolder="$(TargetDir)"
        SkipUnchangedFiles="true" />
</Target>
```

- [ ] **Step 4: 运行专项检查并确认通过**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\RuntimeDependencyTemplate.Tests.ps1`

Expected: PASS，并输出运行库模板检查通过。

### Task 3: 双配置生成与文件一致性验证

**Files:**
- Modify: `任务记录.md`
- Verify: `bin/x64/Debug/空方案.Sol`
- Verify: `bin/x64/Release/空方案.Sol`
- Verify: `bin/x64/Debug/*.dll`
- Verify: `bin/x64/Release/*.dll`

**Interfaces:**
- Consumes: `RuntimeDll\*.dll`、`空方案.Sol`、`TDJS-Vision.csproj`
- Produces: 可重复生成的 x64 Debug、x64 Release 输出环境

- [ ] **Step 1: 生成 x64 Debug**

Run: `MSBuild.exe TDJS-Vision.sln /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 0 个错误。

- [ ] **Step 2: 生成 x64 Release**

Run: `MSBuild.exe TDJS-Vision.sln /t:Build /p:Configuration=Release /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 0 个错误。

- [ ] **Step 3: 校验输出哈希**

对 `RuntimeDll\*.dll` 逐个计算 SHA-256，并与两个输出目录中的同名文件比较；根目录 `空方案.Sol` 也与两个输出副本比较。任何缺失或哈希不同都必须失败。

- [ ] **Step 4: 更新任务记录**

记录 Release 基准来源、35 个顶层 DLL、统一复制入口、三个排除目录、Debug/Release 生成结果和哈希检查结果。

- [ ] **Step 5: 运行最终专项检查与差异检查**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\RuntimeDependencyTemplate.Tests.ps1`

Run: `git diff --check`

Expected: 专项检查通过，Git 差异无空白错误。
