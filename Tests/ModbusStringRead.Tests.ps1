param([string]$BuildDirectory = 'bin\x64\Release', [string]$SolutionPath)
$ErrorActionPreference = 'Stop'

# 使用 .NET Framework 加载实际构建成品，通过代理设备验证接口调用和字符串订阅。
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot $BuildDirectory
$assemblyPath = Join-Path $outputDirectory '机器视觉AI检测系统V1.0.exe'
$jsonPath = Join-Path $outputDirectory 'Newtonsoft.Json.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw '请先编译指定目录的 x64 项目。' }

$testSource = @'
using System;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading;
using TDJS_Vision;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;

/// <summary>拦截真实 IModbus 接口的寄存器读取，避免测试访问现场设备。</summary>
public sealed class StringReadDeviceProxy : RealProxy
{
    /// <summary>下一次模拟返回的寄存器。</summary>
    public ushort[] Registers;
    /// <summary>记录读取调用次数。</summary>
    public int Calls;
    /// <summary>记录请求起始地址。</summary>
    public string Address;
    /// <summary>记录请求寄存器个数。</summary>
    public ushort Count;
    /// <summary>按生产接口构建设备代理。</summary>
    public StringReadDeviceProxy() : base(typeof(IModbus)) { }
    /// <summary>只允许测试访问寄存器读取方法。</summary>
    public override IMessage Invoke(IMessage message)
    {
        IMethodCallMessage call = (IMethodCallMessage)message;
        if (call.MethodName == "get_IsConnect")
            return new ReturnMessage(true, null, 0, call.LogicalCallContext, call);
        if (call.MethodName != "ReadUInt16")
            return new ReturnMessage(new InvalidOperationException("出现非预期的设备操作。"), call);
        Calls++;
        Address = (string)call.Args[0];
        Count = (ushort)call.Args[1];
        return new ReturnMessage(Registers, null, 0, call.LogicalCallContext, call);
    }
}

