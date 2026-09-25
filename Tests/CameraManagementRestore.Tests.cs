using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.CameraAdd;

/// <summary>离线执行相机管理恢复路径，真实分配海康缓冲池，只替代硬件操作。</summary>
internal static class CameraManagementRestoreTests
{
    /// <summary>访问真实海康缓冲池方法。</summary>
    private static readonly MethodInfo Allocate = typeof(CameraHik).GetMethod("EnsureRawFrameBuffers", BindingFlags.Instance | BindingFlags.NonPublic);
    /// <summary>归还真实缓冲池与预算租约。</summary>
    private static readonly MethodInfo Release = typeof(CameraHik).GetMethod("StopFrameConversionWorker", BindingFlags.Instance | BindingFlags.NonPublic);
    /// <summary>统计行为断言数量。</summary>
    private static int _checks;

    /// <summary>六台相机同时反复取流/停流，检查全局重平衡与单相机关闭之间没有死锁或预算泄漏。</summary>
    private static void RunConcurrentReconnect()
    {
        var cameras = Enumerable.Range(1, 6).Select(index => new OfflineCamera(index, 12288000)).ToList();
        foreach (var camera in cameras) Solution.Instance.AllDevices.Add(camera.Camera);
        try
        {
            Task[] tasks = cameras.Select(camera => Task.Run(() =>
            {
                for (int round = 0; round < 20; round++)
                {
                    camera.Camera.Open(); camera.Camera.StartGrabbing();
                    if (Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes > 256L * 1024 * 1024)
                        throw new Exception("并发重连突破预算");
                    camera.Camera.Close();
                }
            })).ToArray();
            Check(Task.WaitAll(tasks, TimeSpan.FromSeconds(10)), "六台相机并发重连超时，疑似锁循环");
            Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes == 0, "120次并发重连后预算必须归零");
            Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.ActiveReservationCount == 0, "120次并发重连后不得残留租约");
        }
        finally
        {
            foreach (var camera in cameras) Release.Invoke(camera.Buffers, null);
            Solution.Instance.AllDevices.Clear();
        }
    }

    /// <summary>模拟正在转换的帧稍后归还，验证重平衡不会释放在用帧或因瞬时忙碌误报失败。</summary>
    private static void RunBusyPoolAddition()
    {
        var cameras = new List<OfflineCamera>();
        var held = new List<Tuple<object, object>>();
        Task returning = null;
        using (var allowReturn = new ManualResetEventSlim(false))
        {
            try
            {
                for (int index = 0; index < 3; index++)
                {
                    var camera = new OfflineCamera(index + 1, 12288000);
                    cameras.Add(camera); Solution.Instance.AllDevices.Add(camera.Camera);
                    camera.Camera.Open(); camera.Camera.StartGrabbing();
                }
                foreach (var camera in cameras)
                {
                    object queue = typeof(CameraHik).GetField("_availableRawFrames", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(camera.Buffers);
                    var dequeue = queue.GetType().GetMethod("TryDequeue");
                    var parameters = new object[] { null };
                    while ((bool)dequeue.Invoke(queue, parameters)) held.Add(Tuple.Create(queue, parameters[0]));
                }
                returning = Task.Run(() =>
                {
                    allowReturn.Wait(); Thread.Sleep(100);
                    foreach (var pair in held)
                    {
                        if ((IntPtr)pair.Item2.GetType().GetProperty("Buffer").GetValue(pair.Item2) == IntPtr.Zero)
                            throw new Exception("重平衡错误释放了在用帧");
                        pair.Item1.GetType().GetMethod("Enqueue").Invoke(pair.Item1, new[] { pair.Item2 });
                    }
                });
                var added = new OfflineCamera(4, 12288000);
                cameras.Add(added); Solution.Instance.AllDevices.Add(added.Camera);
                allowReturn.Set();
                added.Camera.Open(); added.Camera.StartGrabbing();
                returning.GetAwaiter().GetResult();
                Check(cameras.All(c => c.Grabbing), "已有帧转换归还后新增相机应能连接");
                Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes <= 256L * 1024 * 1024, "等待空闲帧期间仍不得越过预算");
            }
            finally
            {
                allowReturn.Set(); returning?.GetAwaiter().GetResult();
                foreach (var camera in cameras) Release.Invoke(camera.Buffers, null);
                Solution.Instance.AllDevices.Clear();
            }
        }
    }

    /// <summary>验证运行期间逐台新增相机，已有空闲池必须为后来相机让出预算。</summary>
    private static void RunDynamicAddition()
    {
        var cameras = new List<OfflineCamera>();
        try
        {
            for (int index = 0; index < 5; index++)
            {
                var camera = new OfflineCamera(index + 1, 12288000);
                cameras.Add(camera);
                Solution.Instance.AllDevices.Add(camera.Camera);
                camera.Camera.Open();
                camera.Camera.StartGrabbing();
                Check(cameras.All(c => c.Grabbing), "新增相机不得停止已有相机");
                Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.ActiveReservationCount == index + 1, "新增相机应取得独立租约");
                Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes <= 256L * 1024 * 1024, "动态新增不得突破总预算");
            }
            // 手动断开并删除一台，新增替代相机，再重新连回原相机。
            var removed = cameras[1];
            removed.Camera.Close();
            Solution.Instance.AllDevices.Remove(removed.Camera);
            var replacement = new OfflineCamera(6, 12288000);
            cameras.Add(replacement);
            Solution.Instance.AllDevices.Add(replacement.Camera);
            replacement.Camera.Open(); replacement.Camera.StartGrabbing();
            Solution.Instance.AllDevices.Add(removed.Camera);
            removed.Camera.Open(); removed.Camera.StartGrabbing();
            Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.ActiveReservationCount == 6, "删除、新增、重新添加后6台相机均应取得预算");
        }
        finally
        {
            foreach (var camera in cameras) Release.Invoke(camera.Buffers, null);
            Solution.Instance.AllDevices.Clear();
        }
        Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes == 0, "动态场景关闭后不得泄漏预算");
    }

    /// <summary>硬件接口替身；取流仍使用生产缓冲分配代码。</summary>
    private sealed class OfflineCamera : RealProxy
    {
        /// <summary>真实缓冲池拥有者。</summary>
        public readonly CameraHik Buffers = new CameraHik();
        /// <summary>是否在加载方案时恢复连接。</summary>
        public bool Restore = true;
        /// <summary>替代硬件的连接状态。</summary>
        public bool Opened;
        /// <summary>取流成功状态。</summary>
        public bool Grabbing;
        /// <summary>模拟相机标识。</summary>
        public readonly string Name;
        /// <summary>单帧原始字节数。</summary>
        public readonly int Payload;
        /// <summary>记录被界面捕获的底层异常。</summary>
        public Exception Failure;
        /// <summary>创建设备时观察到的方案相机数。</summary>
        public int CountAtCreate;
        /// <summary>创建指定有效载荷的离线相机。</summary>
        public OfflineCamera(int index, int payload) : base(typeof(ICamera))
        { Name = "离线CCD" + index; Payload = payload; }
        /// <summary>获得相机接口。</summary>
        public ICamera Camera => (ICamera)GetTransparentProxy();
        /// <summary>处理外部硬件调用，保留生产内存分配与释放。</summary>
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try
            {
                object value = null;
                switch (call.MethodName)
                {
                    case "Equals": value = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
                    case "GetHashCode": value = Name.GetHashCode(); break;
                    case "get_DevName": case "get_UserDefinedName": case "get_SN": value = Name; break;
                    case "get_IsOpen": value = Opened; break;
                    case "get_RestoreConnectionRequested": value = Restore; break;
                    case "CreateDevice": CountAtCreate = Solution.Instance.CameraDevices.Count; break;
                    case "Open": Opened = true; value = true; break;
                    case "StartGrabbing": Allocate.Invoke(Buffers, new object[] { Payload }); Grabbing = true; break;
                    case "Close": Opened = false; goto case "StopGrabbing";
                    case "StopGrabbing": Release.Invoke(Buffers, null); Grabbing = false; break;
                    case "GetGrabStatus": value = Grabbing; break;
                    default:
                        var returnType = ((MethodInfo)call.MethodBase).ReturnType;
                        if (returnType != typeof(void) && returnType.IsValueType) value = Activator.CreateInstance(returnType);
                        break;
                }
                return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
            }
            catch (Exception ex)
            {
                Failure = ex.InnerException ?? ex;
                return new ReturnMessage(Failure, call);
            }
        }
    }

    /// <summary>验证行为，失败时立即报告。</summary>
    private static void Check(bool condition, string message)
    { _checks++; if (!condition) throw new Exception(message); }

    /// <summary>执行相机管理控件真实连接开关事件并泵送界面异步回调。</summary>
    private static void ToggleCamera(SingleCamera control, OfflineCamera camera, bool connected)
    {
        typeof(SingleCamera).GetMethod("uiSwitch1_ValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(control, new object[] { null, connected });
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (camera.Grabbing != connected && camera.Failure == null && timer.ElapsedMilliseconds < 5000)
        { Application.DoEvents(); Thread.Sleep(1); }
        Application.DoEvents();
        Check(camera.Grabbing == connected && camera.Failure == null, "管理界面开关未完成连接/断开：" + camera.Failure);
    }

    /// <summary>执行真实反序列化处理器，跳过会枚举物理设备的窗口构造函数。</summary>
    private static void RestoreCameras(List<OfflineCamera> cameras, FlowLayoutPanel panel)
    {
        ConfigHelper.SolConfig = new SolConfig { Devices = cameras.Select(c => (IDevice)c.Camera).ToList() };
        var form = (FrmCameraListView)FormatterServices.GetUninitializedObject(typeof(FrmCameraListView));
        GC.SuppressFinalize(form);
        typeof(FrmCameraListView).GetField("flowLayoutPanel1", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, panel);
        typeof(FrmCameraListView).GetMethod("Deserialization", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, false });
    }

    /// <summary>检查加载、管理界面手动连接以及连续重连后的内存占用。</summary>
    private static void RunScenario(bool manualLastTwo, bool reverse)
    {
        // 五台不同分辨率相机；11.7MB大图模拟用户报错的CCD3/CCD5。
        int[] payloads = { 5 * 1024 * 1024, 6 * 1024 * 1024, 12288000, 12288000, 12288000 };
        var cameras = payloads.Select((size, index) => new OfflineCamera(index + 1, size)).ToList();
        if (reverse) cameras.Reverse();
        if (manualLastTwo) foreach (var camera in cameras.Skip(3)) camera.Restore = false;
        using (var panel = new FlowLayoutPanel())
        {
            try
            {
                RestoreCameras(cameras, panel);
                Check(cameras.All(c => c.CountAtCreate == 5), "第一台创建前必须登记全部5台相机；实际=" + string.Join(",", cameras.Select(c => c.CountAtCreate)));
                Check(Solution.Instance.CameraDevices.Count == 5, "恢复控件不得重复登记相机");
                Check(panel.Controls.Count == 5, "所有相机应显示在管理界面");
                foreach (var camera in cameras.Where(c => !c.Restore))
                    ToggleCamera(panel.Controls.OfType<SingleCamera>().Single(c => ReferenceEquals(c.Camera, camera.Camera)), camera, true);
                Check(cameras.All(c => c.Grabbing && c.Failure == null), "五台相机必须全部取得取流预算：" + string.Join(";", cameras.Where(c => c.Failure != null).Select(c => c.Failure.Message)));
                var manager = Solution.Instance.CameraRawFrameMemoryBudgetManager;
                long stableBytes = manager.CurrentReservedBytes;
                Check(stableBytes <= 256L * 1024 * 1024, "总分配不得突破256MB预算");
                Check(manager.ActiveReservationCount == 5, "每台取流相机只拥有一个预算租约");
                for (int round = 0; round < 3; round++)
                {
                    foreach (var camera in cameras)
                    {
                        var control = panel.Controls.OfType<SingleCamera>().Single(c => ReferenceEquals(c.Camera, camera.Camera));
                        ToggleCamera(control, camera, false);
                        ToggleCamera(control, camera, true);
                        Check(manager.CurrentReservedBytes == stableBytes, "断开重连必须复用归还的预算，不得累积占用");
                    }
                }
            }
            finally
            {
                foreach (var camera in cameras) Release.Invoke(camera.Buffers, null);
                foreach (Control control in panel.Controls.Cast<Control>().ToArray()) control.Dispose();
                SingleCamera.SingleCameraList.Clear();
                Solution.Instance.AllDevices.Clear();
            }
        }
        Check(Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes == 0, "全部停流必须完整归还预算");
    }

    /// <summary>离线测试入口，不枚举或连接真实相机、PLC。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        try
        {
            LanguageManager.Initialize();
            if (args.Contains("busy")) { RunBusyPoolAddition(); Console.WriteLine("PASS: 在用帧安全回收"); return 0; }
            if (args.Contains("dynamic")) { RunDynamicAddition(); Console.WriteLine("PASS: 动态新增相机 {0} 项断言", _checks); return 0; }
            RunScenario(false, false); RunScenario(true, false); RunScenario(false, true); RunScenario(true, true);
            RunDynamicAddition();
            RunBusyPoolAddition();
            RunConcurrentReconnect();
            Console.WriteLine("PASS: 相机管理恢复、手动连接及重连预算 {0} 项断言", _checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
