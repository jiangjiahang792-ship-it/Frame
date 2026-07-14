param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseExe,

    [Parameter(Mandatory = $true)]
    [string]$CapturePlan,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ResultJson,

    [string[]]$OnlyModules = @()
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class RealWorkflowCaptureDpi
{
    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();
}
'@

[RealWorkflowCaptureDpi]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    throw '真实流程截图脚本必须在 STA 模式运行。'
}

[Windows.Forms.Application]::EnableVisualStyles()
[Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)

$releasePath = (Resolve-Path -LiteralPath $ReleaseExe).Path
$capturePlanPath = (Resolve-Path -LiteralPath $CapturePlan).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')).Path
$originalScreenshotDirectory = Join-Path $projectRoot 'docs\manual-assets\screenshots'
$outputPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$resultPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $ResultJson))

[IO.Directory]::CreateDirectory($outputPath) | Out-Null
[Environment]::CurrentDirectory = Split-Path -Parent $releasePath
$assembly = [Reflection.Assembly]::LoadFrom($releasePath)
$bindingFlags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$formBaseType = [Windows.Forms.Form]
$nodeBaseType = $assembly.GetType('TDJS_Vision.Node.NodeBase', $true)
$processType = $assembly.GetType('TDJS_Vision.Process', $true)
$nodeTypeType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$unknownNodeType = [Enum]::Parse($nodeTypeType, 'UNKNOWN')
$processConnectionType = $assembly.GetType('TDJS_Vision.ProcessConnection', $true)
$wizardType = $assembly.GetType('TDJS_Vision.Forms.ProcessNew.FormNewProcessWizard', $true)

$nodeTypes = @(
    $assembly.GetTypes() |
        Where-Object {
            -not $_.IsAbstract -and
            $nodeBaseType.IsAssignableFrom($_) -and
            $_ -ne $nodeBaseType
        }
)

function Invoke-UiEvents {
    param([int]$Count = 10)

    for ($tick = 0; $tick -lt $Count; $tick++) {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 120
    }
}

function Get-PrivateFieldValue {
    param(
        [object]$Instance,
        [string]$FieldName
    )

    $field = $Instance.GetType().GetField($FieldName, $bindingFlags)
    if ($null -ne $field) {
        return $field.GetValue($Instance)
    }
    $property = $Instance.GetType().GetProperty($FieldName, $bindingFlags)
    if ($null -ne $property) {
        return $property.GetValue($Instance, $null)
    }
    throw "未找到字段或属性[$FieldName]。"
}

function Get-SafeFileName {
    param(
        [int]$Index,
        [string]$ModuleName
    )

    $safeName = $ModuleName -replace '[\\/:*?"<>|]', '_'
    return ('{0:D3}-{1}.png' -f $Index, $safeName)
}

function Set-FormBounds {
    param([Windows.Forms.Form]$Form)

    $workingArea = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    if ($Form.Width -gt $workingArea.Width) { $Form.Width = $workingArea.Width }
    if ($Form.Height -gt $workingArea.Height) { $Form.Height = $workingArea.Height }
    if ($Form.Width -lt 560) { $Form.Width = [Math]::Min(900, $workingArea.Width) }
    if ($Form.Height -lt 360) { $Form.Height = [Math]::Min(680, $workingArea.Height) }
    $Form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $Form.Left = [Math]::Max(0, [int](($workingArea.Width - $Form.Width) / 2))
    $Form.Top = [Math]::Max(0, [int](($workingArea.Height - $Form.Height) / 2))
}

function Set-ReadableDefaults {
    param([Windows.Forms.Control]$Parent)

    foreach ($control in $Parent.Controls) {
        if ($control -is [Windows.Forms.ComboBox] -and $control.Items.Count -gt 0 -and $control.SelectedIndex -lt 0) {
            try { $control.SelectedIndex = 0 } catch {}
        }
        if ($control.HasChildren) {
            Set-ReadableDefaults $control
        }
    }
}

