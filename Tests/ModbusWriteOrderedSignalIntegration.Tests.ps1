$ErrorActionPreference = 'Stop'

# 生产程序面向.NET Framework 4.8，真实执行测试统一交给Windows PowerShell。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $escapedScriptPath = $PSCommandPath.Replace("'", "''")
    $env:TDJS_MODBUS_WRITE_TEST_PATH = $PSCommandPath
    $command = "`$scriptText = Get-Content -LiteralPath '$escapedScriptPath' -Raw -Encoding UTF8; try { Invoke-Expression `$scriptText } catch { Write-Host (`$_.Exception.ToString()); Write-Host `$_.ScriptStackTrace; exit 1 }"
    & $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$scriptDirectory = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $env:TDJS_MODBUS_WRITE_TEST_PATH
} else {
    $PSScriptRoot
}
$projectRoot = Split-Path -Parent $scriptDirectory
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$sourcePaths = @(
    'Node\5-EquipmentCommunication\ModbusWrite\NodeModbusWrite.cs',
    'Node\5-EquipmentCommunication\ModbusWrite\ParamFormModbusWrite.cs',
    'Device\Modbus\IModbus.cs',
    'Device\Modbus\ModbusTcpPoll.cs',
    'Device\Modbus\ModbusRTUPoll.cs')
Assert-True ($null -ne $application) 'Modbus有序写入测试需要最新Debug程序。'
foreach ($relativePath in $sourcePaths) {
    $sourceFile = Get-Item -LiteralPath (Join-Path $projectRoot $relativePath)
    Assert-True ($application.LastWriteTimeUtc -ge $sourceFile.LastWriteTimeUtc) "Debug程序早于$relativePath，请先重新编译。"
}

$nodeSource = Get-Content -LiteralPath (Join-Path $projectRoot $sourcePaths[0]) -Raw -Encoding UTF8
$formSource = Get-Content -LiteralPath (Join-Path $projectRoot $sourcePaths[1]) -Raw -Encoding UTF8
$interfaceSource = Get-Content -LiteralPath (Join-Path $projectRoot $sourcePaths[2]) -Raw -Encoding UTF8
$tcpSource = Get-Content -LiteralPath (Join-Path $projectRoot $sourcePaths[3]) -Raw -Encoding UTF8
$rtuSource = Get-Content -LiteralPath (Join-Path $projectRoot $sourcePaths[4]) -Raw -Encoding UTF8
Assert-True ($nodeSource.Contains('NodeBase, IOrderedExternalSignalNode')) 'Modbus写入节点必须实现统一有序外发接口。'
Assert-True ($nodeSource.Contains('WorkpieceContextAccessor.Current')) 'Modbus写入运行入口必须取得当前工件上下文。'
Assert-True ($nodeSource.Contains('ModbusWriteSignalSnapshot signalSnapshot = CaptureSignalSnapshot();')) 'Modbus动态值必须一次性冻结。'
Assert-True (-not $nodeSource.Contains('param.Data =')) '订阅值不得写回共享节点参数。'
Assert-True ($nodeSource.Contains('TryExecuteManualExternalSignal(')) 'Modbus人工写入必须经过生产会话门禁。'
Assert-True ($nodeSource.Contains('OrderedSignalSendStatus.DeviceAcknowledged')) 'Modbus成功响应必须记录设备确认。'
Assert-True ($nodeSource.Contains('CultureInfo.InvariantCulture')) 'Modbus数字必须使用固定区域解析。'
Assert-True ($nodeSource.Contains('float.IsNaN(parsed)') -and $nodeSource.Contains('double.IsNaN(parsed)')) 'Modbus浮点值必须拒绝NaN。'
Assert-True ($formSource.Contains('private async void buttonRun_Click')) 'Modbus参数页必须等待人工写入完成。'
Assert-True ($interfaceSource.Contains('int OperationTimeoutMs { get; set; }')) 'Modbus接口必须公开有限操作超时。'
Assert-True ($tcpSource.Contains('ConnectTimeOut = _operationTimeoutMs')) 'Modbus TCP必须应用有限连接超时。'
Assert-True ($tcpSource.Contains('ReceiveTimeOut = _operationTimeoutMs')) 'Modbus TCP必须应用有限收发超时。'
Assert-True ($tcpSource.Contains('EnsureConnectionSucceeded(connectResult);')) 'Modbus TCP必须检查ConnectServer返回结果。'
Assert-True ($rtuSource.Contains('port.ReadTimeout = _operationTimeoutMs;')) 'Modbus RTU必须应用底层串口读取超时。'
Assert-True ($rtuSource.Contains('port.WriteTimeout = _operationTimeoutMs;')) 'Modbus RTU必须应用底层串口写入超时。'

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

