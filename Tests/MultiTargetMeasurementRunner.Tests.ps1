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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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
            VerifyParallel();
        }

        private static void VerifyParallel()
        {
            FieldInfo automaticDegreeField = typeof(MultiTargetMeasurementRunner).GetField(
                "AutomaticMaxDegreeOfParallelism",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert(automaticDegreeField != null && automaticDegreeField.IsInitOnly,
                "The automatic parallel limit must be cached once in a static readonly field.");
            Assert((int)automaticDegreeField.GetValue(null) == Math.Max(1, Environment.ProcessorCount - 1),
                "The cached automatic limit must reserve one logical processor.");

            var corrections = new List<PositionCorrectionInfo>();
            for (int i = 1; i <= 5; i++)
                corrections.Add(new PositionCorrectionInfo { TargetIndex = i });

            int activeCount = 0;
            int maximumActiveCount = 0;
            int startedCount = 0;
            var firstWave = new CountdownEvent(2);
            var releaseFirstWave = new ManualResetEventSlim(false);

            List<RunnerTestItem> items = MultiTargetMeasurementRunner.RunParallel(
                corrections,
                CancellationToken.None,
                correction =>
                {
                    int active = Interlocked.Increment(ref activeCount);
                    UpdateMaximum(ref maximumActiveCount, active);
                    int started = Interlocked.Increment(ref startedCount);
                    try
                    {
                        if (started <= 2)
                        {
                            if (firstWave.Signal())
                                releaseFirstWave.Set();
                            if (!releaseFirstWave.Wait(2000))
                                throw new TimeoutException("Parallel workers did not overlap.");
                        }

                        if (correction.TargetIndex == 3)
                            throw new InvalidOperationException("expected parallel failure");

                        return new RunnerTestItem
                        {
                            IsOk = true,
                            Value = correction.TargetIndex * 10.0
                        };
                    }
                    finally
                    {
                        Interlocked.Decrement(ref activeCount);
                    }
                },
                (correction, exception) => new RunnerTestItem
                {
                    IsOk = false,
                    Value = 0.0,
                    ErrorMessage = exception.Message
                },
                2);

            Assert(maximumActiveCount == 2, "Parallel execution must overlap without exceeding the configured limit.");
            Assert(items.Count == 5, "Parallel output count must equal the correction count.");
            for (int i = 0; i < items.Count; i++)
                Assert(items[i].TargetIndex == i + 1, "Parallel output order must match correction order.");
            Assert(!items[2].IsOk && items[2].Value == 0.0, "The failed parallel target must retain a zero-valued item.");
            Assert(items[4].IsOk && items[4].Value == 50.0, "A later target must complete after another target fails.");

            int callerThreadId = Thread.CurrentThread.ManagedThreadId;
            int singleWorkerThreadId = 0;
            List<RunnerTestItem> single = MultiTargetMeasurementRunner.RunParallel(
                new List<PositionCorrectionInfo> { new PositionCorrectionInfo { TargetIndex = 1 } },
                CancellationToken.None,
                correction =>
                {
                    singleWorkerThreadId = Thread.CurrentThread.ManagedThreadId;
                    return new RunnerTestItem { IsOk = true, Value = 1.0 };
                },
                (correction, exception) => new RunnerTestItem(),
                4);
            Assert(single.Count == 1 && singleWorkerThreadId == callerThreadId, "A single target must execute directly on the caller thread.");

            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool cancellationThrown = false;
            try
            {
                MultiTargetMeasurementRunner.RunParallel(
                    corrections,
                    cancellation.Token,
                    correction => new RunnerTestItem(),
                    (correction, exception) => new RunnerTestItem(),
                    2);
            }
            catch (OperationCanceledException)
            {
                cancellationThrown = true;
            }
            Assert(cancellationThrown, "Parallel cancellation must propagate instead of becoming a failure item.");
        }

        private static void UpdateMaximum(ref int maximum, int candidate)
        {
            int snapshot;
            do
            {
                snapshot = maximum;
                if (candidate <= snapshot)
                    return;
            }
            while (Interlocked.CompareExchange(ref maximum, candidate, snapshot) != snapshot);
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
