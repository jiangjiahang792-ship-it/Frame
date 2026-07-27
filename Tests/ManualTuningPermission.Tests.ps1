$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$contextPath = Join-Path $root "Forms\Login\UserPermissionContext.cs"
$loginPath = Join-Path $root "Forms\Login\FormLogin.cs"
$imagePath = Join-Path $root "Forms\ImageViewer\FrmSingleImage.cs"
$mainPath = Join-Path $root "FormMain.cs"
$projectPath = Join-Path $root "TDJS-Vision.csproj"

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

if (-not (Test-Path -LiteralPath $contextPath)) {
    throw '缺少统一的当前用户权限上下文。'
}

$context = Get-Content -Raw -Encoding UTF8 $contextPath
$login = Get-Content -Raw -Encoding UTF8 $loginPath
$image = Get-Content -Raw -Encoding UTF8 $imagePath
$main = Get-Content -Raw -Encoding UTF8 $mainPath
$project = Get-Content -Raw -Encoding UTF8 $projectPath

Assert-Contains $context 'public static bool CanUseManualTuning' '权限上下文缺少手动调参与一键学习能力判断。'
Assert-Contains $context 'CurrentRole == UserRole.Hight' '手动调参与一键学习没有限制为最高权限。'
Assert-Contains $context 'public static event EventHandler<UserRole> RoleChanged' '权限变化无法即时通知已打开的图像窗口。'
Assert-Contains $login 'UserPermissionContext.UpdateRole(UserRole.Hight);' '高级登录没有更新统一权限上下文。'
Assert-Contains $login 'UserPermissionContext.UpdateRole(UserRole.Medium);' '中级登录没有更新统一权限上下文。'
Assert-Contains $login 'UserPermissionContext.UpdateRole(UserRole.Low);' '初级登录没有更新统一权限上下文。'
Assert-Contains $login 'UserPermissionContext.UpdateRole(UserRole.Unknown);' '退出登录没有撤销手动调参与一键学习权限。'
Assert-Contains $image 'UserPermissionContext.RoleChanged +=' '图像窗口没有监听权限变化。'
Assert-Contains $image 'button1.Enabled = canUseManualTuning;' '一键学习按钮没有按权限即时禁用。'
Assert-Contains $image 'button2.Enabled = canUseManualTuning;' '手动调参按钮没有按权限即时禁用。'
Assert-Contains $image 'if (!UserPermissionContext.CanUseManualTuning)' '按钮事件缺少权限兜底校验。'
Assert-Contains $main 'tsbt_RunParamSetting.Enabled = false;' '中低权限仍可从主工具栏绕过手动调参限制。'
Assert-Contains $main 'if (!UserPermissionContext.CanUseManualTuning)' '主窗口运行参数入口缺少最高权限兜底校验。'
Assert-Contains $project '<Compile Include="Forms\Login\UserPermissionContext.cs" />' '权限上下文没有加入项目编译。'

$lockMethod = [regex]::Match(
    $main,
    'private void SetLockStatus\(UserRole role\)(?<Body>.*?)private void FormMain_FormClosing',
    [System.Text.RegularExpressions.RegexOptions]::Singleline).Groups['Body'].Value
$highBlock = [regex]::Match(
    $lockMethod,
    'case UserRole\.Hight:(?<Body>.*?)break;',
    [System.Text.RegularExpressions.RegexOptions]::Singleline).Groups['Body'].Value
$mediumBlock = [regex]::Match(
    $lockMethod,
    'case UserRole\.Medium:(?<Body>.*?)break;',
    [System.Text.RegularExpressions.RegexOptions]::Singleline).Groups['Body'].Value

Assert-Contains $highBlock 'tsbt_RunParamSetting.Enabled = true;' '最高权限没有开放主工具栏运行参数入口。'
Assert-Contains $mediumBlock 'tsbt_RunParamSetting.Enabled = false;' '中级权限仍可从主工具栏进入运行参数。'

Write-Host "手动调参与一键学习权限检查通过。"
