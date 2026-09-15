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

function Assert-Equal {
    param(
        $Expected,
        $Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message 期望值：$Expected；实际值：$Actual。"
    }
}

$queueSourcePath = Join-Path $PSScriptRoot '..\ResourceManagement\BoundedPriorityWorkQueue.cs'
if (-not (Test-Path -LiteralPath $queueSourcePath)) {
    throw '缺少有界优先资源队列实现。'
}

$queueSource = Get-Content -LiteralPath $queueSourcePath -Raw -Encoding UTF8
$testHarnessSource = @'
namespace TDJS_Vision.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using TDJS_Vision.ResourceManagement;

    public sealed class FakeResourceItem : IPrioritizedResourceWorkItem
    {
        private int _disposed;

        public static int DisposeCount;
        public static int DoubleDisposeCount;

        public int Id { get; set; }
        public bool IsHighPriority { get; set; }
        public long EstimatedBytes { get; set; }
        public string OwnerKey { get; set; }
        public bool IsDisposed { get { return Volatile.Read(ref _disposed) == 1; } }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                Interlocked.Increment(ref DoubleDisposeCount);
                return;
            }

            Interlocked.Increment(ref DisposeCount);
        }

        public static void ResetCounters()
        {
            DisposeCount = 0;
            DoubleDisposeCount = 0;
        }
    }

    public sealed class QueueStressResult
    {
        public int ProducedCount { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int EvictedCount { get; set; }
        public int ConsumedCount { get; set; }
        public int DisposedCount { get; set; }
        public int DoubleDisposeCount { get; set; }
        public int PeakQueueCount { get; set; }
        public long PeakQueuedBytes { get; set; }
        public int PeakConsumerConcurrency { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }

    public static class QueueStressHarness
    {
        public static int BeforeAcceptCount;

        public static long ObservedEvictedBytes;

        public static void CountBeforeAccept()
        {
            Interlocked.Increment(ref BeforeAcceptCount);
        }

        public static bool RunThrowingEvictionHandlers()
        {
            FakeResourceItem.ResetCounters();
            BoundedPriorityWorkQueue<FakeResourceItem> queue =
                new BoundedPriorityWorkQueue<FakeResourceItem>(
                    1,
                    100,
                    exception => { throw new InvalidOperationException("模拟异常观察器失败"); },
                    (item, estimatedBytes) => { throw new InvalidOperationException("模拟异步回收投递失败"); });
            FakeResourceItem normal = new FakeResourceItem { EstimatedBytes = 20, OwnerKey = "普通" };
            FakeResourceItem high = new FakeResourceItem { EstimatedBytes = 20, OwnerKey = "NG", IsHighPriority = true };
            if (!queue.TryEnqueue(normal).Accepted || !queue.TryEnqueue(high).Accepted)
                return false;

            FakeResourceItem taken;
            if (!queue.TryTake(out taken))
                return false;
            taken.Dispose();
            queue.Dispose();
            return FakeResourceItem.DisposeCount == 2 && FakeResourceItem.DoubleDisposeCount == 0;
        }

        public static bool RunFrozenEvictionBytes()
        {
            FakeResourceItem.ResetCounters();
            Interlocked.Exchange(ref ObservedEvictedBytes, 0L);
            BoundedPriorityWorkQueue<FakeResourceItem> queue =
                new BoundedPriorityWorkQueue<FakeResourceItem>(
                    1,
                    100,
                    null,
                    (item, estimatedBytes) =>
                    {
                        Interlocked.Exchange(ref ObservedEvictedBytes, estimatedBytes);
                        item.Dispose();
                    });
            FakeResourceItem normal = new FakeResourceItem { EstimatedBytes = 20, OwnerKey = "普通" };
            if (!queue.TryEnqueue(normal).Accepted)
                return false;

            normal.EstimatedBytes = 1000;
            FakeResourceItem high = new FakeResourceItem { EstimatedBytes = 20, OwnerKey = "NG", IsHighPriority = true };
            if (!queue.TryEnqueue(high).Accepted)
                return false;

            queue.CompleteAndDisposePending();
            queue.Dispose();
            return Interlocked.Read(ref ObservedEvictedBytes) == 20L &&
                FakeResourceItem.DisposeCount == 2 &&
                FakeResourceItem.DoubleDisposeCount == 0;
        }

        public static QueueStressResult RunConcurrentCancelAndComplete(int producerCount, int itemsPerProducer)
        {
            FakeResourceItem.ResetCounters();
            BoundedPriorityWorkQueue<FakeResourceItem> queue =
                new BoundedPriorityWorkQueue<FakeResourceItem>(32, 32768);
            int accepted = 0;
            int rejected = 0;
            int canceling = 1;
            Task cancelTask = Task.Run(() =>
            {
                while (Volatile.Read(ref canceling) == 1)
                {
                    for (int owner = 0; owner < producerCount; owner++)
                        queue.CancelOwner("并发流程" + owner);
                    Thread.Yield();
                }
            });

            Task[] producers = new Task[producerCount];
            for (int producer = 0; producer < producerCount; producer++)
            {
                int owner = producer;
                producers[producer] = Task.Run(() =>
                {
                    for (int index = 0; index < itemsPerProducer; index++)
                    {
                        FakeResourceItem item = new FakeResourceItem
                        {
                            EstimatedBytes = 1024,
                            OwnerKey = "并发流程" + owner,
                            IsHighPriority = index % 10 == 0
                        };
                        BoundedQueueAdmissionResult admission = queue.TryEnqueue(item);
                        if (admission.Accepted)
                            Interlocked.Increment(ref accepted);
                        else
                        {
                            Interlocked.Increment(ref rejected);
                            item.Dispose();
                        }

                        if (index == itemsPerProducer / 2 && owner == 0)
                            queue.CompleteAdding();
                    }
                });
            }

            Task.WaitAll(producers);
            Volatile.Write(ref canceling, 0);
            if (!cancelTask.Wait(30000))
                throw new TimeoutException("并发取消任务未能退出。");
            queue.CompleteAndDisposePending();
            QueueStressResult result = new QueueStressResult
            {
                ProducedCount = producerCount * itemsPerProducer,
                AcceptedCount = accepted,
                RejectedCount = rejected,
                DisposedCount = FakeResourceItem.DisposeCount,
                DoubleDisposeCount = FakeResourceItem.DoubleDisposeCount,
                PeakQueueCount = queue.PeakCount,
                PeakQueuedBytes = queue.PeakQueuedBytes
            };
            queue.Dispose();
            return result;
        }

        public static QueueStressResult Run(int producerCount, int itemsPerProducer)
        {
            const int capacity = 8;
            const long memoryBudgetBytes = 8192;
            FakeResourceItem.ResetCounters();
            BoundedPriorityWorkQueue<FakeResourceItem> queue =
                new BoundedPriorityWorkQueue<FakeResourceItem>(capacity, memoryBudgetBytes);
            int accepted = 0;
            int rejected = 0;
            int evicted = 0;
            int consumed = 0;
            int activeConsumers = 0;
            int peakConsumers = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            Task[] consumers = new Task[2];
            for (int index = 0; index < consumers.Length; index++)
            {
                consumers[index] = Task.Run(() =>
                {
                    FakeResourceItem item;
                    while (queue.TryTake(out item))
                    {
                        int active = Interlocked.Increment(ref activeConsumers);
                        UpdateMaximum(ref peakConsumers, active);
                        Thread.SpinWait(1000);
                        item.Dispose();
                        Interlocked.Increment(ref consumed);
                        Interlocked.Decrement(ref activeConsumers);
                    }
                });
            }

            Task[] producers = new Task[producerCount];
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int capturedProducer = producerIndex;
                producers[producerIndex] = Task.Run(() =>
                {
                    for (int itemIndex = 0; itemIndex < itemsPerProducer; itemIndex++)
                    {
                        FakeResourceItem item = new FakeResourceItem
                        {
                            Id = capturedProducer * itemsPerProducer + itemIndex,
                            IsHighPriority = itemIndex % 10 == 0,
                            EstimatedBytes = 1024,
                            OwnerKey = "流程" + capturedProducer
                        };
                        BoundedQueueAdmissionResult result = queue.TryEnqueue(item);
                        if (result.Accepted)
                        {
                            Interlocked.Increment(ref accepted);
                            Interlocked.Add(ref evicted, result.EvictedLowPriorityCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref rejected);
                            item.Dispose();
                        }
                    }
                });
            }

            Task.WaitAll(producers);
            queue.CompleteAdding();
            if (!Task.WaitAll(consumers, 30000))
                throw new TimeoutException("慢消费者未能在30秒内排空并退出。");
            stopwatch.Stop();

            QueueStressResult resultSnapshot = new QueueStressResult
            {
                ProducedCount = producerCount * itemsPerProducer,
                AcceptedCount = accepted,
                RejectedCount = rejected,
                EvictedCount = evicted,
                ConsumedCount = consumed,
                DisposedCount = FakeResourceItem.DisposeCount,
                DoubleDisposeCount = FakeResourceItem.DoubleDisposeCount,
                PeakQueueCount = queue.PeakCount,
                PeakQueuedBytes = queue.PeakQueuedBytes,
                PeakConsumerConcurrency = peakConsumers,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
            queue.Dispose();
            return resultSnapshot;
        }

        private static void UpdateMaximum(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
'@

Add-Type -TypeDefinition ($queueSource + [Environment]::NewLine + $testHarnessSource) -Language CSharp

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$queue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 3, 100
$normal1 = New-Object TDJS_Vision.Tests.FakeResourceItem
$normal1.Id = 1; $normal1.EstimatedBytes = 20; $normal1.OwnerKey = '流程1'
$normal2 = New-Object TDJS_Vision.Tests.FakeResourceItem
$normal2.Id = 2; $normal2.EstimatedBytes = 20; $normal2.OwnerKey = '流程1'
$normal3 = New-Object TDJS_Vision.Tests.FakeResourceItem
$normal3.Id = 3; $normal3.EstimatedBytes = 20; $normal3.OwnerKey = '流程2'
Assert-True $queue.TryEnqueue($normal1).Accepted '第一个普通任务应进入空队列。'
Assert-True $queue.TryEnqueue($normal2).Accepted '第二个普通任务应进入队列。'
Assert-True $queue.TryEnqueue($normal3).Accepted '第三个普通任务应进入队列。'

$rejectedNormal = New-Object TDJS_Vision.Tests.FakeResourceItem
$rejectedNormal.Id = 4; $rejectedNormal.EstimatedBytes = 20; $rejectedNormal.OwnerKey = '流程2'
$rejectedResult = $queue.TryEnqueue($rejectedNormal)
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::RejectedFull) $rejectedResult.Status '满队列必须立即拒绝新普通任务。'
Assert-True (-not $rejectedNormal.IsDisposed) '被拒绝任务仍应由调用方持有。'
$rejectedNormal.Dispose()

$ngItem = New-Object TDJS_Vision.Tests.FakeResourceItem
$ngItem.Id = 5; $ngItem.IsHighPriority = $true; $ngItem.EstimatedBytes = 20; $ngItem.OwnerKey = '流程3'
$ngResult = $queue.TryEnqueue($ngItem)
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::AcceptedAfterEviction) $ngResult.Status 'NG任务应通过淘汰最早普通任务进入满队列。'
Assert-Equal 1 $ngResult.EvictedLowPriorityCount '容量边界只需淘汰一个普通任务。'
Assert-True $normal1.IsDisposed '最早普通任务被淘汰时必须立即释放。'
Assert-Equal 3 $queue.Count 'NG替换普通任务后仍不得超过容量。'

$takeItem = $null
Assert-True $queue.TryTake([ref]$takeItem) '应能取得剩余最早任务。'
Assert-Equal 5 $takeItem.Id 'NG进入队列后必须先于普通任务被消费。'
$takeItem.Dispose()
Assert-Equal 1 $queue.CancelOwner('流程2') '按节点取消应释放该节点全部等待任务。'
Assert-Equal 1 $queue.Count '按节点取消不得误删其他节点的等待任务。'
Assert-Equal 1 $queue.CompleteAndDisposePending() '停止时应释放其他节点剩余任务。'
Assert-True (-not $queue.TryTake([ref]$takeItem)) '停止且排空后消费者应正常退出。'
$queue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$byteQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 8, 100
$byteNormal1 = New-Object TDJS_Vision.Tests.FakeResourceItem
$byteNormal1.EstimatedBytes = 40; $byteNormal1.OwnerKey = '字节流程1'
$byteNormal2 = New-Object TDJS_Vision.Tests.FakeResourceItem
$byteNormal2.EstimatedBytes = 40; $byteNormal2.OwnerKey = '字节流程2'
$byteQueue.TryEnqueue($byteNormal1) | Out-Null
$byteQueue.TryEnqueue($byteNormal2) | Out-Null
$largeNg = New-Object TDJS_Vision.Tests.FakeResourceItem
$largeNg.IsHighPriority = $true; $largeNg.EstimatedBytes = 70; $largeNg.OwnerKey = '字节流程3'
$largeNgResult = $byteQueue.TryEnqueue($largeNg)
Assert-True $largeNgResult.Accepted '字节预算满时NG任务应淘汰足够数量的普通任务。'
Assert-Equal 2 $largeNgResult.EvictedLowPriorityCount '70字节NG任务应淘汰两个40字节普通任务。'
Assert-Equal 70 $byteQueue.QueuedBytes '淘汰后累计字节数必须精确回落。'

$oversized = New-Object TDJS_Vision.Tests.FakeResourceItem
$oversized.IsHighPriority = $true; $oversized.EstimatedBytes = 101; $oversized.OwnerKey = '超大图'
$oversizedResult = $byteQueue.TryEnqueue($oversized)
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::RejectedItemExceedsBudget) $oversizedResult.Status '单图超过总预算时即使是NG也必须拒绝。'
$oversized.Dispose()
Assert-Equal 1 $byteQueue.CompleteAndDisposePending() '停止时应释放唯一等待任务。'
$byteQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$ngOnlyQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 2, 100
$earlyNg1 = New-Object TDJS_Vision.Tests.FakeResourceItem
$earlyNg1.Id = 11; $earlyNg1.IsHighPriority = $true; $earlyNg1.EstimatedBytes = 20; $earlyNg1.OwnerKey = '全NG'
$earlyNg2 = New-Object TDJS_Vision.Tests.FakeResourceItem
$earlyNg2.Id = 12; $earlyNg2.IsHighPriority = $true; $earlyNg2.EstimatedBytes = 20; $earlyNg2.OwnerKey = '全NG'
$newNg = New-Object TDJS_Vision.Tests.FakeResourceItem
$newNg.Id = 13; $newNg.IsHighPriority = $true; $newNg.EstimatedBytes = 20; $newNg.OwnerKey = '全NG'
$ngOnlyQueue.TryEnqueue($earlyNg1) | Out-Null
$ngOnlyQueue.TryEnqueue($earlyNg2) | Out-Null
$allNgFullResult = $ngOnlyQueue.TryEnqueue($newNg)
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::RejectedFull) $allNgFullResult.Status '队列全是NG时不得用新NG挤掉更早的NG。'
Assert-True (-not $newNg.IsDisposed) '全NG满队列拒绝的新任务仍由调用方释放。'
$newNg.Dispose()
Assert-Equal 2 $ngOnlyQueue.CompleteAndDisposePending() '全NG队列停止时必须释放两个已接收任务。'
$ngOnlyQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
[TDJS_Vision.Tests.QueueStressHarness]::BeforeAcceptCount = 0
$callbackQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 1, 100
$callbackAccepted = New-Object TDJS_Vision.Tests.FakeResourceItem
$callbackAccepted.EstimatedBytes = 20; $callbackAccepted.OwnerKey = '回调流程'
$callbackRejected = New-Object TDJS_Vision.Tests.FakeResourceItem
$callbackRejected.EstimatedBytes = 20; $callbackRejected.OwnerKey = '回调流程'
$beforeAccept = [Action]{ [TDJS_Vision.Tests.QueueStressHarness]::CountBeforeAccept() }
Assert-True $callbackQueue.TryEnqueue($callbackAccepted, $beforeAccept).Accepted '空队列必须接收带接收前回调的任务。'
$callbackRejectedResult = $callbackQueue.TryEnqueue($callbackRejected, $beforeAccept)
Assert-True (-not $callbackRejectedResult.Accepted) '满队列必须拒绝第二个普通任务。'
Assert-Equal 1 ([TDJS_Vision.Tests.QueueStressHarness]::BeforeAcceptCount) '接收前回调只能对真正接管的任务执行。'
$callbackRejected.Dispose()
$callbackQueue.CompleteAndDisposePending() | Out-Null
$callbackQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$updatedQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 2, 100
$updatedAccepted = New-Object TDJS_Vision.Tests.FakeResourceItem
$updatedAccepted.EstimatedBytes = 20; $updatedAccepted.OwnerKey = '实际字节流程'
$updatedAcceptedResult = $updatedQueue.TryEnqueueWithUpdatedEstimate($updatedAccepted, [System.Func[long]]{ 80 })
Assert-True $updatedAcceptedResult.Accepted '实际字节80未超预算时必须正常接收。'
Assert-Equal 80 $updatedQueue.QueuedBytes '队列必须按回调返回的实际字节记账。'
$updatedRejected = New-Object TDJS_Vision.Tests.FakeResourceItem
$updatedRejected.EstimatedBytes = 10; $updatedRejected.OwnerKey = '实际字节流程'
$updatedRejectedResult = $updatedQueue.TryEnqueueWithUpdatedEstimate($updatedRejected, [System.Func[long]]{ 30 })
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::RejectedFull) $updatedRejectedResult.Status '实际字节使累计预算超限时必须返回明确满队列拒绝。'
Assert-Equal 80 $updatedQueue.QueuedBytes '实际字节拒绝不能改变已有队列记账。'
$updatedRejected.Dispose()
$updatedOversized = New-Object TDJS_Vision.Tests.FakeResourceItem
$updatedOversized.EstimatedBytes = 10; $updatedOversized.OwnerKey = '实际字节流程'
$updatedOversizedResult = $updatedQueue.TryEnqueueWithUpdatedEstimate($updatedOversized, [System.Func[long]]{ 101 })
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::RejectedItemExceedsBudget) $updatedOversizedResult.Status '实际单项字节超过总预算时必须返回明确超预算拒绝。'
$updatedOversized.Dispose()
$updatedQueue.CompleteAndDisposePending() | Out-Null
$updatedQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$metadataQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 1, 100
$metadataItem = New-Object TDJS_Vision.Tests.FakeResourceItem
$metadataItem.EstimatedBytes = 20; $metadataItem.OwnerKey = '回调前所有者'
$mutateMetadata = [Action]{ $metadataItem.OwnerKey = '回调后所有者'; $metadataItem.IsHighPriority = $true }
Assert-True $metadataQueue.TryEnqueue($metadataItem, $mutateMetadata).Accepted '元数据冻结测试任务必须成功入队。'
Assert-Equal 1 $metadataQueue.CancelOwner('回调前所有者') '旧Action回调不得改变入队前已经冻结的所有者。'
$metadataQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$priorityMetadataQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 1, 100
$priorityMetadataItem = New-Object TDJS_Vision.Tests.FakeResourceItem
$priorityMetadataItem.EstimatedBytes = 20; $priorityMetadataItem.OwnerKey = '优先级冻结流程'
$raisePriority = [Action]{ $priorityMetadataItem.IsHighPriority = $true }
Assert-True $priorityMetadataQueue.TryEnqueue($priorityMetadataItem, $raisePriority).Accepted '优先级冻结测试任务必须成功入队。'
$priorityReplacement = New-Object TDJS_Vision.Tests.FakeResourceItem
$priorityReplacement.EstimatedBytes = 20; $priorityReplacement.OwnerKey = '优先级冻结流程'; $priorityReplacement.IsHighPriority = $true
$priorityReplacementResult = $priorityMetadataQueue.TryEnqueue($priorityReplacement)
Assert-True ($priorityReplacementResult.Accepted -and $priorityReplacementResult.EvictedLowPriorityCount -eq 1) '旧Action回调不得把入队前冻结的普通任务改成NG任务。'
$priorityMetadataQueue.CompleteAndDisposePending() | Out-Null
$priorityMetadataQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$updatedHighGrowQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 3, 100
$growNormal1 = New-Object TDJS_Vision.Tests.FakeResourceItem
$growNormal1.EstimatedBytes = 30; $growNormal1.OwnerKey = 'NG增大流程'
$growNormal2 = New-Object TDJS_Vision.Tests.FakeResourceItem
$growNormal2.EstimatedBytes = 30; $growNormal2.OwnerKey = 'NG增大流程'
$updatedHighGrowQueue.TryEnqueue($growNormal1) | Out-Null
$updatedHighGrowQueue.TryEnqueue($growNormal2) | Out-Null
$growHigh = New-Object TDJS_Vision.Tests.FakeResourceItem
$growHigh.EstimatedBytes = 10; $growHigh.OwnerKey = 'NG增大流程'; $growHigh.IsHighPriority = $true
$growHighResult = $updatedHighGrowQueue.TryEnqueueWithUpdatedEstimate($growHigh, [System.Func[long]]{ 50 })
Assert-True ($growHighResult.Accepted -and $growHighResult.EvictedLowPriorityCount -eq 1) 'NG实际字节增大后必须按最终值重新计算淘汰数量。'
Assert-Equal 80 $updatedHighGrowQueue.QueuedBytes 'NG实际字节增大后的队列记账必须准确。'
$updatedHighGrowQueue.CompleteAndDisposePending() | Out-Null
$updatedHighGrowQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$updatedHighShrinkQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 3, 100
$shrinkNormal1 = New-Object TDJS_Vision.Tests.FakeResourceItem
$shrinkNormal1.EstimatedBytes = 40; $shrinkNormal1.OwnerKey = 'NG减小流程'
$shrinkNormal2 = New-Object TDJS_Vision.Tests.FakeResourceItem
$shrinkNormal2.EstimatedBytes = 40; $shrinkNormal2.OwnerKey = 'NG减小流程'
$updatedHighShrinkQueue.TryEnqueue($shrinkNormal1) | Out-Null
$updatedHighShrinkQueue.TryEnqueue($shrinkNormal2) | Out-Null
$shrinkHigh = New-Object TDJS_Vision.Tests.FakeResourceItem
$shrinkHigh.EstimatedBytes = 50; $shrinkHigh.OwnerKey = 'NG减小流程'; $shrinkHigh.IsHighPriority = $true
$shrinkHighResult = $updatedHighShrinkQueue.TryEnqueueWithUpdatedEstimate($shrinkHigh, [System.Func[long]]{ 20 })
Assert-True ($shrinkHighResult.Accepted -and $shrinkHighResult.EvictedLowPriorityCount -eq 0) 'NG实际字节减小时不得按旧缓存过度淘汰普通任务。'
Assert-Equal 100 $updatedHighShrinkQueue.QueuedBytes 'NG实际字节减小后的队列记账必须准确。'
$updatedHighShrinkQueue.CompleteAndDisposePending() | Out-Null
$updatedHighShrinkQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$updatedHighInsufficientQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 3, 100
$insufficientNormal = New-Object TDJS_Vision.Tests.FakeResourceItem
$insufficientNormal.EstimatedBytes = 30; $insufficientNormal.OwnerKey = 'NG不足流程'
$insufficientExistingHigh = New-Object TDJS_Vision.Tests.FakeResourceItem
$insufficientExistingHigh.EstimatedBytes = 60; $insufficientExistingHigh.OwnerKey = 'NG不足流程'; $insufficientExistingHigh.IsHighPriority = $true
$updatedHighInsufficientQueue.TryEnqueue($insufficientNormal) | Out-Null
$updatedHighInsufficientQueue.TryEnqueue($insufficientExistingHigh) | Out-Null
$insufficientIncomingHigh = New-Object TDJS_Vision.Tests.FakeResourceItem
$insufficientIncomingHigh.EstimatedBytes = 10; $insufficientIncomingHigh.OwnerKey = 'NG不足流程'; $insufficientIncomingHigh.IsHighPriority = $true
$insufficientHighResult = $updatedHighInsufficientQueue.TryEnqueueWithUpdatedEstimate($insufficientIncomingHigh, [System.Func[long]]{ 50 })
Assert-Equal ([TDJS_Vision.ResourceManagement.BoundedQueueAdmissionStatus]::RejectedFull) $insufficientHighResult.Status '普通任务全部淘汰后仍不足时必须拒绝增大的NG任务。'
Assert-Equal 90 $updatedHighInsufficientQueue.QueuedBytes 'NG淘汰不足拒绝不能部分移除已有普通任务。'
$insufficientIncomingHigh.Dispose()
$updatedHighInsufficientQueue.CompleteAndDisposePending() | Out-Null
$updatedHighInsufficientQueue.Dispose()

