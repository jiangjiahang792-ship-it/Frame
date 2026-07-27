# AI训练样本分类与目录追加 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为无监督和大模型训练窗口增加默认类别、结构化目录分类、整批类别选择和 OK/NG 样本目录追加，并保证默认图片永不进入训练数据集。

**Architecture:** 新增纯路径级 `ITrainingSampleDirectoryClassifier`，统一处理标准目录识别、强制分类和稳定扫描；两个现有图片加载器只负责把扫描结果转成各自图片项与缩略图。新增可复用的 WinForms 类别选择窗体；两个训练窗体保留各自 UI 状态，两个训练服务采用 OK/NG 白名单构建数据集。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、System.Drawing、PowerShell 5.1 回归脚本、Visual Studio 2022 MSBuild。

## Global Constraints

- 所有新增中文必须使用简体中文。
- 所有新增 WinForms 控件必须声明并布局在对应 `.Designer.cs` 文件中，保证设计器可见。
- 所有新增类型、属性、字段、方法和接口必须添加中文 XML 注释；关键分类和训练排除逻辑添加中文说明。
- 目录扫描和缩略图生成不得阻塞 UI 线程；追加目录不得重新生成已有缩略图。
- 标准类别目录只识别所选主目录的直属 `OK`、`NG` 子目录，名称比较不区分大小写；它们内部继续递归扫描。
- 结构化目录中的其他图片归入默认；默认图片可显示和预览，但不得复制、裁剪或计入训练数量。
- 没有标准目录时，用户必须选择整批 OK、整批 NG 或取消；本次软件运行内恢复路径时同时恢复该选择。
- 追加目录按按钮强制归入 OK 或 NG；不清空已有样本、不改变主路径、不移动源文件、不进行重复过滤。
- 修改既有脏工作区时只触碰本计划明确列出的文件，提交时必须按明确文件路径暂存，禁止带入无关改动。

---

### Task 1: 建立可测试的目录分类核心

**Files:**
- Create: `Forms/AiTrainForm/ITrainingSampleDirectoryClassifier.cs`
- Create: `Forms/AiTrainForm/TrainingSampleCategory.cs`
- Create: `Forms/AiTrainForm/TrainingSampleDirectoryEntry.cs`
- Create: `Forms/AiTrainForm/TrainingSampleDirectoryClassifier.cs`
- Create: `Tests/AiTrainingSampleDirectoryClassifier.Tests.ps1`
- Modify: `TDJS-Vision.csproj`

**Interfaces:**
- Produces: `bool ITrainingSampleDirectoryClassifier.HasStandardCategoryDirectories(string rootFolder)`
- Produces: `IReadOnlyList<TrainingSampleDirectoryEntry> ITrainingSampleDirectoryClassifier.Scan(string rootFolder, TrainingSampleCategory? forcedCategory)`
- Produces: `TrainingSampleCategory` with `Default`, `OK`, `NG`
- Produces: immutable `TrainingSampleDirectoryEntry.FilePath` and `.Category`

- [ ] **Step 1: 写目录分类红灯测试**

创建 `Tests/AiTrainingSampleDirectoryClassifier.Tests.ps1`，编译四个纯 C# 文件并构造临时目录，覆盖结构化目录、单标准目录、无标准目录强制分类及稳定排序：

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sources = @(
    'Forms\AiTrainForm\TrainingSampleCategory.cs',
    'Forms\AiTrainForm\TrainingSampleDirectoryEntry.cs',
    'Forms\AiTrainForm\ITrainingSampleDirectoryClassifier.cs',
    'Forms\AiTrainForm\TrainingSampleDirectoryClassifier.cs'
) | ForEach-Object { Join-Path $root $_ }

foreach ($source in $sources) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "缺少目录分类源码：$source"
    }
}

$types = @(Add-Type -Path $sources -PassThru -WarningAction SilentlyContinue)
$classifierType = $types | Where-Object FullName -eq 'TDJS_Vision.Forms.AiTrainForm.TrainingSampleDirectoryClassifier' | Select-Object -First 1
$classifier = [Activator]::CreateInstance($classifierType)
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('tdjs-training-category-' + [Guid]::NewGuid().ToString('N'))

