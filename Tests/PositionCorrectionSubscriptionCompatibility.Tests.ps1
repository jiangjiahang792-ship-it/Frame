$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$filterPath = Join-Path $projectRoot 'Node\SubscriptionValueTypeFilter.cs'
$subscriptionPath = Join-Path $projectRoot 'Node\NodeSubscription.cs'

if (-not (Test-Path -LiteralPath $filterPath)) {
    throw "Subscription value type filter does not exist: $filterPath"
}

$filterSource = Get-Content -LiteralPath $filterPath -Raw -Encoding UTF8
$fixtureSource = @'
namespace TDJS_Vision.Tests
{
    using System.Collections.Generic;
    using System.ComponentModel;

    public sealed class PositionCorrectionInfo
    {
    }

    public sealed class LegacyAndMultiResult
    {
        [DisplayName("位置修正信息")]
        public PositionCorrectionInfo CorrectionInfo { get; set; }

        [DisplayName("位置修正信息列表")]
        public List<PositionCorrectionInfo> Items { get; set; }
    }
}
'@

Add-Type -TypeDefinition ($filterSource + [Environment]::NewLine + $fixtureSource) -Language CSharp

$resultType = [TDJS_Vision.Tests.LegacyAndMultiResult]
$listType = [System.Collections.Generic.List[TDJS_Vision.Tests.PositionCorrectionInfo]]
$readOnlyListType = [System.Collections.Generic.IReadOnlyList[TDJS_Vision.Tests.PositionCorrectionInfo]]

$listProperties = [TDJS_Vision.Node.SubscriptionValueTypeFilter]::GetDisplayProperties($resultType, $listType)
Assert-True ($listProperties.Count -eq 1) 'List subscription must hide the legacy single correction property.'
Assert-True ($listProperties[0].Name -eq 'Items') 'List subscription must select the multi-target Items property.'

$readOnlyListProperties = [TDJS_Vision.Node.SubscriptionValueTypeFilter]::GetDisplayProperties($resultType, $readOnlyListType)
Assert-True ($readOnlyListProperties.Count -eq 1) 'An IReadOnlyList contract must accept the concrete List result property.'
Assert-True ($readOnlyListProperties[0].Name -eq 'Items') 'The compatible collection property must remain Items.'

$allProperties = [TDJS_Vision.Node.SubscriptionValueTypeFilter]::GetDisplayProperties($resultType, $null)
Assert-True ($allProperties.Count -eq 2) 'Subscriptions without a declared value type must keep existing behavior.'

$subscriptionSource = Get-Content -LiteralPath $subscriptionPath -Raw -Encoding UTF8
Assert-ContainsText $subscriptionSource 'SetExpectedValueType<T>()' 'NodeSubscription must let callers declare the expected result type.'
Assert-ContainsText $subscriptionSource 'SubscriptionValueTypeFilter.GetDisplayProperties' 'NodeSubscription must filter result properties by the declared type.'
Assert-ContainsText $subscriptionSource 'comboBox2.SelectedIndex = index1 == -1 ? 0 : index1;' 'A legacy single-value display name must fall back to the first compatible list property.'

$toolNames = @(
    'CaliperLine',
    'CaliperCircle',
    'CaliperEllipse',
    'FindPoint',
    'LineLineAngle',
    'PointPointDistance',
    'PointLineDistance',
    'PointRegionDistance'
)

foreach ($toolName in $toolNames) {
    $formPath = Join-Path $projectRoot ("Node\4-Measurement\$toolName\NodeParamForm$toolName.cs")
    $formSource = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
    $typeDeclaration = 'nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();'
    $initCall = 'nodeSubscriptionPositionCorrection.Init(node);'
    Assert-ContainsText $formSource $typeDeclaration "$toolName must declare a correction list subscription."
    Assert-True ($formSource.IndexOf($typeDeclaration) -lt $formSource.IndexOf($initCall)) "$toolName must declare the list type before initializing its subscription."
}

Write-Host 'Position correction subscription compatibility checks passed.'
