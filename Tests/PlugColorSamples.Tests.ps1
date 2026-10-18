# 调用真实解析器进行图像试验，不加载模型、不连接设备、不更改现场方案。
param([string]$BuildDirectory = 'bin/NewTerminal12Validation', [string]$SampleDirectory = 'outputs/PlugColorSamples-20261018')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$samples = Join-Path $root $SampleDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'PlugColorSamples.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'PlugColorSamples.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '样图测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output $samples
    if ($LASTEXITCODE -ne 0) { throw '样图测试运行失败。' }
} finally { Pop-Location }
