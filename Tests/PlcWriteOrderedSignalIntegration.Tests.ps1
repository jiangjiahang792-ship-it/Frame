# 保留历史脚本名作为兼容入口；外发节点现已改为直接执行。
$ErrorActionPreference = 'Stop'
$build = if ($env:TDJS_TEST_BUILD_DIRECTORY) { $env:TDJS_TEST_BUILD_DIRECTORY } else { 'bin/ExternalSignalDirectValidation' }
& (Join-Path $PSScriptRoot 'ExternalSignalDirect.Tests.ps1') -BuildDirectory $build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
