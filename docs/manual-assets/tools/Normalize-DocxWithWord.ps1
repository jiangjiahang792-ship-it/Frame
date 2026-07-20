<#
.SYNOPSIS
Normalizes a DOCX by opening it with Word repair enabled and saving a new copy.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$StatusPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-NormalizeStatus {
    <# Writes the current normalization stage to a UTF-8 JSON status file. #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Stage,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $status = [ordered]@{
        ProcessId = $PID
        Stage = $Stage
        Message = $Message
        UpdatedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $json = $status | ConvertTo-Json -Depth 3
    [System.IO.File]::WriteAllText($StatusPath, $json, [System.Text.UTF8Encoding]::new($false))
}

$word = $null
$document = $null

try {
    $resolvedSource = (Resolve-Path -LiteralPath $SourcePath).Path
    $outputDirectory = (Resolve-Path -LiteralPath (Split-Path -Parent $OutputPath)).Path
    $statusDirectory = (Resolve-Path -LiteralPath (Split-Path -Parent $StatusPath)).Path
    $resolvedOutput = Join-Path $outputDirectory (Split-Path -Leaf $OutputPath)
    $resolvedStatus = Join-Path $statusDirectory (Split-Path -Leaf $StatusPath)
    $script:StatusPath = $resolvedStatus

    if (Test-Path -LiteralPath $resolvedOutput) {
        Remove-Item -LiteralPath $resolvedOutput -Force
    }

    Write-NormalizeStatus -Stage 'starting' -Message 'Starting Microsoft Word.'
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $word.AutomationSecurity = 3
    $word.ScreenUpdating = $false
    $word.Options.UpdateLinksAtOpen = $false
    Write-NormalizeStatus -Stage 'word_started' -Message 'Word started; opening with repair enabled.'

    $missing = [Type]::Missing
    $document = $word.Documents.Open(
        $resolvedSource,
        $false,
        $true,
        $false,
        $missing,
        $missing,
        $false,
        $missing,
        $missing,
        $missing,
        $missing,
        $false,
        $true
    )
    Write-NormalizeStatus -Stage 'document_opened' -Message 'Document opened; saving a normalized DOCX copy.'

    $document.SaveAs2($resolvedOutput, 16)
    Write-NormalizeStatus -Stage 'completed' -Message 'Normalized DOCX saved.'
}
catch {
    $failureMessage = "{0} | {1}" -f $_.Exception.Message, $_.ScriptStackTrace
    Write-NormalizeStatus -Stage 'failed' -Message $failureMessage
    exit 1
}
finally {
    if ($null -ne $document) {
        $document.Close($false)
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($document) | Out-Null
    }
    if ($null -ne $word) {
        $word.Quit()
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word) | Out-Null
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
