$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if ($Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$nodeSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\NodeColorDiscern.cs') -Raw -Encoding UTF8
$formSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\NodeParamFormColorDiscern.cs') -Raw -Encoding UTF8
$formDesigner = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\NodeParamFormColorDiscern.Designer.cs') -Raw -Encoding UTF8
$creatorSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\ColorCreate.cs') -Raw -Encoding UTF8
$creatorDesigner = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\ColorCreate.Designer.cs') -Raw -Encoding UTF8

Assert-ContainsText $nodeSource 'inputLease = inputImage.AcquireLease();' 'Color detection must lease the upstream image across asynchronous work.'
Assert-ContainsText $nodeSource 'pendingResult.OutputImage = OutputImage.FromBorrowedSingleImage' 'Color detection output must keep a parent dependency lease.'
Assert-ContainsText $nodeSource 'NodeResultResourceManager.Release(pendingResult);' 'A failed color-result publication must release the pending result.'
Assert-ContainsText $nodeSource 'inputLease?.Dispose();' 'The color-detection computation lease must be released in finally.'
Assert-True (([regex]::Matches($nodeSource, 'Result\s*=\s*pendingResult\s*;')).Count -eq 1) 'Color detection must publish the completed result exactly once.'
Assert-NotContainsText $nodeSource 'Result = nodeResult;' 'Color detection must not publish the same result before it is complete.'

Assert-ContainsText $formSource 'private void ReplaceSourceImage(Mat nextSource)' 'The parameter form must replace and dispose its owned source Mat through one path.'
Assert-ContainsText $formSource 'using (Mat sourceSnapshot = sourceImage.Clone())' 'Parameter preview must use an owned snapshot across await.'
Assert-ContainsText $formSource 'using (var colorCreate = new ColorCreate())' 'The color-template dialog must be disposed after it closes.'
Assert-ContainsText $formDesigner 'ReleaseImageResources();' 'Parameter-form disposal must release the final source Mat.'
Assert-NotContainsText $formSource 'private static ColorCreate colorCreate' 'The parameter form must not retain a static template dialog.'

Assert-ContainsText $creatorSource 'nextImage = Cv2.ImRead(textBox1.Text, ImreadModes.Color);' 'File loading must retain and dispose the native Mat directly.'
Assert-NotContainsText $creatorSource 'Cv2.ImRead(textBox1.Text).ToBitmap()' 'File loading must not leak the temporary ImRead Mat through a conversion chain.'
Assert-ContainsText $creatorSource 'sourceSnapshot = CurrentImage.Clone();' 'Background template detection must use an owned image snapshot.'
Assert-ContainsText $creatorSource 'resultDisplay?.Dispose();' 'Unpublished preview bitmaps must be released in finally.'
Assert-ContainsText $creatorSource 'sourceSnapshot?.Dispose();' 'Background source snapshots must be released in finally.'
Assert-ContainsText $creatorSource 'using (Mat mean = new Mat())' 'Automatic color sampling must release its mean Mat.'
Assert-ContainsText $creatorSource 'using (Mat stdDev = new Mat())' 'Automatic color sampling must release its standard-deviation Mat.'
Assert-ContainsText $creatorDesigner 'ReleaseImageResources();' 'Template-dialog disposal must release the final source Mat.'

Write-Host 'Color discern resource lifecycle checks passed.'
