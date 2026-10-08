using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node._3_Detection.TDAI;

/// <summary>以模拟通信设备验证真实程序集的读取、映射和配置兼容性。</summary>
internal static class TDAICommunicationReadTests
{
    /// <summary>已完成的断言数量。</summary>
    private static int _count;

    /// <summary>断言失败时立即终止测试。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _count++;
    }

    /// <summary>仅实现测试所需通信行为，避免连接真实设备。</summary>
    private sealed class DeviceProxy : RealProxy
    {
        /// <summary>预设寄存器响应。</summary>
        public ushort[] Registers = new ushort[] { 65535 };
        /// <summary>预设布尔响应。</summary>
        public bool[] Bits = new bool[] { true };
        /// <summary>最后调用的方法。</summary>
        public string Method;
        /// <summary>最后读取数量。</summary>
        public ushort Length;
        /// <summary>初始化接口代理。</summary>
        public DeviceProxy() : base(typeof(IModbus)) { }
        /// <summary>截获读取并返回预设数据。</summary>
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            Method = call.MethodName;
            if (call.ArgCount > 1) Length = (ushort)call.Args[1];
            object result = Method == "ReadUInt16" ? (object)Registers
                : Method == "ReadBool" ? (object)Bits : "模拟设备";
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        }
    }

    /// <summary>执行测试，无需启动主程序或连接设备。</summary>
    [STAThread]
    private static void Main()
    {
        try { Run(); }
        catch (Exception ex)
        {
            Console.WriteLine(ex.GetType().FullName);
            Console.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>将程序集加载和测试异常保留在可诊断的边界内。</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var proxy = new DeviceProxy();
        var device = (IModbus)proxy.GetTransparentProxy();
        var options = new TDAIReadOptions();
        Check(options.Read(device, "100") == "65535" && proxy.Length == 1, "整数读取失败");
        options.ReadType = TDAIReadType.Boolean;
        Check(options.Read(device, "100") == "true" && proxy.Method == "ReadBool", "布尔读取失败");
        Check(options.Matches("1", "true") && options.Matches("TRUE", "true"), "布尔真匹配失败");
        proxy.Bits[0] = false;
        Check(options.Read(device, "100") == "false" && options.Matches("0", "false"), "布尔假匹配失败");
        Check(!options.Matches("2", "false"), "非法布尔值不应匹配");
        options.ReadType = TDAIReadType.String;
        options.RegisterCount = 3;
        proxy.Registers = new ushort[] { 0x4142, 0x4300, 0x5859 };
        Check(options.Read(device, "100") == "ABC" && proxy.Length == 3, "字符串零终止失败");
        proxy.Registers = new ushort[] { 0x2041, 0x2042, 0x2000 };
        Check(options.Read(device, "100") == "A B", "首尾空格应删除，中间空格应保留");
        proxy.Registers = new ushort[] { 0x2020, 0x2020, 0x2000 };
        Check(options.Read(device, "100") == "", "全空格字符串应读取为空字符串");
        options.LowByteFirst = true;
        proxy.Registers = new ushort[] { 0x4241, 0x0043, 0 };
        Check(options.Read(device, "100") == "ABC", "低字节顺序失败");
        Check(options.Matches(" A ", " A ") && !options.Matches("A", "a"), "字符串精确匹配失败");
        options.EncodingName = "utf-16";
        proxy.Registers = new ushort[] { 0x4e2d, 0x6587, 0 };
        Check(options.Read(device, "100") == "中文", "中文解码失败");
        proxy.Registers = new ushort[0];
        bool rejected = false;
        try { options.Read(device, "100"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "不完整响应应报错");
        var oldNode = Newtonsoft.Json.JsonConvert.DeserializeObject<NodeParamTDAI>("{}");
        Check(oldNode.CommunicationReadOptions.ReadType == TDAIReadType.UInt16, "旧方案默认类型错误");
        string path = Path.GetTempFileName();
        try
        {
            var mappings = new BindingList<TDAICommuntionParam> { new TDAICommuntionParam { TriggerVal = "ABC", DetectionName = "检测项" } };
            JsonProjectSerializer.SaveProject(mappings, path);
            var legacy = TDAICommunicationFile.Load(path, "100");
            Check(legacy.Items.Count == 1 && legacy.ReadOptions.ReadType == TDAIReadType.UInt16, "带类型信息的旧配置兼容失败");
            File.WriteAllText(path, "[{\"TriggerVal\":\"1\",\"DetectionName\":\"检测项\"}]");
            Check(TDAICommunicationFile.Load(path, "100").Items.Count == 1, "旧数组兼容失败");
            JsonProjectSerializer.SaveProject(new TDAICommunicationFile { Address = "200", ReadOptions = options, Items = mappings }, path);
            var loaded = TDAICommunicationFile.Load(path, "100");
            Check(loaded.Address == "200" && loaded.ReadOptions.EncodingName == "utf-16" && loaded.ReadOptions.LowByteFirst, "新配置保存失败");
            using (var form = new TDAICommuntionDetectionConfig(device, mappings, "200", "", options))
            {
                Check(form.ReadOptions.ReadType == TDAIReadType.String && form.ReadOptions.RegisterCount == 3, "窗体回显失败");
                var type = (ComboBox)form.Controls.Find("comboBoxReadType", true)[0];
                var length = form.Controls.Find("numericRegisterCount", true)[0];
                Check(length.Enabled, "字符串长度未启用");
                type.SelectedIndex = 2;
                Check(!length.Enabled && form.ReadOptions.ReadType == TDAIReadType.Boolean, "类型切换失败");
            }
        }
        finally { File.Delete(path); }
        Console.WriteLine("通过断言数量：" + _count);
    }
}
