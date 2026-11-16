# 编译并调用真实程序中的宏晟端子12类解析器，不加载模型或连接现场设备。
param([string]$BuildDirectory = 'bin/HongShengValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'HongShengTerminal12.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'HongShengTerminal12.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '宏晟端子12类验证程序编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '宏晟端子12类离线验证失败。' }
} finally { Pop-Location }
