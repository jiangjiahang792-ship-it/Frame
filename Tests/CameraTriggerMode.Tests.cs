using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows.Forms;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

/// <summary>真实参数窗体及硬件参数写入验证，全程使用离线设备和接口替身。</summary>
internal static class CameraTriggerModeTests
{
    /// <summary>实例成员反射标志。</summary>
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>累计通过的检查数。</summary>
    private static int _checks;
    /// <summary>条件失败时中止验证。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }
    /// <summary>获取Designer中的真实控件。</summary>
    private static T Field<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, InstanceFlags).GetValue(instance);
    }
    /// <summary>调用真实实例入口。</summary>
    private static object Call(object instance, string name, params object[] args)
    {
        return instance.GetType().GetMethod(name, InstanceFlags).Invoke(instance, args);
    }
    /// <summary>记录硬件写入，禁止测试访问真实相机。</summary>
    private sealed class CameraProbe : RealProxy
    {
        /// <summary>当前测试所观察到的方法调用。</summary>
        public readonly List<string> Calls = new List<string>();
        /// <summary>最后写入的触发模式。</summary>
        public TriggerModel Mode;
        /// <summary>创建相机接口替身。</summary>
        public CameraProbe() : base(typeof(ICamera)) { }
        /// <summary>记录接口调用并返回无设备默认值。</summary>
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            Calls.Add(call.MethodName);
            if (call.MethodName == "SetTriggerMode") Mode = (TriggerModel)call.Args[0];
            Type resultType = ((MethodInfo)call.MethodBase).ReturnType;
            object result = resultType == typeof(void) || !resultType.IsValueType ? null : Activator.CreateInstance(resultType);
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        }
    }
    /// <summary>验证模式恢复、保存、序列化、联动和各入口硬件参数写入。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var camera = new CameraHik { UserDefinedName = "离线验证相机", DevName = "离线验证相机" };
            Solution.Instance.AllDevices.Add(camera);
            using (var form = new ParamFormImageSource(null))
            {
                var saved = new NodeParamImageSoucre { ImageSource = "相机", CameraName = camera.UserDefinedName,
                    TriggerModel = TriggerModel.Off, TriggerSource = TriggerSource.LINE1, TriggerDelay = 123,
                    ExposureTime = 1000, Gain = 2, TimeOut = 500 };
                form.Params = saved;
                form.SetParam2Form();
                var mode = Field<ComboBox>(form, "comboBoxTriggerModel");
                var source = Field<ComboBox>(form, "comboBoxTriggerMode");
                var edge = Field<ComboBox>(form, "comboBoxTriggerEdge");
                var delay = Field<NumericUpDown>(form, "numericUpDownTriggerDelay");
                Check(saved.TriggerModel == TriggerModel.Off && mode.SelectedIndex == 1, "恢复关闭模式时被改写。");
                Check(!source.Enabled && !edge.Enabled && !delay.Enabled, "连续采集未禁用触发配置。");
                Check((bool)Call(form, "SaveParams"), "离线相机保存失败。");
                var roundTrip = JsonConvert.DeserializeObject<NodeParamImageSoucre>(JsonConvert.SerializeObject(form.Params));
                Check(roundTrip.TriggerModel == TriggerModel.Off && roundTrip.TriggerSource == TriggerSource.LINE1 && roundTrip.TriggerDelay == 123,
                    "模式或暂时禁用的触发参数没有持久化。");
                mode.SelectedIndex = 0;
                Check(source.Enabled && edge.Enabled && delay.Enabled, "重新开启未恢复硬触发配置。");
                source.SelectedIndex = 0;
                Check(!edge.Enabled, "软件触发不应启用触发沿。");
                Check((bool)Call(form, "SaveParams") && ((NodeParamImageSoucre)form.Params).TriggerModel == TriggerModel.On, "开启模式未保存。");
                Check(new NodeParamImageSoucre().TriggerModel == TriggerModel.On, "新节点应默认开启。");
                var equivalent = typeof(Solution).GetMethod("HaveEquivalentCameraParameters", BindingFlags.NonPublic | BindingFlags.Static);
                var other = JsonConvert.DeserializeObject<NodeParamImageSoucre>(JsonConvert.SerializeObject(roundTrip));
                other.TriggerModel = TriggerModel.On;
                Check(!(bool)equivalent.Invoke(null, new object[] { roundTrip, other }), "同相机不同模式应判定为冲突。");
                other.TriggerModel = TriggerModel.Off;
                other.TriggerSource = TriggerSource.SOFT;
                other.TriggerDelay = 999;
                Check((bool)equivalent.Invoke(null, new object[] { roundTrip, other }), "连续模式应忽略无效触发参数差异。");
                var process = new Process("触发模式校验") { Enable = true, HasCanvasGraph = false };
                using (var upstream = new NodeImageSource(1, "上游", process, TDJS_Vision.Node.NodeType.ImageSource))
                using (var acquisition = new NodeImageSource(2, "相机", process, TDJS_Vision.Node.NodeType.ImageSource))
                {
                    upstream.Active = true;
                    acquisition.Active = true;
                    acquisition.ParamForm.Params = roundTrip;
                    process.Nodes.Add(upstream);
                    process.Nodes.Add(acquisition);
                    var validator = new ProcessCameraConfigurationValidator();
                    Check(validator.Validate(new[] { process }).Count == 0, "连续采集保留线路源时被误判为硬触发。");
                    roundTrip.TriggerModel = TriggerModel.On;
                    Check(validator.Validate(new[] { process }).Count == 1, "开启线路触发后应恢复上游节点限制。");
                    roundTrip.TriggerModel = TriggerModel.Off;
                }
                var probe = new CameraProbe();
                var fake = (ICamera)probe.GetTransparentProxy();
                var apply = typeof(Solution).GetMethod("ApplyCameraCallbackParameters", BindingFlags.NonPublic | BindingFlags.Static);
                foreach (bool solutionEntry in new[] { false, true })
                {
                    foreach (TriggerModel triggerMode in new[] { TriggerModel.Off, TriggerModel.On })
                    {
                        probe.Calls.Clear();
                        other.TriggerModel = triggerMode;
                        other.TriggerSource = TriggerSource.LINE1;
                        if (solutionEntry) apply.Invoke(null, new object[] { fake, other });
                        else Call(form, "SetCameraParams", fake, triggerMode, other.TriggerSource, other.TriggerEdge, 123, 1000D, 2D, (uint)500);
                        Check(probe.Mode == triggerMode, "硬件入口强制覆盖了所选模式。");
                        Check(probe.Calls.Contains("SetTriggerSource") == (triggerMode == TriggerModel.On) &&
                            probe.Calls.Contains("SetTriggerEdge") == (triggerMode == TriggerModel.On) &&
                            probe.Calls.Contains("SetTriggerDelay") == (triggerMode == TriggerModel.On), "关闭时写入了不适用的触发配置。");
                        Check(probe.Calls.Contains("SetExposureTime") && probe.Calls.Contains("SetGain"), "模式切换丢失曝光或增益设置。");
                    }
                }
                form.Show();
                Application.DoEvents();
                Check(mode.Visible && mode.Parent.ClientRectangle.Contains(mode.Bounds), "触发模式控件被裁切。");
                var save = Field<Button>(form, "button1");
                Check(form.ClientRectangle.Contains(form.PointToClient(save.PointToScreen(System.Drawing.Point.Empty))), "保存按钮不在可见区域。");
                Directory.CreateDirectory(args[0]);
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(args[0], "相机触发模式.png"));
                }
                mode.SelectedIndex = 1;
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(args[0], "相机连续采集.png"));
                }
                form.Close();
            }
            Solution.Instance.AllDevices.Remove(camera);
            Console.WriteLine("相机触发模式验证通过：" + _checks + "项。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
