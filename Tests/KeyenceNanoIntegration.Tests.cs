using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Forms.PLCAdd;

/// <summary>验证真实添加界面、设备工厂、方案恢复和本机基恩士上位链路协议。</summary>
internal static class KeyenceNanoIntegrationTests
{
    /// <summary>按现有方案的属性级转换器方式保存设备列表。</summary>
    private sealed class DeviceSnapshot
    {
        /// <summary>需要保存和恢复的设备列表。</summary>
        [JsonConverter(typeof(DeviceListConverter<IDevice>))]
        public List<IDevice> Devices { get; set; }
    }

    /// <summary>运行不连接实体 PLC 的专项验证。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            VerifyParameters();
            Console.WriteLine("PASS: 设备参数校验。");
            VerifyAddAndPersistence(args[0]);
            Console.WriteLine("PASS: 添加流程与方案保存恢复。");
            // 控件创建后会安装 WinForms 同步上下文；本测试无消息循环，不能在该上下文阻塞等待异步通信。
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            VerifyCommunication();
            Console.WriteLine("PASS: 参数校验、添加界面、设备创建、参数展示、方案恢复、本机通信。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>断言失败时中止专项验证。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>创建本机测试设备的参数。</summary>
    private static PLCParms Parameters(int port)
    {
        return new PLCParms
        {
            DeviceBrand = DeviceBrand.Keyence,
            PlcConType = PlcConType.ETHERNET,
            UserDefinedName = "基恩士验证设备",
            EthernetParms = new EthernetParms("127.0.0.1", port)
        };
    }

    /// <summary>验证无效端口、地址和通信方式不能创建客户端。</summary>
    private static void VerifyParameters()
    {
        foreach (int port in new[] { 0, -1, 65536 })
            ExpectInvalid(Parameters(port));
        var invalid = Parameters(8501);
        invalid.PlcConType = PlcConType.COM;
        ExpectInvalid(invalid);
        invalid = Parameters(8501);
        invalid.EthernetParms.IP = "无效地址";
        ExpectInvalid(invalid);
        var plc = new PlcKeyenceNano(Parameters(8501));
        plc.OperationTimeoutMs = 1;
        Check(plc.OperationTimeoutMs == 100, "最小超时失效。");
        plc.OperationTimeoutMs = int.MaxValue;
        Check(plc.OperationTimeoutMs == 60000, "最大超时失效。");
        Check(!plc.ReadInt16(new string[0]).IsSuccess, "空地址数组必须返回失败。");
        Check(!plc.ReadBoolAsync(new[] { " " }).GetAwaiter().GetResult().IsSuccess, "空白地址必须返回失败。");
        plc.Release();
    }

    /// <summary>确认设备参数校验能够拒绝无效输入。</summary>
    private static void ExpectInvalid(PLCParms parms)
    {
        try
        {
            new PlcKeyenceNano(parms).Release();
        }
        catch (ArgumentException) { return; }
        catch (NotSupportedException) { return; }
        throw new InvalidOperationException("无效参数未被拒绝。");
    }

    /// <summary>读取真实窗体中的设计器控件。</summary>
    private static T Control<T>(object owner, string name) where T : class
    {
        return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }

    /// <summary>执行真实确认事件，通过现有工厂生成设备，并使用现有转换器验证保存恢复。</summary>
    private static void VerifyAddAndPersistence(string imagePath)
    {
        SinglePLC single = null;
        EventHandler<PLCParms> onAdd = (sender, parms) => single = new SinglePLC(parms);
        FrmPLCNew.PLCAddEvent += onAdd;
        try
        {
            using (var form = new FrmPLCNew())
            {
                var tabs = Control<TabControl>(form, "tabControl1");
                Check(tabs.TabPages.Count == 5, "添加页数量错误。");
                var tab = Control<TabPage>(form, "tabPageKeyence");
                Check(tab.Text == "基恩士 Nano OverTcp", "基恩士页标题错误。");
                Check(tabs.Multiline, "新增页不能挤掉原有协议入口。");
                tabs.SelectedTab = tab;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.ShowInTaskbar = false;
                form.Show();
                form.PerformLayout();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, form.ClientRectangle);
                    bitmap.Save(imagePath);
                }

                Control<TextBox>(form, "textBoxNameKeyence").Text = "  基恩士添加验证  ";
                Control<TextBox>(form, "textBoxPortKeyence").Text = "18501";
                Check(Control<Sunny.UI.UIIPTextBox>(form, "uiipTextBoxKeyence").Text == "127.0.0.1", "IP默认资源错误。");
                Control<Button>(form, "buttonConfirmKeyence").PerformClick();
                Check(single != null && single.Plc is PlcKeyenceNano, "确认添加未创建基恩士设备。");
                Check(Solution.Instance.PlcDevices.Contains(single.Plc), "设备未进入现有PLC列表。");
                Check(single.Plc.UserDefinedName == "基恩士添加验证", "设备名称未正确保存。");
                Check(!single.Plc.IsConnect, "添加设备不能主动连接PLC。");

                using (var panel = new EthernetParamsControl(single.Plc.PLCParms))
                {
                    Check(Control<Label>(panel, "labelIP").Text == "127.0.0.1", "IP参数展示错误。");
                    Check(Control<Label>(panel, "labelPort").Text == "18501", "端口参数展示错误。");
                }

                string json = JsonConvert.SerializeObject(new DeviceSnapshot { Devices = new List<IDevice> { single.Plc } });
                var restoredDevices = JsonConvert.DeserializeObject<DeviceSnapshot>(json).Devices;
                Check(restoredDevices != null && restoredDevices.Count == 1, "设备列表恢复失败。");
                var restored = restoredDevices[0] as PlcKeyenceNano;
                Check(restored != null, "保存的ClassName无法恢复基恩士类型。");
                restored.CreateDevice();
                Check(restored.Brand == DeviceBrand.Keyence && restored.PLCParms.DeviceBrand == DeviceBrand.Keyence,
                    "品牌保存恢复失败。");
                Check(restored.PLCParms.EthernetParms.Port == 18501 && restored.UserDefinedName == "基恩士添加验证",
                    "通信参数保存恢复失败。");
                restored.Release();
                // 已保存的旧方案没有协议字段时，必须仍创建原来的 Nano 客户端。
                var legacyJson = Newtonsoft.Json.Linq.JObject.Parse(json);
                var legacyDevice = (Newtonsoft.Json.Linq.JObject)legacyJson["Devices"].First.First;
                ((Newtonsoft.Json.Linq.JObject)legacyDevice["PLCParms"]).Remove("KeyenceProtocol");
                var legacy = (PlcKeyenceNano)JsonConvert.DeserializeObject<DeviceSnapshot>(legacyJson.ToString()).Devices[0];
                using (var legacySingle = new SinglePLC(legacy.PLCParms))
                {
                    Check(legacySingle.Plc is PlcKeyenceNano, "旧方案缺少协议字段时必须保持Nano协议。");
                    Solution.Instance.AllDevices.Remove(legacySingle.Plc);
                    SinglePLC.SinglePLCs.Remove(legacySingle);
                    legacySingle.Plc.Release();
                }
            }
        }
        finally
        {
            FrmPLCNew.PLCAddEvent -= onAdd;
            if (single != null)
            {
                Solution.Instance.AllDevices.Remove(single.Plc);
                SinglePLC.SinglePLCs.Remove(single);
                single.Plc.Release();
                single.Dispose();
            }
        }
    }

    /// <summary>通过库提供的模拟服务验证同步、异步、离散和连续地址读写及连接恢复。</summary>
    private static void VerifyCommunication()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var server = new KeyenceNanoServer();
        var plc = new PlcKeyenceNano(Parameters(port));
        var states = new List<bool>();
        plc.ConnectStatusEvent += (sender, connected) => states.Add(connected);
        try
        {
            server.ServerStart(port);
            Check(plc.Connect(), "基恩士上位链路连接失败。");
            Check(plc.WriteInt("DM100", 123456).IsSuccess, "写入整数失败。");
            Check(plc.ReadInt32("DM100").Content == 123456, "读取整数错误。");
            Check(plc.WriteBoolAsync("MR100", true).GetAwaiter().GetResult().IsSuccess, "异步写入位失败。");
            var bits = plc.ReadBoolAsync(new[] { "MR100", "MR101", "MR100" }).GetAwaiter().GetResult();
            Check(bits.IsSuccess && bits.Content.SequenceEqual(new[] { true, false, true }), "离散位地址顺序错误。");
            Check(plc.WriteFloat("DM110", 12.5f).IsSuccess, "写入浮点数失败。");
            Check(plc.ReadFloatAsync("DM110").GetAwaiter().GetResult().Content == 12.5f, "异步读取浮点数错误。");
            Check(plc.WriteIntAsync("DM120", new[] { 7, 8, 9 }).GetAwaiter().GetResult().IsSuccess, "批量写入失败。");
            var batch = plc.ReadInt32("DM120", 3);
            Check(batch.IsSuccess && batch.Content.SequenceEqual(new[] { 7, 8, 9 }), "连续地址批量读取错误。");
            var scatter = plc.ReadInt32(new[] { "DM124", "DM120", "DM124" });
            Check(scatter.IsSuccess && scatter.Content.SequenceEqual(new[] { 9, 7, 9 }), "离散字地址读取错误。");
            Check(!plc.ReadInt16("INVALID100").IsSuccess, "协议错误应向上传递。");
            var json = JsonConvert.SerializeObject(plc);
            var restored = JsonConvert.DeserializeObject<PlcKeyenceNano>(json);
            try
            {
                restored.CreateDevice();
                Check(restored.IsConnect, "恢复客户端时丢失已连接状态。");
                using (var single = new SinglePLC(restored))
                {
                    Check(restored.IsConnect && restored.ReadInt32("DM100").Content == 123456, "现有恢复流程无法重连。");
                    Solution.Instance.AllDevices.Remove(restored);
                    SinglePLC.SinglePLCs.Remove(single);
                }
            }
            finally { restored.Release(); }
            plc.Disconnect();
            Check(!plc.IsConnect && states.SequenceEqual(new[] { true, false }), "连接状态事件错误。");
            server.ServerClose();
            plc.OperationTimeoutMs = 100;
            Check(!plc.Connect() && !plc.IsConnect, "连接失败未同步设备状态。");
        }
        finally
        {
            plc.Release();
            server.ServerClose();
            server.Dispose();
        }
    }
}
