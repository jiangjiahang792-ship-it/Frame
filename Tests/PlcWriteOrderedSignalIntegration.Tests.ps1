$ErrorActionPreference = 'Stop'

# 生产程序面向.NET Framework 4.8，真实执行测试统一交给Windows PowerShell。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $escapedScriptPath = $PSCommandPath.Replace("'", "''")
    $env:TDJS_PLC_WRITE_TEST_PATH = $PSCommandPath
    $command = "`$scriptText = Get-Content -LiteralPath '$escapedScriptPath' -Raw -Encoding UTF8; try { Invoke-Expression `$scriptText } catch { Write-Host (`$_.Exception.ToString()); Write-Host `$_.ScriptStackTrace; exit 1 }"
    & $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$scriptDirectory = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $env:TDJS_PLC_WRITE_TEST_PATH
} else {
    $PSScriptRoot
}
$projectRoot = Split-Path -Parent $scriptDirectory
$buildDirectory = if ($env:TDJS_TEST_BUILD_DIRECTORY) { $env:TDJS_TEST_BUILD_DIRECTORY } else { 'bin\x64\Debug' }
$debugDirectory = Join-Path $projectRoot $buildDirectory
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$sourcePaths = @(
    'Node\5-EquipmentCommunication\PLCWrite\NodePlcWrite.cs',
    'Node\5-EquipmentCommunication\PLCWrite\ParamFormPlcWrite.cs',
    'Node\5-EquipmentCommunication\PLCWrite\ParamFormPlcWrite.Designer.cs',
    'Device\PLC\IPlc.cs',
    'Device\PLC\PlcMelsec.cs',
    'Device\PLC\PlcPanasonic.cs')
Assert-True ($null -ne $application) 'PLC有序写入测试需要最新Debug程序。'
foreach ($relativePath in $sourcePaths) {
    $sourceFile = Get-Item -LiteralPath (Join-Path $projectRoot $relativePath)
    Assert-True ($application.LastWriteTimeUtc -ge $sourceFile.LastWriteTimeUtc) "Debug程序早于$relativePath，请先重新编译。"
}

$nodePath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\PLCWrite\NodePlcWrite.cs'
$nodeSource = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$interfaceSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Device\PLC\IPlc.cs') -Raw -Encoding UTF8
$melsecSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Device\PLC\PlcMelsec.cs') -Raw -Encoding UTF8
$panasonicSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Device\PLC\PlcPanasonic.cs') -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\5-EquipmentCommunication\PLCWrite\ParamFormPlcWrite.Designer.cs') -Raw -Encoding UTF8
Assert-True ($nodeSource.Contains('NodeBase, IOrderedExternalSignalNode')) 'PLC写入节点必须实现统一有序外发接口。'
Assert-True ($nodeSource.Contains('WorkpieceContextAccessor.Current')) 'PLC写入运行入口必须取得当前工件上下文。'
Assert-True ($nodeSource.Contains('PlcWriteSignalSnapshot signalSnapshot = CaptureSignalSnapshot();')) 'PLC动态值必须一次性冻结。'
Assert-True ($nodeSource.Contains('OrderedSignalSendStatus.DeviceAcknowledged')) 'PLC成功响应必须记录设备确认。'
Assert-True ($nodeSource.Contains('OrderedSignalSendStatus.Unknown')) 'PLC失败或空响应必须保守记录为Unknown。'
Assert-True ($nodeSource.Contains('CultureInfo.InvariantCulture')) 'PLC数字值必须使用稳定文化解析。'
Assert-True ($nodeSource.Contains('float.IsNaN(parsed)') -and $nodeSource.Contains('float.IsInfinity(parsed)')) 'PLC浮点值必须拒绝NaN和无穷大。'
Assert-True ($interfaceSource.Contains('int OperationTimeoutMs { get; set; }')) 'PLC接口必须公开有限操作超时。'
Assert-True ($melsecSource.Contains('melsecMcNet = new MelsecMcNet();')) '三菱PLC构造函数必须创建三菱协议客户端。'
Assert-True (-not $melsecSource.Contains('melsecMcNet = new PanasonicMcNet();')) '三菱PLC不得错误创建松下协议客户端。'
Assert-True ($melsecSource.Contains('melsecMcNet.ReceiveTimeOut = _operationTimeoutMs;')) '三菱PLC必须应用有限收发超时。'
Assert-True ($panasonicSource.Contains('_panasonicMewtocol.ReceiveTimeOut = _operationTimeoutMs;')) '松下串口PLC必须应用有限收发超时。'
Assert-True (($panasonicSource.Split('sp.WriteTimeout = _operationTimeoutMs;').Count - 1) -ge 3) '松下串口PLC初始化和运行时超时调整都必须同步设置底层写超时。'
Assert-True ($panasonicSource.Contains('_panasonicMcNet.ReceiveTimeOut = _operationTimeoutMs;')) '松下网口PLC必须应用有限收发超时。'
Assert-True ($designerSource.Contains('写入的值(多个值使用逗号隔开)')) 'PLC参数页必须显示无歧义数组分隔规则。'

