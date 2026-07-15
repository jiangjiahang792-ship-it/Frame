param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseExe,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ResultJson
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class CaptureDpiAwareness
{
    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();
}
'@

[CaptureDpiAwareness]::SetProcessDPIAware() | Out-Null

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    throw 'This script must run in STA mode.'
}

[Windows.Forms.Application]::EnableVisualStyles()
[Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)

$releaseDirectory = Split-Path -Parent $ReleaseExe
[Environment]::CurrentDirectory = $releaseDirectory
$assembly = [Reflection.Assembly]::LoadFrom($ReleaseExe)
$formBaseType = [Windows.Forms.Form]
$nodeBaseType = $assembly.GetType('TDJS_Vision.Node.NodeBase', $true)
$processType = $assembly.GetType('TDJS_Vision.Process', $true)
$nodeTypeType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$unknownNodeType = [Enum]::Parse($nodeTypeType, 'UNKNOWN')

[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

$nodeTypes = @(
    $assembly.GetTypes() |
        Where-Object { -not $_.IsAbstract -and $nodeBaseType.IsAssignableFrom($_) -and $_ -ne $nodeBaseType }
)

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

    $ranked = foreach ($nodeType in $nodeTypes) {
        $candidate = Get-NormalizedName $nodeType.Name
        $score = 0
        if ($candidate -eq $target) { $score = 100 }
        elseif ($candidate.Contains($target) -or $target.Contains($candidate)) { $score = 50 }
        if ($nodeType.Namespace -eq $FormType.Namespace) { $score += 20 }
        if ($score -gt 0) {
            [pscustomobject]@{ Type = $nodeType; Score = $score }
        }
    }

    $match = $ranked | Sort-Object Score -Descending | Select-Object -First 1
    if ($null -eq $match) {
        throw "No node type was found for $($FormType.FullName)."
    }

    return [Activator]::CreateInstance(
        $match.Type,
        [object[]]@(1, 'ManualCapture', $Process, $unknownNodeType)
    )
}

function New-FormInstance {
    param(
        [Type]$FormType,
        [object]$Process
    )

    $constructors = @($FormType.GetConstructors() | Sort-Object { $_.GetParameters().Count })
    foreach ($constructor in $constructors) {
        $parameters = @($constructor.GetParameters())
        try {
            if ($parameters.Count -eq 0) {
                return $constructor.Invoke(@())
            }

            if ($parameters.Count -eq 1 -and $nodeBaseType.IsAssignableFrom($parameters[0].ParameterType)) {
                $node = Get-NodeForForm $FormType $Process
                return $constructor.Invoke([object[]]@($node))
            }

            if ($parameters.Count -eq 1 -and $parameters[0].ParameterType -eq $processType) {
                return $constructor.Invoke([object[]]@($Process))
            }

            if (
                $parameters.Count -eq 2 -and
                $parameters[0].ParameterType -eq $processType -and
                $nodeBaseType.IsAssignableFrom($parameters[1].ParameterType)
            ) {
                $node = Get-NodeForForm $FormType $Process
                return $constructor.Invoke([object[]]@($Process, $node))
            }

            if (
                $parameters.Count -eq 2 -and
                $parameters[0].ParameterType -eq [string] -and
                $parameters[1].ParameterType -eq $processType
            ) {
                return $constructor.Invoke([object[]]@('ManualCapture', $Process))
            }
        }
        catch {
            $lastConstructorError = $_.Exception.GetBaseException().Message
        }
    }

    if ($lastConstructorError) {
        throw $lastConstructorError
    }

    throw "No supported constructor was found for $($FormType.FullName)."
}

function Save-FormScreenshot {
    param(
        [Windows.Forms.Form]$Form,
        [string]$Path
    )

    $bounds = $Form.Bounds
    if ($bounds.Width -lt 100 -or $bounds.Height -lt 80) {
        throw "Invalid form bounds: $($bounds.Width)x$($bounds.Height)."
    }

    $bitmap = New-Object Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$excludedTypes = @(
    'TDJS_Vision.FormMain'
)

$formTypes = @(
    $assembly.GetTypes() |
        Where-Object {
            -not $_.IsAbstract -and
            $formBaseType.IsAssignableFrom($_) -and
            $excludedTypes -notcontains $_.FullName -and
            (
                $_.FullName -match '^TDJS_Vision\.Forms\.' -or
                $_.FullName -match '^TDJS_Vision\.Node\..*(ParamForm|NodeParamForm)'
            )
        } |
        Sort-Object FullName
)

$process = [Activator]::CreateInstance($processType, [object[]]@('ManualCapture'))
$results = New-Object Collections.Generic.List[object]
$openedForms = New-Object Collections.Generic.List[Windows.Forms.Form]
$index = 10

foreach ($formType in $formTypes) {
    $form = $null
    $fileName = ('{0:D3}-{1}.png' -f $index, ($formType.FullName -replace '[^A-Za-z0-9._-]', '_'))
    $outputPath = Join-Path $OutputDirectory $fileName
    try {
        $form = New-FormInstance $formType $process
        $form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
        $workingArea = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        if ($form.Width -gt $workingArea.Width) { $form.Width = $workingArea.Width }
        if ($form.Height -gt $workingArea.Height) { $form.Height = $workingArea.Height }
        $form.Left = [Math]::Max(0, [int](($workingArea.Width - $form.Width) / 2))
        $form.Top = [Math]::Max(0, [int](($workingArea.Height - $form.Height) / 2))
        $form.Show()
        $form.Activate()
        for ($tick = 0; $tick -lt 8; $tick++) {
            [Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 120
        }
        Save-FormScreenshot $form $outputPath
        $form.Hide()
        $openedForms.Add($form)
        $results.Add([pscustomobject]@{
            index = $index
            type = $formType.FullName
            title = $form.Text
            file = $fileName
            status = 'captured'
            error = $null
        })
    }
    catch {
        $results.Add([pscustomobject]@{
            index = $index
            type = $formType.FullName
            title = $null
            file = $fileName
            status = 'failed'
            error = $_.Exception.GetBaseException().Message
        })
    }
    finally {
        if ($null -ne $form) {
            try { $form.Hide() } catch {}
        }
    }

    $index++
}

$json = $results | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($ResultJson, $json, [Text.UTF8Encoding]::new($false))

$capturedCount = @($results | Where-Object status -eq 'captured').Count
$failedCount = @($results | Where-Object status -eq 'failed').Count
Write-Output "Captured=$capturedCount Failed=$failedCount Result=$ResultJson"
[Console]::Out.Flush()
[Environment]::Exit(0)
