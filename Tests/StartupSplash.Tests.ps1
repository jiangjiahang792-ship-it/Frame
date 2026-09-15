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
$programPath = Join-Path $projectRoot 'Program.cs'
$formMainPath = Join-Path $projectRoot 'FormMain.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$formStartupPath = Join-Path $projectRoot 'Forms\Startup\FormStartup.cs'
$formStartupDesignerPath = Join-Path $projectRoot 'Forms\Startup\FormStartup.Designer.cs'
$startupProgressInfoPath = Join-Path $projectRoot 'Startup\StartupProgressInfo.cs'
$startupProgressContextPath = Join-Path $projectRoot 'Startup\StartupProgressContext.cs'
$startupControllerPath = Join-Path $projectRoot 'Startup\StartupSplashController.cs'
$startupApplicationContextPath = Join-Path $projectRoot 'Startup\StartupApplicationContext.cs'
$resourcesPath = Join-Path $projectRoot 'Properties\Resources.resx'
$resourcesDesignerPath = Join-Path $projectRoot 'Properties\Resources.Designer.cs'
$mascotPath = Join-Path $projectRoot 'Resources\StartupMascot.png'

Assert-True (Test-Path -LiteralPath $startupProgressInfoPath) '缺少启动进度模型 StartupProgressInfo.cs。'
Assert-True (Test-Path -LiteralPath $startupProgressContextPath) '缺少启动进度上下文 StartupProgressContext.cs。'
Assert-True (Test-Path -LiteralPath $startupControllerPath) '缺少独立启动页线程控制器 StartupSplashController.cs。'
Assert-True (Test-Path -LiteralPath $startupApplicationContextPath) '缺少主程序启动上下文 StartupApplicationContext.cs。'
Assert-True (Test-Path -LiteralPath $formStartupPath) '缺少启动窗体 FormStartup.cs。'
Assert-True (Test-Path -LiteralPath $formStartupDesignerPath) '缺少启动窗体设计器 FormStartup.Designer.cs。'
Assert-True (Test-Path -LiteralPath $mascotPath) '缺少启动机器人资源 Resources\StartupMascot.png。'

$programSource = Get-Content -LiteralPath $programPath -Raw -Encoding UTF8
$formMainSource = Get-Content -LiteralPath $formMainPath -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
$formStartupSource = Get-Content -LiteralPath $formStartupPath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $formStartupDesignerPath -Raw -Encoding UTF8
$progressInfoSource = Get-Content -LiteralPath $startupProgressInfoPath -Raw -Encoding UTF8
$progressContextSource = Get-Content -LiteralPath $startupProgressContextPath -Raw -Encoding UTF8
$controllerSource = Get-Content -LiteralPath $startupControllerPath -Raw -Encoding UTF8
$applicationContextSource = Get-Content -LiteralPath $startupApplicationContextPath -Raw -Encoding UTF8
$resourcesSource = Get-Content -LiteralPath $resourcesPath -Raw -Encoding UTF8
$resourcesDesignerSource = Get-Content -LiteralPath $resourcesDesignerPath -Raw -Encoding UTF8

Assert-Contains $programSource 'new StartupApplicationContext(solutionPath)' 'Program 应创建启动上下文。'
Assert-Contains $programSource 'Application.Run(startupContext);' 'Program 应运行启动上下文消息循环。'
Assert-NotContains $programSource 'Application.Run(MainForm);' 'Program 不应在数据加载完成前直接运行主窗体。'
Assert-Contains $programSource 'Application.EnableVisualStyles();' 'WinForms 视觉样式必须在程序入口统一启用。'
Assert-Contains $programSource 'Application.SetCompatibleTextRenderingDefault(false);' '文本呈现默认值必须在创建第一个窗体前统一设置。'

Assert-Contains $progressInfoSource 'Math.Max(0, Math.Min(100, percentage))' '启动进度必须限制在 0 到 100。'
Assert-Contains $progressContextSource 'AsyncLocal<StartupProgressState>' '启动进度上下文必须隔离到当前异步启动链。'
Assert-Contains $progressContextSource 'ReportFailure' '启动进度上下文必须收集关键失败。'
Assert-Contains $progressContextSource 'ThrowIfFailures' '启动进度上下文必须在事件链完成后抛出关键失败。'

