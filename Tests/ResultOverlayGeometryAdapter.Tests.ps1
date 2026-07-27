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

$projectRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}

$adapterPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw2\OverlayGeometryAdapterRegistry.cs'
Assert-True (Test-Path -LiteralPath $adapterPath) 'ROI几何适配器文件尚未创建。'

$adapterSource = Get-Content -LiteralPath $adapterPath -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8

Assert-ContainsText $adapterSource 'interface IOverlayGeometryAdapter' '缺少ROI几何适配器接口。'
Assert-ContainsText $adapterSource 'class OverlayGeometryRenderContext' '缺少ROI绘制上下文。'
Assert-ContainsText $adapterSource 'class OverlayGeometryAdapterRegistry' '缺少ROI几何适配器注册表。'
Assert-ContainsText $adapterSource 'ConcurrentDictionary<Type' 'ROI适配器分派结果必须按类型缓存。'
Assert-ContainsText $adapterSource 'source.Rects' '算法结果适配器必须复制矩形。'
Assert-ContainsText $adapterSource 'source.Lines' '算法结果适配器必须复制线段。'
Assert-ContainsText $adapterSource 'source.Circles' '算法结果适配器必须复制圆。'
Assert-ContainsText $adapterSource 'source.Arcs' '算法结果适配器必须复制圆弧。'
Assert-ContainsText $adapterSource 'source.Ellipses' '算法结果适配器必须复制椭圆。'
Assert-ContainsText $adapterSource 'source.Contours' '算法结果适配器必须复制轮廓。'
Assert-True (-not $adapterSource.Contains('foreach (ColorText text in source.Texts)')) 'ROI适配器不能复制算法结果文本。'
Assert-True (-not $adapterSource.Contains('return new AlgorithmResultGeometryAdapter().TryAppend')) '测量结果转发不能在每帧为每个ROI重复创建算法结果适配器。'
Assert-ContainsText $projectSource 'ResultOverlayDraw2\OverlayGeometryAdapterRegistry.cs' '项目文件必须编译ROI几何适配器。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) '找不到Debug程序，无法执行ROI适配器行为检查。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$registryType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.OverlayGeometryAdapterRegistry', $true)
$contextType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.OverlayGeometryRenderContext', $true)
$algorithmType = $assembly.GetType('TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult', $true)
$lineType = $assembly.GetType('TDJS_Vision.Node._3_Detection.TDAI.ColorLine', $true)
$textType = $assembly.GetType('TDJS_Vision.Node._3_Detection.TDAI.ColorText', $true)
$categoryType = $assembly.GetType('TDJS_Vision.Node.SubscriptionDataCategory', $true)

$source = [Activator]::CreateInstance($algorithmType)
$target = [Activator]::CreateInstance($algorithmType)
$line = [Activator]::CreateInstance(
    $lineType,
    @([Drawing.PointF]::new(1, 2), [Drawing.PointF]::new(3, 4), [Drawing.Color]::Blue))
$text = [Activator]::CreateInstance($textType, @('不应复制', [Drawing.Color]::Yellow))
$source.Lines.Add($line)
$source.Texts.Add($text)

$context = [Activator]::CreateInstance($contextType, $true)
$contextType.GetProperty('FallbackColor').SetValue($context, [Drawing.Color]::Green, $null)
$contextType.GetProperty('LineWidth').SetValue($context, [single]3, $null)
$category = [Enum]::Parse($categoryType, 'AlgorithmResult')
$method = $registryType.GetMethod('TryAppend', [Reflection.BindingFlags]'Static, NonPublic')
Assert-True ($null -ne $method) '找不到ROI几何适配器统一入口。'

$arguments = @($target, $source, $null, $category, $context, 0)
$handled = [bool]$method.Invoke($null, $arguments)
Assert-True $handled '算法结果适配器必须接受AlgorithmResult。'
Assert-True ($target.Lines.Count -eq 1) '算法结果中的线段必须被复制。'
Assert-True ($target.Texts.Count -eq 0) 'ROI适配器不得复制算法结果中的文本。'
Assert-True ([int]$arguments[5] -eq 1) '新增几何数量必须准确返回。'
Assert-True ($target.Lines[0].Color.ToArgb() -eq [Drawing.Color]::Blue.ToArgb()) '未配置颜色判定时必须保留来源几何颜色。'

