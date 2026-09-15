$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-ProjectIncludes {
    param([string]$Project, [string]$RelativePath)
    $projectPath = $RelativePath.Replace('/', '\')
    Assert-ContainsText $Project ('<Compile Include="' + $projectPath + '"') "Project file must include $projectPath."
}

function New-Text {
    param([int[]]$Codes)
    return -join ($Codes | ForEach-Object { [char]$_ })
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$paramPath = Join-Path $projectRoot 'Node\3-Detection\TDAI\NodeParamTDAI.cs'
$nodePath = Join-Path $projectRoot 'Node\3-Detection\TDAI\NodeTDAI.cs'
$formPath = Join-Path $projectRoot 'Node\3-Detection\TDAI\ParamFormTDAI.cs'
$designerPath = Join-Path $projectRoot 'Node\3-Detection\TDAI\ParamFormTDAI.Designer.cs'
$editorPath = Join-Path $projectRoot 'Node\3-Detection\TDAI\ParamFormTDAIRoiEditor.cs'
$editorDesignerPath = Join-Path $projectRoot 'Node\3-Detection\TDAI\ParamFormTDAIRoiEditor.Designer.cs'
$showImagePath = Join-Path $projectRoot 'Forms\DispShowImage\ShowImageControl.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'

$param = Get-Content -LiteralPath $paramPath -Raw -Encoding UTF8
$node = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$designer = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$editor = Get-Content -LiteralPath $editorPath -Raw -Encoding UTF8
$editorDesigner = Get-Content -LiteralPath $editorDesignerPath -Raw -Encoding UTF8
$showImage = Get-Content -LiteralPath $showImagePath -Raw -Encoding UTF8
$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8

$enableRoiText = New-Text @(0x542F, 0x7528, 0x0052, 0x004F, 0x0049)
$drawRegionText = New-Text @(0x7ED8, 0x5236, 0x68C0, 0x6D4B, 0x533A, 0x57DF)
$deletePromptText = New-Text @(0x6309, 0x0020, 0x0044, 0x0065, 0x006C, 0x0020, 0x5220, 0x9664, 0x5BF9, 0x5E94, 0x0020, 0x0052, 0x004F, 0x0049)
$clearRoiText = New-Text @(0x6E05, 0x7A7A, 0x0052, 0x004F, 0x0049)
$getCurrentImageText = New-Text @(0x83B7, 0x53D6, 0x5F53, 0x524D, 0x56FE, 0x50CF)

Assert-ContainsText $param 'public bool RoiEnable { get; set; }' 'TDAI params must persist the ROI enable switch.'
Assert-ContainsText $param 'public List<TDAIRoiRegion> RoiRegions { get; set; }' 'TDAI params must persist multiple ROI regions.'
Assert-ContainsText $param 'public class TDAIRoiRegion' 'TDAI must declare the ROI region parameter class.'
Assert-ContainsText $param 'public TDAIRoiRegion Clone()' 'ROI params must support isolated cloning.'

Assert-ContainsText $designer 'private Sunny.UI.UISwitch uiSwitch_RoiEnable;' 'ParamFormTDAI designer must declare the ROI enable switch.'
Assert-ContainsText $designer 'private System.Windows.Forms.LinkLabel linkLabelRoiEdit;' 'ParamFormTDAI designer must declare the ROI edit link.'
Assert-ContainsText $designer ('this.labelRoiEnable.Text = "' + $enableRoiText + '";') 'ROI enable label must use simplified Chinese text.'
Assert-ContainsText $designer ('this.linkLabelRoiEdit.Text = "' + $drawRegionText + '";') 'ROI edit link must use simplified Chinese text.'

Assert-ContainsText $form 'nodeParamTDAI.RoiEnable = uiSwitch_RoiEnable.Active;' 'SaveParams must write the ROI enable state.'
Assert-ContainsText $form 'nodeParamTDAI.RoiRegions = CloneRoiRegions(_roiRegions);' 'SaveParams must write the ROI region collection.'
Assert-ContainsText $form 'new ParamFormTDAIRoiEditor(bitmap, _roiRegions, GetCurrentRoiBitmapAsync)' 'The edit link must open the ROI editor form with a current-image provider.'
Assert-ContainsText $form 'private async Task<Bitmap> GetCurrentRoiBitmapAsync()' 'The form must centralize current-image refresh for entering and manual refresh.'
Assert-ContainsText $form 'await _node.Process.RunForUpdateImages(_node);' 'The ROI editor must refresh the subscribed image before opening.'

Assert-ContainsText $editor 'showImageControl1.EnableRectangleRoiDrawing = true;' 'The ROI editor must enable rectangle drawing on ShowImageControl.'
Assert-ContainsText $editor 'GetAllRotatedRectInfos()' 'The ROI editor must read multiple dynamic ROI rectangles from ShowImageControl.'
Assert-ContainsText $editor 'private async void buttonGetCurrentImage_Click' 'The ROI editor must provide a manual current-image refresh handler.'
Assert-ContainsText $editor 'bitmap = await _currentImageProvider();' 'Manual current-image refresh must use the shared provider.'
Assert-ContainsText $editor 'showImageControl1.SetImage(bitmap);' 'Manual current-image refresh must update ShowImageControl.'
Assert-ContainsText $editorDesigner 'TDJS_Vision.Forms.DispShowImage.ShowImageControl' 'The ROI editor must use ShowImageControl.'
Assert-ContainsText $editorDesigner $deletePromptText 'The ROI editor must explain Del deletion.'
Assert-ContainsText $editorDesigner ('this.buttonGetCurrentImage.Text = "' + $getCurrentImageText + '";') 'The ROI editor must expose a manual current-image button.'
Assert-ContainsText $editorDesigner ('this.buttonClear.Text = "' + $clearRoiText + '";') 'The ROI editor must provide a clear ROI button.'

Assert-ContainsText $showImage 'public bool EnableRectangleRoiDrawing' 'ShowImageControl must expose a rectangle drawing switch.'
Assert-ContainsText $showImage 'public bool DeleteSelectedDynamicRoi()' 'ShowImageControl must support deleting the selected dynamic ROI.'
Assert-ContainsText $showImage 'e.KeyCode == Keys.Delete' 'ShowImageControl must respond to Delete.'
Assert-ContainsText $showImage 'BeginRectangleRoiDrawing(imgPt)' 'ShowImageControl must start drawing on blank image area.'

Assert-ContainsText $node 'inputImage = BuildRoiInputImage(inputImage, param);' 'TDAI runtime must build ROI input images before inference.'
Assert-ContainsText $node 'BuildOutputRoiRects(localRoiRects, GetBaseOffsetRect(inputImage))' 'TDAI ROI offsets must include upstream offsets.'
Assert-ContainsText $node 'handleDet.Detect(img, offsetRect.X, offsetRect.Y)' 'DET inference must pass each ROI offset.'
Assert-ContainsText $node 'handle.Detect(img, offsetRect.X, offsetRect.Y, needMaskBox)' 'SEG inference must pass each ROI offset.'

Assert-ProjectIncludes $project 'Node\3-Detection\TDAI\ParamFormTDAIRoiEditor.cs'
Assert-ProjectIncludes $project 'Node\3-Detection\TDAI\ParamFormTDAIRoiEditor.Designer.cs'

Write-Host 'TDAI ROI detection source checks passed.'
