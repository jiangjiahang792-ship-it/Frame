# 公共运行库模板

此目录保存程序输出目录根部需要的运行时 DLL。项目生成 Debug 或 Release 后，会自动把这里的 DLL 复制到程序 EXE、DLL 所在目录。

## 添加规则

- 仅在运行时动态加载或作为原生依赖的普通 DLL：直接放入本目录，下次生成时自动复制。
- C# 代码需要直接引用其中类型的托管 DLL：放入项目根目录 `dll`，并在 Visual Studio 的项目引用中添加该 DLL。
- `LargeModelDll`、`UnsupervisedDll`、`YoloGPUDll` 是独立运行环境，不要放入本目录。

本目录的初始文件来自 2026-07-20 确认可用的 `bin\x64\Release` 顶层运行环境。
