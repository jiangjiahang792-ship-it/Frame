$ErrorActionPreference = 'Stop'

# 生产程序面向.NET Framework 4.8，真实执行测试统一交给Windows PowerShell。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $escapedScriptPath = $PSCommandPath.Replace("'", "''")
    $env:TDJS_CAMERA_IO_TEST_PATH = $PSCommandPath
    $command = "`$scriptText = Get-Content -LiteralPath '$escapedScriptPath' -Raw -Encoding UTF8; try { Invoke-Expression `$scriptText } catch { Write-Host (`$_.Exception.ToString()); Write-Host `$_.ScriptStackTrace; exit 1 }"
    & $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$scriptDirectory = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $env:TDJS_CAMERA_IO_TEST_PATH
} else {
    $PSScriptRoot
}
$projectRoot = Split-Path -Parent $scriptDirectory
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$nodePath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\CameraIO\NodeCameraIO.cs'
$paramFormPath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\CameraIO\ParamFormCameraIO.cs'
$designerPath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\CameraIO\ParamFormCameraIO.Designer.cs'
$nodeFile = Get-Item -LiteralPath $nodePath
$paramFormFile = Get-Item -LiteralPath $paramFormPath
$designerFile = Get-Item -LiteralPath $designerPath
Assert-True ($null -ne $application) '相机IO有序发送测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge $nodeFile.LastWriteTimeUtc -and $application.LastWriteTimeUtc -ge $paramFormFile.LastWriteTimeUtc -and $application.LastWriteTimeUtc -ge $designerFile.LastWriteTimeUtc) 'Debug程序早于相机IO源码，请先重新编译。'

$source = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$paramFormSource = Get-Content -LiteralPath $paramFormPath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
Assert-True ($source.Contains('NodeBase, IOrderedExternalSignalNode')) '相机IO节点必须实现统一有序外发接口。'
Assert-True ($source.Contains('WorkpieceContextAccessor.Current')) '相机IO运行入口必须取得当前工件上下文。'
Assert-True ($source.Contains('Solution.Instance.OrderedSignalCoordinator')) '相机IO运行入口必须使用当前生产会话协调器。'
Assert-True (-not $source.Contains('_ = Task.Run(async')) '相机IO异步模式不得保留脱离工件生命周期的后台脉冲。'
Assert-True ($source.Contains('ObserveManagedAsyncSendAsync(')) '相机IO异步模式必须观察受管信号结果。'
Assert-True ($source.Contains('CameraIoSignalSnapshot signalSnapshot = CaptureSignalSnapshot();')) '相机IO运行入口必须一次性冻结全部动态参数。'
Assert-True ($source.Contains('bool runAsynchronouslySnapshot = paramSnapshot.IsAsay;')) '相机IO异步开关必须进入同一份不可变快照。'
Assert-True ($source.Contains('private sealed class CameraIoSignalSnapshot')) '相机IO必须用独立不可变对象保存本轮动态参数。'
Assert-True ($source.Contains('CameraProductionIdentity.GetStableKey(camera)')) '相机IO端点键必须复用统一稳定相机身份。'
Assert-True ($source.Contains('private const int MaximumHoldTimeMicroseconds = 1000000;')) '相机IO保持时间必须具有有限上限。'
Assert-True ($source.Contains('await Task.Delay(coarseDelayMilliseconds)')) '较长相机IO脉冲必须先异步粗等待，不能全程忙等。'
Assert-True ($source.Contains('if (!cameraSnapshot.IsOpen)')) '相机IO提交前必须拒绝已知未连接设备。'
Assert-True ($source.Contains('if (!camera.IsOpen)')) '相机IO取得发送权后必须再次拒绝已知未连接设备。'
Assert-True ($source.Contains('outputMayBeRaised = true;') -and $source.Contains('if (outputMayBeRaised)')) '相机IO必须在拉高调用前进入保守恢复状态，并在异常路径尝试回低。'
Assert-True ($source.Contains('相机IO信号节点只允许使用输出线路')) '相机IO信号节点必须拒绝输入线路配置。'