try {
    $ok = New-Item -ItemType Directory -Path (Join-Path $tempRoot 'ok\nested') -Force
    $ng = New-Item -ItemType Directory -Path (Join-Path $tempRoot 'NG') -Force
    $other = New-Item -ItemType Directory -Path (Join-Path $tempRoot '其他') -Force
    [IO.File]::WriteAllText((Join-Path $ok.FullName 'b.jpg'), 'x')
    [IO.File]::WriteAllText((Join-Path $ng.FullName 'c.png'), 'x')
    [IO.File]::WriteAllText((Join-Path $other.FullName 'a.bmp'), 'x')
    [IO.File]::WriteAllText((Join-Path $tempRoot 'root.tif'), 'x')
    [IO.File]::WriteAllText((Join-Path $tempRoot 'skip.txt'), 'x')

    if (-not $classifier.HasStandardCategoryDirectories($tempRoot)) { throw '应识别直属 OK/NG 目录。' }
    $items = @($classifier.Scan($tempRoot, $null))
    if ($items.Count -ne 4) { throw "结构化目录应得到4张图片，实际为$($items.Count)。" }
    if (@($items | Where-Object Category -eq ([TDJS_Vision.Forms.AiTrainForm.TrainingSampleCategory]::OK)).Count -ne 1) { throw 'OK分类错误。' }
    if (@($items | Where-Object Category -eq ([TDJS_Vision.Forms.AiTrainForm.TrainingSampleCategory]::NG)).Count -ne 1) { throw 'NG分类错误。' }
    if (@($items | Where-Object Category -eq ([TDJS_Vision.Forms.AiTrainForm.TrainingSampleCategory]::Default)).Count -ne 2) { throw '默认分类错误。' }

    Remove-Item -LiteralPath (Join-Path $tempRoot 'ok') -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $tempRoot 'NG') -Recurse -Force
    if ($classifier.HasStandardCategoryDirectories($tempRoot)) { throw '无标准目录时不应进入结构化模式。' }
    $forced = @($classifier.Scan($tempRoot, [TDJS_Vision.Forms.AiTrainForm.TrainingSampleCategory]::NG))
    if (@($forced | Where-Object Category -ne ([TDJS_Vision.Forms.AiTrainForm.TrainingSampleCategory]::NG)).Count -ne 0) { throw '强制NG分类错误。' }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
}

Write-Host 'AI训练样本目录分类检查通过。'
```

- [ ] **Step 2: 运行测试确认红灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleDirectoryClassifier.Tests.ps1`

Expected: FAIL，提示缺少 `TrainingSampleCategory.cs` 或其他目录分类源码。

- [ ] **Step 3: 实现纯目录分类接口与默认实现**

实现以下精确契约；`Scan` 在 `forcedCategory` 有值时递归扫描整个目录并强制分类，否则按直属 OK/NG 根路径判定，其他支持图片返回默认：

```csharp
public enum TrainingSampleCategory
{
    Default,
    OK,
    NG
}

public sealed class TrainingSampleDirectoryEntry
{
    public TrainingSampleDirectoryEntry(string filePath, TrainingSampleCategory category)
    {
        FilePath = filePath;
        Category = category;
    }

    public string FilePath { get; private set; }
    public TrainingSampleCategory Category { get; private set; }
}

public interface ITrainingSampleDirectoryClassifier
{
    bool HasStandardCategoryDirectories(string rootFolder);
    IReadOnlyList<TrainingSampleDirectoryEntry> Scan(string rootFolder, TrainingSampleCategory? forcedCategory);
}
```

默认实现使用 `Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)`、扩展名白名单 `.bmp/.jpg/.jpeg/.png/.tif/.tiff`、`StringComparer.OrdinalIgnoreCase` 排序和带末尾分隔符的完整路径前缀判断。路径无效时抛出带简体中文消息的 `DirectoryNotFoundException`；不得吞掉目录访问异常。

- [ ] **Step 4: 把四个新源文件加入项目并运行绿灯测试**

在 `TDJS-Vision.csproj` 的 AI 训练编译项区域加入以下四个条目：

