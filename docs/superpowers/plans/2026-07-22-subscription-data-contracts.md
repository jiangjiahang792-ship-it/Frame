# 订阅结果数据分类与输入输出契约 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立统一的订阅数据分类、数值兼容和输入输出校验，减少无关候选结果，并保证 Modbus `short` 等基础数值可以被数值型节点直接订阅。

**Architecture:** 现有 `[DisplayName]` 继续作为输出是否公开的显式入口，每个公开属性再通过 `SubscriptionOutputAttribute` 得到完整端口描述；简单 CLR 类型由统一解析器分类，轮廓、区域、位姿等歧义类型显式指定类别。`SubscriptionPortCatalog` 同时汇总静态属性和动态变量，所有订阅控件和自定义结果树都通过 `SubscriptionInputContract` 与同一个兼容服务查询候选项。

**Tech Stack:** C#、.NET Framework WinForms、PowerShell 结构/行为测试、MSBuild x64。

## Global Constraints

- 所有中文内容使用简体中文，新增属性、字段、方法、类、接口均添加 XML 注释。
- WinForms 新增控件必须写入 `.Designer.cs`，控件 `Text` 使用中文。
- 反射结果必须缓存；每帧运行不得重新扫描程序集或全部结果属性。
- 不改变现有节点名、`DisplayName`、动态变量路径和方案订阅序列化文本。
- 隐藏结果只禁止新选，旧方案原订阅继续解析；不存在的动态变量不得自动回退到第一项。
- 本任务按用户要求在当前工作区串行实施，不创建工作树，不执行 Git 提交、暂存、分支或推送。

---

## File Structure

新建文件：

- `Node/SubscriptionPortContracts.cs`：数据类别、数量形态、显示级别、输出特性、端口描述符和输入契约。
- `Node/SubscriptionTypeCompatibility.cs`：类型分类、数值兼容矩阵和统一值转换。
- `Node/SubscriptionPortCatalog.cs`：缓存静态输出，合并动态变量并按输入契约查询。
- `Tests/SubscriptionTypeCompatibility.Tests.ps1`：基础类型和 `short` 数值直订行为测试。
- `Tests/SubscriptionPortCatalog.Tests.ps1`：输出分类、可见性、动态变量和旧订阅行为测试。
- `Tests/SubscriptionConsumerContracts.Tests.ps1`：普通订阅及四则运算、多条件、组合模块的统一目录使用检查。
- `Tests/ModbusNumericSubscription.Tests.ps1`：Modbus `short` 动态输出和写入订阅回归测试。

主要修改文件：

- `TDJS-Vision.csproj`：显式加入三个新 C# 文件。
- `Node/SubscriptionValueTypeFilter.cs`：改为兼容服务的旧 API 门面。
- `Node/DynamicResultVariable.cs`：输出动态端口描述符，而不只返回变量名和 `Type`。
- `Node/NodeSubscription.cs`：按输入契约查询、兼容数值转换、保留失效旧路径。
- `Node/NodeSubscription.Designer.cs`：为结果下拉框加入“显示高级结果”右键勾选项，不改变控件高度。
- 43 个带 `[DisplayName]` 的结果声明文件：为当前 230 个输出补齐输出特性、隐藏级别和歧义类别。
- 固定类型订阅参数窗体：声明图像、算法结果、位置修正、布尔、数值等输入契约。
- `Node/6-LogicTool/ArithmeticOperation/NodeParamFormArithmeticOperation.cs`、`Node/6-LogicTool/MultiCondition/NodeParamFormMultiCondition.cs`、`Node/6-LogicTool/CompositeModule/NodeParamFormCompositeModule.cs`、`NodeParamFormCompositeInput.cs`、`NodeParamFormCompositeOutput.cs`：共用统一端口目录。
- `Node/5-EquipmentCommunication/ModbusWrite/ParamFormModbusWrite.cs`：按所选寄存器类型接受并格式化订阅值。
- `Tests/PositionCorrectionSubscriptionCompatibility.Tests.ps1`：删除“缺失结果自动选第一项”的旧断言。
- `任务记录.md`：记录本次分类、兼容规则和验证结果。

