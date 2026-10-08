using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;

/// <summary>验证真实条件比较的兼容性及文本转换不抛异常。</summary>
internal static class MultiConditionTextConversionTests
{
    /// <summary>生产程序集中的比较入口。</summary>
    private static readonly Func<object, MultiConditionItem, bool> Compare =
        (Func<object, MultiConditionItem, bool>)Delegate.CreateDelegate(
            typeof(Func<object, MultiConditionItem, bool>),
            typeof(NodeParamMultiCondition).Assembly.GetType(
                "TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionEvaluator", true)
                .GetMethod("CompareCondition", BindingFlags.Static | BindingFlags.NonPublic));

    /// <summary>比较路径抛出的首次机会转换异常数量。</summary>
    private static int _conversionExceptions;

    /// <summary>记录被生产代码捕获但仍会通知调试器的转换异常。</summary>
    private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs args)
    {
        if (args.Exception is FormatException || args.Exception is OverflowException)
            _conversionExceptions++;
    }

    /// <summary>断言实际条件结果，覆盖文本和原有类型转换规则。</summary>
    private static void Check(object actual, MultiConditionOperator op, string expected, bool matched)
    {
        var condition = new MultiConditionItem { Operator = op, Value1 = expected };
        if (Compare(actual, condition) != matched)
            throw new InvalidOperationException("条件结果不符：" + actual + " / " + op + " / " + expected);
    }

    /// <summary>执行行为回归，并用异常计数检查性能问题是否消除。</summary>
    private static int Main()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
            foreach (string culture in new[] { "zh-CN", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Check("1.5DS-QEHBE-F", MultiConditionOperator.Equals, "1.5DS-QEHBE-F", true);
                Check("1.5DS-QEHBE-F", MultiConditionOperator.Equals, "1.5DS-QEHBE-M", false);
                Check("1.5ds-qehbe-f", MultiConditionOperator.Equals, "1.5DS-QEHBE-F", true);
                Check("1.5DS-QEHBE-F", MultiConditionOperator.NotEquals, "1.5DS-QEHBE-M", true);
                Check("1.5DS-QEHBE-F", MultiConditionOperator.Contains, "QEHBE", true);
                Check("", MultiConditionOperator.Equals, "", true);
                Check(null, MultiConditionOperator.IsNull, "", true);
                Check(null, MultiConditionOperator.Equals, "", false);
                Check("0012", MultiConditionOperator.Equals, "12", true);
                Check("1e3", MultiConditionOperator.Equals, "1000", true);
                Check("12.5", MultiConditionOperator.GreaterThan, "12", true);
                Check((short)12, MultiConditionOperator.Equals, "12", true);
                Check(12.5M, MultiConditionOperator.LessThan, "13", true);
                Check(true, MultiConditionOperator.Equals, "YES", true);
                Check(false, MultiConditionOperator.Equals, "NG", true);
                Check("OK", MultiConditionOperator.IsTrue, "", true);
                Check("NG", MultiConditionOperator.IsFalse, "", true);
                Check("2026-09-27", MultiConditionOperator.GreaterThan, "2026-09-26", true);
                Check("79228162514264337593543950336", MultiConditionOperator.Equals,
                    "79228162514264337593543950336", true);
            }

            var condition = new MultiConditionItem {
                Operator = MultiConditionOperator.Equals, Value1 = "1.5DS-QEHBE-F" };
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
                if (!Compare("1.5DS-QEHBE-F", condition))
                    throw new InvalidOperationException("重复比较结果不一致。");
            stopwatch.Stop();
            Console.WriteLine("10000次工艺名比较：{0:F3} ms；转换异常：{1}",
                stopwatch.Elapsed.TotalMilliseconds, _conversionExceptions);
            if (_conversionExceptions != 0)
                throw new InvalidOperationException("普通文本比较不应依赖转换异常。");
            Console.WriteLine("多条件文本转换回归通过。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
