using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{

/// <summary>原生 DLL 的低层 P/Invoke 声明。</summary>
internal static class NativeMethods
{
    /// <summary>原生 DLL 的无扩展名文件名。</summary>
    private const string LibraryName = "ShapeMatchNative";

    /// <summary>查询或复制完整轮廓点及各点所属的有序路径。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_get_contours", ExactSpelling = true)]
    internal static extern int GetContours(ShapeMatcherSafeHandle handle, [Out] float[] xy,
        [Out] int[] pathIds, int pointCapacity, out int pointCount);

    /// <summary>读取模型实际 Canny 高阈值。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_get_model_contrast", ExactSpelling = true)]
    internal static extern int GetModelContrast(ShapeMatcherSafeHandle handle, out double contrast);

    /// <summary>查询或复制模型特征坐标。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_get_features", ExactSpelling = true)]
    internal static extern int GetFeatures(
        ShapeMatcherSafeHandle handle, [Out] float[] xy, int pointCapacity, out int pointCount);

    /// <summary>提交 ROI 局部删除遮罩。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_erase_features", ExactSpelling = true)]
    internal static extern int EraseFeatures(
        ShapeMatcherSafeHandle handle, byte[] mask, int width, int height, int stride);

    /// <summary>创建原生匹配器。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_create", ExactSpelling = true)]
    internal static extern IntPtr Create();

    /// <summary>销毁原生匹配器。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_destroy", ExactSpelling = true)]
    internal static extern void Destroy(IntPtr handle);

    /// <summary>清除原生模型。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_clear_model", ExactSpelling = true)]
    internal static extern void ClearModel(ShapeMatcherSafeHandle handle);

    /// <summary>查询原生模型状态。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_has_model", ExactSpelling = true)]
    internal static extern int HasModel(ShapeMatcherSafeHandle handle);

    /// <summary>查询 ABI 版本。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_get_abi_version", ExactSpelling = true)]
    internal static extern int GetAbiVersion();

    /// <summary>获取最近一次原生操作消息。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_get_last_message", ExactSpelling = true)]
    internal static extern IntPtr GetLastMessage(ShapeMatcherSafeHandle handle);

    /// <summary>调用原生建模函数。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_create_model", ExactSpelling = true)]
    internal static extern int CreateModel(
        ShapeMatcherSafeHandle handle,
        IntPtr imageData,
        int imageWidth,
        int imageHeight,
        int imageStride,
        int imageChannels,
        int roiX,
        int roiY,
        int roiWidth,
        int roiHeight,
        int numLevels,
        double angleStartDegrees,
        double angleEndDegrees,
        double angleStepDegrees,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string metric,
        double contrast,
        double minContrast,
        int featureCount,
        IntPtr maskData,
        int maskStride);

    /// <summary>调用原生搜索函数。</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sm_find", ExactSpelling = true)]
    internal static extern int Find(
        ShapeMatcherSafeHandle handle,
        IntPtr imageData,
        int imageWidth,
        int imageHeight,
        int imageStride,
        int imageChannels,
        double angleStartDegrees,
        double angleEndDegrees,
        double minScore,
        int maxMatches,
        double maxOverlap,
        int subPixel,
        int numLevels,
        int mode,
        IntPtr outputMatches,
        int outputCapacity,
        out int outputCount);
}

/// <summary>保证原生匹配器句柄只释放一次。</summary>
internal sealed class ShapeMatcherSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>包装已创建的原生句柄。</summary>
    internal ShapeMatcherSafeHandle(IntPtr nativeHandle)
        : base(true)
    {
        SetHandle(nativeHandle);
    }

    /// <summary>释放原生句柄。</summary>
    protected override bool ReleaseHandle()
    {
        NativeMethods.Destroy(handle);
        return true;
    }
}

/// <summary>与原生 SM_Match 保持二进制布局一致的结构。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeMatch
{
    /// <summary>中心横坐标。</summary>
    internal double CenterX;

    /// <summary>中心纵坐标。</summary>
    internal double CenterY;

    /// <summary>旋转角度。</summary>
    internal double AngleDegrees;

    /// <summary>匹配分数。</summary>
    internal double Score;

    /// <summary>模板宽度。</summary>
    internal double Width;

    /// <summary>模板高度。</summary>
    internal double Height;
}

}
