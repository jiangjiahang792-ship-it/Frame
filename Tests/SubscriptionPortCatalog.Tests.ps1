$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$projectRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}

$contractsPath = Join-Path $projectRoot 'Node\SubscriptionPortContracts.cs'
$compatibilityPath = Join-Path $projectRoot 'Node\SubscriptionTypeCompatibility.cs'
$catalogPath = Join-Path $projectRoot 'Node\SubscriptionPortCatalog.cs'

foreach ($path in @($contractsPath, $compatibilityPath, $catalogPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "订阅端口目录依赖文件不存在：$path"
    }
}

$allUsings = @'
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
'@

$fixtureSource = @'
namespace TDJS_Vision.Node
{
    public class NodeBase
    {
        public object Result { get; set; }
    }

    public static class DynamicResultVariableResolver
    {
        public static IReadOnlyList<SubscriptionOutputDescriptor> GetDescriptors(NodeBase node)
        {
            return new List<SubscriptionOutputDescriptor>();
        }

        public static string ExtractVariableName(string path)
        {
            return path ?? string.Empty;
        }
    }
}

namespace TDJS_Vision.Tests
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using TDJS_Vision.Node;

    public sealed class OutputImage
    {
    }

    public sealed class TestPoint
    {
    }

    public sealed class AlgorithmResult
    {
    }

    public sealed class CatalogFixtureResult
    {
        [SubscriptionOutput]
        [DisplayName("Short值")]
        public short ShortValue { get; set; }

        [SubscriptionOutput]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        [SubscriptionOutput(SubscriptionDataCategory.Image, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("输出图像")]
        public OutputImage Image { get; set; }

        [SubscriptionOutput]
        [DisplayName("自动分类图像")]
        public OutputImage AutoImage { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("诊断信息")]
        public string Diagnostics { get; set; }

        [SubscriptionOutput(SubscriptionDataCategory.PointCollection, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("边缘点")]
        public List<TestPoint> Points { get; set; }

        [SubscriptionOutput]
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; }

        [SubscriptionOutput]
        [DisplayName("结构化结果")]
        public object Structured { get; set; }

        [DisplayName("漏分类属性")]
        public int Unclassified { get; set; }
    }
}
'@

$sources = @(
    (Get-Content -LiteralPath $contractsPath -Raw -Encoding UTF8)
    (Get-Content -LiteralPath $compatibilityPath -Raw -Encoding UTF8)
    (Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8)
)
$source = $allUsings + [Environment]::NewLine
foreach ($item in $sources) {
    $source += ($item -replace '(?m)^using\s+[^;]+;\s*$', '') + [Environment]::NewLine
}
$source += $fixtureSource
Add-Type -TypeDefinition $source -Language CSharp

$outputs = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetStaticOutputs([TDJS_Vision.Tests.CatalogFixtureResult])
Assert-True ($outputs.Count -eq 8) '目录只能收录同时声明 DisplayName 和 SubscriptionOutput 的八个属性。'

$shortOutput = $outputs | Where-Object { $_.DisplayName -eq 'Short值' }
Assert-True ($shortOutput.Category -eq [TDJS_Vision.Node.SubscriptionDataCategory]::Number) 'short 必须由统一解析器归类为数值。'
Assert-True ($shortOutput.ValueType -eq [System.Int16]) '目录必须保留 short 的真实 CLR 类型。'

$boolOutput = $outputs | Where-Object { $_.DisplayName -eq '是否OK' }
Assert-True ($boolOutput.Category -eq [TDJS_Vision.Node.SubscriptionDataCategory]::Boolean) '是否OK 必须归类为布尔，而不是独立判定类别。'

$hiddenOutput = $outputs | Where-Object { $_.DisplayName -eq '诊断信息' }
Assert-True ($hiddenOutput.Visibility -eq [TDJS_Vision.Node.SubscriptionOutputVisibility]::Hidden) '隐藏输出必须保留描述符。'

$pointOutput = $outputs | Where-Object { $_.DisplayName -eq '边缘点' }
Assert-True ($pointOutput.Category -eq [TDJS_Vision.Node.SubscriptionDataCategory]::PointCollection) '歧义集合必须采用显式点集合类别。'
Assert-True ($pointOutput.Multiplicity -eq [TDJS_Vision.Node.SubscriptionValueMultiplicity]::Collection) '边缘点必须标记为集合。'

