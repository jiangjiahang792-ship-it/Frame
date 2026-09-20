using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.Common
{
    /// <summary>
    /// 窗体图标统一提供器，所有窗口都从这里读取主程序图标，避免各窗体私有资源不一致。
    /// </summary>
    internal static class WindowIconProvider
    {
        /// <summary>
        /// 基于统一资源创建新的窗体图标实例，调用方可以交给窗体生命周期释放。
        /// </summary>
        /// <returns>使用主程序图标资源创建的Icon。</returns>
        public static Icon CreateWindowIcon()
        {
            using (var stream = new MemoryStream(Properties.Resources.TDJSIco))
            {
                using (var icon = new Icon(stream))
                {
                    return (Icon)icon.Clone();
                }
            }
        }

        /// <summary>
        /// 将统一窗体图标应用到指定窗口。
        /// </summary>
        /// <param name="form">需要更新图标的窗体。</param>
        public static void Apply(Form form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));
            form.Icon = CreateWindowIcon();
        }
    }
}
