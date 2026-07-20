# 大模型训练强制重建模型链实施计划

> **执行要求：** 使用 `superpowers:test-driven-development`，每项行为先 RED、再最小实现到 GREEN；最终审查清单是本计划的唯一验收来源。

## 目标

- GPU 每次固定执行：准备数据集 → 新 ONNX → 当前电脑新 Engine → 新 Bank → 打包。
- CPU 每次固定执行：准备数据集 → 新 ONNX → 新 Bank → 打包。
- 生成失败、验证失败或取消时，不得回退或消费旧模型继续本次 Bank 训练。
- 同一 `LargeModelDll` 运行目录中的完整训练链必须跨线程、跨进程串行。

## 最终架构

- `LargeModelTrainingCoordinator`：通过 `ILargeModelTrainingStages` 固定 CPU/GPU 阶段顺序，并在 `LargeModelDll` 中持有 `FileShare.None` 锁文件，锁覆盖数据集准备、模型重建、Bank 训练和模板打包。
- `LargeModelArtifactPublisher`：为每次生成创建同目录唯一临时路径，格式为 `<模型名>_<GUID>.building.onnx` 或 `<模型名>_<GUID>.building.engine`；生成完成后检查取消、最小长度和可加载性，全部通过后才原子替换正式模型。
- `ILargeModelArtifactGenerator`：隔离 ONNX 导出器与 TensorRT 构建器，行为测试使用可控假实现。
- `ILargeModelArtifactValidator`：生产实现通过 `LargeModelDinov2Detector.LoadModel` 按 CPU 模式验证 ONNX、按 GPU 模式验证 Engine，和 native Bank 训练使用同一模型加载入口。
- `LargeModelProcessOutputBuffer`：线程安全合并 stdout/stderr，避免两个异步事件并发修改原始 `StringBuilder`。
- `ProductionTrainingStages`：把现有数据集、native Bank 和模板打包逻辑接入协调器，生产入口必须使用该适配器。
- `LargeModelDinov2Detector`：通过 `ILargeModelDinov2NativeApi` 隔离 native 调用，以生命周期锁保证 Dispose 后晚到 Init 句柄只释放、不发布、不进入 Train。
- `LargeModelTemplatePublisher`：通过同目录 GUID 临时 ZIP、可取消复制、必要条目可读性验证和原子替换发布 `.tdlarge`，失败或取消保留旧模板。
- 生产服务注入边界：`ILargeModelTrainingEnvironment`、`ILargeModelTrainingStageFactory`、`ILargeModelTrainingModelRebuilder`、`ILargeModelDinov2DetectorFactory`、`ILargeModelTemplatePublisher`；默认构造仍连接真实生产实现。

## 全局约束

- 不因正式 ONNX 或 Engine 已存在而跳过生成。
- ONNX 和 Engine 最小有效长度均为 64 字节，并且发布前必须通过 native 可加载性验证。
- 外部进程启动前、最终退出后和正式模型替换前必须检查取消。
- 取消外部进程后最多等待 5 秒退出，再尝试清理临时文件。
- 临时文件清理失败只写简体中文日志，不得覆盖原始生成、验证或取消异常。
- 训练锁等待每 100 毫秒检查取消；非共享冲突的磁盘错误必须直接抛出，不得无限重试。
- 固定清理可信根为 `Model\_LargeModelTraining`，拒绝 `.`、`..`，不得使用用户名称派生的工作目录作为允许根。
- 模板复制和验证期间支持取消；正式模板原子提交后不得再次以令牌检查把成功改报为取消。
- 所有新增 C# 类、接口、字段、属性和方法使用简体中文 XML 注释。
- 不修改相机、诊断、native DLL 或其它无关功能。
- 构建固定使用 `/p:RestorePackages=false`，不得下载或更新依赖。

---

### Task 1：以可执行行为测试实现安全训练协调与产物发布

**文件：**

- 新增：`Forms/AiTrainForm/LargeModelTrainingPipeline.cs`
- 新增：`Tests/LargeModelTrainingBehavior.Tests.ps1`
- 修改：`Forms/AiTrainForm/LargeModelTrainingService.cs`
- 修改：`Tests/LargeModelTrainingFreshArtifacts.Tests.ps1`
- 修改：`Tests/LargeModelIntegration.Tests.ps1`
- 修改：`TDJS-Vision.csproj`

**输入：** 已批准设计与最终审查清单。

**输出：** 可注入协调器、唯一临时产物发布器、跨进程训练锁、native 加载验证、线程安全进程日志和生产接入。

- [x] RED：连续两次训练及 CPU/GPU 顺序测试因协调接口不存在而失败。
- [x] GREEN：实现 `ILargeModelTrainingStages` 与固定顺序协调器。
- [x] RED：预置旧模型、成功替换和生成失败测试因发布接口不存在而失败。
- [x] GREEN：实现唯一 GUID 临时路径与失败安全替换。
- [x] RED：启动前取消、生成返回边界取消、模型返回后取消测试发现仍会发布或进入 Bank。
- [x] GREEN：在生成前后、验证后、模型返回后和 Bank 前增加取消检查。
- [x] RED：零字节与短产物测试发现无效文件可替换正式模型。
- [x] GREEN：增加 64 字节最小长度和 native 可加载性验证，失败时旧文件字节不变且 Bank 调用数为零。
- [x] RED：两个并行协调调用可同时进入数据集阶段。
- [x] GREEN：增加 `FileShare.None` 锁文件和可取消等待，锁覆盖整条训练链。
- [x] RED：临时文件被占用时没有清理失败日志。
- [x] GREEN：尽力清理并记录简体中文日志，保留原始异常。
- [x] RED：并发 stdout/stderr 缺少线程安全缓存接口。
- [x] GREEN：增加并使用 `LargeModelProcessOutputBuffer`，并行压力测试无丢行或重复。
- [x] GREEN：生产 `LargeModelTrainingService` 通过 `ProductionTrainingStages` 使用协调器和发布器。
- [x] GREEN：保留并更新源码契约测试作为快速护栏，但行为验收以可执行测试为准。