$selectorIndex = $source.IndexOf('camera.SetLineSelector(lineSelector);')
$modeIndex = $source.IndexOf('camera.SetLineMode(lineMode);')
$lowIndex = $source.IndexOf('camera.SetLineInverter(false);')
$highIndex = $source.IndexOf('camera.SetLineInverter(true);')
Assert-True ($selectorIndex -ge 0 -and $selectorIndex -lt $modeIndex -and $modeIndex -lt $lowIndex -and $lowIndex -lt $highIndex) '完整相机IO脉冲必须按选线路、设模式、置低、拉高顺序执行。'
Assert-True ($paramFormSource.Contains('TryExecuteManualExternalSignal(')) '参数页线路能力枚举必须经过生产会话门禁。'
Assert-True ($paramFormSource.Contains('private void RefreshLineOptions(string lineMode)')) '参数页SDK写操作必须收口到门禁内的独立方法。'
Assert-True (-not $paramFormSource.Contains('mode.Symbolic == "Strobe"')) '参数页不得把只支持Strobe、但不能设置Output的线路列为生产输出线路。'
Assert-True (-not $designerSource.Contains('"输入"')) '相机IO信号节点参数页不得提供输入线路模式。'

[Environment]::CurrentDirectory = $debugDirectory
$assemblyResolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($name.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolveHandler)
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$cameraNodeClassType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.CameraIO.NodeCameraIO', $true)
$interfaceType = $assembly.GetType('TDJS_Vision.ResourceManagement.IOrderedExternalSignalNode', $true)
Assert-True ($interfaceType.IsAssignableFrom($cameraNodeClassType)) '编译程序集中的相机IO节点未实现有序外发接口。'
Assert-True ($null -ne $cameraNodeClassType.GetProperty('OrderedSignalKind')) '相机IO节点缺少有序节点类别属性。'
Assert-True ($null -ne $cameraNodeClassType.GetProperty('OrderedSignalEndpointKey')) '相机IO节点缺少物理端点键属性。'
Assert-True ($null -ne $cameraNodeClassType.GetMethod('SubmitOrderedSignalAsync')) '相机IO节点缺少统一有序提交方法。'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;

public sealed class CameraIoInvocationProxy : RealProxy
{
    private readonly object _syncRoot = new object();
    private readonly List<string> _calls = new List<string>();
    private int _activeHighCount;
    private int _maximumHighCount;

    public CameraIoInvocationProxy(Type interfaceType) : base(interfaceType) { }

    public bool ThrowAfterHigh { get; set; }

    public int ActiveHighCount { get { return Volatile.Read(ref _activeHighCount); } }

    public int MaximumHighCount { get { return Volatile.Read(ref _maximumHighCount); } }

    public string[] Calls
    {
        get { lock (_syncRoot) return _calls.ToArray(); }
    }

    public object CreateProxy()
    {
        return GetTransparentProxy();
    }

    public void AssignProxyToProperty(object target, string propertyName)
    {
        target.GetType().GetProperty(propertyName).SetValue(
            target,
            GetTransparentProxy(),
            null);
    }

    public override IMessage Invoke(IMessage message)
    {
        IMethodCallMessage call = (IMethodCallMessage)message;
        MethodInfo method = (MethodInfo)call.MethodBase;
        try
        {
            object returnValue = HandleCall(method, call.Args);
            return new ReturnMessage(returnValue, null, 0, call.LogicalCallContext, call);
        }
        catch (Exception exception)
        {
            return new ReturnMessage(exception, call);
        }
    }

    private object HandleCall(MethodInfo method, object[] arguments)
    {
        if (method.Name == "get_IsOpen")
            return true;
        if (method.Name == "get_SN")
            return "CAMERA-IO-TEST-SN";
        if (method.Name == "get_Brand")
            return Enum.ToObject(method.ReturnType, 0);
        if (method.Name == "SetLineSelector")
        {
            Record("SELECT:" + arguments[0]);
            return null;
        }
        if (method.Name == "SetLineMode")
        {
            Record("MODE:" + arguments[0]);
            return null;
        }
        if (method.Name == "SetLineInverter")
        {
            bool high = (bool)arguments[0];
            Record("LEVEL:" + high);
            if (high)
            {
                int active = Interlocked.Increment(ref _activeHighCount);
                UpdateMaximum(active);
                if (ThrowAfterHigh)
                    throw new InvalidOperationException("模拟拉高后SDK返回错误");
            }
            else
            {
                DecrementHighIfNeeded();
            }
            return null;
        }

        return method.ReturnType == typeof(void)
            ? null
            : method.ReturnType.IsValueType
                ? Activator.CreateInstance(method.ReturnType)
                : null;
    }

    private void Record(string value)
    {
        lock (_syncRoot)
            _calls.Add(value);
    }

