# TDJS-Vision

TDJS-Vision 是一套基于 WinForms 的机器视觉 AI 检测系统框架，用于工业视觉项目中的图像采集、图像处理、AI 检测、设备通信、流程编排和检测结果输出。

项目采用“方案 - 流程 - 节点”的编排模型：一个方案可以包含多个流程，一个流程由多个节点按顺序组成，节点负责完成具体动作，例如相机取图、光源控制、AI 推理、PLC/Modbus/TCP 通信、结果汇总、图像保存等。

## 技术栈

- .NET Framework 4.8
- Windows Forms
- C# 7.3
- OpenCvSharp
- Newtonsoft.Json
- SunnyUI
- DockPanelSuite / WeifenLuo.WinFormsUI.Docking
- HslCommunication
- NModbus4
- EPPlus
- Microsoft.CodeAnalysis.CSharp.Scripting
- TensorRT / OpenVINO 相关推理运行库
- 海康、Basler、华睿等工业相机 SDK

## 项目结构

```text
TDJS-Vision
├── Program.cs                       程序入口
├── FormMain.cs                      主窗体，负责菜单、工具栏、DockPanel、方案运行入口
├── Solution.cs                      全局方案单例，管理流程、节点、设备、共享变量和运行调度
├── Process.cs                       流程对象，负责节点顺序执行和逻辑跳转
├── ConfigHelper.cs                  方案保存/加载和多态序列化
├── SharedVariable.cs                线程安全共享变量容器
├── ProcessCompletionTracker.cs      流程完成事件和状态跟踪
├── Device                           设备抽象和具体设备实现
├── Forms                            业务窗体、设备管理窗体、结果视图、日志视图等
├── Node                             节点体系，包含采集、处理、检测、通信、逻辑、结果处理节点
├── Resources                        图标和图片资源
├── dll                              第三方或设备 SDK 依赖
├── packages                         NuGet packages.config 方式的依赖包
└── ToolTreeView.xml                 工具箱节点树配置
```

## 核心概念

### 方案 Solution

`Solution` 是全局单例，保存当前软件正在编辑或运行的完整方案信息，包括：

- 所有流程 `AllProcesses`
- 所有节点 `Nodes`
- 所有设备 `AllDevices`
- 共享变量 `SharedVariable`
- 全局信号 `GlobalSignal`
- 检测项配置 `DetectItemDic`
- 运行取消令牌 `CancellationToken`

方案保存为 `.Sol` 文件，底层是 JSON 配置。

### 流程 Process

一个流程表示一条检测或控制链路。流程内部维护节点列表，并按顺序执行节点。

流程支持：

- 启用/禁用
- OK/NG 数量统计
- 运行耗时统计
- 运行优先级 `RunLv`
- 运行组别 `Group`
- 被动触发流程
- If/Else/EndIf 逻辑跳转
- 流程完成事件通知

### 节点 NodeBase

所有功能节点都继承自 `NodeBase`。节点同时具备 UI 控件能力和运行能力。

一个标准节点通常包含：

- `NodeXXX.cs`：节点执行逻辑
- `NodeParamXXX.cs`：节点参数
- `NodeResultXXX.cs`：节点结果
- `ParamFormXXX.cs`：参数设置界面
- `ParamFormXXX.Designer.cs`：WinForms 设计器文件

节点运行入口是：

```csharp
public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
```

节点通过 `NodeReturn` 告诉流程下一步如何执行：

- `ContinueRun`：继续运行
- `StopRun`：停止后续节点
- `NextIndex`：跳转到指定节点索引

## 运行调度

方案运行时，`Solution.Run()` 会按流程组并行运行。

同一组内：

1. 按流程优先级 `RunLv` 从高到低执行。
2. 同优先级主动流程并行运行。
3. 主动流程中的 `ProcessTrigger` 节点可以触发同组被动流程。
4. 等主动流程和被动流程结束后，本轮运行完成。
5. 如果是循环运行，则等待 `RunInterval` 后进入下一轮。

简化链路如下：

```text
Solution.Run
  -> 按 Group 分组并行
    -> 组内按 RunLv 顺序
      -> 主动流程并行
        -> 节点顺序执行
          -> 可触发被动流程
```

## 节点分类

`Node` 目录按功能分为多个模块：

```text
1-Acquisition
  图像源、图像显示

2-ImagePreprocessing
  图像裁剪、图像旋转、图像分割

3-Detection
  AI检测、找线、找圆、模板匹配、二维码识别、颜色识别、二值化分析、电池极耳检测

5-EquipmentCommunication
  光源控制、相机IO、PLC读写、Modbus读写、TCP通信、串口发送、IO模块

6-LogicTool
  共享变量、条件运行、流程触发、流程信号、等待流程完成、If/Else/EndIf、C#脚本、延时、弹窗

7-ResultProcessing
  AI结果绘制、结果汇总、数据显示、图像保存、图像删除、Excel导出
```

## 数据流

