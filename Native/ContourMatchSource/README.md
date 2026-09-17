# 轮廓模板匹配原生算法

本目录保留用户提供的桌面“模板匹配”Demo的 C++ 算法、C ABI 适配层和原生冒烟测试。算法源码未改动。原 Demo 操作与算法说明见 [README-Demo.md](README-Demo.md)，集成后的使用方法见 [节点说明](../../docs/轮廓模板匹配节点.md)。

## 发布库

主工程使用 `Native/ShapeMatchNative.dll`，构建时自动复制到程序目录。该库为 Windows x64，静态链接 OpenCV 4.12 和 C/C++ 运行库，使用 OpenMP 时依赖 `VCOMP140.dll`（Microsoft Visual C++ x64 运行库）。不依赖 .NET 8，不需要分发桌面 Demo。

本次发现桌面 Demo 的预编译 DLL 未导出 `sm_get_contours`，因此使用本目录保留的最新源码重新编译，并通过原生测试和主工程托管调用测试。

## 重新编译

需要 Visual Studio 2022 C++ 工具、Windows SDK、CMake 和已安装的 x64 静态 OpenCV 包（静态 CRT `/MT`，包含 core、imgproc、video 组件）。脚本不自动下载依赖。当前桌面 Demo 的 `vcpkg_installed/shape-match-x64-windows-static` 可直接作为包目录；其他机器应自行准备同样的依赖。

```powershell
./Native/ContourMatchSource/build.ps1 -OpenCvPackageRoot 'C:/path/to/vcpkg_installed/shape-match-x64-windows-static' -CMakeExecutable 'C:/path/to/cmake.exe'
```

脚本在 `artifacts/ContourMatchValidation/native-build` 编译并运行 CTest，全部成功后更新 `Native/ShapeMatchNative.dll`。再构建主工程，即可将新 DLL 复制到输出目录。目录中的 `vcpkg.json` 保留原 Demo 的依赖声明，脚本使用已经编译的包。

托管接入代码位于 `Node/3-Detection/ContourMatch`，通过 `IShapeMatcher` 隔离算法。每个节点独立缓存生产模型；编辑预览使用单独实例。原生函数尚无中途取消接口，取消在调用前后生效，并清除本次结果。
