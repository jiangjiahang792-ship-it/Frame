using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    /// <summary>
    /// 规定所有多目标测量结果项必须提供的目标、状态和错误信息。
    /// </summary>
    public interface IMultiTargetMeasurementItem
    {
        /// <summary>
        /// 获取或设置从一开始的模板目标序号。
        /// </summary>
        int TargetIndex { get; set; }

        /// <summary>
        /// 获取或设置当前目标测量是否成功。
        /// </summary>
        bool IsOk { get; set; }

        /// <summary>
        /// 获取或设置当前目标失败时的简体中文原因。
        /// </summary>
        string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置当前目标使用的位置修正信息。
        /// </summary>
        PositionCorrectionInfo Correction { get; set; }
    }

    /// <summary>
    /// 为各测量工具的目标结果项提供统一公共字段。
    /// </summary>
    public abstract class MultiTargetMeasurementItemBase : IMultiTargetMeasurementItem
    {
        /// <summary>
        /// 初始化公共测量结果项。
        /// </summary>
        protected MultiTargetMeasurementItemBase()
        {
            ErrorMessage = string.Empty;
        }

        /// <summary>
        /// 获取或设置从一开始的模板目标序号。
        /// </summary>
        public int TargetIndex { get; set; }

        /// <summary>
        /// 获取或设置当前目标测量是否成功。
        /// </summary>
        public bool IsOk { get; set; }

        /// <summary>
        /// 获取或设置当前目标失败时的简体中文原因。
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置当前目标使用的位置修正信息。
        /// </summary>
        public PositionCorrectionInfo Correction { get; set; }
    }

    /// <summary>
    /// 按模板目标顺序运行单目标测量，并把普通失败隔离在当前目标结果项中。
    /// </summary>
    public static class MultiTargetMeasurementRunner
    {
        /// <summary>
        /// 公共执行器首次使用时读取并缓存的自动并发上限；逻辑处理器数量在程序运行期间保持稳定，无需逐轮读取。
        /// </summary>
        private static readonly int AutomaticMaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);

        /// <summary>
        /// 逐目标执行测量；普通异常生成失败项，取消异常立即向上抛出。
        /// </summary>
        /// <typeparam name="TItem">具体测量工具的目标结果项类型。</typeparam>
        /// <param name="corrections">按模板目标顺序排列的位置修正集合。</param>
        /// <param name="token">用于立即终止后续目标测量的取消令牌。</param>
        /// <param name="execute">执行单个目标测量的委托。</param>
        /// <param name="createFailure">创建保留列表位置的失败结果项委托。</param>
        /// <returns>数量和顺序均与输入修正集合一致的测量结果项。</returns>
        public static List<TItem> Run<TItem>(
            IReadOnlyList<PositionCorrectionInfo> corrections,
            CancellationToken token,
            Func<PositionCorrectionInfo, TItem> execute,
            Func<PositionCorrectionInfo, Exception, TItem> createFailure)
            where TItem : IMultiTargetMeasurementItem
        {
            ValidateArguments(corrections, execute, createFailure);

            var items = new List<TItem>(corrections.Count);
            for (int i = 0; i < corrections.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                items.Add(ExecuteTarget(corrections[i], i, execute, createFailure));
            }

            return items;
        }

        /// <summary>
        /// 使用受限并发逐目标执行测量，并在全部目标完成后按输入位置统一返回结果。
        /// </summary>
        /// <typeparam name="TItem">具体测量工具的目标结果项类型。</typeparam>
        /// <param name="corrections">按模板目标顺序排列的位置修正集合。</param>
        /// <param name="token">用于取消尚未开始或正在调度的目标测量的令牌。</param>
        /// <param name="execute">执行单个目标测量的委托。</param>
        /// <param name="createFailure">创建保留列表位置的失败结果项委托。</param>
        /// <param name="maxDegreeOfParallelism">
        /// 最大并发度；小于等于零时使用公共执行器首次加载时根据 <see cref="Environment.ProcessorCount"/>
        /// 缓存的自动上限，默认保留一个逻辑处理器供界面、相机和其他流程使用，并且并发度不会超过目标数量。
        /// </param>
        /// <returns>数量和顺序均与输入修正集合一致的测量结果项。</returns>
        public static List<TItem> RunParallel<TItem>(
            IReadOnlyList<PositionCorrectionInfo> corrections,
            CancellationToken token,
            Func<PositionCorrectionInfo, TItem> execute,
            Func<PositionCorrectionInfo, Exception, TItem> createFailure,
            int maxDegreeOfParallelism = 0)
            where TItem : IMultiTargetMeasurementItem
        {
            ValidateArguments(corrections, execute, createFailure);
            token.ThrowIfCancellationRequested();

            // 单目标直接走原串行热路径，避免并发调度成本破坏亚毫秒级测量性能。
            if (corrections.Count <= 1)
                return Run(corrections, token, execute, createFailure);

            int requestedDegree = maxDegreeOfParallelism > 0
                ? maxDegreeOfParallelism
                : AutomaticMaxDegreeOfParallelism;
            int degree = Math.Min(corrections.Count, Math.Max(1, requestedDegree));
            if (degree <= 1)
                return Run(corrections, token, execute, createFailure);

            // 固定下标写入保证并发完成顺序不会改变最终目标顺序。
            var items = new TItem[corrections.Count];
            var options = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = degree
            };

            Parallel.For(0, corrections.Count, options, index =>
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                items[index] = ExecuteTarget(corrections[index], index, execute, createFailure);
            });

            return new List<TItem>(items);
        }

        /// <summary>
        /// 校验公共执行入口的必需参数。
        /// </summary>
        /// <typeparam name="TItem">具体测量工具的目标结果项类型。</typeparam>
        /// <param name="corrections">位置修正集合。</param>
        /// <param name="execute">单目标执行委托。</param>
        /// <param name="createFailure">单目标失败结果工厂。</param>
        private static void ValidateArguments<TItem>(
            IReadOnlyList<PositionCorrectionInfo> corrections,
            Func<PositionCorrectionInfo, TItem> execute,
            Func<PositionCorrectionInfo, Exception, TItem> createFailure)
            where TItem : IMultiTargetMeasurementItem
        {
            if (corrections == null)
                throw new ArgumentNullException("corrections");
            if (execute == null)
                throw new ArgumentNullException("execute");
            if (createFailure == null)
                throw new ArgumentNullException("createFailure");
        }

        /// <summary>
        /// 执行一个目标，隔离普通异常，并统一回填目标序号和位置修正信息。
        /// </summary>
        /// <typeparam name="TItem">具体测量工具的目标结果项类型。</typeparam>
        /// <param name="correction">当前目标的位置修正信息。</param>
        /// <param name="index">当前目标在输入集合中的下标。</param>
        /// <param name="execute">单目标执行委托。</param>
        /// <param name="createFailure">单目标失败结果工厂。</param>
        /// <returns>当前目标对应的成功或失败结果项。</returns>
        private static TItem ExecuteTarget<TItem>(
            PositionCorrectionInfo correction,
            int index,
            Func<PositionCorrectionInfo, TItem> execute,
            Func<PositionCorrectionInfo, Exception, TItem> createFailure)
            where TItem : IMultiTargetMeasurementItem
        {
            TItem item;
            try
            {
                item = execute(correction);
                if (item == null)
                    throw new InvalidOperationException("单目标测量没有返回结果项。");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                item = createFailure(correction, ex);
                if (item == null)
                    throw new InvalidOperationException("单目标失败工厂没有返回结果项。", ex);
            }

            item.TargetIndex = correction != null && correction.TargetIndex > 0
                ? correction.TargetIndex
                : index + 1;
            item.Correction = correction;
            return item;
        }
    }
}
