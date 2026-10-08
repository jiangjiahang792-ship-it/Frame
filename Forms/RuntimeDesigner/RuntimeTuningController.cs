using System;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>调参与学习操作的宿主接口；设计器不持有生产节点。</summary>
    public interface IRuntimeTuningController
    {
        /// <summary>当前身份和窗口状态是否允许使用调参入口。</summary>
        bool CanUse { get; }
        /// <summary>对显式绑定流程执行操作，宿主仍须检查流程有效性和权限。</summary>
        void Execute(IWin32Window owner, RuntimeWidgetKind kind, int? processId);
    }

    /// <summary>通过宿主委托复用原调参入口，不创建执行队列或学习实现。</summary>
    public sealed class RuntimeTuningController : IRuntimeTuningController
    {
        /// <summary>每次执行前重新读取权限，防止注销后的旧按钮点击。</summary>
        private readonly Func<bool> canUse;
        /// <summary>只操作指定流程的正式入口。</summary>
        private readonly Action<IWin32Window, RuntimeWidgetKind, int?> execute;
        /// <summary>初始化宿主策略和操作，不执行任何生产动作。</summary>
        public RuntimeTuningController(Func<bool> canUse, Action<IWin32Window, RuntimeWidgetKind, int?> execute)
        {
            this.canUse = canUse ?? throw new ArgumentNullException(nameof(canUse));
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
        /// <summary>轻量查询由宿主提供，不遍历节点。</summary>
        public bool CanUse => canUse();
        /// <summary>是否属于调参与学习控件。</summary>
        public static bool Supports(RuntimeWidgetKind kind) => kind == RuntimeWidgetKind.ManualParameters || kind == RuntimeWidgetKind.OneClickLearning;
        /// <summary>拒绝未绑定、错误类型和无权限操作后转交原框架入口。</summary>
        public void Execute(IWin32Window owner, RuntimeWidgetKind kind, int? processId)
        {
            if (!Supports(kind)) throw new InvalidOperationException("不支持的调参操作。");
            if (!processId.HasValue || processId.Value < 0) throw new InvalidOperationException("请先为按钮绑定指定流程。");
            if (!CanUse) throw new InvalidOperationException("当前没有调参权限或运行界面已失效，请检查登录状态及方案。");
            execute(owner, kind, processId);
        }
    }
}