---

### Task 1: 建立数据类别与数值兼容核心

**Files:**
- Create: `Node/SubscriptionPortContracts.cs`
- Create: `Node/SubscriptionTypeCompatibility.cs`
- Modify: `Node/SubscriptionValueTypeFilter.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/SubscriptionTypeCompatibility.Tests.ps1`

**Interfaces:**
- Produces: `SubscriptionDataCategory`、`SubscriptionValueMultiplicity`、`SubscriptionOutputVisibility`、`NumericConversionMode`、`SubscriptionOutputAttribute`、`SubscriptionOutputDescriptor`、`SubscriptionInputContract`。
- Produces: `SubscriptionTypeCompatibility.ResolveCategory(Type)`、`IsNumericType(Type)`、`CanAssign(Type, Type, NumericConversionMode)`、`ConvertValue(object, Type, NumericConversionMode)`。
- Preserves: `SubscriptionValueTypeFilter.GetDisplayProperties(Type, Type)` and `IsCompatible(Type, Type)` for existing callers.

- [ ] **Step 1: Write the failing numeric compatibility test**

在 `Tests/SubscriptionTypeCompatibility.Tests.ps1` 中编译两个新核心文件，并明确断言：

```powershell
Assert-True ([TDJS_Vision.Node.SubscriptionTypeCompatibility]::ResolveCategory([short]) -eq [TDJS_Vision.Node.SubscriptionDataCategory]::Number) 'short 必须归类为数值。'
Assert-True ([TDJS_Vision.Node.SubscriptionTypeCompatibility]::ResolveCategory([bool]) -eq [TDJS_Vision.Node.SubscriptionDataCategory]::Boolean) '判定值本质必须归类为布尔。'
Assert-True ([TDJS_Vision.Node.SubscriptionTypeCompatibility]::CanAssign([short], [double], [TDJS_Vision.Node.NumericConversionMode]::SafeWidening)) 'short 必须允许直接订阅到 double 数值输入。'
Assert-True (-not [TDJS_Vision.Node.SubscriptionTypeCompatibility]::CanAssign([short], [bool], [TDJS_Vision.Node.NumericConversionMode]::SafeWidening)) '数值不能默认当作布尔。'
$converted = [TDJS_Vision.Node.SubscriptionTypeCompatibility]::ConvertValue([short]123, [double], [TDJS_Vision.Node.NumericConversionMode]::SafeWidening)
Assert-True ($converted.GetType() -eq [double] -and $converted -eq 123.0) 'short 转 double 必须保留数值。'
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '.\Tests\SubscriptionTypeCompatibility.Tests.ps1')))"
```

Expected: FAIL because `SubscriptionTypeCompatibility` and the contract types do not exist.

- [ ] **Step 3: Implement the contract model**

`SubscriptionPortContracts.cs` 必须定义以下公开形状；枚举值不能把“判定”独立于 `Boolean`：

```csharp
public enum SubscriptionDataCategory
{
    Unknown,
    Boolean,
    Number,
    Text,
    Image,
    Point,
    PointCollection,
    Contour,
    Line,
    Circle,
    Ellipse,
    Rectangle,
    Region,
    Pose,
    PositionCorrection,
    AlgorithmResult,
    MeasurementResult,
    StructuredObject
}

public enum SubscriptionValueMultiplicity { Single, Collection, MultiTarget }
public enum SubscriptionOutputVisibility { Core, Advanced, Hidden }
public enum NumericConversionMode { None, SafeWidening, Checked }
```

`SubscriptionInputContract` 提供以下工厂方法，集合在构造时复制成只读内容：

```csharp
public static SubscriptionInputContract ForType(Type expectedType);
public static SubscriptionInputContract ForCategories(
    IEnumerable<SubscriptionDataCategory> categories,
    NumericConversionMode numericConversionMode = NumericConversionMode.SafeWidening);
public static SubscriptionInputContract AnyVisible();
public bool Accepts(SubscriptionOutputDescriptor output, out string reason);
```

- [ ] **Step 4: Implement the explicit numeric widening matrix**