---

### Task 2：同步记录并完成最终验证

**输入：** Task 1 已通过的行为测试、专项护栏和生产代码。

**输出：** 准确的任务记录、完整测试结果和 x64 Debug 构建证据。

- [x] 更新 `任务记录.md`，准确写明 GUID 临时文件、尽力清理日志、取消边界、跨进程串行和 native 有效性验证。
- [x] 运行：

  ```powershell
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingBehavior.Tests.ps1 -Case All
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingFreshArtifacts.Tests.ps1
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\LargeModelIntegration.Tests.ps1
  ```

- [x] 使用 Visual Studio 2022 MSBuild 执行：

  ```powershell
  & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "TDJS-Vision.csproj" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
  ```

- [x] 最终核对 `git diff --check`、暂存范围、报告和提交记录。

---

### Task 3：第二轮复审——生产入口取消、路径与模板发布安全

**文件：**

- 新增：`Tests/LargeModelTrainingProductionBehavior.Tests.ps1`
- 修改：`Forms/AiTrainForm/LargeModelDinov2Detector.cs`
- 修改：`Forms/AiTrainForm/LargeModelTemplatePackage.cs`
- 修改：`Forms/AiTrainForm/LargeModelTrainingPipeline.cs`
- 修改：`Forms/AiTrainForm/LargeModelTrainingService.cs`
- 修改：`Tests/LargeModelTrainingBehavior.Tests.ps1`

**接口：**

- `ILargeModelDinov2NativeApi`：把 native Configure、Init、Train、Infer、Release 与检测器生命周期分开，测试使用阻塞 Init 实现。
- `ILargeModelDinov2DetectorFactory`：生产阶段与模型验证统一创建真实检测器，测试仍运行真实检测器，只替换 native API。
- `ILargeModelTrainingEnvironment`：向生产服务提供已验证的 runtime/model 根目录，行为测试使用真实临时目录。
- `ILargeModelTrainingStageFactory` 与 `ILargeModelTrainingSession`：服务入口可替换阶段创建，同时默认路径仍创建真实 `ProductionTrainingStages`。
- `ILargeModelTrainingModelRebuilder`：真实生产阶段可记录并替换 ONNX/Engine 外部生成边界，验证逐级路径传递。
- `ILargeModelTemplatePublisher` 与 `ILargeModelTemplateSourceStreamFactory`：生产阶段调用可取消模板发布器；测试用阻塞源流稳定复现复制期间取消。

- [x] RED：阻塞 native Init 后取消并 Dispose，旧实现会让句柄复活且调用 native Train。
- [x] GREEN：检测器增加锁保护 disposed 生命周期；Init 结果只存局部变量，锁内确认可用后发布，否则立即释放；Init 后、Train 前检查令牌。
- [x] RED：生产服务输入模板名 `..` 会把工作目录规范化到 `Model`，可进入错误递归清理范围。
- [x] GREEN：拒绝 `.`、`..`，以固定 `Model\_LargeModelTraining` 为可信根解析工作目录；任何递归清理前验证目标严格位于该固定根下。
- [x] RED：复制模型期间取消或源流失败会截断正式 `.tdlarge`，且成功提交后协调器仍可能改报取消。
- [x] GREEN：同目录 GUID 临时 ZIP、逐块取消、必要条目与 ZIP 可读性验证、提交前最后取消检查、原子替换和失败尽力清理；协调器不在成功提交后再次检查取消。
- [x] RED：现有行为测试只执行协调器，无法证明服务入口及 ONNX → Engine → Bank → Package 路径传递。
- [x] GREEN：动态编译并直接调用真实 `LargeModelTrainingService.Train` 和 `ProductionTrainingStages`，逐次记录 Engine、Bank、Package 输入路径并断言 CPU/GPU 使用本次模型。

---

### Task 4：第二轮记录、完整验证与独立复审

**输入：** Task 3 的四轮 RED/GREEN 证据和已通过的生产入口行为测试。

**输出：** 同步设计、计划、任务记录、完整报告、构建证据、独立复审和逻辑提交。

- [x] 同步设计中的检测器取消生命周期、固定可信清理根和 `.tdlarge` 原子发布。
- [x] 更新 `LargeModelIntegration.Tests.ps1` 的过时断言文案和 `任务记录.md` 的实际覆盖范围。
- [x] 运行全部大模型行为测试、FreshArtifacts、Integration 和 x64 Debug `/p:RestorePackages=false` 构建。
- [x] 执行 `git diff --check`，追加 `.superpowers/sdd/final-fix-report.md`，安排独立只读复审。
- [x] 按代码测试与文档记录拆分提交，确认工作树干净。
