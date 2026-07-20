$ErrorActionPreference = 'Stop'

$runnerPath = Join-Path $PSScriptRoot '..\Node\4-Measurement\Common\MultiTargetMeasurementRunner.cs'
if (-not (Test-Path -LiteralPath $runnerPath)) {
    throw "Multi-target measurement runner production file does not exist: $runnerPath"
}

$runnerBody = Get-Content -LiteralPath $runnerPath -Raw -Encoding UTF8
$runnerBody = $runnerBody -replace '(?m)^using [^;]+;\r?\n', ''
$source = @'
using System;
using System.Collections.Generic;
using System.Threading;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    public class PositionCorrectionInfo
    {
        public int TargetIndex { get; set; }
    }
}
'@ + [Environment]::NewLine + $runnerBody + [Environment]::NewLine + @'
namespace TDJS_Vision.Node._4_Measurement.Common
{
    public sealed class RunnerTestItem : MultiTargetMeasurementItemBase
    {
        public double Value { get; set; }
    }

    public static class RunnerBehaviorHarness
    {
        public static void Verify()
        {
            List<PositionCorrectionInfo> corrections = new List<PositionCorrectionInfo>();
            corrections.Add(new PositionCorrectionInfo { TargetIndex = 1 });
            corrections.Add(new PositionCorrectionInfo { TargetIndex = 2 });
            corrections.Add(new PositionCorrectionInfo { TargetIndex = 3 });
            int executeCount = 0;

            List<RunnerTestItem> items = MultiTargetMeasurementRunner.Run(
                corrections,
                CancellationToken.None,
                correction =>
                {
                    executeCount++;
                    if (correction.TargetIndex == 2)
                        throw new InvalidOperationException("expected failure");

                    return new RunnerTestItem
                    {
                        IsOk = true,
                        Value = correction.TargetIndex * 10.0
                    };
                },
                (correction, exception) => new RunnerTestItem
                {
                    IsOk = false,
                    Value = 0.0,
                    ErrorMessage = exception.Message
                });

            Assert(items.Count == 3, "The output count must equal the correction count.");
            Assert(executeCount == 3, "A normal item failure must not stop later targets.");
            Assert(items[0].TargetIndex == 1 && items[1].TargetIndex == 2 && items[2].TargetIndex == 3, "Target order must be preserved.");
            Assert(items[0].IsOk, "The first target must succeed.");
            Assert(!items[1].IsOk && items[1].Value == 0.0, "The second target must be a zero-valued failure item.");
            Assert(items[2].IsOk && items[2].Value == 30.0, "The third target must still execute.");
            Assert(object.ReferenceEquals(items[2].Correction, corrections[2]), "The runner must write the source correction back to each item.");

            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool cancellationThrown = false;
            try
            {
                MultiTargetMeasurementRunner.Run(
                    corrections,
                    cancellation.Token,
                    correction => new RunnerTestItem(),
                    (correction, exception) => new RunnerTestItem());
            }
            catch (OperationCanceledException)
            {
                cancellationThrown = true;
            }

            Assert(cancellationThrown, "Cancellation must be rethrown instead of becoming a failure item.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
'@

Add-Type -TypeDefinition $source -Language CSharp
[TDJS_Vision.Node._4_Measurement.Common.RunnerBehaviorHarness]::Verify()

Write-Host 'Multi-target measurement runner behavior checks passed.'
