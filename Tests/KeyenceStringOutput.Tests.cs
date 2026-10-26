using System;
using System.Reflection;
using TDJS_Vision.Device;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;

/// <summary>验证基恩士首字段输出规则及其他品牌兼容性，不连接PLC。</summary>
internal static class KeyenceStringOutputTests
{
    /// <summary>检查真实节点使用的字符串规范化方法。</summary>
    private static void Verify(string input, DeviceBrand brand, string expected)
    {
        var method = typeof(NodePlcRead).GetMethod("NormalizeReadString", BindingFlags.Static | BindingFlags.NonPublic);
        var actual = (string)method.Invoke(null, new object[] { input, brand });
        if (actual != expected) throw new Exception("字符串输出不符合预期，品牌：" + brand);
    }

    /// <summary>覆盖首尾空白、正文分隔、空字符终止及空输入。</summary>
    private static void Main()
    {
        Verify("  ABC 123  ", DeviceBrand.Keyence, "ABC");
        Verify("ABC 123", DeviceBrand.Keyence, "ABC");
        Verify("  ABC123  ", DeviceBrand.Keyence, "ABC123");
        Verify("  ABC   123 XYZ  ", DeviceBrand.Keyence, "ABC");
        Verify("\tABC\t123\r\n", DeviceBrand.Keyence, "ABC");
        Verify("ABC\r\n123", DeviceBrand.Keyence, "ABC");
        Verify("ABC-123", DeviceBrand.Keyence, "ABC-123");
        Verify("   \t\r\n", DeviceBrand.Keyence, "");
        Verify("", DeviceBrand.Keyence, "");
        Verify(null, DeviceBrand.Keyence, null);
        Verify("  ABC 123  ", DeviceBrand.Melsec, "ABC 123");
        Verify("  ABC 123  ", DeviceBrand.Panasonic, "ABC 123");
        Verify("1.5DS-QEHBE\0E", DeviceBrand.Keyence, "1.5DS-QEHBE");
        Verify("1.5DS-QEHBE E", DeviceBrand.Keyence, "1.5DS-QEHBE");
        Verify("  00123\0OLD  ", DeviceBrand.Keyence, "00123");
        Verify("ABC\0\0", DeviceBrand.Keyence, "ABC");
        Verify("\0OLD", DeviceBrand.Keyence, "");
        Verify("\0", DeviceBrand.Keyence, "");
        Verify("ABC DEF\0OLD", DeviceBrand.Keyence, "ABC");
        Verify("ABC\0 DEF", DeviceBrand.Keyence, "ABC");
        Verify("ABC\0E", DeviceBrand.Melsec, "ABC\0E");
        Verify("ABC\0E", DeviceBrand.Panasonic, "ABC\0E");
        Console.WriteLine("基恩士字符串输出：22项检查通过。");
    }
}
