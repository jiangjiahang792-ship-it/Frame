# 海康双相机Mat耗时稳定性测试

## 关键参数

| 参数 | 默认值 | 说明 |
| --- | ---: | --- |
| 触发方式 | Line0硬触发 | 与当前视觉主程序现场条件保持一致，也可切换连续采集 |
| 预热帧数 | 10 | 保留在CSV中，但不进入正式分位统计 |
| 每相机正式样本 | 10000 | 两台相机都达到目标后自动停止并导出 |
| 显示队列容量 | 1 | 只显示最新Mat，旧帧立即释放，UI不反压回调 |
| 显示刷新上限 | 20 FPS | 只影响UI，不进入相机回调耗时 |
| 输出像素格式 | 彩色BGR8/单色Mono8 | 与OpenCvSharp Mat通道顺序一致 |
| 构建配置 | Release x64 | 正式性能测试固定使用该配置 |

## 测试链路

本工具以海康官方旧版 `MultipleDemo` 和 `Grab_Callback` 为调用基线：

1. `MV_CC_RegisterImageCallBackEx_NET`接收SDK帧内存。
2. 每台相机使用独立、可复用的非托管目标缓冲区。
3. `MV_CC_ConvertPixelType_NET`把Bayer等输入转换成BGR8或Mono8。
4. 在目标缓冲区上建立OpenCvSharp `Mat`头。
5. `Mat.Clone()`生成回调返回后仍然有效的独立Mat。
6. 独立Mat发布到容量1的最新帧槽，由UI定时器转换成Bitmap显示。
7. 停止采集后才计算分位数并写入CSV，回调内不写日志、不写磁盘。

## 输出结果

每次测试保存在程序目录的 `Results\yyyyMMdd-HHmmss`：

- `相机1_逐帧耗时.csv`
- `相机2_逐帧耗时.csv`
- `测试汇总.txt`

汇总分别记录 `ConvertPixelType`、Mat建立、Mat独立复制、发布最新帧、回调业务总耗时、UI Mat转Bitmap和UI换图的平均值、P50、P95、P99、P99.9、最大值以及超过20/30/50ms的次数。

## 比较要求

- 两边使用同一台电脑、相机、网卡、分辨率、像素格式、曝光和触发频率。
- 主程序与本Demo不能同时打开同一台相机。
- 首先使用Line0硬触发和实时显示，采集每相机10000个正式样本。
- 第二轮可关闭实时显示，判断UI是否通过CPU和内存竞争影响回调长尾。
- 比较时优先看相同阶段，不要把Demo的回调总耗时直接和主程序的单独`ConvertPixelType`耗时混用。

## 依赖

- 海康MVS运行环境及原生 `MvCameraControl.dll`
- .NET Framework 4.8
- OpenCvSharp 4.10 x64

工程内的 `Vendor\MVCamera.cs`复制自本机海康官方示例。OpenCvSharp编译引用来自 `D:\MyCode\PublicWook\TDJS-Vision\packages`，构建时会把托管程序集和x64原生运行库复制到输出目录。
