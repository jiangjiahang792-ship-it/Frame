$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matcherPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\FastTemplateMatcher.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$runtimeProfilePath = Join-Path $projectRoot 'Native\MatchTool.runtime.txt'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    Assert-True -Condition ($Text.Contains($Expected)) -Message $Message
}

$matcherSource = Get-Content -LiteralPath $matcherPath -Encoding UTF8 -Raw
$projectSource = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw
$runtimeProfile = Get-Content -LiteralPath $runtimeProfilePath -Encoding UTF8 -Raw

Assert-Contains $matcherSource 'IsProcessorFeaturePresent' 'Template matching must check CPU features before entering the AVX2 native library.'
Assert-Contains $matcherSource 'PfAvx2InstructionsAvailable = 40' 'Template matching must use the Windows AVX2 processor feature id.'
Assert-Contains $matcherSource 'EnsureCpuInstructionSetSupported' 'Template matching native initialization must gate unsupported CPUs before DLL API calls.'
Assert-Contains $matcherSource 'ShouldUseManagedCpuFallback' 'Template matching must automatically choose a CPU fallback before entering AVX2 native code.'
Assert-Contains $matcherSource 'MatchWithManagedCpuFallback' 'Template matching must provide a managed CPU fallback for non-AVX2 computers.'
Assert-Contains $matcherSource 'Cv2.MatchTemplate' 'The CPU fallback must use OpenCvSharp template matching instead of the AVX2 native DLL.'
Assert-Contains $matcherSource 'RequiresAvx2' 'Template matching must read the native runtime profile.'
Assert-Contains $matcherSource 'InvalidOperationException' 'Unsupported CPU errors must be user-visible instead of entering native code.'
Assert-Contains $matcherSource 'AVX2' 'Unsupported CPU errors must explain the AVX2 compatibility requirement.'
Assert-Contains $matcherSource 'CpuSupportsAvx2' 'Diagnostics must log the detected CPU AVX2 capability.'

Assert-True -Condition (Test-Path -LiteralPath $runtimeProfilePath -PathType Leaf) -Message 'MatchTool runtime profile must be shipped with the native DLL.'
Assert-Contains $runtimeProfile 'instruction_set=AVX2' 'The current runtime profile must identify the shipped native DLL as AVX2.'
Assert-Contains $runtimeProfile 'requires_avx2=true' 'The current runtime profile must mark AVX2 as required.'
Assert-Contains $runtimeProfile 'CPU' 'The runtime profile must document the CPU fallback behavior.'
Assert-Contains $projectSource '<Content Include="Native\MatchTool.runtime.txt">' 'Project file must copy the MatchTool runtime profile to the output directory.'

Write-Host 'Match template CPU compatibility checks passed.'
