# 新版本轮廓匹配原生库

`vendor` 来自 `D:/MyCode/PublicWook/VisionDemo1012/VisionDemo`，导入日期为 2026-10-21。包含 `CContourMatchCV`、`CContourMatchCV2` 及其计算依赖。`CContourMatchHalcon.h` 仅提供共享值类型，不编译 HALCON 实现，不需要 HALCON 运行库。

`native/include/CShapeMatchCV.h` 是框架适配器，默认选择第二版，保留第一版。适配器负责 ROI 掩膜、事务式重建、擦除重放、搜索角度缓存，以及将轮廓原点转换为框架的 ROI 中心。匹配计算由 Demo 类执行。返回宽高包含缩放，ScaleX/ScaleY 为独立尺度；有序轮廓保持 ROI 局部坐标和开放链。

发布库为 `Native/NewContourMatchNative.dll`，Windows x64，ABI 版本 2；与旧 `ShapeMatchNative.dll` 隔离。仅两个 AVX2 源文件启用专用指令，通用分发按 CPU 能力选择实现。

```powershell
./Native/NewContourMatchSource/build.ps1 -OpenCvPackageRoot 'C:/Users/34652/Desktop/模板匹配/模板匹配/vcpkg_installed/shape-match-x64-windows-static'
```

主工程构建完成后运行 `Tests/NewContourMatch.Tests.ps1`。算法输出质量及节拍需结合现场图像和参数评估，合成数据测试不能代替现场验收。