`SubscriptionTypeCompatibility` 先剥离 `Nullable<T>`，再使用显式数值集合和扩宽表；至少覆盖：

```csharp
short -> int, long, float, double, decimal
ushort -> int, uint, long, ulong, float, double, decimal
int -> long, float, double, decimal
uint -> long, ulong, float, double, decimal
float -> double
```

同类型直接允许；`NumericConversionMode.Checked` 可使用 `Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)`，但必须捕获 `OverflowException`、`FormatException`、`InvalidCastException` 并改抛包含实际类型和目标类型的中文 `InvalidCastException`。`SafeWidening` 不允许未登记的缩窄转换。

- [ ] **Step 5: Point the legacy filter at the compatibility service**

把 `SubscriptionValueTypeFilter.IsCompatible` 改为：

```csharp
return SubscriptionTypeCompatibility.CanAssign(
    valueType,
    expectedValueType,
    NumericConversionMode.SafeWidening);
```

因此现有调用方即使尚未迁移输入契约，也不会再把 `short` 排除在 `double` 数值输入之外。

- [ ] **Step 6: Register files and verify GREEN**

在 `TDJS-Vision.csproj` 的 `Node` 编译项附近加入三个新文件中的本任务两个文件，然后运行本任务测试。Expected: PASS and output `Subscription type compatibility checks passed.`

---

### Task 2: 建立静态与动态统一端口目录

**Files:**
- Create: `Node/SubscriptionPortCatalog.cs`
- Modify: `Node/DynamicResultVariable.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/SubscriptionPortCatalog.Tests.ps1`

**Interfaces:**
- Consumes: Task 1 contract and compatibility types.
- Produces: `SubscriptionPortCatalog.GetStaticOutputs(Type)` and `GetOutputs(NodeBase, SubscriptionInputContract, bool, string)`.
- Produces: `DynamicResultVariableResolver.GetDescriptors(NodeBase)`.

- [ ] **Step 1: Write failing catalog tests**

测试夹具包含：核心 `short`、核心 `bool`、高级图像替身、隐藏诊断文本和同为 `List<PointF>` 替身的点集/轮廓属性。断言：

```powershell
$numberOutputs = [SubscriptionPortCatalog]::GetStaticOutputs([CatalogFixtureResult]) |
    Where-Object { $_.Category -eq [SubscriptionDataCategory]::Number }
Assert-True ($numberOutputs.Count -eq 1 -and $numberOutputs[0].ValueType -eq [short]) '目录必须保留 short 实际类型。'
Assert-True (($allOutputs | Where-Object DisplayName -eq '诊断信息')[0].Visibility -eq [SubscriptionOutputVisibility]::Hidden) '隐藏输出必须保留描述符但不能用于新选。'
Assert-True (($allOutputs | Where-Object DisplayName -eq '边缘点')[0].Category -eq [SubscriptionDataCategory]::PointCollection) '歧义集合必须采用显式类别。'
```

- [ ] **Step 2: Run the catalog test and verify RED**

Expected: FAIL because `SubscriptionPortCatalog` does not exist.

- [ ] **Step 3: Implement cached static discovery**

`SubscriptionPortCatalog` 使用：

```csharp
private static readonly ConcurrentDictionary<Type, IReadOnlyList<SubscriptionOutputDescriptor>> StaticCache;
```

只扫描 `CanRead` 且具有 `[DisplayName]` 和 `[SubscriptionOutput]` 的属性。`SubscriptionOutputAttribute.Category == Unknown` 时调用 `ResolveCategory(property.PropertyType)`；缺少特性的 `[DisplayName]` 属性不进入新订阅候选，但测试仍能报告它，便于完成全量迁移。

- [ ] **Step 4: Merge dynamic descriptors**

为 `DynamicResultVariableResolver` 新增：

```csharp
public static IReadOnlyList<SubscriptionOutputDescriptor> GetDescriptors(NodeBase node);
```

每个描述符使用 `$variable:` 路径、`变量.` 显示前缀、真实 `GetVariableValueType` 和 `ResolveCategory`。Modbus `short` 动态变量得到 `Category=Number`、`ValueType=typeof(short)`、`Visibility=Core`，不能归类成通信对象。

