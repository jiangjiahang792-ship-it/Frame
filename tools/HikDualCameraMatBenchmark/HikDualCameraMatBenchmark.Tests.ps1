$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sessionPath = Join-Path $root 'CameraBenchmarkSession.cs'
$designerPath = Join-Path $root 'MainForm.Designer.cs'
$projectPath = Join-Path $root 'HikDualCameraMatBenchmark.csproj'

$session = Get-Content -LiteralPath $sessionPath -Raw -Encoding UTF8
$designer = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8

if ($session -notmatch 'MV_CC_RegisterImageCallBackEx_NET') {
    throw '未使用海康官方回调注册接口。'
}

if ($session -notmatch 'MV_CC_ConvertPixelType_NET') {
    throw '回调中未调用海康官方像素转换接口。'
}

if ($session -notmatch 'bufferView\.Clone\(\)') {
    throw '没有把SDK转换缓冲区复制为独立Mat。'
}

if ($session -match 'File\.(Write|Append)' -or $session -match 'Console\.Write') {
    throw '相机热路径存在同步日志或磁盘写入。'
}

if ($designer -notmatch 'private System\.Windows\.Forms\.PictureBox pictureCamera1' -or
    $designer -notmatch 'private System\.Windows\.Forms\.PictureBox pictureCamera2') {
    throw '双相机显示控件没有放入Designer文件。'
}

if ($project -notmatch '<PlatformTarget>x64</PlatformTarget>' -or
    $project -notmatch '<Optimize>true</Optimize>') {
    throw 'Release x64性能测试配置不完整。'
}

Write-Output 'HikDualCameraMatBenchmark static checks passed.'
