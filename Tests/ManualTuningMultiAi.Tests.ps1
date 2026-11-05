$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$viewer = Get-Content -LiteralPath (Join-Path $root 'Forms\ImageViewer\FrmSingleImage.cs') -Raw -Encoding UTF8
$form = Get-Content -LiteralPath (Join-Path $root 'Forms\SolRunParam\FormSolRunParam.cs') -Raw -Encoding UTF8
$service = Get-Content -LiteralPath (Join-Path $root 'Forms\SolRunParam\ProcessTuningService.cs') -Raw -Encoding UTF8

if ($viewer -notmatch 'ResolveManualTuningTargets') { throw '图像窗口未实现多AI手动调参目标解析。' }
if ($viewer -notmatch 'targetAiNodes\.Count > 1') { throw '图像窗口未按多AI集合调用调参入口。' }
if ($form -notmatch 'foreach \(NodeTDAI pageTarget in pageTargets\)') { throw '手动调参窗体未按AI节点创建独立页签。' }
if ($form -notmatch 'solRunParam\.Init\(process, showSingleProcessOnly \? pageTarget : null, processFilter\)') { throw 'AI页签未绑定对应节点。' }
if ($service -notmatch 'IEnumerable<NodeTDAI> targets') { throw '调参服务未提供多AI入口。' }

Write-Host '手动调参多AI页签静态检查通过。'
