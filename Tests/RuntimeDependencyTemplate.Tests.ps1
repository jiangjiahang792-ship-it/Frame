$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$solutionTemplatePath = Join-Path $projectRoot '空方案.Sol'
$runtimeDllPath = Join-Path $projectRoot 'RuntimeDll'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    Assert-True ($Text.Contains($Expected)) $Message
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Unexpected,
        [string]$Message
    )

    Assert-True (-not $Text.Contains($Unexpected)) $Message
}

$expectedRuntimeDllNames = @(
    'dog_windows_3155929.dll',
    'dog_windows_x64_3155929.dll',
    'dogdnert.dll',
    'dogdnert_x64.dll',
    'Humanizer.dll',
    'libcrypto-3-x64.dll',
    'libssl-3-x64.dll',
    'Microsoft.CodeAnalysis.CSharp.Workspaces.dll',
    'Microsoft.CodeAnalysis.Workspaces.dll',
    'onnxruntime.dll',
    'opencv_world4100.dll',
    'OpenCvSharpExtern.dll',
    'openvino.dll',
    'openvino_auto_batch_plugin.dll',
    'openvino_auto_plugin.dll',
    'openvino_c.dll',
    'openvino_hetero_plugin.dll',
    'openvino_intel_cpu_plugin.dll',
    'openvino_intel_gpu_plugin.dll',
    'openvino_ir_frontend.dll',
    'openvino_onnx_frontend.dll',
    'System.Composition.AttributedModel.dll',
    'System.Composition.Convention.dll',
    'System.Composition.Hosting.dll',
    'System.Composition.Runtime.dll',
    'System.Composition.TypedParts.dll',
    'System.IO.Pipelines.dll',
    'System.Reflection.MetadataLoadContext.dll',
    'System.Threading.Channels.dll',
    'tbb12.dll',
    'td_det.dll',
    'td_obb.dll',
    'td_pose.dll',
    'vcruntime140.dll',
    'vcruntime140_1.dll'
)

Assert-True (Test-Path -LiteralPath $solutionTemplatePath -PathType Leaf) '项目根目录必须存在唯一的空方案.Sol 模板。'
$solutionTemplate = Get-Content -LiteralPath $solutionTemplatePath -Encoding UTF8 -Raw | ConvertFrom-Json
Assert-True ($solutionTemplate.SolVer -eq '1.0.0.0') '空方案模板版本必须为 1.0.0.0。'
Assert-True (@($solutionTemplate.Devices.PSObject.Properties).Count -eq 0) '空方案模板不能包含设备。'
Assert-True (@($solutionTemplate.ProcessInfos).Count -eq 0) '空方案模板不能包含流程。'

$projectSource = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw
Assert-Contains $projectSource '<Content Include="空方案.Sol">' '项目必须包含空方案模板。'
Assert-Contains $projectSource '<CopyToOutputDirectory>Always</CopyToOutputDirectory>' '空方案模板必须在每次生成时复制。'
Assert-Contains $projectSource '<RuntimeDependency Include="$(ProjectDir)RuntimeDll\*.dll" />' '项目必须自动枚举 RuntimeDll 顶层运行库。'
Assert-Contains $projectSource '<Target Name="CopyRuntimeDependencies" AfterTargets="Build">' '项目必须在生成完成后复制运行库模板。'
Assert-Contains $projectSource 'DestinationFolder="$(TargetDir)"' '运行库必须复制到当前程序输出目录根部。'
Assert-Contains $projectSource 'SkipUnchangedFiles="true"' '运行库复制必须跳过未变化文件以保证生成效率。'
Assert-NotContains $projectSource 'LargeModelDll' '项目文件不得通过任何项目项或生成事件绑定 LargeModelDll。'
Assert-NotContains $projectSource 'UnsupervisedDll' '项目文件不得通过任何项目项或生成事件绑定 UnsupervisedDll。'
Assert-NotContains $projectSource 'YoloGPUDll' '项目文件不得通过任何项目项或生成事件绑定 YoloGPUDll。'

Assert-True (Test-Path -LiteralPath $runtimeDllPath -PathType Container) '项目根目录必须存在 RuntimeDll 运行库模板目录。'
$actualRuntimeDllNames = @(Get-ChildItem -LiteralPath $runtimeDllPath -File -Filter '*.dll' | Select-Object -ExpandProperty Name | Sort-Object)
Assert-True ($actualRuntimeDllNames.Count -eq $expectedRuntimeDllNames.Count) "RuntimeDll 必须恰好包含 $($expectedRuntimeDllNames.Count) 个 Release 基准 DLL。"
foreach ($dllName in $expectedRuntimeDllNames) {
    Assert-True ($actualRuntimeDllNames -contains $dllName) "RuntimeDll 缺少 Release 基准运行库：$dllName"
}

Write-Host "运行库模板检查通过：$($expectedRuntimeDllNames.Count) 个 DLL，空方案和项目复制规则均有效。"
