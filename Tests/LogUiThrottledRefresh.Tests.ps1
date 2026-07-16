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

Write-Host 'Log UI bounded buffer behavior checks passed.'
