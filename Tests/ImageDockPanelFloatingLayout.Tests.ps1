param([string]$BuildDirectory = 'bin/x64/CodexVerify', [switch]$Interactive, [switch]$DiagnoseDragSize, [switch]$MergeRegression)
# 编译并启动真实程序集集成测试；配置仅位于独立测试输出目录，不访问生产方案。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root $BuildDirectory
$output = Join-Path $root 'bin/x64/ImageFloatVerify'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$app = Get-ChildItem -LiteralPath $build -Filter '*V1.0.exe' | Select-Object -First 1
$test = Join-Path $output 'ImageDockPanelFloatingLayout.Tests.exe'
$references = @('System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', $app.FullName,
    (Join-Path $build 'WeifenLuo.WinFormsUI.Docking.dll')) | ForEach-Object { '/reference:' + $_ }
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /target:exe /platform:x64 /langversion:7.3 "/win32manifest:$root/app.manifest" "/out:$test" @references (Join-Path $PSScriptRoot 'ImageDockPanelFloatingLayout.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'App.config') -Destination ($test + '.config') -Force
$env:TDJS_FLOAT_TEST_BUILD = $build
if ($MergeRegression) {
    & $test --merge-regression
    if ($LASTEXITCODE -ne 0) { throw 'Floating merge regression failed.' }
    return
}
if ($DiagnoseDragSize) {
    & $test --drag-size
    if ($LASTEXITCODE -ne 0) { throw 'Primary drag diagnostics failed.' }
    & $test --drag-size --secondary
    if ($LASTEXITCODE -ne 0) { throw 'Secondary drag diagnostics failed.' }
    return
}
$layout = Join-Path $output 'DockPanelImageWindows.config'
if (Test-Path -LiteralPath $layout) {
    Move-Item -LiteralPath $layout -Destination ($layout + '.previous-' + [DateTime]::Now.ToString('yyyyMMddHHmmssfff'))
}
& $test
if ($LASTEXITCODE -ne 0) { throw 'Floating and save failed.' }
& $test --restore
if ($LASTEXITCODE -ne 0) { throw 'Restart restore failed.' }

# 人为产生旧清理代码留下的编号缺口，验证修复后仍能恢复浮窗。
$xml = New-Object System.Xml.XmlDocument
$xml.Load($layout)
$map = @{}
foreach ($pane in $xml.SelectNodes('/DockPanel/Panes/Pane')) {
    $old = $pane.GetAttribute('ID')
    $map[$old] = ([int]$old * 2 + 1).ToString()
    $pane.SetAttribute('ID', $map[$old])
}
foreach ($attribute in $xml.SelectNodes('/DockPanel/@ActivePane | /DockPanel/@ActiveDocumentPane | //NestedPanes/Pane/@RefID | //NestedPanes/Pane/@PrevPane')) {
    if ($attribute.Value -ne '-1') { $attribute.Value = $map[$attribute.Value] }
}
$xml.Save($layout)
& $test --restore
if ($LASTEXITCODE -ne 0) { throw 'Legacy pane index repair failed.' }
$xml.Load($layout)
$validLayout = $xml.OuterXml
foreach ($pane in @($xml.SelectNodes('/DockPanel/Panes/Pane[@DockState="Float"]'))) {
    $pane.ParentNode.RemoveChild($pane) | Out-Null
}
$xml.DockPanel.Panes.SetAttribute('Count', $xml.DockPanel.Panes.ChildNodes.Count.ToString())
$floats = $xml.SelectSingleNode('/DockPanel/FloatWindows')
$floats.RemoveAll()
$floats.SetAttribute('Count', '0')
$xml.Save($layout)
& $test --lost-float
if ($LASTEXITCODE -ne 0) { throw 'Deleted floating pane recovery failed.' }
$xml.LoadXml($validLayout)
$xml.Save($layout)
& $test --unopened
if ($LASTEXITCODE -ne 0) { throw 'Unopened panel layout preservation failed.' }
if ($Interactive) { & $test --restore --interactive }
if ($LASTEXITCODE -ne 0) { throw 'Interactive test failed.' }
Write-Host 'Real image DockPanel integration checks passed.'
