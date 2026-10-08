using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>设计画布的滚动宿主，获得编辑焦点时保持用户当前视口。</summary>
    public sealed class RuntimeDesignWorkspace : Panel
    {
        /// <summary>画布大于视口时不能因聚焦而滚回左上角，否则按下坐标与后续拖动坐标会错位。</summary>
        protected override Point ScrollToControl(Control activeControl)
        {
            return activeControl is RuntimeDesignCanvas ? DisplayRectangle.Location : base.ScrollToControl(activeControl);
        }
    }
}
