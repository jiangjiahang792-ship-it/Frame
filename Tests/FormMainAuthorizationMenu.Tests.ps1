$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$designerPath = Join-Path $root "FormMain.Designer.cs"
$formPath = Join-Path $root "FormMain.cs"
$launcherPath = Join-Path $root "ExternalProgramLauncher.cs"
$projectPath = Join-Path $root "TDJS-Vision.csproj"

function Assert-Contains {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Message
    )

    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

$designer = Get-Content -Raw -Encoding UTF8 $designerPath
$form = Get-Content -Raw -Encoding UTF8 $formPath
$project = Get-Content -Raw -Encoding UTF8 $projectPath

Assert-Contains $designer 'this.授权ToolStripMenuItem.Click += new System.EventHandler(this.授权ToolStripMenuItem_Click);' '授权菜单未绑定点击事件。'
Assert-Contains $form 'Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activate.exe")' '授权程序路径未从软件运行目录生成。'
Assert-Contains $form '未找到授权程序' '缺少授权程序不存在时的中文提示。'
Assert-Contains $form '启动授权程序失败' '缺少授权程序启动异常时的中文提示。'
Assert-Contains $form 'externalProgramLauncher.Start(activationProgramPath);' 'FormMain 未通过外部程序启动接口启动授权程序。'
Assert-Contains $project '<Compile Include="ExternalProgramLauncher.cs" />' '项目未编译外部程序启动器。'

if (-not (Test-Path -LiteralPath $launcherPath)) {
    throw '缺少 ExternalProgramLauncher.cs。'
}

$launcher = Get-Content -Raw -Encoding UTF8 $launcherPath
Assert-Contains $launcher 'interface IExternalProgramLauncher' '缺少外部程序启动接口。'
Assert-Contains $launcher 'void Start(string executablePath);' '外部程序启动接口签名不正确。'
Assert-Contains $launcher 'sealed class ExternalProgramLauncher : IExternalProgramLauncher' '缺少默认外部程序启动实现。'
Assert-Contains $launcher 'UseShellExecute = true' '默认启动器未启用可执行文件 Shell 启动。'
Assert-Contains $launcher 'WorkingDirectory = Path.GetDirectoryName(executablePath)' '默认启动器未设置授权程序工作目录。'
Assert-Contains $launcher 'System.Diagnostics.Process.Start(startInfo);' '默认启动器必须使用完整类型名，避免与项目流程类 Process 冲突。'

Write-Host "FormMain 授权菜单检查通过。"
