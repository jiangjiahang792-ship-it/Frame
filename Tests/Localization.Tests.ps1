# 编译显示层独立验证程序；测试目录独立于软件运行目录，不启动设备。
param([string]$BuildDirectory = 'bin/localization-verification')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root 'bin/localization-tests'
New-Item -ItemType Directory -Path (Join-Path $outputDirectory 'Languages') -Force | Out-Null
Copy-Item -Path (Join-Path $root 'Languages/*.json') -Destination (Join-Path $outputDirectory 'Languages') -Force
Copy-Item -LiteralPath (Join-Path $root "$BuildDirectory/Newtonsoft.Json.dll") -Destination $outputDirectory -Force
$output = Join-Path $outputDirectory 'Localization.Tests.exe'
$sources = @('LanguageManager.cs', 'DisplayTranslator.cs', 'UiLocalization.cs', 'Tests/Localization.Tests.cs') | ForEach-Object { Join-Path $root $_ }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/reference:$(Join-Path $outputDirectory 'Newtonsoft.Json.dll')" "/out:$output" @sources
if ($LASTEXITCODE -ne 0) { throw '本地化验证程序编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '本地化验证失败。' }
