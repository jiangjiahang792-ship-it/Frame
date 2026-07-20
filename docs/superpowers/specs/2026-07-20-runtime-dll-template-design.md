# Release 运行库模板与空方案复制设计

## 目标

- 以当前 `bin\x64\Release` 顶层 DLL 环境为正确基准。
- 重新生成 Debug 或 Release 后，项目不能自动恢复的顶层运行库仍与基准一致。
- `空方案.Sol` 在 AnyCPU/x64 的 Debug、Release 输出目录中始终与根模板一致。
- 后续新增普通运行时 DLL 时，只需放入一个明确的源目录。

## 范围

- 新增项目级 `RuntimeDll` 目录，保存当前正确 Release 中无法由项目引用或 NuGet 自动恢复的顶层 DLL。
- 项目生成完成后，把 `RuntimeDll` 中的顶层 DLL 复制到 `$(TargetDir)`。
- 复制任务启用未变化文件跳过，避免每次生成重复写入约 250 MB 文件；源文件或目标文件变化时仍会覆盖目标。
- 根目录保留唯一的 `空方案.Sol`，使用项目内容复制规则输出到 `$(TargetDir)`。

## 不在范围内

- `LargeModelDll` 和 `UnsupervisedDll` 继续作为 Debug 本地隔离环境，不纳入项目复制。
- `YoloGPUDll` 当前 Debug、Release 的 54 个文件路径和大小一致，但体积约 5 GB，继续保持现状，不纳入每次生成复制。
- `Model`、`ProjConfigs`、`Logs` 等运行数据不作为 DLL 模板管理。
- 由编译引用、NuGet 和 OpenCvSharp 构建规则自动产生的文件不重复存入 `RuntimeDll`。

## 生成流程

1. MSBuild 正常编译程序并复制引用依赖。
2. MSBuild 把根目录 `空方案.Sol` 复制到当前输出目录。
3. 自定义生成目标枚举 `RuntimeDll\*.dll`，复制到当前输出目录根部。
4. Debug、Release、AnyCPU、x64 共用相同规则，不维护四份配置。

## 后续添加规则

- 仅在运行时动态加载或作为原生依赖的普通 DLL：放入项目根目录 `RuntimeDll`，无需修改复制脚本。
- C# 代码需要直接引用类型的托管 DLL：仍放入项目根目录 `dll`，并在 Visual Studio 项目引用中添加该 DLL。
- 大模型和无监督隔离环境文件：分别维护 Debug 输出目录下的 `LargeModelDll`、`UnsupervisedDll`，不放入 `RuntimeDll`。

## 验证

- 自动检查空方案 JSON 结构有效且设备、流程为空。
- 自动检查项目复制规则覆盖所有构建配置，并排除三个大型隔离运行环境目录。
- 分别生成 x64 Debug 和 x64 Release。
- 对 `RuntimeDll` 与两个输出目录中的同名 DLL 计算 SHA-256，必须全部一致。
- 对根目录和两个输出目录的 `空方案.Sol` 计算 SHA-256，必须一致。