public sealed class ModbusWriteInvocationProxy : RealProxy
{
    private static int s_activeWriteCount;
    private static int s_maximumWriteCount;
    private readonly object _syncRoot = new object();
    private readonly List<string> _calls = new List<string>();

    public ModbusWriteInvocationProxy(Type interfaceType) : base(interfaceType)
    {
        IpAddress = "192.168.10.30";
        Port = 502;
        PortName = "COM8";
        WriteDelayMs = 50;
    }

    public string IpAddress { get; set; }
    public int Port { get; set; }
    public string PortName { get; set; }
    public bool UseRtu { get; set; }
    public int WriteDelayMs { get; set; }
    public bool ThrowOnWrite { get; set; }

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
        if (method.Name == "get_ModbusParam")
            return CreateParameters(method.ReturnType.Assembly);
        if (method.Name == "get_DevType")
            return Enum.Parse(method.ReturnType, UseRtu ? "ModbusRTUPoll" : "ModbusTcpPoll");
        if (method.Name == "get_Brand")
            return Enum.ToObject(method.ReturnType, 0);
        if (method.Name == "Write")
        {
            string callText = method.Name + ":" + arguments[0] + ":" + FormatValue(arguments[1]);
            lock (_syncRoot)
                _calls.Add(callText);
            int active = Interlocked.Increment(ref s_activeWriteCount);
            UpdateMaximum(active);
            try
            {
                Thread.Sleep(Math.Max(0, WriteDelayMs));
                if (ThrowOnWrite)
                    throw new InvalidOperationException("模拟Modbus协议响应失败");
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref s_activeWriteCount);
            }
        }

        return method.ReturnType == typeof(void)
            ? null
            : method.ReturnType.IsValueType
                ? Activator.CreateInstance(method.ReturnType)
                : null;
    }

    private object CreateParameters(Assembly assembly)
    {
        if (UseRtu)
        {
            Type rtuParameterType = assembly.GetType("TDJS_Vision.Device.Modbus.ModbusRTUParam", true);
            object rtuParameters = Activator.CreateInstance(rtuParameterType);
            rtuParameterType.GetProperty("PortName").SetValue(rtuParameters, PortName, null);
            PropertyInfo rtuDevTypeProperty = rtuParameterType.GetProperty("DevType");
            rtuDevTypeProperty.SetValue(
                rtuParameters,
                Enum.Parse(rtuDevTypeProperty.PropertyType, "ModbusRTUPoll"),
                null);
            return rtuParameters;
        }

        Type parameterType = assembly.GetType("TDJS_Vision.Device.Modbus.ModbusTcpParam", true);
        object parameters = Activator.CreateInstance(parameterType);
        parameterType.GetField("IP").SetValue(parameters, IpAddress);
        parameterType.GetField("Port").SetValue(parameters, Port);
        PropertyInfo devTypeProperty = parameterType.GetProperty("DevType");
        devTypeProperty.SetValue(parameters, Enum.Parse(devTypeProperty.PropertyType, "ModbusTcpPoll"), null);
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

$modbusInterfaceType = $assembly.GetType('TDJS_Vision.Device.Modbus.IModbus', $true)
$nodeClassType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite.NodeModbusWrite', $true)
$paramType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite.NodeParamModbusWrite', $true)
$registerType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead.RegistersType', $true)
$orderedNodeInterfaceType = $assembly.GetType('TDJS_Vision.ResourceManagement.IOrderedExternalSignalNode', $true)
$nodeKindEnumType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$coordinatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalCoordinator', $true)
$terminalType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceTerminalState', $true)
$sendStatusType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendStatus', $true)
$tcpPollType = $assembly.GetType('TDJS_Vision.Device.Modbus.ModbusTcpPoll', $true)
$operateResultType = $assembly.GetType('HslCommunication.OperateResult', $false)
if ($null -eq $operateResultType) {
    $operateResultType = ([Reflection.Assembly]::LoadFrom((Join-Path $debugDirectory 'HslCommunication.dll'))).GetType('HslCommunication.OperateResult', $true)
}
Assert-True ($orderedNodeInterfaceType.IsAssignableFrom($nodeClassType)) '编译程序集中的Modbus写入节点未实现有序外发接口。'
$nodeKind = [Enum]::Parse($nodeKindEnumType, 'ModbusWrite')
$succeeded = [Enum]::Parse($terminalType, 'Succeeded')
$stopped = [Enum]::Parse($terminalType, 'Stopped')
$deviceAcknowledged = [Enum]::Parse($sendStatusType, 'DeviceAcknowledged')
$unknown = [Enum]::Parse($sendStatusType, 'Unknown')
$cancelledBeforeStart = [Enum]::Parse($sendStatusType, 'CancelledBeforeStart')
$none = [Threading.CancellationToken]::None

# HSL连接失败返回不能被误报为连接成功。
$ensureConnectMethod = $tcpPollType.GetMethod(
    'EnsureConnectionSucceeded',
    [Reflection.BindingFlags]'Static,NonPublic')
Assert-True ($null -ne $ensureConnectMethod) 'Modbus TCP缺少连接结果检查方法。'
$failedConnectResult = [Activator]::CreateInstance($operateResultType)
$connectFailureRejected = $false
try { $null = $ensureConnectMethod.Invoke($null, @($failedConnectResult)) } catch { $connectFailureRejected = $true }
Assert-True $connectFailureRejected 'Modbus TCP没有拒绝IsSuccess=false的连接结果。'
$nullConnectRejected = $false
try { $null = $ensureConnectMethod.Invoke($null, @($null)) } catch { $nullConnectRejected = $true }
Assert-True $nullConnectRejected 'Modbus TCP没有拒绝空连接结果。'

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
        @($identity, $Sequence, $Domain, 'modbus-write-test', [DateTime]::UtcNow, $none))
}

