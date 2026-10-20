param([int]$Count=3,[switch]$Affine)
$ErrorActionPreference='Stop'
# 使用现有程序集执行独立离线诊断，不触碰正在运行的软件实例。
$auditBuild = 'D:\MyCode\PublicWook\TDJS-Vision\bin\x64\Release'
$auditRefs = @('机器视觉AI检测系统V1.0.exe','OpenCvSharp.dll','Newtonsoft.Json.dll','SunnyUI.dll','System.Memory.dll') | ForEach-Object { '/reference:' + (Join-Path $auditBuild $_) }
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$auditBuild\Station2AngleAudit.exe" @auditRefs /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll 'outputs\Station2AngleAudit.cs'
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
$auditArgs = @('"D:\项目\AST-Fakra\软件数据\离线测试\ProjConfigs\Sol\9_21号方案_组合模块版_默认发OK.Sol"','"D:\项目\AST-Fakra\采图\111"','"D:\MyCode\PublicWook\TDJS-Vision\outputs\station2-angle-audit\samples.jsonl"',[string]$Count)
if($Affine){$auditArgs[2]='"D:\MyCode\PublicWook\TDJS-Vision\outputs\station2-angle-audit\affine.jsonl"';$auditArgs+='affine'}
$auditProcess=Start-Process -FilePath "$auditBuild\Station2AngleAudit.exe" -ArgumentList $auditArgs -WorkingDirectory $auditBuild -WindowStyle Hidden -PassThru -RedirectStandardOutput 'outputs\station2-angle-audit\run.txt' -RedirectStandardError 'outputs\station2-angle-audit\error.txt'
$auditProcess.Id