[Environment]::CurrentDirectory = $debugDirectory
$assemblyResolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($name.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) { return [Reflection.Assembly]::LoadFrom($dependencyPath) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolveHandler)
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)

Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;

public sealed class PlcWriteInvocationProxy : RealProxy
{
    private static int s_activeWriteCount;
    private static int s_maximumWriteCount;
    private readonly object _syncRoot = new object();
    private readonly List<string> _calls = new List<string>();

    public PlcWriteInvocationProxy(Type interfaceType) : base(interfaceType)
    {
        IpAddress = "192.168.10.20";
        Port = 5000;
        WriteDelayMs = 50;
        Succeed = true;
    }

    public string IpAddress { get; set; }
    public int Port { get; set; }
    public int WriteDelayMs { get; set; }
    public bool Succeed { get; set; }

    public static int ActiveWriteCount { get { return Volatile.Read(ref s_activeWriteCount); } }
    public static int MaximumWriteCount { get { return Volatile.Read(ref s_maximumWriteCount); } }
    public string[] Calls { get { lock (_syncRoot) return _calls.ToArray(); } }

    public static void ResetConcurrency()
    {
        Volatile.Write(ref s_activeWriteCount, 0);
        Volatile.Write(ref s_maximumWriteCount, 0);
    }

    public void AssignProxyToProperty(object target, string propertyName)
    {
        target.GetType().GetProperty(propertyName).SetValue(target, GetTransparentProxy(), null);
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
        if (method.Name == "get_IsConnect")
            return true;
        if (method.Name == "get_OperationTimeoutMs")
            return 5000;
        if (method.Name == "get_Brand")
            return Enum.ToObject(method.ReturnType, 0);
        if (method.Name == "get_PLCParms")
            return CreateEthernetParameters(method.ReturnType);
        if (method.Name.StartsWith("Write", StringComparison.Ordinal))
        {
            string callText = method.Name + ":" + arguments[0] + ":" + FormatValue(arguments[1]);
            lock (_syncRoot)
                _calls.Add(callText);
            Type resultType = method.ReturnType.GetGenericArguments()[0];
            MethodInfo executeMethod = GetType().GetMethod("ExecuteWriteAsync").MakeGenericMethod(resultType);
            return executeMethod.Invoke(this, new object[] { callText });
        }

        return method.ReturnType == typeof(void)
            ? null
            : method.ReturnType.IsValueType
                ? Activator.CreateInstance(method.ReturnType)
                : null;
    }

    public async Task<T> ExecuteWriteAsync<T>(string callText)
    {
        int active = Interlocked.Increment(ref s_activeWriteCount);
        UpdateMaximum(active);
        try
        {
            await Task.Delay(Math.Max(0, WriteDelayMs)).ConfigureAwait(false);
            object result = Activator.CreateInstance(typeof(T));
            typeof(T).GetProperty("IsSuccess").SetValue(result, Succeed, null);
            typeof(T).GetProperty("ErrorCode").SetValue(result, Succeed ? 0 : -1001, null);
            typeof(T).GetProperty("Message").SetValue(result, Succeed ? "OK" : "模拟响应失败", null);
            return (T)result;
        }
        finally
        {
            Interlocked.Decrement(ref s_activeWriteCount);
        }
    }

    private object CreateEthernetParameters(Type parameterType)
    {
        object parameters = Activator.CreateInstance(parameterType);
        FieldInfo connectionTypeField = parameterType.GetField("PlcConType");
        connectionTypeField.SetValue(parameters, Enum.Parse(connectionTypeField.FieldType, "ETHERNET"));
        FieldInfo ethernetField = parameterType.GetField("EthernetParms");
        object ethernet = Activator.CreateInstance(ethernetField.FieldType);
        ethernetField.FieldType.GetField("IP").SetValue(ethernet, IpAddress);
        ethernetField.FieldType.GetField("Port").SetValue(ethernet, Port);
        ethernetField.SetValue(parameters, ethernet);
        return parameters;
    }

    private static string FormatValue(object value)
    {
        Array array = value as Array;
        if (array == null)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        return "[" + string.Join(",", array.Cast<object>().Select(item =>
            Convert.ToString(item, CultureInfo.InvariantCulture))) + "]";
    }

