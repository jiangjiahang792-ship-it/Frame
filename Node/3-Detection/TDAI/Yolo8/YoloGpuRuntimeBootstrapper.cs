using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// YOLO GPU运行环境启动器，负责读取显卡型号并把native DLL加载指向对应的隔离目录。
    /// </summary>
    internal static class YoloGpuRuntimeBootstrapper
    {
        /// <summary>
        /// GTX 750显卡的完整WMI名称，用于切换到CUDA 11.8专用DLL目录。
        /// </summary>
        private const string Gtx750Name = "NVIDIA GeForce GTX 750";

        /// <summary>
        /// 1050Ti及其它非GTX 750显卡默认使用的DLL隔离目录名称。
        /// </summary>
        private const string Gtx1050TiRuntimeDirectoryName = "1050tidll";

        /// <summary>
        /// GTX 750显卡使用的DLL隔离目录名称。
        /// </summary>
        private const string Gtx750RuntimeDirectoryName = "750dll";

        /// <summary>
        /// GPU native YOLO DLL文件名。
        /// </summary>
        private const string YoloNativeDllName = "yolo_openvino_tensorrt.dll";

        /// <summary>
        /// GPU运行环境相对于程序运行目录的根目录名称。
        /// </summary>
        private const string GpuRuntimeRootDirectoryName = "YoloGPUDll";

        /// <summary>
        /// 需要从GPU公共根目录预加载的native DLL，避免同名DLL被外部PATH抢先加载。
        /// </summary>
        private static readonly string[] SharedNativeDllNames =
        {
            "tbb12.dll",
            "opencv_world4100.dll",
            "onnxruntime.dll",
            "openvino.dll",
            "openvino_c.dll"
        };

        /// <summary>
        /// LoadLibraryEx使用DLL所在目录解析依赖项的标记。
        /// </summary>
        private const int LoadWithAlteredSearchPath = 0x00000008;

        /// <summary>
        /// 初始化锁，保证多流程并发加载模型时只初始化一次。
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 当前启动器是否已经完成初始化。
        /// </summary>
        private static bool _initialized;

        /// <summary>
        /// 当前公共DLL运行目录。
        /// </summary>
        private static string _sharedDirectory = string.Empty;

        /// <summary>
        /// 当前选中的显卡专用DLL运行目录。
        /// </summary>
        private static string _runtimeDirectory = string.Empty;

        /// <summary>
        /// 当前读取到的显卡信息摘要。
        /// </summary>
        private static string _gpuSummary = string.Empty;

        /// <summary>
        /// 当前是否检测到GTX 750。
        /// </summary>
        private static bool _isGtx750;

        /// <summary>
        /// 预加载后的YOLO native模块句柄，用于保证DllImport绑定到已选目录中的DLL。
        /// </summary>
        private static IntPtr _yoloModuleHandle = IntPtr.Zero;

        /// <summary>
        /// 预加载后的公共native模块句柄列表，用于维持公共DLL在进程生命周期内保持已加载状态。
        /// </summary>
        private static readonly List<IntPtr> SharedNativeModuleHandles = new List<IntPtr>();

        /// <summary>
        /// 设置进程级DLL搜索目录。
        /// </summary>
        /// <param name="lpPathName">需要加入搜索的DLL目录。</param>
        /// <returns>设置成功返回true。</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// 按完整路径加载native DLL。
        /// </summary>
        /// <param name="lpFileName">native DLL完整路径。</param>
        /// <param name="hFile">保留参数，固定传入IntPtr.Zero。</param>
        /// <param name="dwFlags">加载标记。</param>
        /// <returns>加载成功后的模块句柄。</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        /// <summary>
        /// 当前公共DLL运行目录。
        /// </summary>
        public static string SharedDirectory
        {
            get { return _sharedDirectory; }
        }

        /// <summary>
        /// 当前选择的显卡专用DLL运行目录。
        /// </summary>
        public static string RuntimeDirectory
        {
            get { return _runtimeDirectory; }
        }

        /// <summary>
        /// 当前读取到的显卡信息摘要。
        /// </summary>
        public static string GpuSummary
        {
            get { return _gpuSummary; }
        }

        /// <summary>
        /// 当前是否检测到GTX 750。
        /// </summary>
        public static bool IsGtx750
        {
            get { return _isGtx750; }
        }

        /// <summary>
        /// 初始化YOLO GPU native运行环境，必须在任何GPU YOLO native调用前执行。
        /// </summary>
        public static void Initialize()
        {
            string baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GpuRuntimeRootDirectoryName);
            Initialize(baseDirectory);
        }

        /// <summary>
        /// 初始化YOLO GPU native运行环境。
        /// </summary>
        /// <param name="baseDirectory">GPU运行环境公共根目录。</param>
        public static void Initialize(string baseDirectory)
        {
            lock (SyncRoot)
            {
                if (_initialized)
                    return;

                if (string.IsNullOrWhiteSpace(baseDirectory))
                    throw new ArgumentException("GPU运行环境目录不能为空。", "baseDirectory");

                string sharedDirectory = Path.GetFullPath(baseDirectory);
                if (!Directory.Exists(sharedDirectory))
                    throw new DirectoryNotFoundException("未找到YOLO GPU运行环境公共目录：" + sharedDirectory);

                List<string> videoControllers = ReadVideoControllerNames();
                bool isGtx750 = ContainsExactGpuName(videoControllers, Gtx750Name);
                string runtimeDirectory = ResolveRuntimeDirectory(sharedDirectory, isGtx750);

                RegisterManagedAssemblyResolver(sharedDirectory, runtimeDirectory);
                ConfigureNativeDllSearchPath(sharedDirectory, runtimeDirectory);
                PreloadSharedNativeDlls(sharedDirectory);
                PreloadYoloNativeDll(runtimeDirectory);

                _gpuSummary = JoinNames(videoControllers);
                _sharedDirectory = sharedDirectory;
                _runtimeDirectory = runtimeDirectory;
                _isGtx750 = isGtx750;
                _initialized = true;
            }
        }

        /// <summary>
        /// 读取当前计算机的显卡名称列表。
        /// </summary>
        /// <returns>显卡名称列表，读取失败时包含失败原因。</returns>
        private static List<string> ReadVideoControllerNames()
        {
            List<string> names = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        using (result)
                        {
                            object name = result["Name"];
                            if (name == null)
                                continue;

                            string text = name.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                                names.Add(text.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                names.Add("读取显卡信息失败：" + ex.Message);
            }

            if (names.Count == 0)
                names.Add("未读取到显卡信息");

            return names;
        }

        /// <summary>
        /// 判断显卡列表中是否包含指定的完整型号名称。
        /// </summary>
        /// <param name="videoControllers">显卡名称列表。</param>
        /// <param name="gpuName">需要匹配的完整显卡型号。</param>
        /// <returns>匹配到指定型号返回true。</returns>
        private static bool ContainsExactGpuName(IList<string> videoControllers, string gpuName)
        {
            for (int i = 0; i < videoControllers.Count; i++)
            {
                if (string.Equals(videoControllers[i], gpuName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 根据显卡型号选择隔离的DLL运行目录。
        /// </summary>
        /// <param name="sharedDirectory">GPU运行环境公共根目录。</param>
        /// <param name="isGtx750">当前计算机是否检测到GTX 750。</param>
        /// <returns>选中的DLL运行目录。</returns>
        private static string ResolveRuntimeDirectory(string sharedDirectory, bool isGtx750)
        {
            string directoryName = isGtx750 ? Gtx750RuntimeDirectoryName : Gtx1050TiRuntimeDirectoryName;
            string runtimeDirectory = Path.Combine(sharedDirectory, directoryName);
            if (!Directory.Exists(runtimeDirectory))
            {
                if (isGtx750)
                    throw new DirectoryNotFoundException("检测到NVIDIA GeForce GTX 750，但未找到750专用DLL目录：" + runtimeDirectory);

                throw new DirectoryNotFoundException("未检测到NVIDIA GeForce GTX 750，需要使用1050Ti DLL目录，但目录不存在：" + runtimeDirectory);
            }

            return runtimeDirectory;
        }

        /// <summary>
        /// 注册托管程序集解析器，让OpenCvSharp等托管DLL可从GPU公共根目录或隔离目录加载。
        /// </summary>
        /// <param name="sharedDirectory">公共DLL运行目录。</param>
        /// <param name="runtimeDirectory">当前选中的DLL运行目录。</param>
        private static void RegisterManagedAssemblyResolver(string sharedDirectory, string runtimeDirectory)
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                AssemblyName assemblyName = new AssemblyName(args.Name);
                string sharedCandidatePath = Path.Combine(sharedDirectory, assemblyName.Name + ".dll");
                if (File.Exists(sharedCandidatePath))
                    return Assembly.LoadFrom(sharedCandidatePath);

                string runtimeCandidatePath = Path.Combine(runtimeDirectory, assemblyName.Name + ".dll");
                if (File.Exists(runtimeCandidatePath))
                    return Assembly.LoadFrom(runtimeCandidatePath);

                return null;
            };
        }

        /// <summary>
        /// 配置native DLL搜索路径，优先使用当前选中的隔离目录，再使用公共DLL目录。
        /// </summary>
        /// <param name="sharedDirectory">公共DLL运行目录。</param>
        /// <param name="runtimeDirectory">当前选中的DLL运行目录。</param>
        private static void ConfigureNativeDllSearchPath(string sharedDirectory, string runtimeDirectory)
        {
            string oldPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string newPath = oldPath;
            if (newPath.IndexOf(sharedDirectory, StringComparison.OrdinalIgnoreCase) < 0)
                newPath = sharedDirectory + ";" + newPath;

            if (newPath.IndexOf(runtimeDirectory, StringComparison.OrdinalIgnoreCase) < 0)
                newPath = runtimeDirectory + ";" + newPath;

            Environment.SetEnvironmentVariable("PATH", newPath);

            if (!SetDllDirectory(runtimeDirectory))
                throw new InvalidOperationException("设置DLL搜索目录失败，Win32错误码：" + Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// 预加载公共根目录中的关键native DLL，确保公共依赖从当前程序GPU环境目录取。
        /// </summary>
        /// <param name="sharedDirectory">公共DLL运行目录。</param>
        private static void PreloadSharedNativeDlls(string sharedDirectory)
        {
            for (int i = 0; i < SharedNativeDllNames.Length; i++)
            {
                string dllPath = Path.Combine(sharedDirectory, SharedNativeDllNames[i]);
                if (!File.Exists(dllPath))
                    throw new FileNotFoundException("未找到公共native DLL。", dllPath);

                IntPtr moduleHandle = LoadLibraryEx(dllPath, IntPtr.Zero, LoadWithAlteredSearchPath);
                if (moduleHandle == IntPtr.Zero)
                    throw new InvalidOperationException("加载公共native DLL失败，Win32错误码：" + Marshal.GetLastWin32Error() + "，路径：" + dllPath);

                SharedNativeModuleHandles.Add(moduleHandle);
            }
        }

        /// <summary>
        /// 提前加载当前隔离目录中的YOLO native DLL，确保后续DllImport绑定到指定显卡目录。
        /// </summary>
        /// <param name="runtimeDirectory">当前选中的DLL运行目录。</param>
        private static void PreloadYoloNativeDll(string runtimeDirectory)
        {
            string yoloDllPath = Path.Combine(runtimeDirectory, YoloNativeDllName);
            if (!File.Exists(yoloDllPath))
                throw new FileNotFoundException("未找到YOLO GPU native DLL。", yoloDllPath);

            _yoloModuleHandle = LoadLibraryEx(yoloDllPath, IntPtr.Zero, LoadWithAlteredSearchPath);
            if (_yoloModuleHandle == IntPtr.Zero)
                throw new InvalidOperationException("加载YOLO GPU native DLL失败，Win32错误码：" + Marshal.GetLastWin32Error() + "，路径：" + yoloDllPath);
        }

        /// <summary>
        /// 把显卡名称列表合并成便于日志显示的字符串。
        /// </summary>
        /// <param name="names">显卡名称列表。</param>
        /// <returns>用分号连接后的显卡信息。</returns>
        private static string JoinNames(IList<string> names)
        {
            if (names == null || names.Count == 0)
                return "未读取到显卡信息";

            return string.Join("; ", names);
        }
    }
}
