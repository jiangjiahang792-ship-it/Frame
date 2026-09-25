using System;
using System.Reflection;
using TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger;

/// <summary>PLC软触发配置和触发条件回归。</summary>
internal static class PlcSoftTriggerConfigurationTests
{
    /// <summary>已通过的断言数量。</summary>
    private static int _checks;

    /// <summary>验证业务行为，失败时输出场景。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++;
    }

    /// <summary>读取私有控件字段，确保设计器能看到新增界面。</summary>
    private static object Field(object owner, string name)
    {
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Check(field != null, "参数窗体缺少控件字段：" + name);
        return field.GetValue(owner);
    }

    /// <summary>验证方案参数会保存读取类型、对比值和字符串长度。</summary>
    private static void VerifyParamContract()
    {
        Type type = typeof(NodeParamWaitSoftTrigger);
        Check(type.GetProperty("DataType") != null, "PLC软触发参数缺少读取数据类型。");
        Check(type.GetProperty("CompareValue") != null, "PLC软触发参数缺少对比值。");
        Check(type.GetProperty("StringLength") != null, "PLC软触发参数缺少字符串读取长度。");
        var param = new NodeParamWaitSoftTrigger();
        Check((string)type.GetProperty("DataType").GetValue(param) == typeof(bool).Name, "旧方案默认数据类型必须兼容布尔。");
        Check((string)type.GetProperty("CompareValue").GetValue(param) == "True", "旧方案默认对比值必须兼容True触发。");
    }

    /// <summary>验证界面有读取类型、对比值和字符串长度控件。</summary>
    private static void VerifyFormContract()
    {
        using (var form = new ParamFormWaitSoftTrigger())
        {
            Field(form, "comboBoxDataType");
            Field(form, "textBoxCompareValue");
            Field(form, "textBoxStringLength");
        }
    }

    /// <summary>验证不同PLC读取类型按等值对比决定是否触发。</summary>
    private static void VerifyMatcher()
    {
        var type = typeof(NodeParamWaitSoftTrigger).Assembly.GetType("TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger.PlcSoftTriggerValueMatcher");
        Check(type != null, "缺少PLC软触发值对比工具。");
        var method = type.GetMethod("IsMatch", BindingFlags.Public | BindingFlags.Static);
        Check(method != null, "PLC软触发值对比工具缺少IsMatch方法。");
        Check((bool)method.Invoke(null, new object[] { true, typeof(bool).Name, "true" }), "布尔True对比失败。");
        Check(!(bool)method.Invoke(null, new object[] { false, typeof(bool).Name, "true" }), "布尔False误触发。");
        Check((bool)method.Invoke(null, new object[] { (short)7, typeof(short).Name, "7" }), "16位整数对比失败。");
        Check((bool)method.Invoke(null, new object[] { 123, typeof(int).Name, "123" }), "32位整数对比失败。");
        Check((bool)method.Invoke(null, new object[] { 5000000000L, typeof(long).Name, "5000000000" }), "64位整数对比失败。");
        Check((bool)method.Invoke(null, new object[] { 12.5f, typeof(float).Name, "12.5" }), "浮点对比失败。");
        Check((bool)method.Invoke(null, new object[] { "OK", typeof(string).Name, "OK" }), "字符串对比失败。");
        Check(!(bool)method.Invoke(null, new object[] { "NG", typeof(string).Name, "OK" }), "字符串误触发。");
    }

    /// <summary>执行离线回归。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyParamContract();
            VerifyFormContract();
            VerifyMatcher();
            Console.WriteLine("PLC软触发配置验证通过，共" + _checks + "项断言。");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