    private static void UpdateMaximum(int value)
    {
        int current = Volatile.Read(ref s_maximumWriteCount);
        while (value > current)
        {
            int observed = Interlocked.CompareExchange(ref s_maximumWriteCount, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }
}
'@

$plcInterfaceType = $assembly.GetType('TDJS_Vision.Device.PLC.IPlc', $true)
$plcNodeClassType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte.NodePlcWrite', $true)
$plcParamType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte.NodeParamPlcWrite', $true)
$orderedNodeInterfaceType = $assembly.GetType('TDJS_Vision.ResourceManagement.IOrderedExternalSignalNode', $true)
$nodeKindEnumType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$coordinatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalCoordinator', $true)
$terminalType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceTerminalState', $true)
$sendStatusType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendStatus', $true)
Assert-True ($orderedNodeInterfaceType.IsAssignableFrom($plcNodeClassType)) '编译程序集中的PLC写入节点未实现有序外发接口。'
$plcNodeKind = [Enum]::Parse($nodeKindEnumType, 'PLCWrite')
$succeeded = [Enum]::Parse($terminalType, 'Succeeded')
$stopped = [Enum]::Parse($terminalType, 'Stopped')
$deviceAcknowledged = [Enum]::Parse($sendStatusType, 'DeviceAcknowledged')
$unknown = [Enum]::Parse($sendStatusType, 'Unknown')
$cancelledBeforeStart = [Enum]::Parse($sendStatusType, 'CancelledBeforeStart')
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
        @($identity, $Sequence, $Domain, 'plc-write-test', [DateTime]::UtcNow, $none))
}

function New-PlcNode {
    param(
        [int]$Id,
        [PlcWriteInvocationProxy]$Controller,
        [string]$Address,
        [string]$DataType,
        [string]$Value)
    $node = [Activator]::CreateInstance(
        $plcNodeClassType,
        @($Id, "PLC写入测试$Id", $null, $plcNodeKind))
    $param = [Activator]::CreateInstance($plcParamType)
    $Controller.AssignProxyToProperty($param, 'Plc')
    $param.PlcName = '假PLC'
    $param.Address = $Address
    $param.DataType = $DataType
    $param.Value = $Value
    $node.ParamForm.Params = $param
    return $node
}

function Dispose-PlcNode {
    param([object]$Node)
    if ($null -eq $Node) { return }
    if ($null -ne $Node.ParamForm) { $Node.ParamForm.Dispose() }
    $Node.Dispose()
}

# 两个不同PLC对象但相同物理连接必须互斥，参数替换不能污染已排队快照。
[PlcWriteInvocationProxy]::ResetConcurrency()
$controller1 = [PlcWriteInvocationProxy]::new($plcInterfaceType)
$controller2 = [PlcWriteInvocationProxy]::new($plcInterfaceType)
$controller1.WriteDelayMs = 80
$controller2.WriteDelayMs = 20
$node1 = New-PlcNode 9401 $controller1 'D100' 'Int32' '10'
$node2 = New-PlcNode 9402 $controller2 'D200' 'Int32' '1,-2'
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context1 = New-Context $epoch 'PLC-LINE-A' 1
$context2 = New-Context $epoch 'PLC-LINE-B' 1
$coordinator.RegisterWorkpiece($context1)
$coordinator.RegisterWorkpiece($context2)
$task1 = $node1.SubmitOrderedSignalAsync($context1, 'flow-a/plc', $coordinator, $none)
$deadline = [DateTime]::UtcNow.AddSeconds(2)
while ([PlcWriteInvocationProxy]::ActiveWriteCount -eq 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 1 }
Assert-True ([PlcWriteInvocationProxy]::ActiveWriteCount -eq 1) '第一个假PLC写入没有开始。'
$sealTask1 = $coordinator.CompleteWorkpieceAsync($context1, $succeeded)
Assert-True (-not $sealTask1.IsCompleted) 'PLC写入仍在执行时工件被提前封口。'
$task2 = $node2.SubmitOrderedSignalAsync($context2, 'flow-b/plc', $coordinator, $none)
$replacement = [Activator]::CreateInstance($plcParamType)
$controller2.AssignProxyToProperty($replacement, 'Plc')
$replacement.PlcName = '假PLC'
$replacement.Address = 'D999'
$replacement.DataType = 'Int32'
$replacement.Value = '999'
$node2.ParamForm.Params = $replacement
$result1 = Wait-TaskResult $task1 '第一个假PLC写入未完成。'
$result2 = Wait-TaskResult $task2 '第二个假PLC写入未完成。'
Assert-True ($result1.Status -eq $deviceAcknowledged -and $result2.Status -eq $deviceAcknowledged) 'PLC成功响应没有转换为设备确认。'
Assert-True (-not [string]::IsNullOrWhiteSpace($result2.ConfirmationEvidence)) 'PLC设备确认缺少可记录凭据。'
Assert-True ([PlcWriteInvocationProxy]::MaximumWriteCount -eq 1) '同一物理PLC连接发生并发写入。'
Assert-True ($controller2.Calls -contains 'WriteIntAsync:D200:[1,-2]') 'PLC排队动作没有使用提交时解析的地址和值。'
Assert-True (-not ($controller2.Calls -match ':D999:')) '参数替换污染了已经提交的PLC写入快照。'
Wait-Task $sealTask1 '第一个PLC工件未在写入结束后封口。'
Wait-Task ($coordinator.CompleteWorkpieceAsync($context2, $succeeded)) '第二个PLC工件未封口。'
$coordinator.Dispose()
Dispose-PlcNode $node1
Dispose-PlcNode $node2

