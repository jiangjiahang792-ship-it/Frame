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

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    if (-not $Text.Contains($Needle)) {
        throw $Message
    }
}

$testRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Join-Path (Get-Location) 'Tests'
}
else {
    $PSScriptRoot
}

$programPath = Join-Path $testRoot '..\Program.cs'
$programSource = Get-Content -LiteralPath $programPath -Raw -Encoding UTF8

Assert-Contains $programSource 'private const string SingleInstanceMutexName' 'Program.cs 应定义单实例互斥锁名称常量。'
Assert-Contains $programSource 'Local\TDJS_Vision_SingleInstance' '单实例互斥锁应使用 Local 命名空间，限制同一桌面会话重复启动。'
Assert-Contains $programSource 'using (Mutex singleInstanceMutex = new Mutex(false, SingleInstanceMutexName))' 'Program.Main 应创建命名 Mutex 保护主程序生命周期。'
Assert-Contains $programSource 'private static bool TryEnterSingleInstance(Mutex singleInstanceMutex)' '互斥锁抢占逻辑应抽成独立方法，便于阅读和后续扩展。'
Assert-Contains $programSource 'catch (AbandonedMutexException)' '上次异常退出留下废弃互斥锁时，下一次启动应允许接管。'
Assert-Contains $programSource 'MessageBoxTD.Show("软件已经运行，请勿重复启动！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);' '重复启动时应弹出中文提示并退出。'
Assert-Contains $programSource 'private static void RunApplication(string[] args)' '正常启动流程应抽成 RunApplication，避免互斥锁逻辑和业务初始化混在一起。'
Assert-Contains $programSource 'singleInstanceMutex.ReleaseMutex();' '主程序退出时应释放互斥锁。'

$mutexIndex = $programSource.IndexOf('using (Mutex singleInstanceMutex = new Mutex(false, SingleInstanceMutexName))')
$threadPoolIndex = $programSource.IndexOf('ConfigureThreadPoolMinimums();')
$diagnosticLogIndex = $programSource.IndexOf('LogHelper.AddLog(')
$authorizationIndex = $programSource.IndexOf('HslCommunication.Authorization.SetAuthorizationCode')
$runApplicationIndex = $programSource.IndexOf('RunApplication(args);')
$releaseIndex = $programSource.IndexOf('singleInstanceMutex.ReleaseMutex();')

Assert-True ($mutexIndex -ge 0) 'Program.Main 中没有找到单实例互斥锁创建位置。'
Assert-True ($threadPoolIndex -ge 0) 'Program.cs 缺少线程池配置调用。'
Assert-True ($diagnosticLogIndex -ge 0) 'Program.cs 缺少启动诊断日志。'
Assert-True ($authorizationIndex -ge 0) 'Program.cs 缺少 HslCommunication 授权调用。'
Assert-True ($runApplicationIndex -gt $mutexIndex) '正常启动必须在拿到互斥锁后再进入 RunApplication。'
Assert-True ($mutexIndex -lt $threadPoolIndex) '单实例互斥锁应在线程池配置前创建，避免第二进程继续做启动初始化。'
Assert-True ($mutexIndex -lt $diagnosticLogIndex) '单实例互斥锁应在启动诊断日志前创建，避免第二进程写入启动诊断。'
Assert-True ($mutexIndex -lt $authorizationIndex) '单实例互斥锁应在通信库授权前创建，避免第二进程继续初始化外部资源。'
Assert-True ($releaseIndex -gt $runApplicationIndex) '互斥锁释放应发生在主程序运行结束之后。'

Write-Host '程序单实例启动回归检查通过。'
