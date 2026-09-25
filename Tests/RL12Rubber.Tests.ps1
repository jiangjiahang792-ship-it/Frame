# 编译并调用真实程序的 RL12 解析器，离线验证，不加载模型或连接设备。
param([string]$BuildDirectory = 'bin/RL12RubberValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'RL12Rubber.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'RL12Rubber.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'RL12验证程序编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'RL12胶皮与飞胶离线验证失败。' }
} finally { Pop-Location }
