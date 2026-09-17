# Modbus 字符串读取

在 Modbus 读取节点中将数据类型选择为“字符串”，设置起始地址、寄存器个数、字符串编码及寄存器内字节顺序。

- 一次读取 1～125 个连续保持寄存器，每个寄存器包含两个字节。例如 16 个寄存器最多保存 32 字节，中文占用字节数取决于编码。
- 编码支持 ASCII、UTF-8、GB18030、UTF-16 小端；寄存器内可选择高字节在前或低字节在前。按 PLC 的实际存储格式选择。
- 在零终止符处结束，忽略后续填充，保留有效空格；UTF-16 按双字节边界识别终止符。编码不符、字符截断或返回寄存器不完整时报告失败。
- 下游订阅一个完整字符串，例如地址 900 对应“变量.值01(地址900)”，可用于保存图片节点的条码输入。
- 旧方案没有编码配置时默认为 ASCII、高字节在前；已有 StringEncodingName、StringLowByteFirst 配置按原值恢复，原数字类型序号不变。
- 每次读取开始清空上一轮结果，防止失败或取消后使用旧条码。自动运行只读取并发布结果；点击执行才更新窗体结果列表。

## 实现与验证

通过 ModbusStringReader 扩展方法调用现有 IModbus.ReadUInt16，不增加设备接口成员，也不替换 TCP、RTU 驱动。

Release/x64 编译通过；字符串专项 41 项检查、数值订阅及直接写入回归通过。123 新版方案的 154 个节点参数、4 个字符串读取节点和 8 个存图条码引用完成离线验证。未连接现场 PLC。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/ModbusStringRead.Tests.ps1 -BuildDirectory bin/x64/Release -SolutionPath outputs/123_新版轮廓模板匹配.Sol
```
