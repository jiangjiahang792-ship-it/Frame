param([string]$SchemePath = 'C:/Users/34652/Desktop/9_21号方案_结果发送协议修正版.Sol')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskRoot 'bin/x64/Debug'
$taskRefs = @('机器视觉AI检测系统V1.0.exe','Newtonsoft.Json.dll','WeifenLuo.WinFormsUI.Docking.dll','OpenCvSharp.dll','System.Memory.dll') | ForEach-Object { '/reference:' + (Join-Path $taskBuild $_) }
$taskExe = Join-Path $taskBuild 'DisabledConditionSend.Tests.exe'
$taskHash = (Get-FileHash -LiteralPath $SchemePath).Hash
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 /main:DisabledConditionSendTests /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @taskRefs "/out:$taskExe" (Join-Path $PSScriptRoot 'DisabledConditionSend.Tests.cs') (Join-Path $PSScriptRoot 'Station2ModuleSplit.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '禁用条件发送测试编译失败。' }
Copy-Item -LiteralPath "$taskBuild/机器视觉AI检测系统V1.0.exe.config" -Destination ($taskExe + '.config') -Force
& $taskExe $SchemePath
if ($LASTEXITCODE -ne 0) { throw '禁用条件发送测试失败。' }
if ((Get-FileHash -LiteralPath $SchemePath).Hash -ne $taskHash) { throw '方案被意外修改。' }
Write-Output "方案SHA256保持不变：$taskHash"
