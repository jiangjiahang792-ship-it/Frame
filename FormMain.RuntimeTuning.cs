using System;
using System.Collections.Generic;
using TDJS_Vision.Forms.RuntimeDesigner;
using TDJS_Vision.Forms.SolRunParam;

namespace TDJS_Vision
{
    /// <summary>正式运行窗口的调参与学习宿主接线。</summary>
    public partial class FormMain
    {
        /// <summary>为正式窗口注入限定方案和根流程的调参入口，设计预览不使用此宿主。</summary>
        private IRuntimeTuningController CreateRuntimeTuningController(IEnumerable<Process> roots, Func<bool> isCurrent)
        {
            var service = new ProcessTuningService(roots, isCurrent);
            return new RuntimeTuningController(() => service.CanUse, (owner, kind, processId) =>
            {
                if (!RuntimeTuningController.Supports(kind))
                    throw new InvalidOperationException("不支持的调参操作。");
                service.Execute(owner, kind == RuntimeWidgetKind.OneClickLearning, processId);
            });
        }
    }
}
