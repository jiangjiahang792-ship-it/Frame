$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        $Expected,
        $Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message 期望值：$Expected；实际值：$Actual。"
    }
}

$testRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Join-Path (Get-Location) 'Tests'
}
else {
    $PSScriptRoot
}

$judgmentSourcePath = Join-Path $testRoot '..\Node\7-ResultProcessing\ImageSave\ImageSaveJudgment.cs'
if (-not (Test-Path -LiteralPath $judgmentSourcePath)) {
    throw '缺少保存图像统一判定实现 ImageSaveJudgment.cs。'
}

# 使用生产源码和最小算法结果依赖进行编译，直接验证判定解析行为。
$judgmentSource = Get-Content -LiteralPath $judgmentSourcePath -Raw -Encoding UTF8
$algorithmResultStub = @'
namespace TDJS_Vision.Node._3_Detection.TDAI
{
    public class AlgorithmResult
    {
        public AlgorithmResult()
        {
            DetectResults = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SingleDetectResult>>();
        }

        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SingleDetectResult>> DetectResults { get; set; }
    }

    public class SingleDetectResult
    {
        public bool IsOk { get; set; }
    }
}
'@

Add-Type -TypeDefinition ($judgmentSource + [Environment]::NewLine + $algorithmResultStub) -Language CSharp

$resolver = New-Object TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageSaveJudgmentResolver

$booleanOk = $resolver.Resolve($true)
Assert-True $booleanOk.IsOk '多条件结果为 True 时应判定为 OK。'
Assert-Equal 0 $booleanOk.NgCategoryNames.Count '多条件 OK 结果不应包含 NG 分类。'

$booleanNg = $resolver.Resolve($false)
Assert-True (-not $booleanNg.IsOk) '多条件结果为 False 时应判定为 NG。'
Assert-Equal 0 $booleanNg.NgCategoryNames.Count '多条件 NG 结果没有检测项分类时应保留空分类集合。'
Assert-True $booleanNg.UseGenericNgDirectoryWhenNoCategory '多条件 NG 结果没有分类时应保存到通用 NG 目录。'

$algorithmResult = New-Object TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult
$okItem = New-Object TDJS_Vision.Node._3_Detection.TDAI.SingleDetectResult
$okItem.IsOk = $true
$ngItem = New-Object TDJS_Vision.Node._3_Detection.TDAI.SingleDetectResult
$ngItem.IsOk = $false
$okItems = New-Object 'System.Collections.Generic.List[TDJS_Vision.Node._3_Detection.TDAI.SingleDetectResult]'
$okItems.Add($okItem)
$ngItems = New-Object 'System.Collections.Generic.List[TDJS_Vision.Node._3_Detection.TDAI.SingleDetectResult]'
$ngItems.Add($ngItem)
$algorithmResult.DetectResults.Add('合格项', $okItems)
$algorithmResult.DetectResults.Add('缺陷项', $ngItems)

$algorithmNg = $resolver.Resolve($algorithmResult)
Assert-True (-not $algorithmNg.IsOk) 'AI结果包含 NG 检测项时应判定为 NG。'
Assert-Equal 1 $algorithmNg.NgCategoryNames.Count 'AI结果应只返回 NG 检测项分类。'
Assert-Equal '缺陷项' $algorithmNg.NgCategoryNames[0] 'AI结果返回的 NG 分类名称不正确。'
Assert-True (-not $algorithmNg.UseGenericNgDirectoryWhenNoCategory) 'AI结果应继续按检测项分类保存。'

$unsupportedThrown = $false
try {
    $resolver.Resolve('不支持的结果类型')
}
catch {
    $unsupportedThrown = $_.Exception.Message.Contains('不支持')
}
Assert-True $unsupportedThrown '不支持的订阅类型应抛出清晰异常。'

# 验证保存节点运行链路读取 object，并在无检测项分类的 NG 结果下保存到通用 NG 目录。
$formSourcePath = Join-Path $testRoot '..\Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.cs'
$designerSourcePath = Join-Path $testRoot '..\Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.Designer.cs'
$nodeSourcePath = Join-Path $testRoot '..\Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs'
$projectPath = Join-Path $testRoot '..\TDJS-Vision.csproj'
$formSource = Get-Content -LiteralPath $formSourcePath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $designerSourcePath -Raw -Encoding UTF8
$nodeSource = Get-Content -LiteralPath $nodeSourcePath -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8

Assert-True $formSource.Contains('public ImageSaveJudgment GetImageSaveJudgment()') '参数窗体应公开统一保存判定读取方法。'
Assert-True $formSource.Contains('nodeSubscriptionAiRes.GetValue<object>()') 'OK/NG订阅必须先按 object 读取，避免把布尔值强转为 AlgorithmResult。'
Assert-True $designerSource.Contains('this.label6.Text = "订阅OK/NG判定结果:";') '保存界面应使用兼容 AI 和多条件结果的中文标签。'
Assert-True $nodeSource.Contains('ImageSaveJudgment imageSaveJudgment = null;') '保存节点应使用统一判定对象。'
Assert-True $nodeSource.Contains('imageSaveJudgment.UseGenericNgDirectoryWhenNoCategory') '多条件 NG 结果没有分类时应回落到通用 NG 目录。'
Assert-True $projectSource.Contains('Node\7-ResultProcessing\ImageSave\ImageSaveJudgment.cs') '项目文件应编译统一判定实现。'

Write-Host '保存图像多条件订阅回归检查通过。'
