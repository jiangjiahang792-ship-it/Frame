$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)
    return Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\$RelativePath") -Raw -Encoding UTF8
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$queue = Get-ProjectSource 'ResourceManagement\BoundedPriorityWorkQueue.cs'
$saveNode = Get-ProjectSource 'Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs'
$storageGuard = Get-ProjectSource 'ResourceManagement\ImageSaveStorageGuard.cs'
$outputImage = Get-ProjectSource 'Node\1-Acquisition\ImageSource\NodeResultImageSource.cs'
$sampler = Get-ProjectSource 'ResourceManagement\ImageResourceSizeSampler.cs'
$solution = Get-ProjectSource 'Solution.cs'
$nodeBase = Get-ProjectSource 'Node\NodeBase.cs'
$project = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-Contains $queue 'public sealed class BoundedPriorityWorkQueue<T>' '保存等待队列必须使用独立、可压力验证的有界实现。'
Assert-Contains $queue '_items.Count + additionalCount <= _capacity' '保存队列必须限制等待任务数量。'
Assert-Contains $queue 'additionalBytes <= _memoryBudgetBytes - _queuedBytes' '保存队列必须限制实际图像持有字节。'
Assert-Contains $queue 'if (!preliminaryEntry.IsHighPriority)' '普通图片不得在满队列时淘汰已有任务。'
Assert-Contains $queue 'if (node.Value.IsHighPriority)' 'NG图片只能淘汰较早的普通图片。'
Assert-Contains $queue 'QueueEntry incomingEntry = preliminaryEntry.WithEstimatedBytes(admittedItemBytes);' '队列必须只更新实际字节并保留初判前冻结的优先级和所有者。'
Assert-Contains $queue 'TryEnqueueWithUpdatedEstimate(' '两阶段接纳必须允许取得租约后按实际字节重新判定。'
Assert-Contains $queue '_evictedItemHandler(item, entry.EstimatedBytes);' '淘汰回收必须使用入队瞬间冻结的字节值。'
Assert-Contains $queue 'beforeTake?.Invoke();' '消费者必须在任务离开队列前登记活动状态。'
Assert-Contains $queue 'CompleteAdding()' '停止必须先关闭生产端并允许已接收任务正常排空。'
Assert-Contains $queue 'CompleteAndDisposePending()' '停止超时必须能释放剩余等待任务。'

Assert-NotContains $saveNode 'BlockingCollection<SaveImageTask>' '保存节点不得继续使用无界BlockingCollection。'
Assert-NotContains $saveNode 'new ImageQueueProcessor(4' '保存节点不得继续为每个节点硬编码4个消费者。'
Assert-Contains $saveNode 'Solution.Instance.GetOrCreateImageSaveQueueProcessor()' '所有保存节点必须接入全方案共享工作池。'
Assert-Contains $saveNode '_storageGuard.Evaluate(' '保存消费者必须在Mat转Bitmap前执行磁盘准入。'
Assert-Contains $saveNode 'ImageSaveQueueDiagnosticsSnapshot GetDiagnosticsSnapshot()' '保存工作池必须公开不可变诊断快照。'
Assert-Contains $saveNode '【保存队列汇总】' '工作池停止时必须写一次低频保存队列汇总。'
Assert-Contains $storageGuard 'interface IImageSaveDiskSpaceProbe' '磁盘空间探测必须可替换以支持故障压力注入。'
Assert-Contains $storageGuard 'SkippedCriticalSpace' '磁盘保护必须区分严重低空间。'
Assert-Contains $storageGuard 'AllowedHighPriorityUnderLowSpace' '低空间策略必须允许优先保留NG图片。'
Assert-Contains $saveNode 'Solution.Instance.RecordImageSaveResourceSample(saveImageTask.EstimatedBytes);' '保存节点必须记录完整图像租约字节样本。'
Assert-NotContains $saveNode 'IImageResourceLease candidateLease = outputImage.AcquireLease();' '保存队列拒绝前不得预先取得Mat租约。'
Assert-Contains $saveNode '() => outputImage.AcquireSaveSnapshot()' '保存队列必须确认可接收后才原子取得Mat与租约快照。'
Assert-Contains $saveNode '_imageQueue.TryEnqueueWithUpdatedEstimate(' '保存快照取得实际字节后必须由有界队列返回明确接纳或拒绝状态。'
Assert-Contains $outputImage 'GetRetainedImageBytesEstimateForAdmission()' '保存队列预判必须读取不接触Mat的缓存字节估算。'
Assert-Contains $outputImage 'OutputImageSaveSnapshot AcquireSaveSnapshot()' 'OutputImage必须提供原子保存快照接口。'
Assert-Contains $outputImage 'EstimateRetainedImageBytes()' '保存任务必须估算完整图像租约持有的资源。'
Assert-Contains $outputImage 'checked((long)image.Rows * (long)image.Step())' '保存任务必须按Mat真实行跨度估算持有字节。'
Assert-Contains $saveNode 'TaskCreationOptions.LongRunning' '固定保存消费者不得长期占住通用线程池任务。'
Assert-Contains $saveNode '_imageQueue.CompleteAdding();' '工作池停止必须先给已接收任务正常写完的机会。'
Assert-Contains $saveNode 'GetRemainingShutdownMilliseconds(shutdownStopwatch, shutdownTimeoutMilliseconds)' '工作池停止必须按统一剩余时限等待消费者退出。'
Assert-Contains $saveNode '_imageQueue.CompleteAndDisposePending();' '工作池停止超时必须释放尚未开始的任务。'
Assert-Contains $saveNode '_retiredTaskBacklogCount < _retiredTaskCapacity' '淘汰任务回收通道也必须有固定任务容量。'
Assert-Contains $saveNode 'estimatedBytes <= _retiredTaskMemoryBudgetBytes - _retiredTaskBacklogBytes' '淘汰任务回收通道也必须有固定字节预算。'
Assert-Contains $saveNode 'Volatile.Read(ref _retiredTaskBacklogCount) == 0' '工作池空闲判断必须包含淘汰资源回收积压。'
Assert-Contains $saveNode 'RegisterActiveWorker' '保存消费者必须关闭出队与活动计数之间的竞态窗口。'
Assert-Contains $saveNode 'File.Move(temporaryPath, imagePath);' '完成JPEG编码后必须原子发布到最终文件名。'
Assert-Contains $saveNode 'TryDeleteIncompleteFile(temporaryPath)' '写盘失败必须清理临时文件。'
Assert-Contains $saveNode 'TryDeleteIncompleteFile(committedImagePath)' '多目录保存中途失败必须回滚已经发布的文件。'
Assert-Contains $saveNode 'bitmap?.Dispose();' '转换或叠加异常必须释放已创建Bitmap。'
Assert-Contains $saveNode 'CpuWorkloadKind.ImageConversion' '保存Mat转Bitmap必须取得图像转换CPU许可。'
Assert-Contains $saveNode 'CpuWorkloadKind.ImageEncoding' '保存JPEG编码必须取得图片编码CPU许可。'
Assert-Contains $saveNode '_shutdownCancellationSource' '保存工作池必须拥有独立停止令牌，取消已经出队任务的CPU许可等待。'
Assert-Contains $saveNode 'AcquireAsync(workloadKind, cancellationToken)' '保存CPU许可等待必须使用可取消令牌。'
Assert-Contains $saveNode 'TryCancelShutdownNoThrow()' '停止超时前必须取消仍在等待的CPU许可。'
Assert-Contains $saveNode 'EncodeBitmapToJpeg(' '保存工作池必须把JPEG编码和磁盘写入拆分。'
Assert-Contains $saveNode 'SaveEncodedImage(' '磁盘阶段必须写入已经编码的JPEG缓冲区。'
Assert-Contains $saveNode 'outputStream.Write(encodedImage.Buffer, 0, encodedImage.Length);' '磁盘阶段只能写入编码结果，不得重新执行Bitmap编码。'
Assert-NotContains $saveNode 'bitmap.Save(outputStream' 'JPEG编码不得覆盖慢盘文件写入时间。'
$encodeCallIndex = $saveNode.IndexOf('encodedImage = EncodeBitmapToJpeg(')
$diskCallIndex = $saveNode.IndexOf('string fileName = SaveEncodedImage(')
Assert-True ($encodeCallIndex -ge 0 -and $diskCallIndex -gt $encodeCallIndex) '保存链必须先完成一次JPEG编码，再复用于全部目标目录。'

