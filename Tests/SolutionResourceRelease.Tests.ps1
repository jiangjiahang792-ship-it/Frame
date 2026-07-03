$ErrorActionPreference = 'Stop'

$solutionPath = Join-Path $PSScriptRoot '..\Solution.cs'
$source = Get-Content -Path $solutionPath -Raw -Encoding UTF8

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

function Assert-Sequence {
    param(
        [string]$Text,
        [string]$First,
        [string]$Second,
        [string]$Message
    )

    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw $Message
    }
}

Assert-Contains $source 'private void ReleaseAllProcessResources()' 'Missing all-process resource release helper.'
Assert-Contains $source 'private void ReleaseProcessResources(Process process)' 'Missing single-process resource release helper.'
Assert-Contains $source 'private void ReleaseNodeResources(NodeBase node)' 'Missing node resource release helper.'
Assert-Contains $source 'private void ReleaseAiNodeModel(NodeBase node)' 'Missing AI node model release helper.'
Assert-Contains $source 'private void DisposeNodeParamForm(NodeBase node)' 'Missing node parameter form dispose helper.'
Assert-Contains $source 'ReleaseProcessResources(process);' 'RemoveProcess(Process) must release node resources before removing process.'
Assert-Contains $source 'ReleaseProcessResources(processToRemove);' 'RemoveProcess(string) must release node resources before removing process.'
Assert-Contains $source 'ReleaseAllProcessResources();' 'SolReset must release all process resources before clearing processes.'
Assert-Sequence $source 'ReleaseAllProcessResources();' 'Solution.Instance.AllProcesses.Clear();' 'SolReset must release process resources before clearing process list.'
Assert-Sequence $source 'ReleaseProcessResources(process);' 'AllProcesses.Remove(process);' 'RemoveProcess(Process) must release resources before removing the process.'
Assert-Sequence $source 'ReleaseProcessResources(processToRemove);' 'AllProcesses.Remove(processToRemove);' 'RemoveProcess(string) must release resources before removing the process.'

Write-Host 'Solution resource release regression checks passed.'