function New-ModbusNode {
    param(
        [int]$Id,
        [ModbusWriteInvocationProxy]$Controller,
        [string]$Address,
        [string]$DataType,
        [string]$Value)
    $node = [Activator]::CreateInstance(
        $nodeClassType,
        @($Id, "Modbus写入测试$Id", $null, $nodeKind))
    $param = [Activator]::CreateInstance($paramType)
    $Controller.AssignProxyToProperty($param, 'Device')
    $param.DeviceName = '假Modbus'
    $param.StartAddress = $Address
    $param.DataType = [Enum]::Parse($registerType, $DataType)
    $param.Data = $Value
    $param.IsSubscribed = $false
    $node.ParamForm.Params = $param
    return $node
}

function Dispose-ModbusNode {
    param([object]$Node)
    if ($null -eq $Node) { return }
    if ($null -ne $Node.ParamForm) { $Node.ParamForm.Dispose() }
    $Node.Dispose()
}

# 不同对象只要指向同一物理连接就必须互斥，参数替换不能污染已排队快照。
[ModbusWriteInvocationProxy]::ResetConcurrency()
$controller1 = [ModbusWriteInvocationProxy]::new($modbusInterfaceType)
$controller2 = [ModbusWriteInvocationProxy]::new($modbusInterfaceType)
$controller1.WriteDelayMs = 80
$controller2.WriteDelayMs = 20
$node1 = New-ModbusNode 9701 $controller1 '100' 'Int' '10'
$node2 = New-ModbusNode 9702 $controller2 '200' 'Int' '1,-2'
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context1 = New-Context $epoch 'MODBUS-LINE-A' 1
$context2 = New-Context $epoch 'MODBUS-LINE-B' 1
$coordinator.RegisterWorkpiece($context1)
$coordinator.RegisterWorkpiece($context2)
$task1 = $node1.SubmitOrderedSignalAsync($context1, 'flow-a/modbus', $coordinator, $none)
$deadline = [DateTime]::UtcNow.AddSeconds(2)
while ([ModbusWriteInvocationProxy]::ActiveWriteCount -eq 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 1 }
Assert-True ([ModbusWriteInvocationProxy]::ActiveWriteCount -eq 1) '第一个假Modbus写入没有开始。'
$sealTask1 = $coordinator.CompleteWorkpieceAsync($context1, $succeeded)
Assert-True (-not $sealTask1.IsCompleted) 'Modbus写入仍在执行时工件被提前封口。'
$task2 = $node2.SubmitOrderedSignalAsync($context2, 'flow-b/modbus', $coordinator, $none)
$replacement = [Activator]::CreateInstance($paramType)
$controller2.AssignProxyToProperty($replacement, 'Device')
$replacement.DeviceName = '假Modbus'
$replacement.StartAddress = '999'
$replacement.DataType = [Enum]::Parse($registerType, 'Int')
$replacement.Data = '999'
$replacement.IsSubscribed = $false
$node2.ParamForm.Params = $replacement
$result1 = Wait-TaskResult $task1 '第一个假Modbus写入未完成。'
$result2 = Wait-TaskResult $task2 '第二个假Modbus写入未完成。'
Assert-True ($result1.Status -eq $deviceAcknowledged -and $result2.Status -eq $deviceAcknowledged) 'Modbus正常响应没有转换为设备确认。'
Assert-True (-not [string]::IsNullOrWhiteSpace($result2.ConfirmationEvidence)) 'Modbus设备确认缺少可记录凭据。'
Assert-True ([ModbusWriteInvocationProxy]::MaximumWriteCount -eq 1) '同一物理Modbus连接发生并发写入。'
Assert-True ($controller2.Calls -contains 'Write:200:[1,-2]') 'Modbus排队动作没有使用提交时解析的地址和值。'
Assert-True (-not ($controller2.Calls -match ':999:')) '参数替换污染了已经提交的Modbus写入快照。'
Wait-Task $sealTask1 '第一个Modbus工件未在写入结束后封口。'
Wait-Task ($coordinator.CompleteWorkpieceAsync($context2, $succeeded)) '第二个Modbus工件未封口。'
$coordinator.Dispose()
Dispose-ModbusNode $node1
Dispose-ModbusNode $node2