$overrideTarget = [Activator]::CreateInstance($algorithmType)
$overrideContext = [Activator]::CreateInstance($contextType, $true)
$contextType.GetProperty('FallbackColor').SetValue($overrideContext, [Drawing.Color]::Green, $null)
$contextType.GetProperty('OverrideColor').SetValue($overrideContext, [Drawing.Color]::Red, $null)
$contextType.GetProperty('LineWidth').SetValue($overrideContext, [single]3, $null)
$overrideArguments = @($overrideTarget, $source, $null, $category, $overrideContext, 0)
$overrideHandled = [bool]$method.Invoke($null, $overrideArguments)
Assert-True $overrideHandled '配置颜色判定后仍必须接受AlgorithmResult。'
Assert-True ($overrideTarget.Lines[0].Color.ToArgb() -eq [Drawing.Color]::Red.ToArgb()) '配置颜色判定后必须统一覆盖来源几何颜色。'

$pointCollectionHarness = @'
using System;
using System.Collections;
using System.Drawing;
using System.Reflection;

/// <summary>
/// 从纯C#调用内部适配器，避免PowerShell反射绑定器展开IEnumerable参数。
/// </summary>
public static class ResultOverlayPointCollectionHarness
{
    /// <summary>
    /// 返回“是否识别、绘制线数、新增点数”三个验证值。
    /// </summary>
    public static int[] Run(Assembly assembly)
    {
        Type registryType = assembly.GetType(
            "TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.OverlayGeometryAdapterRegistry",
            true);
        Type contextType = assembly.GetType(
            "TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.OverlayGeometryRenderContext",
            true);
        Type algorithmType = assembly.GetType(
            "TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult",
            true);
        Type categoryType = assembly.GetType("TDJS_Vision.Node.SubscriptionDataCategory", true);

        object target = Activator.CreateInstance(algorithmType);
        object context = Activator.CreateInstance(contextType, true);
        IList points = new ArrayList
        {
            new PointF(12F, 18F),
            new PointF(20F, 24F)
        };
        object category = Enum.Parse(categoryType, "PointCollection");
        MethodInfo method = registryType.GetMethod(
            "TryAppend",
            BindingFlags.Static | BindingFlags.NonPublic);
        object[] arguments = { target, points, null, category, context, 0 };
        bool handled = (bool)method.Invoke(null, arguments);
        IList lines = (IList)algorithmType.GetProperty("Lines").GetValue(target, null);
        return new[] { handled ? 1 : 0, lines.Count, (int)arguments[5] };
    }
}
'@
Add-Type -TypeDefinition $pointCollectionHarness -Language CSharp -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Drawing.dll')
$pointResult = [ResultOverlayPointCollectionHarness]::Run($assembly)
Assert-True ($pointResult[0] -eq 1) '自动ROI必须支持点集合。'
Assert-True ($pointResult[1] -eq 4) '两个点必须生成四条十字标记线。'
Assert-True ($pointResult[2] -eq 2) '点集合新增逻辑几何数量必须按点计数。'

$rectangleTarget = [Activator]::CreateInstance($algorithmType)
$rectangleArguments = @(
    $rectangleTarget,
    [Drawing.Rectangle]::new(10, 20, 30, 40),
    $null,
    [Enum]::Parse($categoryType, 'Rectangle'),
    $context,
    0)
$rectangleHandled = [bool]$method.Invoke($null, $rectangleArguments)
Assert-True $rectangleHandled '自动ROI必须支持直接订阅矩形对象。'
Assert-True ($rectangleTarget.Rects.Count -eq 1) '直接矩形对象必须生成一个矩形ROI。'

$unsupportedArguments = @($target, (New-Object object), $null, [Enum]::Parse($categoryType, 'Circle'), $context, 0)
$unsupportedHandled = [bool]$method.Invoke($null, $unsupportedArguments)
Assert-True (-not $unsupportedHandled) '暂不支持的几何CLR类型必须平稳返回false，不能抛出缓存异常。'

Write-Host 'ROI几何适配器检查通过。'
