# 大模型训练强制重建模型链设计

## 目标

解决开发机生成的 TensorRT Engine 被复制到不同型号客户显卡后仍被直接复用、最终在 native 训练阶段仅返回 `-1` 的问题。每次开始训练时都重新生成与当前训练参数及当前电脑环境匹配的模型文件，再生成新的 memory bank。

## 已确认行为

### GPU 训练

每次训练固定执行以下完整链路，不因同名文件已经存在而跳过任何步骤：

1. 按界面输入宽度和高度重新导出 DINOv2 ONNX。
2. 使用当前电脑的 `trtexec.exe`、当前 NVIDIA 显卡和所选 `fp16` 或 `fp32` 精度重新生成 TensorRT Engine。
3. 使用本次新生成的 Engine 和训练图片重新训练 memory bank。
4. 将本次 Engine、Bank 和参数清单打包到 `.tdlarge` 模板。

### CPU 训练

每次训练固定重新导出 ONNX，再使用本次 ONNX 训练 memory bank。CPU 路径不生成 TensorRT Engine，最终将本次 ONNX、Bank 和参数清单打包到 `.tdlarge` 模板。

## 方案比较与选择

- 方案一：每次完整重建 ONNX、Engine 和 Bank。优点是不会复用开发机或上一次训练残留的模型文件，并能在训练阶段暴露客户机缺少导出环境、GPU 驱动或 TensorRT 不兼容问题；缺点是每次训练耗时最长。用户已选择此方案。
- 方案二：复用 ONNX，只重建 Engine 和 Bank。可以解决跨显卡 Engine 不兼容问题，耗时更短，但不符合用户要求的完整重建规则。
- 方案三：保存显卡和运行库指纹，仅在环境变化时重建 Engine。运行效率最高，但需要维护指纹兼容规则，仍可能遗漏无法识别的环境变化，不在本次范围内。

## 文件生成与失败安全

- ONNX 和 Engine 均先输出为同目录 `<模型名>_<GUID>.building.<扩展名>` 唯一临时文件；达到 64 字节且通过实际 native 加载验证后，才原子替换正式模型。
- 每次训练开始时清理该模板受控工作目录中的旧 Bank；只有本次 native 训练成功并生成 Bank 后才继续打包。
- ONNX 或 Engine 生成失败时保留上一份正式模型文件，便于已有模板继续推理，但本次训练必须停止，不能回退复用旧文件继续训练。
- `.tdlarge` 先写入同目录 `<模板名>_<GUID>.building.tdlarge`；模型和 Bank 逐块复制时检查取消，完整关闭 ZIP 后验证 `manifest.json`、本次模型和 `bank.bin` 均存在且可读，最后一次取消检查通过后才原子替换正式模板。
- 模板打包失败或取消时保留旧正式模板并尽力清理本次临时文件；正式模板一旦原子提交，调用链不得再用提交后的取消检查把成功改报为取消。
- 模板工作目录的可信允许根固定为 `Model\_LargeModelTraining`。模板名 `.`、`..` 必须在任何递归清理前拒绝，规范化后的模板工作目录及 `dataset`、`model` 清理目标都必须严格位于该固定根之下。

## 取消与 native 生命周期

- 外部 ONNX/Engine 进程在启动前、最终退出后检查取消；取消时终止进程并最多等待 5 秒退出，再尽力清理临时模型。
- DINOv2 检测器使用生命周期锁保护 disposed 状态和句柄发布。native Init 返回值先保存在局部变量中；只有对象未释放且令牌未取消时才能在锁内发布，否则立即释放局部句柄。
- `TrainMemoryBank` 接收 `CancellationToken`，在 Init 返回后及 native Train 前检查取消；取消发生在阻塞 Init 内时，Dispose 后句柄不能复活，native Train 调用数必须为零。
- 同一 `LargeModelDll` 的训练链使用 `FileShare.None` 锁文件跨线程、跨进程串行，锁覆盖数据集、模型、Bank 和模板原子发布。

## 日志与错误

训练日志按阶段输出简体中文信息：重新导出 ONNX、重新生成 TensorRT Engine、训练 memory bank、打包模板。每个外部进程失败时保留其退出码和标准输出；错误信息必须明确指出失败阶段，避免统一表现为 native `-1`。

## 测试

- 可执行组件测试检查连续 CPU/GPU 顺序、模型发布、无效产物、取消、跨进程锁、清理异常和并发进程日志。
- 可执行生产测试动态编译真实 `LargeModelTrainingService`、`ProductionTrainingStages`、检测器和模板发布器，直接调用 `Train`。
- 阻塞 native Init 测试按“进入 Init → 取消并 Dispose → 允许 Init 返回”排序，断言晚到句柄释放一次且 native Train 为零。
- 生产路径边界测试输入模板名 `..`，断言在任何清理前失败且 `Model\dataset`、`Model\model` 哨兵不变。
- 模板测试覆盖复制期间取消、源流写入失败、旧模板保留、无 GUID 临时残留、ZIP 必要条目验证和成功原子替换。
- CPU/GPU 生产入口逐次记录并断言：CPU Bank/Package 使用本次 ONNX；GPU Engine 使用本次 ONNX；GPU Bank/Package 使用本次 Engine。
- 运行大模型专项 PowerShell 测试，并使用 Visual Studio 2022 MSBuild 编译 x64 Debug 项目。

## 非目标

- 本次不修改 DINOv2、TensorRT 或 ONNX Runtime 的 native 实现。
- 本次不解决 NVIDIA 驱动缺失、显存不足、TensorRT 不支持目标显卡等外部环境问题；程序应在对应生成阶段给出明确日志。
- 本次不处理无监督模块与大模型模块之间的进程级 native DLL 版本冲突。