# 两个不同RTU对象共用同一COM口时也必须按整条物理串口互斥。
[ModbusWriteInvocationProxy]::ResetConcurrency()
$rtuController1 = [ModbusWriteInvocationProxy]::new($modbusInterfaceType)
$rtuController2 = [ModbusWriteInvocationProxy]::new($modbusInterfaceType)
$rtuController1.UseRtu = $true
$rtuController2.UseRtu = $true
$rtuController1.PortName = 'COM12'
$rtuController2.PortName = 'com12'
$rtuController1.WriteDelayMs = 70
$rtuController2.WriteDelayMs = 20
$rtuNode1 = New-ModbusNode 9751 $rtuController1 '110' 'UShort' '1'
$rtuNode2 = New-ModbusNode 9752 $rtuController2 '120' 'UShort' '2'
$rtuCoordinator = [Activator]::CreateInstance($coordinatorType)
$rtuEpoch = [Guid]::NewGuid()
$rtuContext1 = New-Context $rtuEpoch 'MODBUS-RTU-A' 1
$rtuContext2 = New-Context $rtuEpoch 'MODBUS-RTU-B' 1
$rtuCoordinator.RegisterWorkpiece($rtuContext1)
$rtuCoordinator.RegisterWorkpiece($rtuContext2)
$rtuTask1 = $rtuNode1.SubmitOrderedSignalAsync($rtuContext1, 'flow-a/modbus-rtu', $rtuCoordinator, $none)
$rtuDeadline = [DateTime]::UtcNow.AddSeconds(2)
while ([ModbusWriteInvocationProxy]::ActiveWriteCount -eq 0 -and [DateTime]::UtcNow -lt $rtuDeadline) { Start-Sleep -Milliseconds 1 }
$rtuTask2 = $rtuNode2.SubmitOrderedSignalAsync($rtuContext2, 'flow-b/modbus-rtu', $rtuCoordinator, $none)
$rtuResult1 = Wait-TaskResult $rtuTask1 '第一个假RTU写入未完成。'
$rtuResult2 = Wait-TaskResult $rtuTask2 '第二个假RTU写入未完成。'
Assert-True ($rtuResult1.Status -eq $deviceAcknowledged -and $rtuResult2.Status -eq $deviceAcknowledged) 'RTU正常响应没有转换为设备确认。'
Assert-True ([ModbusWriteInvocationProxy]::MaximumWriteCount -eq 1) '同一物理RTU串口发生并发写入。'
Wait-Task ($rtuCoordinator.CompleteWorkpieceAsync($rtuContext1, $succeeded)) '第一个RTU工件未封口。'
Wait-Task ($rtuCoordinator.CompleteWorkpieceAsync($rtuContext2, $succeeded)) '第二个RTU工件未封口。'
$rtuCoordinator.Dispose()
Dispose-ModbusNode $rtuNode1
Dispose-ModbusNode $rtuNode2

