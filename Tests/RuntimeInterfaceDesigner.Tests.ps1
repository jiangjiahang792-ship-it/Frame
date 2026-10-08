param([string]$BuildDirectory = 'bin/x64/DesignerVerify', [switch]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$build = Join-Path $projectRoot $BuildDirectory
$application = Join-Path $build '机器视觉AI检测系统V1.0.exe'
if (-not (Test-Path -LiteralPath $application)) { throw '运行界面设计器尚未编译，请先构建对应目录。' }
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExe = Join-Path $build 'RuntimeInterfaceDesigner.Tests.exe'
$references = @($application, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Core.dll') | ForEach-Object { '/reference:' + $_ }
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$testExe" @references (Join-Path $PSScriptRoot 'RuntimeInterfaceDesigner.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '运行界面设计器测试编译失败。' }
if ($Preview) {
    $previewExe = Join-Path $build '运行界面设计器体验.exe'
    & $compiler /nologo /target:winexe /platform:x64 /langversion:7.3 "/out:$previewExe" @references (Join-Path $projectRoot 'tools/RuntimeInterfaceDesignerPreview/Program.cs')
    if ($LASTEXITCODE -ne 0) { throw '设计器体验入口编译失败。' }
    Start-Process -FilePath $previewExe -WorkingDirectory $build
} else {
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw '运行界面设计器行为测试失败。' }
}
