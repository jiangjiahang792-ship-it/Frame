using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    // 定义接口 IDetectResult
    internal interface IDetectResult { }

    // 定义DetResult结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct DetResult : IDetectResult
    {
        public int ClassId;
        public float Score;
        public Rect Box;
    }
    // 定义 ObbResult 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct ObbResult : IDetectResult
    {
        public RotatedRect RotateBox;
        public int ClassId;
        public float Score;
    }

    // 定义 PoseResult 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct PoseResult : IDetectResult
    {
        public int ClassId;    
        public float Score;
        public Rect Box;
        public List<Point> KeyPoints; 
    }

    // 定义 SegResult 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct SegResult : IDetectResult
    {
        public int ClassId;
        public float Score;
        public Rect Box;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RotatedRect
    {
        public Point2f center;
        public Size2f size;
        public float angle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point2f
    {
        public float x;
        public float y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Size2f
    {
        public float width;
        public float height;
    }
}
