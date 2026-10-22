# 使用隔离构建执行组合布尔文本回归，不运行设备通信。
param([string]$BuildDirectory = 'bin/CombinedBooleanValidation')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $root $BuildDirectory
$executable = Join-Path $directory 'CombinedBooleanOverlay.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'SunnyUI.dll', 'System.Memory.dll') |
    ForEach-Object { '/reference:' + (Join-Path $directory $_) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 "/out:$executable" @references /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'CombinedBooleanOverlay.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '组合布尔测试编译失败' }
Copy-Item -LiteralPath (Join-Path $directory '机器视觉AI检测系统V1.0.exe.config') -Destination ($executable + '.config')
Push-Location $directory
try {
    & $executable
    if ($LASTEXITCODE -ne 0) { throw '组合布尔测试失败' }
} finally { Pop-Location }