- [ ] **Step 5: Implement query behavior**

`GetOutputs` 依次执行：作用域来源、输入契约兼容、显示级别过滤。`includeAdvanced=false` 时仍自动显示“输入契约只接受的高级类别”，例如图像或位置修正；`Hidden` 永不作为新候选。`selectedPath` 指向隐藏旧属性时，只追加当前一个 `IsLegacySelection=true` 描述符；指向不存在的动态变量时返回一个 `IsMissing=true` 描述符，不能返回第一项替代它。

- [ ] **Step 6: Run catalog tests and verify GREEN**

Expected: PASS and output `Subscription port catalog checks passed.`

---

### Task 3: 为现有输出完成显式分类和显示分层

**Files:**
- Modify: all 43 files returned by `rg -l --glob "*.cs" "\[DisplayName\(" Node`
- Test: `Tests/SubscriptionPortCatalog.Tests.ps1`
- Reference: `docs/订阅节点结果审计与保留建议-2026-07-22.md`

**Interfaces:**
- Consumes: `SubscriptionOutputAttribute` from Task 1.
- Produces: every current `[DisplayName]` property has exactly one output declaration.

- [ ] **Step 1: Add the failing completeness assertion**

测试扫描所有 C# 文件，逐个检查 `[DisplayName(...)]` 后、属性声明前存在 `[SubscriptionOutput...]`。Expected initial result: FAIL and report all unclassified property paths.

- [ ] **Step 2: Add `[SubscriptionOutput]` to every current output**

简单属性直接写：

```csharp
[SubscriptionOutput]
[DisplayName("匹配数量")]
public int MatchCount { get; set; }
```

`bool` 自动分类为 `Boolean`，所有整数和浮点类型自动分类为 `Number`，`string` 自动分类为 `Text`，不依据节点来源改变类别。

- [ ] **Step 3: Mark ambiguous visual and multi-target outputs explicitly**

至少使用以下模式：

```csharp
[SubscriptionOutput(SubscriptionDataCategory.PointCollection, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
[DisplayName("边缘点集合")]
public List<PointF> EdgePoints { get; set; }

[SubscriptionOutput(SubscriptionDataCategory.Contour, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
[DisplayName("轮廓集合")]
public List<List<PointF>> Contours { get; set; }

[SubscriptionOutput(SubscriptionDataCategory.Pose, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
[DisplayName("匹配位姿列表")]
public List<TemplateMatchPose> Poses { get; set; }

[SubscriptionOutput(SubscriptionDataCategory.PositionCorrection, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
[DisplayName("位置修正信息列表")]
public List<PositionCorrectionInfo> Items { get; set; }
```

测量工具的 `Items` 统一归类 `MeasurementResult + MultiTarget + Advanced`；`OutputImage` 为 `Image + Single + Advanced`；`AlgorithmResult` 为 `AlgorithmResult + Single + Advanced`；PLC、Modbus 批量对象和共享变量包装对象为 `StructuredObject + Advanced`。

- [ ] **Step 4: Mark the exact hidden result set**

按照审计文档隐藏 46 项，代码属性集合固定为：

- `ImagePreprocess.ModeName`；`ModbusRead.ValueCount`；`ResultOverlayDraw2.RuleDiagnostics`。
- 八类多目标测量及位置修正中的重复原始 `IsOk`，所有测量 `AlgorithmMs`，`FindPoint.Message`。
- `LineMergeFit.Message`、`LineMergeFit.AlgorithmMs`。
- `MultiCondition.Details`、`MultiCondition.DiagnosticsText`。
- `ArithmeticOperation.IsOk`、`ValueText`、`ExpressionText`、`OperandCount`、`OperationCount`、`DefaultVariableName`、`Message`、`Variables`、`VariablesText`。
- `CompositeInput.Message`、`Values`、`ValuesText`；`CompositeOutput.Message`、`Values`、`ValuesText`。
- `CompositeModule.ModuleName`、`InternalRunTime`、`InternalNodeCount`、`Message`、`OutputValues`、`OutputValuesText`。