function Set-WorkflowDisplayText {
    param(
        [Windows.Forms.Control]$Parent,
        [int]$NodeCount
    )

    foreach ($control in $Parent.Controls) {
        if ($control.Text -eq 'ProcessNew.NodeCount') { $control.Text = "节点数量：$NodeCount" }
        elseif ($control.Text -eq 'ProcessNew.Elapsed') { $control.Text = '运行耗时：--' }
        if ($control.HasChildren) {
            Set-WorkflowDisplayText $control $NodeCount
        }
    }
}

function Get-DescendantControls {
    param([Windows.Forms.Control]$Parent)

    foreach ($control in $Parent.Controls) {
        $control
        if ($control.HasChildren) {
            Get-DescendantControls $control
        }
    }
}

function Set-ModuleReadableState {
    param(
        [string]$ModuleName,
        [Windows.Forms.Form]$Form
    )

    if ($ModuleName -eq '运行参数') {
        $Form.Text = '运行参数设置'
        $textMap = @{
            'SolRunParam.Exposure' = '曝光时间'
            'SolRunParam.Gain' = '增益'
            'SolRunParam.ScoreThreshold' = '置信度阈值'
            'SolRunParam.NMSScore' = 'NMS阈值'
            'SolRunParam.ContinuousGrab' = '连续采集'
            'SolRunParam.OneClickLearning' = '一键学习'
            'SolRunParam.GrabOnce' = '单次取图'
            'SolRunParam.SingleImageTest' = '单图测试'
            'SolRunParam.MultiImageTest' = '批量测试'
            'SolRunParam.SaveSettings' = '保存设置'
        }
        foreach ($control in Get-DescendantControls $Form) {
            if ($textMap.ContainsKey($control.Text)) {
                $control.Text = $textMap[$control.Text]
            }
            if ($control -is [Windows.Forms.DataGridView]) {
                foreach ($column in $control.Columns) {
                    if ($column.HeaderText -match 'Enable') { $column.HeaderText = '启用' }
                    elseif ($column.HeaderText -match 'Detect') { $column.HeaderText = '检测项' }
                    elseif ($column.HeaderText -match 'Min') { $column.HeaderText = '最小值' }
                    elseif ($column.HeaderText -match 'Current') { $column.HeaderText = '当前值' }
                    elseif ($column.HeaderText -match 'Max') { $column.HeaderText = '最大值' }
                    elseif ($column.HeaderText -match 'Count') { $column.HeaderText = '统计项' }
                }
            }
        }
        $tabControl = @(Get-DescendantControls $Form | Where-Object { $_ -is [Windows.Forms.TabControl] }) | Select-Object -First 1
        if ($null -ne $tabControl -and $tabControl.TabPages.Count -gt 0) {
            if ($tabControl.TabPages[0].Text -eq 'SolRunParam.ModbusCommunication') {
                $tabControl.TabPages[0].Text = 'Modbus通信'
            }
            $tabControl.SelectedIndex = $tabControl.TabPages.Count - 1
        }
    }
}