[TDJS_Vision.Tests.FakeResourceItem]::ResetCounters()
$snapshotQueue = New-Object 'TDJS_Vision.ResourceManagement.BoundedPriorityWorkQueue[TDJS_Vision.Tests.FakeResourceItem]' -ArgumentList 2, 100
$snapshotItem = New-Object TDJS_Vision.Tests.FakeResourceItem
$snapshotItem.EstimatedBytes = 20; $snapshotItem.OwnerKey = '冻结所有者'
Assert-True $snapshotQueue.TryEnqueue($snapshotItem).Accepted '快照测试任务必须成功入队。'
$snapshotItem.EstimatedBytes = 1000
$snapshotItem.OwnerKey = '篡改所有者'
$snapshotItem.IsHighPriority = $true
Assert-Equal 20 $snapshotQueue.QueuedBytes '任务入队后外部修改字节属性不得破坏队列记账。'
Assert-Equal 1 $snapshotQueue.CancelOwner('冻结所有者') '定向取消必须使用入队瞬间冻结的所有者。'
Assert-Equal 0 $snapshotQueue.QueuedBytes '取消冻结任务后队列字节必须归零。'
$snapshotQueue.Dispose()

Assert-True ([TDJS_Vision.Tests.QueueStressHarness]::RunThrowingEvictionHandlers()) '淘汰处理器和异常观察器同时抛错时仍必须回退释放，且不得破坏队列。'
Assert-True ([TDJS_Vision.Tests.QueueStressHarness]::RunFrozenEvictionBytes()) '淘汰回收必须使用任务入队瞬间冻结的字节值。'