Assert-Contains $controllerSource 'SetApartmentState(ApartmentState.STA)' '启动页线程必须使用 STA。'
Assert-Contains $controllerSource 'Application.Run(startupForm)' '启动页线程必须拥有独立消息循环。'
Assert-NotContains $controllerSource 'Application.EnableVisualStyles();' '二次打开方案动画时不得在线程内重复启用视觉样式。'
Assert-NotContains $controllerSource 'Application.SetCompatibleTextRenderingDefault(false);' '创建主窗体后不得再次设置默认文本呈现方式。'
Assert-Contains $controllerSource 'startupForm.Shown += StartupForm_Shown;' '控制器必须等待启动页真正显示后再允许跨线程更新。'
Assert-Contains $controllerSource 'startupForm.FormClosed += StartupForm_FormClosed;' '启动页意外关闭时必须完成等待中的失败操作。'
Assert-Contains $controllerSource 'TaskCompletionSource<StartupFailureAction>' '错误操作必须通过可等待结果返回主线程。'
Assert-Contains $controllerSource 'startupThreadFailure' '启动页线程创建失败时必须将异常传回主启动线程。'
Assert-Contains $controllerSource 'private volatile bool closeRequested;' '启动页控制器必须记录跨线程关闭请求。'
Assert-Contains $controllerSource 'closeRequested = true;' '启动超时或关闭时必须请求动画线程退出。'
Assert-Contains $controllerSource 'if (closeRequested &&' '动画窗体延迟创建完成后必须处理此前的关闭请求。'
$disposeMethodIndex = $controllerSource.IndexOf('public void Dispose()')
$disposeThreadCheckIndex = $controllerSource.IndexOf('if (startupThread == null || !startupThread.IsAlive)', $disposeMethodIndex)
$disposeReadyIndex = $controllerSource.IndexOf('startupReady.Dispose();', $disposeMethodIndex)
Assert-True ($disposeThreadCheckIndex -ge 0 -and $disposeThreadCheckIndex -lt $disposeReadyIndex) '动画线程未确认退出前不得释放启动同步对象。'
$showFailureStartIndex = $controllerSource.IndexOf('public Task<StartupFailureAction> ShowFailureAsync')
$showFailureLockIndex = $controllerSource.IndexOf('lock (failureActionSync)', $showFailureStartIndex)
$showFailureClosedCheckIndex = $controllerSource.IndexOf('if (startupClosed)', $showFailureStartIndex)
Assert-True ($showFailureLockIndex -ge 0 -and $showFailureLockIndex -lt $showFailureClosedCheckIndex) '关闭状态检查和失败等待源创建必须由同一把锁保护。'
$formClosedStartIndex = $controllerSource.IndexOf('private void StartupForm_FormClosed')
$formClosedLockIndex = $controllerSource.IndexOf('lock (failureActionSync)', $formClosedStartIndex)
$formClosedAssignmentIndex = $controllerSource.IndexOf('startupClosed = true;', $formClosedStartIndex)
Assert-True ($formClosedLockIndex -ge 0 -and $formClosedLockIndex -lt $formClosedAssignmentIndex) '启动页关闭状态和失败等待源完成必须在同一把锁内切换。'

