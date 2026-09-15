# 编译运行真实程序集离线测试，不连接设备或加载真实模型。
param([string]$BuildDirectory = 'bin/UltrasoundValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'UltrasoundSide.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'UltrasoundSide.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '超声波验证编译失败。' }
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '超声波离线验证失败。' }
} finally { Pop-Location }
