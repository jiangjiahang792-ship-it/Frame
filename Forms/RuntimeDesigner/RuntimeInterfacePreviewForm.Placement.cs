using System;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>正式运行窗口的本机位置配置；普通设计预览不接入。</summary>
    public partial class RuntimeInterfacePreviewForm
    {
        /// <summary>只属于当前窗口的本机位置生命周期连接。</summary>
        private RuntimeWindowPlacementBinding placementBinding;
        /// <summary>由正式宿主记录配置异常，不能影响检测或关闭。</summary>
        public event Action<string> PlacementWarning;

        /// <summary>以方案路径和页面编号隔离窗口记录；未保存的方案不创建持久化配置。</summary>
        public void AttachLocalPlacement(string solutionPath, string pageId, IRuntimeWindowPlacementStore store = null)
        {
            placementBinding?.Dispose();
            placementBinding = null;
            if (string.IsNullOrWhiteSpace(solutionPath) || string.IsNullOrWhiteSpace(pageId)) return;
            placementBinding = new RuntimeWindowPlacementBinding(this, store ?? JsonRuntimeWindowPlacementStore.CreateDefault(),
                solutionPath, pageId, message => PlacementWarning?.Invoke(message));
        }
    }
}
