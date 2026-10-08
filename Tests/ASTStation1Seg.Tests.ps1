# 编译实际程序集离线回归，模型推理另用--infer显式执行。
param([string]$BuildDirectory = 'bin/ASTSegValidation', [switch]$PrepareConfiguration)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$references = @('机器视觉AI检测系统V1.0.exe', 'Newtonsoft.Json.dll', 'OpenCvSharp.dll', 'SunnyUI.dll') | ForEach-Object { '/reference:' + (Join-Path $build $_) }
$output = Join-Path $build 'ASTStation1Seg.Tests.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 "/out:$output" @references /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ASTStation1Seg.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'AST分割测试编译失败。' }
Push-Location $build
try {
    if ($PrepareConfiguration) {
        & $output --prepare (Join-Path $root 'Template/TDAI/AST工位1分割模型配置.json') (Join-Path $root 'Template/TDAI/AST工位1分割模型.ai')
        if ($LASTEXITCODE -ne 0) { throw 'AST分割配置生成失败。' }
        Copy-Item -LiteralPath (Join-Path $root 'Template/TDAI/AST工位1分割模型.ai') -Destination (Join-Path $build 'Template/TDAI/AST工位1分割模型.ai')
    }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'AST分割离线验证失败。' }
} finally { Pop-Location }
