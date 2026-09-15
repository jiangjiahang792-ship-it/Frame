$ErrorActionPreference = 'Stop'

# 生产程序集基于.NET Framework 4.8，统一使用Windows PowerShell执行运行时检查。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param([string]$Text, [string]$Expected, [string]$Message)

    if (-not $Text.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Unexpected, [string]$Message)

    if ($Text.Contains($Unexpected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$processSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Process.cs') -Raw -Encoding UTF8
$solutionSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Solution.cs') -Raw -Encoding UTF8
$classifierSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\CpuWorkloadClassifier.cs') -Raw -Encoding UTF8
$monitorSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\CpuResourceMonitor.cs') -Raw -Encoding UTF8
$schedulerSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\CpuWorkScheduler.cs') -Raw -Encoding UTF8
$imageShowSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\1-Acquisition\ImageShow\NodeImageShow.cs') -Raw -Encoding UTF8
$imageShow3DSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\1-Acquisition\ImageShow3D\NodeImageShow3D.cs') -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8

Assert-Contains $processSource 'RunNodeWithCpuResourcePolicy' '流程节点必须通过统一CPU资源策略入口执行。'
Assert-True (([regex]::Matches($processSource, 'RunNodeWithCpuResourcePolicy\(')).Count -eq 7) '六条节点执行路径必须全部调用统一CPU资源策略入口。'
Assert-True (([regex]::Matches($processSource, 'await node\.Run\(')).Count -eq 2) '直接节点运行只能保留在统一资源策略方法的受控和直通分支。'
Assert-Contains $classifierSource 'DefaultCpuWorkloadClassifier : ICpuWorkloadClassifier' 'CPU节点分类器必须通过可替换接口实现。'
Assert-NotContains $classifierSource 'NodeType.AITD' 'TDAI节点不得进入CPU重任务分类器。'
Assert-NotContains $classifierSource 'NodeType.UnsupervisedDetection' '无监督AI节点不得进入CPU重任务分类器。'
Assert-NotContains $classifierSource 'NodeType.LargeModelDetection' '大模型节点不得进入CPU重任务分类器。'
Assert-Contains $monitorSource 'GetSystemTimes' '系统CPU采样必须读取Windows全机累计时间。'
Assert-Contains $monitorSource 'private readonly Timer _timer' 'CPU监控必须使用后台低频定时器。'
Assert-NotContains $monitorSource 'System.Windows.Forms.Timer' 'CPU监控不得占用UI消息循环定时器。'
Assert-NotContains $monitorSource 'ThreadPool.Set' 'CPU监控不得修改全局线程池。'
Assert-NotContains $monitorSource 'ProcessorAffinity' 'CPU监控不得修改进程亲和性。'
Assert-Contains $solutionSource '_cpuWorkScheduler.Reconfigure' '自动硬件档案必须重配CPU调度器。'
Assert-Contains $solutionSource '_cpuResourceMonitor.Reconfigure' '自动硬件档案必须重配CPU采样周期。'
Assert-Contains $schedulerSource 'OptionsEqual(_options, normalizedOptions)' '相同硬件档案不得重置已经动态下降的CPU额度。'
Assert-Contains $projectSource '<Compile Include="ResourceManagement\CpuWorkloadClassifier.cs" />' '项目必须编译CPU节点分类器。'
Assert-Contains $projectSource '<Compile Include="ResourceManagement\CpuResourceMonitor.cs" />' '项目必须编译CPU监控器。'

$refreshIndex = $imageShowSource.IndexOf('if (refreshGranted)')
$acquireIndex = $imageShowSource.IndexOf('CpuWorkloadKind.ImageConversion')
$toBitmapIndex = $imageShowSource.IndexOf('image = firstMat.ToBitmap()', $refreshIndex)
Assert-True ($refreshIndex -ge 0 -and $acquireIndex -gt $refreshIndex -and $toBitmapIndex -gt $acquireIndex) '二维显示必须先通过帧率门控，再为真实ToBitmap取得转换许可。'
$refresh3DIndex = $imageShow3DSource.IndexOf('if (refreshGranted)')
$acquire3DIndex = $imageShow3DSource.IndexOf('CpuWorkloadKind.ImageConversion')
$toBitmap3DIndex = $imageShow3DSource.IndexOf('image = firstMat.ToBitmap()', $refresh3DIndex)
Assert-True ($refresh3DIndex -ge 0 -and $acquire3DIndex -gt $refresh3DIndex -and $toBitmap3DIndex -gt $acquire3DIndex) '三维显示必须先通过帧率门控，再为真实ToBitmap取得转换许可。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'CPU生产接入测试需要最新Debug程序。'

[Environment]::CurrentDirectory = $debugDirectory
$originalPath = $env:PATH
$env:PATH = $debugDirectory + ';' + $originalPath
try {
    $helperSource = @'
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node;
using TDJS_Vision.ResourceManagement;

/// <summary>CPU生产接入专项结果。</summary>
public sealed class CpuWorkloadIntegrationResult
{
    /// <summary>传统CPU节点分类成功数。</summary>
    public int ClassifiedCpuNodeCount { get; set; }

    /// <summary>明确排除节点保持直通的数量。</summary>
    public int ExcludedNodeCount { get; set; }

    /// <summary>模拟监控传给调度器的CPU样本数。</summary>
    public int MonitorSampleCount { get; set; }

    /// <summary>Windows全机CPU采样是否取得有效差值。</summary>
    public bool WindowsCpuSampleSucceeded { get; set; }

    /// <summary>动态下降后的并发。</summary>
    public int ReducedConcurrency { get; set; }

    /// <summary>相同参数重配后的并发。</summary>
    public int ReconfiguredConcurrency { get; set; }

    /// <summary>Windows全机CPU采样值。</summary>
    public double WindowsCpuPercent { get; set; }
}

/// <summary>CPU生产接入专项运行宿主。</summary>
public static class CpuWorkloadIntegrationHarness
{
    /// <summary>验证分类边界、后台采样和动态额度保持。</summary>
    public static CpuWorkloadIntegrationResult Run()
    {
        DefaultCpuWorkloadClassifier classifier = new DefaultCpuWorkloadClassifier();
        NodeType[] cpuNodeTypes = new NodeType[]
        {
            NodeType.ImageCrop,
            NodeType.GrayScale,
            NodeType.BlobAnalysis,
            NodeType.LineFind,
            NodeType.CircleFind,
            NodeType.CaliperLine,
            NodeType.CaliperCircle,
            NodeType.CaliperEllipse,
            NodeType.FindPoint,
            NodeType.PositionCorrection,
            NodeType.TemplateMatch,
            NodeType.ImageRotate,
            NodeType.ImageSplit,
            NodeType.QRScan,
            NodeType.MatchTemplate,
            NodeType.NccMatchTemplate,
            NodeType.DrawAIResult,
            NodeType.ResultOverlayDraw,
            NodeType.ResultOverlayDraw2,
            NodeType.BatteryEar,
            NodeType.RGBDiscern,
            NodeType.BinarizationAnalysis,
            NodeType.LineLineAngle,
            NodeType.PointPointDistance,
            NodeType.PointLineDistance,
            NodeType.PointRegionDistance,
            NodeType.LineMergeFit,
            NodeType.ImagePreprocess
        };
        NodeType[] excludedNodeTypes = new NodeType[]
        {
            NodeType.AITD,
            NodeType.UnsupervisedDetection,
            NodeType.LargeModelDetection,
            NodeType.PLCRead,
            NodeType.PLCWrite,
            NodeType.ModbusRead,
            NodeType.ModbusWrite,
            NodeType.TCPClientRequest,
            NodeType.TCPServerResponse,
            NodeType.CameraIO,
            NodeType.ImageSource,
            NodeType.ImageSource3D,
            NodeType.ImageShow,
            NodeType.ImageShow3D,
            NodeType.ImageSave,
            NodeType.CompositeModule
        };

        int classifiedCount = 0;
        foreach (NodeType nodeType in cpuNodeTypes)
        {
            CpuWorkloadKind kind;
            if (TryClassify(classifier, nodeType, out kind) && kind == CpuWorkloadKind.TraditionalVisionAlgorithm)
                classifiedCount++;
        }

        int excludedCount = 0;
        foreach (NodeType nodeType in excludedNodeTypes)
        {
            CpuWorkloadKind kind;
            if (!TryClassify(classifier, nodeType, out kind))
                excludedCount++;
        }

        FakeCpuWorkScheduler fakeScheduler = new FakeCpuWorkScheduler();
        FakeSystemCpuUsageSampler fakeSampler = new FakeSystemCpuUsageSampler(90D, 40D);
        using (CpuResourceMonitor monitor = new CpuResourceMonitor(fakeScheduler, fakeSampler, 60000))
        {
            monitor.SampleNow();
            monitor.SampleNow();
        }

        WindowsSystemCpuUsageSampler windowsSampler = new WindowsSystemCpuUsageSampler();
        double windowsCpuPercent;
        windowsSampler.TrySample(out windowsCpuPercent);
        bool windowsSampleSucceeded = false;
        for (int attempt = 0; attempt < 5 && !windowsSampleSucceeded; attempt++)
        {
            Thread.Sleep(50);
            windowsSampleSucceeded = windowsSampler.TrySample(out windowsCpuPercent);
        }

        CpuWorkSchedulerOptions options = CreateOptions();
        int reducedConcurrency;
        int reconfiguredConcurrency;
        using (CpuWorkScheduler scheduler = new CpuWorkScheduler(options))
        {
            scheduler.ObserveCpuSample(90D, 0L);
            scheduler.ObserveCpuSample(90D, 1000L);
            reducedConcurrency = scheduler.GetSnapshot().CurrentConcurrency;
            scheduler.Reconfigure(CreateOptions());
            reconfiguredConcurrency = scheduler.GetSnapshot().CurrentConcurrency;
        }

        return new CpuWorkloadIntegrationResult
        {
            ClassifiedCpuNodeCount = classifiedCount,
            ExcludedNodeCount = excludedCount,
            MonitorSampleCount = fakeScheduler.SampleCount,
            WindowsCpuSampleSucceeded = windowsSampleSucceeded,
            WindowsCpuPercent = windowsCpuPercent,
            ReducedConcurrency = reducedConcurrency,
            ReconfiguredConcurrency = reconfiguredConcurrency
        };
    }

    /// <summary>使用不运行WinForms构造函数的节点对象验证分类器。</summary>
    private static bool TryClassify(
        DefaultCpuWorkloadClassifier classifier,
        NodeType nodeType,
        out CpuWorkloadKind workloadKind)
    {
        NodeBase node = (NodeBase)FormatterServices.GetUninitializedObject(typeof(NodeBase));
        node.NodeType = nodeType;
        return classifier.TryClassify(node, out workloadKind);
    }

    /// <summary>创建相同的动态策略参数。</summary>
    private static CpuWorkSchedulerOptions CreateOptions()
    {
        return new CpuWorkSchedulerOptions
        {
            MaximumConcurrency = 4,
            MinimumConcurrency = 1,
            ImageConversionMaximumConcurrency = 2,
            HighCpuPercent = 85D,
            RecoveryCpuPercent = 65D,
            HighCpuSustainMilliseconds = 1000,
            RecoveryCpuSustainMilliseconds = 1000,
            AdjustmentIntervalMilliseconds = 1000,
            AdjustmentCooldownMilliseconds = 5000,
            AdjustmentStep = 1,
            WaitSampleCapacity = 128
        };
    }

    /// <summary>记录监控器输入样本的调度器替身。</summary>
    private sealed class FakeCpuWorkScheduler : ICpuWorkScheduler
    {
        /// <summary>收到的CPU样本数量。</summary>
        public int SampleCount { get; private set; }

        /// <summary>测试不使用许可获取。</summary>
        public Task<ICpuWorkLease> AcquireAsync(CpuWorkloadKind workloadKind, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <summary>记录CPU样本。</summary>
        public void ObserveCpuSample(double cpuPercent, long timestampMilliseconds)
        {
            SampleCount++;
        }

        /// <summary>测试不使用重配。</summary>
        public void Reconfigure(CpuWorkSchedulerOptions options)
        {
        }

        /// <summary>返回空诊断。</summary>
        public CpuWorkSchedulerSnapshot GetSnapshot()
        {
            return new CpuWorkSchedulerSnapshot();
        }

        /// <summary>测试替身没有非托管资源。</summary>
        public void Dispose()
        {
        }
    }

    /// <summary>按顺序返回固定CPU样本的采样器替身。</summary>
    private sealed class FakeSystemCpuUsageSampler : ISystemCpuUsageSampler
    {
        /// <summary>待返回样本队列。</summary>
        private readonly Queue<double> _samples;

        /// <summary>创建固定样本采样器。</summary>
        public FakeSystemCpuUsageSampler(params double[] samples)
        {
            _samples = new Queue<double>(samples);
        }

        /// <summary>返回下一个固定样本。</summary>
        public bool TrySample(out double cpuPercent)
        {
            if (_samples.Count == 0)
            {
                cpuPercent = 0D;
                return false;
            }
            cpuPercent = _samples.Dequeue();
            return true;
        }
    }
}
'@

    $applicationReferencePath = Join-Path $debugDirectory 'TDJSVision.CpuIntegrationTestHost.dll'
    Copy-Item -LiteralPath $application.FullName -Destination $applicationReferencePath -Force
    try {
        [void][System.Reflection.Assembly]::LoadFrom($applicationReferencePath)
        try {
            Add-Type -TypeDefinition $helperSource -Language CSharp -ReferencedAssemblies @(
                'mscorlib',
                'System',
                'System.Core',
                'System.Runtime.Serialization',
                'System.Threading',
                'System.Windows.Forms',
                $applicationReferencePath)
        }
        catch [System.Reflection.ReflectionTypeLoadException] {
            $loaderMessages = $_.Exception.LoaderExceptions | ForEach-Object { $_.Message }
            throw ('CPU生产接入测试宿主依赖加载失败：' + [string]::Join('；', [string[]]$loaderMessages))
        }
    }
    finally {
        Remove-Item -LiteralPath $applicationReferencePath -Force -ErrorAction SilentlyContinue
    }

    $result = [CpuWorkloadIntegrationHarness]::Run()
    Assert-True ($result.ClassifiedCpuNodeCount -eq 28) '28类传统视觉、测量和绘制节点必须全部进入CPU许可池。'
    Assert-True ($result.ExcludedNodeCount -eq 16) 'AI、通信、相机、显示、保存和组合模块必须保持直通。'
    Assert-True ($result.MonitorSampleCount -eq 2) '后台CPU监控必须把有效样本传给调度器。'
    Assert-True $result.WindowsCpuSampleSucceeded 'Windows全机CPU采样必须取得有效差值。'
    Assert-True ($result.WindowsCpuPercent -ge 0 -and $result.WindowsCpuPercent -le 100) 'Windows全机CPU采样必须位于0～100%。'
    Assert-True ($result.ReducedConcurrency -eq 3) '高CPU持续后并发必须从4降到3。'
    Assert-True ($result.ReconfiguredConcurrency -eq 3) '相同参数重配不得把动态并发从3重置回4。'

    Write-Host ('CPU生产接入检查通过：传统CPU节点={0}/28；明确直通={1}/16；监控样本={2}；系统CPU={3:N2}%；动态额度保持={4}->{5}。' -f `
        $result.ClassifiedCpuNodeCount,
        $result.ExcludedNodeCount,
        $result.MonitorSampleCount,
        $result.WindowsCpuPercent,
        $result.ReducedConcurrency,
        $result.ReconfiguredConcurrency)
}
finally {
    $env:PATH = $originalPath
}
