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

$projectRoot = Split-Path -Parent $PSScriptRoot
$bufferPath = Join-Path $projectRoot 'Forms\Logger\LogUiBuffer.cs'

Assert-True (Test-Path -LiteralPath $bufferPath) 'Missing Forms\Logger\LogUiBuffer.cs.'

$bufferSource = Get-Content -Raw -Encoding UTF8 -LiteralPath $bufferPath
$testHarness = @'

namespace Logger
{
    public enum MsgLevel
    {
        Debug,
        Info,
        Warn,
        Exception,
        Fatal
    }

    public static class LogUiBufferBehaviorChecks
    {
        public static void Run()
        {
            BoundedLogUiBuffer buffer = new BoundedLogUiBuffer(3);
            for (int index = 1; index <= 5; index++)
            {
                buffer.Enqueue(new LogUiEntry(MsgLevel.Info, "log" + index));
            }

            if (buffer.Count != 3)
                throw new System.Exception("The buffer exceeded its capacity.");

            System.Collections.Generic.IReadOnlyList<LogUiEntry> batch = buffer.DequeueBatch(2);
            if (batch.Count != 2 || batch[0].Info != "log3" || batch[1].Info != "log4")
                throw new System.Exception("The buffer did not retain the latest entries in order.");
            if (buffer.Count != 1)
                throw new System.Exception("The batch size was incorrect.");

            buffer.Clear();
            if (buffer.Count != 0)
                throw new System.Exception("Clear did not empty the buffer.");

            BoundedLogUiBuffer concurrentBuffer = new BoundedLogUiBuffer(2000);
            System.Threading.Tasks.Parallel.For(
                0,
                10000,
                index => concurrentBuffer.Enqueue(new LogUiEntry(MsgLevel.Debug, index.ToString())));
            if (concurrentBuffer.Count < 0 || concurrentBuffer.Count > 2000)
                throw new System.Exception("Concurrent writes exceeded the capacity.");
        }
    }
}
'@

Add-Type -TypeDefinition ($bufferSource + $testHarness) -ReferencedAssemblies @('System.dll', 'System.Core.dll')
[Logger.LogUiBufferBehaviorChecks]::Run()

$helperPath = Join-Path $projectRoot 'Forms\Logger\LogHelper.cs'
$designerPath = Join-Path $projectRoot 'Forms\Logger\LogHelper.Designer.cs'
$helperSource = Get-Content -Raw -Encoding UTF8 -LiteralPath $helperPath
$designerSource = Get-Content -Raw -Encoding UTF8 -LiteralPath $designerPath

Assert-True ($helperSource.Contains('new BoundedLogUiBuffer(2000)')) 'The UI buffer capacity must be 2000.'
Assert-True ($helperSource.Contains('_logUiBuffer.Enqueue')) 'The log event must enqueue UI entries.'
Assert-True (-not $helperSource.Contains('TDJS_Vision.Solution.Instance.IsRunning')) 'UI scheduling must not depend on solution run state.'
Assert-True (-not $helperSource.Contains('this.BeginInvoke')) 'High-frequency logs must not enqueue one UI delegate per entry.'
Assert-True ($helperSource.Contains('const int UiRefreshBatchSize = 100')) 'The UI refresh batch size must be 100.'
Assert-True ($designerSource.Contains('this.logRefreshTimer.Interval = 100;')) 'The designer timer interval must be 100ms.'
Assert-True ($designerSource.Contains('this.logRefreshTimer.Tick +=')) 'The designer timer must bind the refresh event.'
Assert-True ($helperSource.Contains('LogAddEvent -= LogHelper_LogAddEvent;')) 'The static log event must be detached when the control is disposed.'
Assert-True ($helperSource.Contains('_logUiBuffer.Clear();')) 'Clearing the display must clear pending UI logs.'
Assert-True ($helperSource.Contains('finally')) 'Batch updates must always resume list rendering.'
Assert-True (-not $helperSource.Contains('if (listBoxAll.Items.Count > 1000)')) 'History limits must trim old entries instead of clearing the full list.'

Write-Host 'Log UI throttled refresh checks passed.'
