param([string]$Output='outputs/PlugPerformance-before.json')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskBuild=Join-Path $taskRoot 'bin/PlugContourValidation'
$taskExe=Join-Path $taskBuild 'PlugPerformance.Tests.exe'
$taskRefs=@('机器视觉AI检测系统V1.0.exe','Newtonsoft.Json.dll','OpenCvSharp.dll','SunnyUI.dll') | ForEach-Object {'/reference:'+(Join-Path $taskBuild $_)}
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$taskExe" @taskRefs /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'PlugPerformance.Tests.cs')
if($LASTEXITCODE -ne 0){throw '性能验证编译失败'}
Copy-Item -LiteralPath (Join-Path $taskBuild '机器视觉AI检测系统V1.0.exe.config') -Destination ($taskExe+'.config') -Force
& $taskExe (Join-Path $taskRoot 'outputs/PlugLocalContour-20261018/results.json') 'C:/Users/34652/Desktop/防水栓轮廓测试图像' (Join-Path $taskRoot $Output)
if($LASTEXITCODE -ne 0){throw '性能验证失败'}