$autoImageOutput = $outputs | Where-Object { $_.DisplayName -eq '自动分类图像' }
Assert-True ($autoImageOutput.Category -eq [TDJS_Vision.Node.SubscriptionDataCategory]::Image) 'OutputImage 必须自动归类为图像。'
Assert-True ($autoImageOutput.Visibility -eq [TDJS_Vision.Node.SubscriptionOutputVisibility]::Advanced) '复杂图像输出即使未显式写高级，也必须默认归入高级结果。'

$secondRead = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetStaticOutputs([TDJS_Vision.Tests.CatalogFixtureResult])
Assert-True ([object]::ReferenceEquals($outputs, $secondRead)) '静态结果描述必须按类型缓存。'

$numberContract = [TDJS_Vision.Node.SubscriptionInputContract]::ForType([double])
$node = New-Object TDJS_Vision.Node.NodeBase
$node.Result = New-Object TDJS_Vision.Tests.CatalogFixtureResult
$numberCandidates = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetOutputs($node, $numberContract, $false, '')
Assert-True ($numberCandidates.Count -eq 1 -and $numberCandidates[0].DisplayName -eq 'Short值') 'double 数值输入应直接看到 short，且不看到图像、布尔和隐藏诊断。'

$imageCategories = [TDJS_Vision.Node.SubscriptionDataCategory[]]@([TDJS_Vision.Node.SubscriptionDataCategory]::Image)
$imageContract = [TDJS_Vision.Node.SubscriptionInputContract]::ForCategories(
    $imageCategories,
    [TDJS_Vision.Node.NumericConversionMode]::SafeWidening)
$imageCandidates = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetOutputs($node, $imageContract, $false, '')
Assert-True ($imageCandidates.Count -eq 2) '明确图像输入应自动看到全部高级图像结果。'
Assert-True (($imageCandidates | Where-Object { $_.DisplayName -eq '输出图像' }).Count -eq 1) '显式高级图像结果必须保留。'
Assert-True (($imageCandidates | Where-Object { $_.DisplayName -eq '自动分类图像' }).Count -eq 1) '自动分类图像结果必须进入候选。'

$complexCategories = [TDJS_Vision.Node.SubscriptionDataCategory[]]@(
    [TDJS_Vision.Node.SubscriptionDataCategory]::AlgorithmResult,
    [TDJS_Vision.Node.SubscriptionDataCategory]::StructuredObject)
$complexContract = [TDJS_Vision.Node.SubscriptionInputContract]::ForCategories(
    $complexCategories,
    [TDJS_Vision.Node.NumericConversionMode]::None)
$complexCandidates = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetOutputs($node, $complexContract, $false, '')
Assert-True ($complexCandidates.Count -eq 2) '只接受复杂类别的输入必须自动看到对应高级结果，不要求手动展开。'

$textOverlayCategories = [TDJS_Vision.Node.SubscriptionDataCategory[]]@(
    [TDJS_Vision.Node.SubscriptionDataCategory]::Boolean,
    [TDJS_Vision.Node.SubscriptionDataCategory]::Number,
    [TDJS_Vision.Node.SubscriptionDataCategory]::Text,
    [TDJS_Vision.Node.SubscriptionDataCategory]::AlgorithmResult)
$textOverlayContract = [TDJS_Vision.Node.SubscriptionInputContract]::ForCategories(
    $textOverlayCategories,
    [TDJS_Vision.Node.NumericConversionMode]::SafeWidening)
$textOverlayCandidates = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetOutputs($node, $textOverlayContract, $false, '')
Assert-True (($textOverlayCandidates | Where-Object { $_.DisplayName -eq '算法结果' }).Count -eq 1) 'ROI结果绘制文本项虽然同时接受基础文本，也必须自动显示AI输出结果。'

$legacyCandidates = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetOutputs($node, [TDJS_Vision.Node.SubscriptionInputContract]::AnyVisible(), $false, '诊断信息')
$legacy = $legacyCandidates | Where-Object { $_.DisplayName -eq '诊断信息' }
Assert-True ($null -ne $legacy -and $legacy.IsLegacySelection) '旧方案当前选中的隐藏结果必须以兼容订阅保留。'

$missingCandidates = [TDJS_Vision.Node.SubscriptionPortCatalog]::GetOutputs($node, [TDJS_Vision.Node.SubscriptionInputContract]::AnyVisible(), $false, '变量.已删除')
$missing = $missingCandidates | Where-Object { $_.IsMissing }
Assert-True ($null -ne $missing -and $missing.DisplayName -eq '变量.已删除') '已经删除的结果必须保留缺失占位，不能回退到第一项。'

Write-Host 'Subscription port catalog checks passed.'
