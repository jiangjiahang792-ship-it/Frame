$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-MethodBody {
    param([string]$Source, [string]$Signature, [string]$NextMarker)
    $start = $Source.IndexOf($Signature, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        throw "Method signature not found: $Signature"
    }
    $end = $Source.IndexOf($NextMarker, $start, [System.StringComparison]::Ordinal)
    if ($end -lt 0) {
        throw "Method end marker not found: $NextMarker"
    }
    return $Source.Substring($start, $end - $start)
}

$root = Split-Path -Parent $PSScriptRoot
$cameraPath = Join-Path $root 'Device/Camera/CameraHik.cs'
$cameraInterfacePath = Join-Path $root 'Device/Camera/ICamera.cs'
$singleCameraPath = Join-Path $root 'Forms/CameraAdd/SingleCamera.cs'
$cameraSource = Get-Content -LiteralPath $cameraPath -Raw -Encoding UTF8
$cameraInterfaceSource = Get-Content -LiteralPath $cameraInterfacePath -Raw -Encoding UTF8
$singleCameraSource = Get-Content -LiteralPath $singleCameraPath -Raw -Encoding UTF8

$openBody = Get-MethodBody $cameraSource 'public bool Open()' '/// <summary>注册需要相机帧回调的拥有者。'
$closeBody = Get-MethodBody $cameraSource 'public void Close()' '/// <summary>关闭相机并销毁原生设备句柄。'
$callbackRegistrationBody = Get-MethodBody $cameraSource 'private void EnsureNativeCallbackRegisteredCore()' '/// <summary>创建原生相机句柄并替换当前未打开的句柄。'
$createNativeBody = Get-MethodBody $cameraSource 'private void CreateNativeDeviceCore(' '/// <summary>把枚举信息同步到可序列化的相机属性。'
$ensureOpenBody = Get-MethodBody $cameraSource 'private void EnsureDeviceOpen()' '/// <summary>对象完成永久销毁后禁止再次访问'

Assert-True ($cameraSource.Contains('private int _nativeDeviceOpened;')) '原生真实打开状态必须是非序列化私有字段。'
Assert-True ($cameraSource.Contains('private int _deviceConfigurationCompleted;')) 'SDK句柄打开与相机配置完成必须使用不同状态。'
Assert-True ($cameraSource.Contains('private int _restoreConnectionRequested;')) '保存恢复连接请求必须与原生状态使用不同字段。'
Assert-True ($cameraSource.Contains('[JsonIgnore]') -and $cameraSource.Contains('public bool IsOpen')) '运行时IsOpen不得直接序列化。'
Assert-True ($cameraSource.Contains('[JsonProperty("IsOpen")]')) '旧方案IsOpen字段必须映射到兼容的持久化属性。'
Assert-True ($cameraInterfaceSource.Contains('bool RestoreConnectionRequested { get; }')) '相机接口必须公开只读恢复连接请求。'
Assert-True ($openBody.Contains('Volatile.Read(ref _nativeDeviceOpened) != 0')) 'Open只能根据原生真实状态判断是否已经打开。'
Assert-True ($openBody.Contains('Volatile.Read(ref _deviceConfigurationCompleted) != 0')) 'Open只有在句柄和配置都完成时才能提前返回成功。'
Assert-True ($openBody.Contains('if (Volatile.Read(ref _nativeDeviceOpened) == 0)')) '配置未完成但句柄仍打开时必须跳过重复SDK Open并重跑配置。'
Assert-True (-not $openBody.Contains('if (IsOpen)')) '保存文件恢复的IsOpen不能让Open跳过SDK打开。'
Assert-True ($openBody.Contains('_device.MV_CC_OpenDevice_NET()')) '自动重连必须真实调用海康SDK打开设备。'
Assert-True ($openBody.Contains('Volatile.Write(ref _nativeDeviceOpened, 1)')) 'SDK打开成功后必须记录原生真实状态。'
Assert-True ($openBody.Contains('if (closeResult == MyCamera.MV_OK)')) '打开后配置失败只能在SDK回滚关闭成功时清除真实状态。'
Assert-True ($openBody.Contains('Volatile.Write(ref _deviceConfigurationCompleted, 0)')) '配置异常必须标记为未完成。'
Assert-True ($openBody.Contains('lock (_imageCallbackLock)')) 'Open配置完成和失败回滚都必须与回调注册串行。'
Assert-True ($openBody.IndexOf('RegisterNativeCallbackCore();', [System.StringComparison]::Ordinal) -lt $openBody.IndexOf('Volatile.Write(ref _deviceConfigurationCompleted, 1);', [System.StringComparison]::Ordinal)) '触发源和回调全部完成后才能开放配置完成门禁。'
Assert-True ($cameraSource.Contains('private TriggerSource GetTriggerSourceCore()')) 'Open必须使用不提前开放配置门禁的触发源Core方法。'
Assert-True ($cameraSource.Contains('private CameraEnumValue GetEnumValueCore(string key)')) 'Open内部枚举读取必须使用专用Core方法。'
Assert-True ($closeBody.Contains('lock (_imageCallbackLock)')) '关闭设备必须与回调注册串行。'
Assert-True ($closeBody.Contains('Volatile.Write(ref _restoreConnectionRequested, 0)')) '用户关闭相机必须清除下次恢复连接请求。'
Assert-True ($createNativeBody.Contains('Volatile.Read(ref _nativeDeviceOpened) != 0')) '创建设备句柄只能拒绝真实已打开的相机。'
Assert-True (-not $createNativeBody.Contains('if (IsOpen || _isGrabbing)')) '反序列化的IsOpen=true不能阻止重建原生句柄。'
Assert-True ($createNativeBody.Contains('if (destroyResult != MyCamera.MV_OK)')) '重建前销毁旧句柄失败必须停止覆盖旧引用。'
Assert-True ($createNativeBody.Contains('MyCamera newDevice = new MyCamera();')) '新句柄创建成功前不得覆盖当前设备字段。'
Assert-True ($ensureOpenBody.Contains('Volatile.Read(ref _nativeDeviceOpened) == 0')) 'SDK操作必须验证原生真实打开状态。'
Assert-True ($ensureOpenBody.Contains('Volatile.Read(ref _deviceConfigurationCompleted) == 0')) 'SDK操作必须拒绝尚未完成配置的句柄。'
Assert-True ($callbackRegistrationBody.Contains('Volatile.Read(ref _deviceConfigurationCompleted) == 0')) '配置完成前不得注册原生回调。'
Assert-True ($singleCameraSource.Contains('camera.IsOpen || camera.RestoreConnectionRequested')) '保存时已连接状态必须通过独立请求触发启动后的自动重连。'

