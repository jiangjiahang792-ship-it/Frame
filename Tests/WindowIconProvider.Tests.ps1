$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

$projectFile = Get-ProjectSource 'TDJS-Vision.csproj'
$formBase = Get-ProjectSource 'FormBase.cs'
$globalSignalForm = Get-ProjectSource 'Forms\GlobalSignalSettings\FormGlobalSignal.cs'
$globalSignalDesigner = Get-ProjectSource 'Forms\GlobalSignalSettings\FormGlobalSignal.Designer.cs'
$iconProvider = Get-ProjectSource 'Forms\Common\WindowIconProvider.cs'

Assert-Contains $projectFile 'Compile Include="Forms\Common\WindowIconProvider.cs"' 'Project file must compile the shared window icon provider.'
Assert-Contains $iconProvider 'public static Icon CreateWindowIcon()' 'WindowIconProvider must expose one shared window icon factory.'
Assert-Contains $iconProvider 'Properties.Resources.TDJSIco' 'WindowIconProvider must use the central TDJS icon resource.'

Assert-Contains $formBase 'WindowIconProvider.Apply(this);' 'FormBase custom title bar must use the shared window icon provider.'
Assert-Contains $globalSignalForm 'WindowIconProvider.Apply(this);' 'FormGlobalSignal must apply the shared window icon provider.'
Assert-NotContains $globalSignalDesigner 'this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));' 'FormGlobalSignal designer must not keep a private embedded form icon.'

Write-Host 'Window icon provider regression checks passed.'
