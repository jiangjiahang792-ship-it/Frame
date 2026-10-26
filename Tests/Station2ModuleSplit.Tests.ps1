# 使用已编译程序验证方案副本；不连接PLC、相机，不在交付方案上开启学习。
param(
    [string]$BuildDirectory = 'bin/x64/Debug',
    [switch]$FailureIsolationOnly,
    [string]$SchemePath = 'C:\Users\34652\Desktop\9_21号方案_工位2_双角度独立模块.Sol'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskRoot $BuildDirectory
$taskExecutable = Join-Path $taskBuild 'Station2ModuleSplit.Tests.exe'
$taskReferences = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'WeifenLuo.WinFormsUI.Docking.dll', 'OpenCvSharp.dll', 'System.Memory.dll') |
    ForEach-Object { '/reference:' + (Join-Path $taskBuild $_) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 `
    /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @taskReferences `
    "/out:$taskExecutable" (Join-Path $PSScriptRoot 'Station2ModuleSplit.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '工位2模块回归测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $taskBuild '机器视觉AI检测系统V1.0.exe.config') -Destination ($taskExecutable + '.config') -Force
if ($FailureIsolationOnly) {
    & $taskExecutable $SchemePath --failure-isolation
    if ($LASTEXITCODE -ne 0) { throw '单视角失败隔离回归失败。' }
    return
}
& $taskExecutable $SchemePath
if ($LASTEXITCODE -ne 0) { throw '工位2调参或判定回归失败。' }
& $taskExecutable $SchemePath --load --run-angles
if ($LASTEXITCODE -ne 0) { throw '工位2真实模块加载失败。' }
