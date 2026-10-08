param([string]$BuildDirectory = 'bin/x64/DesignerRunControlVerify', [string]$ApplicationPath = '')
# 仅编译独立测试入口，引用现有程序集，不构建或启动生产主程序。
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$build = Join-Path $projectRoot $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
if ($ApplicationPath) {
    $application = (Resolve-Path -LiteralPath $ApplicationPath).Path
} else {
    $candidate = Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1
    if ($null -eq $candidate) { throw '未找到已构建程序集，请指定 -ApplicationPath 或主代理的构建目录。' }
    $application = $candidate.FullName
}
$assemblyDirectory = Split-Path -Parent $application
$testExe = Join-Path $assemblyDirectory 'RuntimeRunControlEditor.Tests.exe'
$references = @($application, (Join-Path $assemblyDirectory 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') |
    ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExe" @references (Join-Path $PSScriptRoot 'RuntimeRunControlEditor.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '运行控制编辑器专项测试编译失败。' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw '运行控制编辑器专项测试失败。' }