# 已经开始写入后抛异常时副作用无法确认，必须返回Unknown并停止工件。
$failureController = [ModbusWriteInvocationProxy]::new($modbusInterfaceType)
$failureController.ThrowOnWrite = $true
$failureNode = New-ModbusNode 9801 $failureController '300' 'Bool' '1'
$failureCoordinator = [Activator]::CreateInstance($coordinatorType)
$failureEpoch = [Guid]::NewGuid()
$failureContext = New-Context $failureEpoch 'MODBUS-FAILURE' 1
$failureCoordinator.RegisterWorkpiece($failureContext)
$failureResult = Wait-TaskResult (
    $failureNode.SubmitOrderedSignalAsync(
        $failureContext,
        'flow-failure/modbus',
        $failureCoordinator,
        $none)) '异常假Modbus写入未返回。'
Assert-True ($failureResult.Status -eq $unknown) 'Modbus写入异常必须保守归类为Unknown。'
Wait-Task ($failureCoordinator.CompleteWorkpieceAsync($failureContext, $succeeded)) '异常Modbus工件未封口。'
$failureTerminal = Wait-TaskResult $failureContext.Completion '异常Modbus工件没有终态。'
Assert-True ($failureTerminal -eq $stopped) 'Modbus Unknown结果没有把工件升级为Stopped。'
$failureCoordinator.Dispose()
Dispose-ModbusNode $failureNode

# 发送权取得前取消必须永久移除意图，不能调用Modbus写入接口。
$cancelController = [ModbusWriteInvocationProxy]::new($modbusInterfaceType)
$cancelNode = New-ModbusNode 9901 $cancelController '400' 'Bool' '0'
$cancelCoordinator = [Activator]::CreateInstance($coordinatorType)
$cancelEpoch = [Guid]::NewGuid()
$cancelContext = New-Context $cancelEpoch 'MODBUS-CANCEL' 1
$cancelCoordinator.RegisterWorkpiece($cancelContext)
$cancelSource = [Threading.CancellationTokenSource]::new()
$cancelSource.Cancel()
$cancelResult = Wait-TaskResult (
    $cancelNode.SubmitOrderedSignalAsync(
        $cancelContext,
        'flow-cancel/modbus',
        $cancelCoordinator,
        $cancelSource.Token)) '发送前取消的Modbus意图未返回。'
Assert-True ($cancelResult.Status -eq $cancelledBeforeStart) 'Modbus发送前取消没有返回CancelledBeforeStart。'
Assert-True ($cancelController.Calls.Count -eq 0) 'Modbus发送前取消仍调用了写入接口。'
Wait-Task ($cancelCoordinator.CompleteWorkpieceAsync($cancelContext, $succeeded)) '取消Modbus工件未封口。'
$cancelSource.Dispose()
$cancelCoordinator.Dispose()
Dispose-ModbusNode $cancelNode

# 数值格式在发送前验证：负数和科学计数法合法，NaN和非法布尔值拒绝。
$validateMethod = $nodeClassType.GetMethod('ValidateWriteValue')
$null = $validateMethod.Invoke($null, @([Enum]::Parse($registerType, 'Int'), '-5'))
$null = $validateMethod.Invoke($null, @([Enum]::Parse($registerType, 'Double'), '1e-3'))
$nanRejected = $false
try { $null = $validateMethod.Invoke($null, @([Enum]::Parse($registerType, 'Float'), 'NaN')) } catch { $nanRejected = $true }
Assert-True $nanRejected 'Modbus浮点写入没有拒绝NaN。'
$invalidBooleanRejected = $false
try { $null = $validateMethod.Invoke($null, @([Enum]::Parse($registerType, 'Bool'), 'abc')) } catch { $invalidBooleanRejected = $true }
Assert-True $invalidBooleanRejected 'Modbus布尔写入没有拒绝非法文本。'

Write-Host 'Modbus写入有序协调器接入检查通过。'
