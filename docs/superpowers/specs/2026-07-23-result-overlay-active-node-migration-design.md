# ROI结果绘制实际入口迁移设计

日期：2026-07-23

## 背景与根因

现场截图打开的是 NodeType.ResultOverlayDraw，对应窗体标题“ROI结果绘制”。2026-07-22完成的两类型简化落在 NodeType.ResultOverlayDraw2，对应标题“ROI结果绘制2”，两者由 NodeFactory、ProcessEditPanel 和方案序列化分别实例化，因此当前方案中的旧节点不会显示新界面。

## 目标

把已经确认的“文本 + 自动ROI”设计迁移到实际使用的 ROI结果绘制 节点。保留 NodeType.ResultOverlayDraw、NodeParamResultOverlayDraw 和 NodeResultResultOverlayDraw，避免现有 .Sol 方案节点类型、参数对象及下游输出订阅变化。

## 方案选择

采用“原节点原位升级并复用共享适配器”：

- 不把 ResultOverlayDraw 强制映射为 ResultOverlayDraw2，避免旧参数和结果类型发生隐式替换。
- 在旧参数模型尾部增加 Roi、单布尔判定和全局OK/NG颜色字段。
- 新ROI分支调用已经实现的 OverlayGeometryAdapterRegistry。
- 旧 Text、Line、Rectangle、Region 枚举和每项颜色字段继续反序列化；旧几何类型仍按原专用逻辑运行。
- 新界面只提供添加文本、添加ROI和删除，旧几何项显示为“ROI（旧配置）”。

## 运行规则

1. 配置新版布尔判定时，true使用OK色，false使用NG色，并覆盖文本和新ROI来源颜色。
2. 新ROI未配置布尔判定时保留来源几何颜色；来源颜色为空时使用OK色。
3. 旧线、矩形、区域在未配置新判定时继续使用旧的固定颜色或按源判定颜色。
4. AlgorithmResult的ROI复制全部矩形、线、圆、圆弧、椭圆和轮廓，不复制文本。
5. 每个ROI项每轮只读取一次订阅值；类型和反射提取沿用共享缓存。

## 界面

- 标题继续为“ROI结果绘制”。
- 基础设置包含输入图像、可选布尔颜色判定、OK色和NG色。
- 绘制项按钮仅保留“添加文本”“添加ROI”“删除”。
- 文本项显示手动/订阅、前缀、字号、位置及折叠高级参数。
- ROI项显示订阅与线宽。
- 所有控件保存在 NodeParamFormResultOverlayDraw.Designer.cs。

## 兼容与验证

- 旧方案的线、矩形、区域、名称和每项颜色字段不得删除。
- 新旧参数必须完成Newtonsoft.Json序列化往返检查。
- 自动检查必须明确针对 ResultOverlayDraw，而不是 ResultOverlayDraw2。
- 实际打开 NodeParamFormResultOverlayDraw 截图，确认标题和按钮已经变化。
- Debug|x64 完整编译必须为0个错误。

## 实施约束

- 所有新增类、字段、属性和方法使用简体中文注释。
- 不操作Git。
- 不修改其它节点类型的方案映射。
