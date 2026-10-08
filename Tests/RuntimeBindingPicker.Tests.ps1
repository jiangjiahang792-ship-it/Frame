param([string]$BuildDirectory = 'bin/x64/DesignerBevelVerify', [string]$ApplicationPath = '')
# 只编译测试入口，不构建或启动生产主程序；使用主代理已构建的程序集。
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
$json = Join-Path $assemblyDirectory 'Newtonsoft.Json.dll'
$testExe = Join-Path $assemblyDirectory 'RuntimeBindingPicker.Regression.Tests.exe'
$references = @($application, $json, 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll')
$referenceArguments = $references | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExe" @referenceArguments (Join-Path $PSScriptRoot 'RuntimeBindingPicker.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '绑定选择器专项测试编译失败。' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw '绑定选择器专项测试失败。' }
