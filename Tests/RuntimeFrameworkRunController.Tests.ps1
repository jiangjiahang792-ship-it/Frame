# 仅编译纯控制器与任务屏障测试，不加载生产主程序、设备或窗体。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'bin/RuntimeFrameworkRunControllerTests'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'RuntimeFrameworkRunController.Tests.exe'
$sources = @(
    (Join-Path $root 'Forms/RuntimeDesigner/RuntimeRunControl.cs'),
    (Join-Path $root 'Forms/RuntimeDesigner/RuntimeTuningController.cs'),
    (Join-Path $root 'Forms/RuntimeDesigner/RuntimeFrameworkRunController.cs'),
    (Join-Path $root 'ResourceManagement/SolutionRunAdmissionController.cs'),
    (Join-Path $root 'FormMain.RuntimeInterface.cs'),
    (Join-Path $root 'FormMain.RuntimeTuning.cs'),
    (Join-Path $PSScriptRoot 'RuntimeFrameworkHost.Tests.cs'),
    (Join-Path $PSScriptRoot 'RuntimeFrameworkRunController.Tests.cs')
)
& $compiler /nologo /target:exe /langversion:7.3 /reference:System.Core.dll /reference:System.Windows.Forms.dll "/out:$output" @sources
if ($LASTEXITCODE -ne 0) { throw 'Backend controller test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Backend controller tests failed.' }