    private void UpdateMaximum(int value)
    {
        int current = Volatile.Read(ref _maximumHighCount);
        while (value > current)
        {
            int observed = Interlocked.CompareExchange(ref _maximumHighCount, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    private void DecrementHighIfNeeded()
    {
        int current = Volatile.Read(ref _activeHighCount);
        while (current > 0)
        {
            int observed = Interlocked.CompareExchange(ref _activeHighCount, current - 1, current);
            if (observed == current)
                return;
            current = observed;
        }
    }
}
'@

$cameraInterfaceType = $assembly.GetType('TDJS_Vision.Device.Camera.ICamera', $true)
$cameraParamType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.CameraIO.NodeParamCameraIO', $true)
$nodeKindEnumType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$coordinatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalCoordinator', $true)
$terminalType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceTerminalState', $true)
$sendStatusType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendStatus', $true)
$cameraNodeKind = [Enum]::Parse($nodeKindEnumType, 'CameraIO')
$succeeded = [Enum]::Parse($terminalType, 'Succeeded')
$localCompleted = [Enum]::Parse($sendStatusType, 'LocalCallCompleted')
$unknown = [Enum]::Parse($sendStatusType, 'Unknown')
$cancelledBeforeStart = [Enum]::Parse($sendStatusType, 'CancelledBeforeStart')
$stopped = [Enum]::Parse($terminalType, 'Stopped')
$none = [Threading.CancellationToken]::None

function Wait-TaskResult {
    param([Threading.Tasks.Task]$Task, [string]$Message)
    Assert-True ($Task.Wait(5000)) $Message
    return $Task.GetAwaiter().GetResult()
}

function Wait-Task {
    param([Threading.Tasks.Task]$Task, [string]$Message)
    Assert-True ($Task.Wait(5000)) $Message
    $null = $Task.GetAwaiter().GetResult()
}

function New-Context {
    param([Guid]$Epoch, [string]$Domain, [long]$Sequence)
    $identity = [Activator]::CreateInstance($identityType, @($Epoch, $Domain, $Sequence))
    return [Activator]::CreateInstance(
        $contextType,
        @($identity, $Sequence, $Domain, 'camera-io-test', [DateTime]::UtcNow, $none))
}

function New-CameraNode {
    param([int]$Id, [CameraIoInvocationProxy]$Controller, [string]$Line, [int]$HoldTime)
    $node = [Activator]::CreateInstance(
        $cameraNodeClassType,
        @($Id, "相机IO测试$Id", $null, $cameraNodeKind))
    $param = [Activator]::CreateInstance($cameraParamType)
    $Controller.AssignProxyToProperty($param, 'Camera')
    $param.CameraName = '假相机'
    $param.LineSelector = $Line
    $param.LineMode = '输出'
    $param.HoldTime = $HoldTime
    $param.IsAsay = $false
    $node.ParamForm.Params = $param
    return $node
}

function Dispose-CameraNode {
    param([object]$Node)
    if ($null -eq $Node) { return }
    if ($null -ne $Node.ParamForm) { $Node.ParamForm.Dispose() }
    $Node.Dispose()
}

# 两个生产线共用同一物理相机时，端点必须串行；提交后的参数替换不得改变已排队快照。
$proxyController = [CameraIoInvocationProxy]::new($cameraInterfaceType)
$node1 = New-CameraNode 9101 $proxyController 'Line1' 80000
$node2 = New-CameraNode 9102 $proxyController 'Line2' 10000
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context1 = New-Context $epoch 'CAMERA-LINE-A' 1
$context2 = New-Context $epoch 'CAMERA-LINE-B' 1
$coordinator.RegisterWorkpiece($context1)
$coordinator.RegisterWorkpiece($context2)
$task1 = $node1.SubmitOrderedSignalAsync($context1, 'flow-a/camera-io', $coordinator, $none)
$deadline = [DateTime]::UtcNow.AddSeconds(2)
while ($proxyController.ActiveHighCount -eq 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 1 }
Assert-True ($proxyController.ActiveHighCount -eq 1) '第一个假相机脉冲没有进入高电平。'
$sealTask1 = $coordinator.CompleteWorkpieceAsync($context1, $succeeded)
Assert-True (-not $sealTask1.IsCompleted) '相机IO脉冲仍在执行时工件被提前封口。'
$task2 = $node2.SubmitOrderedSignalAsync($context2, 'flow-b/camera-io', $coordinator, $none)
$replacement = [Activator]::CreateInstance($cameraParamType)
$proxyController.AssignProxyToProperty($replacement, 'Camera')
$replacement.CameraName = '假相机'
$replacement.LineSelector = 'LineAfterSubmit'
$replacement.LineMode = '输出'
$replacement.HoldTime = 0
$replacement.IsAsay = $true
$node2.ParamForm.Params = $replacement
$result1 = Wait-TaskResult $task1 '第一个假相机脉冲未完成。'
$result2 = Wait-TaskResult $task2 '第二个假相机脉冲未完成。'
Assert-True ($result1.Status -eq $localCompleted -and $result2.Status -eq $localCompleted) '正常假相机脉冲没有返回本地调用完成。'
Assert-True ($proxyController.MaximumHighCount -eq 1) '同一物理相机的两个脉冲发生了高电平重叠。'
$calls = @($proxyController.Calls)
Assert-True ($calls -contains 'SELECT:Line2') '已排队相机IO没有使用提交时冻结的线路。'
Assert-True (-not ($calls -contains 'SELECT:LineAfterSubmit')) '参数替换污染了已经提交的相机IO快照。'
Wait-Task $sealTask1 '第一个假相机工件未在脉冲结束后封口。'
Wait-Task ($coordinator.CompleteWorkpieceAsync($context2, $succeeded)) '第二个假相机工件未封口。'
$coordinator.Dispose()
Dispose-CameraNode $node1
Dispose-CameraNode $node2

# 模拟硬件已经拉高但SDK返回错误，必须再次选回原线路并置低，协调器返回Unknown。
$failureProxyController = [CameraIoInvocationProxy]::new($cameraInterfaceType)
$failureProxyController.ThrowAfterHigh = $true
$failureNode = New-CameraNode 9201 $failureProxyController 'Line3' 1000
$failureCoordinator = [Activator]::CreateInstance($coordinatorType)
$failureEpoch = [Guid]::NewGuid()
$failureContext = New-Context $failureEpoch 'CAMERA-FAILURE' 1
$failureCoordinator.RegisterWorkpiece($failureContext)
$failureResult = Wait-TaskResult (
    $failureNode.SubmitOrderedSignalAsync(
        $failureContext,
        'flow-failure/camera-io',
        $failureCoordinator,
        $none)) '异常假相机脉冲未返回。'
Assert-True ($failureResult.Status -eq $unknown) '拉高后SDK异常必须归类为Unknown。'
$failureCalls = @($failureProxyController.Calls)
$highIndex = [Array]::IndexOf($failureCalls, 'LEVEL:True')
$lastLowIndex = [Array]::LastIndexOf($failureCalls, 'LEVEL:False')
Assert-True ($highIndex -ge 0 -and $lastLowIndex -gt $highIndex) '拉高后SDK异常没有再次尝试恢复低电平。'
Assert-True ($failureProxyController.ActiveHighCount -eq 0) '异常清理后假相机仍处于高电平。'
Wait-Task ($failureCoordinator.CompleteWorkpieceAsync($failureContext, $succeeded)) '异常假相机工件未封口。'
$failureTerminal = Wait-TaskResult $failureContext.Completion '异常假相机工件没有发布终态。'
Assert-True ($failureTerminal -eq $stopped) 'Unknown相机IO结果没有把工件终态升级为Stopped。'
$failureCoordinator.Dispose()
Dispose-CameraNode $failureNode

# 发送权取得前取消必须永久移除意图，不能触碰任何相机线路。
$cancelProxyController = [CameraIoInvocationProxy]::new($cameraInterfaceType)
$cancelNode = New-CameraNode 9301 $cancelProxyController 'Line4' 1000
$cancelCoordinator = [Activator]::CreateInstance($coordinatorType)
$cancelEpoch = [Guid]::NewGuid()
$cancelContext = New-Context $cancelEpoch 'CAMERA-CANCEL' 1
$cancelCoordinator.RegisterWorkpiece($cancelContext)
$cancelSource = [Threading.CancellationTokenSource]::new()
$cancelSource.Cancel()
$cancelResult = Wait-TaskResult (
    $cancelNode.SubmitOrderedSignalAsync(
        $cancelContext,
        'flow-cancel/camera-io',
        $cancelCoordinator,
        $cancelSource.Token)) '发送前取消的相机IO意图未返回。'
Assert-True ($cancelResult.Status -eq $cancelledBeforeStart) '发送前取消没有返回CancelledBeforeStart。'
Assert-True ($cancelProxyController.Calls.Count -eq 0) '发送前取消仍然写入了假相机线路。'
Wait-Task ($cancelCoordinator.CompleteWorkpieceAsync($cancelContext, $succeeded)) '取消假相机工件未封口。'
$cancelSource.Dispose()
$cancelCoordinator.Dispose()
Dispose-CameraNode $cancelNode

Write-Host '相机IO有序协调器接入检查通过。'