Assert-Contains $solution 'private ImageQueueProcessor _imageSaveQueueProcessor;' 'Solution必须持有唯一共享保存工作池。'
Assert-Contains $solution 'CreateImageSaveQueueProcessor(CurrentResourceProfile)' '共享工作池必须按当前资源档案延迟创建。'
Assert-Contains $solution 'new CachedImageSaveStorageGuard(' '共享工作池创建时必须应用磁盘保护参数。'
Assert-Contains $solution 'storageGuard,' '共享保存工作池必须接收磁盘保护器。'
Assert-Contains $solution '_cpuWorkScheduler);' '共享保存工作池必须复用全方案CPU调度器。'
Assert-Contains $solution 'profile?.ImageSaveWorkerCount ?? 1' '保存工作线程数必须读取自动资源档案并提供保守降级值。'
Assert-Contains $solution 'AverageImageBytes = _imageSaveSizeSampler.AverageBytes' '资源档案必须使用运行时滚动图像字节样本。'
Assert-Contains $solution 'if (!StopImageSaveQueueProcessor())' '方案重置必须确认共享保存工作池已经完全退出。'
Assert-Contains $solution 'if (!TryStopRunsForReset(drainTimeoutMilliseconds))' '方案重置必须先取消并等待运行流程退出。'
Assert-Contains $solution 'lock (_solutionMutationSync)' '并发方案重置必须由方案级互斥锁串行化。'
Assert-Contains $solution 'internal IDisposable EnterSolutionMutationScope()' '方案加载必须能够覆盖完整变更过程持有方案级互斥锁。'
Assert-Contains $solution 'if (!_imageSaveQueueProcessor.IsFullyStopped)' '旧工作池未完全退出时必须禁止创建重叠工作池。'
Assert-Contains $solution 'DisposeNodeControl(node);' '删除流程或重置方案必须触发保存节点Dispose。'
Assert-Contains $nodeBase '(ParamForm as IDisposable)?.Dispose();' '单独删除节点必须释放参数窗体。'
Assert-Contains $nodeBase 'Dispose();' '单独删除节点必须触发派生节点资源释放。'
Assert-Contains $sampler 'while (_samples.Count > _capacity)' '图像资源采样器必须保持固定滚动窗口。'
Assert-Contains $project '<Compile Include="ResourceManagement\BoundedPriorityWorkQueue.cs" />' '项目必须编译有界优先资源队列。'
Assert-Contains $project '<Compile Include="ResourceManagement\ImageResourceSizeSampler.cs" />' '项目必须编译图像资源滚动采样器。'
Assert-Contains $project '<Compile Include="ResourceManagement\ImageSaveStorageGuard.cs" />' '项目必须编译磁盘保护和保存诊断模型。'

Write-Host '保存队列共享架构、双重边界、CPU转换与编码分段、停止排空和原子写盘结构检查通过。'