每项使用：

```csharp
[SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
```

- [ ] **Step 5: Mark advanced scalar details**

把 `LineLineAngle` 的八个 `Line1*/Line2*` 端点坐标、`PointLineDistance` 的四个直线端点坐标、`PointRegionDistance.RegionPointCount` 标记为 `Visibility=Advanced`。其余核心布尔、数值和文本按审计文档默认显示。

- [ ] **Step 6: Run completeness and count checks**

Expected: 230 static outputs classified; hidden total remains 46; no `[DisplayName]` property lacks an output declaration. Core/advanced totals以当前新增属性的实际分类重新计算并写入任务记录。

---

### Task 4: 改造普通 NodeSubscription 和高级结果界面

**Files:**
- Modify: `Node/NodeSubscription.cs`
- Modify: `Node/NodeSubscription.Designer.cs`
- Modify: `Tests/PositionCorrectionSubscriptionCompatibility.Tests.ps1`
- Test: `Tests/SubscriptionConsumerContracts.Tests.ps1`

**Interfaces:**
- Consumes: `SubscriptionPortCatalog`, `SubscriptionInputContract`, `SubscriptionTypeCompatibility`.
- Produces: `SetInputContract(SubscriptionInputContract)` while preserving `SetExpectedValueType<T>()`.

- [ ] **Step 1: Write failing source and behavior checks**

断言 `NodeSubscription`：

```text
使用 SubscriptionPortCatalog.GetOutputs
使用 SubscriptionTypeCompatibility.ConvertValue
包含 SetInputContract
旧结果缺失时不执行 SelectedIndex = 0 回退
Designer 中存在 toolStripMenuItemShowAdvancedResults 且 Text 为“显示高级结果”
```

同时把旧测试中的 `comboBox2.SelectedIndex = index1 == -1 ? 0 : index1;` 正向断言改为反向断言。

- [ ] **Step 2: Add the input contract API**

`SetExpectedValueType(Type)` 改为构造 `SubscriptionInputContract.ForType(expectedValueType)`；新增：

```csharp
public void SetInputContract(SubscriptionInputContract inputContract)
{
    _inputContract = inputContract ?? SubscriptionInputContract.AnyVisible();
    if (_selectedNode != null)
        InitProperties(_selectedNode, _text2);
}
```

- [ ] **Step 3: Replace property and dynamic loops with catalog descriptors**

`InitProperties` 只遍历目录返回项；下拉显示保持原 `DisplayName`/`变量.` 文本以兼容序列化。兼容旧属性在文字后显示“（兼容订阅）”，内部仍保存原 `_text2`；不存在项显示“（结果不存在）”。刷新时如果保存文本找不到，保持该文本和缺失标记，不能选第一结果。

- [ ] **Step 4: Use unified runtime conversion**

删除本地任意 `Convert.ChangeType` 逻辑，改为：

```csharp
return (T)SubscriptionTypeCompatibility.ConvertValue(
    value,
    typeof(T),
    _inputContract.NumericConversionMode);
```

这样 `GetValue<double>()` 能读取 `short`，但 `GetValue<bool>()` 不会默认接受任意数字。

- [ ] **Step 5: Add the Designer-owned advanced result menu**

在 `NodeSubscription.Designer.cs` 中新增 `ContextMenuStrip` 和可勾选的 `toolStripMenuItemShowAdvancedResults`，把菜单绑定到 `comboBox2`，`Text = "显示高级结果"`、`CheckOnClick = true`。勾选事件只刷新当前结果列表并保留 `_text2`，不能改变节点订阅，也不改变 `NodeSubscription` 的现有尺寸。

- [ ] **Step 6: Run NodeSubscription tests**

Expected: position correction list still filters correctly; missing selection is preserved; advanced result menu is Designer-owned; numeric compatibility is delegated to the shared service.

---

### Task 5: 给固定输入控件补齐契约，并修复 Modbus 数值订阅

