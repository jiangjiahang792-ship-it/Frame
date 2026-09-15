using System;
using System.IO;
using System.Management;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 使用Windows和WMI读取CPU、内存、GPU及程序磁盘信息的默认硬件探针。
    /// </summary>
    public sealed class WindowsHardwareResourceProbe : IHardwareResourceProbe
    {
        /// <summary>硬件内存读取失败时使用的保守总内存，单位MB。</summary>
        private const long FallbackTotalMemoryMb = 4096;

        /// <summary>硬件内存读取失败时使用的保守可用内存，单位MB。</summary>
        private const long FallbackAvailableMemoryMb = 2048;

        /// <summary>
        /// 读取当前计算机硬件资源；任何局部失败都记录警告并继续返回。
        /// </summary>
        /// <returns>规范化后的硬件资源快照。</returns>
        public HardwareResourceSnapshot Capture()
        {
            int logicalCount = Math.Max(1, Environment.ProcessorCount);
            HardwareResourceSnapshot snapshot = new HardwareResourceSnapshot
            {
                CapturedAt = DateTime.Now,
                LogicalProcessorCount = logicalCount,
                AvailableLogicalProcessorCount = logicalCount,
                Is64BitProcess = Environment.Is64BitProcess
            };

            ReadCpu(snapshot);
            ReadProcessAffinity(snapshot);
            ReadMemory(snapshot);
            ReadGpu(snapshot);
            ReadProgramDrive(snapshot);
            Normalize(snapshot);
            return snapshot;
        }

        /// <summary>
        /// 读取CPU名称与物理核心数。
        /// </summary>
        /// <param name="snapshot">待填充的硬件快照。</param>
        private static void ReadCpu(HardwareResourceSnapshot snapshot)
        {
            try
            {
                int physicalCores = 0;
                string cpuName = string.Empty;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        physicalCores += ConvertToInt32(result["NumberOfCores"]);
                        string name = Convert.ToString(result["Name"]);
                        if (string.IsNullOrWhiteSpace(cpuName) && !string.IsNullOrWhiteSpace(name))
                            cpuName = name.Trim();
                    }
                }

                snapshot.PhysicalCoreCount = physicalCores;
                snapshot.CpuName = cpuName;
            }
            catch (Exception ex)
            {
                snapshot.ProbeWarnings.Add("CPU物理核心读取失败，使用逻辑线程保守推算：" + ex.Message);
            }
        }

        /// <summary>
        /// 读取当前进程亲和性允许使用的逻辑处理器数量。
        /// </summary>
        /// <param name="snapshot">待填充的硬件快照。</param>
        private static void ReadProcessAffinity(HardwareResourceSnapshot snapshot)
        {
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    ulong affinityMask = unchecked((ulong)process.ProcessorAffinity.ToInt64());
                    int bitCount = CountSetBits(affinityMask);
                    if (bitCount > 0)
                        snapshot.AvailableLogicalProcessorCount = Math.Min(snapshot.LogicalProcessorCount, bitCount);
                }
            }
            catch (Exception ex)
            {
                snapshot.ProbeWarnings.Add("进程可用逻辑线程读取失败，使用系统逻辑线程数：" + ex.Message);
            }
        }

        /// <summary>
        /// 读取总内存与当前可用内存。
        /// </summary>
        /// <param name="snapshot">待填充的硬件快照。</param>
        private static void ReadMemory(HardwareResourceSnapshot snapshot)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        snapshot.TotalMemoryMb = ConvertToInt64(result["TotalVisibleMemorySize"]) / 1024L;
                        snapshot.AvailableMemoryMb = ConvertToInt64(result["FreePhysicalMemory"]) / 1024L;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                snapshot.ProbeWarnings.Add("物理内存读取失败，使用保守内存档案：" + ex.Message);
            }
        }

        /// <summary>
        /// 读取显卡名称；显卡信息只进入诊断文本。
        /// </summary>
        /// <param name="snapshot">待填充的硬件快照。</param>
        private static void ReadGpu(HardwareResourceSnapshot snapshot)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        string name = Convert.ToString(result["Name"]);
                        if (!string.IsNullOrWhiteSpace(name))
                            snapshot.GpuNames.Add(name.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                snapshot.ProbeWarnings.Add("GPU诊断信息读取失败，不影响资源参数计算：" + ex.Message);
            }
        }

        /// <summary>
        /// 读取程序所在磁盘可用空间。
        /// </summary>
        /// <param name="snapshot">待填充的硬件快照。</param>
        private static void ReadProgramDrive(HardwareResourceSnapshot snapshot)
        {
            try
            {
                string assemblyLocation = typeof(WindowsHardwareResourceProbe).Assembly.Location;
                string baseDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : Path.GetDirectoryName(assemblyLocation);
                string root = Path.GetPathRoot(baseDirectory);
                DriveInfo drive = new DriveInfo(root);
                snapshot.SystemDriveName = drive.Name;
                snapshot.SystemDriveFreeSpaceMb = drive.AvailableFreeSpace / 1024L / 1024L;
            }
            catch (Exception ex)
            {
                snapshot.ProbeWarnings.Add("程序磁盘可用空间读取失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 修正硬件探针返回的空值和无效值。
        /// </summary>
        /// <param name="snapshot">待规范化的硬件快照。</param>
        private static void Normalize(HardwareResourceSnapshot snapshot)
        {
            if (snapshot.PhysicalCoreCount <= 0)
            {
                snapshot.PhysicalCoreCount = Math.Max(1, snapshot.LogicalProcessorCount / 2);
                snapshot.ProbeWarnings.Add("未读取到物理核心数，已按逻辑线程数的一半保守推算。");
            }

            if (string.IsNullOrWhiteSpace(snapshot.CpuName))
                snapshot.CpuName = "未知CPU";
            snapshot.LogicalProcessorCount = Math.Max(1, snapshot.LogicalProcessorCount);
            snapshot.AvailableLogicalProcessorCount = Math.Max(1, Math.Min(snapshot.LogicalProcessorCount, snapshot.AvailableLogicalProcessorCount));

            if (snapshot.TotalMemoryMb <= 0)
            {
                snapshot.TotalMemoryMb = FallbackTotalMemoryMb;
                snapshot.ProbeWarnings.Add("未读取到总内存，已使用4096MB保守值。");
            }

            if (snapshot.AvailableMemoryMb <= 0)
            {
                snapshot.AvailableMemoryMb = Math.Min(snapshot.TotalMemoryMb, FallbackAvailableMemoryMb);
                snapshot.ProbeWarnings.Add("未读取到可用内存，已使用保守值。");
            }

            snapshot.AvailableMemoryMb = Math.Min(snapshot.TotalMemoryMb, snapshot.AvailableMemoryMb);
            snapshot.SystemDriveFreeSpaceMb = Math.Max(0, snapshot.SystemDriveFreeSpaceMb);
            if (string.IsNullOrWhiteSpace(snapshot.SystemDriveName))
                snapshot.SystemDriveName = "未知";
        }

        /// <summary>
        /// 统计64位亲和性掩码中允许使用的处理器数量。
        /// </summary>
        /// <param name="value">处理器亲和性掩码。</param>
        /// <returns>置位数量。</returns>
        private static int CountSetBits(ulong value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        /// <summary>
        /// 将WMI值转换为32位整数，空值返回0。
        /// </summary>
        /// <param name="value">WMI属性值。</param>
        /// <returns>转换后的整数。</returns>
        private static int ConvertToInt32(object value)
        {
            return value == null ? 0 : Convert.ToInt32(value);
        }

        /// <summary>
        /// 将WMI值转换为64位整数，空值返回0。
        /// </summary>
        /// <param name="value">WMI属性值。</param>
        /// <returns>转换后的整数。</returns>
        private static long ConvertToInt64(object value)
        {
            return value == null ? 0L : Convert.ToInt64(value);
        }
    }
}
