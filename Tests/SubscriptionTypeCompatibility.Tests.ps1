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

if (-not (Test-Path -LiteralPath $contractsPath)) {
    throw "订阅端口契约文件不存在：$contractsPath"
}
if (-not (Test-Path -LiteralPath $compatibilityPath)) {
    throw "订阅类型兼容服务文件不存在：$compatibilityPath"
}

$contractsSource = Get-Content -LiteralPath $contractsPath -Raw -Encoding UTF8
$compatibilitySource = Get-Content -LiteralPath $compatibilityPath -Raw -Encoding UTF8
$allUsings = @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
'@
$source = $allUsings + [Environment]::NewLine +
    ($contractsSource -replace '(?m)^using\s+[^;]+;\s*$', '') + [Environment]::NewLine +
    ($compatibilitySource -replace '(?m)^using\s+[^;]+;\s*$', '')
Add-Type -TypeDefinition $source -Language CSharp

$compatibilityType = [TDJS_Vision.Node.SubscriptionTypeCompatibility]
$numberCategory = [TDJS_Vision.Node.SubscriptionDataCategory]::Number
$booleanCategory = [TDJS_Vision.Node.SubscriptionDataCategory]::Boolean
$safeWidening = [TDJS_Vision.Node.NumericConversionMode]::SafeWidening
$checkedMode = [TDJS_Vision.Node.NumericConversionMode]::Checked

Assert-True ($compatibilityType::ResolveCategory([System.Int16]) -eq $numberCategory) 'short 必须归类为数值。'
Assert-True ($compatibilityType::ResolveCategory([Nullable[System.Int16]]) -eq $numberCategory) '可空 short 必须归类为数值。'
Assert-True ($compatibilityType::ResolveCategory([bool]) -eq $booleanCategory) '判定值本质必须归类为布尔。'

Assert-True ($compatibilityType::CanAssign([System.Int16], [double], $safeWidening)) 'short 必须允许直接订阅到 double 数值输入。'
Assert-True ($compatibilityType::CanAssign([System.Int16], [object], $safeWidening)) 'short 必须允许被 object 输入读取。'
Assert-True (-not $compatibilityType::CanAssign([System.Int16], [bool], $safeWidening)) '数值不能默认当作布尔。'
Assert-True (-not $compatibilityType::CanAssign([double], [System.Int16], $safeWidening)) '安全扩宽模式不能静默缩窄 double。'
Assert-True ($compatibilityType::CanAssign([double], [System.Int16], $checkedMode)) '显式检查模式应允许经过范围检查的数值缩窄。'

$converted = $compatibilityType::ConvertValue([System.Int16]123, [double], $safeWidening)
Assert-True ($converted.GetType() -eq [double]) 'short 转 double 后类型必须为 Double。'
Assert-True ([double]$converted -eq 123.0) 'short 转 double 后必须保留数值。'

$overflowThrown = $false
try {
    $null = $compatibilityType::ConvertValue([int]40000, [System.Int16], $checkedMode)
}
catch [System.InvalidCastException] {
    $overflowThrown = $_.Exception.Message.Contains('Int32') -and $_.Exception.Message.Contains('Int16')
}
Assert-True $overflowThrown '数值缩窄溢出必须抛出包含实际类型和目标类型的中文异常。'

Write-Host 'Subscription type compatibility checks passed.'
