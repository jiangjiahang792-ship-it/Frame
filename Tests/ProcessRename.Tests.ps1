# 通过真实重命名窗体验证句点名称及选项卡更新，不弹出交互窗口。
param([string]$BuildDirectory = 'bin/x64/Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildPath = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testPath = Join-Path $buildPath 'ProcessRename.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'SunnyUI.dll', 'Newtonsoft.Json.dll') | ForEach-Object { '/reference:' + (Join-Path $buildPath $_) }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'ProcessRename.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '流程重命名测试编译失败。' }
& $testPath
if ($LASTEXITCODE -ne 0) { throw '流程重命名测试失败。' }