# 守护只隔离单相机，取流和参数恢复成功之前保持节点等待，失败后继续重试。
$solutionSource = Get-Content -LiteralPath (Join-Path $root 'Solution.cs') -Raw -Encoding UTF8
$imageSource = Get-Content -LiteralPath (Join-Path $root 'Node/1-Acquisition/ImageSource/NodeImageSource.cs') -Raw -Encoding UTF8
$reconnectBody = Get-MethodBody $solutionSource 'private void ReconnectDisconnectedCamera(ICamera camera)' '/// <summary>判断相机重连成功后是否需要恢复取流。'
Assert-True (-not $solutionSource.Contains('StopRunningSolutionForCameraDisconnect')) '相机掉线守护不得主动请求停止整个方案。'
Assert-True (-not $reconnectBody.Contains('Stop();')) '重连入口不得调用方案停止。'
Assert-True ($reconnectBody.IndexOf('SuspendCameraForReconnect(') -lt $reconnectBody.IndexOf('camera.TryReconnect()')) '物理重连前必须先隔离旧票据。'
Assert-True ($reconnectBody.Contains('IsCameraUsedByActiveImageSource(camera) && !parametersApplied')) '方案参数恢复失败不得提前恢复生产。'
Assert-True ($reconnectBody.Contains('if (camera.IsOpen && camera.GetGrabStatus())')) '恢复取流成功后才能放行新取图。'
Assert-True ($solutionSource.Contains('connected && !IsCameraReconnecting(camera)')) '仅SDK在线但恢复未完成的相机必须继续重试。'
Assert-True ($solutionSource.Contains('await Task.Delay(100, token)')) '重连等待必须异步且响应用户停止。'
Assert-True ($imageSource.Contains('await Solution.Instance.WaitForCameraReconnectAsync(camera, token)')) '图像源取图前必须等待当前相机恢复。'
Write-Host '海康自动重连回归检查通过：持久化连接意图、真实状态、单相机隔离及不停方案恢复结构符合预期。'
