# 真实程序集离线加载检查；不反序列化设备配置，不运行 PLC/相机节点。
param([Parameter(Mandatory=$true)][string]$SchemePath, [string]$BuildDirectory='bin/x64/Release', [switch]$Baseline)
$ErrorActionPreference='Stop'
$buildPath=Join-Path (Split-Path -Parent $PSScriptRoot) $BuildDirectory
$testPath=Join-Path $buildPath 'StationCompositeMigration.Tests.exe'
$references=@('机器视觉AI检测系统V1.0.exe','OpenCvSharp.dll','SunnyUI.dll','Newtonsoft.Json.dll','WeifenLuo.WinFormsUI.Docking.dll') | ForEach-Object { '/reference:'+(Join-Path $buildPath $_) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testPath" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'StationCompositeMigration.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '迁移验证程序编译失败。' }
Copy-Item -LiteralPath (Join-Path $buildPath '机器视觉AI检测系统V1.0.exe.config') -Destination ($testPath+'.config') -Force
if ($Baseline) { & $testPath $SchemePath --baseline } else { & $testPath $SchemePath }
if ($LASTEXITCODE -ne 0) { throw '方案迁移验证失败。' }
