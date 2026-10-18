# 编译并运行真实程序集离线测试，不加载现场模型或连接设备。
param([string]$BuildDirectory = 'bin/NewTerminal12Validation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'NewTerminal12.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'NewTerminal12.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '新端子验证编译失败。' }
# 使用主程序相同的程序集绑定策略，真实OpenCV像素访问依赖Unsafe程序集。
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '新端子12类离线验证失败。' }
} finally { Pop-Location }
