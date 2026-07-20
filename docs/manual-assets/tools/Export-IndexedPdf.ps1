<#
.SYNOPSIS
Exports the customer manual to PDF with heading bookmarks.

.DESCRIPTION
Opens the DOCX read-only, suppresses repair dialogs, and writes export stages
to a JSON status file so a long conversion can be monitored without blocking.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$StatusPath,

    [Parameter(Mandatory = $false)]
    [int]$PageFrom = 0,

    [Parameter(Mandatory = $false)]
    [int]$PageTo = 0,

    [Parameter(Mandatory = $false)]
    [string]$ApplicationProgId = 'Word.Application'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-ExportStatus {
    <# Writes the current export stage to a UTF-8 JSON status file. #>
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
    $resolvedOutputDirectory = (Resolve-Path -LiteralPath (Split-Path -Parent $OutputPath)).Path
    $resolvedStatusDirectory = (Resolve-Path -LiteralPath (Split-Path -Parent $StatusPath)).Path
    $resolvedOutput = Join-Path $resolvedOutputDirectory (Split-Path -Leaf $OutputPath)
    $resolvedStatus = Join-Path $resolvedStatusDirectory (Split-Path -Leaf $StatusPath)
    $script:StatusPath = $resolvedStatus

    if (Test-Path -LiteralPath $resolvedOutput) {
        Remove-Item -LiteralPath $resolvedOutput -Force
    }

    Write-ExportStatus -Stage 'starting' -Message "Starting $ApplicationProgId."
    $word = New-Object -ComObject $ApplicationProgId
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $word.AutomationSecurity = 3
    $word.ScreenUpdating = $false
    $word.Options.UpdateLinksAtOpen = $false
    $word.Options.CheckGrammarAsYouType = $false
    $word.Options.CheckSpellingAsYouType = $false
    $word.Options.BackgroundSave = $false
    Write-ExportStatus -Stage 'word_started' -Message 'Word started; opening the document read-only.'

    # OpenAndRepair=true prevents a hidden repair prompt from blocking COM.
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
    $isPageRange = $PageFrom -gt 0 -and $PageTo -ge $PageFrom
    $exportRange = if ($isPageRange) { 3 } else { 0 }
    $exportFrom = if ($isPageRange) { $PageFrom } else { 1 }
    $exportTo = if ($isPageRange) { $PageTo } else { 1 }
    $createBookmarks = if ($isPageRange) { 0 } else { 1 }
    $rangeMessage = if ($isPageRange) { "pages $PageFrom-$PageTo" } else { 'all pages' }
    Write-ExportStatus -Stage 'document_opened' -Message "Document opened; exporting $rangeMessage."

    # CreateBookmarks=1 builds the PDF outline from Heading 1/2/3 styles.
    $document.ExportAsFixedFormat(
        $resolvedOutput,
        17,
        $false,
        0,
        $exportRange,
        $exportFrom,
        $exportTo,
        0,
        $true,
        $true,
        $createBookmarks,
        $true,
        $true,
        $false
    )
    Write-ExportStatus -Stage 'completed' -Message 'PDF export completed.'
}
catch {
    $failureMessage = "{0} | {1}" -f $_.Exception.Message, $_.ScriptStackTrace
    Write-ExportStatus -Stage 'failed' -Message $failureMessage
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
