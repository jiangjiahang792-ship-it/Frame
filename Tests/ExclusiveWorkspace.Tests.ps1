$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    if (-not $Text.Contains($Needle)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    if ($Text.Contains($Needle)) {
        throw $Message
    }
}

$testRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Join-Path (Get-Location) 'Tests'
}
else {
    $PSScriptRoot
}

$projectRoot = Split-Path -Parent $testRoot
$formMainPath = Join-Path $projectRoot 'FormMain.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$workspaceInterfacePath = Join-Path $projectRoot 'Forms\Workspace\IExclusiveWorkspacePresenter.cs'
$workspacePresenterPath = Join-Path $projectRoot 'Forms\Workspace\ExclusiveWorkspacePresenter.cs'
$loadingInterfacePath = Join-Path $projectRoot 'Startup\ISolutionLoadingCoordinator.cs'
$loadingCoordinatorPath = Join-Path $projectRoot 'Startup\SolutionLoadingCoordinator.cs'

Assert-True (Test-Path -LiteralPath $workspaceInterfacePath) '缺少独占工作区显示接口。'
Assert-True (Test-Path -LiteralPath $workspacePresenterPath) '缺少独占工作区显示实现。'
Assert-True (Test-Path -LiteralPath $loadingInterfacePath) '缺少方案动画加载接口。'
Assert-True (Test-Path -LiteralPath $loadingCoordinatorPath) '缺少方案动画加载实现。'

$formMainSource = Get-Content -LiteralPath $formMainPath -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
$workspacePresenterSource = Get-Content -LiteralPath $workspacePresenterPath -Raw -Encoding UTF8
$loadingCoordinatorSource = Get-Content -LiteralPath $loadingCoordinatorPath -Raw -Encoding UTF8

Assert-Contains $formMainSource 'IExclusiveWorkspacePresenter exclusiveWorkspacePresenter' 'FormMain 必须依赖独占工作区显示接口。'
Assert-Contains $formMainSource 'ISolutionLoadingCoordinator solutionLoadingCoordinator' 'FormMain 必须依赖方案动画加载接口。'
Assert-Contains $formMainSource 'exclusiveWorkspacePresenter.ShowDialog(this, FrmNewProcessWizard);' '流程编辑必须使用独占工作区显示接口。'
$aiPresenterCalls = [regex]::Matches($formMainSource, 'exclusiveWorkspacePresenter\.ShowDialog\(this, form\);').Count
Assert-True ($aiPresenterCalls -eq 2) '无监督训练和大模型训练必须全部使用独占工作区显示接口。'
Assert-NotContains $formMainSource 'ShowOwnedMaximizedDialog(' 'FormMain 不应继续保留只禁用但不隐藏主窗体的旧显示方法。'

$mainHideIndex = $workspacePresenterSource.IndexOf('mainForm.Hide();')
$workspaceShowIndex = $workspacePresenterSource.IndexOf('workspaceForm.ShowDialog(mainForm);')
$workspaceTryIndex = $workspacePresenterSource.IndexOf('try')
$finallyIndex = $workspacePresenterSource.IndexOf('finally')
$mainShowIndex = $workspacePresenterSource.IndexOf('mainForm.Show();', $finallyIndex)
$mainActivateIndex = $workspacePresenterSource.IndexOf('mainForm.Activate();', $finallyIndex)
$mainBringToFrontIndex = $workspacePresenterSource.IndexOf('mainForm.BringToFront();', $finallyIndex)
Assert-True ($mainHideIndex -ge 0 -and $mainHideIndex -lt $workspaceShowIndex) '显示工作窗口前必须隐藏主窗体。'
Assert-True ($workspaceTryIndex -ge 0 -and $workspaceTryIndex -lt $mainHideIndex) '主窗体隐藏操作必须由恢复用的 try/finally 保护。'
Assert-True ($finallyIndex -gt $workspaceShowIndex) '主窗体恢复必须放在 finally 中。'
Assert-True ($mainShowIndex -gt $finallyIndex) '工作窗口结束后必须重新显示主窗体。'
Assert-True ($mainActivateIndex -gt $mainShowIndex) '恢复主窗体后必须激活它。'
Assert-True ($mainBringToFrontIndex -gt $mainActivateIndex) '激活主窗体后必须置前。'

