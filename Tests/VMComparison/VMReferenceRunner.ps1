# 编译官方 SDK 基准，不修改系统设置或 VM 安装文件。
param(
    [string]$VmDirectory = 'D:/VM/VisionMaster4.4.0/Applications',
    [string]$SolutionPath = 'artifacts/VMComparison/VM基准原流程.sol',
    [string]$ImagePath = 'C:/Users/34652/Desktop/工位3/20260904/OK/2026-09-04_14-12-23-104-CAN.jpg',
    [string]$Polarity = 'saved',
    [string]$ResultName = 'vm-reference.json'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildPath = Join-Path $projectRoot 'artifacts/VMComparison/runner'
New-Item -ItemType Directory -Path $buildPath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $buildPath 'myLibs') -Force | Out-Null
$compilerPath = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('myLibs/VM.Core.dll', 'PublicFile/x64/VM.PlatformSDKCS.dll', 'myLibs/VMControls.BaseInterface.dll', 'myLibs/VMControls.Interface.dll', 'myLibs/VMControls.RenderInterface.dll', 'Module(sp)/x64/Collection/ImageSourceModule/ImageSourceModuleCs.dll', 'Module(sp)/x64/Location/IMVSContourMatchModu/IMVSContourMatchModuCs.dll') | ForEach-Object { '/reference:' + (Join-Path $VmDirectory $_) }
$executable = Join-Path $buildPath ('VMReferenceBenchmark-' + (Get-Date -Format 'HHmmss') + '.exe')
& $compilerPath /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$executable" @references /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'VMReferenceRunner.cs')
if ($LASTEXITCODE -ne 0) { throw 'VM 基准编译失败。' }
$configuration = '<?xml version="1.0" encoding="utf-8"?><configuration><appSettings><add key="StartClientMode" value="1"/><add key="StartServerByExe" value="0"/></appSettings><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup></configuration>'
Set-Content -LiteralPath ($executable + '.config') -Value $configuration -Encoding UTF8
& $executable $VmDirectory (Resolve-Path -LiteralPath $SolutionPath).Path $ImagePath $Polarity $ResultName
if ($LASTEXITCODE -ne 0) { throw 'VM 基准执行失败。' }
