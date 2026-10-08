param([string]$BuildDirectory = 'bin/x64/WorkspacePermissionVerify')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = Join-Path $build '机器视觉AI检测系统V1.0.exe'
$output = Join-Path $build 'WorkspacePermission.Tests.exe'
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @($application, (Join-Path $build 'WeifenLuo.WinFormsUI.Docking.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') |
    ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/win32manifest:$root/app.manifest" "/out:$output" @references (Join-Path $PSScriptRoot 'WorkspacePermission.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '工作区权限测试编译失败。' }
Copy-Item -LiteralPath ($application + '.config') -Destination ($output + '.config') -Force
Push-Location $build
try {
    & $output
    if ($LASTEXITCODE -ne 0) { throw '工作区权限测试失败。' }
}
finally { Pop-Location }