Assert-Contains $applicationContextSource 'TimeSpan.FromSeconds(2)' '启动页必须至少显示 2 秒。'
Assert-True ([regex]::IsMatch($applicationContextSource, 'if \(!StartupDisplayMode\.IsBackgroundAcceptance\)\s+splashController\.Start\(\);')) '普通启动必须创建启动动画，后台验收必须跳过不可见启动动画线程。'
Assert-True ([regex]::IsMatch($applicationContextSource, 'if \(!StartupDisplayMode\.IsBackgroundAcceptance\)\s+\{\s+TimeSpan remaining')) '普通启动必须保留最短动画时长，后台验收不得等待不可见动画。'
Assert-Contains $applicationContextSource 'ShowFailureAsync' '关键失败必须停留在启动页等待处理。'
Assert-Contains $applicationContextSource 'mainForm.Show();' '关键初始化完成后才能显示主窗体。'
Assert-Contains $applicationContextSource 'return $"软件启动初始化失败：{message}";' '启动失败提示必须包含简体中文说明。'
$showMainMethodIndex = $applicationContextSource.IndexOf('private void ShowMainForm()')
$mainFormShowIndex = $applicationContextSource.IndexOf('mainForm.Show();', $showMainMethodIndex)
$splashCloseIndex = $applicationContextSource.IndexOf('splashController.Close();', $showMainMethodIndex)
$mainFormActivateIndex = $applicationContextSource.IndexOf('mainForm.Activate();', $showMainMethodIndex)
$mainFormBringToFrontIndex = $applicationContextSource.IndexOf('mainForm.BringToFront();', $showMainMethodIndex)
Assert-True ($mainFormShowIndex -ge 0 -and $mainFormShowIndex -lt $splashCloseIndex) '必须先显示主窗体再关闭置顶启动页，避免前台焦点丢失。'
Assert-True ($mainFormActivateIndex -gt $splashCloseIndex) '关闭启动页后必须显式激活主窗体。'
Assert-True ($mainFormBringToFrontIndex -gt $mainFormActivateIndex) '激活主窗体后必须将其置于前台。'
$splashStartIndex = $applicationContextSource.IndexOf('splashController.Start();')
$stopwatchStartIndex = $applicationContextSource.IndexOf('startupStopwatch = Stopwatch.StartNew();')
Assert-True ($splashStartIndex -ge 0 -and $splashStartIndex -lt $stopwatchStartIndex) '最短展示时间必须从启动页真正显示后开始计算。'

Assert-Contains $formMainSource 'InitializeForStartupAsync' '主窗体应提供启动初始化入口。'
Assert-Contains $formMainSource '_startupStructureInitialized' '主窗体结构初始化必须幂等。'
Assert-Contains $formMainSource 'InitializeStartupStructure()' '主窗体应提取结构初始化方法。'
Assert-Contains $formMainSource 'LoadStartupSolution()' '主窗体应提取可重试的方案加载方法。'
Assert-Contains $formMainSource 'StartNonBlockingWarmups()' '主窗体应提取非阻塞后台预热方法。'
Assert-Contains $formMainSource 'Task.Run(() => { CSharpScriptEngine.Instance.WarmUp(); })' '脚本预热应保持后台执行。'
Assert-Contains $formMainSource 'WarmUpScriptEngineInBackgroundAsync' '脚本预热应通过独立后台方法处理异常。'
Assert-Contains $formMainSource '脚本引擎后台预热失败' '脚本预热失败必须写入日志。'
Assert-NotContains $formMainSource 'AutoLoadSolutionAsync();' '主窗体 Load 事件不应继续触发未等待的方案加载。'

$designerControls = @(
    'pictureBoxMascot',
    'labelTitle',
    'labelStatus',
    'labelDetail',
    'panelProgressTrack',
    'panelProgressValue',
    'panelError',
    'buttonRetry',
    'buttonExit',
    'timerAnimation'
)

foreach ($controlName in $designerControls) {
    Assert-Contains $designerSource ('this.' + $controlName) ('启动窗体设计器缺少控件：' + $controlName)
}

Assert-Contains $designerSource 'this.Controls.Add(this.panelMain);' '启动页主容器必须由设计器加入窗体控件树。'
Assert-Contains $designerSource 'this.buttonRetry.Text = "重试";' '重试按钮必须显示简体中文。'
Assert-Contains $designerSource 'this.buttonExit.Text = "退出软件";' '退出按钮必须显示简体中文。'
Assert-Contains $designerSource 'this.labelStatus.Text = "正在启动软件……";' '启动状态必须显示简体中文。'
Assert-Contains $designerSource 'global::TDJS_Vision.Properties.Resources.StartupMascot' '设计器必须使用项目内嵌机器人资源。'
Assert-Contains $formStartupSource 'UpdateProgress' '启动窗体必须支持实时进度更新。'
Assert-Contains $formStartupSource 'ShowFailure' '启动窗体必须显示关键失败状态。'
Assert-Contains $formStartupSource 'ConfigureAnimationOverlays();' '启动窗体必须初始化圆形天线呼吸灯和动画遮罩。'