图像数据主要使用 `OpenCvSharp.Mat`。

图像类节点通常输出 `OutputImage`：

```csharp
public class OutputImage
{
    public Mat SrcImg { get; set; }
    public List<Mat> Bitmaps { get; set; }
    public List<Rect> Rectangles { get; set; }
}
```

AI 检测节点输出 `AlgorithmResult`：

```csharp
public class AlgorithmResult
{
    public Dictionary<string, List<SingleDetectResult>> DetectResults { get; set; }
    public List<ColorRotatedRect> Rects { get; set; }
    public Dictionary<string, List<ColorRotatedRect>> RectsNgMap { get; set; }
    public List<ColorText> Texts { get; set; }
    public List<ColorLine> Lines { get; set; }
    public List<ColorCircle> Circles { get; set; }
}
```

典型流程：

```text
图像源
  -> 图像预处理
  -> AI/传统视觉检测
  -> 结果汇总
  -> 结果绘制/显示
  -> 通信输出/图片保存/Excel导出
```

## AI 检测

AI 主节点位于：

```text
Node/3-Detection/TDAI
```

支持的模型类型：

- DET
- OBB
- SEG
- POSE

模型推理封装位于：

```text
Node/3-Detection/TDAI/Yolo8
```

结果解析位于：

```text
Node/3-Detection/TDAI/Parse
```

当前项目中已有的模型业务类型包括：

- `RL_12类线芯模型`
- `RL_线芯截面`
- `合压模型`

## 设备体系

设备统一实现 `IDevice` 接口，并挂载到 `Solution.Instance.AllDevices`。

当前支持的设备类型包括：

- 光源
- 工业相机
- PLC
- Modbus RTU/TCP
- TCP Client/Server
- 串口 COM

相关目录：

```text
Device/Camera
Device/Light
Device/PLC
Device/Modbus
Device/TCP
Device/COM
```

## 方案保存与加载

方案通过 `ConfigHelper` 保存和加载。

保存内容主要包括：

- 方案版本
- 方案名称
- 运行间隔
- 设备列表
- 流程列表
- 节点列表
- 节点参数
- 全局信号
- 检测项配置
- OK/NG 统计

由于设备、节点参数等大量使用接口和多态类型，项目自定义了多个 JSON 转换器：

- `PolyConverter`
- `DeviceListConverter<T>`
- `ROIListConverter<T>`

加载方案时会先清理旧方案资源，再通过反序列化完成事件按顺序恢复设备、流程和节点。

## 构建与运行

建议使用 Visual Studio 打开：

```text
TDJS-Vision.sln
```

目标框架：

```text
.NET Framework 4.8
```

常用构建配置：

```text
Debug | x64
Release | x64
```

运行时需要确保设备 SDK、AI 推理 DLL、OpenCV/OpenVINO/TensorRT 相关 DLL 位于输出目录或可被系统路径加载。

程序支持通过命令行传入 `.Sol` 文件：

```text
TD-Vision.exe path\to\方案.Sol
```

## 开发新节点

新增节点建议按现有结构创建独立目录，并包含以下文件：

```text
NodeXXX.cs
NodeParamXXX.cs
NodeResultXXX.cs
ParamFormXXX.cs
ParamFormXXX.Designer.cs
```

开发步骤：

1. 在 `INode.cs` 的 `NodeType` 中增加节点类型。
2. 创建节点类并继承 `NodeBase`。
3. 实现参数类并继承 `INodeParam`。
4. 实现结果类并继承 `INodeResult`。
5. 实现参数窗体并继承 `INodeParamForm`。
6. 在流程编辑/节点创建逻辑中注册新节点。
7. 在 `ToolTreeView.xml` 中加入工具箱显示项。
8. 如需保存参数，确保参数类型可以被 `PolyConverter` 正确反序列化。

## 开发注意事项

- 不要直接绕过 `Solution.Instance` 修改全局流程、设备、节点状态。
- 节点运行中应定期调用 `CheckTokenCancel(token)` 响应停止运行。
- 节点失败时应设置节点状态并抛出异常，让流程统一处理。
- 设备资源需要在方案重置或软件关闭时正确释放。
- AI 节点删除或方案切换时需要释放模型句柄。
- 多线程访问共享数据时应使用已有的共享变量或线程安全容器。
- 方案文件依赖类型全名反序列化，重命名命名空间或类名可能导致旧方案无法加载。
- WinForms 设计器文件尽量通过设计器维护，避免手动大规模修改。

## 当前已知维护点

- `README.md` 和 `ToolTreeView.xml` 旧内容曾出现编码不匹配导致的乱码，需要统一保存为 UTF-8。
- `Solution.Run(bool, bool)` 中存在空转等待逻辑，后续应确认是否为临时代码。
- 部分异常处理存在吞异常或 `throw ex` 的写法，后续可统一为更清晰的异常传播方式。
- 运行状态里“提前完成”和“取消”的语义可进一步统一。

