# 检查首次保存的模态重入边界，不显示文件对话框、不初始化生产方案。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $root 'FormMain.cs') -Encoding UTF8 -Raw
if ($source.Contains('new Forms.RuntimeDesigner.RuntimeInterfaceManagerForm(') -or
    -not $source.Contains('RuntimeInterfaceEditorForm.CreateForPages(')) {
    throw '运行界面设计入口必须直接打开内联多页设计器，不能经过管理窗口。'
}
$start = $source.IndexOf('private bool SaveRuntimeInterfaces(')
$end = $source.IndexOf('private void 关于TDJS_VisionToolStripMenuItem1_Click', $start)
if ($start -lt 0 -or $end -lt 0) { throw '保存方法边界未找到。' }
$method = $source.Substring($start, $end - $start)
$dialog = $method.IndexOf('dialog.ShowDialog(')
if ($method.IndexOf('var solution = Solution.Instance;') -lt 0 -or $method.IndexOf('var solution = Solution.Instance;') -gt $dialog) {
    throw '必须在首次保存对话框前捕获方案身份。'
}
if ($method.LastIndexOf('Role != UserRole.Hight') -lt $dialog -or $method.LastIndexOf('RuntimeRunController.IsRunning') -lt $dialog) {
    throw '对话框返回后必须重新检查权限与运行状态。'
}
foreach ($guard in @('ReferenceEquals(solution, Solution.Instance)', 'ReferenceEquals(previous, solution.RuntimeInterfaces)',
    'ReferenceEquals(configuration, ConfigHelper.SolConfig)', 'roots.SequenceEqual(solution.AllProcesses)')) {
    if ($method.IndexOf($guard) -lt $dialog) { throw "保存弹窗后缺少身份检查：$guard" }
}
foreach ($operation in @('solution.RuntimeInterfaces = snapshot;', 'solution.Save(path);', 'solution.RuntimeInterfaces = previous;',
    'configuration.RuntimeInterface = previousConfig;', 'configuration.RuntimeInterfaces = previousConfigPages;')) {
    if (-not $method.Contains($operation)) { throw "保存与回滚必须使用捕获的对象：$operation" }
}
Write-Output 'PASS: runtime interface save dialog scope and rollback checks'
