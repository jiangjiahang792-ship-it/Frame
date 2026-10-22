using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace TDJS_Vision
{
    /// <summary>磁盘容量查询接口，便于替换存储来源。</summary>
    internal interface IDiskCapacityReader
    {
        /// <summary>读取容量快照。</summary>
        DiskCapacitySnapshot Read(string driveName);
    }

    /// <summary>不可变磁盘容量快照。</summary>
    internal sealed class DiskCapacitySnapshot
    {
        /// <summary>总字节数。</summary>
        public long TotalBytes { get; }
        /// <summary>当前用户可用字节数。</summary>
        public long AvailableBytes { get; }
        /// <summary>实际已用比例，不将配额限制算作磁盘占用。</summary>
        public double UsedPercent { get; }
        /// <summary>保存容量并限制百分比范围。</summary>
        public DiskCapacitySnapshot(long total, long available, long free)
        {
            if (total <= 0) throw new IOException("磁盘总容量无效。");
            TotalBytes = total;
            AvailableBytes = Math.Max(0, Math.Min(total, available));
            UsedPercent = Math.Max(0, Math.Min(100, (total - (double)free) * 100 / total));
        }
    }

    /// <summary>读取系统磁盘信息，不扫描文件目录。</summary>
    internal sealed class SystemDiskCapacityReader : IDiskCapacityReader
    {
        /// <summary>读取就绪磁盘的容量。</summary>
        public DiskCapacitySnapshot Read(string driveName)
        {
            var drive = new DriveInfo(driveName);
            if (!drive.IsReady) throw new IOException("磁盘未就绪。");
            return new DiskCapacitySnapshot(drive.TotalSize, drive.AvailableFreeSpace, drive.TotalFreeSpace);
        }
    }

    public partial class FormMain
    {
        /// <summary>与参考项目一致，显示存图使用的 D 盘。</summary>
        private const string CapacityDrive = @"D:\";
        /// <summary>磁盘查询服务。</summary>
        private readonly IDiskCapacityReader _diskCapacityReader = new SystemDiskCapacityReader();
        /// <summary>防止慢磁盘导致查询重叠。</summary>
        private bool _diskCapacityRefreshRunning;

        /// <summary>首次显示立即刷新，随后每分钟刷新。</summary>
        private async void FormMain_DiskUsageShown(object sender, EventArgs e)
        {
            diskUsageTimer.Start();
            await RefreshDiskCapacityAsync();
        }

        /// <summary>定时触发后台容量查询。</summary>
        private async void DiskUsageTimer_Tick(object sender, EventArgs e)
        {
            await RefreshDiskCapacityAsync();
        }

        /// <summary>异步查询后在界面线程更新；窗口释放后丢弃结果。</summary>
        private async Task RefreshDiskCapacityAsync()
        {
            if (_diskCapacityRefreshRunning || IsDisposed || Disposing) return;
            _diskCapacityRefreshRunning = true;
            try
            {
                var snapshot = await Task.Run(() => _diskCapacityReader.Read(CapacityDrive));
                if (IsDisposed || Disposing) return;
                toolStripProgressBar1.Value = (int)Math.Round(snapshot.UsedPercent);
                toolStripDiskCapacityLabel.ForeColor = snapshot.UsedPercent >= 80 ? Color.Firebrick :
                    snapshot.UsedPercent >= 60 ? Color.DarkGoldenrod : Color.DarkGreen;
                const double gib = 1024d * 1024 * 1024;
                toolStripDiskCapacityLabel.Text = string.Format(LanguageManager.T("磁盘空间占用：{0:F2}%"), snapshot.UsedPercent);
                toolStripDiskCapacityLabel.ToolTipText = string.Format(LanguageManager.T("D盘：总容量 {0:F1} GB　剩余 {1:F1} GB　已用 {2:F1}%"),
                    snapshot.TotalBytes / gib, snapshot.AvailableBytes / gib, snapshot.UsedPercent);
                toolStripProgressBar1.ToolTipText = toolStripDiskCapacityLabel.ToolTipText;
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                toolStripProgressBar1.Value = 0;
                toolStripDiskCapacityLabel.ForeColor = Color.Gray;
                toolStripDiskCapacityLabel.Text = LanguageManager.T("D盘：容量暂不可用");
                toolStripDiskCapacityLabel.ToolTipText = ex.Message;
                toolStripProgressBar1.ToolTipText = toolStripDiskCapacityLabel.Text;
            }
            finally { _diskCapacityRefreshRunning = false; }
        }
    }
}
