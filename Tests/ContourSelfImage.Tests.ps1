# 从用户指定方案读取原图验证，不修改原方案，不连接现场设备。
param(
    [Parameter(Mandatory = $true)][string]$SolutionPath,
    [string]$BuildDirectory = 'bin/ContourSelfImageValidation',
    [switch]$ZeroAngle
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testBuildPath = Join-Path $projectRoot $BuildDirectory
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'System.Memory.dll') | ForEach-Object { '/reference:' + (Join-Path $testBuildPath $_) }
$testPath = Join-Path $testBuildPath 'ContourSelfImage.Tests.exe'
& $compilerPath /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ContourSelfImage.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '原图回归编译失败。' }
Copy-Item -LiteralPath (Join-Path $testBuildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath + '.config') -Force
& $testPath $SolutionPath $ZeroAngle.IsPresent
if ($LASTEXITCODE -ne 0) { throw '原图回归失败。' }
