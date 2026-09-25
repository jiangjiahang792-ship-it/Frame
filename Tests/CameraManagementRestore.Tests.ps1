param([string]$BuildDirectory = 'bin/x64/Release', [string]$Mode = 'all')
$ErrorActionPreference = 'Stop'
$build = Join-Path (Split-Path -Parent $PSScriptRoot) $BuildDirectory
$test = Join-Path $build 'CameraManagementRestore.Tests.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$test" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'CameraManagementRestore.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '相机管理恢复回归编译失败。' }
Copy-Item -LiteralPath (Join-Path $build '机器视觉AI检测系统V1.0.exe.config') -Destination ($test + '.config') -Force
& $test $Mode
if ($LASTEXITCODE -ne 0) { throw '相机管理恢复回归失败。' }
