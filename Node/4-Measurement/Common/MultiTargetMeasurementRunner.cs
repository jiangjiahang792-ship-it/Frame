using System;
using System.Collections.Generic;
using System.Threading;

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
            if (corrections == null)
                throw new ArgumentNullException("corrections");
            if (execute == null)
                throw new ArgumentNullException("execute");
            if (createFailure == null)
                throw new ArgumentNullException("createFailure");

            var items = new List<TItem>(corrections.Count);
            for (int i = 0; i < corrections.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                PositionCorrectionInfo correction = corrections[i];
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
                    : i + 1;
                item.Correction = correction;
                items.Add(item);
            }

            return items;
        }
    }
}