# PLC返回失败响应时副作用无法确认，必须返回Unknown并把工件升级为Stopped。
$failureController = [PlcWriteInvocationProxy]::new($plcInterfaceType)
$failureController.Succeed = $false
$failureNode = New-PlcNode 9501 $failureController 'M100' 'Boolean' '1'
$failureCoordinator = [Activator]::CreateInstance($coordinatorType)
$failureEpoch = [Guid]::NewGuid()
$failureContext = New-Context $failureEpoch 'PLC-FAILURE' 1
$failureCoordinator.RegisterWorkpiece($failureContext)
$failureResult = Wait-TaskResult (
    $failureNode.SubmitOrderedSignalAsync(
        $failureContext,
        'flow-failure/plc',
        $failureCoordinator,
        $none)) '失败响应的假PLC写入未返回。'
Assert-True ($failureResult.Status -eq $unknown) 'PLC失败响应必须保守归类为Unknown。'
Wait-Task ($failureCoordinator.CompleteWorkpieceAsync($failureContext, $succeeded)) '失败响应PLC工件未封口。'
$failureTerminal = Wait-TaskResult $failureContext.Completion '失败响应PLC工件没有终态。'
Assert-True ($failureTerminal -eq $stopped) 'PLC Unknown结果没有把工件升级为Stopped。'
$failureCoordinator.Dispose()
Dispose-PlcNode $failureNode

# 发送权取得前取消必须永久移除意图，不能调用PLC写入接口。
$cancelController = [PlcWriteInvocationProxy]::new($plcInterfaceType)
$cancelNode = New-PlcNode 9601 $cancelController 'M200' 'Boolean' '0'
$cancelCoordinator = [Activator]::CreateInstance($coordinatorType)
$cancelEpoch = [Guid]::NewGuid()
$cancelContext = New-Context $cancelEpoch 'PLC-CANCEL' 1
$cancelCoordinator.RegisterWorkpiece($cancelContext)
$cancelSource = [Threading.CancellationTokenSource]::new()
$cancelSource.Cancel()
$cancelResult = Wait-TaskResult (
    $cancelNode.SubmitOrderedSignalAsync(
        $cancelContext,
        'flow-cancel/plc',
        $cancelCoordinator,
        $cancelSource.Token)) '发送前取消的PLC意图未返回。'
Assert-True ($cancelResult.Status -eq $cancelledBeforeStart) 'PLC发送前取消没有返回CancelledBeforeStart。'
Assert-True ($cancelController.Calls.Count -eq 0) 'PLC发送前取消仍调用了写入接口。'
Wait-Task ($cancelCoordinator.CompleteWorkpieceAsync($cancelContext, $succeeded)) '取消PLC工件未封口。'
$cancelSource.Dispose()
$cancelCoordinator.Dispose()
Dispose-PlcNode $cancelNode

# 数值格式在发送前验证：负数和科学计数法合法，NaN和非法布尔值拒绝。
$validateMethod = $plcNodeClassType.GetMethod('ValidateWriteValue')
$null = $validateMethod.Invoke($null, @('Int32', '-5'))
$null = $validateMethod.Invoke($null, @('Single', '1e-3'))
$nanRejected = $false
try { $null = $validateMethod.Invoke($null, @('Single', 'NaN')) } catch { $nanRejected = $true }
Assert-True $nanRejected 'PLC浮点写入没有拒绝NaN。'
$invalidBooleanRejected = $false
try { $null = $validateMethod.Invoke($null, @('Boolean', 'abc')) } catch { $invalidBooleanRejected = $true }
Assert-True $invalidBooleanRejected 'PLC布尔写入没有拒绝非法文本。'

Write-Host 'PLC写入有序协调器接入检查通过。'