function Save-ScreenRectangle {
    param(
        [Drawing.Rectangle]$Rectangle,
        [string]$Path
    )

    if ($Rectangle.Width -lt 100 -or $Rectangle.Height -lt 80) {
        throw "无效截图区域：$($Rectangle.Width)x$($Rectangle.Height)。"
    }
    $bitmap = New-Object Drawing.Bitmap $Rectangle.Width, $Rectangle.Height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($Rectangle.Location, [Drawing.Point]::Empty, $Rectangle.Size)
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Save-FormScreenshot {
    param(
        [Windows.Forms.Form]$Form,
        [string]$Path
    )

    $Form.Activate()
    $Form.BringToFront()
    Invoke-UiEvents 5
    Save-ScreenRectangle $Form.Bounds $Path
}

function Save-ControlScreenshot {
    param(
        [Windows.Forms.Control]$Control,
        [string]$Path
    )

    $screenPoint = $Control.PointToScreen([Drawing.Point]::Empty)
    $rectangle = New-Object Drawing.Rectangle $screenPoint, $Control.Size
    Save-ScreenRectangle $rectangle $Path
}

function Get-NormalizedName {
    param([string]$Name)

    return ($Name -replace '^(NodeParamForm|ParamForm|Form|Node)', '' -replace '[^A-Za-z0-9]', '').ToLowerInvariant()
}

function Get-NodeForForm {
    param(
        [Type]$FormType,
        [object]$Process
    )

    $target = Get-NormalizedName $FormType.Name
    $aliases = @{
        'findcircle' = 'findcircle'
        'findline' = 'findline'
        'binaryanalysis' = 'binaryanalysis'
        'colordiscern' = 'colordiscern'
        'imageshow' = 'imagesource'
        'signalsend' = 'signalsend'
        'flagread' = 'flagread'
        'waitsofttrigger' = 'waitsofttrigger'
        'summarize' = 'summarize'
    }
    if ($aliases.ContainsKey($target)) {
        $target = $aliases[$target]
    }

    $match = @(
        foreach ($nodeType in $nodeTypes) {
            $candidate = Get-NormalizedName $nodeType.Name
            $score = 0
            if ($candidate -eq $target) { $score = 100 }
            elseif ($candidate.Contains($target) -or $target.Contains($candidate)) { $score = 50 }
            if ($nodeType.Namespace -eq $FormType.Namespace) { $score += 20 }
            if ($score -gt 0) {
                [pscustomobject]@{ Type = $nodeType; Score = $score }
            }
        }
    ) | Sort-Object Score -Descending | Select-Object -First 1

    if ($null -eq $match) {
        throw "未找到窗口[$($FormType.FullName)]对应的节点类型。"
    }
    return [Activator]::CreateInstance(
        $match.Type,
        [object[]]@(9000, '客户演示参数节点', $Process, $unknownNodeType)
    )
}

function New-FormInstance {
    param(
        [Type]$FormType,
        [object]$Process
    )

    if ($FormType.FullName -match '^TDJS_Vision\.Node\.') {
        try {
            $node = Get-NodeForForm $FormType $Process
            if ($null -ne $node.ParamForm -and $FormType.IsInstanceOfType($node.ParamForm)) {
                return $node.ParamForm
            }
        }
        catch {
            $nodeCreationError = $_.Exception.GetBaseException().Message
        }
    }

    $constructors = @($FormType.GetConstructors())
    $orderedConstructors = @(
        $constructors | Sort-Object {
            $parameters = @($_.GetParameters())
            if ($parameters.Count -eq 2 -and $parameters[0].ParameterType -eq $processType) { 1 }
            elseif ($parameters.Count -eq 1 -and $nodeBaseType.IsAssignableFrom($parameters[0].ParameterType)) { 2 }
            elseif ($parameters.Count -eq 1 -and $parameters[0].ParameterType -eq $processType) { 3 }
            elseif ($parameters.Count -eq 2 -and $parameters[0].ParameterType -eq [string] -and $parameters[1].ParameterType -eq $processType) { 4 }
            elseif ($parameters.Count -eq 0) { 5 }
            else { 99 }
        }
    )

    foreach ($constructor in $orderedConstructors) {
        $parameters = @($constructor.GetParameters())
        try {
            if ($parameters.Count -eq 2 -and $parameters[0].ParameterType -eq $processType -and $nodeBaseType.IsAssignableFrom($parameters[1].ParameterType)) {
                $node = Get-NodeForForm $FormType $Process
                return $constructor.Invoke([object[]]@($Process, $node))
            }
            if ($parameters.Count -eq 1 -and $nodeBaseType.IsAssignableFrom($parameters[0].ParameterType)) {
                $node = Get-NodeForForm $FormType $Process
                return $constructor.Invoke([object[]]@($node))
            }
            if ($parameters.Count -eq 1 -and $parameters[0].ParameterType -eq $processType) {
                return $constructor.Invoke([object[]]@($Process))
            }
            if ($parameters.Count -eq 2 -and $parameters[0].ParameterType -eq [string] -and $parameters[1].ParameterType -eq $processType) {
                return $constructor.Invoke([object[]]@('客户演示流程', $Process))
            }
            if ($parameters.Count -eq 0) {
                return $constructor.Invoke(@())
            }
        }
        catch {
            $lastConstructorError = $_.Exception.GetBaseException().Message
        }
    }

    if ($lastConstructorError) { throw $lastConstructorError }
    if ($nodeCreationError) { throw $nodeCreationError }
    throw "窗口[$($FormType.FullName)]没有受支持的构造函数。"
}

function New-DemoWorkflow {
    $wizard = [Activator]::CreateInstance($wizardType)
    $workingArea = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $wizard.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $wizard.Bounds = $workingArea
    $wizard.Show()
    $wizard.Activate()
    Invoke-UiEvents 10

    $buttonAdd = Get-PrivateFieldValue $wizard 'buttonAdd'
    $tabControl = Get-PrivateFieldValue $wizard 'tabControl1'
    $buttonAdd.PerformClick()
    Invoke-UiEvents 10

    if ($tabControl.TabPages.Count -lt 1) {
        throw '执行新增流程后没有生成流程页。'
    }
    $selectedPage = $tabControl.SelectedTab
    $editPanel = @($selectedPage.Controls | Where-Object { $_.GetType().FullName -eq 'TDJS_Vision.Forms.ProcessNew.ProcessEditPanel' }) | Select-Object -First 1
    if ($null -eq $editPanel) {
        throw '新增流程页中没有找到流程编辑面板。'
    }

    $createNode = @($editPanel.GetType().GetMethods($bindingFlags) | Where-Object { $_.Name -eq 'CreateNode' -and $_.GetParameters().Count -eq 6 }) | Select-Object -First 1
    if ($null -eq $createNode) {
        throw '没有找到流程节点创建方法。'
    }

    $specifications = @(
        @('ImageSource', '1.图像源（未连接）', 40, 80),
        @('ImagePreprocess', '2.图像预处理', 290, 80),
        @('ImageCrop', '3.ROI裁剪', 540, 80),
        @('LineFind', '4.直线查找', 790, 80),
        @('CircleFind', '5.圆查找', 790, 310),
        @('Summarize', '6.结果汇总', 1040, 195),
        @('ImageShow', '7.图像显示', 1290, 195),
        @('ImageSave', '8.图片保存', 1290, 430)
    )
    $nodes = @{}
    foreach ($specification in $specifications) {
        $nodeType = [Enum]::Parse($nodeTypeType, $specification[0])
        $point = New-Object Drawing.Point ([int]$specification[2]), ([int]$specification[3])
        $arguments = [object[]]@($nodeType, $specification[1], -1, $point, $false, $false)
        $node = $createNode.Invoke($editPanel, $arguments)
        $nodes[$specification[0]] = $node
    }

    $process = Get-PrivateFieldValue $editPanel '_process'
    $process.ProcessName = '客户演示流程'
    $selectedPage.Text = '客户演示流程'
    $links = @(
        @('ImageSource', 'ImagePreprocess'),
        @('ImagePreprocess', 'ImageCrop'),
        @('ImageCrop', 'LineFind'),
        @('ImageCrop', 'CircleFind'),
        @('LineFind', 'Summarize'),
        @('CircleFind', 'Summarize'),
        @('Summarize', 'ImageShow'),
        @('ImageShow', 'ImageSave')
    )
    foreach ($link in $links) {
        $connection = [Activator]::CreateInstance($processConnectionType)
        $connection.FromNodeId = $nodes[$link[0]].ID
        $connection.ToNodeId = $nodes[$link[1]].ID
        $connection.FromAnchor = 'Right'
        $connection.ToAnchor = 'Left'
        $process.Connections.Add($connection)
    }
    $process.NotifyConnectionsChanged()
    # 运行参数等真实窗体从方案单例读取流程和节点；仅在本截图进程中注册演示对象。
    $solutionType = $assembly.GetType('TDJS_Vision.Solution', $true)
    $solutionInstance = $solutionType.GetProperty('Instance').GetValue($null, $null)
    if (-not $solutionInstance.AllProcesses.Contains($process)) {
        $solutionInstance.AllProcesses.Add($process)
    }
    foreach ($node in $process.Nodes) {
        if (-not $solutionInstance.Nodes.Contains($node)) {
            $solutionInstance.Nodes.Add($node)
        }
    }
    # 直接加载 Release 程序集不会经过主程序语言初始化，这里只修正演示截图中的可见占位键。
    Set-WorkflowDisplayText $wizard $process.Nodes.Count
    $wizard.Text = '流程编辑器'
    $canvas = Get-PrivateFieldValue $editPanel 'processFlowCanvas'
    $canvas.Invalidate()
    Invoke-UiEvents 15

    return [pscustomobject]@{
        Wizard = $wizard
        EditPanel = $editPanel
        TabControl = $tabControl
        Process = $process
        NodeCount = $process.Nodes.Count
        ConnectionCount = $process.Connections.Count
    }
}

$plan = Get-Content -LiteralPath $capturePlanPath -Encoding UTF8 -Raw | ConvertFrom-Json
$workflow = New-DemoWorkflow
$results = New-Object Collections.Generic.List[object]
$index = 0

foreach ($property in $plan.PSObject.Properties) {
    $index++
    $moduleName = $property.Name
    if ($OnlyModules.Count -gt 0 -and $moduleName -notin $OnlyModules) {
        continue
    }
    $task = $property.Value
    $fileName = Get-SafeFileName $index $moduleName
    $targetFile = Join-Path $outputPath $fileName
    $form = $null
    try {
        if ($moduleName -eq '流程画布') {
            Save-FormScreenshot $workflow.Wizard $targetFile
        }
        elseif ($moduleName -eq '流程管理') {
            Save-ControlScreenshot $workflow.TabControl $targetFile
        }
        elseif ($moduleName -eq '运行控制') {
            Save-ControlScreenshot $workflow.EditPanel $targetFile
        }
        elseif ($moduleName -in @('主界面与视图管理', '方案管理', '运行日志')) {
            $sourceName = if ($moduleName -eq '运行日志') { '001-平台基础-软件启动-主窗口.png' } else { '002-平台基础-主界面-停止运行状态.png' }
            Copy-Item -LiteralPath (Join-Path $originalScreenshotDirectory $sourceName) -Destination $targetFile -Force
        }
        else {
            $typeName = [IO.Path]::GetFileNameWithoutExtension([string]$task.source_window) -replace '^\d{3}-', ''
            $formType = $assembly.GetType($typeName, $false)
            if ($null -eq $formType -or -not $formBaseType.IsAssignableFrom($formType)) {
                throw "截图来源无法解析为真实窗体类型：$typeName"
            }
            $form = New-FormInstance $formType $workflow.Process
            Set-FormBounds $form
            $form.TopMost = $true
            $form.Show()
            $form.Activate()
            Invoke-UiEvents 12
            Set-ReadableDefaults $form
            Set-ModuleReadableState $moduleName $form
            Invoke-UiEvents 8
            Save-FormScreenshot $form $targetFile
        }

        $results.Add([pscustomobject]@{
            module = $moduleName
            file = $targetFile
            status = 'captured'
            node_count = $workflow.NodeCount
            connection_count = $workflow.ConnectionCount
            window_title = if ($null -ne $form) { $form.Text } else { $workflow.Wizard.Text }
            hardware_state = if ([bool]$task.hardware_required) { '未连接演示' } else { '不依赖硬件' }
            error = $null
        })
    }
    catch {
        $results.Add([pscustomobject]@{
            module = $moduleName
            file = $targetFile
            status = 'failed'
            node_count = $workflow.NodeCount
            connection_count = $workflow.ConnectionCount
            window_title = $null
            hardware_state = if ([bool]$task.hardware_required) { '未连接演示' } else { '不依赖硬件' }
            error = $_.Exception.GetBaseException().Message
        })
    }
    finally {
        if ($null -ne $form) {
            try { $form.Close() } catch {}
            try { $form.Dispose() } catch {}
        }
        $workflow.Wizard.Show()
        $workflow.Wizard.Activate()
        Invoke-UiEvents 2
    }
}

[IO.Directory]::CreateDirectory((Split-Path -Parent $resultPath)) | Out-Null
[IO.File]::WriteAllText(
    $resultPath,
    ($results | ConvertTo-Json -Depth 5),
    [Text.UTF8Encoding]::new($false)
)

$capturedCount = @($results | Where-Object status -eq 'captured').Count
$failedCount = @($results | Where-Object status -eq 'failed').Count
Write-Output "Captured=$capturedCount Failed=$failedCount Result=$resultPath"

try { $workflow.Wizard.Close() } catch {}
try { $workflow.Wizard.Dispose() } catch {}
[Environment]::Exit(0)