```xml
<Compile Include="Forms\AiTrainForm\ITrainingSampleDirectoryClassifier.cs" />
<Compile Include="Forms\AiTrainForm\TrainingSampleCategory.cs" />
<Compile Include="Forms\AiTrainForm\TrainingSampleDirectoryEntry.cs" />
<Compile Include="Forms\AiTrainForm\TrainingSampleDirectoryClassifier.cs" />
```

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleDirectoryClassifier.Tests.ps1`

Expected: PASS，输出“AI训练样本目录分类检查通过。”

- [ ] **Step 5: 提交目录分类核心**

```powershell
git add -- 'Forms/AiTrainForm/ITrainingSampleDirectoryClassifier.cs' 'Forms/AiTrainForm/TrainingSampleCategory.cs' 'Forms/AiTrainForm/TrainingSampleDirectoryEntry.cs' 'Forms/AiTrainForm/TrainingSampleDirectoryClassifier.cs' 'Tests/AiTrainingSampleDirectoryClassifier.Tests.ps1' 'TDJS-Vision.csproj'
git commit -m "feat: 添加AI训练样本目录分类器"
```

### Task 2: 增加整批样本类别选择窗体

**Files:**
- Create: `Forms/AiTrainForm/SampleCategorySelectionForm.cs`
- Create: `Forms/AiTrainForm/SampleCategorySelectionForm.Designer.cs`
- Create: `Tests/AiTrainingSampleCategoryDialog.Tests.ps1`
- Modify: `TDJS-Vision.csproj`

**Interfaces:**
- Consumes: `TrainingSampleCategory`
- Produces: `TrainingSampleCategory? SampleCategorySelectionForm.SelectedCategory`
- Produces: `DialogResult.OK` for OK/NG choice and `DialogResult.Cancel` for cancellation

- [ ] **Step 1: 写类别选择窗体红灯测试**

测试必须读取窗体和设计器源码，断言设计器声明 `buttonChooseOk`、`buttonChooseNg`、`buttonCancel`、中文文本和事件绑定；代码文件必须公开只读选择结果，并分别设置 OK、NG、Cancel。

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$codePath = Join-Path $root 'Forms\AiTrainForm\SampleCategorySelectionForm.cs'
$designerPath = Join-Path $root 'Forms\AiTrainForm\SampleCategorySelectionForm.Designer.cs'
if (-not (Test-Path -LiteralPath $codePath)) { throw '缺少样本类别选择窗体代码。' }
if (-not (Test-Path -LiteralPath $designerPath)) { throw '缺少样本类别选择窗体设计器。' }
$code = Get-Content -Raw -Encoding UTF8 -LiteralPath $codePath
$designer = Get-Content -Raw -Encoding UTF8 -LiteralPath $designerPath
foreach ($token in @('buttonChooseOk', 'buttonChooseNg', 'buttonCancel', '加入OK样本', '加入NG样本', '取消')) {
    if ($designer -notmatch [regex]::Escape($token)) { throw "设计器缺少：$token" }
}
foreach ($token in @('SelectedCategory', 'TrainingSampleCategory.OK', 'TrainingSampleCategory.NG', 'DialogResult.Cancel')) {
    if ($code -notmatch [regex]::Escape($token)) { throw "窗体逻辑缺少：$token" }
}
Write-Host '样本类别选择窗体检查通过。'
```

- [ ] **Step 2: 运行测试确认红灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleCategoryDialog.Tests.ps1`

Expected: FAIL，提示缺少类别选择窗体。

- [ ] **Step 3: 在设计器文件中实现中文类别选择窗体**

`SampleCategorySelectionForm.Designer.cs` 创建固定大小、居中父窗体的对话框，包含说明标签和三个按钮；所有控件字段、位置、锚定、中文 `Text` 和事件绑定均写在设计器文件。代码文件只保留构造、结果属性和三个点击事件：

```csharp
public partial class SampleCategorySelectionForm : Form
{
    public SampleCategorySelectionForm()
    {
        InitializeComponent();
    }

    public TrainingSampleCategory? SelectedCategory { get; private set; }

