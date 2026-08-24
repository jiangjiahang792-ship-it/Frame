param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$cameraPath = Join-Path $ProjectRoot "Device\Camera\CameraHik.cs"
$paramFormPath = Join-Path $ProjectRoot "Node\1-Acquisition\ImageSource\ParamFormImageSource.cs"
$paramFormDesignerPath = Join-Path $ProjectRoot "Node\1-Acquisition\ImageSource\ParamFormImageSource.Designer.cs"
$nodePath = Join-Path $ProjectRoot "Node\1-Acquisition\ImageSource\NodeImageSource.cs"
$solutionPath = Join-Path $ProjectRoot "Solution.cs"

function Assert-Regex {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

$camera = Get-Content -LiteralPath $cameraPath -Raw -Encoding UTF8
$paramForm = Get-Content -LiteralPath $paramFormPath -Raw -Encoding UTF8
$paramFormDesigner = Get-Content -LiteralPath $paramFormDesignerPath -Raw -Encoding UTF8
$node = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$solution = Get-Content -LiteralPath $solutionPath -Raw -Encoding UTF8

Assert-Regex $camera 'if\s*\(value == 4\)\s*return "Counter0";' "TriggerSource数值4必须按海康官方SDK定义识别为Counter0。"
Assert-Regex $camera 'entry\.Value <= \(uint\)MyCamera\.MV_CAM_TRIGGER_SOURCE\.MV_TRIGGER_SOURCE_LINE3' "触发源数值兜底必须止于Line3，不能包含Counter0。"
Assert-Regex $camera 'if\s*\(!IsLineTriggerSource\(GetTriggerSource\(\)\)\)\s*throw new InvalidOperationException\("只有线路硬触发模式可以设置触发极性。"\);' "相机底层必须拒绝在线路触发以外写入TriggerActivation。"
Assert-Regex $camera 'if\s*\(!IsLineTriggerSource\(GetTriggerSource\(\)\)\)\s*return CreateUnavailableEnumValue\(\);' "软件触发或连续采集时不得读取TriggerActivation。"
Assert-Regex $paramForm 'TriggerSource effectiveTriggerSource = triggerSource == TriggerSource\.Auto[\s\S]*?if\s*\(IsHardwareTriggerSource\(effectiveTriggerSource\)\)\s*camera\.SetTriggerEdge\(triggerEdge\);' "图像源参数保存必须先归一Auto，并仅在线路触发时设置极性。"
Assert-Regex $paramForm 'RefreshTriggerSourceItems\(camera\);' "图像源参数界面必须按当前相机SDK能力刷新触发源。"
if ($paramFormDesigner -match '"Line4"') {
    throw "图像源参数界面不能默认提供海康SDK未定义的Line4。"
}
Assert-Regex $node 'if\s*\(effectiveTriggerSource >= TriggerSource\.LINE0 && effectiveTriggerSource <= TriggerSource\.LINE4\)\s*camera\.SetTriggerEdge\(param\.TriggerEdge\);' "图像源运行时必须仅在线路触发时设置极性。"
Assert-Regex $solution 'if\s*\(effectiveTriggerSource >= TriggerSource\.LINE0 && effectiveTriggerSource <= TriggerSource\.LINE4\)\s*camera\.SetTriggerEdge\(param\.TriggerEdge\);' "方案启动应用相机参数时必须仅在线路触发写入极性。"

Write-Output "海康触发语义安全检查通过。"