/// <summary>验证实际程序集的字符串解码、参数序列化和订阅兼容性。</summary>
public static class ModbusStringReadChecks
{
    /// <summary>在独立测试进程中执行断言。</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Console.WriteLine(Run());
            if (args.Length > 0) VerifySolution(args[0]);
        }
        catch (Exception ex)
        {
            for (Exception error = ex; error != null; error = error.InnerException)
                Console.Error.WriteLine(error.GetType().FullName + ": " + error.Message);
            Environment.ExitCode = 1;
        }
    }
    /// <summary>读取方案数据验证所有节点参数及条码订阅，不创建或连接现场设备。</summary>
    private static void VerifySolution(string path)
    {
        JObject solution = JObject.Parse(File.ReadAllText(path));
        int nodes = 0, strings = 0, subscriptions = 0;
        foreach (JObject process in solution["ProcessInfos"])
        {
            var stringIds = new System.Collections.Generic.HashSet<int>();
            foreach (JObject nodeJson in process["NodeInfos"])
            {
                NodeConfig config = nodeJson.ToObject<NodeConfig>();
                Check(config.NodeParam != null, "节点参数还原失败：" + config.ID);
                nodes++;
                NodeParamModbusRead param = config.NodeParam as NodeParamModbusRead;
                if (param == null || param.DataType != RegistersType.String) continue;
                strings++;
                stringIds.Add(config.ID);
                Check(param.Count == 16 && param.StartAddress == "900" && param.StringEncodingName == "us-ascii" &&
                    param.StringLowByteFirst && param.OutputValueCount == 1, "旧字符串配置未完整还原。");
                StringReadDeviceProxy proxy = new StringReadDeviceProxy();
                byte[] bytes = new byte[32];
                Encoding.ASCII.GetBytes("BARCODE-123").CopyTo(bytes, 0);
                proxy.Registers = Pack(bytes, true);
                param.Device = (IModbus)proxy.GetTransparentProxy();
                using (NodeModbusRead node = new NodeModbusRead(config.ID, config.NodeName, null, NodeType.ModbusRead))
                {
                    node.Active = true;
                    node.ParamForm.Params = param;
                    node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    object value;
                    Check(((NodeResultModbusRead)node.Result).TryGetDynamicVariable("变量.值01(地址900)", out value) &&
                        (string)value == "BARCODE-123", "旧方案条码订阅无法读取完整字符串。");
                }
            }
            foreach (JObject nodeJson in process["NodeInfos"])
            {
                JToken param = ((JObject)nodeJson["NodeParam"]).Properties().Single().Value;
                string source = (string)param["BarCodeSubText1"];
                string variable = (string)param["BarCodeSubText2"];
                if (variable != "变量.值01(地址900)") continue;
                Check(stringIds.Any(id => source == id + ".Modbus读取"), "图像保存引用的字符串节点不存在。");
                subscriptions++;
            }
        }
        Check(nodes == 154 && strings == 4 && subscriptions == 8,
            "123方案节点或条码引用数量不匹配：" + nodes + "/" + strings + "/" + subscriptions);
        Console.WriteLine("123方案兼容检查通过：" + nodes + "个节点参数、" + strings + "个字符串节点、" + subscriptions + "个条码订阅。");
    }
    /// <summary>已完成断言数量。</summary>
    private static int assertions;
    /// <summary>断言条件成立。</summary>
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    /// <summary>断言操作以指定类型的错误拒绝无效数据。</summary>
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        assertions++;
        try { action(); }
        catch (T) { return; }
        throw new Exception(message);
    }
    /// <summary>把测试字节按指定顺序装入寄存器。</summary>
    private static ushort[] Pack(byte[] bytes, bool lowFirst)
    {
        ushort[] registers = new ushort[(bytes.Length + 1) / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            int shift = (i % 2 == 0) != lowFirst ? 8 : 0;
            registers[i / 2] |= (ushort)(bytes[i] << shift);
        }
        return registers;
    }
    /// <summary>执行全部回归检查。</summary>
    public static string Run()
    {
        assertions = 0;
        StringReadDeviceProxy proxy = new StringReadDeviceProxy();
        IModbus device = (IModbus)proxy.GetTransparentProxy();
        proxy.Registers = new ushort[] { 0x4142, 0x4331, 0x3233 };
        Check(device.ReadString("100", 3, "us-ascii", false) == "ABC123", "ASCII 字符串读取错误。");
        Check(proxy.Calls == 1 && proxy.Address == "100" && proxy.Count == 3, "必须只发起一次指定地址和长度的读取。");
        proxy.Registers = new ushort[] { 0x4241, 0x3143, 0x3332 };
        Check(device.ReadString("100", 3, "us-ascii", true) == "ABC123", "低字节在前解码错误。");
        proxy.Registers = new ushort[] { 0x4120, 0x4200, 0xffff };
        Check(device.ReadString("100", 3, "us-ascii", false) == "A B", "零终止符后填充应忽略。");
        proxy.Registers = new ushort[] { 0x4120 };
        Check(device.ReadString("100", 1, "us-ascii", false) == "A ", "有效尾部空格不能被擅自删除。");
        proxy.Registers = new ushort[] { 0 };
        Check(device.ReadString("100", 1, "us-ascii", false) == "", "全零寄存器应得到空字符串。");
        foreach (string encodingName in new[] { "utf-8", "gb18030", "utf-16" })
        {
            foreach (bool lowFirst in new[] { false, true })
            {
                proxy.Registers = Pack(Encoding.GetEncoding(encodingName).GetBytes("产品A1\0"), lowFirst);
                Check(device.ReadString("100", (ushort)proxy.Registers.Length, encodingName, lowFirst) == "产品A1",
                    "中文或 UTF-16 零终止符解码错误：" + encodingName);
            }
        }
        proxy.Registers = new ushort[] { 0xe4b8 };
        Reject<DecoderFallbackException>(() => device.ReadString("100", 1, "utf-8", false), "截断中文字符必须报错。");
        Reject<DecoderFallbackException>(() => device.ReadString("100", 1, "us-ascii", false), "ASCII 非法字节必须报错。");
        int previousCalls = proxy.Calls;
        Reject<ArgumentOutOfRangeException>(() => device.ReadString("100", 0, "us-ascii", false), "长度为零必须拒绝。");
        Reject<ArgumentOutOfRangeException>(() => device.ReadString("100", 126, "us-ascii", false), "超长读取必须拒绝。");
        Reject<ArgumentException>(() => device.ReadString("100", 1, "错误编码", false), "无效编码必须拒绝。");
        Check(proxy.Calls == previousCalls, "参数无效时不能访问设备。");
        proxy.Registers = new ushort[] { 0x4142 };
        Reject<InvalidOperationException>(() => device.ReadString("100", 2, "us-ascii", false), "寄存器返回不完整必须报错。");
        proxy.Registers = null;
        Reject<InvalidOperationException>(() => device.ReadString("100", 1, "us-ascii", false), "设备返回空结果必须报错。");
        proxy.Registers = Enumerable.Repeat((ushort)0x4142, 125).ToArray();
        Check(device.ReadString("100", 125, "us-ascii", false).Length == 250, "最大长度边界错误。");

        NodeParamModbusRead param = new NodeParamModbusRead { DataType = RegistersType.String, Count = 12,
            StartAddress = "100", DeviceName = "[未设置]", StringEncodingName = "utf-8", StringLowByteFirst = true };
        Check(param.OutputValueCount == 1, "字符串参数只能预声明一个订阅值。");
        string json = JsonConvert.SerializeObject(param);
        NodeParamModbusRead restored = JsonConvert.DeserializeObject<NodeParamModbusRead>(json);
        Check(restored.DataType == RegistersType.String && restored.Count == 12 &&
            restored.StringEncodingName == "utf-8" && restored.StringLowByteFirst, "字符串参数还原错误。");
        NodeParamModbusRead legacy = JsonConvert.DeserializeObject<NodeParamModbusRead>("{\"DataType\":\"Short\",\"Count\":3}");
        Check(legacy.OutputValueCount == 3 && legacy.StringEncodingName == "us-ascii" && !legacy.StringLowByteFirst,
            "旧方案默认值或数值订阅数量被改变。");
        Check((int)RegistersType.离散输入 == 10 && (int)RegistersType.String == 11, "已有类型序号不能改变。");

        NodeResultModbusRead result = new NodeResultModbusRead {
            ReadData = new ModbusReadResult(new[] { "ABC123" }, typeof(string[]).Name, "100") };
        string[] names = result.GetDynamicVariableNames().ToArray();
        Check(names.Length == 1 && result.ValueCount == 1, "运行结果必须只发布完整字符串。");
        object value;
        Type valueType;
        Check(result.TryGetDynamicVariable(names[0], out value) && (string)value == "ABC123", "订阅没有得到完整字符串。");
        Check(result.TryGetDynamicResultVariableType(names[0], out valueType) && valueType == typeof(string), "订阅必须声明 string 类型。");
        string reason;
        SubscriptionOutputDescriptor output = new SubscriptionOutputDescriptor {
            ValueType = valueType, Category = SubscriptionTypeCompatibility.ResolveCategory(valueType),
            Multiplicity = SubscriptionValueMultiplicity.Single };
        Check(SubscriptionInputContract.ForType(typeof(string)).Accepts(output, out reason), "保存图像的字符串契约必须接受读取值。");
        Check(!result.TryGetDynamicVariable("值02", out value), "字符串不能发布第二个虚假订阅值。");
        result.ReadData = new ModbusReadResult(new short[] { 7, 8 }, "Int16[]", "100");
        Check(result.TryGetDynamicVariable("值02", out value) && (short)value == 8, "原有数值订阅回归失败。");
        using (NodeModbusRead node = new NodeModbusRead(1, "Modbus读取", null, default(NodeType)))
        {
            ParamFormModbusRead form = (ParamFormModbusRead)node.ParamForm;
            form.Params = restored;
            form.SetParam2Form();
            ComboBox typeBox = (ComboBox)form.Controls.Find("comboBox2", true)[0];
            ComboBox encodingBox = (ComboBox)form.Controls.Find("comboBoxStringEncoding", true)[0];
            ComboBox orderBox = (ComboBox)form.Controls.Find("comboBoxStringByteOrder", true)[0];
            Check(typeBox.Text == "字符串" && encodingBox.SelectedIndex == 1 && orderBox.SelectedIndex == 1,
                "窗体没有还原字符串选项。");
            Check(encodingBox.Enabled && orderBox.Enabled, "字符串模式必须启用专用参数。");
            var candidates = SubscriptionPortCatalog.GetOutputs(node, SubscriptionInputContract.ForType(typeof(string)), false, null);
            Check(candidates.Count == 1 && candidates[0].ValueType == typeof(string), "未运行节点必须可以被保存图像订阅。");
            // 旧数值结果不能在切换字符串参数后产生额外候选项。
            ((NodeResultModbusRead)node.Result).ReadData = result.ReadData;
            Check(SubscriptionPortCatalog.GetOutputs(node, SubscriptionInputContract.ForType(typeof(string)), false, null).Count == 1,
                "切换类型后旧结果干扰字符串候选项。");
            restored.Device = device;
            node.Active = true;
            proxy.Registers = Pack(Encoding.UTF8.GetBytes("产品12345678\0"), true);
            restored.Count = (ushort)proxy.Registers.Length;
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            NodeResultModbusRead actual = (NodeResultModbusRead)node.Result;
            Check(actual.TryGetDynamicVariable("变量.值01(地址100)", out value) && (string)value == "产品12345678",
                "真实节点执行后必须得到完整字符串。");
            Check(actual.ReadData.DataType == "String[]" && actual.ValueCount == 1, "真实节点结果类型错误。");
            proxy.Registers = null;
            Reject<Exception>(() => node.Run(CancellationToken.None, false).GetAwaiter().GetResult(), "读取失败必须传播异常。");
            Check(actual.ReadData == null && !actual.TryGetDynamicVariable("值01", out value), "读取失败不得残留上次条码。");
            // 在屏幕外创建窗体句柄，用于验证真实设计器布局，不展示测试窗口。
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.Show();
            form.PerformLayout();
            using (Bitmap preview = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(preview, new Rectangle(0, 0, form.Width, form.Height));
                preview.Save(Environment.GetEnvironmentVariable("TDJS_MODBUS_PREVIEW"));
            }
            legacy.DeviceName = "[未设置]";
            legacy.StartAddress = "100";
            form.Params = legacy;
            form.SetParam2Form();
            Check(!encodingBox.Enabled && !orderBox.Enabled, "数值模式应禁用字符串参数。");
            Check(node.GetDynamicResultVariableNames().Count() == 3, "数值模式预声明数量必须保持不变。");
            form.Close();
        }
        return "Modbus 字符串读取检查通过，断言数：" + assertions;
    }
}
'@
$artifactDirectory = Join-Path $projectRoot 'artifacts\ModbusStringRead'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$env:TDJS_MODBUS_PREVIEW = Join-Path $artifactDirectory '界面预览.png'
$sourcePath = Join-Path $env:TEMP 'TDJS-ModbusStringReadChecks.cs'
$testExe = Join-Path $outputDirectory 'ModbusStringReadChecks.exe'
Copy-Item -LiteralPath ($assemblyPath + '.config') -Destination ($testExe + '.config') -Force
[System.IO.File]::WriteAllText($sourcePath, $testSource, [System.Text.UTF8Encoding]::new($true))
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
& $compiler /nologo /target:exe /platform:x64 "/out:$testExe" "/reference:$assemblyPath" "/reference:$jsonPath" /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $sourcePath
if ($LASTEXITCODE -ne 0) { throw '字符串测试程序编译失败。' }
& $testExe @($SolutionPath | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) { throw '字符串运行时测试失败。' }
