$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$nodePath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\ComSend\NodeComSend.cs'
$devicePath = Join-Path $projectRoot 'Device\COM\ComDevice.cs'
$paramFormPath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\ComSend\ParamFormComSend.cs'
$solutionPath = Join-Path $projectRoot 'Solution.cs'
$nodeSourceFile = Get-Item -LiteralPath $nodePath
$deviceSourceFile = Get-Item -LiteralPath $devicePath
$paramFormSourceFile = Get-Item -LiteralPath $paramFormPath
$solutionSourceFile = Get-Item -LiteralPath $solutionPath
Assert-True ($null -ne $application) '串口有序发送测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge $nodeSourceFile.LastWriteTimeUtc -and $application.LastWriteTimeUtc -ge $deviceSourceFile.LastWriteTimeUtc -and $application.LastWriteTimeUtc -ge $paramFormSourceFile.LastWriteTimeUtc -and $application.LastWriteTimeUtc -ge $solutionSourceFile.LastWriteTimeUtc) 'Debug程序早于串口发送源码，请先重新编译。'

$source = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$deviceSource = Get-Content -LiteralPath $devicePath -Raw -Encoding UTF8
$paramFormSource = Get-Content -LiteralPath $paramFormPath -Raw -Encoding UTF8
$solutionSource = Get-Content -LiteralPath $solutionPath -Raw -Encoding UTF8
Assert-True ($source.Contains('NodeBase, IOrderedExternalSignalNode')) '串口发送节点必须实现统一有序外发接口。'
Assert-True ($source.Contains('WorkpieceContextAccessor.Current')) '串口发送运行入口必须取得当前工件上下文。'
Assert-True ($source.Contains('Solution.Instance.OrderedSignalCoordinator')) '串口发送运行入口必须使用当前生产会话协调器。'
Assert-True ($source.Contains('await SubmitOrderedSignalAsync(')) '串口发送运行入口必须等待明确协调结果。'
Assert-True (-not $source.Contains('param.Dev.Send(param.Cmd, param.Encoding)')) '串口发送运行入口不得直接读取共享参数并写设备。'
Assert-True ($source.Contains('ComDevice deviceSnapshot = param.Dev;')) '串口发送必须冻结本轮设备对象。'
Assert-True ($source.Contains('string commandSnapshot = param.Cmd;')) '串口发送必须冻结本轮命令。'
Assert-True ($source.Contains('string encodingSnapshot = param.Encoding;')) '串口发送必须冻结本轮编码。'
Assert-True ($source.Contains('device.ComParams?.PortName')) '串口端点键必须优先使用真实物理端口名。'
Assert-True ($source.Contains('throw new InvalidOperationException("串口物理端口名不能为空。")')) '串口物理端口名为空时必须明确拒绝。'
Assert-True ($source.Contains('OrderedSignalSendStatus.LocalCallCompleted')) '串口API正常返回必须记录为本地调用完成。'
Assert-True ([regex]::Matches($source, '\.Send\(').Count -eq 1) '真实串口写入只能存在于冻结动作执行方法中。'
Assert-True ($deviceSource.Contains('_serialPort.Close();') -and $deviceSource.Contains('IsOpen = false;')) '串口关闭后必须同步清除公开连接状态。'
Assert-True ($deviceSource.Contains('throw new ArgumentException("串口发送数据不能为空。", nameof(data));')) '空串口命令不得静默成功。'
Assert-True ($deviceSource.Contains('public int WriteTimeout { get; set; } = 2000;')) '串口写入必须具有默认有限超时。'
Assert-True ($deviceSource.Contains('port.WriteTimeout = Math.Min(60000, Math.Max(100, WriteTimeout));')) '串口写超时必须限制在安全范围。'
Assert-True ($paramFormSource.Contains('private async void buttonTest_Click')) '参数页测试发送不得同步阻塞UI线程。'
Assert-True ($paramFormSource.Contains('TryExecuteManualExternalSignal(')) '参数页测试发送必须通过生产会话门禁。'
Assert-True ($paramFormSource.Contains('private bool SaveParams()')) '参数保存必须返回明确成功状态。'
Assert-True ($paramFormSource.Contains('if (!SaveParams())')) '测试发送必须在参数保存失败后立即终止。'
Assert-True ($paramFormSource.Contains('if (com == null)')) '所选串口已移除时必须明确拒绝，不能读取空设备。'
Assert-True ($solutionSource.Contains('if (_isResetting || _activeRunSessionCount != 0)')) '手动外发门禁必须拒绝生产运行和方案重置期间的动作。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$nodeType = $assembly.GetType('TDJS_Vision.Node._5_EquipmentCommunication.ComSend.NodeComSend', $true)
$interfaceType = $assembly.GetType('TDJS_Vision.ResourceManagement.IOrderedExternalSignalNode', $true)
Assert-True ($interfaceType.IsAssignableFrom($nodeType)) '编译程序集中的串口节点未实现有序外发接口。'
Assert-True ($null -ne $nodeType.GetProperty('OrderedSignalKind')) '串口节点缺少有序节点类别属性。'
Assert-True ($null -ne $nodeType.GetProperty('OrderedSignalEndpointKey')) '串口节点缺少物理端点键属性。'
Assert-True ($null -ne $nodeType.GetMethod('SubmitOrderedSignalAsync')) '串口节点缺少统一有序提交方法。'

Write-Host '串口发送有序协调器接入检查通过。'
