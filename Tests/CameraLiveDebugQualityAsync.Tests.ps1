$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$controlPath = Join-Path $root "Forms\CameraAdd\CameraLiveDebugControl.cs"
$designerPath = Join-Path $root "Forms\CameraAdd\CameraLiveDebugControl.Designer.cs"
$projectPath = Join-Path $root "TDJS-Vision.csproj"
$qualityDir = Join-Path $root "Forms\CameraAdd\Quality"

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw $Message
    }
}

function Assert-Regex {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

if (-not (Test-Path $qualityDir)) {
    throw "Missing camera quality analysis folder."
}

$control = Get-Content -LiteralPath $controlPath -Encoding UTF8 -Raw
$designer = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw
$project = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw

Assert-Contains $project "Forms\CameraAdd\Quality\ImageQualityAnalyzer.cs" "Project must include the image quality analyzer."
Assert-Contains $project "Forms\CameraAdd\Quality\CameraImageQualityResult.cs" "Project must include the quality result model."
Assert-Contains $project "Forms\CameraAdd\Quality\MovingAverageFilter.cs" "Project must include the smoothing filter."

Assert-Contains $control "private int _qualityAnalysisRunning;" "Preview control must use a concurrent gate for quality analysis."
Assert-Contains $control "AnalyzePreviewQualityAsync" "Preview control must analyze quality on a background task."
Assert-Contains $control "Interlocked.CompareExchange(ref _qualityAnalysisRunning, 1, 0)" "Only one quality analysis task may run at a time."
Assert-Contains $control "Task.Run(() => AnalyzePreviewQualityAsync" "Quality analysis must run in the background, not in the preview UI update path."
Assert-Contains $control "new Bitmap(bitmap)" "Quality analysis must clone the bitmap so preview ownership is not blocked."
Assert-Regex $control "ShowPreviewImageAsync\(Bitmap bitmap\)[\s\S]*?TryPrepareQualityAnalysisBitmap\(bitmap, out Bitmap analysisBitmap\)[\s\S]*?BeginInvokeSafe[\s\S]*?StartPreviewQualityAnalysis\(analysisBitmap\)" "Preview control must clone analysis image before UI ownership transfer and start analysis after preview posting."
Assert-Contains $control "private Rectangle _qualityAnalysisRoi = Rectangle.Empty;" "Preview control must keep an image-coordinate ROI snapshot for quality analysis."
Assert-Contains $control "private static readonly Dictionary<string, Rectangle> _qualityRoiByCameraKey" "Preview control must persist ROI by camera key across control recreation."
Assert-Contains $control "GetQualityRoiStorageKey" "Preview control must build a stable camera key for ROI persistence."
Assert-Contains $control "TryRestoreQualityRoiSnapshot" "Preview control must restore saved ROI before falling back to default ROI."
Assert-Contains $control "SaveQualityRoiSnapshot" "Preview control must save edited ROI for the selected camera."
Assert-Contains $control "private enum RoiHitArea" "Preview control must support ROI hit testing for drag/resize editing."
Assert-Contains $control "private bool _isDraggingQualityRoi;" "Preview control must keep ROI drag state."
Assert-Contains $control "CreateDefaultQualityRoi" "Preview control must create a demo-style default center ROI."
Assert-Contains $control "ClampQualityRoi" "Preview control must clamp ROI to the current image bounds."
Assert-Contains $control "UpdateQualityRoiFromInputs" "ROI numeric inputs must update the quality analysis ROI."
Assert-Regex $control "AnalyzePreviewQualityAsync\(Bitmap bitmap\)[\s\S]*?GetQualityAnalysisRoi\(bitmap\.Size\)[\s\S]*?_qualityAnalyzer\.Analyze" "Quality analysis must pass the selected ROI into the analyzer."
Assert-Contains $control "pictureBoxPreview.Paint += PictureBoxPreview_Paint;" "Preview picture box must draw the selected ROI overlay."
Assert-Contains $control "pictureBoxPreview.MouseDown += PictureBoxPreview_MouseDown;" "Preview picture box must start ROI editing from mouse down."
Assert-Contains $control "pictureBoxPreview.MouseMove += PictureBoxPreview_MouseMove;" "Preview picture box must drag or resize ROI from mouse move."
Assert-Contains $control "pictureBoxPreview.MouseUp += PictureBoxPreview_MouseUp;" "Preview picture box must complete ROI editing from mouse up."
Assert-Contains $control "DrawImageRoiOverlay" "Preview must render the ROI overlay so users can see the active analysis area."
Assert-Contains $control "HitTestQualityRoi" "Preview control must decide whether mouse hits ROI body or resize handles."
Assert-Contains $control "DisplayRectangleToImageRoi" "Preview control must convert dragged display ROI back to image coordinates."
Assert-Regex $control "EnsureQualityRoiForImageSize\(System\.Drawing\.Size imageSize\)[\s\S]*?TryRestoreQualityRoiSnapshot\(imageSize, out Rectangle restoredRoi\)[\s\S]*?CreateDefaultQualityRoi" "ROI initialization must prefer saved camera ROI before default ROI."
Assert-Regex $control "UpdateQualityRoiFromInputs\(object sender, EventArgs e\)[\s\S]*?SaveQualityRoiSnapshot\(roi\)" "Manual ROI numeric edits must be saved."
Assert-Regex $control "PictureBoxPreview_MouseMove\(object sender, MouseEventArgs e\)[\s\S]*?SaveQualityRoiSnapshot\(imageRoi\)" "Dragged ROI edits must be saved."
Assert-Regex $designer "Dispose\(bool disposing\)[\s\S]*?SaveQualityRoiSnapshot\(\)" "ROI snapshot must be saved when the debug control is disposed."
Assert-Contains $control "TryPrepareQualityAnalysisBitmap" "Preview control must create the analysis bitmap before handing the source bitmap to PictureBox."
Assert-Regex $control "ShowPreviewImageAsync\(Bitmap bitmap\)[\s\S]*?TryPrepareQualityAnalysisBitmap\(bitmap, out Bitmap analysisBitmap\)[\s\S]*?BeginInvokeSafe" "Analysis clone must be prepared before UI posting to avoid GDI+ object-in-use races."
Assert-Contains $control "BuildQualitySuggestion" "Preview control must build a brightness-first user suggestion."
Assert-Regex $control "BuildQualitySuggestion[\s\S]*?BrightnessLevel\.暗[\s\S]*?BrightnessLevel\.亮[\s\S]*?SharpnessLevel\.模糊" "Suggestion logic must prioritize brightness before sharpness."

Assert-Contains $designer "labelBrightnessStateValue" "Designer must contain the brightness state label."
Assert-Contains $designer "labelSharpnessStateValue" "Designer must contain the sharpness state label."
Assert-Contains $designer "labelQualitySuggestionValue" "Designer must contain the brightness-first suggestion label."
Assert-Contains $designer "buttonEditRoi" "Designer must contain the ROI edit button."
Assert-Contains $designer "buttonCompleteRoi" "Designer must contain the ROI complete button."
Assert-Contains $designer "亮度判定" "Brightness result text must be shown in Simplified Chinese."
Assert-Contains $designer "清晰度判定" "Sharpness result text must be shown in Simplified Chinese."
Assert-Contains $designer "编辑ROI" "ROI edit button text must be shown in Simplified Chinese."
Assert-Contains $designer "完成" "ROI complete button text must be shown in Simplified Chinese."

Write-Host "Camera live debug async quality analysis checks passed."