$concurrentLifecycle = [TDJS_Vision.Tests.QueueStressHarness]::RunConcurrentCancelAndComplete(4, 5000)
Assert-Equal $concurrentLifecycle.ProducedCount ($concurrentLifecycle.AcceptedCount + $concurrentLifecycle.RejectedCount) '并发取消和停止期间每个任务必须得到明确结果。'
Assert-Equal $concurrentLifecycle.ProducedCount $concurrentLifecycle.DisposedCount '并发取消和停止期间全部任务必须恰好释放一次。'
Assert-Equal 0 $concurrentLifecycle.DoubleDisposeCount '并发取消和停止期间不得重复释放。'
Assert-True ($concurrentLifecycle.PeakQueueCount -le 32 -and $concurrentLifecycle.PeakQueuedBytes -le 32768) '并发取消和停止不得突破任务或字节边界。'

$stress = [TDJS_Vision.Tests.QueueStressHarness]::Run(4, 25000)
Assert-Equal 100000 $stress.ProducedCount '四生产者压力任务总数不正确。'
Assert-Equal $stress.ProducedCount ($stress.AcceptedCount + $stress.RejectedCount) '每个生产任务必须得到明确入队结果。'
Assert-Equal $stress.ProducedCount $stress.DisposedCount '所有接收、淘汰、拒绝和消费任务都必须恰好释放一次。'
Assert-Equal 0 $stress.DoubleDisposeCount '压力测试不允许重复释放任务。'
Assert-True ($stress.PeakQueueCount -le 8) '并发压力下队列任务峰值不得超过容量8。'
Assert-True ($stress.PeakQueuedBytes -le 8192) '并发压力下队列字节峰值不得超过8192字节。'
Assert-True ($stress.PeakConsumerConcurrency -le 2) '后台消费并发不得超过固定工作线程数2。'

Write-Host ("有界保存队列压力通过：生产={0}；接收={1}；拒绝={2}；淘汰普通={3}；实际消费={4}；队列峰值={5}/8；字节峰值={6}/8192；消费并发峰值={7}/2；总耗时={8}ms；释放={9}；重复释放={10}" -f `
    $stress.ProducedCount,
    $stress.AcceptedCount,
    $stress.RejectedCount,
    $stress.EvictedCount,
    $stress.ConsumedCount,
    $stress.PeakQueueCount,
    $stress.PeakQueuedBytes,
    $stress.PeakConsumerConcurrency,
    $stress.ElapsedMilliseconds,
    $stress.DisposedCount,
    $stress.DoubleDisposeCount)
