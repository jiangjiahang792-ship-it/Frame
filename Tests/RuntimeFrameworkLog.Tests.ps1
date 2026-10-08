param([string]$BuildDirectory = 'bin/x64/RuntimeDisplayFixVerify', [switch]$Source)
# 只编译并运行无设备测试入口，不启动正式程序。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$application = Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1
if ($null -eq $application) { throw 'A reference assembly is required.' }
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$output = Join-Path $build 'RuntimeFrameworkLog.Tests.exe'
$references = @($application.FullName, (Join-Path $build 'Newtonsoft.Json.dll'), 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { '/reference:' + $_ }
$sources = @((Join-Path $PSScriptRoot 'RuntimeFrameworkLog.Tests.cs'))
if ($Source) {
    # 编译实际后端源文件，旧正式产物只提供流程类型和已有公共依赖。
    $sources += @('Forms/Logger/RuntimeLogContext.cs', 'Forms/Logger/RuntimeLogStream.cs',
        'Forms/Logger/LogHelper.cs', 'Forms/Logger/LogHelper.Designer.cs',
        'Forms/RuntimeDesigner/RuntimeLog.cs', 'Forms/RuntimeDesigner/RuntimeFrameworkLogSource.cs') |
        ForEach-Object { Join-Path $root $_ }
}
& $compiler /nologo /target:exe /platform:x64 /langversion:7.3 /nowarn:0436 "/out:$output" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Runtime log test compilation failed.' }
& $output $root
if ($LASTEXITCODE -ne 0) { throw 'Runtime log tests failed.' }