**Files:**
- Modify: fixed-type parameter forms containing `NodeSubscription.Init(node)`
- Modify: `Node/5-EquipmentCommunication/ModbusWrite/ParamFormModbusWrite.cs`
- Test: `Tests/ModbusNumericSubscription.Tests.ps1`
- Test: `Tests/SubscriptionConsumerContracts.Tests.ps1`

**Interfaces:**
- Consumes: `SetExpectedValueType<T>()` and `SetInputContract(...)`.
- Produces: every visible result selector declares a type/category contract before `Init(node)`.

- [ ] **Step 1: Write a failing consumer coverage test**

扫描所有 `NodeSubscription` 使用点，凡结果下拉框可见且后续调用固定 `GetValue<T>`，必须在同一控件 `Init(node)` 之前调用 `SetExpectedValueType<T>` 或 `SetInputContract`。`HideText2()` 的节点级来源选择器排除在结果属性契约之外。

- [ ] **Step 2: Add fixed image and structured-result contracts**

所有调用 `GetValue<OutputImage>()` 的控件在 `Init(node)` 前执行：

```csharp
nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
```

实际控件名为 `nodeSubscription1` 等时使用原字段名。`AlgorithmResult`、`List<TemplateMatchPose>`、`List<PositionCorrectionInfo>`、`Camera3DFrameData`、`string`、`bool` 等固定输入按实际 `GetValue<T>` 添加对应声明。

- [ ] **Step 3: Give multi-type logic controls category contracts**

`ConditionRun`、`If`、`ProcessTrigger`、`MessageBox`、`CameraIO`、`ERUIIO` 根据现有运行分支接受 `Boolean`、`AlgorithmResult` 和必要的 `StructuredObject`，不显示图像、轮廓和测量集合。`SharedVariable`、Excel 导出等确实接受任意对象的控件使用 `AnyVisible()`，但默认不显示 Hidden。

- [ ] **Step 4: Write the failing Modbus short regression test**

测试 `NodeModbusRead.TryGetDynamicResultVariableType` 对 `RegistersType.Short` 返回 `typeof(short)`，目录把变量列为 `Number`；测试 `ParamFormModbusWrite` 不再固定调用 `GetValue<bool>()`，而是按所选 `RegistersType` 接受布尔或数值。

- [ ] **Step 5: Fix Modbus write subscription formatting**

把 `GetSubValue()` 改为读取 `object` 后按 `NodeParamModbusWrite.DataType` 转换和格式化：布尔输出 `1/0`；整数使用 `InvariantCulture`；浮点使用 `InvariantCulture`；数组逐项格式化并以逗号连接。`comboBoxType_SelectedIndexChanged` 根据所选类型刷新输入契约：`Bool/线圈` 只接受 `Boolean`，其余寄存器类型接受 `Number` 并使用 `NumericConversionMode.Checked`。

- [ ] **Step 6: Run consumer and Modbus tests**

Expected: `short` 动态值能出现在数值输入中，写入 Short 时保留 `123` 而不是被转换成 `1`；布尔输入仍不接受任意数值。

---

### Task 6: 统一四则运算、多条件和组合模块的候选目录

**Files:**
- Modify: `Node/6-LogicTool/ArithmeticOperation/NodeParamFormArithmeticOperation.cs`
- Modify: `Node/6-LogicTool/MultiCondition/NodeParamFormMultiCondition.cs`
- Modify: `Node/6-LogicTool/CompositeModule/NodeParamFormCompositeModule.cs`
- Modify: `Node/6-LogicTool/CompositeModule/NodeParamFormCompositeInput.cs`
- Modify: `Node/6-LogicTool/CompositeModule/NodeParamFormCompositeOutput.cs`
- Test: `Tests/SubscriptionConsumerContracts.Tests.ps1`

**Interfaces:**
- Consumes: `SubscriptionPortCatalog.GetOutputs`.
- Produces: no custom selector performs independent recursive reflection for new candidates.

- [ ] **Step 1: Add failing checks for duplicated reflection**

测试要求五个窗体使用 `SubscriptionPortCatalog`；四则运算和多条件的候选构建不得再从结果对象递归两层扫描全部可读成员。

- [ ] **Step 2: Migrate arithmetic candidates**

