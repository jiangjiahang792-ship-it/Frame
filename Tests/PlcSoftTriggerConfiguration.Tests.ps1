# 编译并运行PLC软触发配置离线测试，不连接PLC。
param([string]$BuildDirectory = 'bin/PlcSoftTriggerConfigurationValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'PlcSoftTriggerConfiguration.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'PlcSoftTriggerConfiguration.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'PLC软触发配置验证编译失败。' }
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'PLC软触发配置离线验证失败。' }
} finally { Pop-Location }
