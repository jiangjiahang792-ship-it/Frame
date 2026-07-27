$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$resolverPath = Join-Path $root "Node\DynamicResultVariable.cs"
$nodeBasePath = Join-Path $root "Node\NodeBase.cs"
$subscriptionPath = Join-Path $root "Node\NodeSubscription.cs"
$catalogPath = Join-Path $root "Node\SubscriptionPortCatalog.cs"
$canvasPath = Join-Path $root "Forms\ProcessNew\ProcessFlowCanvas.cs"
$arithmeticPath = Join-Path $root "Node\6-LogicTool\ArithmeticOperation\NodeArithmeticOperation.cs"
$compositeInputPath = Join-Path $root "Node\6-LogicTool\CompositeModule\NodeCompositeInput.cs"
$compositeOutputPath = Join-Path $root "Node\6-LogicTool\CompositeModule\NodeCompositeOutput.cs"
$compositeModulePath = Join-Path $root "Node\6-LogicTool\CompositeModule\NodeCompositeModule.cs"

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

function Assert-MethodDoesNotContainRuntimeResult {
    param([string]$Content, [string]$ResultType, [string]$Message)
    $start = $Content.IndexOf('public IEnumerable<string> GetDynamicResultVariableNames()')
    if ($start -lt 0) {
        throw '动态输出节点缺少配置变量提供方法。'
    }
    $end = $Content.IndexOf('public bool TryGetDynamicResultVariableType', $start)
    if ($end -lt 0) {
        $end = [Math]::Min($Content.Length, $start + 4000)
    }
    $methodText = $Content.Substring($start, $end - $start)
    if ($methodText -like "*$ResultType*") {
        throw $Message
    }
}

$resolver = Get-Content -Raw -Encoding UTF8 $resolverPath
$nodeBase = Get-Content -Raw -Encoding UTF8 $nodeBasePath
$subscription = Get-Content -Raw -Encoding UTF8 $subscriptionPath
$catalog = Get-Content -Raw -Encoding UTF8 $catalogPath
$canvas = Get-Content -Raw -Encoding UTF8 $canvasPath
$arithmetic = Get-Content -Raw -Encoding UTF8 $arithmeticPath
$compositeInput = Get-Content -Raw -Encoding UTF8 $compositeInputPath
$compositeOutput = Get-Content -Raw -Encoding UTF8 $compositeOutputPath
$compositeModule = Get-Content -Raw -Encoding UTF8 $compositeModulePath

Assert-Contains $resolver 'if (provider != null)' '动态变量解析器没有识别参数定义提供者。'
Assert-Contains $resolver 'return NormalizeNames(provider.GetDynamicResultVariableNames());' '参数定义存在时仍会与上一次运行结果合并。'
Assert-Contains $nodeBase 'OutputDefinitionChanged' '节点缺少输出定义变化通知。'
Assert-Contains $nodeBase 'NotifyOutputDefinitionChanged' '节点缺少统一的输出定义刷新入口。'
Assert-Contains $canvas 'node.NotifyOutputDefinitionChanged();' '参数窗口关闭后没有立即通知下游刷新订阅列表。'
Assert-Contains $subscription 'NodeBase.OutputDefinitionChanged +=' '普通订阅控件没有监听输出定义变化。'
Assert-Contains $subscription 'SubscriptionPortCatalog.GetOutputs' '普通订阅控件没有通过统一目录读取动态输出。'
Assert-Contains $catalog 'DynamicResultVariableResolver.GetDescriptors(node)' '统一目录没有读取配置阶段动态输出描述。'
Assert-Contains $resolver 'Type valueType = GetVariableValueType(node, variableName);' '动态输出描述没有使用参数提供的真实类型。'
Assert-MethodDoesNotContainRuntimeResult $arithmetic 'NodeResultArithmeticOperation' '四则运算配置变量仍混入上一次运行结果。'
Assert-MethodDoesNotContainRuntimeResult $compositeInput 'NodeResultCompositeInput' '组合输入配置变量仍混入上一次运行结果。'
Assert-MethodDoesNotContainRuntimeResult $compositeOutput 'NodeResultCompositeOutput' '组合输出配置变量仍混入上一次运行结果。'
Assert-MethodDoesNotContainRuntimeResult $compositeModule 'NodeResultCompositeModule' '组合模块配置变量仍混入上一次运行结果。'

Write-Host "动态输出变量即时刷新检查通过。"