`RefreshSourceTree` 对每个上游节点调用目录，输入契约为 `Number + Single + SafeWidening`。因此 Modbus `short`、测量 `double?`、四则运算动态变量都直接出现；图像、布尔、文本、耗时和结构化对象不出现。旧运算行已经保存的嵌套路径仍由现有运行解析器读取，但不再作为新候选扩散。

- [ ] **Step 3: Migrate multi-condition candidates**

输入契约只接受 `Boolean`、`Number`、`Text` 的单值核心结果和动态变量。操作符仍根据描述符 `ValueType` 选择，`bool` 使用真假操作，数值使用比较/范围，文本使用包含/相等。

- [ ] **Step 4: Migrate composite bindings**

组合模块每个目标输入端口从 `ValueTypeName` 构造 `SubscriptionInputContract.ForType`；只列出可赋值或允许安全数值转换的上游输出。组合输入、输出和模块动态端口均通过统一目录发布，旧端口没有分类时按 CLR 类型推断并标记兼容。

- [ ] **Step 5: Preserve live refresh**

继续监听 `NodeBase.OutputDefinitionChanged`，刷新时按保存路径重选；变量删除后显示“结果不存在”，不能恢复到上一轮运行结果或第一候选项。

- [ ] **Step 6: Run custom-consumer tests**

Expected: all custom selectors use the catalog; arithmetic sees Modbus `short`; multi-condition sees bool/number/text only; composite binding uses the declared target type.

---

### Task 7: 全量回归、性能检查和任务记录

**Files:**
- Modify: `Tests/DynamicOutputImmediateRefresh.Tests.ps1`
- Modify: `Tests/PositionCorrectionSubscriptionCompatibility.Tests.ps1`
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: all prior tasks.
- Produces: verified x64 build and regression evidence.

- [ ] **Step 1: Run focused tests**

```powershell
$tests = @(
  '.\Tests\SubscriptionTypeCompatibility.Tests.ps1',
  '.\Tests\SubscriptionPortCatalog.Tests.ps1',
  '.\Tests\SubscriptionConsumerContracts.Tests.ps1',
  '.\Tests\ModbusNumericSubscription.Tests.ps1',
  '.\Tests\DynamicOutputImmediateRefresh.Tests.ps1',
  '.\Tests\PositionCorrectionSubscriptionCompatibility.Tests.ps1'
)
foreach ($test in $tests) {
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '$test')))"
  if ($LASTEXITCODE -ne 0) { throw "测试失败：$test" }
}
```

Expected: all scripts exit 0.

- [ ] **Step 2: Run classification integrity checks**

确认 230 个静态输出全部有 `SubscriptionOutput`，隐藏 46；核心/高级按最终目录统计；动态变量均有真实类型和数据类别；没有 `TODO`、`TBD` 或“结果缺失自动选第一项”的实现。

- [ ] **Step 3: Build x64 Debug**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: `0 Error(s)`; existing unrelated warnings are recorded but do not mask new warnings in modified files.

- [ ] **Step 4: Perform manual scenario checks**

1. Modbus 读取配置为 Short，数量 1；四则运算能够直接选择 `变量.值01(...)` 并计算。
2. 多条件能够直接比较该 Short 值，不出现“订阅值类型不匹配”。
3. 图像输入只显示图像结果，不显示数值、判定和诊断信息。
4. 卡尺位置修正输入只显示多目标位置修正结果。
5. 删除四则运算动态输出后，下游显示“结果不存在”，单次运行前后都不跳到其他结果。
6. 打开“显示高级结果”可看到兼容的高级结果；隐藏诊断结果只在旧方案原订阅时显示“兼容订阅”。

- [ ] **Step 5: Update task record**

在 `任务记录.md` 追加日期、改动范围、`bool`/数值分类规则、Modbus `short` 直订示例、旧方案兼容行为、测试脚本和 MSBuild 结果。明确记录“本任务未执行任何 Git 操作”。

- [ ] **Step 6: Final verification**

重新运行四个新增测试与 x64 构建，依据本轮最新输出交付，不引用较早一次结果。
