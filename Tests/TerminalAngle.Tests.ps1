param(
    [string]$BuildDirectory = 'bin/TerminalAngleValidation',
    [string]$DemoDirectory = 'C:/Users/34652/Documents/Codex/2026-09-18/c-users-34652-desktop-20260807/outputs/OpenCV端子测量',
    [switch]$SubscriptionTextOnly,
    [switch]$ConfirmCloseOnly,
    [switch]$GeometryOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$artifacts = Join-Path $root 'artifacts/TerminalAngleValidation'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe','OpenCvSharp.dll','Newtonsoft.Json.dll','SunnyUI.dll','System.Memory.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$test = Join-Path $build 'TerminalAngle.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$test" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'TerminalAngle.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '端子角度测试编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($test + '.config') -Force
# 独立进程不启动主程序、不连接设备，截图窗体位于屏幕外。
$argsText = '"{0}" "{1}"' -f $DemoDirectory,$artifacts
if ($ConfirmCloseOnly) { $argsText += ' confirm-close' }
elseif ($SubscriptionTextOnly) { $argsText += ' subscription-text' }
elseif ($GeometryOnly) { $argsText += ' geometry' }
$p = Start-Process -FilePath $test -ArgumentList $argsText -WorkingDirectory $build -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $artifacts '测试日志.txt') -RedirectStandardError (Join-Path $artifacts '异常日志.txt')
Get-Content -Encoding utf8 (Join-Path $artifacts '测试日志.txt')
if ($p.ExitCode -ne 0) { Get-Content -Encoding utf8 (Join-Path $artifacts '异常日志.txt'); throw "端子角度测试失败：$($p.ExitCode)" }
