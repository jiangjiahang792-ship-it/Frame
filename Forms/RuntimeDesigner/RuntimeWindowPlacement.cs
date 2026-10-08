using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>由宿主提供的显示器身份与可用工作区，不依赖窗口或设备。</summary>
    public sealed class RuntimeDisplayInfo
    {
        /// <summary>系统显示器名称。</summary>
        public string DeviceName { get; set; }
        /// <summary>排除任务栏后的桌面工作区，允许负坐标。</summary>
        public Rectangle WorkingArea { get; set; }
    }

    /// <summary>仅保存在本机配置中的运行窗口位置，不属于方案或界面模板。</summary>
    public sealed class RuntimeWindowPlacement
    {
        /// <summary>上次所在显示器的系统名称。</summary>
        public string DeviceName { get; set; }
        /// <summary>记录位置时的显示器工作区。</summary>
        public Rectangle WorkingArea { get; set; }
        /// <summary>非最大化状态的桌面窗口矩形。</summary>
        public Rectangle NormalBounds { get; set; }
        /// <summary>恢复时是否使用最大化状态。</summary>
        public bool Maximized { get; set; }
        /// <summary>复制位置快照，调用方修改不会影响原对象。</summary>
        public RuntimeWindowPlacement Copy()
        {
            return new RuntimeWindowPlacement { DeviceName = DeviceName, WorkingArea = WorkingArea,
                NormalBounds = NormalBounds, Maximized = Maximized };
        }
    }

    /// <summary>根据当前显示器布局恢复安全位置，不读取配置或操作窗口。</summary>
    public static class RuntimeWindowPlacementGeometry
    {
        /// <summary>恢复新快照；无有效显示器时拒绝恢复，交由宿主使用系统默认行为。</summary>
        public static RuntimeWindowPlacement Resolve(RuntimeWindowPlacement saved,
            IReadOnlyList<RuntimeDisplayInfo> displays, string fallbackDeviceName, Size minimumSize)
        {
            RuntimeDisplayInfo[] available = (displays ?? new RuntimeDisplayInfo[0])
                .Where(display => display != null && !string.IsNullOrWhiteSpace(display.DeviceName) &&
                    IsValidRectangle(display.WorkingArea)).ToArray();
            if (available.Length == 0) throw new ArgumentException("没有可用的显示器工作区。", nameof(displays));
            RuntimeDisplayInfo target = available.FirstOrDefault(display =>
                string.Equals(display.DeviceName, saved?.DeviceName, StringComparison.OrdinalIgnoreCase)) ??
                available.FirstOrDefault(display =>
                    string.Equals(display.DeviceName, fallbackDeviceName, StringComparison.OrdinalIgnoreCase)) ?? available[0];
            Rectangle area = target.WorkingArea;
            double width = area.Width * 0.8, height = area.Height * 0.8;
            double localX = (area.Width - width) / 2, localY = (area.Height - height) / 2;
            bool hasSavedBounds = saved != null && IsValidRectangle(saved.WorkingArea) && IsValidRectangle(saved.NormalBounds);
            if (hasSavedBounds)
            {
                // 先转为局部坐标再缩放，避免负屏坐标和大整数加减溢出。
                double scaleX = (double)area.Width / saved.WorkingArea.Width;
                double scaleY = (double)area.Height / saved.WorkingArea.Height;
                width = saved.NormalBounds.Width * scaleX;
                height = saved.NormalBounds.Height * scaleY;
                localX = ((double)saved.NormalBounds.X - saved.WorkingArea.X) * scaleX;
                localY = ((double)saved.NormalBounds.Y - saved.WorkingArea.Y) * scaleY;
            }
            int safeWidth = ClampRounded(width, Math.Min(area.Width, Math.Max(1, minimumSize.Width)), area.Width);
            int safeHeight = ClampRounded(height, Math.Min(area.Height, Math.Max(1, minimumSize.Height)), area.Height);
            if (!hasSavedBounds)
            {
                localX = (area.Width - safeWidth) / 2.0;
                localY = (area.Height - safeHeight) / 2.0;
            }
            int x = area.X + ClampRounded(localX, 0, area.Width - safeWidth);
            int y = area.Y + ClampRounded(localY, 0, area.Height - safeHeight);
            return new RuntimeWindowPlacement { DeviceName = target.DeviceName, WorkingArea = area,
                NormalBounds = new Rectangle(x, y, safeWidth, safeHeight), Maximized = saved?.Maximized ?? true };
        }

        /// <summary>检查正尺寸及可表示的矩形右下边界，不使用可能溢出的矩形属性。</summary>
        internal static bool IsValidRectangle(Rectangle value)
        {
            return value.Width > 0 && value.Height > 0 &&
                (long)value.X + value.Width <= int.MaxValue && (long)value.Y + value.Height <= int.MaxValue;
        }

        /// <summary>先按有效边界夹取再四舍五入，转换整数时不会溢出。</summary>
        private static int ClampRounded(double value, int minimum, int maximum)
        {
            return (int)Math.Round(Math.Max(minimum, Math.Min(maximum, value)), MidpointRounding.AwayFromZero);
        }
    }
}