    private void buttonChooseOk_Click(object sender, EventArgs e)
    {
        SelectedCategory = TrainingSampleCategory.OK;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void buttonChooseNg_Click(object sender, EventArgs e)
    {
        SelectedCategory = TrainingSampleCategory.NG;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void buttonCancel_Click(object sender, EventArgs e)
    {
        SelectedCategory = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
```

- [ ] **Step 4: 加入项目并运行绿灯测试**

在 `TDJS-Vision.csproj` 中以 Form/DependentUpon 关系加入 `.cs` 和 `.Designer.cs`。

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleCategoryDialog.Tests.ps1`

Expected: PASS。

- [ ] **Step 5: 提交类别选择窗体**

```powershell
git add -- 'Forms/AiTrainForm/SampleCategorySelectionForm.cs' 'Forms/AiTrainForm/SampleCategorySelectionForm.Designer.cs' 'Tests/AiTrainingSampleCategoryDialog.Tests.ps1' 'TDJS-Vision.csproj'
git commit -m "feat: 添加训练样本类别选择窗口"
```

### Task 3: 接入默认类别、目录追加和人工分类界面

**Files:**
- Modify: `Forms/AiTrainForm/UnsupervisedTrainingModels.cs`
- Modify: `Forms/AiTrainForm/LargeModelTrainingModels.cs`
- Modify: `Forms/AiTrainForm/UnsupervisedImageLoader.cs`
- Modify: `Forms/AiTrainForm/LargeModelImageLoader.cs`
- Modify: `Forms/AiTrainForm/UnsupervisedTrainForm.cs`
- Modify: `Forms/AiTrainForm/UnsupervisedTrainForm.Designer.cs`
- Modify: `Forms/AiTrainForm/LargeModelTrainForm.cs`
- Modify: `Forms/AiTrainForm/LargeModelTrainForm.Designer.cs`
- Create: `Tests/AiTrainingSampleImportUi.Tests.ps1`

**Interfaces:**
- Consumes: `ITrainingSampleDirectoryClassifier.Scan`
- Consumes: `SampleCategorySelectionForm.SelectedCategory`
- Produces: `Default` members in `UnsupervisedImageCategory` and `LargeModelImageCategory`
- Produces: loader overloads accepting `IReadOnlyList<TrainingSampleDirectoryEntry>`
- Produces: `LoadImagesAsync(string folder, TrainingSampleCategory? forcedCategory, bool append)` in both forms

- [ ] **Step 1: 写两个窗体与加载器的红灯检查**

创建 `Tests/AiTrainingSampleImportUi.Tests.ps1`，读取两个模型、两个加载器、两个窗体和两个设计器，逐个断言以下精确标记：

```powershell
$requiredDesignerTokens = @(
    'buttonFilterDefault',
    'buttonAddOkSampleFolder',
    'buttonAddNgSampleFolder',
    'buttonSetSelectedDefault',
    '默认 0',
    '添加OK样本目录',
    '添加NG样本目录',
    '设默认'
)
$requiredCodeTokens = @(
    'ITrainingSampleDirectoryClassifier',
    'HasStandardCategoryDirectories',
    'SampleCategorySelectionForm',
    'TrainingSampleCategory.Default',
    'buttonAddOkSampleFolder_Click',
    'buttonAddNgSampleFolder_Click',
    'buttonSetSelectedDefault_Click',
    'LoadImagesAsync(string folder, TrainingSampleCategory? forcedCategory, bool append)'
)
```

测试还必须断言两个设计器的四个新增按钮和一个“设默认”按钮都有事件绑定，两个模型枚举含 `Default`，两个加载器接受 `IReadOnlyList<TrainingSampleDirectoryEntry>`，并断言不存在基于路径去重的 `Distinct`、`HashSet<string>` 或 `GroupBy`。

- [ ] **Step 2: 运行测试确认红灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleImportUi.Tests.ps1`

Expected: FAIL，首先报告缺少 `buttonFilterDefault` 或 `Default` 枚举成员。

- [ ] **Step 3: 扩展类别模型和图片加载器**

在两个现有类别枚举的 `All` 后增加带 XML 注释的 `Default`。两个加载器新增以下入口，并用私有映射方法把共享类别转换成窗体类别；删除旧的文件名启发式 `DetectCategory`，防止默认图片被名称误判：

```csharp
public static List<UnsupervisedImageItem> LoadImages(
    IReadOnlyList<TrainingSampleDirectoryEntry> entries,
    Size thumbnailSize,
    CancellationToken token)
```

```csharp
public static List<LargeModelImageItem> LoadImages(
    IReadOnlyList<TrainingSampleDirectoryEntry> entries,
    Size thumbnailSize,
    CancellationToken token)
```

两个实现都按输入顺序生成缩略图；坏图释放临时位图并跳过，不执行去重。

- [ ] **Step 4: 在两个 Designer 文件加入完整控件布局**

左侧 `panelCategory` 从上到下布局“全部、默认、OK、NG”，缩小单个筛选按钮高度以容纳两个追加按钮；在筛选按钮下方加入“添加OK样本目录”“添加NG样本目录”。底部 `panelCheckStatus` 增加“设默认”，与“设OK”“设NG”保持统一尺寸和间距。所有 `Text`、`Name`、`Size`、`Location`、`Anchor`、`TabIndex`、`Click +=` 和字段声明必须写入对应 `.Designer.cs`。

- [ ] **Step 5: 接入两个窗体的选择、追加和恢复流程**

两个窗体分别保存本次运行期间的主路径和可空强制类别：

```csharp
private static string _lastImageFolder;
private static TrainingSampleCategory? _lastForcedCategory;
private readonly ITrainingSampleDirectoryClassifier _directoryClassifier;
```

参数less构造函数创建默认分类器，内部构造函数接收接口以便测试。主路径选择流程先调用 `HasStandardCategoryDirectories`；无标准目录时显示 `SampleCategorySelectionForm`，取消即返回；随后调用：

```csharp
private async Task LoadImagesAsync(
    string folder,
    TrainingSampleCategory? forcedCategory,
    bool append)
```

`append == false` 时在成功扫描且至少得到一张可读取图片后替换旧列表、路径和恢复状态；`append == true` 时保留旧列表和主路径，只追加结果。追加按钮分别传 `TrainingSampleCategory.OK/NG`。恢复时直接使用 `_lastForcedCategory`，不再次弹窗。

筛选、计数、边框和单张设置必须覆盖 Default：

```csharp
private void buttonSetSelectedDefault_Click(object sender, EventArgs e)
{
    SetSelectedImageCategory(UnsupervisedImageCategory.Default);
}
```

大模型窗体使用 `LargeModelImageCategory.Default`。默认卡片边框采用灰色；更新状态文本时显示总数、默认、OK、NG 四个计数。

- [ ] **Step 6: 运行窗体专项绿灯与既有界面回归**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleImportUi.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\UnsupervisedTrainForm.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\LargeModelIntegration.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\UnsupervisedTrainingDialogOwnership.Tests.ps1
```

Expected: 四组全部 PASS。若既有测试对旧按钮尺寸做精确断言，按新设计更新该断言，同时继续验证按钮可读、设计器可见和事件已绑定。

- [ ] **Step 7: 提交窗体和加载器改动**

```powershell
git add -- 'Forms/AiTrainForm/UnsupervisedTrainingModels.cs' 'Forms/AiTrainForm/LargeModelTrainingModels.cs' 'Forms/AiTrainForm/UnsupervisedImageLoader.cs' 'Forms/AiTrainForm/LargeModelImageLoader.cs' 'Forms/AiTrainForm/UnsupervisedTrainForm.cs' 'Forms/AiTrainForm/UnsupervisedTrainForm.Designer.cs' 'Forms/AiTrainForm/LargeModelTrainForm.cs' 'Forms/AiTrainForm/LargeModelTrainForm.Designer.cs' 'Tests/AiTrainingSampleImportUi.Tests.ps1' 'Tests/UnsupervisedTrainForm.Tests.ps1'
git commit -m "feat: 完善AI训练样本分类与目录追加"
```

### Task 4: 从两个训练数据集中严格排除默认图片

**Files:**
- Modify: `Forms/AiTrainForm/UnsupervisedTrainingService.cs`
- Modify: `Forms/AiTrainForm/LargeModelTrainingService.cs`
- Create: `Tests/AiTrainingDefaultCategoryExclusion.Tests.ps1`

**Interfaces:**
- Consumes: `UnsupervisedImageCategory.Default`
- Consumes: `LargeModelImageCategory.Default`
- Preserves: existing `DatasetBuildResult.OkCount` and `.NgCount`

- [ ] **Step 1: 写默认类别排除红灯测试**

创建静态契约测试，要求两个 `BuildDataset` 循环先对白名单做显式判断，并禁止原来的二选一目标目录表达式：

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$cases = @(
    @{ Path = 'Forms\AiTrainForm\UnsupervisedTrainingService.cs'; Prefix = 'UnsupervisedImageCategory' },
    @{ Path = 'Forms\AiTrainForm\LargeModelTrainingService.cs'; Prefix = 'LargeModelImageCategory' }
)

foreach ($case in $cases) {
    $source = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root $case.Path)
    $defaultGuard = 'if (item.Category == ' + $case.Prefix + '.Default)'
    $okBranch = 'item.Category == ' + $case.Prefix + '.OK'
    $ngBranch = 'item.Category == ' + $case.Prefix + '.NG'
    if ($source -notmatch [regex]::Escape($defaultGuard)) { throw "$($case.Path) 未显式跳过默认图片。" }
    if ($source -notmatch [regex]::Escape($okBranch)) { throw "$($case.Path) 未显式处理OK白名单。" }
    if ($source -notmatch [regex]::Escape($ngBranch)) { throw "$($case.Path) 未显式处理NG白名单。" }
    if ($source -match 'Category\s*==\s*[^\r\n]+\.NG\s*\?\s*ngPath\s*:\s*okPath') { throw "$($case.Path) 仍把非NG类别当成OK。" }
}

Write-Host 'AI训练默认类别排除检查通过。'
```

- [ ] **Step 2: 运行测试确认红灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingDefaultCategoryExclusion.Tests.ps1`

Expected: FAIL，指出训练服务未显式跳过默认图片。

- [ ] **Step 3: 在两个数据集循环中加入类别白名单**

两个 `BuildDataset` 循环采用同一结构；默认先跳过且不增加 `index`，OK/NG 才选择目标目录和执行复制/裁剪：

```csharp
if (item.Category == UnsupervisedImageCategory.Default)
{
    continue;
}

string targetFolder;
if (item.Category == UnsupervisedImageCategory.OK)
{
    targetFolder = okPath;
}
else if (item.Category == UnsupervisedImageCategory.NG)
{
    targetFolder = ngPath;
}
else
{
    continue;
}
```

大模型版本使用 `LargeModelImageCategory`。成功计数同样只允许显式 OK/NG 分支。请求校验继续要求至少一个 OK，但错误提示改为“训练至少需要一张已归入OK的图片。”以避免默认图片造成误解。

- [ ] **Step 4: 运行绿灯和训练回归**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingDefaultCategoryExclusion.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\UnsupervisedTemplatePackage.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingBehavior.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingProductionBehavior.Tests.ps1
```

Expected: 全部 PASS，默认类别排除检查输出成功信息。

- [ ] **Step 5: 提交训练安全改动**

```powershell
git add -- 'Forms/AiTrainForm/UnsupervisedTrainingService.cs' 'Forms/AiTrainForm/LargeModelTrainingService.cs' 'Tests/AiTrainingDefaultCategoryExclusion.Tests.ps1'
git commit -m "fix: 排除AI训练默认类别图片"
```

### Task 5: 完整验证并记录任务

**Files:**
- Modify: `任务记录.md`
- Verify: all files changed by Tasks 1-4

**Interfaces:**
- Consumes: all completed feature behavior
- Produces: reproducible verification evidence and Chinese task record

- [ ] **Step 1: 运行全部专项检查**

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleDirectoryClassifier.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleCategoryDialog.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingSampleImportUi.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\AiTrainingDefaultCategoryExclusion.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\UnsupervisedTrainForm.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\UnsupervisedTrainingDialogOwnership.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\UnsupervisedTemplatePackage.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\LargeModelIntegration.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingBehavior.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\LargeModelTrainingProductionBehavior.Tests.ps1
```

Expected: 所有脚本退出码为 0。

- [ ] **Step 2: 编译完整解决方案**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' 'D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln' /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: Build succeeded，0 个错误；既有警告可记录但不得新增由本任务导致的编译警告。

- [ ] **Step 3: 检查变更范围和编码**

```powershell
git diff --check
git status --short
```

确认没有改动无关测量节点；所有新增 PowerShell 测试在 Windows PowerShell 5.1 下能按 UTF-8 解析中文字符串。

- [ ] **Step 4: 追加中文任务记录**

在 `任务记录.md` 末尾新增“2026-07-27：AI训练样本分类与目录追加”，记录：默认/OK/NG 规则、无结构目录弹窗、两个追加目录按钮、允许重复、默认排除、专项测试和编译结果。不得覆盖现有其他任务记录。

- [ ] **Step 5: 提交任务记录和最终测试调整**

```powershell
git add -- '任务记录.md'
git commit -m "docs: 记录AI训练样本分类改造"
```
