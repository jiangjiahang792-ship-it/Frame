# 使用实际方案模型与ROI运行样图对照，结果写到独立目录，不改现场方案。
param(
    [Parameter(Mandatory = $true)][string]$SolutionPath,
    [Parameter(Mandatory = $true)][string]$SampleDirectory,
    [string]$BuildDirectory = 'bin/PlugContourValidation',
    [string]$OutputDirectory = 'outputs/PlugLocalContour-20261018'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskRoot $BuildDirectory
$taskOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $taskRoot $OutputDirectory }
$taskReferences = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') |
    ForEach-Object { '/reference:' + (Join-Path $taskBuild $_) }
$taskExecutable = Join-Path $taskBuild 'PlugLocalContour.Samples.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$taskExecutable" @taskReferences /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'PlugLocalContour.Samples.cs')
if ($LASTEXITCODE -ne 0) { throw '实际模型样图测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $taskBuild '机器视觉AI检测系统V1.0.exe.config') -Destination ($taskExecutable + '.config') -Force
& $taskExecutable $SolutionPath $SampleDirectory $taskOutput
if ($LASTEXITCODE -ne 0) { throw '实际模型样图测试失败，请检查模型及本机有效授权。' }
