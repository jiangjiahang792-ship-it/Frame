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

- ONNX 先输出为同目录临时文件，导出成功且文件有效后再替换正式 ONNX。
- Engine 继续先输出为 `.building.engine` 临时文件，构建成功后再替换正式 Engine。
- 每次训练开始时清理该模板受控工作目录中的旧 Bank；只有本次 native 训练成功并生成 Bank 后才继续打包。
- ONNX 或 Engine 生成失败时保留上一份正式模型文件，便于已有模板继续推理，但本次训练必须停止，不能回退复用旧文件继续训练。
- 临时文件只允许在 `LargeModelDll` 运行目录中创建和清理，工作目录清理继续受现有路径边界保护。

## 日志与错误

训练日志按阶段输出简体中文信息：重新导出 ONNX、重新生成 TensorRT Engine、训练 memory bank、打包模板。每个外部进程失败时保留其退出码和标准输出；错误信息必须明确指出失败阶段，避免统一表现为 native `-1`。

## 测试

- 自动检查 GPU 路径不再因为 `File.Exists(enginePath)` 直接返回旧 Engine。
- 自动检查 ONNX 路径不再因为 `File.Exists(onnxPath)` 直接返回旧 ONNX。
- 自动检查 GPU 顺序为强制 ONNX 导出、Engine 构建、Bank 训练。
- 自动检查 CPU 路径强制 ONNX 导出但不生成 Engine。
- 自动检查 ONNX 使用临时文件并在成功后原子替换，失败时不回退旧模型继续训练。
- 运行大模型专项 PowerShell 测试，并使用 Visual Studio 2022 MSBuild 编译 x64 Debug 项目。

## 非目标

- 本次不修改 DINOv2、TensorRT 或 ONNX Runtime 的 native 实现。
- 本次不解决 NVIDIA 驱动缺失、显存不足、TensorRT 不支持目标显卡等外部环境问题；程序应在对应生成阶段给出明确日志。
- 本次不处理无监督模块与大模型模块之间的进程级 native DLL 版本冲突。
