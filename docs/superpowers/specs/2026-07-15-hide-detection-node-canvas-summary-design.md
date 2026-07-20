# 隐藏检测节点画布参数摘要设计

## 目标

删除自由画布中“无监督检测”和“大模型调用”节点底部的参数摘要文字，使这两类节点恢复为与其他普通节点一致的默认高度。

## 设计

- 删除 `NodeUnsupervisedDetection` 和 `NodeLargeModelDetection` 对 `GetCanvasParameterSummary()` 的重写。
- 保留 `NodeBase.GetCanvasParameterSummary()`、`ProcessFlowCanvas.DrawNodeParameterSummary()` 和通用高度计算逻辑，避免影响其他节点或后续扩展。
- 两类节点继承基类的空摘要后，画布不会绘制第三行参数，也不会把节点高度提高到 84。
- 参数窗体、参数持久化、推理运行和结果输出保持不变。

## 测试

- 更新无监督节点集成检查，确认节点类不再重写参数摘要。
- 更新大模型集成检查，确认节点类不再重写参数摘要。
- 运行两项专项检查和完整解决方案编译。