$loaderContracts = @(
    @{ Path = 'ConfigHelper.cs'; Stage = '正在读取方案文件' },
    @{ Path = 'Forms\LightAdd\FrmLightListView.cs'; Stage = '正在恢复光源设备' },
    @{ Path = 'Forms\CameraAdd\FrmCameraListView.cs'; Stage = '正在恢复相机设备' },
    @{ Path = 'Forms\PLCAdd\FrmPLCListView.cs'; Stage = '正在恢复PLC设备' },
    @{ Path = 'Forms\ModbusAdd\FrmModbusListView.cs'; Stage = '正在恢复Modbus设备' },
    @{ Path = 'Forms\TCPAdd\FrmTCPListView.cs'; Stage = '正在恢复TCP设备' },
    @{ Path = 'Forms\COMAdd\FrmCOMListView.cs'; Stage = '正在恢复串口设备' },
    @{ Path = 'Forms\ProcessNew\FormNewProcessWizard.cs'; Stage = '正在恢复检测流程' }
)

foreach ($contract in $loaderContracts) {
    $sourcePath = Join-Path $projectRoot $contract.Path
    $source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
    Assert-Contains $source 'StartupProgressContext.ReportItem' ($contract.Path + ' 应上报当前加载对象。')
    Assert-Contains $source $contract.Stage ($contract.Path + ' 缺少中文加载阶段名称。')
    if ($contract.Path -ne 'ConfigHelper.cs') {
        Assert-Contains $source 'StartupProgressContext.ReportFailure' ($contract.Path + ' 应收集启动关键失败。')
    }
}

$configSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ConfigHelper.cs') -Raw -Encoding UTF8
$nullCheckIndex = $configSource.IndexOf('if (SolConfig == null)')
$solutionNameAssignmentIndex = $configSource.LastIndexOf('SolConfig.SolName = solFile;')
Assert-True ($nullCheckIndex -ge 0 -and $nullCheckIndex -lt $solutionNameAssignmentIndex) '方案反序列化结果必须先判空，再访问方案属性。'

$processEditSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Forms\ProcessNew\ProcessEditPanel.cs') -Raw -Encoding UTF8
Assert-Contains $processEditSource 'StartupProgressContext.ReportItem(' '流程节点恢复必须上报当前节点和总进度。'
Assert-Contains $processEditSource '正在恢复流程节点' '流程节点进度必须显示简体中文阶段名称。'
Assert-Contains $processEditSource 'StartupProgressContext.ReportFailure("恢复流程节点"' '流程节点恢复异常必须传回启动页。'
Assert-Contains $processEditSource 'StartupProgressContext.IsActive' '流程节点失败时必须区分启动期与普通运行期交互。'

$projectItems = @(
    'Startup\IStartupProgressReporter.cs',
    'Startup\StartupProgressInfo.cs',
    'Startup\StartupProgressContext.cs',
    'Startup\StartupSplashController.cs',
    'Startup\StartupApplicationContext.cs',
    'Forms\Startup\FormStartup.cs',
    'Forms\Startup\FormStartup.Designer.cs',
    'Forms\Startup\FormStartup.resx',
    'Resources\StartupMascot.png'
)

foreach ($projectItem in $projectItems) {
    Assert-Contains $projectSource $projectItem ('项目文件缺少启动动画包含项：' + $projectItem)
}

Assert-Contains $resourcesSource 'StartupMascot' 'Resources.resx 应注册启动机器人资源。'
Assert-Contains $resourcesDesignerSource 'internal static System.Drawing.Bitmap StartupMascot' 'Resources.Designer.cs 应公开启动机器人位图。'

Write-Host '启动动画回归检查通过。'
