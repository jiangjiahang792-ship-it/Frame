# 轮廓模板匹配 DLL 与 WinForms Demo

界面只保留四个操作按钮：**加载搜索图像、创建模板、涂抹、匹配**。所有控件及布局均在 `MainForm.Designer.cs` 中，可用 Visual Studio 设计器编辑。

画布随窗口大小自动扩展，图像等比例居中完整显示。改变窗口大小时 ROI、涂抹和匹配轮廓同步适配；图像与画布比例不同时保留留白。窗体支持高 DPI，并在首次显示时限制到当前屏幕工作区。

## 操作流程

1. **加载搜索图像**：图像显示在唯一的画布中。
2. **创建模板**：进入框选状态，按住左键拖动 ROI。松开左键只完成框选；在画布内**右键**才调用算法、创建并确认模板。
3. **涂抹**：显示模板实际参与匹配的绿色轮廓特征。按住左键涂抹不需要的细节，被涂中的特征暂时标红。**右键**确认后，这些特征从模型中删除，并更新所有旋转模板及金字塔缓存。
4. **匹配**：使用最后确认的模板，在当前搜索图像中搜索，在每个目标上叠加亮绿色模板轮廓特征，并显示位置、角度、分数、结果框和耗时。轮廓随目标的位置与角度变换，已涂抹删除的特征不会显示。

无需点击额外的 ROI、保留、擦除、清空遮罩或确认按钮。按 **Esc** 可取消尚未确认的框选或涂抹。编辑期间主要按钮暂时禁用，以免误提交。

创建模型后可以再次加载另一张搜索图，模型会保留。之后点击“涂抹”会在同一画布显示模型来源图；点击“匹配”会回到当前搜索图。模板参数在重新建模时生效；涂抹只删除已有特征，不重新提取新的点。删除后至少保留 8 个特征，删除失败不改变原模型。删除只影响模型，不修改原图文件。

## 工程与接口

### Canny 建模

“阈值”旁有 **自动** 复选框，界面默认勾选：左键框选 ROI、右键确认时，DLL 根据有效 ROI 的 Sobel 梯度幅值直方图估算阈值，成功后回填原图层实际值，并在状态栏显示。取消勾选即可手动输入，也可以在最近自动值基础上微调。切换/改值只对下一次建模生效，不修改已确认的模型。

自动估计采用 Otsu 两组分离，再取弱组均值向强组均值的 1/4 分界（4～1024），不是灰度二值化，也不保证每种材质都最优。自动模式粗金字塔层额外限制高阈值不超过搜索阈值的两倍，再按层衰减，防止小轮廓缩小后丢失。手动模式保持原有衰减策略，因此把自动值填入手动模式仅保证相同原图层阈值，并不承诺各粗层特征完全相同。

调用方可设置 `CreateModelOptions.AutoContrast = true`；默认 `false` 保持已有代码的手动行为。`IShapeMatcher.ModelContrast` 查询当前成功模型的实际原图层高阈值（无模型为0）。原生 `sm_create_model` 的 `contrast=0` 表示自动，新增 `sm_get_model_contrast` 查询值。请同步更新 Demo 和 DLL，旧版 DLL 不支持此新增功能。

模板各金字塔层采用 3×3 Sobel 的 16 位导数运行 L2 Canny，再进行遮罩过滤、网格选点和方向归一化。模型对比度为 Canny 高阈值，低阈值固定为高阈值的一半；特征数是上限，边缘不足时不会补入非边缘点。没有新增操作按钮，现有 DLL ABI、右键建模、涂抹删除及轮廓叠加保持不变。

搜索端有意保留方向梯度响应和亚像素精修，不将搜索图限制为一像素 Canny 边缘。本项目对照实验中，双侧 Canny 在旋转/噪声场景出现漏检；Canny 模板配合梯度搜索通过同组检查。Canny 不等于自动实现工业级精度，速度和误检仍取决于 ROI、角度范围、阈值和场景。

对于 `多目检测/方形` 素材，建议最低分数 **0.85**、模型对比度 **20**、搜索最小对比度 **10**；需更准的小目标角度时使用“高精度”。0.65 的宽松阈值可能接受细线空心框，不建议据此统计该组目标。

### 结构一致性验证

为避免三点模板把五点目标当作高分匹配，最终分数不再仅表示模板边缘的正向覆盖：在模板轮廓凸包内部，同时校验多余边缘和缺失细节；对面积接近的重复闭环，进一步验证数量及位置的一对一对应。这是通用轮廓结构校验，不读取骰子点数标签，也不使用骰子分类器。复杂、不稳定闭环不启用数量约束。