Assert-Contains $formMainSource 'solutionLoadingCoordinator.Load(this, openFileDialog1.FileName, true);' '手动打开方案必须使用动画加载协调器。'
Assert-NotContains $formMainSource 'Solution.Instance.Load(openFileDialog1.FileName, true);' '手动打开方案不应继续走吞掉异常的旧加载入口。'
Assert-Contains $loadingCoordinatorSource 'splashController.Start();' '方案加载前必须启动动画窗体。'
Assert-Contains $loadingCoordinatorSource 'mainForm.Hide();' '方案加载期间必须隐藏主窗体。'
Assert-Contains $loadingCoordinatorSource 'StartupProgressContext.Begin(splashController)' '方案加载必须建立真实进度作用域。'
Assert-Contains $loadingCoordinatorSource 'ConfigHelper.SolLoad(solutionPath, showInfo);' '方案必须在协调器中执行真实反序列化。'
Assert-Contains $loadingCoordinatorSource 'StartupProgressContext.ThrowIfFailures();' '设备或流程加载失败必须传回协调器。'
Assert-Contains $loadingCoordinatorSource 'TimeSpan.FromSeconds(2)' '手动方案加载动画必须至少显示 2 秒。'
Assert-Contains $loadingCoordinatorSource 'Thread.Sleep(remaining);' '同步方案加载结束后必须补足最短动画时间。'
Assert-NotContains $loadingCoordinatorSource 'Task.Run' '方案反序列化禁止移动到后台线程操作 WinForms 控件。'
$loadingTryIndex = $loadingCoordinatorSource.IndexOf('try')
$loadingHideIndex = $loadingCoordinatorSource.IndexOf('mainForm.Hide();')
$minimumWaitCallIndex = $loadingCoordinatorSource.IndexOf('WaitForMinimumSplashDuration(stopwatch);')
$restoreCallIndex = $loadingCoordinatorSource.IndexOf('RestoreMainForm(mainForm, splashController, restoreMainForm);')
Assert-True ($loadingTryIndex -ge 0 -and $loadingTryIndex -lt $loadingHideIndex) '方案加载隐藏主窗体的操作必须由 try/finally 保护。'
Assert-True ($minimumWaitCallIndex -ge 0 -and $minimumWaitCallIndex -lt $restoreCallIndex) '成功或失败都必须先补足最短动画时间，再恢复主窗体。'
Assert-Contains $loadingCoordinatorSource '动画关闭失败' '动画关闭异常必须记录日志且不能覆盖方案加载异常。'

$loadingMainShowIndex = $loadingCoordinatorSource.IndexOf('mainForm.Show();')
$loadingSplashCloseIndex = $loadingCoordinatorSource.IndexOf('splashController.Close();')
$loadingMainActivateIndex = $loadingCoordinatorSource.IndexOf('mainForm.Activate();')
$loadingMainBringToFrontIndex = $loadingCoordinatorSource.IndexOf('mainForm.BringToFront();')
Assert-True ($loadingMainShowIndex -ge 0 -and $loadingMainShowIndex -lt $loadingSplashCloseIndex) '方案加载结束时必须先显示主窗体再关闭动画。'
Assert-True ($loadingMainActivateIndex -gt $loadingSplashCloseIndex) '关闭方案动画后必须激活主窗体。'
Assert-True ($loadingMainBringToFrontIndex -gt $loadingMainActivateIndex) '方案加载恢复后必须将主窗体置前。'

$unsupervisedMethodIndex = $formMainSource.IndexOf('private void OpenUnsupervisedTrainForm()')
$unsupervisedFinallyIndex = $formMainSource.IndexOf('finally', $unsupervisedMethodIndex)
$unsupervisedClearIndex = $formMainSource.IndexOf('unsupervisedTrainForm = null;', $unsupervisedMethodIndex)
$largeModelMethodIndex = $formMainSource.IndexOf('private void OpenLargeModelTrainForm()')
$largeModelFinallyIndex = $formMainSource.IndexOf('finally', $largeModelMethodIndex)
$largeModelClearIndex = $formMainSource.IndexOf('largeModelTrainForm = null;', $largeModelMethodIndex)
Assert-True ($unsupervisedFinallyIndex -ge 0 -and $unsupervisedFinallyIndex -lt $unsupervisedClearIndex) '无监督训练窗口异常结束时也必须清空活动窗体字段。'
Assert-True ($largeModelFinallyIndex -ge 0 -and $largeModelFinallyIndex -lt $largeModelClearIndex) '大模型训练窗口异常结束时也必须清空活动窗体字段。'

Assert-Contains $projectSource 'Forms\Workspace\IExclusiveWorkspacePresenter.cs' '项目文件缺少独占工作区接口包含项。'
Assert-Contains $projectSource 'Forms\Workspace\ExclusiveWorkspacePresenter.cs' '项目文件缺少独占工作区实现包含项。'
Assert-Contains $projectSource 'Startup\ISolutionLoadingCoordinator.cs' '项目文件缺少方案动画加载接口包含项。'
Assert-Contains $projectSource 'Startup\SolutionLoadingCoordinator.cs' '项目文件缺少方案动画加载实现包含项。'

Write-Host '独占工作区与方案动画加载回归检查通过。'
