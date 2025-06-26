using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    // 定义DetResult结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct DetResult
    {
        public int ClassId;
        public float Score;
        public Rect Box;
    }
    // 定义 ObbResult 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct ObbResult
    {
        public RotatedRect RotateBox;
        public int ClassId;
        public float Score;
    }

    // 定义 PoseResult 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct PoseResult
    {
        public List<Rect> Boxs;
        public List<Point> KeyPoints;
    }

    // 定义 SegResult 结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct SegResult
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

    [StructLayout(LayoutKind.Sequential)]
    public struct RotatedRectResult
    {
        public float center_x;
        public float center_y;
        public float width;
        public float height;
        public float angle;
        public int class_id;
        public float confidence;
    }
}