正向模型的 Canny 阈值仍遵守自动/手动设置。独立的内部结构校验采用不低于自动估计值的强边缘阈值，避免低手动阈值引入背景纹理稀释结构惩罚。搜索候选仍来自梯度方向金字塔；搜索 Canny 仅用于验证，不取代候选搜索。涂抹区同步退出双向校验；确认涂抹后禁用完整闭环数量约束，避免已删细节继续参与判定。

粗层保留必要的细节和角度假设；方向响应按行并行，OpenCV 静态库内部采用最多4线程预算。没有增加界面操作按钮或改变 C ABI 布局。分数语义比旧版严格，遮挡/大幅变形可能降低得分；当前不支持独立的尺度或透视搜索。

骰子原图及亮度噪声交叉验证（固定0.65门限、±180°、2°步长、300特征）：

```powershell
dotnet .\managed\ShapeMatch.Demo\bin\Release\net8.0-windows\ShapeMatch.Demo.dll --dice-benchmark "C:\Users\admin\Desktop\素材" artifacts/dice-verified.json
dotnet .\managed\ShapeMatch.Demo\bin\Release\net8.0-windows\ShapeMatch.Demo.dll --dice-robustness "C:\Users\admin\Desktop\素材" artifacts/dice-robust-verified.json
```

每个入口测试4种点数模板×2种阈值模式×7张图，共56项；预热一次、重复5次，保存中位数、最大值、误检/漏检和Demo真实画布叠加图。任何误检/漏检均以非零退出，不弹窗阻塞。详见《骰子匹配测试报告.md》。

- `CShapeMatchCV(1).cpp`：方向梯度轮廓匹配核心。
- `native/include/ShapeMatchNative.h`：DLL 的 C ABI；新增 `sm_get_features`、`sm_erase_features`。
- `managed/ShapeMatch.Demo/Interop/IShapeMatcher.cs`：C# 算法服务接口。
- `managed/ShapeMatch.Demo/Views/MainForm.Designer.cs`：四按钮、单画布、参数和结果表的设计器布局。
- `managed/ShapeMatch.Demo/Controls/ImageCanvas.cs`：框选、轮廓预览、连续涂抹及右键确认。
- `任务记录.md`：修改和验证记录。

DLL 使用同步调用和调用方拥有的缓冲区，不跨 C ABI 传递 C++ 对象。支持灰度、BGR、BGRA 图像。单个匹配器实例应串行调用；WinForms 在后台线程执行算法，并在执行期间锁定交互。编辑后关闭灰度 ECC 精修，防止已删细节通过整图灰度重新影响匹配。

## 构建与验证

需要 Windows x64、Visual Studio C++ 工具、Windows SDK、.NET 8 或兼容 SDK，以及静态 OpenCV。统一脚本通过 vcpkg 准备依赖：

```powershell
.\build.ps1 -Configuration Release -VcpkgRoot D:\Tools\shape-match-vcpkg -VisualStudioRoot D:\Tools\vs2022
```

添加 `-Run` 可在完成后启动程序。vcpkg 和 Visual Studio 工具路径需使用 ASCII 路径；VS 安装在中文目录时，`-VisualStudioRoot` 可指定指向该安装目录的英文目录联接。

输出程序：`managed/ShapeMatch.Demo/bin/Release/net8.0-windows/ShapeMatch.Demo.exe`。原生 DLL 会自动复制到同一目录。

构建脚本运行原生 CTest 和 C# 烟雾测试。托管测试验证真实特征删除、全删失败保留模型、删除后匹配、左右键提交语义以及四按钮单画布结构；同时生成 `smoke-layout.png` 供布局检查。可以单独运行：

```powershell
dotnet .\managed\ShapeMatch.Demo\bin\Release\net8.0-windows\ShapeMatch.Demo.dll --smoke-test
```

可重复素材基准（真实文件只读；结果及叠加图写入 artifacts）：

```powershell
dotnet .\managed\ShapeMatch.Demo\bin\Release\net8.0-windows\ShapeMatch.Demo.dll --benchmark "D:\Visonpro\素材" artifacts/benchmark-canny.json
```

基准采用固定 ROI、±30°/1° 建模角度、300 特征上限，测试已知旋转、亚像素平移、亮度变为 65%、±5 灰度随机噪声及无目标图。预热一次后重复七次，只测 Find，不包含磁盘读取、图像变换及绘图。P95 为七样本的最近秩估计（即最大值），不代表长时间工业产线尾延迟。真实方框额外按人工核对的数量做 0.85 阈值回归，失败会返回非零退出码；数量检查不等同于逐目标完整标注评测。
