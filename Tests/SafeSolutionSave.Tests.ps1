$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$configHelperPath = Join-Path $projectRoot 'ConfigHelper.cs'
$configHelperSource = Get-Content -Raw -Encoding UTF8 $configHelperPath

function Assert-Contains {
    param(
        [string]$Content,
        [string]$Expected,
        [string]$Message
    )

    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Content,
        [string]$Unexpected,
        [string]$Message
    )

    if ($Content.Contains($Unexpected)) {
        throw $Message
    }
}

Assert-Contains $configHelperSource 'SafeWriteSolutionFile(solFile, json,' 'Full solution save must use the safe write pipeline.'
Assert-Contains $configHelperSource 'WriteTextWithFlush(temporaryPath, json);' 'Safe save must write a temporary file first.'
Assert-Contains $configHelperSource 'ValidateSolutionTemporaryFile(temporaryPath);' 'Safe save must validate the temporary file before replacement.'
Assert-Contains $configHelperSource 'File.Replace(temporaryPath, targetPath, null, true);' 'Existing solution files must be replaced from a validated temporary file.'
Assert-Contains $configHelperSource 'File.Move(temporaryPath, targetPath);' 'First-time solution saves must move the temporary file into place.'
Assert-Contains $configHelperSource "json.IndexOf('\0') >= 0" 'Temporary file validation must detect null bytes.'
Assert-Contains $configHelperSource 'WriteSolutionSaveFailureLog(operationName, targetPath, temporaryPath, ex);' 'Temporary write, validation, or replacement failures must be logged.'
Assert-Contains $configHelperSource 'MsgLevel.Exception' 'Solution save failures must use exception-level logging.'
Assert-NotContains $configHelperSource 'File.WriteAllText(solFile, json);' 'Solution save must not directly overwrite the active file.'

Assert-Contains $configHelperSource 'return targetPath + ".bak";' 'Backups must be isolated by full solution path.'
Assert-Contains $configHelperSource 'SolConfig = ReadSolutionWithRecovery(solFile);' 'Load must validate and recover before resetting the active solution.'
Assert-Contains $configHelperSource 'File.Replace(temporary, backupPath, null, true);' 'Updating a backup must preserve the old copy until atomic replacement.'
