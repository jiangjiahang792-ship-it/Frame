param([string]$BuildDirectory = 'bin/x64/RuntimeRoiPagesVerify', [string]$ApplicationPath = '')
# 只编译隐藏窗口专项测试，不构建或启动生产主程序。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($ApplicationPath) { $application = (Resolve-Path -LiteralPath $ApplicationPath).Path }
else {
    $candidate = Get-ChildItem -LiteralPath (Join-Path $root $BuildDirectory) -Filter '*V1.0.exe' | Select-Object -First 1
    if ($null -eq $candidate) { throw '未找到主方程序集，请指定构建目录。' }
    $application = $candidate.FullName
}
$directory = Split-Path -Parent $application
$output = Join-Path $directory 'RuntimeInlinePages.Tests.exe'
$references = @($application, (Join-Path $directory 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /debug:full /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references (Join-Path $PSScriptRoot 'RuntimeInlinePages.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '内联页签专项测试编译失败。' }
& $output
if ($LASTEXITCODE -ne 0) { throw '内联页签专项测试失败。' }
